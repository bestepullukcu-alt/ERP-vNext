using Diten.PpmService.Application.Features.BenefitCommitments;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.PpmService.Api.Controllers;

[Authorize]
[Route("api/v1/ppm/benefit-commitments")]
public sealed class BenefitCommitmentsController(ISender sender) : CustomBaseController
{
    [HttpGet] public async Task<IActionResult> List(CancellationToken ct) => CreateActionResultInstance(await sender.Send(new ListBenefitCommitmentsQuery(), ct));
    [HttpGet("{id:guid}")] public async Task<IActionResult> Get(Guid id, CancellationToken ct) => CreateActionResultInstance(await sender.Send(new GetBenefitCommitmentByIdQuery(id), ct));
    [HttpPost] public async Task<IActionResult> Create(CreateBenefitCommitmentCommand command, CancellationToken ct) => CreateActionResultInstance(await sender.Send(command, ct));
    [HttpPut("{id:guid}")] public async Task<IActionResult> Update(Guid id, UpdateBenefitCommitmentCommand command, CancellationToken ct) => CreateActionResultInstance(await sender.Send(command with { Id = id }, ct));
    [HttpPost("{id:guid}/lifecycle")] public async Task<IActionResult> Transition(Guid id, TransitionBenefitCommitmentLifecycleCommand command, CancellationToken ct) => CreateActionResultInstance(await sender.Send(command with { Id = id }, ct));
    [HttpDelete("{id:guid}")] public async Task<IActionResult> SoftDelete(Guid id, [FromQuery] int expectedVersion, CancellationToken ct) => CreateActionResultInstance(await sender.Send(new SoftDeleteBenefitCommitmentCommand(id, expectedVersion), ct));
}
