using System.Reflection;
using Diten.MdmService.Api.Controllers;
using Diten.MdmService.Infrastructure.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Diten.MdmService.Application.Tests.Authorization;

/// <summary>MOD-0289 DL-1 (AC-8) — every action of the bank controller holds the pack's key, and none is left without one.</summary>
public sealed class BanksControllerPermissionTests
{
    [Theory]
    [InlineData(nameof(BanksController.GetList), "mdm.banks.read")]
    [InlineData(nameof(BanksController.GetById), "mdm.banks.read")]
    [InlineData(nameof(BanksController.Create), "mdm.banks.create")]
    [InlineData(nameof(BanksController.Update), "mdm.banks.update")]
    [InlineData(nameof(BanksController.Activate), "mdm.banks.activate")]
    [InlineData(nameof(BanksController.Deactivate), "mdm.banks.deactivate")]
    [InlineData(nameof(BanksController.Delete), "mdm.banks.delete")]
    [InlineData(nameof(BanksController.BulkDelete), "mdm.banks.bulk-delete")]
    public void Mod0289Dl1_AC08_Bank_action_enforces_the_packs_permission_key(string method, string expectedKey)
    {
        var attribute = typeof(BanksController).GetMethod(method)!.GetCustomAttribute<HasPermissionAttribute>();
        Assert.NotNull(attribute);
        Assert.Equal($"Permission:{expectedKey}", attribute!.Policy);
    }

    [Fact]
    public void Mod0289Dl1_AC08_No_public_action_of_either_bank_controller_is_without_a_permission()
    {
        foreach (var controller in new[] { typeof(BanksController), typeof(BankBranchesController) })
        {
            var actions = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttributes().Any(a => a is Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute)).ToList();
            Assert.NotEmpty(actions);
            Assert.All(actions, action =>
                Assert.True(action.GetCustomAttribute<HasPermissionAttribute>() is not null, $"{controller.Name}.{action.Name} has no [HasPermission]"));
            // The bank controllers are authenticated as a whole, and no action opts out of it.
            Assert.NotNull(controller.GetCustomAttribute<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>());
            Assert.DoesNotContain(actions, a => a.GetCustomAttribute<Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute>() is not null);
        }
    }
}
