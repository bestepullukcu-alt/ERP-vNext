using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.VisitEvidence;
using Diten.CrmService.Application.Features.VisitExecution;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ExcPerms = Diten.CrmService.Application.Features.VisitEvidence.VisitEvidenceExceptionPermissions;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// WP-VW-W3 (W3-BE-c, §2.2–2.3) — the visit evidence settings page's API:
/// <list type="bullet">
/// <item><c>api/crm/visit-evidence-policy/{history|effective|reps|reps/roster}</c> — read with <c>crm.visit-evidence-policy.read</c>
/// OR <c>.manage</c> (the policy GET / PUT themselves stay on <see cref="VisitExecutionController"/>);</item>
/// <item>WP-VW-W3L — <c>api/crm/visit-evidence-policy/scopes[/{scopeType}/{scopeRef}]</c>: the legal entity / business
/// unit layers — list / read with read OR manage, write (PUT, versioned) / remove (DELETE = archive) with <c>.manage</c>;
/// the history takes <c>?scope=&amp;scopeRef=</c>;</item>
/// <item><c>api/crm/visit-evidence-exceptions</c> — the time-boxed exceptions: read (<c>crm.visit-evidence-exception.read</c>),
/// write / submit / withdraw / cancel / end early (<c>…manage</c>), decide (<c>…read</c>; the Work Center task decides
/// who may).</item>
/// </list>
/// TenantId is server-resolved.
/// </summary>
[Authorize]
public sealed class VisitEvidenceSettingsController : CustomBaseController
{
    private const string Policy = "api/crm/visit-evidence-policy";
    private const string Exceptions = "api/crm/visit-evidence-exceptions";

    private readonly IMediator _mediator;

    public VisitEvidenceSettingsController(IMediator mediator) => _mediator = mediator;

    /// <summary>Read OR manage: the settings page is readable by a viewer (K-C4).</summary>
    public static bool MayReadPolicy(System.Security.Claims.ClaimsPrincipal user)
        => PermissionClaims.HasPermission(user, VisitExecutionPermissions.PolicyRead)
           || PermissionClaims.HasPermission(user, VisitExecutionPermissions.PolicyManage);

    [HttpGet(Policy + "/history")]
    public async Task<IActionResult> History([FromQuery] int? page, [FromQuery] int? pageSize, [FromQuery] string? scope,
        [FromQuery] string? scopeRef, CancellationToken cancellationToken)
        => MayReadPolicy(User)
            ? CreateActionResultInstance(await _mediator.Send(new GetVisitEvidencePolicyHistoryQuery(page, pageSize, scope, scopeRef), cancellationToken))
            : Forbidden();

    /// <summary>WP-VW-W3L — every scope (the tenant first): type, ref, the fields it overrides, the last change, how many
    /// reps it applies to today.</summary>
    [HttpGet(Policy + "/scopes")]
    public async Task<IActionResult> Scopes(CancellationToken cancellationToken)
        => MayReadPolicy(User)
            ? CreateActionResultInstance(await _mediator.Send(new ListVisitEvidencePolicyScopesQuery(), cancellationToken))
            : Forbidden();

    /// <summary>WP-VW-W3L — one scope's fields (null = inherited); <c>tenant</c> answers the tenant policy.</summary>
    [HttpGet(Policy + "/scopes/{scopeType}/{scopeRef}")]
    public async Task<IActionResult> GetScope(string scopeType, string scopeRef, CancellationToken cancellationToken)
        => MayReadPolicy(User)
            ? CreateActionResultInstance(await _mediator.Send(new GetVisitEvidencePolicyScopeQuery(scopeType, scopeRef), cancellationToken))
            : Forbidden();

