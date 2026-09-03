using Diten.PpmService.Application.Features.Initiatives;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.PpmService.Api.Controllers;

[Authorize]
[Route("api/v1/ppm/initiatives")]
public sealed class InitiativesController(ISender sender) : CustomBaseController
{
    [HttpGet("contracts/v2")] public async Task<IActionResult> ContractsV2(CancellationToken ct) => CreateActionResultInstance(await sender.Send(new GetInitiativeContractsV2Query(), ct));
    [HttpGet("lifecycle-contracts/v2")] public async Task<IActionResult> LifecycleContractsV2(CancellationToken ct) => CreateActionResultInstance(await sender.Send(new GetInitiativeLifecycleContractsV2Query(), ct));
    [HttpGet] public async Task<IActionResult> List(CancellationToken ct) => CreateActionResultInstance(await sender.Send(new ListInitiativesQuery(), ct));
    [HttpGet("{id:guid}")] public async Task<IActionResult> Get(Guid id, CancellationToken ct) => CreateActionResultInstance(await sender.Send(new GetInitiativeByIdQuery(id), ct));
    [HttpGet("{id:guid}/details/links")] public async Task<IActionResult> DetailLinks(Guid id, CancellationToken ct) => CreateActionResultInstance(await sender.Send(new GetInitiativeDetailLinksQuery(id), ct));
    [HttpPost] public async Task<IActionResult> Create(CreateInitiativeCommand command, CancellationToken ct) => CreateActionResultInstance(await sender.Send(command, ct));
    [HttpPost("{terminalId:guid}/successors")] public async Task<IActionResult> CreateSuccessor(Guid terminalId, CreateInitiativeSuccessorCommand command, CancellationToken ct) => CreateActionResultInstance(await sender.Send(command with { TerminalId = terminalId }, ct));
    [HttpPut("{id:guid}")] public async Task<IActionResult> Update(Guid id, UpdateInitiativeCommand command, CancellationToken ct) => CreateActionResultInstance(await sender.Send(command with { Id = id }, ct));
    [HttpPost("{id:guid}/lifecycle")] public async Task<IActionResult> Transition(Guid id, TransitionInitiativeLifecycleCommand command, CancellationToken ct) => CreateActionResultInstance(await sender.Send(command with { Id = id }, ct));
    [HttpDelete("{id:guid}")] public async Task<IActionResult> SoftDelete(Guid id, [FromQuery] int expectedVersion, CancellationToken ct) => CreateActionResultInstance(await sender.Send(new SoftDeleteInitiativeCommand(id, expectedVersion), ct));
}
