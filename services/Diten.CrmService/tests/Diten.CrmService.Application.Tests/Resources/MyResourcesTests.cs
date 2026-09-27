using System.Reflection;
using System.Security.Claims;
using Diten.CrmService.Api.Controllers.CRM;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Resources;
using Diten.CrmService.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Diten.CrmService.Application.Tests.Resources;

/// <summary>
/// WP-MOB-B02 — <c>GET /api/crm/resources/me</c>, interim user-as-resource. Drives the real controller + real handler
/// (a dispatching mediator, no canned answer) with a constructed caller principal, so every assertion is about
/// production code: identity comes only from the token, tenant is mandatory, anonymous / identityless → 401, and the
/// interim contract is exactly one active <c>user</c> item whose id equals the <c>sub</c> claim.
/// </summary>
public sealed class MyResourcesTests
{
    private static readonly Guid TenantA = Guid.Parse("97c50000-0000-0000-0000-00000000000a");

    // ---------- surface ----------

    [Fact]
    public void Controller_requires_authorization_and_me_is_not_anonymous()
    {
        Assert.NotNull(typeof(ResourcesController).GetCustomAttribute<AuthorizeAttribute>());
        var me = typeof(ResourcesController).GetMethod(nameof(ResourcesController.Me))!;
        Assert.Null(me.GetCustomAttribute<AllowAnonymousAttribute>());
    }

    [Fact]
    public void Me_is_GET_on_resources_base()
    {
        Assert.Equal("api/crm/resources", typeof(ResourcesController).GetCustomAttribute<RouteAttribute>()!.Template);
        var me = typeof(ResourcesController).GetMethod(nameof(ResourcesController.Me))!;
        Assert.Equal("me", me.GetCustomAttribute<HttpGetAttribute>()!.Template);
    }

    [Fact]
    public void Me_takes_no_caller_supplied_identity()
    {
        // Only a CancellationToken: no route/query/body parameter through which another user's or tenant's
        // resource could be requested.
        var parameters = typeof(ResourcesController).GetMethod(nameof(ResourcesController.Me))!.GetParameters();
        Assert.All(parameters, p => Assert.Equal(typeof(CancellationToken), p.ParameterType));
        Assert.Equal(nameof(GetMyResourcesQuery.UserId), typeof(GetMyResourcesQuery).GetProperties().Single().Name);
    }

    // ---------- behaviour ----------

    [Fact]
    public async Task Self_gets_exactly_one_active_user_resource_equal_to_sub()
    {
        var (status, body) = await CallMe(Principal(("sub", "user-42")), TenantA);

        Assert.Equal(200, status);
        var item = Assert.Single(body!.Data!.Items); // interim: always exactly one
        Assert.Equal("user-42", item.ResourceId);
        Assert.Equal(PlannedVisitResourceTypes.User, item.ResourceType);
        Assert.Equal("active", item.Status);
    }

    [Fact]
    public async Task NameIdentifier_is_used_when_sub_was_remapped_by_the_jwt_handler()
    {
        var (status, body) = await CallMe(Principal((ClaimTypes.NameIdentifier, "user-77")), TenantA);

        Assert.Equal(200, status);
        Assert.Equal("user-77", Assert.Single(body!.Data!.Items).ResourceId);
    }

    [Fact]
    public async Task Anonymous_caller_gets_401()
    {
        var (status, _) = await CallMe(new ClaimsPrincipal(new ClaimsIdentity()), TenantA);
        Assert.Equal(401, status);
    }

    [Fact]
    public async Task Authenticated_without_sub_or_nameidentifier_gets_401_and_email_is_not_a_fallback()
    {
        var (status, _) = await CallMe(Principal((ClaimTypes.Email, "rep@example.com"), ("name", "Rep")), TenantA);
        Assert.Equal(401, status);
    }

    [Fact]
    public async Task Blank_sub_gets_401()
    {
        var (status, _) = await CallMe(Principal(("sub", "   ")), TenantA);
        Assert.Equal(401, status);
    }

    [Fact]
    public async Task Missing_tenant_context_is_refused()
    {
        var (status, _) = await CallMe(Principal(("sub", "user-42")), tenantId: null);
        Assert.Equal(400, status);
    }

    [Fact]
    public async Task Two_callers_each_see_only_themselves()
    {
        var (_, a) = await CallMe(Principal(("sub", "user-A")), TenantA);
        var (_, b) = await CallMe(Principal(("sub", "user-B")), Guid.NewGuid());

        Assert.Equal(["user-A"], a!.Data!.Items.Select(i => i.ResourceId));
        Assert.Equal(["user-B"], b!.Data!.Items.Select(i => i.ResourceId));
    }

    // ---------- helpers ----------

    private static ClaimsPrincipal Principal(params (string Type, string Value)[] claims)
        => new(new ClaimsIdentity(claims.Select(c => new System.Security.Claims.Claim(c.Type, c.Value)), authenticationType: "Bearer"));

    private static async Task<(int Status, Response<MyResourcesDto>? Body)> CallMe(ClaimsPrincipal user, Guid? tenantId)
    {
        var tenant = new TenantContext();
        if (tenantId is { } t) tenant.SetTenant(t);

        var controller = new ResourcesController(new DispatchingMediator(new GetMyResourcesQueryHandler(tenant)))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } }
        };

        var result = Assert.IsAssignableFrom<ObjectResult>(await controller.Me(CancellationToken.None));
        return (result.StatusCode ?? 200, result.Value as Response<MyResourcesDto>);
    }

    // Routes GetMyResourcesQuery to the real handler; anything else is a test bug.
    private sealed class DispatchingMediator(GetMyResourcesQueryHandler handler) : IMediator
    {
        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
            => (TResponse)(object)await handler.Handle((GetMyResourcesQuery)(object)request, cancellationToken);

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest
            => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
