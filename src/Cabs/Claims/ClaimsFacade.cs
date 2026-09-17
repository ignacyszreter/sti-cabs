using LegacyFighter.Cabs.Claims.Acl;
using LegacyFighter.Cabs.Claims.Storage;
using LegacyFighter.Cabs.Config;
using NodaTime;

namespace LegacyFighter.Cabs.Claims;

public class ClaimsFacade
{
  private readonly LegacyCabs _legacy;
  private readonly ClaimsDbContext _claims;
  private readonly IAppProperties _properties;
  private readonly IClock _clock;

  public ClaimsFacade(LegacyCabs legacy, ClaimsDbContext claims, IAppProperties properties, IClock clock)
  {
    _legacy = legacy;
    _claims = claims;
    _properties = properties;
    _clock = clock;
  }

  public async Task<ClaimView> Register(NewClaim newClaim)
  {
    var registered = await _legacy.Register(newClaim, _clock.GetCurrentInstant());
    _claims.Claims.Add(registered);
    await _claims.SaveChangesAsync();
    return ClaimView.Of(registered);
  }

  public async Task<ClaimView> Resolve(long claimId)
  {
    var now = _clock.GetCurrentInstant();
    var record = await Claim(claimId);
    var toResolve = await _legacy.ClaimToResolve(claimId);
    var resolution = ClaimResolver.Resolve(toResolve, Policy());
    await _legacy.Apply(claimId, record.ClaimNo, record.ClaimantId, resolution, now);
    var refunded = resolution.Decision == Decision.Refunded;
    record.Status = refunded ? ClaimStatus.Refunded : ClaimStatus.Escalated;
    record.CompletionMode = refunded ? ClaimCompletionMode.Automatic : ClaimCompletionMode.Manual;
    record.CompletedAt = now;
    await _claims.SaveChangesAsync();
    return ClaimView.Of(record);
  }

  public async Task<ClaimView> MarkInProcess(long claimId)
  {
    var record = await Claim(claimId);
    await _legacy.MarkInProcess(claimId);
    record.Status = ClaimStatus.InProcess;
    await _claims.SaveChangesAsync();
    return ClaimView.Of(record);
  }

  public async Task<ClaimView> View(long claimId)
  {
    return ClaimView.Of(await _legacy.View(claimId));
  }

  private ClaimPolicy Policy()
  {
    return new ClaimPolicy(
      _properties.AutomaticRefundForVipThreshold,
      _properties.NoOfTransitsForClaimAutomaticRefund);
  }

  private async Task<ClaimRecord> Claim(long claimId)
  {
    return await _claims.Claims.FindAsync(claimId)
           ?? throw new InvalidOperationException("Claim does not exists");
  }
}
