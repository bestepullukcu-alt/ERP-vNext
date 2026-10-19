using System.Reflection;
using Diten.MdmService.Api.Controllers;
using Diten.MdmService.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class ProductLegalEntityScopeAuthorizationTests
{
    [Fact]
    public void Controller_and_every_action_require_authentication_and_exact_permissions()
    {
        var type = typeof(ProductLegalEntityScopesController);
        Assert.NotNull(type.GetCustomAttribute<AuthorizeAttribute>());
        AssertPolicies(nameof(ProductLegalEntityScopesController.GetPolicy), "mdm.product-legal-entity-scopes.read");
        AssertPolicies(nameof(ProductLegalEntityScopesController.GetHistory), "mdm.product-legal-entity-scopes.read");
        AssertPolicies(nameof(ProductLegalEntityScopesController.GetEffectiveFacts), "mdm.product-legal-entity-scopes.read");
        AssertPolicies(nameof(ProductLegalEntityScopesController.CreatePolicy), "mdm.product-legal-entity-scopes.configure");
        AssertPolicies(nameof(ProductLegalEntityScopesController.ReplacePolicy), "mdm.product-legal-entity-scopes.replace");
        AssertPolicies(nameof(ProductLegalEntityScopesController.EndPolicy), "mdm.product-legal-entity-scopes.end");
    }

    [Fact]
    public void Create_options_requires_exact_three_policy_conjunction()
    {
        AssertPolicies(nameof(ProductLegalEntityScopesController.GetCreateOptions),
            "mdm.product-legal-entity-scopes.configure",
            "mdm.global-products.read",
            "mdm.legal-entities.read");
    }

    private static void AssertPolicies(string methodName, params string[] permissions)
    {
        var method = typeof(ProductLegalEntityScopesController).GetMethod(methodName)!;
        var policies = method.GetCustomAttributes<AuthorizeAttribute>()
            .Select(attribute => attribute.Policy)
            .Where(policy => policy is not null)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(permissions.Select(permission => $"Permission:{permission}").Order(StringComparer.Ordinal), policies);
    }
}
