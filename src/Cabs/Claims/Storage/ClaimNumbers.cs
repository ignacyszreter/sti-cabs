using System.Globalization;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace LegacyFighter.Cabs.Claims.Storage;

public class ClaimNumbers
{
  private readonly ClaimsDbContext _claims;

  public ClaimNumbers(ClaimsDbContext claims)
  {
    _claims = claims;
  }

  public async Task<string> Next(Instant createdAt)
  {
    var count = await _claims.Claims.CountAsync();
    return count + "---" + createdAt.InZone(DateTimeZoneProviders.Bcl.GetSystemDefault())
      .ToString("dd/MM/yyyy", CultureInfo.CreateSpecificCulture("en-US"));
  }
}
