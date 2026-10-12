using System.Text.Json;
using Diten.InventoryService.Api.Controllers;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Quarantine;
using Diten.InventoryService.Application.Features.Quarantine.Ports;
using Diten.InventoryService.Application.Features.Quarantine.Services;
using Diten.InventoryService.Domain.Quarantine.Entities;
using Diten.InventoryService.Domain.Quarantine.Enums;
using Diten.InventoryService.Domain.Quarantine.Repositories;
using Diten.InventoryService.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Diten.InventoryService.Tests.TestInfrastructure;

/// <summary>
/// The slice Q1 TEST approval port, now a test device only (Q5, O-Q2-5: production code keeps the Workflow path alone).
/// <see cref="QuarantineApiFactory"/> registers it in place of <c>WorkflowApprovalPort</c>: every submit is accepted with a synthetic
/// instance id <c>test:{decisionId}</c>, and the test reports the outcome through <see cref="TestApprovalController"/> — the same
/// <see cref="QuarantineOutcomeApplier"/> the Workflow reconciliation calls, so the apply engine under test is the production one.
/// </summary>
public sealed class TestApprovalPort : IQuarantineApprovalPort
{
    public const string InstancePrefix = "test:";

    public Task<ApprovalStart> StartAsync(QuarantineDecision decision, CancellationToken cancellationToken = default)
        => Task.FromResult(ApprovalStart.Ok(InstancePrefix + decision.Id.ToString("D")));
}

/// <summary>
/// The test port's outcome endpoint (not QUARANTINE-BUNDLE). It lives in the TEST assembly and is added to the host as an
/// application part only by <see cref="QuarantineApiFactory"/> with the test port on — a production host has no such route
/// (QuarantineApprovalPortTests + QuarantineValidationTests measure it).
/// </summary>
[Route("api/quarantine/test-approval")]
[Authorize]
public sealed class TestApprovalController : CustomBaseController
{
    [HttpPost("decisions/{decisionId}")]
    [HasPermission(QuarantinePermissions.DecisionsApply)]
    public async Task<IActionResult> RecordOutcome(string decisionId, CancellationToken ct)
    {
        var services = HttpContext.RequestServices;
        var decisions = services.GetRequiredService<IQuarantineDecisionRepository>();
        if (!Guid.TryParse(decisionId, out var id) || await decisions.FindAsync(id, ct) is not { } decision)
        {
            return ContractFailure(StatusCodes.Status404NotFound, QuarantineErrorCodes.DecisionNotFound, "Decision not found.");
        }

        using var body = await JsonDocument.ParseAsync(Request.Body, cancellationToken: ct);
        if (!body.RootElement.TryGetProperty("outcome", out var raw) || !Enum.TryParse<ApprovalOutcome>(raw.GetString(), out var outcome))
        {
            return ContractFailure(StatusCodes.Status400BadRequest, InventoryErrorCodes.InvalidRequest, "outcome is required.");
        }

        var approver = body.RootElement.TryGetProperty("approverUserId", out var a) && Guid.TryParse(a.GetString(), out var parsed)
            ? parsed
            : services.GetRequiredService<ITenantContext>().UserId;
        var instance = (await decisions.EventsAsync([decision.Id], ct)).LastOrDefault(e => e.Kind == DecisionEventKind.WorkflowStarted)?.WorkflowInstanceId;
        var applied = await services.GetRequiredService<QuarantineOutcomeApplier>().ApplyOutcomeAsync(decision.Id, outcome, approver, instance, ct);
        if (!applied.IsSuccessful)
        {
            return CreateActionResultInstance(Response<DecisionDto>.Fail(applied.ErrorCode!, applied.Errors[0], applied.StatusCode));
        }

        return CreateActionResultInstance(Response<DecisionDto>.Success(QuarantineMapping.Decision(decision, await decisions.EventsAsync([decision.Id], ct))));
    }
}
