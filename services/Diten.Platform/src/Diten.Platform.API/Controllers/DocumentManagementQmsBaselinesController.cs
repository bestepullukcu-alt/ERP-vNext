using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Models.DocumentManagement;
using Diten.Platform.API.Observability;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.DocumentManagementContract;
using Diten.Platform.Application.Features.DocumentManagementQmsBaseline;
using Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Commands;
using Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers;

/// <summary>
/// MOD-0028-FU02 — thin tenant-facing controller for QMS folder baseline import/governance under the FU01 route
/// family. Every action delegates to MediatR and returns the shared Response&lt;T&gt; envelope with a body-level
/// correlation id; authorization is enforced per action by the central HasPermissionAttribute.
/// </summary>
[ApiController]
[Route("api/v1/document-management/qms-baselines")]
[Authorize]
public sealed class DocumentManagementQmsBaselinesController : CustomBaseController
{
    private readonly IMediator _mediator;
    private readonly ICorrelationContext _correlationContext;

    public DocumentManagementQmsBaselinesController(IMediator mediator, ICorrelationContext correlationContext)
    {
        _mediator = mediator;
        _correlationContext = correlationContext;
    }

    [HttpPost("import/dry-run")]
    [HasPermission(QmsBaselinePermissions.Import)]
    public async Task<IActionResult> DryRunImport([FromBody] QmsBaselineDryRunRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(
            new DryRunQmsBaselineImportCommand(
                request.FileName, request.Format, request.ContentBase64, request.SourceBaselineKey, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("import/commit")]
    [HasPermission(QmsBaselinePermissions.Import)]
    public async Task<IActionResult> CommitImport([FromBody] QmsBaselineCommitRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(
            new CommitQmsBaselineImportCommand(
                request.FileName, request.Format, request.ContentBase64, request.SourceBaselineKey,
                request.BaselineVersion, request.ChangeSummary, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("manual")]
    [HasPermission(QmsBaselinePermissions.Create)]
    public async Task<IActionResult> CreateManualBaseline([FromBody] ManualQmsBaselineRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(
            new CreateManualQmsBaselineCommand(
                new ManualQmsBaselineRequestModel(
                    request.BaselineVersion,
                    request.Name,
                    request.ChangeSummary,
                    request.EffectiveDate),
                CorrelationId),
            ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("")]
    [HasPermission(QmsBaselinePermissions.View)]
    public async Task<IActionResult> GetBaselines(CancellationToken ct)
    {
        var response = await _mediator.Send(new GetQmsBaselineListQuery(CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("{id:guid}")]
    [HasPermission(QmsBaselinePermissions.View)]
    public async Task<IActionResult> GetBaseline(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetQmsBaselineByIdQuery(id, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("{id:guid}/publish")]
    [HasPermission(QmsBaselinePermissions.Publish)]
    public async Task<IActionResult> PublishBaseline(Guid id, [FromBody] QmsBaselinePublishRequest? request, CancellationToken ct)
    {
        var response = await _mediator.Send(
            new PublishQmsBaselineCommand(id, request?.ExpectedVersion ?? 0, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("{id:guid}/definitions")]
    [HasPermission(DocumentManagementPermissions.CollectionDefinitionsList)]
    public async Task<IActionResult> GetDefinitions(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetQmsBaselineDefinitionsQuery(id, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("{id:guid}/definitions/{canonicalId}")]
    [HasPermission(DocumentManagementPermissions.CollectionDefinitionsView)]
    public async Task<IActionResult> GetDefinition(Guid id, string canonicalId, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetQmsBaselineDefinitionByCanonicalIdQuery(id, canonicalId, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("{id:guid}/definitions")]
    [HasPermission(QmsBaselinePermissions.CollectionDefinitionsCreate)]
    public async Task<IActionResult> CreateDefinition(Guid id, [FromBody] QmsCollectionDefinitionUpsertRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(
            new CreateQmsBaselineDefinitionCommand(id, ToApplicationModel(request), CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPut("{id:guid}/definitions/{canonicalId}")]
    [HasPermission(QmsBaselinePermissions.CollectionDefinitionsEdit)]
    public async Task<IActionResult> UpdateDefinition(
        Guid id,
        string canonicalId,
        [FromBody] QmsCollectionDefinitionUpsertRequest request,
        CancellationToken ct)
    {
        var response = await _mediator.Send(
            new UpdateQmsBaselineDefinitionCommand(id, canonicalId, ToApplicationModel(request), CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPatch("{id:guid}/definitions/{canonicalId}/move")]
    [HasPermission(QmsBaselinePermissions.CollectionDefinitionsMove)]
    public async Task<IActionResult> MoveDefinition(
        Guid id,
        string canonicalId,
        [FromBody] QmsCollectionDefinitionMoveRequest request,
        CancellationToken ct)
    {
        var response = await _mediator.Send(
            new MoveQmsBaselineDefinitionCommand(
                id,
                canonicalId,
                new QmsCollectionDefinitionMoveModel(request.ParentCanonicalId, request.DisplayOrder, request.VersionToken),
                CorrelationId),
            ct);
        return CreateActionResultInstance(response);
    }

    [HttpDelete("{id:guid}/definitions/{canonicalId}")]
    [HasPermission(QmsBaselinePermissions.CollectionDefinitionsDelete)]
    public async Task<IActionResult> DeleteDefinition(Guid id, string canonicalId, [FromQuery] int versionToken, CancellationToken ct)
    {
        var response = await _mediator.Send(new DeleteQmsBaselineDefinitionCommand(id, canonicalId, versionToken, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("{id:guid}/validate")]
    [HasPermission(QmsBaselinePermissions.Validate)]
    public async Task<IActionResult> ValidateDraft(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new ValidateQmsBaselineDraftCommand(id, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    private string CorrelationId =>
        string.IsNullOrWhiteSpace(_correlationContext.CorrelationId)
            ? HttpContext.TraceIdentifier
            : _correlationContext.CorrelationId!;

    private static QmsCollectionDefinitionUpsertModel ToApplicationModel(QmsCollectionDefinitionUpsertRequest request) =>
        new(
            request.Name,
            request.ParentCanonicalId,
            request.PurposeScope,
            request.RequiredByScope,
            request.AllowedDocClass,
            request.DefaultClassificationLevel,
            request.DefaultRetentionHint,
            request.DisplayOrder,
            request.AllowsManualChildren,
            request.TemplatesAllowed,
            request.IsMandatory,
            request.IsProtected,
            request.VersionToken);
}
