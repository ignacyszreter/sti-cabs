namespace LegacyFighter.Cabs.Claims;

public record NewClaim(long ClientId, long TransitId, string Reason, string IncidentDescription, bool IsDraft);
