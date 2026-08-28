using System.Text.Json;
using Diten.Platform.API.Middleware;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.ModuleCatalog.Commands;
using Diten.Platform.Application.Features.ModuleCatalog.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Features.Workflow.Queries;
using Diten.Platform.Application.Services;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.Middleware;

/// <summary>
/// The HTTP CONTRACT for a blocked workflow transition, asserted at the middleware that actually writes the
/// response.
///
/// <para>Why this layer: the handler-level tests only proved that a blocked transition throws
/// <see cref="WorkflowTransitionBlockedException"/>, and they were green while the live endpoint answered
/// <b>500 Server Error</b>. The exception had no branch in the handler's switch, so it fell through to the
/// catch-all. A green handler test cannot see that — this is the seam where the status code is decided.</para>
///
/// <para>What must hold: <b>409</b> (a business refusal, not a crash), and the reason code carried as an extension
/// named exactly <c>reason_code</c>, which is the field the frontend bridge reads (Tasks/api.js) to render a
/// localized message. Without the code the client can only show a generic error.</para>
/// </summary>
public sealed class BlockedTransitionHttpStatusTests
{
    [Theory]
    [InlineData(WorkflowReasonCodes.WorkflowPendingApproval)]
    [InlineData(WorkflowReasonCodes.WorkflowRejected)]
    [InlineData(WorkflowReasonCodes.WorkflowCancelled)]
    [InlineData(WorkflowReasonCodes.WorkflowNotTerminalApproved)]
    [InlineData(WorkflowGateReasonCodes.EvaluationFailed)]
    public async Task A_blocked_transition_is_409_and_carries_reason_code_on_the_wire(string reasonCode)
    {
        var (status, body) = await HandleAsync(Blocked(reasonCode));

        Assert.Equal(StatusCodes.Status409Conflict, status);
        Assert.Equal(reasonCode, body.GetProperty("reason_code").GetString());
    }

    [Fact]
    public async Task The_field_name_is_reason_code_exactly_because_the_frontend_bridge_reads_that_name()
    {
        // Renaming it to reasonCode/ReasonCode would silently fall back to the generic message in the browser,
        // which is why the exact name is pinned rather than "some field containing the code".
        var (_, body) = await HandleAsync(Blocked(WorkflowReasonCodes.WorkflowPendingApproval));

        Assert.True(body.TryGetProperty("reason_code", out _));
        Assert.False(body.TryGetProperty("reasonCode", out _));
    }

    [Fact]
    public async Task A_block_with_no_reason_code_still_answers_409_without_inventing_one()
    {
        var (status, body) = await HandleAsync(Blocked(reasonCode: null));

        Assert.Equal(StatusCodes.Status409Conflict, status);
        Assert.False(body.TryGetProperty("reason_code", out _));
    }

    [Fact]
    public async Task An_ordinary_exception_is_still_a_500_so_the_change_stays_narrow()
    {
        // The fix must not turn unrelated faults into conflicts — that would hide real crashes behind a business code.
        var (status, _) = await HandleAsync(new Exception("something genuinely broke"));

        Assert.Equal(StatusCodes.Status500InternalServerError, status);
    }

    [Fact]
    public async Task Validation_still_answers_400_so_the_new_branch_did_not_shadow_it()
    {
        var (status, _) = await HandleAsync(new ValidationException("bad input"));

        Assert.Equal(StatusCodes.Status400BadRequest, status);
    }

    /// <summary>
    /// The MODULE CATALOG activate path, end to end through the same middleware: it is the other caller that uses
    /// the throwing gate helper, so it inherited the same 500 and is fixed by the same branch.
    /// </summary>
    [Fact]
    public async Task Module_catalog_activate_blocked_by_the_gate_answers_409_not_500()
    {
        var item = new ModuleCatalogItem
        {
            Id = Guid.NewGuid(),
            ModuleCode = "MOD-REF",
            ModuleName = "Reference Module",
            DisplayName = "Reference Module",
            Status = ModuleCatalogStatus.Draft
        };
        // The same fixtures the handler-level test uses, so this asserts the real activate path rather than a
        // re-implementation of it.
        var repository = new ModuleCatalog.ModuleCatalogActivateGateTests.FakeModuleCatalogRepository(item);
        var handler = new ActivateModuleCatalogItemCommandHandler(
            repository,
            new WorkflowTransitionGate(
                new ModuleCatalog.ModuleCatalogActivateGateTests.GateMediator(
                    WorkflowTransitionGateDecision.Blocked,
                    WorkflowTransitionGateStatus.PendingApproval,
                    blockingReason: WorkflowReasonCodes.WorkflowPendingApproval),
                NullLogger<WorkflowTransitionGate>.Instance));

        var thrown = await Assert.ThrowsAsync<WorkflowTransitionBlockedException>(
            () => handler.Handle(new ActivateModuleCatalogItemCommand(item.Id), CancellationToken.None));

        var (status, body) = await HandleAsync(thrown);

        Assert.Equal(StatusCodes.Status409Conflict, status);
        Assert.Equal(WorkflowReasonCodes.WorkflowPendingApproval, body.GetProperty("reason_code").GetString());
        // And the refusal really did refuse: the item never left Draft.
        Assert.Equal(ModuleCatalogStatus.Draft, item.Status);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static WorkflowTransitionBlockedException Blocked(string? reasonCode)
        => new(new WorkflowGateResult(
            IsAllowed: false,
            Decision: "Blocked",
            GateStatus: "PendingApproval",
            BlockingReasonCode: reasonCode,
            BlockingMessage: "Workflow approval is still pending.",
            CorrelationId: "corr-middleware"));

    private static async Task<(int Status, JsonElement Body)> HandleAsync(Exception exception)
    {
        var context = new DefaultHttpContext();
        var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        var handled = await new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance)
            .TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled, "the handler must own the response for these exceptions");

        responseBody.Position = 0;
        var body = JsonDocument.Parse(responseBody).RootElement;
        return (context.Response.StatusCode, body);
    }
}
