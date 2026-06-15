using Diten.Platform.Common.Authorization;
using Diten.Platform.Infrastructure;
using Diten.Platform.Infrastructure.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.Platform.Application.Tests.Authorization;

public sealed class TenantAuthorizationContextRegistrationTests
{
    [Fact]
    public void AddTenantAuthorizationContext_registers_jwt_context_as_scoped()
    {
        var services = new ServiceCollection();

        services.AddTenantAuthorizationContext();

        var descriptor = services.FirstOrDefault(x => x.ServiceType == typeof(ITenantAuthorizationContext));
        Assert.NotNull(descriptor);
        Assert.Equal(typeof(JwtTenantAuthorizationContext), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        Assert.DoesNotContain(
            services,
            x => x.ServiceType == typeof(ITenantAuthorizationContext)
                 && x.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddTenantAuthorizationContext_resolves_same_instance_inside_scope()
    {
        var services = CreateServices();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var first = scope.ServiceProvider.GetRequiredService<ITenantAuthorizationContext>();
        var second = scope.ServiceProvider.GetRequiredService<ITenantAuthorizationContext>();

        Assert.IsType<JwtTenantAuthorizationContext>(first);
        Assert.Same(first, second);
    }

    [Fact]
    public void AddTenantAuthorizationContext_resolves_distinct_instances_across_scopes()
    {
        var services = CreateServices();
        using var provider = services.BuildServiceProvider();

        ITenantAuthorizationContext first;
        ITenantAuthorizationContext second;
        using (var firstScope = provider.CreateScope())
        {
            first = firstScope.ServiceProvider.GetRequiredService<ITenantAuthorizationContext>();
        }

        using (var secondScope = provider.CreateScope())
        {
            second = secondScope.ServiceProvider.GetRequiredService<ITenantAuthorizationContext>();
        }

        Assert.NotSame(first, second);
    }

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddHttpContextAccessor();
        services.AddScoped<IDataScopeResolver, NoOpDataScopeResolver>();
        services.AddTenantAuthorizationContext();
        return services;
    }
}
