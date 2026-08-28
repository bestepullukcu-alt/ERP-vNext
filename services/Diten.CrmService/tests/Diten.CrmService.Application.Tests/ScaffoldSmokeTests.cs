using Diten.CrmService.Application;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.Account;
using Diten.CrmService.Infrastructure;
using Diten.CrmService.Persistence;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.CrmService.Application.Tests;

/// <summary>
/// Service wiring smoke tests for Diten.CrmService. Proves the container resolves the core seams and the
/// Account-foundation services (MOD-0149). Business behavior is covered by <see cref="AccountFoundationTests"/>.
/// </summary>
public sealed class ScaffoldSmokeTests
{
    private static ServiceProvider BuildProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mongo:ConnectionString"] = "mongodb://localhost:27017",
                ["Mongo:DatabaseName"] = "DitenERP_Test",
                ["Gateway:BaseUrl"] = "http://localhost:5000"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddApplication();
        services.AddInfrastructure(configuration);
        services.AddPersistence(configuration);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void DependencyInjection_Resolves_Core_And_Foundation_Services()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;

        Assert.NotNull(sp.GetService<IMediator>());
        Assert.NotNull(sp.GetService<ITenantContext>());
        Assert.NotNull(sp.GetService<IAccountCodeGenerator>());
        Assert.NotNull(sp.GetService<IReferenceDataValidator>());
        Assert.NotNull(sp.GetService<IAccountAuditPublisher>());
    }

    [Fact]
    public void TenantContext_Guard_Behaves()
    {
        var tenantContext = new TenantContext();
        Assert.False(tenantContext.HasTenant);
        Assert.Null(tenantContext.TenantId);

        var tenantId = Guid.NewGuid();
        tenantContext.SetTenant(tenantId);
        Assert.True(tenantContext.HasTenant);
        Assert.Equal(tenantId, tenantContext.TenantId);

        tenantContext.Clear();
        Assert.False(tenantContext.HasTenant);
        Assert.Null(tenantContext.TenantId);
    }

    [Fact]
    public void ResponseEnvelope_Success_And_Fail()
    {
        var ok = Response<string>.Success("value", 200);
        Assert.True(ok.IsSuccessful);
        Assert.Equal(200, ok.StatusCode);
        Assert.Equal("value", ok.Data);

        var fail = Response<string>.Fail("boom", 400);
        Assert.False(fail.IsSuccessful);
        Assert.Equal(400, fail.StatusCode);
        Assert.NotNull(fail.Errors);
        Assert.Contains("boom", fail.Errors!);
    }
}
