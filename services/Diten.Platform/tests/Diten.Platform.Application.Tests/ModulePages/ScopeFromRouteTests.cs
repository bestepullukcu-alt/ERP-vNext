using Diten.Platform.Application.Features.ModulePages;
using Xunit;

namespace Diten.Platform.Application.Tests.ModulePages;

// İŞ3-FAZ1b — the route→scope rule the manifest/operator sync carries to AuthService: a /Platform/... route (or the
// exact "/Platform") is platform-admin; everything else is tenant-facing. This is what keeps workflow/reference-data
// PlatformAdmin and organization/legal-entity/access-governance Tenant after the Module flip.
public sealed class ScopeFromRouteTests
{
    [Theory]
    [InlineData("/Platform/Workflow", "PlatformAdmin")]
    [InlineData("/Platform/Workflow/Definitions/{id}/Designer", "PlatformAdmin")]
    [InlineData("/Platform/ReferenceData", "PlatformAdmin")]
    [InlineData("/platform/referencedata", "PlatformAdmin")]   // case-insensitive
    [InlineData("/Platform", "PlatformAdmin")]                 // exact segment
    [InlineData("/Users", "Tenant")]
    [InlineData("/Roles", "Tenant")]
    [InlineData("/LegalEntities", "Tenant")]
    [InlineData("/OrganizationUnits", "Tenant")]
    [InlineData("/DocumentManagementControlledDocuments", "Tenant")] // tenant route (its platform.* keys are locked separately)
    [InlineData("/PlatformFoo", "Tenant")]                    // NOT a /Platform/ route → not a false positive
    [InlineData("", "Tenant")]
    [InlineData(null, "Tenant")]
    public void ScopeFromRoute_maps_platform_prefix_to_platformadmin_else_tenant(string? route, string expected)
    {
        Assert.Equal(expected, ModulePageDescriptorNormalizer.ScopeFromRoute(route));
    }
}
