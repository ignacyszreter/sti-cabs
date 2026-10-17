using System;
using System.Linq;
using LegacyFighter.Cabs.Repository;
using LegacyFighter.Cabs.Tax;
using LegacyFighter.CabsTests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace LegacyFighter.CabsTests.Integration;

public class ParallelTaxRuleChangesOnPostgresIntegrationTest
{
  private const int ParallelRequests = 5;

  private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
    .WithImage("postgres:17-alpine")
    .Build();

  private CabsApp _app = default!;

  [OneTimeSetUp]
  public async Task StartPostgres()
  {
    await _postgres.StartAsync();
  }

  [OneTimeTearDown]
  public async Task StopPostgres()
  {
    await _postgres.DisposeAsync();
  }

  [SetUp]
  public void InitializeApp()
  {
    var connectionString = _postgres.GetConnectionString();
    _app = CabsApp.CreateInstance(services =>
    {
      services.RemoveAll<IDbContextOptionsConfiguration<SqLiteDbContext>>();
      services.AddDbContext<SqLiteDbContext>(options => options.UseNpgsql(connectionString));
    });
  }

  [TearDown]
  public async Task DisposeOfApp()
  {
    await _app.DisposeAsync();
  }

  [Test]
  public async Task ParallelDeletesFromFacadeKeepAtLeastOneRule()
  {
    //given
    var config = await TaxRuleService.CreateTaxConfigWithRule("Polska", ParallelRequests, TaxRule.LinearRule(8, 0, "VAT-8"));
    //and
    for (var i = 1; i < ParallelRequests; i++)
    {
      await TaxRuleService.AddTaxRuleToCountry("Polska", i, 100 * i, $"OPLATA-{i}");
    }
    //and
    var ruleIds = (await TaxRuleService.FindRules("Polska")).Select(r => r.Id).ToList();

    //when
    var failures = await Task.WhenAll(ruleIds.Select(ruleId => Task.Run(() => DeleteInOwnRequest(ruleId, config.Id))));

    //then
    failures.Where(f => f != null).Should().Contain(f => f is DbUpdateConcurrencyException);
    //and
    (await TaxRuleService.RulesCount("Polska")).Should().BeGreaterThanOrEqualTo(1);
  }

  private async Task<Exception?> DeleteInOwnRequest(long? ruleId, long? configId)
  {
    using var request = _app.NewConcurrentRequestScope();
    try
    {
      await request.ServiceProvider.GetRequiredService<ITaxRuleService>().DeleteRule(ruleId, configId);
      return null;
    }
    catch (Exception e)
    {
      return e;
    }
  }

  private ITaxRuleService TaxRuleService => _app.TaxRuleService;
}
