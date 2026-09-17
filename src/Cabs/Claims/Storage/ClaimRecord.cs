using NodaTime;

namespace LegacyFighter.Cabs.Claims.Storage;

public class ClaimRecord
{
  public long ClaimId { get; set; }
  public string ClaimNo { get; set; } = "";
  public long ClaimantId { get; set; }
  public long TransitId { get; set; }
  public ClaimStatus Status { get; set; }
  public ClaimCompletionMode? CompletionMode { get; set; }
  public Instant CreatedAt { get; set; }
}

public class ClaimantRecord
{
  public long ClientId { get; set; }
  public bool IsVip { get; set; }
  public int OrderedTransits { get; set; }
}

public class ClaimedTransitRecord
{
  public long TransitId { get; set; }
  public long ClaimantId { get; set; }
  public long? DriverId { get; set; }
  public int? Fare { get; set; }
}
