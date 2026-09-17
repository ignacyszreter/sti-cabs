using System;
using System.Collections.Generic;
using System.Linq;
using LegacyFighter.Cabs.Service;
using LegacyFighter.Cabs.DriverPayments;
using LegacyFighter.Cabs.MoneyValue;
using Microsoft.Extensions.Logging;
using Microsoft.FeatureManagement;

namespace LegacyFighter.CabsTests.DriverPayments;

public class ReconciledDriverPaymentsCalculatorTest
{
  private const long DriverId = 1L;
  private const int Year = 2000;

  private IDriverPaymentsCalculator _primary = default!;
  private IDriverPaymentsCalculator _shadow = default!;
  private IFeatureManager _featureManager = default!;
  private CollectingLogger _logger = default!;
  private ReconciledDriverPaymentsCalculator _calculator = default!;

  [SetUp]
  public void SetUp()
  {
    _primary = Substitute.For<IDriverPaymentsCalculator>();
    _shadow = Substitute.For<IDriverPaymentsCalculator>();
    _featureManager = Substitute.For<IFeatureManager>();
    _logger = new CollectingLogger();
    _calculator = new ReconciledDriverPaymentsCalculator(_primary, _shadow, _featureManager, _logger);
  }

  [Test]
  public async Task DoesNotCalculateWithShadowWhenReconciliationIsSwitchedOff()
  {
    //given
    _primary.YearlyPayments(DriverId, Year).Returns(Payments(march: 100));
    _featureManager.IsEnabledAsync(ReconciledDriverPaymentsCalculator.DriverPaymentsReconciliation).Returns(false);

    //when
    var payments = await _calculator.YearlyPayments(DriverId, Year);

    //then
    payments[Month.March].Should().Be(new Money(100));
    _shadow.ReceivedCalls().Should().BeEmpty();
  }

  [Test]
  public async Task LogsEveryMonthThatDiffersAndReturnsPrimaryPayments()
  {
    //given
    _primary.YearlyPayments(DriverId, Year).Returns(Payments(march: 17));
    _shadow.YearlyPayments(DriverId, Year).Returns(Payments(march: 16));
    _featureManager.IsEnabledAsync(ReconciledDriverPaymentsCalculator.DriverPaymentsReconciliation).Returns(true);

    //when
    var payments = await _calculator.YearlyPayments(DriverId, Year);

    //then
    payments[Month.March].Should().Be(new Money(17));
    _logger.Warnings.Should().ContainSingle()
      .Which.Should().Contain("driver 1").And.Contain("2000-3").And.Contain("17").And.Contain("16");
  }

  [Test]
  public async Task LogsNothingWhenBothCalculationsAgree()
  {
    //given
    _primary.YearlyPayments(DriverId, Year).Returns(Payments(march: 17));
    _shadow.YearlyPayments(DriverId, Year).Returns(Payments(march: 17));
    _featureManager.IsEnabledAsync(ReconciledDriverPaymentsCalculator.DriverPaymentsReconciliation).Returns(true);

    //when
    await _calculator.YearlyPayments(DriverId, Year);

    //then
    _logger.Warnings.Should().BeEmpty();
  }

  [Test]
  public async Task ReturnsPrimaryPaymentsWhenShadowFails()
  {
    //given
    _primary.YearlyPayments(DriverId, Year).Returns(Payments(march: 17));
    _shadow.YearlyPayments(DriverId, Year)
      .Returns<Task<Dictionary<Month, Money>>>(_ => throw new InvalidOperationException("shadow is down"));
    _featureManager.IsEnabledAsync(ReconciledDriverPaymentsCalculator.DriverPaymentsReconciliation).Returns(true);

    //when
    var payments = await _calculator.YearlyPayments(DriverId, Year);

    //then
    payments[Month.March].Should().Be(new Money(17));
    _logger.Warnings.Should().ContainSingle();
  }

  private static Dictionary<Month, Money> Payments(int march)
  {
    var payments = Month.Values().ToDictionary(m => m, _ => Money.Zero);
    payments[Month.March] = new Money(march);
    return payments;
  }

  private class CollectingLogger : ILogger<ReconciledDriverPaymentsCalculator>
  {
    public List<string> Warnings { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
      Func<TState, Exception?, string> formatter)
    {
      if (logLevel == LogLevel.Warning)
      {
        Warnings.Add(formatter(state, exception));
      }
    }
  }
}
