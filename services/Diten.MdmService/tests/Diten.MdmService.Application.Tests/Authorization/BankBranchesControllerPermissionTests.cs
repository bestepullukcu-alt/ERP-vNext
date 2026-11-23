using System.Reflection;
using Diten.MdmService.Api.Controllers;
using Diten.MdmService.Infrastructure.Authorization;
using Xunit;

namespace Diten.MdmService.Application.Tests.Authorization;

/// <summary>MOD-0289 DL-1 (AC-8, branch part) — a branch is guarded by the same <c>mdm.banks.*</c> family as its bank.</summary>
public sealed class BankBranchesControllerPermissionTests
{
    [Theory]
    [InlineData(nameof(BankBranchesController.GetList), "mdm.banks.read")]
    [InlineData(nameof(BankBranchesController.GetById), "mdm.banks.read")]
    [InlineData(nameof(BankBranchesController.Create), "mdm.banks.create")]
    [InlineData(nameof(BankBranchesController.Update), "mdm.banks.update")]
    [InlineData(nameof(BankBranchesController.Activate), "mdm.banks.activate")]
    [InlineData(nameof(BankBranchesController.Deactivate), "mdm.banks.deactivate")]
    [InlineData(nameof(BankBranchesController.Delete), "mdm.banks.delete")]
    public void Mod0289Dl1_AC08_Branch_action_enforces_the_packs_permission_key(string method, string expectedKey)
    {
        var attribute = typeof(BankBranchesController).GetMethod(method)!.GetCustomAttribute<HasPermissionAttribute>();
        Assert.NotNull(attribute);
        Assert.Equal($"Permission:{expectedKey}", attribute!.Policy);
    }
}
