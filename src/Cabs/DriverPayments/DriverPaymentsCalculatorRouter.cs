using LegacyFighter.Cabs.Service;
using LegacyFighter.Cabs.MoneyValue;
using Microsoft.FeatureManagement;

namespace LegacyFighter.Cabs.DriverPayments;

public class DriverPaymentsCalculatorRouter : IDriverPaymentsCalculator
{
  public const string DriverPaymentsInCode = "DriverPaymentsInCode";

  private readonly IDriverPaymentsCalculator _storedProcedureCalculator;
  private readonly IDriverPaymentsCalculator _codeCalculator;
  private readonly IFeatureManager _featureManager;

  public DriverPaymentsCalculatorRouter(
    IDriverPaymentsCalculator storedProcedureCalculator,
    IDriverPaymentsCalculator codeCalculator,
    IFeatureManager featureManager)
  {
    _storedProcedureCalculator = storedProcedureCalculator;
    _codeCalculator = codeCalculator;
    _featureManager = featureManager;
  }

  public async Task<Dictionary<Month, Money>> YearlyPayments(long? driverId, int year)
  {
    if (await _featureManager.IsEnabledAsync(DriverPaymentsInCode))
    {
      return await _codeCalculator.YearlyPayments(driverId, year);
    }

    return await _storedProcedureCalculator.YearlyPayments(driverId, year);
  }
}
