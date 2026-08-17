using Diten.Platform.Application.Authorization;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TenantOrganization.Services;
using Diten.Platform.Application.Tests.TenantOrganization;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.Platform.Application.Tests.Authorization;

public sealed class DataScopeResolverRegistrationTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public void AddApplication_registers_real_data_scope_resolver_as_scoped()
    {
        var services = new ServiceCollection();

        services.AddApplication();

        var descriptor = services.FirstOrDefault(x => x.ServiceType == typeof(IDataScopeResolver));
        Assert.NotNull(descriptor);
        Assert.Equal(typeof(OrgDataScopeResolver), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddApplication_does_not_register_noop_resolver_as_production_default()
    {
        var services = new ServiceCollection();

        services.AddApplication();

        var resolverDescriptors = services.Where(x => x.ServiceType == typeof(IDataScopeResolver)).ToList();
        Assert.Single(resolverDescriptors);
        Assert.DoesNotContain(resolverDescriptors, d => d.ImplementationType == typeof(NoOpDataScopeResolver));
    }

    [Fact]
    public void AddApplication_resolves_real_instance_through_scope()
    {
        var provider = BuildProviderWithRepositoryStubs();

        using var scope = provider.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<IDataScopeResolver>();

        Assert.IsType<OrgDataScopeResolver>(resolver);
    }

    [Fact]
    public void Distinct_scopes_produce_distinct_resolver_instances()
    {
        var provider = BuildProviderWithRepositoryStubs();

        IDataScopeResolver first;
        IDataScopeResolver second;
        using (var scopeA = provider.CreateScope())
        {
            first = scopeA.ServiceProvider.GetRequiredService<IDataScopeResolver>();
        }

        using (var scopeB = provider.CreateScope())
        {
            second = scopeB.ServiceProvider.GetRequiredService<IDataScopeResolver>();
        }

        Assert.NotSame(first, second);
    }

    [Fact]
    public void Same_scope_returns_same_resolver_instance()
    {
        var provider = BuildProviderWithRepositoryStubs();

        using var scope = provider.CreateScope();
        var first = scope.ServiceProvider.GetRequiredService<IDataScopeResolver>();
        var second = scope.ServiceProvider.GetRequiredService<IDataScopeResolver>();

        Assert.Same(first, second);
    }

    // The org repositories are wired in the Infrastructure module in production; these registration tests only build
    // the Application module, so they supply lightweight in-memory repositories to let the resolver be constructed.
    private static ServiceProvider BuildProviderWithRepositoryStubs()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddScoped<IOrganizationUnitRepository>(_ => new InMemoryOrganizationUnitRepository(TenantId));
        services.AddScoped<IPositionRepository>(_ => new InMemoryPositionRepository(TenantId));
        services.AddScoped<IPositionAssignmentRepository>(_ => new InMemoryPositionAssignmentRepository(TenantId));
        services.AddScoped<ILegalEntityReferenceValidator, StubLegalEntityReferenceValidator>();
        return services.BuildServiceProvider();
    }

    private sealed class StubLegalEntityReferenceValidator : ILegalEntityReferenceValidator
    {
        public Task<Response<LegalEntityReferenceDto>> ValidateAsync(Guid legalEntityId, CancellationToken ct = default) =>
            Task.FromResult(Response<LegalEntityReferenceDto>.Fail("not referenceable", 404));
    }
}
