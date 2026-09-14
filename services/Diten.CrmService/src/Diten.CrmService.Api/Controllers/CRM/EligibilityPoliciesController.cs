using Diten.CrmService.Api.Models.CRM;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.ContentComposition.Eligibility;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Perms = Diten.CrmService.Application.Features.ContentComposition.Eligibility.EligibilityPermissions;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// SCMM-11-follow-API (CAND-CAP-0011) — eligibility policy authoring + evaluate HTTP surface. Canonical under
/// <c>/api/crm/content-composition/eligibility-policies</c> (CRUD) and <c>/api/crm/content-composition/eligibility:evaluate</c>
/// (the resolve action). A thin adapter over the ready EligibilityPolicy CQRS + the single <see cref="ResolveEligibilityQuery"/>
/// resolver — every action maps the request onto the command / query and dispatches through MediatR; no business logic,
/// no eligibility decision and no silent default live here. There is <b>no delete endpoint</b> — closing a policy is
/// Archive. Evaluate is fail-closed at the resolver (missing required dimension ⇒ Unresolved, infra failure propagates);
/// this surface only rejects a malformed / empty context up front with a 400.
/// </summary>
[Authorize]
public sealed class EligibilityPoliciesController : CustomBaseController
{
    private readonly IMediator _mediator;

    public EligibilityPoliciesController(IMediator mediator) => _mediator = mediator;

    private const string Base = "api/crm/content-composition/eligibility-policies";

    [HttpGet(Base)]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] DateTimeOffset? effectiveAt,
        [FromQuery] string? search,
        [FromQuery] bool includeArchived = true,
        CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(
            new ListEligibilityPoliciesQuery(status, effectiveAt, search, includeArchived), cancellationToken));

    [HttpGet(Base + "/{eligibilityPolicyId:guid}")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> Get(Guid eligibilityPolicyId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new GetEligibilityPolicyQuery(eligibilityPolicyId), cancellationToken));

    [HttpPost(Base)]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> Create(
        [FromBody] CreateEligibilityPolicyRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new CreateEligibilityPolicyCommand(
                request.PolicyCode, request.PolicyName, request.EffectiveFrom, ToConditionInputs(request.Conditions),
                request.Description, request.PolicyVersion, request.Status, request.EffectiveTo),
            cancellationToken));

    [HttpPut(Base + "/{eligibilityPolicyId:guid}")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> Update(
        Guid eligibilityPolicyId, [FromBody] UpdateEligibilityPolicyRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new UpdateEligibilityPolicyCommand(
                eligibilityPolicyId, request.PolicyName, request.EffectiveFrom, ToConditionInputs(request.Conditions),
                request.Description, request.PolicyVersion, request.Status, request.EffectiveTo),
            cancellationToken));

    [HttpPost(Base + "/{eligibilityPolicyId:guid}/archive")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> Archive(Guid eligibilityPolicyId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new ArchiveEligibilityPolicyCommand(eligibilityPolicyId), cancellationToken));

    /// <summary>Resolve one context against one policy (disjoint Eligible / Blocked / Unresolved). The context is
    /// required and supplied explicitly — an empty / malformed context is a 400 here (no silent default); a well-formed
    /// context with a missing REQUIRED policy dimension is the resolver's Unresolved, not a 400.</summary>
    [HttpPost("api/crm/content-composition/eligibility:evaluate")]
    [HasPermission(Perms.Evaluate)]
    public async Task<IActionResult> Evaluate(
        [FromBody] EvaluateEligibilityRequest request, CancellationToken cancellationToken)
    {
        if (request is null || request.PolicyId == Guid.Empty)
        {
            return CreateActionResultInstance(Response<EligibilityResult>.Fail("PolicyId is required.", 400));
        }

        if (request.Context is null || request.Context.Count == 0)
        {
            return CreateActionResultInstance(
                Response<EligibilityResult>.Fail("At least one context dimension is required.", 400));
        }

        var dimensions = new List<EligibilityContextDimension>();
        foreach (var d in request.Context)
        {
            var values = (d.Values ?? Array.Empty<string>())
                .Select(v => v?.Trim() ?? string.Empty).Where(v => v.Length > 0).ToList();
            if (string.IsNullOrWhiteSpace(d.Dimension) || values.Count == 0)
            {
                return CreateActionResultInstance(Response<EligibilityResult>.Fail(
                    "Each context dimension requires a non-empty name and at least one value.", 400));
            }

            dimensions.Add(new EligibilityContextDimension(d.Dimension.Trim(), values));
        }

        var query = new ResolveEligibilityQuery(
            request.PolicyId, new EligibilityContext(dimensions), request.PinnedSelections, request.At);
        return CreateActionResultInstance(await _mediator.Send(query, cancellationToken));
    }

    // Maps the API condition request shape onto the application command input. Null stays an empty list.
    private static IReadOnlyList<EligibilityConditionInput> ToConditionInputs(
        IReadOnlyList<EligibilityConditionRequest>? conditions)
        => (conditions ?? Array.Empty<EligibilityConditionRequest>())
            .Select(c => new EligibilityConditionInput(c.Dimension, c.Values, c.Match, c.Required)).ToList();
}
