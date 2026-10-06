using System.Reflection;
using Diten.CrmService.Api.Controllers.CRM;
using Diten.CrmService.Api.Models.CRM;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.ContentComposition.Eligibility;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Diten.CrmService.Application.Tests;

/// <summary>
/// SCMM-11-follow-API (CAND-CAP-0011) — EligibilityPoliciesController surface. Reflection over attributes (class
/// [Authorize], per-action verb + canonical route + HasPermission key Read/Manage/Evaluate, no delete/patch) plus
/// behaviour: Create/Update/Evaluate map the request onto the ready command/query and dispatch through MediatR, and an
/// empty/malformed evaluate context is rejected with a 400 up front (no silent default, no dispatch).
/// </summary>
public sealed class EligibilityPoliciesControllerTests
{
    private const string Base = "api/crm/content-composition/eligibility-policies";
    private static readonly DateTimeOffset Jan1 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    // ── reflection ──────────────────────────────────────────────────────────────

    [Fact]
    public void Controller_requires_authorization()
        => Assert.NotNull(typeof(EligibilityPoliciesController).GetCustomAttribute<AuthorizeAttribute>());

    [Fact]
    public void No_delete_or_patch_endpoint_exists()
    {
        foreach (var method in typeof(EligibilityPoliciesController).GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            Assert.Empty(method.GetCustomAttributes<HttpDeleteAttribute>());
            Assert.Empty(method.GetCustomAttributes<HttpPatchAttribute>());
        }
    }

    [Theory]
    [InlineData(nameof(EligibilityPoliciesController.List), "GET", Base, EligibilityPermissions.Read)]
    [InlineData(nameof(EligibilityPoliciesController.Get), "GET", Base + "/{eligibilityPolicyId:guid}", EligibilityPermissions.Read)]
    [InlineData(nameof(EligibilityPoliciesController.Create), "POST", Base, EligibilityPermissions.Manage)]
    [InlineData(nameof(EligibilityPoliciesController.Update), "PUT", Base + "/{eligibilityPolicyId:guid}", EligibilityPermissions.Manage)]
    [InlineData(nameof(EligibilityPoliciesController.Archive), "POST", Base + "/{eligibilityPolicyId:guid}/archive", EligibilityPermissions.Manage)]
    [InlineData(nameof(EligibilityPoliciesController.Evaluate), "POST", "api/crm/content-composition/eligibility:evaluate", EligibilityPermissions.Evaluate)]
    public void Action_has_expected_verb_route_and_permission(string action, string verb, string route, string permission)
    {
        var method = typeof(EligibilityPoliciesController).GetMethod(action, BindingFlags.Public | BindingFlags.Instance)!;
        var (template, methods) = verb switch
        {
            "GET" => (method.GetCustomAttribute<HttpGetAttribute>()?.Template, method.GetCustomAttribute<HttpGetAttribute>()?.HttpMethods),
            "POST" => (method.GetCustomAttribute<HttpPostAttribute>()?.Template, method.GetCustomAttribute<HttpPostAttribute>()?.HttpMethods),
            "PUT" => (method.GetCustomAttribute<HttpPutAttribute>()?.Template, method.GetCustomAttribute<HttpPutAttribute>()?.HttpMethods),
            _ => (null, null)
        };
        Assert.NotNull(methods);
        Assert.Equal(route, template);
        Assert.Equal(permission, method.GetCustomAttribute<HasPermissionAttribute>()?.Permission);
    }

    // ── behaviour (DTO → command/query map + dispatch + evaluate 400) ────────────

