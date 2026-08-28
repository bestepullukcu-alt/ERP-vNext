using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Models.DocumentManagement;
using Diten.Platform.API.Observability;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments;
using Diten.Platform.Application.Features.DocumentManagementExternalDocuments;
using Diten.Platform.Application.Features.DocumentManagementExternalDocuments.Commands;
using Diten.Platform.Application.Features.DocumentManagementExternalDocuments.Queries;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers;

/// <summary>
/// MOD-0029-FU14 — TenantShell External Document Register API (GMG-QMS-SOP-0001 §10). Thin controller; dispatches
/// via MediatR. It registers external regulations/guidelines/standards/pharmacopeia, records monitoring evidence,
/// raises impact assessments with the 10-working-day regulated deadline, and links external requirements to the
/// internal FU06 register.
///
/// NOT in scope and deliberately absent: any regulatory-intelligence crawler, authority website/API monitoring,
/// external file ingestion, e-signature, CAPA/Quality Event records and MOD-0023 workflow runtime integration.
/// There is no delete endpoint — supersession, archival and link closure are status changes.
///
/// Layer 1 RBAC REUSES the seeded controlled-documents view/create keys (no AuthService seed change); dedicated
/// <see cref="ExternalDocumentPermissions"/> keys should be seeded in a later hardening FU. TenantId is never read
/// from the client — it is resolved server-side.
/// </summary>
[ApiController]
[Route("api/v1/document-management")]
[Authorize]
public sealed class DocumentManagementExternalDocumentsController : CustomBaseController
{
    private readonly IMediator _mediator;
    private readonly ICorrelationContext _correlationContext;

    public DocumentManagementExternalDocumentsController(IMediator mediator, ICorrelationContext correlationContext)
    {
        _mediator = mediator;
        _correlationContext = correlationContext;
    }

    // ── register ──────────────────────────────────────────────────────────────

    [HttpGet("external-documents")]
    [HasPermission(ExternalDocumentPermissions.View)]
    public async Task<IActionResult> List(
        [FromQuery] string? externalDocumentStatus,
        [FromQuery] string? sourceStatus,
        [FromQuery] string? externalDocumentType,
        [FromQuery] string? impactAssessmentStatus,
        [FromQuery] Guid? monitoringOwnerUserId,
        CancellationToken ct)
    {
        var filter = new ExternalDocumentListFilter(
            Parse<ExternalDocumentStatus>(externalDocumentStatus),
            Parse<ExternalSourceStatus>(sourceStatus),
            Parse<ExternalDocumentType>(externalDocumentType),
            Parse<ExternalImpactAssessmentStatus>(impactAssessmentStatus),
            monitoringOwnerUserId);
        return CreateActionResultInstance(await _mediator.Send(new GetExternalDocumentsQuery(filter, CorrelationId), ct));
    }

