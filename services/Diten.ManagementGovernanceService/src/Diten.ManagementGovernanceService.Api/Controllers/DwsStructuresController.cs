using Diten.ManagementGovernanceService.Application.Features.Dws;
using Diten.ManagementGovernanceService.Application.Modules.Dws;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Diten.ManagementGovernanceService.Api.Controllers;

[Route("api/dws/structures")]
public sealed class DwsStructuresController(ISender sender) : CustomBaseController
{
    [HttpPost] public Task<IActionResult> Create([FromBody] CreateStructureCommand command, CancellationToken token) => Dispatch(command, token);
    [HttpPut("{id:guid}/metadata")] public Task<IActionResult> UpdateMetadata(Guid id,[FromBody] UpdateStructureMetadataCommand command,CancellationToken token)=>Dispatch(Match(id,command.StructureDefinitionId,command),token);
    [HttpPost("{id:guid}/nodes")] public Task<IActionResult> AddNode(Guid id,[FromBody] AddStructureNodeCommand command,CancellationToken token)=>Dispatch(Match(id,command.StructureDefinitionId,command),token);
    [HttpPost("{id:guid}/nodes/{logicalNodeId:guid}/move")] public Task<IActionResult> MoveNode(Guid id,Guid logicalNodeId,[FromBody] MoveStructureNodeCommand command,CancellationToken token)=>Dispatch(Match(id,command.StructureDefinitionId,logicalNodeId,command.LogicalNodeId,command),token);
    [HttpPost("{id:guid}/nodes/{logicalNodeId:guid}/reorder")] public Task<IActionResult> ReorderNode(Guid id,Guid logicalNodeId,[FromBody] ReorderStructureNodeCommand command,CancellationToken token)=>Dispatch(Match(id,command.StructureDefinitionId,logicalNodeId,command.LogicalNodeId,command),token);
    [HttpDelete("{id:guid}/nodes/{logicalNodeId:guid}")] public Task<IActionResult> RemoveNode(Guid id,Guid logicalNodeId,[FromBody] RemoveStructureNodeCommand command,CancellationToken token)=>Dispatch(Match(id,command.StructureDefinitionId,logicalNodeId,command.LogicalNodeId,command),token);
    [HttpPost("{id:guid}/dependencies")] public Task<IActionResult> AddDependency(Guid id,[FromBody] AddStructuralDependencyCommand command,CancellationToken token)=>Dispatch(Match(id,command.StructureDefinitionId,command),token);
    [HttpDelete("{id:guid}/dependencies")] public Task<IActionResult> RemoveDependency(Guid id,[FromBody] RemoveStructuralDependencyCommand command,CancellationToken token)=>Dispatch(Match(id,command.StructureDefinitionId,command),token);
    [HttpPost("{id:guid}/baselines")] public Task<IActionResult> CreateBaseline(Guid id,[FromBody] CreateStructureBaselineCommand command,CancellationToken token)=>Dispatch(Match(id,command.StructureDefinitionId,command),token);
    [HttpPost("{id:guid}/revisions")] public Task<IActionResult> CreateRevision(Guid id,[FromBody] CreateNextStructureRevisionCommand command,CancellationToken token)=>Dispatch(Match(id,command.StructureDefinitionId,command),token);
    [HttpGet("{id:guid}")] public Task<IActionResult> Get(Guid id,CancellationToken token)=>Dispatch(new GetStructureByIdQuery(id),token);
    [HttpGet("{id:guid}/tree")] public Task<IActionResult> Tree(Guid id,[FromQuery]int? revisionNumber,CancellationToken token)=>Dispatch(new GetStructureTreeQuery(id,revisionNumber),token);
    [HttpGet("{id:guid}/validation")] public Task<IActionResult> Validate(Guid id,[FromQuery]int? revisionNumber,CancellationToken token)=>Dispatch(new ValidateStructureQuery(id,revisionNumber),token);
    [HttpGet("{id:guid}/revision-comparison")] public Task<IActionResult> CompareRevisions(Guid id,[FromQuery]int left,[FromQuery]int right,CancellationToken token)=>Dispatch(new CompareStructureRevisionsQuery(id,left,right),token);
    [HttpGet("{id:guid}/baseline-comparison")] public Task<IActionResult> CompareBaselines(Guid id,[FromQuery]int left,[FromQuery]int right,CancellationToken token)=>Dispatch(new CompareStructureBaselinesQuery(id,left,right),token);

    private async Task<IActionResult> Dispatch(IDwsRequestContract contract,CancellationToken token)
    {
        var response=await sender.Send(new DwsDispatchRequest(contract.GetType().Name,contract,Context()),token);
        return FromResponse(response);
    }
    private DwsTrustedContext Context()=>new(ParseGuid("X-Diten-Test-Tenant"),ParseGuid("X-Diten-Test-Actor"),Request.Headers["X-Diten-Test-Idempotency-Key"].ToString());
    private Guid ParseGuid(string name)=>Guid.TryParse(Request.Headers[name].ToString(),out var value)?value:Guid.Empty;
    private static T Match<T>(Guid route,Guid body,T value)=>route==body?value:throw new BadHttpRequestException("dws_invalid_request");
    private static T Match<T>(Guid route,Guid body,Guid routeChild,Guid bodyChild,T value)=>route==body&&routeChild==bodyChild?value:throw new BadHttpRequestException("dws_invalid_request");
}
