using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LegacyFighter.Cabs.Claims;
using LegacyFighter.Cabs.Claims.Storage;
using LegacyFighter.Cabs.DistanceValue;
using LegacyFighter.Cabs.Entity;
using LegacyFighter.Cabs.MoneyValue;
using LegacyFighter.Cabs.Repository;
using LegacyFighter.CabsTests.Common;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using NUnit.Framework;

namespace LegacyFighter.CabsTests.Claims;

public class ClaimsMigrationTest
{
  private static readonly Instant When = Instant.FromUtc(2019, 1, 7, 12, 0);

  private CabsApp _app = default!;
  private HttpClient _http = default!;

  [SetUp]
  public void Start()
  {
    _app = CabsApp.CreateInstance();
    _http = _app.CreateClient();
  }

  [TearDown]
  public void TearDown()
  {
    _http.Dispose();
    _app.Dispose();
  }

  [Test]
  public async Task MovesAnOldClaimIntoTheModuleDatabase()
  {
    var claimId = await AnOldClaim();

    Assert.That(await Migrate(), Is.EqualTo(1));

    var view = await _http.GetFromJsonAsync<ClaimView>($"/bubble/claims/{claimId}");
    Assert.That(view!.ClaimId, Is.EqualTo(claimId));
    Assert.That(view.Status, Is.EqualTo(ClaimStatus.New));
    Assert.That(view.Reason, Is.EqualTo("za drogo"));
  }

  [Test]
  public async Task RunsAgainWithoutDuplicatingAnything()
  {
    await AnOldClaim();
    await Migrate();

    Assert.That(await Migrate(), Is.EqualTo(0));
    Assert.That(await CopiedClaims(), Is.EqualTo(1));
  }

  private async Task<int> Migrate()
  {
    using var scope = _app.Services.CreateScope();
    return await scope.ServiceProvider.GetRequiredService<ClaimsMigration>().Run();
  }

  private async Task<int> CopiedClaims()
  {
    using var scope = _app.Services.CreateScope();
    return scope.ServiceProvider.GetRequiredService<ClaimsDbContext>().Claims.Count();
  }

  private async Task<long> AnOldClaim()
  {
    var driverId = (await _app.Fixtures.ADriver()).Id!.Value;
    using var scope = _app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<SqLiteDbContext>();
    var addresses = scope.ServiceProvider.GetRequiredService<AddressRepository>();
    var driver = await scope.ServiceProvider.GetRequiredService<IDriverRepository>().Find(driverId);
    var client = await scope.ServiceProvider.GetRequiredService<IClientRepository>()
      .Save(new Client { Type = Client.Types.Normal });
    var transit = new Transit(
      await addresses.Save(new Address("Polska", "Warszawa", "Mlynarska", 1)),
      await addresses.Save(new Address("Polska", "Warszawa", "Zytnia", 2)),
      client,
      null,
      When,
      Distance.Zero);
    transit.PublishAt(When);
    transit.ProposeTo(driver);
    transit.AcceptBy(driver, When);
    transit.Price = new Money(5100);
    await scope.ServiceProvider.GetRequiredService<ITransitRepository>().Save(transit);

    var claim = new Claim
    {
      ClaimNo = "1---07/01/2019",
      CreationDate = When,
      Reason = "za drogo",
      IncidentDescription = "za drogo",
      Status = Claim.Statuses.New
    };
    var entry = context.Claims.Add(claim);
    entry.Property("OwnerId").CurrentValue = client.Id!.Value;
    entry.Property("TransitId").CurrentValue = transit.Id!.Value;
    await context.SaveChangesAsync();
    return claim.Id!.Value;
  }
}
