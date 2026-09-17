using LegacyFighter.Cabs.Claims.Storage;
using LegacyFighter.Cabs.Entity;
using LegacyFighter.Cabs.Repository;
using LegacyFighter.Cabs.Service;
using Microsoft.EntityFrameworkCore;

namespace LegacyFighter.Cabs.Claims.Acl;

public class LegacyCabs
{
  private readonly SqLiteDbContext _context;
  private readonly IAwardsService _awards;
  private readonly IClientNotificationService _clientNotifications;
  private readonly IDriverNotificationService _driverNotifications;

  public LegacyCabs(
    SqLiteDbContext context,
    IAwardsService awards,
    IClientNotificationService clientNotifications,
    IDriverNotificationService driverNotifications)
  {
    _context = context;
    _awards = awards;
    _clientNotifications = clientNotifications;
    _driverNotifications = driverNotifications;
  }

  public async Task Apply(string claimNo, long claimantId, Resolution resolution)
  {
    Send(claimNo, resolution);
    if (resolution.SpecialMiles > 0)
    {
      await _awards.RegisterSpecialMiles(claimantId, resolution.SpecialMiles);
    }
  }

  public async Task<List<ClaimantRecord>> AllClaimants()
  {
    return await _context.Clients
      .Select(client => new ClaimantRecord
      {
        ClientId = client.Id!.Value,
        IsVip = client.Type == Client.Types.Vip,
        OrderedTransits = _context.Transits.Count(transit => transit.Client == client)
      })
      .ToListAsync();
  }

  public async Task<List<ClaimedTransitRecord>> AllClaimedTransits()
  {
    return await _context.Transits
      .Where(transit => transit.Client != null)
      .Select(transit => new ClaimedTransitRecord
      {
        TransitId = transit.Id!.Value,
        ClaimantId = transit.Client.Id!.Value,
        DriverId = transit.Driver == null ? null : transit.Driver.Id,
        Fare = transit.Price == null ? null : transit.Price.IntValue
      })
      .ToListAsync();
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
}
