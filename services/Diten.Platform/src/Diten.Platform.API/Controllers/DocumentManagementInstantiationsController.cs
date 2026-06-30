using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Models.DocumentManagement;
using Diten.Platform.API.Observability;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.DocumentManagementInstantiation;
using Diten.Platform.Application.Features.DocumentManagementInstantiation.Commands;
using Diten.Platform.Application.Features.DocumentManagementInstantiation.Queries;
using Diten.Platform.Domain.Enums.DocumentManagement;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers;

[ApiController]
[Route("api/v1/document-management")]
[Authorize]
public sealed class DocumentManagementInstantiationsController : CustomBaseController
{
    private readonly IMediator _mediator;
    private readonly ICorrelationContext _correlationContext;

    public DocumentManagementInstantiationsController(IMediator mediator, ICorrelationContext correlationContext)
    {
        _mediator = mediator;
        _correlationContext = correlationContext;
    }

    [HttpGet("instantiations/prerequisites")]
    [HasPermission(DocumentManagementInstantiationPermissions.BaselineReleasesView)]
    public async Task<IActionResult> GetPrerequisites(CancellationToken ct)
    {
        var response = await _mediator.Send(new GetInstantiationPrerequisitesQuery(CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("instantiations/dry-run")]
    [HasPermission(DocumentManagementInstantiationPermissions.InstantiationsDryRun)]
    public async Task<IActionResult> DryRun([FromBody] InstantiationActionRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(
            new DryRunInstantiationCommand(request.BaselineReleaseId, ToScope(request.Scope), ToSelection(request), CorrelationId),
            ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("instantiations/execute")]
    [HasPermission(DocumentManagementInstantiationPermissions.InstantiationsExecute)]
    public async Task<IActionResult> Execute([FromBody] InstantiationActionRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(
            new ExecuteInstantiationCommand(request.BaselineReleaseId, ToScope(request.Scope), ToSelection(request), CorrelationId),
            ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("instantiations/{operationId:guid}")]
    [HasPermission(DocumentManagementInstantiationPermissions.BaselinesInstantiate)]
    public async Task<IActionResult> GetOperation(Guid operationId, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetInstantiationOperationQuery(operationId, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("instantiations/{operationId:guid}/retry")]
    [HasPermission(DocumentManagementInstantiationPermissions.CollectionInstancesRetry)]
    public async Task<IActionResult> Retry(Guid operationId, [FromBody] RetryInstantiationRequest? request, CancellationToken ct)
    {
        var response = await _mediator.Send(
            new RetryInstantiationCommand(operationId, request?.NodeKeys ?? [], CorrelationId),
            ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("collection-instances")]
    [HasPermission(DocumentManagementInstantiationPermissions.CollectionInstancesView)]
    public async Task<IActionResult> GetCollectionInstances(
        [FromQuery] Guid? companyId,
        [FromQuery] Guid? baselineReleaseId,
        [FromQuery] string? instanceToken,
        [FromQuery] string? requiredAction,
        CancellationToken ct)
    {
        var response = await _mediator.Send(
            new GetCollectionInstancesQuery(companyId, baselineReleaseId, instanceToken, requiredAction, CorrelationId),
            ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("collection-instances/{id:guid}")]
    [HasPermission(DocumentManagementInstantiationPermissions.CollectionInstancesView)]
    public async Task<IActionResult> GetCollectionInstance(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetCollectionInstanceByIdQuery(id, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    // MOD-0028-FU05 follow-up: soft-archive a company instance node + its descendant subtree.
    // Interim gate reuses the instantiate/execute privilege; a dedicated collection-instances.archive
    // permission is a separate MOD-0018/security seed task.
    [HttpPost("collection-instances/{id:guid}/archive")]
    [HasPermission(DocumentManagementInstantiationPermissions.InstantiationsExecute)]
    public async Task<IActionResult> ArchiveCollectionInstance(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new ArchiveCollectionInstanceCommand(id, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    // MOD-0028-FU05 follow-up: restore (un-archive) a company instance node + its subtree.
    [HttpPost("collection-instances/{id:guid}/restore")]
    [HasPermission(DocumentManagementInstantiationPermissions.InstantiationsExecute)]
    public async Task<IActionResult> RestoreCollectionInstance(Guid id, CancellationToken ct)
    {
        var response = await _mediator.Send(new RestoreCollectionInstanceCommand(id, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    private string CorrelationId =>
        string.IsNullOrWhiteSpace(_correlationContext.CorrelationId)
            ? HttpContext.TraceIdentifier
            : _correlationContext.CorrelationId!;

    private static InstantiationScopeRequest ToScope(InstantiationScopeApiRequest request) =>
        new(request.CompanyId, request.PlantId, request.BusinessUnitId, request.InstanceToken);

    private static InstantiationSelectionRequest ToSelection(InstantiationActionRequest request)
    {
        var mode = request.SelectionMode?.Trim().ToUpperInvariant() switch
        {
            null or "" or "FULL_TREE" => InstantiationSelectionMode.FullTree,
            "SELECTED_BRANCHES" => InstantiationSelectionMode.SelectedBranches,
            _ => (InstantiationSelectionMode)999
        };
        var selected = request.SelectedCanonicalIds?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToList() ?? [];
        return new InstantiationSelectionRequest(mode, selected, request.IncludeDescendants, request.IncludeRequiredAncestors);
    }
}
