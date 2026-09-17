using LegacyFighter.Cabs.Claims.Acl;

namespace LegacyFighter.Cabs.Claims.Storage;

public class ClaimsMigration
{
  private readonly LegacyCabs _legacy;
  private readonly ClaimsDbContext _claims;

  public ClaimsMigration(LegacyCabs legacy, ClaimsDbContext claims)
  {
    _legacy = legacy;
    _claims = claims;
  }

  public async Task<int> Run()
  {
    var copied = 0;
    foreach (var claimId in await _legacy.AllClaimIds())
    {
      if (await _claims.Claims.FindAsync(claimId) != null)
      {
        continue;
      }

      _claims.Claims.Add(await _legacy.ReadClaim(claimId));
      await _claims.SaveChangesAsync();
      copied++;
    }

    return copied;
  }
}
