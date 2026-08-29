using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Models.DocumentManagement;
using Diten.Platform.API.Observability;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments;
using Diten.Platform.Application.Features.DocumentManagementVariantLocalization;
using Diten.Platform.Application.Features.DocumentManagementVariantLocalization.Commands;
using Diten.Platform.Application.Features.DocumentManagementVariantLocalization.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers;

/// <summary>
/// MOD-0029-FU18 — TenantShell variant translation / site-adoption governance API (GMG-QMS-SOP-0001 §13.2). Thin
/// controller; dispatches via MediatR.
///
/// SCOPE: this governs BUSINESS DOCUMENT variants — translations and site-adopted copies of controlled documents.
/// It is unrelated to the application's UI localization resources. It stores metadata and evidence references
/// only: no translated content, no content or binary comparison, no machine translation.
///
/// Deliberately absent: e-signature, CAPA records, MOD-0023 workflow runtime integration, and any endpoint that
/// mutates the FU03 variant aggregate, its drift computation, its create/rebase/compare behaviour or the parent
/// master. There is no DELETE verb — evidence is append-only and assessment history is preserved.
///
/// Layer 1 RBAC REUSES the seeded controlled-documents view/create keys (no AuthService seed change); dedicated
/// <see cref="VariantLocalizationPermissions"/> keys should be seeded in a later hardening FU. TenantId is never
/// read from the client; it is resolved server-side.
/// </summary>
[ApiController]
[Route("api/v1/document-management/template-variants")]
[Authorize]
public sealed class DocumentManagementVariantLocalizationController : CustomBaseController
{
    private readonly IMediator _mediator;
    private readonly ICorrelationContext _correlationContext;

    public DocumentManagementVariantLocalizationController(IMediator mediator, ICorrelationContext correlationContext)
    {
        _mediator = mediator;
        _correlationContext = correlationContext;
    }

    // ── localization profile ──────────────────────────────────────────────────

