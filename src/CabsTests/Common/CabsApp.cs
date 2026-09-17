using System;
using LegacyFighter.Cabs.Controllers;
using LegacyFighter.Cabs.DriverSettlements;
using LegacyFighter.Cabs.Repository;
using LegacyFighter.Cabs.Service;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NodaTime;
using NodaTime.Testing;

namespace LegacyFighter.CabsTests.Common;

internal class CabsApp : WebApplicationFactory<Program>
{
  private readonly FakeClock _clock = new(SystemClock.Instance.GetCurrentInstant());
  private CabsApi? _api;
  private IServiceScope _scope;
  private readonly Action<IServiceCollection> _customization;
  
  /// <summary>
  /// https://stackoverflow.com/questions/66942392/unwanted-unique-constraint-in-many-to-many-relationship
  /// </summary>
  private bool _reuseScope = false;

  private CabsApp(Action<IServiceCollection> customization)
  {
    _customization = customization;
    _scope = base.Services.CreateAsyncScope();
  }

  public static CabsApp CreateInstance()
  {
    var cabsApp = new CabsApp(_ => { });
    return cabsApp;
  }

  public static CabsApp CreateInstance(Action<IServiceCollection> customization)
  {
    var cabsApp = new CabsApp(customization);
    return cabsApp;
  }

  protected override void ConfigureWebHost(IWebHostBuilder builder)
  {
    builder.ConfigureServices(collection => collection.AddTransient<Fixtures>());
    builder.ConfigureServices(collection =>
    {
      collection.RemoveAll<IClock>();
      collection.AddSingleton<IClock>(_clock);
    });
    builder.ConfigureServices(_customization);
  }

  public void StartReuseRequestScope()
  {
    _reuseScope = true;
  }

  public void EndReuseRequestScope()
  {
    _reuseScope = false;
  }

  protected override void Dispose(bool disposing)
  {
    _scope?.Dispose();
    base.Dispose(disposing);
  }

  private IServiceScope RequestScope()
  {
    if (!_reuseScope)
    {
      _scope.Dispose();
      _scope = Services.CreateAsyncScope();
    }
    return _scope;
  }

  public Fixtures Fixtures 
    => RequestScope().ServiceProvider.GetRequiredService<Fixtures>().WithApi(Api, _clock, Services);

  public IDriverFeeService DriverFeeService
    => RequestScope().ServiceProvider.GetRequiredService<IDriverFeeService>();

  public IDriverService DriverService
    => RequestScope().ServiceProvider.GetRequiredService<IDriverService>();

  public IDriverSettlement DriverSettlement
    => RequestScope().ServiceProvider.GetRequiredService<IDriverSettlement>();

  public ITransitService TransitService
    => RequestScope().ServiceProvider.GetRequiredService<ITransitService>();

  public IDriverSessionService DriverSessionService
    => RequestScope().ServiceProvider.GetRequiredService<IDriverSessionService>();

  public IDriverTrackingService DriverTrackingService
    => RequestScope().ServiceProvider.GetRequiredService<IDriverTrackingService>();

  public TransitController TransitController
    => RequestScope().ServiceProvider.GetRequiredService<TransitController>();

  public ICarTypeService CarTypeService
    => RequestScope().ServiceProvider.GetRequiredService<ICarTypeService>();

  public IClaimService ClaimService
    => RequestScope().ServiceProvider.GetRequiredService<IClaimService>();

  public IAwardsService AwardsService
    => RequestScope().ServiceProvider.GetRequiredService<IAwardsService>();

  public IAwardsAccountRepository AwardsAccountRepository
    => RequestScope().ServiceProvider.GetRequiredService<IAwardsAccountRepository>();

  public IContractService ContractService
    => RequestScope().ServiceProvider.GetRequiredService<IContractService>();

  public FakeClock Clock => _clock;

  public IClientService ClientService
    => RequestScope().ServiceProvider.GetRequiredService<IClientService>();

  public CabsApi Api => _api ??= new CabsApi(this);
}
