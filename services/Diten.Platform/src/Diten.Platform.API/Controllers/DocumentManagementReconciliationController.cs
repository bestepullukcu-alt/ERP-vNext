using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Models.DocumentManagement;
using Diten.Platform.API.Observability;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.DocumentManagementReconciliation;
using Diten.Platform.Application.Features.DocumentManagementReconciliation.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers;

/// <summary>
/// MOD-0028-FU09 — read-back reconciliation, provisioning evidence, IT/QA sign-off, deviation workflow, and
/// qualification readiness. Reads use the baseline view permission; mutations use the baseline publish permission
/// (no new permission seed in this FU). TenantId is always server-side.
/// </summary>
[ApiController]
[Route("api/v1/document-management")]
[Authorize]
public sealed class DocumentManagementReconciliationController : CustomBaseController
{
    private readonly IMediator _mediator;
    private readonly ICorrelationContext _correlationContext;

    public DocumentManagementReconciliationController(IMediator mediator, ICorrelationContext correlationContext)
    {
        _mediator = mediator;
        _correlationContext = correlationContext;
    }

    [HttpPost("qms-baselines/{baselineReleaseId:guid}/reconciliation/dry-run")]
    [HasPermission(ReconciliationPermissions.View)]
    public async Task<IActionResult> ReconciliationDryRun(Guid baselineReleaseId, [FromBody] ReconciliationRunApiRequest? request, CancellationToken ct)
    {
        var response = await _mediator.Send(
            new ReconciliationDryRunCommand(ToRequest(baselineReleaseId, request), CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("qms-baselines/{baselineReleaseId:guid}/reconciliation/apply-findings")]
    [HasPermission(ReconciliationPermissions.Manage)]
    public async Task<IActionResult> ReconciliationApplyFindings(Guid baselineReleaseId, [FromBody] ReconciliationRunApiRequest? request, CancellationToken ct)
    {
        var response = await _mediator.Send(
            new ReconciliationApplyFindingsCommand(ToRequest(baselineReleaseId, request), CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("collection-provisioning-evidence/upsert")]
    [HasPermission(ReconciliationPermissions.Manage)]
    public async Task<IActionResult> UpsertEvidence([FromBody] ProvisioningEvidenceUpsertApiRequest request, CancellationToken ct)
    {
        var input = new EvidenceUpsertInput(
            request.BaselineReleaseId, request.CollectionInstanceId, request.CollectionDefinitionId,
            request.RegisterFolderId, request.RegisterParentFolderId, request.FullPath, request.PlatformProvider,
            request.PlatformFolderId, request.PlatformParentId, request.ProvisioningStatus,
            request.CreatedOnPlatformAt, request.CreatedOnPlatformBy, request.DeviationComment);
        var response = await _mediator.Send(new UpsertProvisioningEvidenceCommand(input, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("collection-provisioning-evidence/{id:guid}/permissions-applied")]
    [HasPermission(ReconciliationPermissions.Manage)]
    public async Task<IActionResult> PermissionsApplied(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new MarkPermissionsAppliedCommand(id, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("collection-provisioning-evidence/{id:guid}/qa-verify")]
    [HasPermission(ReconciliationPermissions.Manage)]
    public async Task<IActionResult> QaVerify(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new MarkQaVerifiedCommand(id, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("qms-baselines/{baselineReleaseId:guid}/deviations")]
    [HasPermission(ReconciliationPermissions.View)]
    public async Task<IActionResult> GetDeviations(Guid baselineReleaseId, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetDeviationsQuery(baselineReleaseId, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("deviations/{id:guid}/resolve")]
    [HasPermission(ReconciliationPermissions.Manage)]
    public async Task<IActionResult> ResolveDeviation(Guid id, [FromBody] DeviationResolutionApiRequest? request, CancellationToken ct)
    {
        var response = await _mediator.Send(new ResolveDeviationCommand(id, request?.Comment, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("deviations/{id:guid}/accept")]
    [HasPermission(ReconciliationPermissions.Manage)]
    public async Task<IActionResult> AcceptDeviation(Guid id, [FromBody] DeviationResolutionApiRequest? request, CancellationToken ct)
    {
        var response = await _mediator.Send(new AcceptDeviationCommand(id, request?.Comment, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("qms-baselines/{baselineReleaseId:guid}/qualification-readiness")]
    [HasPermission(ReconciliationPermissions.View)]
    public async Task<IActionResult> QualificationReadiness(Guid baselineReleaseId, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetQualificationReadinessQuery(baselineReleaseId, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("qms-baselines/{baselineReleaseId:guid}/provisioning-evidence")]
    [HasPermission(ReconciliationPermissions.View)]
    public async Task<IActionResult> GetProvisioningEvidence(Guid baselineReleaseId, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetProvisioningEvidenceQuery(baselineReleaseId, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    private static ReconciliationRequest ToRequest(Guid baselineReleaseId, ReconciliationRunApiRequest? request) =>
        new(baselineReleaseId, request?.Scope ?? ReconciliationScope.DefinitionToInstance,
            request?.Provider ?? Domain.Enums.DocumentManagement.ProvisioningPlatformProvider.InHouse, DryRun: true);

    private string CorrelationId =>
        string.IsNullOrWhiteSpace(_correlationContext.CorrelationId)
            ? HttpContext.TraceIdentifier
            : _correlationContext.CorrelationId!;
}
