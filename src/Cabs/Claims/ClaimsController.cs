using Microsoft.AspNetCore.Mvc;

namespace LegacyFighter.Cabs.Claims;

[ApiController]
public class ClaimsController
{
  [HttpPost("/bubble/claims/send")]
  public Task<object> Send([FromBody] NewClaim claim)
  {
    throw new NotImplementedException();
  }

  [HttpGet("/bubble/claims/{id}")]
  public Task<object> Find(long id)
  {
    throw new NotImplementedException();
  }
}
