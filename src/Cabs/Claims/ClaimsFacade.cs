using LegacyFighter.Cabs.Claims.Acl;
using LegacyFighter.Cabs.Claims.Storage;
using LegacyFighter.Cabs.Config;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace LegacyFighter.Cabs.Claims;

public class ClaimsFacade
{
  private readonly LegacyCabs _legacy;
  private readonly ClaimsDbContext _claims;
  private readonly ClaimNumbers _numbers;
  private readonly ClaimsQueries _queries;
  private readonly IAppProperties _properties;
  private readonly IClock _clock;

  public ClaimsFacade(
    LegacyCabs legacy,
    ClaimsDbContext claims,
    ClaimNumbers numbers,
    ClaimsQueries queries,
    IAppProperties properties,
    IClock clock)
  {
    _legacy = legacy;
    _claims = claims;
    _numbers = numbers;
    _queries = queries;
    _properties = properties;
    _clock = clock;
  }

  public async Task<ClaimView> Register(NewClaim newClaim)
  {
    _ = await _claims.Claimants.FindAsync(newClaim.ClientId)
        ?? throw new InvalidOperationException("Client does not exists");
    _ = await _claims.ClaimedTransits.FindAsync(newClaim.TransitId)
        ?? throw new InvalidOperationException("Transit does not exists");
    var now = _clock.GetCurrentInstant();
    var record = new ClaimRecord
    {
      ClaimNo = await _numbers.Next(now),
      ClaimantId = newClaim.ClientId,
      TransitId = newClaim.TransitId,
      Status = newClaim.IsDraft ? ClaimStatus.Draft : ClaimStatus.New,
      CreatedAt = now,
      Reason = newClaim.Reason,
      IncidentDescription = newClaim.IncidentDescription
    };
    _claims.Claims.Add(record);
    await _claims.SaveChangesAsync();
    return ClaimView.Of(record);
  }

  public async Task<ClaimView> Resolve(long claimId)
  {
    var record = await Claim(claimId);
    var resolution = ClaimResolver.Resolve(await ToResolve(record), Policy());
    await _legacy.Apply(record.ClaimNo, record.ClaimantId, resolution);
    var refunded = resolution.Decision == Decision.Refunded;
    record.Status = refunded ? ClaimStatus.Refunded : ClaimStatus.Escalated;
    record.CompletionMode = refunded ? ClaimCompletionMode.Automatic : ClaimCompletionMode.Manual;
    record.CompletedAt = _clock.GetCurrentInstant();
    await _claims.SaveChangesAsync();
    return ClaimView.Of(record);
  }

  public async Task<ClaimView> MarkInProcess(long claimId)
  {
    var record = await Claim(claimId);
    record.Status = ClaimStatus.InProcess;
    await _claims.SaveChangesAsync();
    return ClaimView.Of(record);
  }

  public async Task<ClaimView> View(long claimId)
  {
    return ClaimView.Of(await Claim(claimId));
  }

  public async Task<ClaimView?> ClaimOf(long transitId)
  {
    return await _queries.ClaimOf(transitId);
  }

  private async Task<ClaimToResolve> ToResolve(ClaimRecord claim)
  {
    var claimant = await _claims.Claimants.FindAsync(claim.ClaimantId)
                   ?? throw new InvalidOperationException("Claimant does not exists");
    var transit = await _claims.ClaimedTransits.FindAsync(claim.TransitId)
                  ?? throw new InvalidOperationException("Claimed transit does not exists");
    return new ClaimToResolve(
      claim.ClaimNo,
      new Claimant(claimant.ClientId, claimant.IsVip, claimant.OrderedTransits),
      new ClaimedTransit(transit.TransitId, transit.Fare, transit.DriverId),
      await _claims.Claims.CountAsync(c => c.ClaimantId == claim.ClaimantId),
      await _claims.Claims.CountAsync(c => c.ClaimantId == claim.ClaimantId && c.TransitId == claim.TransitId));
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
