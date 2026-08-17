using Diten.Platform.Common.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.Platform.Application.Tests.Authorization;

public sealed class TemporaryAccessFoundationTests
{
    [Fact]
    public void EntitlementDataScopeKind_preserves_original_foundation_values()
    {
        var values = Enum.GetValues<EntitlementDataScopeKind>();

        Assert.Contains(EntitlementDataScopeKind.Company, values);
        Assert.Contains(EntitlementDataScopeKind.Country, values);
        Assert.Contains(EntitlementDataScopeKind.Own, values);
        Assert.Contains(EntitlementDataScopeKind.Assigned, values);
        Assert.Contains(EntitlementDataScopeKind.ProcessRelatedRecord, values);
        Assert.Equal(0, (int)EntitlementDataScopeKind.Company);
        Assert.Equal(1, (int)EntitlementDataScopeKind.Country);
        Assert.Equal(2, (int)EntitlementDataScopeKind.Own);
        Assert.Equal(3, (int)EntitlementDataScopeKind.Assigned);
        Assert.Equal(4, (int)EntitlementDataScopeKind.ProcessRelatedRecord);
    }

    [Fact]
    public void TemporaryAccessGrant_keeps_required_contract_fields_and_allows_empty_scopes()
    {
        var expiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(15);

        var grant = new TemporaryAccessGrant(
            "process-123",
            "HR",
            featureCode: null,
            expiresAtUtc,
            Array.Empty<EntitlementDataScope>());

        Assert.Equal("process-123", grant.ProcessInstanceId);
        Assert.Equal("HR", grant.ModuleCode);
        Assert.Null(grant.FeatureCode);
        Assert.Equal(expiresAtUtc, grant.ExpiresAtUtc);
        Assert.NotNull(grant.DataScopes);
        Assert.Empty(grant.DataScopes);
    }

    [Fact]
    public void TemporaryAccessGrant_normalizes_null_scopes_to_empty_list()
    {
        var grant = new TemporaryAccessGrant(
            "process-123",
            "HR",
            "EmployeeRead",
            DateTimeOffset.UtcNow.AddMinutes(15),
            dataScopes: null);

        Assert.NotNull(grant.DataScopes);
        Assert.Empty(grant.DataScopes);
    }

    [Theory]
    [InlineData(null, "HR")]
    [InlineData("", "HR")]
    [InlineData("process-123", null)]
    [InlineData("process-123", "")]
    public void TemporaryAccessGrant_rejects_missing_required_identifiers(string? processInstanceId, string? moduleCode)
    {
        Assert.ThrowsAny<ArgumentException>(() => new TemporaryAccessGrant(
            processInstanceId!,
            moduleCode!,
            featureCode: null,
            DateTimeOffset.UtcNow.AddMinutes(15),
            Array.Empty<EntitlementDataScope>()));
    }

    [Fact]
    public async Task NoOpTemporaryAccessProvider_always_returns_empty_grants()
    {
        var provider = new NoOpTemporaryAccessProvider();

        var grants = await provider.GetActiveGrantsAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "HR",
            "EmployeeRead",
            CancellationToken.None);

        Assert.NotNull(grants);
        Assert.Empty(grants);
    }

    [Fact]
    public void AddApplication_registers_noop_temporary_access_provider_by_default()
    {
        var services = new ServiceCollection();

        services.AddApplication();

        var descriptor = services.FirstOrDefault(x => x.ServiceType == typeof(ITemporaryAccessProvider));
        Assert.NotNull(descriptor);
        Assert.Equal(typeof(NoOpTemporaryAccessProvider), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }
}
