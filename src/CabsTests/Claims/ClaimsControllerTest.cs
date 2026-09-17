using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LegacyFighter.Cabs.Claims.Sync;
using LegacyFighter.Cabs.DistanceValue;
using LegacyFighter.Cabs.Entity;
using LegacyFighter.Cabs.MoneyValue;
using LegacyFighter.Cabs.Repository;
using LegacyFighter.CabsTests.Common;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using NUnit.Framework;

namespace LegacyFighter.CabsTests.Claims;

public class ClaimsControllerTest
{
  private const string Claims = "/bubble/claims";
  private const long Unknown = 999_999;

  private static readonly Instant When = Instant.FromUtc(2019, 1, 7, 12, 0);

  private CabsApp _app = default!;
  private HttpClient _http = default!;
  private long _driverId;
  private int _addressNumber;

  [SetUp]
  public async Task Start()
  {
    _app = CabsApp.CreateInstance();
    _http = _app.CreateClient();
    _driverId = (await _app.Fixtures.ADriver()).Id!.Value;
  }

  [TearDown]
  public void TearDown()
  {
    _http.Dispose();
    _app.Dispose();
  }

  [Test]
  public async Task RegistersAClaimOnTheClientsTransit()
  {
    var client = await AClient();
    var transit = await ATransitFor(client);

    var claim = await Register(client, transit);

    Assert.That(claim, Is.EqualTo(new RegisteredClaim(
      claim.ClaimId, claim.ClaimNo, client, transit, "za drogo", "trasa dłuższa o 3 km", false)));
  }

  [Test]
  public async Task GivesEveryClaimItsOwnNumber()
  {
    var client = await AClient();

    var first = await Register(client, await ATransitFor(client));
    var second = await Register(client, await ATransitFor(client));

    Assert.That(second.ClaimNo, Is.Not.Empty.And.Not.EqualTo(first.ClaimNo));
  }

  [Test]
  public async Task ShowsARegisteredClaim()
  {
    var client = await AClient();
    var registered = await Register(client, await ATransitFor(client));

    var shown = await Show(registered.ClaimId);

    Assert.That(shown, Is.EqualTo(registered));
  }

  [Test]
  public async Task RejectsAClaimOfAnUnknownClient()
  {
    var client = await AClient();
    var transit = await ATransitFor(client);

    Assert.That(async () => await Register(Unknown, transit), Throws.InvalidOperationException.With.Message.Contains("Client does not exists"));
  }

  [Test]
  public async Task RejectsAClaimOnAnUnknownTransit()
  {
    var client = await AClient();

    Assert.That(async () => await Register(client, Unknown), Throws.InvalidOperationException.With.Message.Contains("Transit does not exists"));
  }

  [Test]
  public void RejectsShowingAnUnknownClaim()
  {
    Assert.That(async () => await Show(Unknown), Throws.InvalidOperationException.With.Message.Contains("Claim does not exists"));
  }

  private sealed record RegisteredClaim(
    long ClaimId,
    string ClaimNo,
    long ClientId,
    long TransitId,
    string Reason,
    string IncidentDescription,
    bool IsDraft);

  private async Task<RegisteredClaim> Register(long clientId, long transitId)
  {
    var response = await Ok(await _http.PostAsJsonAsync(
      $"{Claims}/send",
      new { clientId, transitId, reason = "za drogo", incidentDescription = "trasa dłuższa o 3 km" }));
    return (await response.Content.ReadFromJsonAsync<RegisteredClaim>())!;
  }

  private async Task<RegisteredClaim> Show(long claimId)
  {
    var response = await Ok(await _http.GetAsync($"{Claims}/{claimId}"));
    return (await response.Content.ReadFromJsonAsync<RegisteredClaim>())!;
  }

  // The fixtures write straight to the old tables, so they announce the facts the services would announce.
  private async Task<long> AClient()
  {
    using var scope = _app.Services.CreateScope();
    var client = await scope.ServiceProvider.GetRequiredService<IClientRepository>()
      .Save(new Client { Type = Client.Types.Normal });
    await scope.ServiceProvider.GetRequiredService<IClaimsEvents>()
      .Publish(new ClientTypeChanged(client.Id!.Value, false));
    return client.Id!.Value;
  }

  private async Task<long> ATransitFor(long clientId)
  {
    using var scope = _app.Services.CreateScope();
    var clients = scope.ServiceProvider.GetRequiredService<IClientRepository>();
    var transits = scope.ServiceProvider.GetRequiredService<ITransitRepository>();
    var addresses = scope.ServiceProvider.GetRequiredService<AddressRepository>();
    var driver = await scope.ServiceProvider.GetRequiredService<IDriverRepository>().Find(_driverId);
    var transit = new Transit(
      await addresses.Save(new Address("Polska", "Warszawa", "Mlynarska", ++_addressNumber)),
      await addresses.Save(new Address("Polska", "Warszawa", "Zytnia", ++_addressNumber)),
      await clients.Find(clientId),
      null,
      When,
      Distance.Zero);
    transit.PublishAt(When);
    transit.ProposeTo(driver);
    transit.AcceptBy(driver, When);
    transit.Price = new Money(5100);
    var transitId = (await transits.Save(transit)).Id!.Value;
    await scope.ServiceProvider.GetRequiredService<IClaimsEvents>()
      .Publish(new TransitOrdered(transitId, clientId));
    return transitId;
  }

  private static async Task<HttpResponseMessage> Ok(HttpResponseMessage response)
  {
    if (!response.IsSuccessStatusCode)
    {
      throw new InvalidOperationException(
        $"{(int)response.StatusCode} {response.RequestMessage?.RequestUri}: {await response.Content.ReadAsStringAsync()}");
    }

    return response;
  }
}
