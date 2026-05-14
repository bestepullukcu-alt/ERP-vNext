using Diten.BuildingBlocks.InterfaceRegistry.Abstractions;
using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.InterfaceRegistry;
using Diten.Platform.Application.Features.InterfaceRegistry.Commands;
using Diten.Platform.Application.Features.InterfaceRegistry.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Platform;

[ApiController]
[Route("api/platform/interface-registry")]
[Authorize(Policy = "PlatformActor")]
public sealed class InterfaceRegistryController : CustomBaseController
{
    private readonly IMediator _mediator;

    public InterfaceRegistryController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("manifests/import")]
    [HasPermission("Platform.InterfaceRegistry.Import")]
    public async Task<IActionResult> ImportManifest([FromBody] InterfaceManifestDocument manifest, CancellationToken ct)
    {
        var response = await _mediator.Send(new ImportInterfaceManifestRequest(manifest), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("discovery-batches")]
    [HasPermission("Platform.InterfaceRegistry.Read")]
    public async Task<IActionResult> GetDiscoveryBatches(CancellationToken ct)
    {
        var response = await _mediator.Send(new GetInterfaceDiscoveryBatchesRequest(), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("discovery-batches/{batchId:guid}")]
    [HasPermission("Platform.InterfaceRegistry.Read")]
    public async Task<IActionResult> GetDiscoveryBatch(Guid batchId, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetInterfaceDiscoveryBatchByIdRequest(batchId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("discovery-batches/{batchId:guid}/diffs")]
    [HasPermission("Platform.InterfaceRegistry.Read")]
    public async Task<IActionResult> GetDiscoveryDiffs(Guid batchId, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetInterfaceDiscoveryDiffItemsRequest(batchId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("interfaces")]
    [HasPermission("Platform.InterfaceRegistry.Read")]
    public async Task<IActionResult> GetInterfaces(CancellationToken ct)
    {
        var response = await _mediator.Send(new GetInterfaceDefinitionsRequest(), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("interfaces/{interfaceCode}/snapshot")]
    [HasPermission("Platform.InterfaceRegistry.Read")]
    public async Task<IActionResult> GetActiveSnapshot(string interfaceCode, [FromQuery] string version, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetInterfaceDefinitionSnapshotRequest(interfaceCode, version), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("discovery-batches/{batchId:guid}/confirm")]
    [HasPermission("Platform.InterfaceRegistry.Review")]
    public async Task<IActionResult> ConfirmBatch(Guid batchId, CancellationToken ct)
    {
        var response = await _mediator.Send(new ConfirmInterfaceDiscoveryBatchRequest(batchId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("discovery-batches/{batchId:guid}/reject")]
    [HasPermission("Platform.InterfaceRegistry.Review")]
    public async Task<IActionResult> RejectBatch(Guid batchId, [FromBody] InterfaceReviewDecisionRequest? request, CancellationToken ct)
    {
        var response = await _mediator.Send(new RejectInterfaceDiscoveryBatchRequest(batchId, request?.ReviewReason ?? string.Empty), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("diffs/{diffItemId:guid}/confirm")]
    [HasPermission("Platform.InterfaceRegistry.Review")]
    public async Task<IActionResult> ConfirmDiffItem(Guid diffItemId, CancellationToken ct)
    {
        var response = await _mediator.Send(new ConfirmInterfaceDiffItemRequest(diffItemId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("diffs/{diffItemId:guid}/reject")]
    [HasPermission("Platform.InterfaceRegistry.Review")]
    public async Task<IActionResult> RejectDiffItem(Guid diffItemId, [FromBody] InterfaceReviewDecisionRequest? request, CancellationToken ct)
    {
        var response = await _mediator.Send(new RejectInterfaceDiffItemRequest(diffItemId, request?.ReviewReason ?? string.Empty), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("interfaces/{interfaceCode}/deprecate")]
    [HasPermission("Platform.InterfaceRegistry.Deprecate")]
    public async Task<IActionResult> DeprecateInterface(string interfaceCode, [FromBody] DeprecateInterfaceRequestBody? request, CancellationToken ct)
    {
        var response = await _mediator.Send(new DeprecateInterfaceRequest(interfaceCode, request?.Version ?? string.Empty, request?.Reason ?? string.Empty), ct);
        return CreateActionResultInstance(response);
    }
}
