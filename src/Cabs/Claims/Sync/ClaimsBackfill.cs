using LegacyFighter.Cabs.Claims.Acl;
using LegacyFighter.Cabs.Claims.Storage;
using Microsoft.EntityFrameworkCore;

namespace LegacyFighter.Cabs.Claims.Sync;

public class ClaimsBackfill
{
  private readonly LegacyCabs _legacy;
  private readonly ClaimsDbContext _claims;

  public ClaimsBackfill(LegacyCabs legacy, ClaimsDbContext claims)
  {
    _legacy = legacy;
    _claims = claims;
  }

  public async Task Run()
  {
    var known = await _claims.Claimants.Select(claimant => claimant.ClientId).ToListAsync();
    var copiedTransits = await _claims.ClaimedTransits.Select(transit => transit.TransitId).ToListAsync();
    _claims.Claimants.AddRange(
      (await _legacy.AllClaimants()).Where(claimant => !known.Contains(claimant.ClientId)));
    _claims.ClaimedTransits.AddRange(
      (await _legacy.AllClaimedTransits()).Where(transit => !copiedTransits.Contains(transit.TransitId)));
    await _claims.SaveChangesAsync();
  }
}
