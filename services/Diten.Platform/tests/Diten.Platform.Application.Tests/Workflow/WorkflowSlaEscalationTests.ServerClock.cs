using System.Net;
using System.Text.Json;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Features.Workflow.Commands;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.Platform.Application.Tests.Workflow;

/*
 * BL-422 — AN ESCALATION RUN IS EVALUATED AGAINST THE SERVER CLOCK, NEVER A CALLER-SUPPLIED ONE.
 *
 * WHAT WAS WRONG. RunWorkflowEscalationsHandler read `request.NowUtc ?? DateTimeOffset.UtcNow`. Anyone holding
 * platform.workflow.escalations.run could send a date in the future and escalate or TIME OUT tasks in their own tenant
 * that were not due — the transition log then records the system acting on an SLA that had not been breached.
 *
 * THE RULE. The handler reads the injected TimeProvider. A request carrying NowUtc is refused 400
 * (WORKFLOW_ESCALATION_CLOCK_NOT_ACCEPTED) and nothing moves; tests control time by advancing the injected clock.
 *
 * The "clock advanced → escalates" cases are the control: the same task, rule and endpoint DO escalate once the server
 * clock passes the due time, so a refusal is not a handler that never escalates.
 */
public sealed partial class WorkflowSlaEscalationTests
{
    private const string RunEscalationsPath = "/api/v1/workflow/escalations/run";
    private static readonly Guid EscalationOperator = Guid.Parse("42242242-0000-4000-8000-0000000000d2");

    [Fact]
    public async Task A_request_NowUtc_far_in_the_future_is_refused_and_a_task_that_is_not_due_is_not_touched()
    {
        var f = Fixture(TenantA);
        var runtime = await f.SeedRuntimeWithRuleAsync(
            ApprovalTaskStatus.WaitingApproval,
            dueAt: DateTimeOffset.UtcNow.AddMinutes(5),
            timeoutAfterMinutes: 40);

        var response = await f.Run.Handle(
            new RunWorkflowEscalationsCommand(
                new RunWorkflowEscalationsRequest(DateTimeOffset.UtcNow.AddYears(1), 100, null),
                Correlation),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowEscalationClockNotAccepted, response.ReasonCode);
        AssertUntouched(f, runtime);
    }

    [Fact]
    public async Task The_injected_server_clock_decides_the_task_escalates_only_once_that_clock_passes_its_due_time()
    {
        var f = Fixture(TenantA);
        var runtime = await f.SeedRuntimeWithRuleAsync(ApprovalTaskStatus.WaitingApproval, dueAt: DateTimeOffset.UtcNow.AddMinutes(5));

        var early = await f.Run.Handle(Run(), CancellationToken.None);

        Assert.True(early.IsSuccessful);
        Assert.Equal(0, early.Data!.EvaluatedCount);
        AssertUntouched(f, runtime);

        f.Clock.Advance(TimeSpan.FromMinutes(30));
        var due = await f.Run.Handle(Run(), CancellationToken.None);

        Assert.True(due.IsSuccessful);
        Assert.Equal(1, due.Data!.EscalatedCount);
        Assert.Equal(ApprovalTaskStatus.Escalated, runtime.Task.Status);
    }

    [Fact]
    public async Task Http_a_run_whose_body_carries_a_far_future_nowUtc_is_refused_400_and_the_task_is_not_touched()
    {
        var f = Fixture(TenantA);
        var runtime = await f.SeedRuntimeWithRuleAsync(
            ApprovalTaskStatus.WaitingApproval,
            dueAt: DateTimeOffset.UtcNow.AddMinutes(5),
            timeoutAfterMinutes: 40);
        using var host = HttpHost(f);

        var response = await host.PostJsonAsync(
            RunEscalationsPath,
            OperatorToken(),
            """{"nowUtc":"2099-01-01T00:00:00Z","maxItems":100}""");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(WorkflowReasonCodes.WorkflowEscalationClockNotAccepted, body.RootElement.GetProperty("reason_code").GetString());
        AssertUntouched(f, runtime);
    }

    [Fact]
    public async Task Http_the_same_run_escalates_the_task_once_the_injected_server_clock_passes_its_due_time()
    {
        var f = Fixture(TenantA);
        var runtime = await f.SeedRuntimeWithRuleAsync(ApprovalTaskStatus.WaitingApproval, dueAt: DateTimeOffset.UtcNow.AddMinutes(5));
        using var host = HttpHost(f);

        var early = await host.PostJsonAsync(RunEscalationsPath, OperatorToken(), """{"maxItems":100}""");

        Assert.Equal(HttpStatusCode.OK, early.StatusCode);
        AssertUntouched(f, runtime);

        f.Clock.Advance(TimeSpan.FromMinutes(30));
        var due = await host.PostJsonAsync(RunEscalationsPath, OperatorToken(), """{"maxItems":100}""");

        Assert.Equal(HttpStatusCode.OK, due.StatusCode);
        using var body = JsonDocument.Parse(await due.Content.ReadAsStringAsync());
        Assert.Equal(1, body.RootElement.GetProperty("data").GetProperty("escalatedCount").GetInt32());
        Assert.Equal(ApprovalTaskStatus.Escalated, runtime.Task.Status);
    }

    private static void AssertUntouched(TestFixture f, RuntimeSeed runtime)
    {
        Assert.Equal(ApprovalTaskStatus.WaitingApproval, runtime.Task.Status);
        Assert.Equal(WorkflowInstanceStatus.Active, runtime.Instance.Status);
        Assert.Equal(0, runtime.Task.EscalationLevel);
        Assert.Null(runtime.Task.EscalatedAt);
        Assert.Null(runtime.Task.TimedOutAt);
        Assert.Single(f.Logs.Items); // only the start log the seed wrote
    }

    private static string OperatorToken()
        => SignedTenantTokenHttpHost.TenantUserToken(TenantA, EscalationOperator, WorkflowPermissions.EscalationsRun);

    /// <summary>The shipped WorkflowDefinitionsController over the fixture's repositories and clock.</summary>
    private static SignedTenantTokenHttpHost HttpHost(TestFixture f) => new(services =>
    {
        services.AddSingleton<IApprovalTaskRepository>(f.Tasks);
        services.AddSingleton<IWorkflowInstanceRepository>(f.Instances);
        services.AddSingleton<ISlaEscalationRuleRepository>(f.Rules);
        services.AddSingleton<IWorkflowTransitionLogRepository>(f.Logs);
        services.AddSingleton<IRuntimeAssignmentSnapshotRepository>(f.Snapshots);
        services.AddSingleton<TimeProvider>(f.Clock);
    });

    /// <summary>
    /// The server clock the handler reads: real time plus an offset the test moves forward. With no offset it is
    /// exactly the <c>DateTimeOffset.UtcNow</c> these tests used to put in the request.
    /// </summary>
    private sealed class ServerClock : TimeProvider
    {
        private TimeSpan _offset;

        public void Advance(TimeSpan by) => _offset += by;

        public override DateTimeOffset GetUtcNow() => TimeProvider.System.GetUtcNow() + _offset;
    }
}
