using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using LegacyFighter.Cabs.Entity;
using LegacyFighter.CabsTests.Common;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using static VerifyNUnit.Verifier;

namespace LegacyFighter.CabsTests.Integration;

public class DriverSettlementIntegrationTestWithStub
{
  private CabsApp _app = default!;

  [SetUp]
  public void InitializeApp()
  {
    _app = CabsApp.CreateInstance(services =>
    {
      services.AddHttpClient("Nbp")
        .ConfigurePrimaryHttpMessageHandler(() => new CannedResponses(NbpRates));
      services.AddHttpClient("PublicHolidays")
        .ConfigurePrimaryHttpMessageHandler(() => new CannedResponses(NagerPublicHolidays));
    });
  }

  [TearDown]
  public async Task DisposeOfApp()
  {
    await _app.DisposeAsync();
  }

  [Test]
  public async Task SettlesTwoYears()
  {
    var driverId = await ADriverWithTransits(2024, 2025);

    var settlements = new
    {
      In2024 = await _app.DriverSettlement.Settle(driverId, 2024),
      In2025 = await _app.DriverSettlement.Settle(driverId, 2025)
    };

    await Verify(settlements)
      .DontScrubDateTimes()
      .AddExtraSettings(settings => settings.DefaultValueHandling = Argon.DefaultValueHandling.Include);
  }

  [Test]
  public async Task SettlesYearWithoutTransits()
  {
    var driverId = await ADriverWithTransits();

    var settlement = await _app.DriverSettlement.Settle(driverId, 2025);

    await Verify(settlement)
      .DontScrubDateTimes()
      .AddExtraSettings(settings => settings.DefaultValueHandling = Argon.DefaultValueHandling.Include);
  }

  private async Task<long> ADriverWithTransits(params int[] years)
  {
    _app.StartReuseRequestScope();
    var driver = await _app.Fixtures.ADriver();
    foreach (var year in years)
    {
      await _app.Fixtures.ATransit(driver, 60, new LocalDateTime(year, 10, 1, 6, 30));
      await _app.Fixtures.ATransit(driver, 70, new LocalDateTime(year, 10, 10, 2, 30));
      await _app.Fixtures.ATransit(driver, 80, new LocalDateTime(year, 10, 30, 6, 30));
      await _app.Fixtures.ATransit(driver, 60, new LocalDateTime(year, 11, 10, 1, 30));
      await _app.Fixtures.ATransit(driver, 30, new LocalDateTime(year, 11, 10, 1, 30));
      await _app.Fixtures.ATransit(driver, 15, new LocalDateTime(year, 12, 10, 2, 30));
    }
    await _app.Fixtures.DriverHasFee(driver, DriverFee.FeeTypes.Flat, 10);
    _app.EndReuseRequestScope();
    return driver.Id!.Value;
  }

  private static readonly Dictionary<string, string> NbpRates = new()
  {
    ["https://api.nbp.pl/api/exchangerates/rates/a/eur/2024-12-20/2024-12-31/?format=json"] =
      """
      {
        "table": "A",
        "currency": "euro",
        "code": "EUR",
        "rates": [
          { "no": "246/A/NBP/2024", "effectiveDate": "2024-12-20", "mid": 4.2594 },
          { "no": "247/A/NBP/2024", "effectiveDate": "2024-12-23", "mid": 4.2678 },
          { "no": "248/A/NBP/2024", "effectiveDate": "2024-12-24", "mid": 4.2715 },
          { "no": "249/A/NBP/2024", "effectiveDate": "2024-12-27", "mid": 4.2708 },
          { "no": "250/A/NBP/2024", "effectiveDate": "2024-12-30", "mid": 4.2730 }
        ]
      }
      """,
    ["https://api.nbp.pl/api/exchangerates/rates/a/eur/2025-12-20/2025-12-31/?format=json"] =
      """
      {
        "table": "A",
        "currency": "euro",
        "code": "EUR",
        "rates": [
          { "no": "246/A/NBP/2025", "effectiveDate": "2025-12-22", "mid": 4.2319 },
          { "no": "247/A/NBP/2025", "effectiveDate": "2025-12-23", "mid": 4.2296 },
          { "no": "248/A/NBP/2025", "effectiveDate": "2025-12-24", "mid": 4.2281 },
          { "no": "249/A/NBP/2025", "effectiveDate": "2025-12-29", "mid": 4.2274 },
          { "no": "250/A/NBP/2025", "effectiveDate": "2025-12-30", "mid": 4.2267 }
        ]
      }
      """
  };