    /// <summary>WP-VW-W3L — create a scope, or replace its overrides at <c>expectedVersion</c> (409 otherwise).</summary>
    [HttpPut(Policy + "/scopes/{scopeType}/{scopeRef}")]
    [HasPermission(VisitExecutionPermissions.PolicyManage)]
    public async Task<IActionResult> PutScope(string scopeType, string scopeRef, [FromBody] VisitEvidencePolicyLayerInput input,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new UpsertVisitEvidencePolicyScopeCommand(scopeType, scopeRef, input ?? new()), cancellationToken));

    /// <summary>WP-VW-W3L — "remove the scope": archived (never deleted), written to the history.</summary>
    [HttpDelete(Policy + "/scopes/{scopeType}/{scopeRef}")]
    [HasPermission(VisitExecutionPermissions.PolicyManage)]
    public async Task<IActionResult> RemoveScope(string scopeType, string scopeRef, [FromQuery] int? expectedVersion,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new ArchiveVisitEvidencePolicyScopeCommand(scopeType, scopeRef, expectedVersion), cancellationToken));

    [HttpGet(Policy + "/effective")]
    public async Task<IActionResult> Effective([FromQuery] string? resourceId, CancellationToken cancellationToken)
        => MayReadPolicy(User)
            ? CreateActionResultInstance(await _mediator.Send(new GetVisitEvidenceEffectiveQuery(resourceId), cancellationToken))
            : Forbidden();

    [HttpGet(Policy + "/reps")]
    public async Task<IActionResult> Reps([FromQuery] string? q, CancellationToken cancellationToken)
        => MayReadPolicy(User)
            ? CreateActionResultInstance(await _mediator.Send(new ListVisitEvidenceRepsQuery(q), cancellationToken))
            : Forbidden();

    /// <summary>WP-VW-W3E (W3E-E4R2-FIX-d) — the paged rep roster of the settings page ({ items, total, page, pageSize };
    /// pageSize 1–100, default 25). The picker read above stays as it is.</summary>
    [HttpGet(Policy + "/reps/roster")]
    public async Task<IActionResult> RepRoster([FromQuery] string? q, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
        => MayReadPolicy(User)
            ? CreateActionResultInstance(await _mediator.Send(new ListVisitEvidenceRepRosterQuery(q, page, pageSize), cancellationToken))
            : Forbidden();

    [HttpGet(Exceptions)]
    [HasPermission(ExcPerms.Read)]
    public async Task<IActionResult> ListExceptions([FromQuery] string? status, [FromQuery] string? phase, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new ListVisitEvidenceExceptionsQuery(status, phase), cancellationToken));

    [HttpGet(Exceptions + "/{id:guid}")]
    [HasPermission(ExcPerms.Read)]
    public async Task<IActionResult> GetException(Guid id, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetVisitEvidenceExceptionQuery(id), cancellationToken));

    [HttpPost(Exceptions)]
    [HasPermission(ExcPerms.Manage)]
    public async Task<IActionResult> CreateException([FromBody] VisitEvidenceExceptionInput input, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new CreateVisitEvidenceExceptionCommand(input), cancellationToken));

    [HttpPut(Exceptions + "/{id:guid}")]
    [HasPermission(ExcPerms.Manage)]
    public async Task<IActionResult> UpdateException(Guid id, [FromBody] VisitEvidenceExceptionInput input, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new UpdateVisitEvidenceExceptionCommand(id, input), cancellationToken));

    [HttpPost(Exceptions + "/{id:guid}/submit")]
    [HasPermission(ExcPerms.Manage)]
    public async Task<IActionResult> SubmitException(Guid id, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new SubmitVisitEvidenceExceptionCommand(id), cancellationToken));

    [HttpPost(Exceptions + "/{id:guid}/withdraw")]
    [HasPermission(ExcPerms.Manage)]
    public async Task<IActionResult> WithdrawException(Guid id, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new WithdrawVisitEvidenceExceptionCommand(id), cancellationToken));

    [HttpPost(Exceptions + "/{id:guid}/decision")]
    [HasPermission(ExcPerms.Read)]
    public async Task<IActionResult> DecideException(Guid id, [FromBody] ApprovalDecisionRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new DecideVisitEvidenceExceptionCommand(id, request.Decision, request.Comment), cancellationToken));

    [HttpPost(Exceptions + "/{id:guid}/cancel")]
    [HasPermission(ExcPerms.Manage)]
    public async Task<IActionResult> CancelException(Guid id, [FromBody] ExceptionReasonRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new CancelVisitEvidenceExceptionCommand(id, request.Reason), cancellationToken));

    [HttpPost(Exceptions + "/{id:guid}/end-early")]
    [HasPermission(ExcPerms.Manage)]
    public async Task<IActionResult> EndExceptionEarly(Guid id, [FromBody] ExceptionReasonRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new EndVisitEvidenceExceptionEarlyCommand(id, request.Reason), cancellationToken));

    private IActionResult Forbidden()
        => CreateActionResultInstance(Response<object>.Fail(new[]
        {
            "Reading the visit evidence settings needs crm.visit-evidence-policy.read or .manage.", "permission_denied"
        }, 403));
}

/// <summary>WP-VW-W3 (W3-BE-c) — cancel / end early body.</summary>
public sealed record ExceptionReasonRequest(string? Reason);
