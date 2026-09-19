namespace LegacyFighter.Cabs.Claims;

public record Claimant(long ClientId, bool IsVip, int OrderedTransits);

public record ClaimedTransit(long TransitId, int? Fare, long? DriverId);

public record ClaimToResolve(
  string ClaimNo,
  Claimant Claimant,
  ClaimedTransit Transit,
  int ClaimsByClaimant,
  int ClaimsOnThisTransit);

public record ClaimPolicy(int AutomaticRefundThreshold, int TransitsForAutomaticRefund);

public enum Decision
{
  Refunded,
  Escalated
}

public enum Ask
{
  Nobody,
  ClientAboutRefund,
  ClientForMoreInformation,
  DriverForDetails
}

public record Resolution(Decision Decision, Ask Ask, long? Recipient, int SpecialMiles);

public static class ClaimResolver
{
  public static Resolution Resolve(ClaimToResolve claim, ClaimPolicy policy)
  {
    throw new NotImplementedException();
  }
}
