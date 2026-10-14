using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;
using Xunit;

namespace Diten.AuthService.Application.Tests.Roles;

// B5 (WP-MDM-GSKU-ACCEPT-01 FIX1): retiring a GSKU directly, without an approved request, is a system action.
public sealed class GskuDirectRetirementPermissionTests
{
    [Theory]
    [InlineData("retire", false)]
    [InlineData("request-retirement", true)]
    public void Direct_gsku_retirement_is_not_grantable_to_a_human_role(string action, bool grantable)
    {
        var permission = new Permission("mdm", "gskus", action, "", null, moduleOverride: "product-item-sku-master");

        Assert.Equal(grantable, DefaultRolePermissionTemplate.CanBeGrantedToHumanRole(permission));
    }
}
