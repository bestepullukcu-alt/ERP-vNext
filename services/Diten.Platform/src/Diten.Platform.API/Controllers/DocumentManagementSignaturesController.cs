using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Models.DocumentManagement;
using Diten.Platform.API.Observability;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments;
using Diten.Platform.Application.Features.DocumentManagementElectronicSignature;
using Diten.Platform.Application.Features.DocumentManagementElectronicSignature.Commands;
using Diten.Platform.Application.Features.DocumentManagementElectronicSignature.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers;

/// <summary>
/// MOD-0029-FU23 — TenantShell document-control electronic signature API (GMG-QMS-SOP-0001 §11.2). Thin controller;
/// dispatches via MediatR.
///
/// WHAT THIS ADDS OVER FU09–FU22: those features record that an approval, release, training completion, correction
/// or CAPA closure happened, as evidence REFERENCES. This records WHO attested, WITH WHAT STATED MEANING, and
/// against WHICH EXACT OBJECT STATE — and drops the signature to RequiresResign when that state later changes.
///
/// ⚠️ EXPLICITLY NOT A REGULATED E-SIGNATURE CAPABILITY. No external e-signature provider is called, no certificate
/// chain is validated, no PAdES/XAdES artefact is produced, and no 21 CFR Part 11 / Annex 11 compliance claim is
/// made. An approved interim repository is never presented as a validated DMS. Every response carries the boundary
/// statement, and every signature record persists it.
///
/// Also deliberately absent: MOD-0023 workflow runtime integration, and any mutation of the signed subject —
/// signing an approval evidence record does not approve anything. FU09–FU22 behaviour is untouched.
///
/// There is no DELETE verb. Policy retirement, request cancellation/rejection and signature invalidation are status
/// changes; a signature record is append-only.
///
/// Layer 1 RBAC REUSES the seeded controlled-documents view/create keys (no AuthService seed change); dedicated
/// <see cref="ElectronicSignaturePermissions"/> keys should be seeded in a later hardening FU — signing and
/// invalidating are materially different authorities from viewing a signature history. TenantId is never read from
/// the client.
/// </summary>
[ApiController]
[Route("api/v1/document-management")]
[Authorize]
public sealed class DocumentManagementSignaturesController : CustomBaseController
{
    private readonly IMediator _mediator;
    private readonly ICorrelationContext _correlationContext;

    public DocumentManagementSignaturesController(IMediator mediator, ICorrelationContext correlationContext)
    {
        _mediator = mediator;
        _correlationContext = correlationContext;
    }

    // ── signature policies ────────────────────────────────────────────────────

