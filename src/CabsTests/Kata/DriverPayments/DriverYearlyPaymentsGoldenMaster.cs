using System.Linq;
using LegacyFighter.Cabs.Entity;
using LegacyFighter.Cabs.Service;
using LegacyFighter.CabsTests.Common;
using NodaTime;
using static VerifyNUnit.Verifier;

namespace LegacyFighter.CabsTests.Kata.DriverPayments;

[Category("SqlServer")]
public class DriverYearlyPaymentsGoldenMaster
{
  private sealed record FeePlan(string Label, DriverFee.FeeTypes Type, int Amount, int? Min)
  {
    public override string ToString() => Label;
  }

  private static readonly FeePlan[] FeePlans =
  {
    new("flat 10, min 0      ", DriverFee.FeeTypes.Flat, 10, 0),
    new("flat 50, no min     ", DriverFee.FeeTypes.Flat, 50, null),
    new("flat 50, min 25     ", DriverFee.FeeTypes.Flat, 50, 25),
    new("percent 50, min 0   ", DriverFee.FeeTypes.Percentage, 50, 0),
    new("percent 33, no min  ", DriverFee.FeeTypes.Percentage, 33, null),
    new("percent 7, min 5    ", DriverFee.FeeTypes.Percentage, 7, 5),
  };

  private static readonly (int Price, LocalDateTime When)[] Transits =
  {
    (1000, new LocalDateTime(1999, 12, 31, 12, 0)),
    (81, new LocalDateTime(2000, 1, 15, 12, 0)),
    (15, new LocalDateTime(2000, 2, 10, 12, 0)),
    (150, new LocalDateTime(2000, 2, 20, 12, 0)),
    (50, new LocalDateTime(2000, 3, 5, 12, 0)),
    (45, new LocalDateTime(2000, 5, 1, 12, 0)),
    (1235, new LocalDateTime(2000, 7, 14, 12, 0)),
    (99, new LocalDateTime(2000, 11, 30, 12, 0)),
    (1000, new LocalDateTime(2001, 1, 1, 12, 0)),
  };

  private CabsApp _app = default!;

  [SetUp]
  public void Setup()
  {
    _app = CabsApp.CreateInstanceOnSqlServer(_ => { });
  }

  [TearDown]
  public void TearDown()
  {
    _app.Dispose();
  }

  [Test]
  public Task YearlyPayments()
  {
    return Combination().Verify(PaymentsIn2000, FeePlans);
  }

  private async Task<string> PaymentsIn2000(FeePlan plan)
  {
    var driverId = await ADriverWithTransits(plan);
    var payments = await _app.DriverService.CalculateDriverYearlyPayment(driverId, 2000);
    return string.Join(" | ", Month.Values().Select(m => $"{m.Value,2}: {payments[m].IntValue,5}"));
  }

  private async Task<long> ADriverWithTransits(FeePlan plan)
  {
    _app.StartReuseRequestScope();
    var driver = await _app.Fixtures.ADriver();
    foreach (var (price, when) in Transits)
    {
      await _app.Fixtures.ATransit(driver, price, when);
    }
    await _app.Fixtures.DriverHasFee(driver, plan.Type, plan.Amount, plan.Min);
    _app.EndReuseRequestScope();
    return driver.Id!.Value;
  }
}
