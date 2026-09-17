using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LegacyFighter.Cabs.Claims.Sync;
using LegacyFighter.Cabs.Config;
using LegacyFighter.Cabs.DistanceValue;
using LegacyFighter.Cabs.Entity;
using LegacyFighter.Cabs.MoneyValue;
using LegacyFighter.Cabs.Repository;
using LegacyFighter.CabsTests.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NodaTime;
using NUnit.Framework;

namespace LegacyFighter.CabsTests.Claims;

public class AutomaticClaimResolution
{
  private const int RefundThreshold = 10000;
  private const int TransitsForAutomaticRefund = 3;
  private const int CheapFare = RefundThreshold - 1;
  private const int ExpensiveFare = RefundThreshold;

  private static readonly Instant When = Instant.FromUtc(2019, 1, 7, 12, 0);

  private CabsApp _app = default!;
  private HttpClient _http = default!;
  private long _driverId;
  private int _addressNumber;

  [SetUp]
  public async Task Start()
  {
    // Cabs has no entry for the claim thresholds, so the test replaces the whole IAppProperties.
    _app = CabsApp.CreateInstance(services =>
    {
      services.RemoveAll<IAppProperties>();
      services.AddSingleton<IAppProperties>(new AppProperties
      {
        AutomaticRefundForVipThreshold = RefundThreshold,
        NoOfTransitsForClaimAutomaticRefund = TransitsForAutomaticRefund
      });
    });
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
  public async Task EscalatesASecondClaimOnTheSameTransit()
  {
    var claimant = await AClaimant(Client.Types.Normal);
    var transit = await ATransitFor(claimant, CheapFare);
    await SendClaim(claimant, transit);

    var outcome = await ResolveNewClaim(claimant, transit);

    Assert.That(outcome, Is.EqualTo(Escalated));
  }

  [Test]
  public async Task RefundsTheThirdClaimWhateverTheFare()
  {
    var claimant = await AClaimant(Client.Types.Normal);
    await ClaimsOnNewTransits(claimant, 2);
    var transit = await ATransitFor(claimant, ExpensiveFare);

    var outcome = await ResolveNewClaim(claimant, transit);

    Assert.That(outcome, Is.EqualTo(Refunded()));
  }

  [Test]
  public async Task RefundsAFrequentVipOnACheapTransitWithTenMiles()
  {
    var claimant = await AClaimant(Client.Types.Vip);
    await ClaimsOnNewTransits(claimant, 3);
    var transit = await ATransitFor(claimant, CheapFare);

    var outcome = await ResolveNewClaim(claimant, transit);

    Assert.That(outcome, Is.EqualTo(Refunded(miles: 10)));
  }

  [Test]
  public async Task EscalatesAFrequentVipOnAnExpensiveTransit()
  {
    var claimant = await AClaimant(Client.Types.Vip);
    await ClaimsOnNewTransits(claimant, 3);
    var transit = await ATransitFor(claimant, ExpensiveFare);

    var outcome = await ResolveNewClaim(claimant, transit);

    Assert.That(outcome, Is.EqualTo(Escalated));
  }

  [Test]
  public async Task RefundsAFrequentClaimantWithThreeOrderedTransitsOnACheapTransit()
  {
    var claimant = await AClaimant(Client.Types.Normal);
    var twiceClaimed = await ATransitFor(claimant, CheapFare);
    await SendClaims(claimant, twiceClaimed, 2);
    await ClaimsOnNewTransits(claimant, 1);
    var transit = await ATransitFor(claimant, CheapFare);

    var outcome = await ResolveNewClaim(claimant, transit);

    Assert.That(outcome, Is.EqualTo(Refunded()));
  }

  [Test]
  public async Task EscalatesAFrequentClaimantWithThreeOrderedTransitsOnAnExpensiveTransit()
  {
    var claimant = await AClaimant(Client.Types.Normal);
    var twiceClaimed = await ATransitFor(claimant, CheapFare);
    await SendClaims(claimant, twiceClaimed, 2);
    await ClaimsOnNewTransits(claimant, 1);
    var transit = await ATransitFor(claimant, ExpensiveFare);

    var outcome = await ResolveNewClaim(claimant, transit);

    Assert.That(outcome, Is.EqualTo(Escalated));
  }

  [Test]
  public async Task EscalatesAFrequentClaimantWithTwoOrderedTransits()
  {
    var claimant = await AClaimant(Client.Types.Normal);
    var other = await ATransitFor(claimant, CheapFare);
    await SendClaims(claimant, other, 3);
    var transit = await ATransitFor(claimant, CheapFare);

    var outcome = await ResolveNewClaim(claimant, transit);

    Assert.That(outcome, Is.EqualTo(Escalated));
  }

  private sealed record Outcome(Claim.Statuses? Status, Claim.CompletionModes? CompletionMode, int Miles);

  private static Outcome Refunded(int miles = 0) =>
    new(Claim.Statuses.Refunded, Claim.CompletionModes.Automatic, miles);

  private static readonly Outcome Escalated =
    new(Claim.Statuses.Escalated, Claim.CompletionModes.Manual, 0);

  private async Task<Outcome> ResolveNewClaim(long claimantId, long transitId)
  {
    var claim = await SendClaim(claimantId, transitId);
    var response = await Ok(await _http.PostAsync($"/claims/{claim.ClaimId}", null));
    var resolved = (await response.Content.ReadFromJsonAsync<ClaimView>())!;
    return new Outcome(resolved.Status, resolved.CompletionMode, await AwardsBalance(claimantId));
  }

  private async Task ClaimsOnNewTransits(long claimantId, int count)
  {
    for (var claim = 0; claim < count; claim++)
    {
      await SendClaim(claimantId, await ATransitFor(claimantId, CheapFare));
    }
  }

  private async Task SendClaims(long claimantId, long transitId, int count)
  {
    for (var claim = 0; claim < count; claim++)
    {
      await SendClaim(claimantId, transitId);
    }
  }

  // The fixtures write straight to the old tables, so they announce the facts the services would announce.
  private async Task<long> AClaimant(Client.Types type)
  {
    using var scope = _app.Services.CreateScope();
    var client = await scope.ServiceProvider.GetRequiredService<IClientRepository>()
      .Save(new Client { Type = type });
    var clientId = client.Id!.Value;
    await scope.ServiceProvider.GetRequiredService<IClaimsEvents>()
      .Publish(new ClientTypeChanged(clientId, type == Client.Types.Vip));
    await Ok(await _http.PostAsync($"/clients/{clientId}/awards", null));
    await Ok(await _http.PostAsync($"/clients/{clientId}/awards/activate", null));
    return clientId;
  }

  private async Task<long> ATransitFor(long clientId, int price)
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
    transit.Price = new Money(price);
    var transitId = (await transits.Save(transit)).Id!.Value;
    var events = scope.ServiceProvider.GetRequiredService<IClaimsEvents>();
    await events.Publish(new TransitOrdered(transitId, clientId));
    await events.Publish(new TransitCompleted(transitId, clientId, _driverId, price));
    return transitId;
  }

  private async Task<ClaimView> SendClaim(long clientId, long transitId)
  {
    var response = await Ok(await _http.PostAsJsonAsync(
      "/claims/send",
      new { clientId, transitId, reason = "za drogo", incidentDescription = "za drogo" }));
    return (await response.Content.ReadFromJsonAsync<ClaimView>())!;
  }

  private async Task<int> AwardsBalance(long clientId)
  {
    var response = await Ok(await _http.GetAsync($"/clients/{clientId}/awards/balance"));
    return await response.Content.ReadFromJsonAsync<int>();
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

  private sealed class ClaimView
  {
    public long ClaimId { get; set; }
    public Claim.Statuses? Status { get; set; }
    public Claim.CompletionModes? CompletionMode { get; set; }
  }
}
