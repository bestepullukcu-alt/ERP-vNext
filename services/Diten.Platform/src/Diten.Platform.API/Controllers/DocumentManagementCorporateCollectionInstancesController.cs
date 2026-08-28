using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Models.DocumentManagement;
using Diten.Platform.API.Observability;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.DocumentManagementCorporateCollectionInstances.Commands;
using Diten.Platform.Application.Features.DocumentManagementCorporateCollectionInstances.Queries;
using Diten.Platform.Application.Features.DocumentManagementInstantiation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers;

[ApiController]
[Route("api/v1/document-management/corporate-collection-instances")]
[Authorize]
public sealed class DocumentManagementCorporateCollectionInstancesController : CustomBaseController
{
    private readonly IMediator _mediator;
    private readonly ICorrelationContext _correlationContext;

    public DocumentManagementCorporateCollectionInstancesController(
        IMediator mediator,
        ICorrelationContext correlationContext)
    {
        _mediator = mediator;
        _correlationContext = correlationContext;
    }

    [HttpPost("provision")]
    [HasPermission(DocumentManagementInstantiationPermissions.InstantiationsExecute)]
    public async Task<IActionResult> Provision(
        [FromBody] ProvisionCorporateCollectionInstanceRequest request,
        CancellationToken ct)
    {
        var response = await _mediator.Send(new ProvisionCorporateCollectionInstanceCommand(
            request.BaselineReleaseId,
            request.CorporateOwnerId,
            request.IdempotencyKey,
            request.DisplayName,
            request.Description,
            CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("{collectionInstanceId:guid}")]
    [HasPermission(DocumentManagementInstantiationPermissions.CollectionInstancesView)]
    public async Task<IActionResult> Get(Guid collectionInstanceId, CancellationToken ct)
    {
        var response = await _mediator.Send(
            new GetCorporateCollectionInstanceQuery(collectionInstanceId, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet]
    [HasPermission(DocumentManagementInstantiationPermissions.CollectionInstancesView)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? baselineReleaseId,
        [FromQuery] Guid? corporateOwnerId,
        CancellationToken ct)
    {
        var response = await _mediator.Send(
            new ListCorporateCollectionInstancesQuery(baselineReleaseId, corporateOwnerId, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("provisioning-operations/{operationId:guid}")]
    [HasPermission(DocumentManagementInstantiationPermissions.CollectionInstancesView)]
    public async Task<IActionResult> GetOperation(Guid operationId, CancellationToken ct)
    {
        var response = await _mediator.Send(
            new GetCorporateCollectionProvisioningOperationQuery(operationId, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("provisioning-operations/{operationId:guid}/retry")]
    [HasPermission(DocumentManagementInstantiationPermissions.CollectionInstancesRetry)]
    public async Task<IActionResult> Retry(Guid operationId, CancellationToken ct)
    {
        var response = await _mediator.Send(
            new RetryCorporateCollectionProvisioningCommand(operationId, CorrelationId), ct);
        return CreateActionResultInstance(response);
    }

    private string CorrelationId =>
        string.IsNullOrWhiteSpace(_correlationContext.CorrelationId)
            ? HttpContext.TraceIdentifier
            : _correlationContext.CorrelationId!;
}
