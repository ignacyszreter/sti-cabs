using System;
using System.Collections.Generic;
using System.Linq;
using LegacyFighter.Cabs.DriverSettlements;
using LegacyFighter.Cabs.Entity;
using LegacyFighter.CabsTests.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NodaTime;

namespace LegacyFighter.CabsTests.DriverSettlements;

public class DriverSettlementServiceTest
{
  private FakeExchangeRates _exchangeRates = default!;
  private FakePublicHolidays _publicHolidays = default!;
  private CabsApp _app = default!;

  [SetUp]
  public void InitializeApp()
  {
    _exchangeRates = new FakeExchangeRates();
    _publicHolidays = new FakePublicHolidays();
    _app = CabsApp.CreateInstance(services =>
    {
      services.RemoveAll<IExchangeRates>();
      services.AddSingleton<IExchangeRates>(_exchangeRates);
      services.RemoveAll<IPublicHolidays>();
      services.AddSingleton<IPublicHolidays>(_publicHolidays);
    });
  }

  [TearDown]
  public async Task DisposeOfApp()
  {
    await _app.DisposeAsync();
  }

  [Test]
  public async Task ConvertsTotalToEuroWithRateFromEndOfYear()
  {
    //given
    var driverId = await ADriverWithTransitsIn(2025);
    _exchangeRates.EurRateAtEndOfYear(2025, 4.2267m);

    //when
    var settlement = await _app.DriverSettlement.Settle(driverId, 2025);

    //then
    settlement.Total.Should().Be(255);
    settlement.TotalInEur.Should().Be(60.33m);
  }

  [Test]
  public async Task PaysOnTenthOfJanuaryWhenItIsWorkingDay()
  {
    //given
    var driverId = await ADriverWithTransitsIn(2024);

    //when
    var settlement = await _app.DriverSettlement.Settle(driverId, 2024);

    //then
    settlement.PayoutDate.Should().Be(new DateOnly(2025, 1, 10));
  }

  [Test]
  public async Task MovesPayoutPastWeekend()
  {
    //given
    var driverId = await ADriverWithTransitsIn(2025);

    //when
    var settlement = await _app.DriverSettlement.Settle(driverId, 2025);

    //then
    settlement.PayoutDate.Should().Be(new DateOnly(2026, 1, 12));
  }

  [Test]
  public async Task MovesPayoutPastWeekendAndHolidaysThatFollowIt()
  {
    //given
    var driverId = await ADriverWithTransitsIn(2025);
    _publicHolidays.DaysOff(new DateOnly(2026, 1, 12), new DateOnly(2026, 1, 13));

    //when
    var settlement = await _app.DriverSettlement.Settle(driverId, 2025);

    //then
    settlement.PayoutDate.Should().Be(new DateOnly(2026, 1, 14));
  }

  [Test]
  public async Task SettlesYearWithoutPayments()
  {
    //given
    var driverId = await ADriverWithoutTransits();

    //when
    var settlement = await _app.DriverSettlement.Settle(driverId, 2025);

    //then
    settlement.Total.Should().Be(0);
    settlement.TotalInEur.Should().Be(0m);
  }

  private async Task<long> ADriverWithoutTransits()
  {
    return await ADriver(_ => Task.CompletedTask);
  }

  private async Task<long> ADriverWithTransitsIn(int year)
  {
    return await ADriver(async driver =>
    {
      await _app.Fixtures.ATransit(driver, 60, new LocalDateTime(year, 10, 1, 6, 30));
      await _app.Fixtures.ATransit(driver, 70, new LocalDateTime(year, 10, 10, 2, 30));
      await _app.Fixtures.ATransit(driver, 80, new LocalDateTime(year, 10, 30, 6, 30));
      await _app.Fixtures.ATransit(driver, 60, new LocalDateTime(year, 11, 10, 1, 30));
      await _app.Fixtures.ATransit(driver, 30, new LocalDateTime(year, 11, 10, 1, 30));
      await _app.Fixtures.ATransit(driver, 15, new LocalDateTime(year, 12, 10, 2, 30));
    });
  }

  private async Task<long> ADriver(Func<Driver, Task> withTransits)
  {
    _app.StartReuseRequestScope();
    var driver = await _app.Fixtures.ADriver();
    await withTransits(driver);
    await _app.Fixtures.DriverHasFee(driver, DriverFee.FeeTypes.Flat, 10);
    _app.EndReuseRequestScope();
    return driver.Id!.Value;
  }

  private class FakeExchangeRates : IExchangeRates
  {
    private readonly Dictionary<int, decimal> _rates = new();

    public void EurRateAtEndOfYear(int year, decimal rate) => _rates[year] = rate;

    public Task<decimal> EurRateAtEndOf(int year) =>
      Task.FromResult(_rates.TryGetValue(year, out var rate) ? rate : 4m);
  }

  private class FakePublicHolidays : IPublicHolidays
  {
    private readonly HashSet<DateOnly> _daysOff = new();

    public void DaysOff(params DateOnly[] days) => _daysOff.UnionWith(days);

    public Task<IReadOnlySet<DateOnly>> In(int year) =>
      Task.FromResult<IReadOnlySet<DateOnly>>(_daysOff.Where(d => d.Year == year).ToHashSet());
  }
}