    [Fact]
    public async Task Create_maps_request_onto_the_command()
    {
        var mediator = new CapturingMediator { Response = Response<Guid>.Success(Guid.NewGuid(), 201) };
        var controller = new EligibilityPoliciesController(mediator);

        var request = new CreateEligibilityPolicyRequest("POL-1", "Policy 1", Jan1,
            new[] { new EligibilityConditionRequest("market", new[] { "eu" }, "includes", true) },
            "desc", "1.0", "draft", null);
        await controller.Create(request, default);

        var cmd = Assert.IsType<CreateEligibilityPolicyCommand>(mediator.LastRequest);
        Assert.Equal("POL-1", cmd.PolicyCode);
        Assert.Equal("Policy 1", cmd.PolicyName);
        var cond = Assert.Single(cmd.Conditions);
        Assert.Equal("market", cond.Dimension);
        Assert.Equal(new[] { "eu" }, cond.Values.ToArray());
        Assert.True(cond.Required);
    }

    [Fact]
    public async Task Update_maps_request_onto_the_command()
    {
        var mediator = new CapturingMediator { Response = Response<bool>.Success(true) };
        var controller = new EligibilityPoliciesController(mediator);
        var id = Guid.NewGuid();

        await controller.Update(id, new UpdateEligibilityPolicyRequest("Renamed", Jan1, null), default);

        var cmd = Assert.IsType<UpdateEligibilityPolicyCommand>(mediator.LastRequest);
        Assert.Equal(id, cmd.EligibilityPolicyId);
        Assert.Equal("Renamed", cmd.PolicyName);
        Assert.Empty(cmd.Conditions);   // null → empty list
    }

    [Fact]
    public async Task Evaluate_maps_context_and_dispatches_the_resolve_query()
    {
        var mediator = new CapturingMediator
        {
            Response = Response<EligibilityResult>.Success(new EligibilityResult(
                EligibilityState.Eligible, null, null, Guid.NewGuid(), "1.0", Array.Empty<EligibilityConditionOutcome>(), Jan1))
        };
        var controller = new EligibilityPoliciesController(mediator);
        var policyId = Guid.NewGuid();

        var request = new EvaluateEligibilityRequest(policyId,
            new[] { new EligibilityContextDimensionRequest("market", new[] { "eu", "" }) },
            new[] { "content:x@1.0" }, Jan1);
        var result = await controller.Evaluate(request, default);

        var query = Assert.IsType<ResolveEligibilityQuery>(mediator.LastRequest);
        Assert.Equal(policyId, query.PolicyId);
        var dim = Assert.Single(query.Context.Dimensions);
        Assert.Equal("market", dim.Dimension);
        Assert.Equal(new[] { "eu" }, dim.Values.ToArray());   // blank value dropped
        Assert.Equal(new[] { "content:x@1.0" }, query.PinnedSelections!.ToArray());
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Evaluate_with_empty_context_returns_400_without_dispatch()
    {
        var mediator = new CapturingMediator();
        var controller = new EligibilityPoliciesController(mediator);

        var result = await controller.Evaluate(new EvaluateEligibilityRequest(Guid.NewGuid(), Context: null), default);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Null(mediator.LastRequest);   // no silent default — nothing dispatched
    }

    [Fact]
    public async Task Evaluate_with_a_blank_dimension_returns_400_without_dispatch()
    {
        var mediator = new CapturingMediator();
        var controller = new EligibilityPoliciesController(mediator);

        var request = new EvaluateEligibilityRequest(Guid.NewGuid(),
            new[] { new EligibilityContextDimensionRequest("  ", new[] { "eu" }) });
        var result = await controller.Evaluate(request, default);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Null(mediator.LastRequest);
    }

    [Fact]
    public async Task Evaluate_with_empty_policy_id_returns_400()
    {
        var mediator = new CapturingMediator();
        var controller = new EligibilityPoliciesController(mediator);

        var request = new EvaluateEligibilityRequest(Guid.Empty,
            new[] { new EligibilityContextDimensionRequest("market", new[] { "eu" }) });
        var result = await controller.Evaluate(request, default);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Null(mediator.LastRequest);
    }

    // Minimal capturing IMediator: records the dispatched request and returns a canned response.
    private sealed class CapturingMediator : IMediator
    {
        public object? LastRequest { get; private set; }
        public object? Response { get; set; }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult((TResponse)Response!);
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(Response);
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest
        {
            LastRequest = request;
            return Task.CompletedTask;
        }

        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
