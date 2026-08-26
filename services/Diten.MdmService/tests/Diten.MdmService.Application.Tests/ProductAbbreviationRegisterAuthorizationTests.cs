using System.Reflection;
using System.Security.Claims;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Commands;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Handlers.CommandHandlers;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Services;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Infrastructure.Security;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class ProductAbbreviationRegisterAuthorizationTests
{
    [Fact]
    public void Permission_contract_contains_exactly_the_eight_approved_keys()
    {
        var expected = new HashSet<string>(StringComparer.Ordinal)
        {
            "mdm.product-abbreviations.read",
            "mdm.product-abbreviations.request",
            "mdm.product-abbreviations.cancel",
            "mdm.product-abbreviations.approve",
            "mdm.product-abbreviations.reject",
            "mdm.product-abbreviations.correct",
            "mdm.product-abbreviations.retire",
            "mdm.product-abbreviations.audit"
        };

        Assert.True(expected.SetEquals(ProductAbbreviationPermissions.All));
        Assert.DoesNotContain("mdm.product-abbreviations.allocate", ProductAbbreviationPermissions.All);
        Assert.DoesNotContain("mdm.product-abbreviations.cancel-own", ProductAbbreviationPermissions.All);
        Assert.DoesNotContain("mdm.product-abbreviations.cancel-managed", ProductAbbreviationPermissions.All);
    }

    [Theory]
    [InlineData("service")]
    [InlineData("delegated")]
    [InlineData("workflow")]
    [InlineData("platform_admin")]
    [InlineData("")]
    [InlineData("unknown")]
    public void Non_direct_human_actor_types_fail_closed(string actorType)
    {
        var context = TrustedContext() with { ActorTypeValue = actorType };
        var result = new ProductAbbreviationAuthorization(context)
            .Demand(ProductAbbreviationPermissions.Request);

        Assert.False(result.Succeeded);
        Assert.Equal("ABBREVIATION_ACTOR_NOT_DIRECT_TENANT_HUMAN", result.ErrorCode);
    }

    [Fact]
    public void Missing_canonical_subject_fails_closed_even_with_permission()
    {
        var context = TrustedContext() with { Subject = string.Empty };
        var result = new ProductAbbreviationAuthorization(context)
            .Demand(ProductAbbreviationPermissions.Request);

        Assert.False(result.Succeeded);
        Assert.Equal("ABBREVIATION_ACTOR_NOT_DIRECT_TENANT_HUMAN", result.ErrorCode);
    }

    [Fact]
    public void Platform_admin_cannot_bypass_permission_or_actor_type_guard()
    {
        var context = TrustedContext() with
        {
            ActorTypeValue = "platform_admin",
            Permissions = ProductAbbreviationPermissions.All
        };

        Assert.False(new ProductAbbreviationAuthorization(context)
            .Demand(ProductAbbreviationPermissions.Approve).Succeeded);
    }

    [Fact]
    public void Valid_guid_subject_claims_are_compared_as_guids_and_returned_in_canonical_format()
    {
        var subjectId = Guid.NewGuid();
        var context = HttpActorContext(subjectId.ToString("N").ToUpperInvariant(), subjectId.ToString("D"));

        var result = new ProductAbbreviationAuthorization(context)
            .Demand(ProductAbbreviationPermissions.Request);

        Assert.True(result.Succeeded);
        Assert.Equal(subjectId.ToString("D"), context.CanonicalHumanSubjectId);
    }

    [Theory]
    [InlineData("not-a-guid", null)]
    [InlineData(null, "not-a-guid")]
    [InlineData("11111111-1111-1111-1111-111111111111", "22222222-2222-2222-2222-222222222222")]
    [InlineData(null, null)]
    public async Task Invalid_mismatched_or_missing_subject_claims_fail_closed_before_repository_access(
        string? nameIdentifier,
        string? subject)
    {
        var context = HttpActorContext(nameIdentifier, subject);

        var result = new ProductAbbreviationAuthorization(context)
            .Demand(ProductAbbreviationPermissions.Request);

        Assert.False(result.Succeeded);
        Assert.Equal("ABBREVIATION_ACTOR_NOT_DIRECT_TENANT_HUMAN", result.ErrorCode);
        Assert.Equal(string.Empty, context.CanonicalHumanSubjectId);

        var workflow = new ProductAbbreviationWorkflow(
            Proxy<IProductAbbreviationRegisterRepository>(),
            Proxy<IProductAbbreviationAllocationLedgerRepository>(),
            Proxy<IProductAbbreviationHistoryRepository>(),
            Proxy<IGlobalProductRepository>(),
            context,
            new ProductAbbreviationAuthorization(context));
        var response = await new RequestProductAbbreviationAllocationHandler(workflow).Handle(
            new RequestProductAbbreviationAllocationCommand(Guid.NewGuid(), "ABC", "invalid-subject"),
            default);

        Assert.False(response.IsSuccessful);
        Assert.Equal("ABBREVIATION_ACTOR_NOT_DIRECT_TENANT_HUMAN", Assert.Single(response.Errors));
    }

    private static T Proxy<T>() where T : class => DispatchProxy.Create<T, ThrowingProxy>();

    private static ProductAbbreviationActorContext HttpActorContext(
        string? nameIdentifier,
        string? subject)
    {
        var claims = new List<Claim>
        {
            new("actor_type", "tenant_user"),
            new("permission", ProductAbbreviationPermissions.Request)
        };
        if (nameIdentifier is not null)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, nameIdentifier));
        }

        if (subject is not null)
        {
            claims.Add(new Claim("sub", subject));
        }

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
        };
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(Guid.NewGuid());
        return new ProductAbbreviationActorContext(
            new HttpContextAccessor { HttpContext = httpContext },
            tenantContext);
    }

    private static TestActorContext TrustedContext()
        => new(
            Guid.NewGuid(),
            true,
            true,
            "tenant_user",
            "human-1",
            ProductAbbreviationPermissions.All,
            "correlation");

    private sealed record TestActorContext(
        Guid Tenant,
        bool TenantResolved,
        bool Authenticated,
        string ActorTypeValue,
        string Subject,
        IReadOnlySet<string> Permissions,
        string Correlation) : IProductAbbreviationActorContext
    {
        public Guid TenantId => Tenant;
        public bool TenantIsResolved => TenantResolved;
        public bool IsAuthenticated => Authenticated;
        public string ActorType => ActorTypeValue;
        public string CanonicalHumanSubjectId => Subject;
        public IReadOnlySet<string> GrantedPermissions => Permissions;
        public string CorrelationId => Correlation;
    }

    private class ThrowingProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => throw new InvalidOperationException($"Repository access was not expected: {targetMethod?.Name}");
    }
}
