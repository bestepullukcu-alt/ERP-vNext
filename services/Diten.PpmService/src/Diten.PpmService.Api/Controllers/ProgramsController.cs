using Diten.PpmService.Application.Features.Programs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.PpmService.Api.Controllers;

[Authorize]
[Route("api/v1/ppm/programs")]
public sealed class ProgramsController(ISender sender) : CustomBaseController
{
    [HttpGet] public async Task<IActionResult> List(CancellationToken ct) => CreateActionResultInstance(await sender.Send(new ListProgramsQuery(), ct));
    [HttpGet("{id:guid}")] public async Task<IActionResult> Get(Guid id, CancellationToken ct) => CreateActionResultInstance(await sender.Send(new GetProgramByIdQuery(id), ct));
    [HttpPost] public async Task<IActionResult> Create(CreateProgramCommand command, CancellationToken ct) => CreateActionResultInstance(await sender.Send(command, ct));
    [HttpPut("{id:guid}")] public async Task<IActionResult> Update(Guid id, UpdateProgramCommand command, CancellationToken ct) => CreateActionResultInstance(await sender.Send(command with { Id = id }, ct));
    [HttpPost("{id:guid}/lifecycle")] public async Task<IActionResult> Transition(Guid id, TransitionProgramLifecycleCommand command, CancellationToken ct) => CreateActionResultInstance(await sender.Send(command with { Id = id }, ct));
    [HttpDelete("{id:guid}")] public async Task<IActionResult> SoftDelete(Guid id, [FromQuery] int expectedVersion, CancellationToken ct) => CreateActionResultInstance(await sender.Send(new SoftDeleteProgramCommand(id, expectedVersion), ct));
}
