using System.Text;
using System.Text.Json;
using Diten.PpmService.Application.Features.InvestmentCases.GateI.DecisionTrace;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.PpmService.Api.Controllers;

[Authorize]
[Route("api/v1/ppm/investment-cases/{id:guid}/gate-i")]
public sealed class InvestmentCaseGateIReferencesController(ISender sender) : CustomBaseController
{
    [HttpPut("governing-decision")]
    public Task<IActionResult> SetGoverning(Guid id, [FromQuery] int expectedVersion, [FromBody] JsonElement body, CancellationToken ct) =>
        Send(id, GateIRelationshipKind.GoverningDecision, GateIRelationshipAction.AttachOrReplace, body, null,
            expectedVersion, "ppm.gate-i.governing-decision.set", ct);

    [HttpDelete("governing-decision")]
    public Task<IActionResult> RemoveGoverning(Guid id, [FromQuery] int expectedVersion, CancellationToken ct) =>
        Send(id, GateIRelationshipKind.GoverningDecision, GateIRelationshipAction.Remove, default, null,
            expectedVersion, "ppm.gate-i.governing-decision.remove", ct);

    [HttpPost("supporting-decisions")]
    public Task<IActionResult> AddSupporting(Guid id, [FromQuery] int expectedVersion, [FromBody] JsonElement body, CancellationToken ct) =>
        Send(id, GateIRelationshipKind.SupportingDecision, GateIRelationshipAction.AttachOrReplace, body, null,
            expectedVersion, "ppm.gate-i.supporting-decision.add", ct);

    [HttpDelete("supporting-decisions/{referenceId:guid}")]
    public Task<IActionResult> RemoveSupporting(Guid id, Guid referenceId, [FromQuery] int expectedVersion, CancellationToken ct) =>
        Send(id, GateIRelationshipKind.SupportingDecision, GateIRelationshipAction.Remove, default, referenceId,
            expectedVersion, "ppm.gate-i.supporting-decision.remove", ct);

    [HttpPut("selected-budget-version")]
    public Task<IActionResult> SetBudget(Guid id, [FromQuery] int expectedVersion, [FromBody] JsonElement body, CancellationToken ct) =>
        Send(id, GateIRelationshipKind.SelectedBudgetVersion, GateIRelationshipAction.AttachOrReplace, body, null,
            expectedVersion, "ppm.gate-i.selected-budget-version.set", ct);

    [HttpDelete("selected-budget-version")]
    public Task<IActionResult> RemoveBudget(Guid id, [FromQuery] int expectedVersion, CancellationToken ct) =>
        Send(id, GateIRelationshipKind.SelectedBudgetVersion, GateIRelationshipAction.Remove, default, null,
            expectedVersion, "ppm.gate-i.selected-budget-version.remove", ct);

    [HttpPost("scenario-versions")]
    public Task<IActionResult> AddScenario(Guid id, [FromQuery] int expectedVersion, [FromBody] JsonElement body, CancellationToken ct) =>
        Send(id, GateIRelationshipKind.ScenarioVersion, GateIRelationshipAction.AttachOrReplace, body, null,
            expectedVersion, "ppm.gate-i.scenario-version.add", ct);

    [HttpDelete("scenario-versions/{referenceId:guid}")]
    public Task<IActionResult> RemoveScenario(Guid id, Guid referenceId, [FromQuery] int expectedVersion, CancellationToken ct) =>
        Send(id, GateIRelationshipKind.ScenarioVersion, GateIRelationshipAction.Remove, default, referenceId,
            expectedVersion, "ppm.gate-i.scenario-version.remove", ct);

    [HttpPost("comparator-outputs")]
    public Task<IActionResult> AddComparator(Guid id, [FromQuery] int expectedVersion, [FromBody] JsonElement body, CancellationToken ct) =>
        Send(id, GateIRelationshipKind.ComparatorOutput, GateIRelationshipAction.AttachOrReplace, body, null,
            expectedVersion, "ppm.gate-i.comparator-output.add", ct);

    [HttpDelete("comparator-outputs/{referenceId:guid}")]
    public Task<IActionResult> RemoveComparator(Guid id, Guid referenceId, [FromQuery] int expectedVersion, CancellationToken ct) =>
        Send(id, GateIRelationshipKind.ComparatorOutput, GateIRelationshipAction.Remove, default, referenceId,
            expectedVersion, "ppm.gate-i.comparator-output.remove", ct);

    [HttpPut("selected-scenario")]
    public Task<IActionResult> SetSelectedScenario(Guid id, [FromQuery] int expectedVersion, [FromBody] JsonElement body, CancellationToken ct) =>
        Send(id, GateIRelationshipKind.SelectedScenario, GateIRelationshipAction.AttachOrReplace, body, null,
            expectedVersion, "ppm.gate-i.selected-scenario.set", ct);

    [HttpDelete("selected-scenario")]
    public Task<IActionResult> RemoveSelectedScenario(Guid id, [FromQuery] int expectedVersion, CancellationToken ct) =>
        Send(id, GateIRelationshipKind.SelectedScenario, GateIRelationshipAction.Remove, default, null,
            expectedVersion, "ppm.gate-i.selected-scenario.remove", ct);

    private async Task<IActionResult> Send(
        Guid id,
        GateIRelationshipKind kind,
        GateIRelationshipAction action,
        JsonElement body,
        Guid? referenceId,
        int expectedVersion,
        string operation,
        CancellationToken cancellationToken)
    {
        var bytes = action == GateIRelationshipAction.Remove
            ? Array.Empty<byte>()
            : Encoding.UTF8.GetBytes(body.GetRawText());
        var result = await sender.Send(new GateIRelationshipMutationCommand(
            id, kind, action, bytes, referenceId, expectedVersion,
            Request.Headers["Idempotency-Key"].ToString(), operation), cancellationToken);
        return CreateActionResultInstance(result);
    }
}
