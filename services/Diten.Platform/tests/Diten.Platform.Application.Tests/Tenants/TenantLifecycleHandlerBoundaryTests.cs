using Diten.Platform.Application.Features.Tenants.Handlers;
using Diten.Platform.Domain.Entities;
using Xunit;

namespace Diten.Platform.Application.Tests.Tenants;

public sealed class TenantLifecycleHandlerBoundaryTests
{
    private static readonly Type[] HandlerTypes =
    [
        typeof(RegisterTenantCommandHandler),
        typeof(SuspendTenantCommandHandler),
        typeof(ReactivateTenantCommandHandler),
        typeof(DeleteTenantCommandHandler)
    ];

    [Fact]
    public void TenantLifecycleHandlers_DoNotReferenceDirectBrokerNotificationOrAuditTypes()
    {
        var forbiddenTerms = new[]
        {
            "RabbitMQ.Client",
            "MassTransit.IBus",
            "QueueEmailNotificationCommand",
            "INotificationService",
            "INotificationEventMapper",
            "IAuditService"
        };

        foreach (var handlerType in HandlerTypes)
        {
            var source = File.ReadAllText($"{GetRepoRoot()}\\services\\Diten.Platform\\src\\Diten.Platform.Application\\Features\\Tenants\\Handlers\\{handlerType.Name}.cs");

            foreach (var forbiddenTerm in forbiddenTerms)
            {
                Assert.DoesNotContain(forbiddenTerm, source, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void TenantDomainEntity_DoesNotPublishEventsDirectly()
    {
        var source = File.ReadAllText($"{GetRepoRoot()}\\services\\Diten.Platform\\src\\Diten.Platform.Domain\\Entities\\Tenant.cs");

        Assert.DoesNotContain("IEventBus", source, StringComparison.Ordinal);
        Assert.DoesNotContain("PublishAsync", source, StringComparison.Ordinal);
    }

    private static string GetRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repo root could not be located.");
    }
}
