using Diten.PpmService.Application.Features.Portfolios;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.PpmService.Api.Controllers;

[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/v1/ppm/portfolios")]
public sealed class PortfoliosController(ISender sender) : CustomBaseController
{
    [HttpGet("page-access")]
    public async Task<IActionResult> PageAccess(CancellationToken ct) =>
        CreateActionResultInstance(await sender.Send(new GetPortfolioPageAccessQuery(), ct));

    [HttpGet("{id:guid}/owner-candidates")]
    public async Task<IActionResult> OwnerCandidates(Guid id, [FromQuery] string search = "", [FromQuery] int limit = 20, CancellationToken ct = default) =>
        CreateActionResultInstance(await sender.Send(new GetPortfolioOwnerCandidatesQuery(id, search, limit), ct));

    [HttpPost("{id:guid}/owner-assignments")]
    public async Task<IActionResult> ChangeOwner(Guid id, ChangePortfolioOwnerCommand command, CancellationToken ct) =>
        CreateActionResultInstance(await sender.Send(command with { Id = id }, ct));

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        CreateActionResultInstance(await sender.Send(new ListPortfoliosQuery(), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await sender.Send(new GetPortfolioByIdQuery(id), ct));

    [HttpPost]
    public async Task<IActionResult> Create(CreatePortfolioCommand command, CancellationToken ct) =>
        CreateActionResultInstance(await sender.Send(command, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdatePortfolioCommand command, CancellationToken ct) =>
        CreateActionResultInstance(await sender.Send(command with { Id = id }, ct));

    [HttpPost("{id:guid}/lifecycle")]
    public async Task<IActionResult> Transition(Guid id, TransitionPortfolioLifecycleCommand command, CancellationToken ct) =>
        CreateActionResultInstance(await sender.Send(command with { Id = id }, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> SoftDelete(Guid id, [FromQuery] int expectedVersion, CancellationToken ct) =>
        CreateActionResultInstance(await sender.Send(new SoftDeletePortfolioCommand(id, expectedVersion), ct));
}
