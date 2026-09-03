using System.Reflection;
using System.Security.Claims;
using Diten.MdmService.Api.Controllers;
using Diten.MdmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class ProductIdentityWorkflowOperationRecoveryAuthorizationTests
{
    [Fact]
    public void Controller_requires_authentication_and_exact_operator_permission()
    {
        var type = typeof(ProductIdentityWorkflowOperationsController);
        Assert.NotNull(type.GetCustomAttribute<AuthorizeAttribute>());
        var method = type.GetMethod(nameof(ProductIdentityWorkflowOperationsController.RecoverBeforeStart))!;
        var permission = Assert.Single(method.GetCustomAttributes<HasPermissionAttribute>());
        Assert.Equal("Permission:mdm.product-identity.lifecycle-operations.recover", permission.Policy);
        Assert.Null(method.GetCustomAttribute<AllowAnonymousAttribute>());
    }

    [Theory]
    [InlineData("platform_admin")]
    [InlineData("partner_admin")]
    [InlineData("service")]
    [InlineData("machine")]
    [InlineData("")]
    public async Task Non_tenant_actor_types_fail_before_dispatch(string actorType) =>
        await AssertRejectedBeforeDispatch(Claims(actorType));

    [Fact]
    public async Task Missing_or_duplicate_actor_type_fails_before_dispatch()
    {
        await AssertRejectedBeforeDispatch(Claims(null));
        await AssertRejectedBeforeDispatch(Claims("tenant_user")
            .Append(new Claim("actor_type", "tenant_user")));
    }

    [Fact]
    public async Task Actor_tenant_and_subject_claim_type_aliases_fail_before_dispatch()
    {
        await AssertRejectedBeforeDispatch(Claims("tenant_user")
            .Where(claim => claim.Type != "actor_type")
            .Append(new Claim("Actor_Type", "tenant_user")));
        await AssertRejectedBeforeDispatch(Claims("tenant_user")
            .Where(claim => claim.Type != "tenant_id")
            .Append(new Claim("Tenant_Id",
                ProductIdentityWorkflowOperationRecoveryApiContractTests.TenantId.ToString("D"))));
        await AssertRejectedBeforeDispatch(Claims("tenant_user")
            .Where(claim => claim.Type != "sub")
            .Append(new Claim("Sub",
                ProductIdentityWorkflowOperationRecoveryApiContractTests.SubjectId.ToString("D"))));
    }

    [Fact]
    public async Task Exact_and_case_alias_tenant_claim_divergence_fails_before_dispatch()
    {
        var middlewareSelectedTenant = Guid.Parse("10000000-0000-0000-0000-000000000002");
        var claims = Claims("tenant_user")
            .Prepend(new Claim("Tenant_Id", middlewareSelectedTenant.ToString("D")));

        await AssertRejectedBeforeDispatch(claims, middlewareSelectedTenant);
    }

    [Theory]
    [InlineData("MDM.PRODUCT-IDENTITY.LIFECYCLE-OPERATIONS.RECOVER")]
    [InlineData("mdm.Global-products.submit")]
    [InlineData("mdm.Gskus.submit")]
    [InlineData("mdm.Lskus.submit")]
    [InlineData("mdm.Finished-goods.submit")]
    public async Task Recovery_or_family_submit_case_aliases_fail_before_dispatch(string alias)
    {
        var claims = Claims("tenant_user")
            .Where(claim => claim.Type != "permission")
            .Append(new Claim("permission", ProductIdentityWorkflowOperationRecoveryApiContractTests.RecoverPermission))
            .Append(new Claim("permission", alias));
        await AssertRejectedBeforeDispatch(claims);
    }

    [Fact]
    public async Task Guarded_family_submit_claim_in_plural_or_delimited_alias_form_fails_before_dispatch()
    {
        await AssertRejectedBeforeDispatch(Claims("tenant_user")
            .Append(new Claim("permissions", "mdm.global-products.submit")));
        await AssertRejectedBeforeDispatch(Claims("tenant_user")
            .Append(new Claim("permissions", "unrelated.read,MDM.GLOBAL-PRODUCTS.SUBMIT")));
    }

    [Fact]
    public async Task Recovery_permission_claim_type_alias_and_duplicate_exact_permission_fail_before_dispatch()
    {
        var typeAlias = Claims("tenant_user")
            .Where(claim => claim.Type != "permission")
            .Append(new Claim("Permission", ProductIdentityWorkflowOperationRecoveryApiContractTests.RecoverPermission));
        await AssertRejectedBeforeDispatch(typeAlias);

        var duplicate = Claims("tenant_user")
            .Append(new Claim("permission", ProductIdentityWorkflowOperationRecoveryApiContractTests.RecoverPermission));
        await AssertRejectedBeforeDispatch(duplicate);

        var plural = Claims("tenant_user")
            .Where(claim => claim.Type != "permission")
            .Append(new Claim("permissions", ProductIdentityWorkflowOperationRecoveryApiContractTests.RecoverPermission));
        await AssertRejectedBeforeDispatch(plural);

        var hiddenDuplicate = Claims("tenant_user")
            .Append(new Claim("permissions", ProductIdentityWorkflowOperationRecoveryApiContractTests.RecoverPermission));
        await AssertRejectedBeforeDispatch(hiddenDuplicate);
    }

    [Fact]
    public async Task Missing_recovery_permission_fails_before_dispatch() =>
        await AssertRejectedBeforeDispatch(Claims("tenant_user").Where(claim => claim.Type != "permission"));

    [Fact]
    public async Task Delegated_authorization_header_fails_before_dispatch_even_when_empty()
    {
        var mediator = DispatchProxy.Create<IMediator,
            ProductIdentityWorkflowOperationRecoveryApiContractTests.CapturingMediator>();
        var controller = ProductIdentityWorkflowOperationRecoveryApiContractTests.Controller(
            mediator, Guid.NewGuid().ToString("D"));
        controller.Request.Headers["X-Delegated-Authorization"] = "delegated-token-is-forbidden";

        var result = await controller.RecoverBeforeStart(
            Guid.NewGuid(), ProductIdentityWorkflowOperationRecoveryApiContractTests.ValidBody(), default);

        Assert.Equal(400, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
        Assert.Null(((ProductIdentityWorkflowOperationRecoveryApiContractTests.CapturingMediator)(object)mediator).Request);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("10000000-0000-0000-0000-000000000002")]
    [InlineData("10000000-0000-0000-0000-00000000000A")]
    public async Task Optional_tenant_header_must_be_one_canonical_exact_JWT_tenant(string? tenantHeader)
    {
        var mediator = DispatchProxy.Create<IMediator,
            ProductIdentityWorkflowOperationRecoveryApiContractTests.CapturingMediator>();
        var controller = ProductIdentityWorkflowOperationRecoveryApiContractTests.Controller(
            mediator, Guid.NewGuid().ToString("D"));
        controller.Request.Headers["X-Tenant-Id"] = tenantHeader;

        var result = await controller.RecoverBeforeStart(
            Guid.NewGuid(), ProductIdentityWorkflowOperationRecoveryApiContractTests.ValidBody(), default);

        Assert.Equal(400, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
        Assert.Null(((ProductIdentityWorkflowOperationRecoveryApiContractTests.CapturingMediator)(object)mediator).Request);
    }

    [Fact]
    public async Task Duplicate_tenant_header_and_missing_duplicate_or_noncanonical_tenant_claim_fail_before_dispatch()
    {
        var mediator = DispatchProxy.Create<IMediator,
            ProductIdentityWorkflowOperationRecoveryApiContractTests.CapturingMediator>();
        var controller = ProductIdentityWorkflowOperationRecoveryApiContractTests.Controller(
            mediator, Guid.NewGuid().ToString("D"));
        controller.Request.Headers["X-Tenant-Id"] = new StringValues(
            [ProductIdentityWorkflowOperationRecoveryApiContractTests.TenantId.ToString("D"),
                ProductIdentityWorkflowOperationRecoveryApiContractTests.TenantId.ToString("D")]);
        var result = await controller.RecoverBeforeStart(
            Guid.NewGuid(), ProductIdentityWorkflowOperationRecoveryApiContractTests.ValidBody(), default);
        Assert.Equal(400, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);

        await AssertRejectedBeforeDispatch(Claims("tenant_user").Where(claim => claim.Type != "tenant_id"));
        await AssertRejectedBeforeDispatch(Claims("tenant_user").Append(new Claim(
            "tenant_id", ProductIdentityWorkflowOperationRecoveryApiContractTests.TenantId.ToString("D"))));
        await AssertRejectedBeforeDispatch(Claims("tenant_user")
            .Where(claim => claim.Type != "tenant_id")
            .Append(new Claim("tenant_id", "10000000-0000-0000-0000-00000000000A")));
    }

    [Fact]
    public async Task Missing_duplicate_or_noncanonical_subject_fails_before_dispatch()
    {
        await AssertRejectedBeforeDispatch(Claims("tenant_user").Where(claim => claim.Type != "sub"));
        await AssertRejectedBeforeDispatch(Claims("tenant_user").Append(new Claim(
            "sub", ProductIdentityWorkflowOperationRecoveryApiContractTests.SubjectId.ToString("D"))));
        await AssertRejectedBeforeDispatch(Claims("tenant_user")
            .Where(claim => claim.Type != "sub")
            .Append(new Claim("sub", "20000000-0000-0000-0000-00000000000A")));
    }

    [Fact]
    public async Task Conflicting_duplicate_or_noncanonical_name_identifier_fails_before_dispatch()
    {
        await AssertRejectedBeforeDispatch(Claims("tenant_user")
            .Append(new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString("D"))));
        await AssertRejectedBeforeDispatch(Claims("tenant_user")
            .Append(new Claim(ClaimTypes.NameIdentifier,
                ProductIdentityWorkflowOperationRecoveryApiContractTests.SubjectId.ToString("D")))
            .Append(new Claim(ClaimTypes.NameIdentifier,
                ProductIdentityWorkflowOperationRecoveryApiContractTests.SubjectId.ToString("D"))));
        await AssertRejectedBeforeDispatch(Claims("tenant_user")
            .Append(new Claim(ClaimTypes.NameIdentifier,
                "20000000-0000-0000-0000-00000000000A")));
        await AssertRejectedBeforeDispatch(Claims("tenant_user")
            .Append(new Claim(
                ClaimTypes.NameIdentifier.ToUpperInvariant(),
                ProductIdentityWorkflowOperationRecoveryApiContractTests.SubjectId.ToString("D"))));
    }

    [Fact]
    public async Task Unresolved_or_claim_divergent_tenant_context_fails_before_dispatch()
    {
        await AssertRejectedBeforeDispatch(Claims("tenant_user"), resolveTenant: false);
        await AssertRejectedBeforeDispatch(Claims("tenant_user"), Guid.NewGuid());
    }

    [Fact]
    public async Task Exact_tenant_user_with_exact_header_reaches_application_authorization()
    {
        var mediator = DispatchProxy.Create<IMediator,
            ProductIdentityWorkflowOperationRecoveryApiContractTests.CapturingMediator>();
        var controller = ProductIdentityWorkflowOperationRecoveryApiContractTests.Controller(
            mediator, Guid.NewGuid().ToString("D"));
        controller.Request.Headers["X-Tenant-Id"] =
            ProductIdentityWorkflowOperationRecoveryApiContractTests.TenantId.ToString("D");

        await Assert.ThrowsAsync<ProductIdentityWorkflowOperationRecoveryApiContractTests.CapturedRequestException>(
            () => controller.RecoverBeforeStart(
                Guid.NewGuid(), ProductIdentityWorkflowOperationRecoveryApiContractTests.ValidBody(), default));
    }

    [Fact]
    public async Task Exact_matching_name_identifier_is_accepted()
    {
        var mediator = DispatchProxy.Create<IMediator,
            ProductIdentityWorkflowOperationRecoveryApiContractTests.CapturingMediator>();
        var claims = Claims("tenant_user").Append(new Claim(
            ClaimTypes.NameIdentifier,
            ProductIdentityWorkflowOperationRecoveryApiContractTests.SubjectId.ToString("D")));
        var controller = ProductIdentityWorkflowOperationRecoveryApiContractTests.Controller(
            mediator, Guid.NewGuid().ToString("D"), claims);

        await Assert.ThrowsAsync<ProductIdentityWorkflowOperationRecoveryApiContractTests.CapturedRequestException>(
            () => controller.RecoverBeforeStart(
                Guid.NewGuid(), ProductIdentityWorkflowOperationRecoveryApiContractTests.ValidBody(), default));
    }

    private static Claim[] Claims(string? actorType)
    {
        var claims = new List<Claim>
        {
            new("sub", ProductIdentityWorkflowOperationRecoveryApiContractTests.SubjectId.ToString("D")),
            new("tenant_id", ProductIdentityWorkflowOperationRecoveryApiContractTests.TenantId.ToString("D")),
            new("permission", ProductIdentityWorkflowOperationRecoveryApiContractTests.RecoverPermission)
        };
        if (actorType is not null) claims.Add(new Claim("actor_type", actorType));
        return claims.ToArray();
    }

    private static async Task AssertRejectedBeforeDispatch(
        IEnumerable<Claim> claims,
        Guid? resolvedTenantId = null,
        bool resolveTenant = true)
    {
        var mediator = DispatchProxy.Create<IMediator,
            ProductIdentityWorkflowOperationRecoveryApiContractTests.CapturingMediator>();
        var controller = ProductIdentityWorkflowOperationRecoveryApiContractTests.Controller(
            mediator,
            Guid.NewGuid().ToString("D"),
            claims,
            resolvedTenantId,
            resolveTenant);
        var result = await controller.RecoverBeforeStart(
            Guid.NewGuid(), ProductIdentityWorkflowOperationRecoveryApiContractTests.ValidBody(), default);
        Assert.Equal(400, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
        Assert.Null(((ProductIdentityWorkflowOperationRecoveryApiContractTests.CapturingMediator)(object)mediator).Request);
    }
}
