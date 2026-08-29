using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Models.DocumentManagement;
using Diten.Platform.API.Observability;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments;
using Diten.Platform.Application.Features.DocumentManagementControlledDocumentRegistration;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Commands;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers;

/// <summary>
/// MOD-0029-FU06 — TenantShell Document Master Register API (GMG-QMS-SOP-0001 §18 LOG-0001, §20). Thin controller;
/// dispatches via MediatR. Layer 1 RBAC is enforced here via <c>[HasPermission]</c>. This FU REUSES the already-seeded
/// controlled-documents view/create keys (no AuthService seed change); the dedicated
/// <see cref="DocumentMasterRegisterPermissions"/> keys should be seeded and switched in FU06A/FU07. TenantId is never
/// read from the client — it is resolved server-side.
/// </summary>
[ApiController]
[Route("api/v1/document-management")]
[Authorize]
public sealed class DocumentManagementMasterRegisterController : CustomBaseController
{
    private readonly IMediator _mediator;
    private readonly ICorrelationContext _correlationContext;

    public DocumentManagementMasterRegisterController(IMediator mediator, ICorrelationContext correlationContext)
    {
        _mediator = mediator;
        _correlationContext = correlationContext;
    }

    [HttpGet("document-master-register")]
    [HasPermission(DocumentMasterRegisterPermissions.View)]
    public async Task<IActionResult> List(
        [FromQuery] string? registerStatus,
        [FromQuery] string? lifecycleStatus,
        [FromQuery] string? criticality,
        [FromQuery] string? documentClass,
        [FromQuery] Guid? ownerCompanyId,
        CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(
            new GetMasterRegisterListQuery(registerStatus, lifecycleStatus, criticality, documentClass, ownerCompanyId, CorrelationId), ct));

    [HttpGet("document-master-register/summary")]
    [HasPermission(DocumentMasterRegisterPermissions.View)]
    public async Task<IActionResult> Summary(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetMasterRegisterSummaryQuery(CorrelationId), ct));

    [HttpGet("document-master-register/{id:guid}")]
    [HasPermission(DocumentMasterRegisterPermissions.View)]
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetMasterRegisterEntryByIdQuery(id, CorrelationId), ct));

    [HttpPost("document-master-register")]
    [HasPermission(DocumentMasterRegisterPermissions.Manage)]
    public async Task<IActionResult> Create([FromBody] CreateMasterRegisterEntryApiRequest request, CancellationToken ct)
    {
        var input = new CreateMasterRegisterEntryInput(
            request.DocumentTitle, request.DocumentClass, request.Criticality, request.DocumentType,
            request.PermanentUid, request.DocumentCode, request.LegacyCode,
            request.ProcessOwnerRole, request.ProcessOwnerUserId, request.AuthorUserId, request.OwnerFunction, request.OwnerCompanyId,
            request.GoverningLanguage, request.ReviewCycleMonths, request.RetentionClass,
            request.IsControlledDocument, request.IsRecord, request.IsExternalDocument, request.IsTemplate, request.IsVariant,
            request.ParentDocumentUid, request.ParentDocumentCode, request.SourceSystem, request.SourceLegacyId);
        return CreateActionResultInstance(await _mediator.Send(new CreateMasterRegisterEntryCommand(input, CorrelationId), ct));
    }

    [HttpPut("document-master-register/{id:guid}")]
    [HasPermission(DocumentMasterRegisterPermissions.Manage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateMasterRegisterMetadataApiRequest request, CancellationToken ct)
    {
        var input = new UpdateMasterRegisterMetadataInput(
            request.DocumentTitle, request.DocumentClass, request.Criticality, request.DocumentType, request.LegacyCode,
            request.ProcessOwnerRole, request.ProcessOwnerUserId, request.AuthorUserId, request.OwnerFunction, request.OwnerCompanyId,
            request.GoverningLanguage, request.ReviewCycleMonths, request.RetentionClass,
            request.ApprovedRepositoryId, request.ApprovedRepositoryName, request.ApprovedRepositoryPath,
            request.ParentDocumentUid, request.ParentDocumentCode);
        return CreateActionResultInstance(await _mediator.Send(new UpdateMasterRegisterMetadataCommand(id, input, CorrelationId), ct));
    }

    [HttpPost("document-master-register/{id:guid}/link-controlled-document")]
    [HasPermission(DocumentMasterRegisterPermissions.Link)]
    [HasPermission(ControlledDocumentRegistrationPermissions.Reconcile)]
    public async Task<IActionResult> LinkControlledDocument(Guid id, [FromBody] LinkControlledDocumentApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(
            new LinkControlledDocumentToRegisterEntryCommand(
                id,
                new LinkControlledDocumentInput(request.ControlledDocumentId, request.ReconciliationReason),
                CorrelationId), ct));

    private string CorrelationId =>
        string.IsNullOrWhiteSpace(_correlationContext.CorrelationId) ? HttpContext.TraceIdentifier : _correlationContext.CorrelationId!;
}
