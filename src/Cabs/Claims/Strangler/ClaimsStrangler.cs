using LegacyFighter.Cabs.Claims.Storage;

namespace LegacyFighter.Cabs.Claims.Strangler;

public class StranglerFlags
{
  public bool RegisterClaims { get; set; }
  public bool ResolveClaimsOfRegularClients { get; set; }
  public bool ResolveClaimsOfVipClients { get; set; }
}

public static class ClaimsStrangler
{
  // Rewrites the path before routing, so the old controller never learns about the new module.
  public static IApplicationBuilder UseClaimsStrangler(this IApplicationBuilder app)
  {
    return app.Use(async (context, next) =>
    {
      if (await GoesToModule(context))
      {
        context.Request.Path = "/bubble" + context.Request.Path;
      }

      await next();
    });
  }

  private static async Task<bool> GoesToModule(HttpContext context)
  {
    if (!context.Request.Path.StartsWithSegments("/claims"))
    {
      return false;
    }

    var flags = context.RequestServices.GetRequiredService<StranglerFlags>();
    if (!ResolutionOfClaim(context, out var claimId))
    {
      return flags.RegisterClaims;
    }

    if (!flags.ResolveClaimsOfRegularClients && !flags.ResolveClaimsOfVipClients)
    {
      return false;
    }

    // A claim the module does not know was registered the old way, so only the old code can resolve it.
    var claims = context.RequestServices.GetRequiredService<ClaimsDbContext>();
    var claim = await claims.Claims.FindAsync(claimId);
    if (claim is null)
    {
      return false;
    }

    var claimant = await claims.Claimants.FindAsync(claim.ClaimantId);
    return claimant is { IsVip: true } ? flags.ResolveClaimsOfVipClients : flags.ResolveClaimsOfRegularClients;
  }

  private static bool ResolutionOfClaim(HttpContext context, out long claimId)
  {
    claimId = 0;
    return context.Request.Method == HttpMethods.Post
           && context.Request.Path.StartsWithSegments("/claims", out var rest)
           && long.TryParse(rest.Value?.TrimStart('/'), out claimId);
  }
}
