using System.Collections;
using System.Reflection;
using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Contracts.Events;
using Xunit;

namespace Diten.Platform.Eventing.Tests;

public sealed class TenantLifecycleEventContractTests
{
    public static TheoryData<IIntegrationEvent, string, int> TenantLifecycleEvents =>
        new()
        {
            { new TenantCreatedV1(Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), Guid.NewGuid(), "Acme", "en", Guid.NewGuid()), TenantCreatedV1.Name, TenantCreatedV1.Version },
            { new TenantActivatedV1(Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid()), TenantActivatedV1.Name, TenantActivatedV1.Version },
            { new TenantSuspendedV1(Guid.NewGuid(), DateTimeOffset.UtcNow, "billing issue", Guid.NewGuid()), TenantSuspendedV1.Name, TenantSuspendedV1.Version },
            { new TenantReactivatedV1(Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid()), TenantReactivatedV1.Name, TenantReactivatedV1.Version },
            { new TenantCancelledV1(Guid.NewGuid(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "customer request", Guid.NewGuid()), TenantCancelledV1.Name, TenantCancelledV1.Version },
            { new TenantProvisioningCompletedV1(Guid.NewGuid(), DateTimeOffset.UtcNow, ["registry-created"]), TenantProvisioningCompletedV1.Name, TenantProvisioningCompletedV1.Version },
            { new TenantProvisioningFailedV1(Guid.NewGuid(), DateTimeOffset.UtcNow, "bootstrap-platform", "broker unavailable", 1), TenantProvisioningFailedV1.Name, TenantProvisioningFailedV1.Version }
        };

    public static TheoryData<Type> NewTenantLifecycleEventTypes =>
        new()
        {
            typeof(TenantCreatedV1),
            typeof(TenantSuspendedV1),
            typeof(TenantReactivatedV1),
            typeof(TenantCancelledV1),
            typeof(TenantProvisioningCompletedV1),
            typeof(TenantProvisioningFailedV1)
        };

    [Theory]
    [MemberData(nameof(TenantLifecycleEvents))]
    public void TenantLifecycleEvents_ExposeExpectedNameAndVersion(IIntegrationEvent @event, string expectedName, int expectedVersion)
    {
        Assert.True(EventName.IsValid(@event.EventName));
        Assert.Equal(expectedName, @event.EventName);
        Assert.Equal(expectedVersion, @event.EventVersion);
        EventName.EnsureMatchesVersion(@event.EventName, @event.EventVersion);
    }

    [Theory]
    [MemberData(nameof(NewTenantLifecycleEventTypes))]
    public void NewTenantLifecycleEvents_ImplementInternalEvent(Type eventType)
    {
        Assert.True(typeof(IInternalEvent).IsAssignableFrom(eventType));
    }

    [Theory]
    [MemberData(nameof(NewTenantLifecycleEventTypes))]
    public void NewTenantLifecycleEvents_AvoidForbiddenPayloadShapes(Type eventType)
    {
        foreach (var property in eventType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

            Assert.NotEqual(typeof(DateTime), propertyType);
            Assert.False(IsEntityType(propertyType), $"{eventType.Name}.{property.Name} must not include entity types.");
            Assert.NotEqual(typeof(byte[]), propertyType);
            Assert.NotEqual(typeof(Stream), propertyType);

            var normalizedName = property.Name.Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
            Assert.DoesNotContain("password", normalizedName);
            Assert.DoesNotContain("token", normalizedName);
            Assert.DoesNotContain("secret", normalizedName);
            Assert.DoesNotContain("credential", normalizedName);
            Assert.DoesNotContain("connectionstring", normalizedName);

            if (propertyType != typeof(string) && typeof(IEnumerable).IsAssignableFrom(propertyType))
            {
                Assert.True(
                    IsScalarStringCollection(propertyType),
                    $"{eventType.Name}.{property.Name} can only use a small scalar string collection.");
            }
        }
    }

    [Fact]
    public void TenantCreatedV1_UsesIdOnlyInitialAdminReference()
    {
        var properties = typeof(TenantCreatedV1)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(x => x.Name)
            .ToArray();

        Assert.Contains(nameof(TenantCreatedV1.InitialAdminUserId), properties);
        Assert.DoesNotContain("AdminEmail", properties);
    }

    [Fact]
    public void TenantCreatedV1_RejectsInvalidRequiredFields()
    {
        Assert.Throws<ArgumentException>(() => new TenantCreatedV1(Guid.Empty, DateTimeOffset.UtcNow, null, null, "Acme", "en", null));
        Assert.Throws<ArgumentException>(() => new TenantCreatedV1(Guid.NewGuid(), DateTimeOffset.UtcNow, null, null, "", "en", null));
        Assert.Throws<ArgumentException>(() => new TenantCreatedV1(Guid.NewGuid(), DateTimeOffset.UtcNow, null, null, "Acme", "", null));
    }

    [Fact]
    public void TenantSuspendedV1_RejectsInvalidReason()
    {
        Assert.Throws<ArgumentException>(() => new TenantSuspendedV1(Guid.NewGuid(), DateTimeOffset.UtcNow, "", null));
        Assert.Throws<ArgumentException>(() => new TenantSuspendedV1(Guid.NewGuid(), DateTimeOffset.UtcNow, new string('x', 501), null));
    }

    [Fact]
    public void TenantCancelledV1_RejectsEffectiveDateBeforeCancelledDate()
    {
        var cancelledAt = DateTimeOffset.UtcNow;

        Assert.Throws<ArgumentException>(() => new TenantCancelledV1(
            Guid.NewGuid(),
            cancelledAt,
            cancelledAt.AddSeconds(-1),
            null,
            null));
    }

    [Fact]
    public void TenantProvisioningCompletedV1_RequiresSteps()
    {
        Assert.Throws<ArgumentException>(() => new TenantProvisioningCompletedV1(Guid.NewGuid(), DateTimeOffset.UtcNow, []));
    }

    [Fact]
    public void TenantProvisioningFailedV1_RedactsSensitiveErrorAndRequiresAttempt()
    {
        var @event = new TenantProvisioningFailedV1(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "bootstrap-platform",
            "connectionString=mongodb://root:secret@localhost token=abc123 password=hunter2",
            1);

        Assert.DoesNotContain("mongodb://root", @event.Error, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("abc123", @event.Error, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hunter2", @event.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[REDACTED]", @event.Error);
        Assert.Throws<ArgumentException>(() => new TenantProvisioningFailedV1(Guid.NewGuid(), DateTimeOffset.UtcNow, "bootstrap-platform", "failed", 0));
    }

    private static bool IsScalarStringCollection(Type propertyType)
    {
        if (!typeof(IEnumerable).IsAssignableFrom(propertyType))
        {
            return false;
        }

        if (propertyType.IsGenericType)
        {
            return propertyType.GetGenericArguments().Length == 1
                   && propertyType.GetGenericArguments()[0] == typeof(string);
        }

        return propertyType.IsArray && propertyType.GetElementType() == typeof(string);
    }

    private static bool IsEntityType(Type type)
    {
        return type.Name is "BaseEntity" or "EntityBase" or "GlobalEntity"
               || type.BaseType is not null && IsEntityType(type.BaseType)
               || type.Namespace?.Contains(".Entities", StringComparison.OrdinalIgnoreCase) == true;
    }
}
