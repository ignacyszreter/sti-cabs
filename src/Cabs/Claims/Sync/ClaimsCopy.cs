using LegacyFighter.Cabs.Claims.Storage;
using Mediator;

namespace LegacyFighter.Cabs.Claims.Sync;

public class ClaimsCopy :
  INotificationHandler<ClientTypeChanged>,
  INotificationHandler<TransitOrdered>,
  INotificationHandler<TransitCompleted>
{
  private readonly ClaimsDbContext _claims;

  public ClaimsCopy(ClaimsDbContext claims)
  {
    _claims = claims;
  }

  public async ValueTask Handle(ClientTypeChanged notification, CancellationToken cancellationToken)
  {
    var claimant = await Claimant(notification.ClientId);
    claimant.IsVip = notification.IsVip;
    await _claims.SaveChangesAsync(cancellationToken);
  }

  public async ValueTask Handle(TransitOrdered notification, CancellationToken cancellationToken)
  {
    if (await _claims.ClaimedTransits.FindAsync(new object[] { notification.TransitId }, cancellationToken) != null)
    {
      return;
    }

    _claims.ClaimedTransits.Add(new ClaimedTransitRecord
    {
      TransitId = notification.TransitId,
      ClaimantId = notification.ClientId
    });
    var claimant = await Claimant(notification.ClientId);
    claimant.OrderedTransits++;
    await _claims.SaveChangesAsync(cancellationToken);
  }

  public async ValueTask Handle(TransitCompleted notification, CancellationToken cancellationToken)
  {
    var transit = await _claims.ClaimedTransits.FindAsync(new object[] { notification.TransitId }, cancellationToken);
    if (transit == null)
    {
      return;
    }

    transit.DriverId = notification.DriverId;
    transit.Fare = notification.Fare;
    await _claims.SaveChangesAsync(cancellationToken);
  }

  private async Task<ClaimantRecord> Claimant(long clientId)
  {
    var claimant = await _claims.Claimants.FindAsync(clientId);
    if (claimant == null)
    {
      claimant = new ClaimantRecord { ClientId = clientId };
      _claims.Claimants.Add(claimant);
    }

    return claimant;
  }
}