  private static readonly Dictionary<string, string> NagerPublicHolidays = new()
  {
    ["https://date.nager.at/api/v3/PublicHolidays/2025/PL"] =
      """
      [
        { "date": "2025-01-01", "localName": "Nowy Rok", "name": "New Year's Day", "countryCode": "PL" },
        { "date": "2025-01-06", "localName": "Święto Trzech Króli", "name": "Epiphany", "countryCode": "PL" },
        { "date": "2025-04-20", "localName": "Wielkanoc", "name": "Easter Sunday", "countryCode": "PL" },
        { "date": "2025-04-21", "localName": "Poniedziałek Wielkanocny", "name": "Easter Monday", "countryCode": "PL" },
        { "date": "2025-05-01", "localName": "Święto Pracy", "name": "May Day", "countryCode": "PL" },
        { "date": "2025-05-03", "localName": "Święto Konstytucji Trzeciego Maja", "name": "Constitution Day", "countryCode": "PL" },
        { "date": "2025-06-08", "localName": "Zielone Świątki", "name": "Pentecost Sunday", "countryCode": "PL" },
        { "date": "2025-06-19", "localName": "Boże Ciało", "name": "Corpus Christi", "countryCode": "PL" },
        { "date": "2025-08-15", "localName": "Wniebowzięcie Najświętszej Maryi Panny", "name": "Assumption Day", "countryCode": "PL" },
        { "date": "2025-11-01", "localName": "Wszystkich Świętych", "name": "All Saints' Day", "countryCode": "PL" },
        { "date": "2025-11-11", "localName": "Narodowe Święto Niepodległości", "name": "Independence Day", "countryCode": "PL" },
        { "date": "2025-12-25", "localName": "Boże Narodzenie", "name": "Christmas Day", "countryCode": "PL" },
        { "date": "2025-12-26", "localName": "Drugi dzień Bożego Narodzenia", "name": "St. Stephen's Day", "countryCode": "PL" }
      ]
      """,
    ["https://date.nager.at/api/v3/PublicHolidays/2026/PL"] =
      """
      [
        { "date": "2026-01-01", "localName": "Nowy Rok", "name": "New Year's Day", "countryCode": "PL" },
        { "date": "2026-01-06", "localName": "Święto Trzech Króli", "name": "Epiphany", "countryCode": "PL" },
        { "date": "2026-04-05", "localName": "Wielkanoc", "name": "Easter Sunday", "countryCode": "PL" },
        { "date": "2026-04-06", "localName": "Poniedziałek Wielkanocny", "name": "Easter Monday", "countryCode": "PL" },
        { "date": "2026-05-01", "localName": "Święto Pracy", "name": "May Day", "countryCode": "PL" },
        { "date": "2026-05-03", "localName": "Święto Konstytucji Trzeciego Maja", "name": "Constitution Day", "countryCode": "PL" },
        { "date": "2026-05-24", "localName": "Zielone Świątki", "name": "Pentecost Sunday", "countryCode": "PL" },
        { "date": "2026-06-04", "localName": "Boże Ciało", "name": "Corpus Christi", "countryCode": "PL" },
        { "date": "2026-08-15", "localName": "Wniebowzięcie Najświętszej Maryi Panny", "name": "Assumption Day", "countryCode": "PL" },
        { "date": "2026-11-01", "localName": "Wszystkich Świętych", "name": "All Saints' Day", "countryCode": "PL" },
        { "date": "2026-11-11", "localName": "Narodowe Święto Niepodległości", "name": "Independence Day", "countryCode": "PL" },
        { "date": "2026-12-25", "localName": "Boże Narodzenie", "name": "Christmas Day", "countryCode": "PL" },
        { "date": "2026-12-26", "localName": "Drugi dzień Bożego Narodzenia", "name": "St. Stephen's Day", "countryCode": "PL" }
      ]
      """
  };

  private class CannedResponses : HttpMessageHandler
  {
    private readonly Dictionary<string, string> _byUrl;

    public CannedResponses(Dictionary<string, string> byUrl)
    {
      _byUrl = byUrl;
    }

    protected override Task<HttpResponseMessage> SendAsync(
      HttpRequestMessage request,
      CancellationToken cancellationToken)
    {
      var url = request.RequestUri!.ToString();
      if (!_byUrl.TryGetValue(url, out var json))
      {
        throw new InvalidOperationException($"No canned response for {url}");
      }

      return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
      {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
      });
    }
  }
}