    [HttpGet("{id:guid}/localization-profile")]
    [HasPermission(VariantLocalizationPermissions.View)]
    public async Task<IActionResult> GetProfile(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetVariantLocalizationProfileQuery(id, CorrelationId), ct));

    [HttpPut("{id:guid}/localization-profile")]
    [HasPermission(VariantLocalizationPermissions.Manage)]
    public async Task<IActionResult> UpsertProfile(Guid id, [FromBody] VariantLocalizationProfileApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new UpsertVariantLocalizationProfileCommand(id, ToFields(request), CorrelationId), ct));

    // ── bilingual review ──────────────────────────────────────────────────────

    [HttpPost("{id:guid}/bilingual-review/require")]
    [HasPermission(VariantLocalizationPermissions.TranslationReviewRecord)]
    public async Task<IActionResult> RequireBilingualReview(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new RequireBilingualReviewCommand(id, CorrelationId), ct));

    [HttpPost("{id:guid}/bilingual-review/complete")]
    [HasPermission(VariantLocalizationPermissions.TranslationReviewRecord)]
    public async Task<IActionResult> CompleteBilingualReview(Guid id, [FromBody] RecordBilingualReviewApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new RecordBilingualReviewEvidenceCommand(id,
            new RecordBilingualReviewInput(request.ReviewerUserId, request.ReviewerRole, request.EvidenceReference, request.Comment),
            CorrelationId), ct));

    [HttpPost("{id:guid}/bilingual-review/reject")]
    [HasPermission(VariantLocalizationPermissions.TranslationReviewRecord)]
    public async Task<IActionResult> RejectBilingualReview(Guid id, [FromBody] RejectVariantReviewApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new RejectBilingualReviewCommand(id,
            new RejectVariantReviewInput(request.Reason, request.EvidenceReference), CorrelationId), ct));

    // ── local approval ────────────────────────────────────────────────────────

    [HttpPost("{id:guid}/local-approval/require")]
    [HasPermission(VariantLocalizationPermissions.LocalApprovalRecord)]
    public async Task<IActionResult> RequireLocalApproval(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new RequireLocalApprovalCommand(id, CorrelationId), ct));

    [HttpPost("{id:guid}/local-approval/complete")]
    [HasPermission(VariantLocalizationPermissions.LocalApprovalRecord)]
    public async Task<IActionResult> CompleteLocalApproval(Guid id, [FromBody] RecordLocalApprovalApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new RecordLocalApprovalEvidenceCommand(id,
            new RecordLocalApprovalInput(request.ApproverUserId, request.ApproverRole, request.EvidenceReference, request.Comment),
            CorrelationId), ct));

    [HttpPost("{id:guid}/local-approval/reject")]
    [HasPermission(VariantLocalizationPermissions.LocalApprovalRecord)]
    public async Task<IActionResult> RejectLocalApproval(Guid id, [FromBody] RejectVariantReviewApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new RejectLocalApprovalCommand(id,
            new RejectVariantReviewInput(request.Reason, request.EvidenceReference), CorrelationId), ct));

    // ── parent change assessment ──────────────────────────────────────────────

    /// <summary>Records what the parent looks like now and what the variant must do. Transitions nothing.</summary>
    [HttpPost("{id:guid}/parent-change/evaluate")]
    [HasPermission(VariantLocalizationPermissions.Manage)]
    public async Task<IActionResult> EvaluateParentChange(Guid id, [FromBody] EvaluateVariantParentChangeApiRequest? request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateVariantParentChangeCommand(id, request?.EvidenceReference, CorrelationId), ct));

    [HttpGet("{id:guid}/parent-change/assessments")]
    [HasPermission(VariantLocalizationPermissions.View)]
    public async Task<IActionResult> ParentChangeAssessments(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetVariantParentChangeAssessmentsQuery(id, CorrelationId), ct));

    // ── readiness + evidence ──────────────────────────────────────────────────

    [HttpGet("{id:guid}/readiness")]
    [HasPermission(VariantLocalizationPermissions.View)]
    public async Task<IActionResult> Readiness(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetVariantReadinessQuery(id, CorrelationId), ct));

    [HttpGet("{id:guid}/localization-evidence")]
    [HasPermission(VariantLocalizationPermissions.View)]
    public async Task<IActionResult> Evidence(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetVariantReviewEvidenceQuery(id, CorrelationId), ct));

    // ── temporary English master allowance ────────────────────────────────────

    [HttpPost("{id:guid}/temporary-english-master/allow")]
    [HasPermission(VariantLocalizationPermissions.Manage)]
    public async Task<IActionResult> AllowTemporaryEnglishMaster(Guid id, [FromBody] AllowTemporaryEnglishMasterApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new AllowTemporaryEnglishMasterCommand(id,
            new AllowTemporaryEnglishMasterInput(request.Justification, request.ApprovedBy, request.ExpiresAt, request.EvidenceReference),
            CorrelationId), ct));

    [HttpPost("{id:guid}/temporary-english-master/revoke")]
    [HasPermission(VariantLocalizationPermissions.Manage)]
    public async Task<IActionResult> RevokeTemporaryEnglishMaster(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new RevokeTemporaryEnglishMasterCommand(id, CorrelationId), ct));

    private static VariantLocalizationProfileInput ToFields(VariantLocalizationProfileApiRequest r) => new(
        r.VariantIdentifier, r.VariantLanguageCode, r.VariantLanguageName, r.SourceLanguageCode, r.CountryCode,
        r.SiteCode, r.IsTranslationVariant, r.IsSiteAdoptedVariant, r.IsLocalLanguageMandatory,
        r.ParentTemplateMasterId, r.ParentTemplateMasterVersionId, r.ParentRegisterEntryId, r.ParentDocumentUid,
        r.ParentDocumentCode, r.ParentVersionLabel, r.LocalDocumentRegisterEntryId, r.AuthorUserId,
        r.BilingualReviewerUserId, r.BilingualReviewerRole, r.LocalApproverUserId, r.LocalApproverRole,
        r.LocalEffectiveDate);

    private string CorrelationId =>
        string.IsNullOrWhiteSpace(_correlationContext.CorrelationId) ? HttpContext.TraceIdentifier : _correlationContext.CorrelationId!;
}