    [HttpGet("external-documents/monitoring-due")]
    [HasPermission(ExternalDocumentPermissions.View)]
    public async Task<IActionResult> MonitoringDue(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetExternalDocumentsMonitoringDueQuery(CorrelationId), ct));

    [HttpGet("external-documents/impact-assessments/overdue")]
    [HasPermission(ExternalDocumentPermissions.View)]
    public async Task<IActionResult> OverdueImpactAssessments(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetOverdueExternalDocumentImpactAssessmentsQuery(CorrelationId), ct));

    [HttpGet("external-documents/{id:guid}")]
    [HasPermission(ExternalDocumentPermissions.View)]
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetExternalDocumentByIdQuery(id, CorrelationId), ct));

    [HttpPost("external-documents")]
    [HasPermission(ExternalDocumentPermissions.Manage)]
    public async Task<IActionResult> Create([FromBody] ExternalDocumentApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateExternalDocumentRegisterEntryCommand(ToFields(request), CorrelationId), ct));

    [HttpPut("external-documents/{id:guid}")]
    [HasPermission(ExternalDocumentPermissions.Manage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] ExternalDocumentApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new UpdateExternalDocumentRegisterEntryCommand(id, ToFields(request), CorrelationId), ct));

    [HttpPost("external-documents/{id:guid}/mark-superseded")]
    [HasPermission(ExternalDocumentPermissions.Manage)]
    public async Task<IActionResult> MarkSuperseded(Guid id, [FromBody] MarkExternalDocumentSupersededApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new MarkExternalDocumentSupersededCommand(
            id, new MarkExternalDocumentSupersededInput(request.SourceSupersededDate, request.SupersessionSummary), CorrelationId), ct));

    [HttpPost("external-documents/{id:guid}/archive")]
    [HasPermission(ExternalDocumentPermissions.Manage)]
    public async Task<IActionResult> Archive(Guid id, [FromBody] ArchiveExternalDocumentApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ArchiveExternalDocumentCommand(
            id, new ArchiveExternalDocumentInput(request.Reason), CorrelationId), ct));

    // ── monitoring ────────────────────────────────────────────────────────────

    [HttpGet("external-documents/{id:guid}/monitoring-checks")]
    [HasPermission(ExternalDocumentPermissions.View)]
    public async Task<IActionResult> MonitoringChecks(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetExternalDocumentMonitoringChecksQuery(id, CorrelationId), ct));

    [HttpPost("external-documents/{id:guid}/monitoring-checks")]
    [HasPermission(ExternalDocumentPermissions.MonitoringRecord)]
    public async Task<IActionResult> RecordMonitoringCheck(Guid id, [FromBody] RecordExternalDocumentMonitoringCheckApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new RecordExternalDocumentMonitoringCheckCommand(
            id, new RecordMonitoringCheckInput(request.MonitoringSource, request.EvidenceReference, request.ChangeDetected,
                request.ChangeSummary, request.SourceVersionObserved, request.SourceEffectiveDateObserved, request.CheckDate), CorrelationId), ct));

    // ── impact assessment ─────────────────────────────────────────────────────

    [HttpGet("external-documents/{id:guid}/impact-assessments")]
    [HasPermission(ExternalDocumentPermissions.View)]
    public async Task<IActionResult> ImpactAssessments(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetExternalDocumentImpactAssessmentsQuery(id, CorrelationId), ct));

    [HttpPost("external-documents/{id:guid}/impact-assessments")]
    [HasPermission(ExternalDocumentPermissions.ImpactManage)]
    public async Task<IActionResult> CreateImpactAssessment(Guid id, [FromBody] CreateExternalDocumentImpactAssessmentApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateExternalDocumentImpactAssessmentCommand(
            id, new CreateExternalImpactAssessmentInput(request.TriggerType, request.HasGmpImpact, request.HasGdpImpact,
                request.HasPvImpact, request.HasRaImpact, request.HasBatchReleaseImpact, request.HasTrainingImpact,
                request.HasDocumentImpact, request.ImpactSummary, request.TriggerDate), CorrelationId), ct));

    [HttpPost("external-documents/{id:guid}/impact-assessments/{assessmentId:guid}/complete")]
    [HasPermission(ExternalDocumentPermissions.ImpactManage)]
    public async Task<IActionResult> CompleteImpactAssessment(
        Guid id, Guid assessmentId, [FromBody] CompleteExternalDocumentImpactAssessmentApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CompleteExternalDocumentImpactAssessmentCommand(
            id, assessmentId, new CompleteExternalImpactAssessmentInput(request.AssessmentEvidenceReference,
                request.RecommendedAction, request.ImpactSummary, request.ActionOwnerUserId, request.ActionOwnerRole,
                request.ActionDueDate, request.ActionReference), CorrelationId), ct));

    // ── internal register links ───────────────────────────────────────────────

    [HttpGet("external-documents/{id:guid}/internal-links")]
    [HasPermission(ExternalDocumentPermissions.View)]
    public async Task<IActionResult> InternalLinks(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetExternalDocumentInternalLinksQuery(id, CorrelationId), ct));

    [HttpPost("external-documents/{id:guid}/internal-links")]
    [HasPermission(ExternalDocumentPermissions.Manage)]
    public async Task<IActionResult> LinkInternal(Guid id, [FromBody] LinkExternalDocumentToInternalApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new LinkExternalDocumentToInternalRegisterEntryCommand(
            id, new LinkExternalDocumentToInternalInput(request.InternalRegisterEntryId, request.LinkType, request.Notes), CorrelationId), ct));

    /// <summary>Closes a link (status change). There is deliberately no unlink/delete — nothing is hard-deleted.</summary>
    [HttpPost("external-documents/{id:guid}/internal-links/{linkId:guid}/close")]
    [HasPermission(ExternalDocumentPermissions.Manage)]
    public async Task<IActionResult> CloseInternalLink(Guid id, Guid linkId, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CloseExternalDocumentInternalLinkCommand(id, linkId, CorrelationId), ct));

    private static ExternalDocumentFieldsInput ToFields(ExternalDocumentApiRequest r) => new(
        r.ExternalDocumentTitle, r.ExternalAuthorityName, r.SourceReference, r.ExternalDocumentCode,
        r.ExternalDocumentType, r.Jurisdiction, r.CountryCode, r.RegionCode, r.SourceUrl, r.SourceVersion,
        r.SourceEffectiveDate, r.SourcePublishedDate, r.SourceSupersededDate, r.SourceStatus,
        r.MonitoringOwnerUserId, r.MonitoringOwnerRole, r.MonitoringFunction, r.MonitoringFrequency,
        r.HasGmpImpact, r.HasGdpImpact, r.HasPvImpact, r.HasRaImpact, r.HasBatchReleaseImpact,
        r.HasTrainingImpact, r.HasDocumentImpact, r.PromotionEvidenceReference);

    private static TEnum? Parse<TEnum>(string? value) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(value, true, out var parsed) ? parsed : null;

    private string CorrelationId =>
        string.IsNullOrWhiteSpace(_correlationContext.CorrelationId) ? HttpContext.TraceIdentifier : _correlationContext.CorrelationId!;
}
