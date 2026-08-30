using Diten.PpmService.Application.Features.Projects;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.PpmService.Api.Controllers;

[Authorize]
[Route("api/v1/ppm/projects")]
public sealed class ProjectsController(ISender sender) : CustomBaseController
{
    [HttpGet] public async Task<IActionResult> List(CancellationToken ct) => CreateActionResultInstance(await sender.Send(new ListProjectsQuery(), ct));
    [HttpGet("{id:guid}")] public async Task<IActionResult> Get(Guid id, CancellationToken ct) => CreateActionResultInstance(await sender.Send(new GetProjectByIdQuery(id), ct));
    [HttpPost] public async Task<IActionResult> Create(CreateProjectCommand command, CancellationToken ct) => CreateActionResultInstance(await sender.Send(command, ct));
    [HttpPut("{id:guid}")] public async Task<IActionResult> Update(Guid id, UpdateProjectCommand command, CancellationToken ct) => CreateActionResultInstance(await sender.Send(command with { Id = id }, ct));
    [HttpPost("{id:guid}/lifecycle")] public async Task<IActionResult> Transition(Guid id, TransitionProjectLifecycleCommand command, CancellationToken ct) => CreateActionResultInstance(await sender.Send(command with { Id = id }, ct));
    [HttpDelete("{id:guid}")] public async Task<IActionResult> SoftDelete(Guid id, [FromQuery] int expectedVersion, CancellationToken ct) => CreateActionResultInstance(await sender.Send(new SoftDeleteProjectCommand(id, expectedVersion), ct));
}
