using LegacyFighter.Cabs.Claims.Acl;
using LegacyFighter.Cabs.Claims.Storage;
using NodaTime;

namespace LegacyFighter.Cabs.Claims;

public class ClaimsFacade
{
  private readonly LegacyCabs _legacy;
  private readonly ClaimsDbContext _claims;
  private readonly IClock _clock;

  public ClaimsFacade(LegacyCabs legacy, ClaimsDbContext claims, IClock clock)
  {
    _legacy = legacy;
    _claims = claims;
    _clock = clock;
  }

  public async Task<object> Register(NewClaim newClaim)
  {
    var registered = await _legacy.Register(newClaim);
    _claims.Claims.Add(new ClaimRecord
    {
      ClaimId = registered.ClaimId!.Value,
      ClaimNo = registered.ClaimNo,
      ClaimantId = newClaim.ClientId,
      TransitId = newClaim.TransitId,
      Status = newClaim.IsDraft ? ClaimStatus.Draft : ClaimStatus.New,
      CreatedAt = _clock.GetCurrentInstant()
    });
    await _claims.SaveChangesAsync();
    return registered;
  }

  public async Task<object> Resolve(long claimId)
  {
    var claim = await _legacy.ClaimToResolve(claimId);
    var resolution = ClaimResolver.Resolve(claim, _legacy.Policy());
    var response = await _legacy.Apply(claimId, resolution);
    await Record(claimId, resolution.Decision == Decision.Refunded
      ? (ClaimStatus.Refunded, ClaimCompletionMode.Automatic)
      : (ClaimStatus.Escalated, ClaimCompletionMode.Manual));
    return response;
  }

  public async Task<object> MarkInProcess(long claimId)
  {
    var response = await _legacy.MarkInProcess(claimId);
    await Record(claimId, (ClaimStatus.InProcess, null));
    return response;
  }

  public async Task<object> View(long claimId)
  {
    return await _legacy.View(claimId);
  }

  private async Task Record(long claimId, (ClaimStatus Status, ClaimCompletionMode? Mode) outcome)
  {
    var record = await _claims.Claims.FindAsync(claimId);
    if (record == null)
    {
      return;
    }

    record.Status = outcome.Status;
    record.CompletionMode = outcome.Mode;
    await _claims.SaveChangesAsync();
  }
}
