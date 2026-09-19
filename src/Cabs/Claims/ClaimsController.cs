using LegacyFighter.Cabs.Claims.Acl;
using LegacyFighter.Cabs.Common;
using Microsoft.AspNetCore.Mvc;

namespace LegacyFighter.Cabs.Claims;

[ApiController]
public class ClaimsController
{
  private readonly LegacyCabs _legacy;
  private readonly ITransactions _transactions;

  public ClaimsController(LegacyCabs legacy, ITransactions transactions)
  {
    _legacy = legacy;
    _transactions = transactions;
  }

  [HttpPost("/bubble/claims/send")]
  public async Task<object> Send([FromBody] NewClaim claim)
  {
    return await Register(claim with { IsDraft = false });
  }

  [HttpGet("/bubble/claims/{id}")]
  public async Task<object> Find(long id)
  {
    await using var tx = await _transactions.BeginTransaction();
    var claim = await _legacy.View(id);
    await tx.Commit();
    return claim;
  }

  private async Task<object> Register(NewClaim claim)
  {
    await using var tx = await _transactions.BeginTransaction();
    var registered = await _legacy.Register(claim);
    await tx.Commit();
    return registered;
  }
}
