using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Models.DocumentManagement;
using Diten.Platform.API.Observability;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.DocumentManagementControlledDocumentRegistration;
using Diten.Platform.Application.Features.DocumentManagementControlledDocumentRegistration.Commands;
using Diten.Platform.Application.Features.DocumentManagementControlledDocumentRegistration.Queries;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments;
using Diten.Platform.Application.Features.Lookups.Queries;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister;
using Diten.Platform.Domain.Enums.DocumentManagement;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers;

[ApiController]
[Route("api/v1/document-management")]
[Authorize]
public sealed class DocumentManagementControlledDocumentRegistrationController : CustomBaseController
{
    private readonly IMediator _mediator;
    private readonly ICorrelationContext _correlation;

    public DocumentManagementControlledDocumentRegistrationController(IMediator mediator, ICorrelationContext correlation)
    {
        _mediator = mediator;
        _correlation = correlation;
    }

    [HttpPost("controlled-document-registrations")]
    [HasPermission(ControlledDocumentRegistrationPermissions.Create)]
    [HasPermission(DocumentMasterRegisterPermissions.Manage)]
    [HasPermission(DocumentMasterRegisterPermissions.Link)]
    [HasPermission(DocumentManagementControlledDocumentsPermissions.ControlledDocumentsCreate)]
    public async Task<IActionResult> Create(
        [FromBody] CreateControlledDocumentRegistrationApiRequest request,
        CancellationToken ct)
    {
        var scope = string.IsNullOrWhiteSpace(request.DocumentScope)
            ? DocumentScope.Company
            : Enum.TryParse<DocumentScope>(request.DocumentScope, true, out var parsedScope)
                ? parsedScope
                : (DocumentScope)(-1);
        var kind = string.IsNullOrWhiteSpace(request.Kind)
            ? RegistrationKind.ControlledDocument
            : Enum.TryParse<RegistrationKind>(request.Kind, true, out var parsedKind)
                ? parsedKind
                : (RegistrationKind)(-1);
        var variantType = string.IsNullOrWhiteSpace(request.VariantType)
            ? DocumentVariantType.Translation
            : Enum.TryParse<DocumentVariantType>(request.VariantType, true, out var parsedVariantType)
                ? parsedVariantType
                : (DocumentVariantType)(-1);
        var input = new CreateControlledDocumentRegistrationInput(
            request.IdempotencyKey, request.DocumentTitle, request.DocumentClass, request.Criticality,
            request.DocumentType, request.Description, request.Tags, request.GoverningLanguage,
            request.OwnerFunction, request.OwnerCompanyId, request.ProcessOwnerRole, request.ProcessOwnerUserId,
            request.ReviewCycleMonths, request.RetentionClass, request.CompanyId, request.CollectionInstanceId,
            new(request.InitialFile.FileName, request.InitialFile.MediaType, request.InitialFile.ContentBase64))
        {
            DocumentScope = scope,
            CorporateOwnerId = request.CorporateOwnerId,
            FolderId = request.FolderId,
            AuthorUserId = request.AuthorUserId,
            GoverningLanguageId = request.GoverningLanguageId,
            RetentionClassId = request.RetentionClassId,
            Kind = kind,
            RecordCode = request.RecordCode,
            ParentRegisterEntryId = request.ParentRegisterEntryId,
            VariantType = variantType,
            LanguageCode = request.LanguageCode,
            CountryCode = request.CountryCode,
            SiteCode = request.SiteCode
        };
        return CreateActionResultInstance(await _mediator.Send(
            new CreateControlledDocumentRegistrationCommand(input, CorrelationId), ct));
    }

    [HttpGet("controlled-document-registrations/{operationId:guid}")]
    [HasPermission(ControlledDocumentRegistrationPermissions.View)]
    public async Task<IActionResult> GetOperation(Guid operationId, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(
            new GetControlledDocumentRegistrationOperationQuery(operationId, CorrelationId), ct));

    [HttpGet("controlled-document-registrations/governed-languages")]
    [HasPermission(DocumentManagementControlledDocumentsPermissions.ControlledDocumentsView)]
    public async Task<IActionResult> GetGovernedLanguages(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetLocaleLookupQuery(), ct));

    [HttpPost("controlled-document-registrations/{operationId:guid}/retry")]
    [HasPermission(ControlledDocumentRegistrationPermissions.Reconcile)]
    public async Task<IActionResult> Retry(Guid operationId, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(
            new RetryControlledDocumentRegistrationCommand(operationId, CorrelationId), ct));

    [HttpGet("controlled-documents/{controlledDocumentId:guid}/master-register")]
    [HasPermission(DocumentManagementControlledDocumentsPermissions.ControlledDocumentsView)]
    [HasPermission(DocumentMasterRegisterPermissions.View)]
    public async Task<IActionResult> GetMasterRegister(Guid controlledDocumentId, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(
            new GetMasterRegisterByControlledDocumentQuery(controlledDocumentId, CorrelationId), ct));

    private string CorrelationId =>
        string.IsNullOrWhiteSpace(_correlation.CorrelationId) ? HttpContext.TraceIdentifier : _correlation.CorrelationId!;
}
