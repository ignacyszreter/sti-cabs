using Microsoft.EntityFrameworkCore;


namespace LegacyFighter.Cabs.Claims.Storage;

public class ClaimsQueries
{
  private readonly ClaimsDbContext _claims;

  public ClaimsQueries(ClaimsDbContext claims)
  {
    _claims = claims;
  }

  public async Task<ClaimView?> ClaimOf(long transitId)
  {
    var record = await _claims.Claims.FirstOrDefaultAsync(claim => claim.TransitId == transitId);
    return record == null ? null : ClaimView.Of(record);
  }

  public async Task<int> ClaimsBy(long claimantId)
  {
    return await _claims.Claims.CountAsync(claim => claim.ClaimantId == claimantId);
  }
}
