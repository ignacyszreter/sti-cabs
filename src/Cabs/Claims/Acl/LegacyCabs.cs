using LegacyFighter.Cabs.Dto;
using LegacyFighter.Cabs.Entity;
using LegacyFighter.Cabs.Repository;
using LegacyFighter.Cabs.Service;
using NodaTime;

namespace LegacyFighter.Cabs.Claims.Acl;

public class LegacyCabs
{
  private readonly IClaimRepository _claims;
  private readonly IClientRepository _clients;
  private readonly ITransitRepository _transits;
  private readonly ClaimNumberGenerator _claimNumbers;
  private readonly IClock _clock;

  public LegacyCabs(
    IClaimRepository claims,
    IClientRepository clients,
    ITransitRepository transits,
    ClaimNumberGenerator claimNumbers,
    IClock clock)
  {
    _claims = claims;
    _clients = clients;
    _transits = transits;
    _claimNumbers = claimNumbers;
    _clock = clock;
  }

  public async Task<ClaimDto> Register(NewClaim newClaim)
  {
    var owner = await _clients.Find(newClaim.ClientId)
                ?? throw new InvalidOperationException("Client does not exists");
    var transit = await _transits.Find(newClaim.TransitId)
                  ?? throw new InvalidOperationException("Transit does not exists");
    var claim = new Claim { CreationDate = _clock.GetCurrentInstant() };
    claim.ClaimNo = await _claimNumbers.Generate(claim);
    claim.Owner = owner;
    claim.Transit = transit;
    claim.Reason = newClaim.Reason;
    claim.IncidentDescription = newClaim.IncidentDescription;
    claim.Status = newClaim.IsDraft ? Claim.Statuses.Draft : Claim.Statuses.New;
    return new ClaimDto(await _claims.Save(claim));
  }

  public async Task<ClaimDto> View(long claimId)
  {
    return new ClaimDto(await Find(claimId));
  }

  private async Task<Claim> Find(long claimId)
  {
    return await _claims.Find(claimId) ?? throw new InvalidOperationException("Claim does not exists");
  }
}
