using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Models.DocumentManagement;
using Diten.Platform.API.Observability;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.DocumentManagementAccessMatrix;
using Diten.Platform.Application.Features.DocumentManagementAccessMatrix.Commands;
using Diten.Platform.Application.Features.DocumentManagementAccessMatrix.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers;

/// <summary>MOD-0029-FU04 — TenantShell document access matrix API. Thin MediatR controller.</summary>
[ApiController]
[Route("api/v1/document-management")]
[Authorize]
public sealed class DocumentManagementAccessPoliciesController : CustomBaseController
{
    private readonly IMediator _mediator;
    private readonly ICorrelationContext _correlationContext;

    public DocumentManagementAccessPoliciesController(IMediator mediator, ICorrelationContext correlationContext)
    {
        _mediator = mediator;
        _correlationContext = correlationContext;
    }

    [HttpGet("access-policies")]
    [HasPermission(DocumentManagementAccessPermissions.View)]
    public async Task<IActionResult> List(
        [FromQuery] string? targetType,
        [FromQuery] string? targetId,
        [FromQuery] string? principalType,
        [FromQuery] string? principalId,
        [FromQuery] string? effect,
        [FromQuery] string? action,
        [FromQuery] string? status,
        CancellationToken ct)
    {
        var filter = new DocumentAccessPolicyListFilter(targetType, targetId, principalType, principalId, effect, action, status);
        return CreateActionResultInstance(await _mediator.Send(new GetDocumentAccessPolicyListQuery(filter, CorrelationId), ct));
    }

    [HttpGet("access-policies/{id:guid}")]
    [HasPermission(DocumentManagementAccessPermissions.View)]
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetDocumentAccessPolicyByIdQuery(id, CorrelationId), ct));

    [HttpPost("access-policies")]
    [HasPermission(DocumentManagementAccessPermissions.Manage)]
    public async Task<IActionResult> Create([FromBody] DocumentAccessPolicyApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateDocumentAccessPolicyCommand(ToInput(request), CorrelationId), ct));

    [HttpPut("access-policies/{id:guid}")]
    [HasPermission(DocumentManagementAccessPermissions.Manage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] DocumentAccessPolicyApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new UpdateDocumentAccessPolicyCommand(id, ToInput(request), CorrelationId), ct));

    [HttpDelete("access-policies/bulk")]
    [HasPermission(DocumentManagementAccessPermissions.Manage)]
    public async Task<IActionResult> BulkDelete([FromBody] List<Guid> ids, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new BulkDeleteDocumentAccessPolicyCommand(ids ?? [], CorrelationId), ct));

    [HttpDelete("access-policies/{id:guid}")]
    [HasPermission(DocumentManagementAccessPermissions.Manage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DeleteDocumentAccessPolicyCommand(id, CorrelationId), ct));

    [HttpGet("access-policies/effective")]
    [HasPermission(DocumentManagementAccessPermissions.Preview)]
    public async Task<IActionResult> Effective(
        [FromQuery] string targetType,
        [FromQuery] string targetId,
        [FromQuery] string principalType,
        [FromQuery] string principalId,
        CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetEffectiveDocumentAccessQuery(targetType, targetId, principalType, principalId, CorrelationId), ct));

    [HttpPost("access-policies/effective/batch")]
    [HasPermission(DocumentManagementAccessPermissions.Preview)]
    public async Task<IActionResult> EffectiveBatch([FromBody] EffectiveAccessBatchApiRequest request, CancellationToken ct)
    {
        var input = new EffectiveDocumentAccessBatchInput(
            request.PrincipalType,
            request.PrincipalId,
            (request.Targets ?? []).Select(t => new EffectiveDocumentAccessRequestItem(t.TargetType, t.TargetId)).ToList());
        return CreateActionResultInstance(await _mediator.Send(new GetEffectiveDocumentAccessBatchQuery(input, CorrelationId), ct));
    }

    [HttpGet("access-target-options")]
    [HasPermission(DocumentManagementAccessPermissions.View)]
    public async Task<IActionResult> TargetOptions(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetDocumentAccessTargetOptionsQuery(CorrelationId), ct));

    [HttpGet("access-principal-options")]
    [HasPermission(DocumentManagementAccessPermissions.View)]
    public async Task<IActionResult> PrincipalOptions(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetDocumentAccessPrincipalOptionsQuery(CorrelationId), ct));

    private static DocumentAccessPolicyInput ToInput(DocumentAccessPolicyApiRequest request) => new(
        request.TargetType,
        request.TargetId,
        request.PrincipalType,
        request.PrincipalId,
        request.Actions ?? [],
        request.Effect,
        request.InheritFromParent,
        request.SourcePolicyId,
        request.ValidFrom,
        request.ValidTo,
        request.Status,
        request.Reason);

    private string CorrelationId =>
        string.IsNullOrWhiteSpace(_correlationContext.CorrelationId) ? HttpContext.TraceIdentifier : _correlationContext.CorrelationId!;
}
