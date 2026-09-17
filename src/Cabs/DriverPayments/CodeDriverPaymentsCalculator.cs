using LegacyFighter.Cabs.Entity;
using LegacyFighter.Cabs.MoneyValue;
using LegacyFighter.Cabs.Repository;
using LegacyFighter.Cabs.Service;
using NodaTime;

namespace LegacyFighter.Cabs.DriverPayments;

public class CodeDriverPaymentsCalculator : IDriverPaymentsCalculator
{
  private readonly IDriverRepository _driverRepository;
  private readonly IDriverFeeRepository _driverFeeRepository;
  private readonly ITransitRepository _transitRepository;

  public CodeDriverPaymentsCalculator(
    IDriverRepository driverRepository,
    IDriverFeeRepository driverFeeRepository,
    ITransitRepository transitRepository)
  {
    _driverRepository = driverRepository;
    _driverFeeRepository = driverFeeRepository;
    _transitRepository = transitRepository;
  }

  public async Task<Dictionary<Month, Money>> YearlyPayments(long? driverId, int year)
  {
    var driver = await _driverRepository.Find(driverId);
    if (driver == null)
    {
      throw new ArgumentException($"Driver does not exists, id = {driverId}");
    }

    var driverFee = await _driverFeeRepository.FindByDriver(driver);
    if (driverFee == null)
    {
      throw new ArgumentException($"driver Fees not defined for driver, driver id = {driverId}");
    }

    var from = StartOfYearInUtc(year);
    var to = StartOfYearInUtc(year + 1);
    var transits = await _transitRepository.FindAllByDriverAndDateTimeBetween(driver, from, to);

    var payments = Month.Values().ToDictionary(m => m, _ => Money.Zero);
    foreach (var transit in transits.Where(t => t.DateTime < to && t.Price != null))
    {
      var month = new Month(transit.DateTime!.Value.InUtc().Month);
      payments[month] += Fee(driverFee, transit.Price);
    }

    return payments;
  }

  private static Instant StartOfYearInUtc(int year)
  {
    return new LocalDate(year, 1, 1).AtStartOfDayInZone(DateTimeZone.Utc).ToInstant();
  }

  private static Money Fee(DriverFee driverFee, Money transitPrice)
  {
    var fee = driverFee.FeeType == DriverFee.FeeTypes.Flat
      ? transitPrice.IntValue - driverFee.Amount
      : (int)Math.Round(transitPrice.IntValue * driverFee.Amount / 100m, MidpointRounding.AwayFromZero);

    if (driverFee.Min != null && fee < driverFee.Min.IntValue)
    {
      return driverFee.Min;
    }

    return new Money(fee);
  }
}
