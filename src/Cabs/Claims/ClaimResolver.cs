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
    if (claim.ClaimsOnThisTransit > 1)
    {
      return new Resolution(Decision.Escalated, Ask.Nobody, null, 0);
    }

    if (claim.ClaimsByClaimant <= 3)
    {
      return Refund(claim);
    }

    if (claim.Claimant.IsVip)
    {
      return CheapEnough(claim, policy)
        ? Refund(claim) with { SpecialMiles = 10 }
        : new Resolution(Decision.Escalated, Ask.DriverForDetails, claim.Transit.DriverId, 0);
    }

    if (claim.Claimant.OrderedTransits >= policy.TransitsForAutomaticRefund)
    {
      return CheapEnough(claim, policy)
        ? Refund(claim)
        : new Resolution(Decision.Escalated, Ask.ClientForMoreInformation, claim.Claimant.ClientId, 0);
    }

    // Legacy asks the driver using the client id. Reproduced on purpose; fix it after the switch.
    return new Resolution(Decision.Escalated, Ask.DriverForDetails, claim.Claimant.ClientId, 0);
  }

  private static bool CheapEnough(ClaimToResolve claim, ClaimPolicy policy)
  {
    return claim.Transit.Fare < policy.AutomaticRefundThreshold;
  }

  private static Resolution Refund(ClaimToResolve claim)
  {
    return new Resolution(Decision.Refunded, Ask.ClientAboutRefund, claim.Claimant.ClientId, 0);
  }
}
