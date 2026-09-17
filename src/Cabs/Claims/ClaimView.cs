using LegacyFighter.Cabs.Claims.Storage;
using NodaTime;

namespace LegacyFighter.Cabs.Claims;

public record ClaimView(
  long ClaimId,
  string ClaimNo,
  long ClientId,
  long TransitId,
  ClaimStatus Status,
  ClaimCompletionMode? CompletionMode,
  Instant CreatedAt,
  Instant? CompletedAt,
  string Reason,
  string IncidentDescription)
{
  public bool IsDraft => Status == ClaimStatus.Draft;

  public static ClaimView Of(ClaimRecord claim)
  {
    return new ClaimView(
      claim.ClaimId,
      claim.ClaimNo,
      claim.ClaimantId,
      claim.TransitId,
      claim.Status,
      claim.CompletionMode,
      claim.CreatedAt,
      claim.CompletedAt,
      claim.Reason,
      claim.IncidentDescription);
  }
}
