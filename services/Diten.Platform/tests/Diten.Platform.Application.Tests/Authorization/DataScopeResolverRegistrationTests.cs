using Diten.Platform.Common.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.Platform.Application.Tests.Authorization;

public sealed class DataScopeResolverRegistrationTests
{
    [Fact]
    public void AddApplication_registers_noop_data_scope_resolver_as_scoped()
    {
        var services = new ServiceCollection();

        services.AddApplication();

        var descriptor = services.FirstOrDefault(x => x.ServiceType == typeof(IDataScopeResolver));
        Assert.NotNull(descriptor);
        Assert.Equal(typeof(NoOpDataScopeResolver), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddApplication_resolves_noop_instance_through_scope()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<IDataScopeResolver>();

        Assert.IsType<NoOpDataScopeResolver>(resolver);
    }

    [Fact]
    public void Distinct_scopes_produce_distinct_resolver_instances()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        var provider = services.BuildServiceProvider();

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
        var services = new ServiceCollection();
        services.AddApplication();
        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var first = scope.ServiceProvider.GetRequiredService<IDataScopeResolver>();
        var second = scope.ServiceProvider.GetRequiredService<IDataScopeResolver>();

        Assert.Same(first, second);
    }
}
