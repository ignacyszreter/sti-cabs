using LegacyFighter.Cabs.Common;
using Microsoft.AspNetCore.Mvc;

namespace LegacyFighter.Cabs.Claims;

[ApiController]
public class ClaimsController
{
  private readonly ClaimsFacade _claims;
  private readonly ITransactions _transactions;

  public ClaimsController(ClaimsFacade claims, ITransactions transactions)
  {
    _claims = claims;
    _transactions = transactions;
  }

  [HttpPost("/claims/createDraft")]
  public async Task<ClaimView> CreateDraft([FromBody] NewClaim claim)
  {
    return await Register(claim with { IsDraft = true });
  }

  [HttpPost("/claims/send")]
  public async Task<ClaimView> Send([FromBody] NewClaim claim)
  {
    return await Register(claim with { IsDraft = false });
  }

  [HttpPost("/claims/{id}/markInProcess")]
  public async Task<ClaimView> MarkInProcess(long id)
  {
    await using var tx = await _transactions.BeginTransaction();
    var claim = await _claims.MarkInProcess(id);
    await tx.Commit();
    return claim;
  }

  [HttpGet("/claims/{id}")]
  public async Task<ClaimView> Find(long id)
  {
    await using var tx = await _transactions.BeginTransaction();
    var claim = await _claims.View(id);
    await tx.Commit();
    return claim;
  }

  [HttpPost("/claims/{id}")]
  public async Task<ClaimView> Resolve(long id)
  {
    await using var tx = await _transactions.BeginTransaction();
    var claim = await _claims.Resolve(id);
    await tx.Commit();
    return claim;
  }

  private async Task<ClaimView> Register(NewClaim claim)
  {
    await using var tx = await _transactions.BeginTransaction();
    var registered = await _claims.Register(claim);
    await tx.Commit();
    return registered;
  }
}
