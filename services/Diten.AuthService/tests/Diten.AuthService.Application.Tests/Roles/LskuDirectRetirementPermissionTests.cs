using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;
using Xunit;

namespace Diten.AuthService.Application.Tests.Roles;

// WP-MDM-LSKU-ACCEPT-01 S1: retiring an LSKU directly, without an approved request, is a system action — the key is
// catalogued but never granted to a human role (a person retires through mdm.lskus.request-retirement).
public sealed class LskuDirectRetirementPermissionTests
{
    [Theory]
    [InlineData("retire", false)]
    [InlineData("request-retirement", true)]
    public void Direct_lsku_retirement_is_not_grantable_to_a_human_role(string action, bool grantable)
    {
        var permission = new Permission("mdm", "lskus", action, "", null, moduleOverride: "product-item-sku-master");

        Assert.Equal(grantable, DefaultRolePermissionTemplate.CanBeGrantedToHumanRole(permission));
    }
}
