using Diten.MdmService.Application.Contracts.Audit;
using Xunit;

namespace Diten.MdmService.Application.Tests.Audit;

public sealed class TrustedSourceAuditServiceIdentityContractTests
{
    [Fact]
    public void Identity_contains_only_bearer_material_and_expiry()
    {
        Assert.Equal(["AccessToken", "ExpiresAtUtc"],
            typeof(TrustedSourceAuditServiceIdentity).GetProperties().Select(property => property.Name));
        Assert.DoesNotContain(typeof(TrustedSourceAuditServiceIdentity).GetProperties(), property =>
            property.Name.Contains("Refresh", StringComparison.OrdinalIgnoreCase)
            || property.Name.Contains("Secret", StringComparison.OrdinalIgnoreCase)
            || property.Name.Contains("Tenant", StringComparison.OrdinalIgnoreCase));
    }
}
