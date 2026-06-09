using System.Reflection;
using Diten.MdmService.Api.Controllers;
using Diten.MdmService.Infrastructure.Authorization;
using Xunit;

namespace Diten.MdmService.Application.Tests.Authorization;

// Slice 1A (D-5) — locks the Legal Entity controller onto the canonical, already-seeded permission keys
// and guards against any regression back to the never-granted legacy `Modules.LegalEntity.*` keys.
public sealed class LegalEntitiesControllerPermissionTests
{
    [Theory]
    [InlineData(nameof(LegalEntitiesController.GetById), "mdm.legal-entities.read")]
    [InlineData(nameof(LegalEntitiesController.ValidateReference), "mdm.legal-entities.read")]
    [InlineData(nameof(LegalEntitiesController.Create), "mdm.legal-entities.create")]
    [InlineData(nameof(LegalEntitiesController.Activate), "mdm.legal-entities.update")]
    [InlineData(nameof(LegalEntitiesController.Archive), "mdm.legal-entities.update")]
    [InlineData(nameof(LegalEntitiesController.Delete), "mdm.legal-entities.delete")]
    public void Action_enforces_canonical_permission_key(string methodName, string expectedKey)
    {
        var attribute = typeof(LegalEntitiesController)
            .GetMethod(methodName)!
            .GetCustomAttribute<HasPermissionAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal($"Permission:{expectedKey}", attribute!.Policy);
    }

    [Fact]
    public void No_action_uses_the_legacy_modules_legalentity_key()
    {
        var legacy = typeof(LegalEntitiesController)
            .GetMethods()
            .SelectMany(m => m.GetCustomAttributes<HasPermissionAttribute>())
            .Where(a => a.Policy is not null
                && a.Policy.Contains("Modules.LegalEntity", System.StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Empty(legacy);
    }
}
