using LegacyFighter.Cabs.Config;
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
  private readonly IAppProperties _properties;
  private readonly IAwardsService _awards;
  private readonly IClientNotificationService _clientNotifications;
  private readonly IDriverNotificationService _driverNotifications;
  private readonly IClock _clock;

  public LegacyCabs(
    IClaimRepository claims,
    IClientRepository clients,
    ITransitRepository transits,
    ClaimNumberGenerator claimNumbers,
    IAppProperties properties,
    IAwardsService awards,
    IClientNotificationService clientNotifications,
    IDriverNotificationService driverNotifications,
    IClock clock)
  {
    _claims = claims;
    _clients = clients;
    _transits = transits;
    _claimNumbers = claimNumbers;
    _properties = properties;
    _awards = awards;
    _clientNotifications = clientNotifications;
    _driverNotifications = driverNotifications;
    _clock = clock;
  }

  public ClaimPolicy Policy()
  {
    return new ClaimPolicy(
      _properties.AutomaticRefundForVipThreshold,
      _properties.NoOfTransitsForClaimAutomaticRefund);
  }

  public Task<ClaimToResolve> ClaimToResolve(long claimId)
  {
    throw new NotImplementedException();
  }

  public async Task<ClaimDto> Apply(long claimId, Resolution resolution)
  {
    var claim = await Find(claimId);
    var now = _clock.GetCurrentInstant();
    var refunded = resolution.Decision == Decision.Refunded;
    claim.Status = refunded ? Claim.Statuses.Refunded : Claim.Statuses.Escalated;
    claim.CompletionMode = refunded ? Claim.CompletionModes.Automatic : Claim.CompletionModes.Manual;
    claim.CompletionDate = now;
    claim.ChangeDate = now;
    Send(claim.ClaimNo, resolution);
    if (resolution.SpecialMiles > 0)
    {
      await _awards.RegisterSpecialMiles(claim.Owner.Id, resolution.SpecialMiles);
    }

    return new ClaimDto(claim);
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

  public async Task<ClaimDto> MarkInProcess(long claimId)
  {
    var claim = await Find(claimId);
    claim.Status = Claim.Statuses.InProcess;
    return new ClaimDto(claim);
  }

  public async Task<ClaimDto> View(long claimId)
  {
    return new ClaimDto(await Find(claimId));
  }

  private void Send(string claimNo, Resolution resolution)
  {
    switch (resolution.Ask)
    {
      case Ask.ClientAboutRefund:
        _clientNotifications.NotifyClientAboutRefund(claimNo, resolution.Recipient);
        break;
      case Ask.ClientForMoreInformation:
        _clientNotifications.AskForMoreInformation(claimNo, resolution.Recipient);
        break;
      case Ask.DriverForDetails:
        _driverNotifications.AskDriverForDetailsAboutClaim(claimNo, resolution.Recipient);
        break;
    }
  }

  private async Task<Claim> Find(long claimId)
  {
    return await _claims.Find(claimId) ?? throw new InvalidOperationException("Claim does not exists");
  }
}