    [HttpGet("signature-policies")]
    [HasPermission(ElectronicSignaturePermissions.SignaturesView)]
    public async Task<IActionResult> ListPolicies(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetSignaturePoliciesQuery(CorrelationId), ct));

    [HttpGet("signature-policies/{id:guid}")]
    [HasPermission(ElectronicSignaturePermissions.SignaturesView)]
    public async Task<IActionResult> PolicyDetail(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetSignaturePolicyByIdQuery(id, CorrelationId), ct));

    [HttpPost("signature-policies")]
    [HasPermission(ElectronicSignaturePermissions.SignaturePoliciesManage)]
    public async Task<IActionResult> CreatePolicy(
        [FromBody] CreateSignaturePolicyApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateSignaturePolicyCommand(
            new CreateSignaturePolicyInput(
                request.PolicyKey, request.PolicyName, request.SignableSubjectType, request.SignatureMeaning,
                request.RequiresReAuthentication, request.RequiresSecondFactor, request.RequiresMeaningStatement,
                request.RequiresRepositoryAssessment, request.RequiresObjectFingerprint,
                request.RequiresManifestation, request.AllowedRepositoryTypes,
                request.AllowInterimRepositorySignature, request.InterimRepositoryBoundaryStatement),
            CorrelationId), ct));

    [HttpPost("signature-policies/{id:guid}/activate")]
    [HasPermission(ElectronicSignaturePermissions.SignaturePoliciesManage)]
    public async Task<IActionResult> ActivatePolicy(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ActivateSignaturePolicyCommand(id, CorrelationId), ct));

    [HttpPost("signature-policies/{id:guid}/retire")]
    [HasPermission(ElectronicSignaturePermissions.SignaturePoliciesManage)]
    public async Task<IActionResult> RetirePolicy(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new RetireSignaturePolicyCommand(id, CorrelationId), ct));

    // ── signature requests ────────────────────────────────────────────────────

    [HttpGet("signature-requests")]
    [HasPermission(ElectronicSignaturePermissions.SignaturesView)]
    public async Task<IActionResult> ListRequests(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetSignatureRequestsQuery(CorrelationId), ct));

    [HttpGet("signature-requests/{id:guid}")]
    [HasPermission(ElectronicSignaturePermissions.SignaturesView)]
    public async Task<IActionResult> RequestDetail(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetSignatureRequestByIdQuery(id, CorrelationId), ct));

    [HttpPost("signature-requests")]
    [HasPermission(ElectronicSignaturePermissions.SignaturesRequest)]
    public async Task<IActionResult> CreateRequest(
        [FromBody] CreateSignatureRequestApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateSignatureRequestCommand(
            new CreateSignatureRequestInput(
                request.SubjectType, request.SubjectId, request.RegisterEntryId, request.ControlledDocumentId,
                request.RequestedSignerUserId, request.RequestedSignerRole, request.SignatureMeaning,
                request.DueDate, request.RequestReason, request.RepositoryAssessmentId),
            CorrelationId), ct));

    [HttpPost("signature-requests/{id:guid}/cancel")]
    [HasPermission(ElectronicSignaturePermissions.SignaturesRequest)]
    public async Task<IActionResult> CancelRequest(
        Guid id, [FromBody] CancelSignatureRequestApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CancelSignatureRequestCommand(id,
            new CancelSignatureRequestInput(request.Reason), CorrelationId), ct));

    [HttpPost("signature-requests/{id:guid}/reject")]
    [HasPermission(ElectronicSignaturePermissions.SignaturesRequest)]
    public async Task<IActionResult> RejectRequest(
        Guid id, [FromBody] RejectSignatureRequestApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new RejectSignatureRequestCommand(id,
            new RejectSignatureRequestInput(
                request.Reason, request.RejectionEvidenceReference, request.RejectedByUserId),
            CorrelationId), ct));

    // ── signatures ────────────────────────────────────────────────────────────

    [HttpGet("signatures")]
    [HasPermission(ElectronicSignaturePermissions.SignaturesView)]
    public async Task<IActionResult> ListSignatures(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetSignaturesQuery(CorrelationId), ct));

    [HttpGet("signatures/{id:guid}")]
    [HasPermission(ElectronicSignaturePermissions.SignaturesView)]
    public async Task<IActionResult> SignatureDetail(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetSignatureByIdQuery(id, CorrelationId), ct));

    [HttpGet("signatures/by-subject/{subjectType}/{subjectId:guid}")]
    [HasPermission(ElectronicSignaturePermissions.SignaturesView)]
    public async Task<IActionResult> SignaturesBySubject(
        string subjectType, Guid subjectId, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(
            new GetSignaturesBySubjectQuery(subjectType, subjectId, CorrelationId), ct));

    [HttpGet("signatures/fingerprints/{subjectType}/{subjectId:guid}")]
    [HasPermission(ElectronicSignaturePermissions.SignaturesView)]
    public async Task<IActionResult> FingerprintHistory(
        string subjectType, Guid subjectId, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(
            new GetSignedObjectFingerprintsQuery(subjectType, subjectId, CorrelationId), ct));

    [HttpPost("signatures/sign")]
    [HasPermission(ElectronicSignaturePermissions.SignaturesSign)]
    public async Task<IActionResult> Sign([FromBody] SignDocumentSubjectApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new SignDocumentSubjectCommand(
            new SignDocumentSubjectInput(
                request.SignatureRequestId, request.SubjectType, request.SubjectId, request.RegisterEntryId,
                request.ControlledDocumentId, request.SignatureMeaning, request.MeaningStatement,
                request.SignatureMethod, request.SignerRole, request.SignatureEvidenceReference,
                request.ExternalProviderReference, request.AuthenticationContextReference,
                request.RepositoryAssessmentId),
            CorrelationId), ct));

    /// <summary>
    /// Recomputes the subject's canonical metadata fingerprint and compares it with the one captured at signing.
    /// This checks OBJECT INTEGRITY ONLY — it performs no certificate or provider validation.
    /// </summary>
    [HttpPost("signatures/{id:guid}/verify")]
    [HasPermission(ElectronicSignaturePermissions.SignaturesVerify)]
    public async Task<IActionResult> Verify(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new VerifySignatureCommand(id, CorrelationId), ct));

    [HttpPost("signatures/{id:guid}/invalidate")]
    [HasPermission(ElectronicSignaturePermissions.SignaturesInvalidate)]
    public async Task<IActionResult> Invalidate(
        Guid id, [FromBody] InvalidateSignatureApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new InvalidateSignatureCommand(id,
            new InvalidateSignatureInput(request.Reason), CorrelationId), ct));

    private string CorrelationId =>
        string.IsNullOrWhiteSpace(_correlationContext.CorrelationId) ? HttpContext.TraceIdentifier : _correlationContext.CorrelationId!;
}
