using System.Collections.Generic;
using LegacyFighter.Cabs.Service;
using LegacyFighter.Cabs.DriverPayments;
using LegacyFighter.Cabs.MoneyValue;
using Microsoft.FeatureManagement;

namespace LegacyFighter.CabsTests.DriverPayments;

public class DriverPaymentsCalculatorRouterTest
{
  private const long DriverId = 1L;
  private const int Year = 2000;

  private IDriverPaymentsCalculator _storedProcedure = default!;
  private IDriverPaymentsCalculator _code = default!;
  private IFeatureManager _featureManager = default!;
  private DriverPaymentsCalculatorRouter _router = default!;

  [SetUp]
  public void SetUp()
  {
    _storedProcedure = Substitute.For<IDriverPaymentsCalculator>();
    _code = Substitute.For<IDriverPaymentsCalculator>();
    _featureManager = Substitute.For<IFeatureManager>();
    _storedProcedure.YearlyPayments(DriverId, Year).Returns(PaymentsInJanuary(100));
    _code.YearlyPayments(DriverId, Year).Returns(PaymentsInJanuary(200));
    _router = new DriverPaymentsCalculatorRouter(_storedProcedure, _code, _featureManager);
  }

  [Test]
  public async Task CalculatesWithStoredProcedureWhenCodeIsSwitchedOff()
  {
    //given
    _featureManager.IsEnabledAsync(DriverPaymentsCalculatorRouter.DriverPaymentsInCode).Returns(false);

    //when
    var payments = await _router.YearlyPayments(DriverId, Year);

    //then
    payments[Month.January].Should().Be(new Money(100));
    _code.ReceivedCalls().Should().BeEmpty();
  }

  [Test]
  public async Task CalculatesInCodeWhenCodeIsSwitchedOn()
  {
    //given
    _featureManager.IsEnabledAsync(DriverPaymentsCalculatorRouter.DriverPaymentsInCode).Returns(true);

    //when
    var payments = await _router.YearlyPayments(DriverId, Year);

    //then
    payments[Month.January].Should().Be(new Money(200));
    _storedProcedure.ReceivedCalls().Should().BeEmpty();
  }

  private static Dictionary<Month, Money> PaymentsInJanuary(int amount)
  {
    return new Dictionary<Month, Money> { [Month.January] = new Money(amount) };
  }
}
