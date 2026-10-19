using Diten.Platform.API.Security;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Authorization;

/// <summary>
/// BL-521 FIX1 F8-7 — the partner scope must be REGISTERED by the running service, not only by the test hosts. Every
/// HTTP test host calls the production registration method (PlatformActorTenantScopeRegistration), so a registration
/// lost from that method is red there; this file pins the other half — that Program.cs calls it — and that the one method
/// gives the policies, the permission filter and the handlers the SAME per-request instance (one record read).
/// </summary>
public sealed class PartnerAdminScopeRegistrationTests
{
    [Fact]
    public void Program_registers_the_partner_scope_through_the_production_registration()
    {
        var program = File.ReadAllText(Path.Combine(RepositoryRoot(), "services", "Diten.Platform", "src", "Diten.Platform.API", "Program.cs"));

        Assert.Contains("PlatformActorTenantScopeRegistration.AddPlatformActorTenantScope(builder.Services);", program, StringComparison.Ordinal);
    }

    [Fact]
    public void The_production_registration_gives_one_scope_per_request_to_the_interface_and_the_concrete_type()
    {
        using var root = new ServiceCollection()
            .AddPlatformActorTenantScope()
            .AddSingleton(new Mock<IPlatformAdministratorRepository>().Object)
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var first = root.CreateScope();
        using var second = root.CreateScope();

        var byInterface = first.ServiceProvider.GetRequiredService<IPlatformActorTenantScope>();
        Assert.Same(byInterface, first.ServiceProvider.GetRequiredService<PlatformActorTenantScope>());
        Assert.NotSame(byInterface, second.ServiceProvider.GetRequiredService<IPlatformActorTenantScope>());
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("repository_root_not_found");
    }
}
