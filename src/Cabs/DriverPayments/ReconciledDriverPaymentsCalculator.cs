using LegacyFighter.Cabs.Service;
using LegacyFighter.Cabs.MoneyValue;
using Microsoft.FeatureManagement;

namespace LegacyFighter.Cabs.DriverPayments;

public class ReconciledDriverPaymentsCalculator : IDriverPaymentsCalculator
{
  public const string DriverPaymentsReconciliation = "DriverPaymentsReconciliation";

  private readonly IDriverPaymentsCalculator _primary;
  private readonly IDriverPaymentsCalculator _shadow;
  private readonly IFeatureManager _featureManager;
  private readonly ILogger<ReconciledDriverPaymentsCalculator> _logger;

  public ReconciledDriverPaymentsCalculator(
    IDriverPaymentsCalculator primary,
    IDriverPaymentsCalculator shadow,
    IFeatureManager featureManager,
    ILogger<ReconciledDriverPaymentsCalculator> logger)
  {
    _primary = primary;
    _shadow = shadow;
    _featureManager = featureManager;
    _logger = logger;
  }

  public async Task<Dictionary<Month, Money>> YearlyPayments(long? driverId, int year)
  {
    var payments = await _primary.YearlyPayments(driverId, year);
    if (await _featureManager.IsEnabledAsync(DriverPaymentsReconciliation))
    {
      await Reconcile(driverId, year, payments);
    }

    return payments;
  }

  private async Task Reconcile(long? driverId, int year, Dictionary<Month, Money> payments)
  {
    try
    {
      var shadowPayments = await _shadow.YearlyPayments(driverId, year);
      foreach (var month in Month.Values().Where(m => payments[m] != shadowPayments[m]))
      {
        _logger.LogWarning(
          "Driver payments differ: driver {DriverId}, {Year}-{Month}, {Primary} {PrimaryPayment}, {Shadow} {ShadowPayment}",
          driverId, year, month.Value,
          _primary.GetType().Name, payments[month].IntValue,
          _shadow.GetType().Name, shadowPayments[month].IntValue);
      }
    }
    catch (Exception e)
    {
      _logger.LogWarning(e, "Driver payments reconciliation failed: driver {DriverId}, {Year}", driverId, year);
    }
  }
}
