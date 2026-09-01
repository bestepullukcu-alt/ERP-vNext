using Diten.ManagementGovernanceService.Api.LocalTest;
using Diten.ManagementGovernanceService.Application.Features.Dws;
using Diten.ManagementGovernanceService.Application.Features.Dws.Commands;
using Diten.ManagementGovernanceService.Application.Features.Dws.Queries;
using Diten.ManagementGovernanceService.Domain.Modules.Dws;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.ManagementGovernanceService.Api.Controllers;

[Authorize(AuthenticationSchemes = DwsLocalTestAuthenticationDefaults.Scheme)]
[Route("api/dws/structures")]
public sealed class DwsStructuresController(ISender sender, IDwsLocalTestTrustedContextResolver contexts) : CustomBaseController
{
    [HttpPost]
    public Task<IActionResult> Create([FromBody] CreateStructureRequest request, CancellationToken token) =>
        Send(new CreateStructureCommand(request, CommandContext()), token);

    [HttpPut("{id:guid}/metadata")]
    public Task<IActionResult> UpdateMetadata(Guid id, [FromBody] UpdateStructureMetadataRequest request, CancellationToken token) =>
        id == request.StructureDefinitionId
            ? Send(new UpdateStructureMetadataCommand(request, CommandContext()), token)
            : Invalid<UpdateStructureMetadataResult>();

    [HttpPost("{id:guid}/nodes")]
    public Task<IActionResult> AddNode(Guid id, [FromBody] AddStructureNodeRequest request, CancellationToken token) =>
        id == request.StructureDefinitionId
            ? Send(new AddStructureNodeCommand(request, CommandContext()), token)
            : Invalid<AddStructureNodeResult>();

    [HttpPost("{id:guid}/nodes/{logicalNodeId:guid}/move")]
    public Task<IActionResult> MoveNode(Guid id, Guid logicalNodeId, [FromBody] MoveStructureNodeRequest request, CancellationToken token) =>
        id == request.StructureDefinitionId && logicalNodeId == request.LogicalNodeId
            ? Send(new MoveStructureNodeCommand(request, CommandContext()), token)
            : Invalid<MoveStructureNodeResult>();

    [HttpPost("{id:guid}/nodes/{logicalNodeId:guid}/reorder")]
    public Task<IActionResult> ReorderNode(Guid id, Guid logicalNodeId, [FromBody] ReorderStructureNodeRequest request, CancellationToken token) =>
        id == request.StructureDefinitionId && logicalNodeId == request.LogicalNodeId
            ? Send(new ReorderStructureNodeCommand(request, CommandContext()), token)
            : Invalid<ReorderStructureNodeResult>();

    [HttpDelete("{id:guid}/nodes/{logicalNodeId:guid}")]
    public Task<IActionResult> RemoveNode(Guid id, Guid logicalNodeId, [FromBody] RemoveStructureNodeRequest request, CancellationToken token) =>
        id == request.StructureDefinitionId && logicalNodeId == request.LogicalNodeId
            ? Send(new RemoveStructureNodeCommand(request, CommandContext()), token)
            : Invalid<RemoveStructureNodeResult>();

    [HttpPost("{id:guid}/dependencies")]
    public Task<IActionResult> AddDependency(Guid id, [FromBody] AddStructuralDependencyRequest request, CancellationToken token) =>
        id == request.StructureDefinitionId
            ? Send(new AddStructuralDependencyCommand(request, CommandContext()), token)
            : Invalid<AddStructuralDependencyResult>();

    [HttpDelete("{id:guid}/dependencies")]
    public Task<IActionResult> RemoveDependency(Guid id, [FromBody] RemoveStructuralDependencyRequest request, CancellationToken token) =>
        id == request.StructureDefinitionId
            ? Send(new RemoveStructuralDependencyCommand(request, CommandContext()), token)
            : Invalid<RemoveStructuralDependencyResult>();

    [HttpPost("{id:guid}/baselines")]
    public Task<IActionResult> CreateBaseline(Guid id, [FromBody] CreateStructureBaselineRequest request, CancellationToken token) =>
        id == request.StructureDefinitionId
            ? Send(new CreateStructureBaselineCommand(request, CommandContext()), token)
            : Invalid<CreateStructureBaselineResult>();

    [HttpPost("{id:guid}/revisions")]
    public Task<IActionResult> CreateRevision(Guid id, [FromBody] CreateNextStructureRevisionRequest request, CancellationToken token) =>
        id == request.StructureDefinitionId
            ? Send(new CreateNextStructureRevisionCommand(request, CommandContext()), token)
            : Invalid<CreateNextStructureRevisionResult>();

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken token) =>
        Send(new GetStructureByIdQuery(id, QueryContext()), token);

    [HttpGet("{id:guid}/tree")]
    public Task<IActionResult> Tree(Guid id, [FromQuery] int? revisionNumber, CancellationToken token) =>
        Send(new GetStructureTreeQuery(id, revisionNumber, QueryContext()), token);

    [HttpGet("{id:guid}/validation")]
    public Task<IActionResult> Validate(Guid id, [FromQuery] int? revisionNumber, CancellationToken token) =>
        Send(new ValidateStructureQuery(id, revisionNumber, QueryContext()), token);

    [HttpGet("{id:guid}/revision-comparison")]
    public Task<IActionResult> CompareRevisions(Guid id, [FromQuery] int left, [FromQuery] int right, CancellationToken token) =>
        Send(new CompareStructureRevisionsQuery(id, left, right, QueryContext()), token);

    [HttpGet("{id:guid}/baseline-comparison")]
    public Task<IActionResult> CompareBaselines(Guid id, [FromQuery] int left, [FromQuery] int right, CancellationToken token) =>
        Send(new CompareStructureBaselinesQuery(id, left, right, QueryContext()), token);

    private DwsTrustedActorContext CommandContext() => contexts.Resolve(
        User,
        Request.Headers[DwsLocalTestAuthenticationDefaults.IdempotencyHeader].ToString());

    private DwsTrustedActorContext QueryContext() => contexts.Resolve(User, null);

    private async Task<IActionResult> Send<T>(IRequest<Response<T>> request, CancellationToken token) =>
        FromResponse(await sender.Send(request, token));

    private Task<IActionResult> Invalid<T>() =>
        Task.FromResult(FromResponse(Response<T>.Fail(DwsErrors.InvalidRequest, 400)));
}
