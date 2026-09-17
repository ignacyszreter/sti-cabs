using Mediator;

namespace LegacyFighter.Cabs.Claims.Sync;

public record ClientTypeChanged(long ClientId, bool IsVip) : INotification;

public record TransitOrdered(long TransitId, long ClientId) : INotification;

public record TransitCompleted(long TransitId, long ClientId, long DriverId, int Fare) : INotification;

public interface IClaimsEvents
{
  Task Publish(INotification @event);
}

public class MediatorClaimsEvents : IClaimsEvents
{
  private readonly IMediator _mediator;

  public MediatorClaimsEvents(IMediator mediator)
  {
    _mediator = mediator;
  }

  public async Task Publish(INotification @event)
  {
    await _mediator.Publish(@event);
  }
}
