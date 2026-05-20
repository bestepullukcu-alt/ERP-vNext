using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application;
using Diten.Platform.Application.Features.Notifications.Eventing;
using Diten.Platform.Application.Features.Tenants.Notifications;
using Diten.Platform.Contracts.Events;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.Platform.Application.Tests.Tenants;

public sealed class TenantLifecycleNotificationMapperTests
{
    [Fact]
    public void TenantLifecycleNotificationMappers_AreRegistered()
    {
        var provider = new ServiceCollection()
            .AddApplication()
            .BuildServiceProvider();

        Assert.IsType<TenantCreatedV1NotificationMapper>(
            provider.GetRequiredService<INotificationEventMapper<TenantCreatedV1>>());
        Assert.IsType<TenantSuspendedV1NotificationMapper>(
            provider.GetRequiredService<INotificationEventMapper<TenantSuspendedV1>>());
        Assert.IsType<TenantReactivatedV1NotificationMapper>(
            provider.GetRequiredService<INotificationEventMapper<TenantReactivatedV1>>());
    }

    [Fact]
    public void TenantCreatedMapper_MapsResolvedInitialAdminRecipient()
    {
        var tenantId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var causationId = Guid.NewGuid();
        var initialAdminUserId = Guid.NewGuid();
        var envelope = CreateEnvelope(
            TenantCreatedV1.Name,
            new TenantCreatedV1(
                tenantId,
                DateTimeOffset.UtcNow,
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Tenant Alpha",
                "tr-TR",
                initialAdminUserId),
            tenantId,
            correlationId,
            causationId);

        var result = new TenantCreatedV1NotificationMapper().Map(
            envelope,
            [new("admin@example.com", "Tenant Admin")]);

        Assert.NotNull(result);
        Assert.Equal("tenant.invite.email", result!.TemplateKey);
        Assert.Equal("tr-TR", result.Locale);
        Assert.Equal(tenantId, result.Variables["TenantId"]);
        Assert.Equal("Tenant Alpha", result.Variables["TenantDisplayName"]);
        Assert.Equal(initialAdminUserId, result.Variables["InitialAdminUserId"]);
        Assert.Equal(causationId, result.CausationId);
        Assert.Single(result.To);
        Assert.Equal("admin@example.com", result.To[0].Email);
        Assert.Equal("Tenant Admin", result.To[0].DisplayName);
    }

    [Fact]
    public void TenantCreatedMapper_ReturnsControlledNull_WhenResolvedRecipientsAreMissing()
    {
        var tenantId = Guid.NewGuid();
        var envelope = CreateEnvelope(
            TenantCreatedV1.Name,
            new TenantCreatedV1(
                tenantId,
                DateTimeOffset.UtcNow,
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Tenant Alpha",
                "tr-TR",
                Guid.NewGuid()),
            tenantId,
            Guid.NewGuid());

        var result = new TenantCreatedV1NotificationMapper().Map(envelope, []);

        Assert.Null(result);
        Assert.Equal("tenant.invite.email", TenantCreatedV1NotificationMapper.TemplateKey);
        Assert.Contains("InitialAdminUserId", TenantCreatedV1NotificationMapper.MissingRecipientResolutionContractReason);
        Assert.DoesNotContain("AdminEmail", TenantCreatedV1NotificationMapper.MissingRecipientResolutionContractReason, StringComparison.Ordinal);
    }

    [Fact]
    public void TenantSuspendedMapper_MapsResolvedTenantAdminRecipients()
    {
        var tenantId = Guid.NewGuid();
        var suspendedAt = DateTimeOffset.UtcNow;
        var envelope = CreateEnvelope(
            TenantSuspendedV1.Name,
            new TenantSuspendedV1(tenantId, suspendedAt, "billing hold", Guid.NewGuid()),
            tenantId,
            Guid.NewGuid());

        var result = new TenantSuspendedV1NotificationMapper().Map(
            envelope,
            [new("owner@example.com", "Owner")],
            "tr-TR");

        Assert.NotNull(result);
        Assert.Equal("tenant.suspended.email", result!.TemplateKey);
        Assert.Equal("tr-TR", result.Locale);
        Assert.Equal(tenantId, result.Variables["TenantId"]);
        Assert.Equal("billing hold", result.Variables["Reason"]);
        Assert.Equal(suspendedAt, result.Variables["SuspendedAtUtc"]);
        Assert.Single(result.To);
        Assert.Equal("tenant.suspended.email", TenantSuspendedV1NotificationMapper.TemplateKey);
    }

    [Fact]
    public void TenantReactivatedMapper_MapsResolvedTenantAdminRecipients()
    {
        var tenantId = Guid.NewGuid();
        var reactivatedAt = DateTimeOffset.UtcNow;
        var envelope = CreateEnvelope(
            TenantReactivatedV1.Name,
            new TenantReactivatedV1(tenantId, reactivatedAt, Guid.NewGuid()),
            tenantId,
            Guid.NewGuid());

        var result = new TenantReactivatedV1NotificationMapper().Map(
            envelope,
            [new("owner@example.com", "Owner")],
            null);

        Assert.NotNull(result);
        Assert.Equal("tenant.reactivated.email", result!.TemplateKey);
        Assert.Equal("en-US", result.Locale);
        Assert.Equal(tenantId, result.Variables["TenantId"]);
        Assert.Equal(reactivatedAt, result.Variables["ReactivatedAtUtc"]);
        Assert.Single(result.To);
        Assert.Equal("tenant.reactivated.email", TenantReactivatedV1NotificationMapper.TemplateKey);
    }

    private static EventEnvelope<TEvent> CreateEnvelope<TEvent>(
        string eventName,
        TEvent payload,
        Guid tenantId,
        Guid correlationId,
        Guid? causationId = null)
        where TEvent : IIntegrationEvent
    {
        return new EventEnvelope<TEvent>(
            new EventMetadata(
                Guid.NewGuid(),
                eventName,
                1,
                correlationId,
                causationId ?? Guid.NewGuid(),
                tenantId,
                "Diten.Platform.Tests",
                DateTimeOffset.UtcNow),
            payload);
    }
}
