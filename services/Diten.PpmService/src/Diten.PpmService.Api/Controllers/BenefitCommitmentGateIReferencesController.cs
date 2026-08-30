using System.Text;
using System.Text.Json;
using Diten.PpmService.Application.Features.InvestmentCases.GateI.DecisionTrace;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.PpmService.Api.Controllers;

[Authorize]
[Route("api/v1/ppm/benefit-commitments/{id:guid}/gate-i")]
public sealed class BenefitCommitmentGateIReferencesController(ISender sender) : CustomBaseController
{
    [HttpPost("outcomes")]
    public async Task<IActionResult> AddOutcome(
        Guid id,
        [FromQuery] int expectedVersion,
        [FromBody] JsonElement body,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GateIRelationshipMutationCommand(
            id,
            GateIRelationshipKind.BenefitOutcome,
            GateIRelationshipAction.AttachOrReplace,
            Encoding.UTF8.GetBytes(body.GetRawText()),
            null,
            expectedVersion,
            Request.Headers["Idempotency-Key"].ToString(),
            "ppm.gate-i.benefit-outcome.add"), cancellationToken);
        return CreateActionResultInstance(result);
    }

    [HttpDelete("outcomes/{referenceId:guid}")]
    public async Task<IActionResult> RemoveOutcome(
        Guid id,
        Guid referenceId,
        [FromQuery] int expectedVersion,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GateIRelationshipMutationCommand(
            id,
            GateIRelationshipKind.BenefitOutcome,
            GateIRelationshipAction.Remove,
            Array.Empty<byte>(),
            referenceId,
            expectedVersion,
            Request.Headers["Idempotency-Key"].ToString(),
            "ppm.gate-i.benefit-outcome.remove"), cancellationToken);
        return CreateActionResultInstance(result);
    }
}
