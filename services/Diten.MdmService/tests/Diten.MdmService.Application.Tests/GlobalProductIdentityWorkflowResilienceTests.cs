using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Microsoft.Extensions.Logging;
using Xunit;
using Outcome = Diten.MdmService.Application.Contracts.Workflow.ProductIdentityWorkflowTransportOutcome;
using Scope = Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.ProductIdentityWorkflowFaultScope;
using TransportCancellationEvidence = Diten.MdmService.Application.Contracts.Workflow.ProductIdentityWorkflowCancellationEvidence;

namespace Diten.MdmService.Application.Tests;

// WP-GP-1B-A-RESILIENCE-01 — a fault of the service chain is not a fault of the record. One test per row of the
// approved table, on the production processor and the production policy; only the transport and the stores are fakes.
public sealed partial class GlobalProductIdentityWorkflowProcessorTests
{
    private const string Resubmit = ProductIdentityWorkflowMakerCodes.ServiceUnavailableResubmit;

    // ── submit: the start is refused before it is processed (rows 2, 3, 4, 7, 8, 10, 11 and unknown 401/403) ──────

    [Theory]
    [InlineData(Outcome.AuthenticationRejected, "PRODUCT_WORKFLOW_IDENTITY_CONFIGURATION_INVALID", Scope.AllTenants)]
    [InlineData(Outcome.AuthenticationRejected, "PRODUCT_WORKFLOW_IDENTITY_REJECTED", Scope.AllTenants)]
    [InlineData(Outcome.AuthenticationRejected, "PRODUCT_WORKFLOW_IDENTITY_NOT_GRANTED", Scope.Tenant)]
    [InlineData(Outcome.AuthenticationRejected, "PRODUCT_WORKFLOW_IDENTITY_RESPONSE_INVALID", Scope.AllTenants)]
    [InlineData(Outcome.AuthenticationRejected, "WORKFLOW_TRUSTED_CONSUMER_UNAUTHENTICATED", Scope.AllTenants)]
    [InlineData(Outcome.AuthenticationRejected, "A_401_NOBODY_HAS_SEEN_YET", Scope.AllTenants)]
    [InlineData(Outcome.Forbidden, "WORKFLOW_TRUSTED_CONSUMER_FORBIDDEN", Scope.AllTenants)]
    [InlineData(Outcome.Forbidden, "WORKFLOW_TRUSTED_START_FORBIDDEN", Scope.None)]
    [InlineData(Outcome.Forbidden, "A_403_NOBODY_HAS_SEEN_YET", Scope.None)]
    public async Task Submit_ServiceIdentityRefused_DoesNotQuarantine_AndTheMakerIsToldToResubmit(
        Outcome outcome, string code, Scope scope)
    {
        var harness = Harness.Create();
        harness.Client.Start = (_, _, _, _) => Failed<ProductIdentityWorkflowStartResult>(outcome, code);

        var result = await Submit(harness);

        var stored = harness.Repository.Current!;
        Assert.False(result.Succeeded);
        Assert.Equal(503, result.StatusCode);
        Assert.Equal(Resubmit, result.ErrorCode);
        Assert.Equal(scope, result.FaultScope);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.AwaitingMakerReplay, stored.Checkpoint);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.AwaitingMakerReplay, stored.RecoveryDisposition);
        Assert.Equal(Resubmit, stored.LastFailureCode);
        Assert.Equal(0, stored.ServiceFaultCount); // a maker's attempt is not a worker attempt
        Assert.NotNull(stored.ServiceFaultSinceUtcTicksV1);
        Assert.Null(stored.NextAttemptAtUtcTicksV1);
        Assert.Null(stored.LeaseOwner);
        Assert.Equal(1, harness.Client.StartCalls); // refused once, not repeated until the step budget ran out
        Assert.Equal(0, harness.Client.StartResultCalls);
        Assert.Equal(0, harness.Products.SubmitCount);
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, harness.Product.LifecycleStatus);
        var line = Assert.Single(harness.Log.Entries);
        Assert.Equal(LogLevel.Warning, line.Level);
        Assert.Contains(code, line.Message, StringComparison.Ordinal);
        Assert.Contains(OperationId.ToString(), line.Message, StringComparison.Ordinal);
        Assert.Contains(TenantId.ToString(), line.Message, StringComparison.Ordinal);
        Assert.Contains("service fault at Start", line.Message, StringComparison.Ordinal);
        Assert.Contains("worker attempt 0", line.Message, StringComparison.Ordinal);
        Assert.Contains("the maker resubmits", line.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("the worker retries", line.Message, StringComparison.Ordinal);
    }

    // ── submit: the engine answered, and the reason is the maker's or the set-up's (rows 9, 12, 14, 16, 17) ───────

    [Theory]
    [InlineData(Outcome.AuthenticationRejected, "WORKFLOW_DELEGATED_USER_UNAUTHENTICATED",
        ProductIdentityWorkflowMakerCodes.SessionExpired, 401)]
    [InlineData(Outcome.Forbidden, "WORKFLOW_DELEGATED_USER_FORBIDDEN",
        ProductIdentityWorkflowMakerCodes.StartPermissionRequired, 403)]
    [InlineData(Outcome.Invalid, "WORKFLOW_ASSIGNMENT_CANDIDATES_REQUIRED",
        ProductIdentityWorkflowMakerCodes.ApproverNotAssigned, 409)]
    [InlineData(Outcome.NotFound, "NOT_FOUND_NON_LEAKAGE",
        ProductIdentityWorkflowMakerCodes.DefinitionNotReady, 409)]
    [InlineData(Outcome.Conflict, "WORKFLOW_TEMPLATE_NO_ACTIVE_VERSION",
        ProductIdentityWorkflowMakerCodes.DefinitionNotReady, 409)]
    [InlineData(Outcome.Conflict, "WORKFLOW_TEMPLATE_VERSION_NOT_FOUND",
        ProductIdentityWorkflowMakerCodes.DefinitionNotReady, 409)]
    [InlineData(Outcome.Conflict, "WORKFLOW_TEMPLATE_NOT_PUBLISHED",
        ProductIdentityWorkflowMakerCodes.DefinitionNotReady, 409)]
    public async Task Submit_RefusedForTheMakerOrTheSetUp_DoesNotQuarantine_AndCarriesAStableCode(
        Outcome outcome, string code, string makerCode, int status)
    {
        var harness = Harness.Create();
        harness.Client.Start = (_, _, _, _) => Failed<ProductIdentityWorkflowStartResult>(outcome, code);

        var result = await Submit(harness);

        var stored = harness.Repository.Current!;
        Assert.False(result.Succeeded);
        Assert.Equal(status, result.StatusCode);
        Assert.Equal(makerCode, result.ErrorCode);
        Assert.Equal(Scope.None, result.FaultScope);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.AwaitingMakerReplay, stored.Checkpoint);
        Assert.Equal(makerCode, stored.LastFailureCode);
        Assert.Equal(0, stored.ServiceFaultCount);
        Assert.Null(stored.ServiceFaultSinceUtcTicksV1);
        Assert.Equal(1, harness.Client.StartCalls);
        Assert.Equal(0, harness.Client.StartResultCalls);
        var line = Assert.Single(harness.Log.Entries);
        Assert.Equal(LogLevel.Warning, line.Level);
        Assert.Contains(code, line.Message, StringComparison.Ordinal);
        Assert.Contains(makerCode, line.Message, StringComparison.Ordinal);
    }

    // ── submit: the answer is about the record (rows 13, 15, 18, 21 and unknown 400/409) ──────────────────────────

    [Theory]
    [InlineData(Outcome.Forbidden, "WORKFLOW_TRUSTED_CONSUMER_TENANT_MISMATCH")]
    [InlineData(Outcome.Invalid, "WORKFLOW_TRUSTED_START_INVALID")]
    [InlineData(Outcome.Invalid, "VALIDATION_FAILED")]
    [InlineData(Outcome.Invalid, "PRODUCT_WORKFLOW_REQUEST_INVALID")]
    [InlineData(Outcome.Invalid, "PRODUCT_WORKFLOW_FAILURE_WITHOUT_REASON")]
    [InlineData(Outcome.Invalid, "A_400_NOBODY_HAS_SEEN_YET")]
    [InlineData(Outcome.Conflict, "WORKFLOW_START_IDEMPOTENCY_CONFLICT")]
    [InlineData(Outcome.Conflict, "WORKFLOW_START_RECOVERY_CONFLICT")]
    [InlineData(Outcome.Conflict, "A_409_NOBODY_HAS_SEEN_YET")]
    public async Task Submit_AnAnswerAboutTheRecord_StillQuarantines(Outcome outcome, string code)
    {
        var harness = Harness.Create();
        harness.Client.Start = (_, _, _, _) => Failed<ProductIdentityWorkflowStartResult>(outcome, code);

        var result = await Submit(harness);

        Assert.False(result.Succeeded);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal("PRODUCT_IDENTITY_WORKFLOW_RECONCILIATION_REQUIRED", result.ErrorCode);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired,
            harness.Repository.Current!.Checkpoint);
        Assert.Equal(code, harness.Repository.Current.LastFailureCode);
        Assert.Equal(1, harness.Client.StartCalls);
        var line = Assert.Single(harness.Log.Entries);
        Assert.Equal(LogLevel.Error, line.Level);
        Assert.Contains("quarantined", line.Message, StringComparison.Ordinal);
        Assert.Contains(code, line.Message, StringComparison.Ordinal);
    }

    // ── submit: no answer, or one nobody could read (rows 6, 20, 22) ──────────────────────────────────────────────

    [Theory]
    [InlineData(Outcome.Retryable, "PRODUCT_WORKFLOW_IDENTITY_UNAVAILABLE")]
    [InlineData(Outcome.Timeout, "PRODUCT_WORKFLOW_IDENTITY_TIMEOUT")]
    [InlineData(Outcome.Retryable, "PRODUCT_WORKFLOW_UNAVAILABLE")]
    [InlineData(Outcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT")]
    [InlineData(Outcome.Invalid, "PRODUCT_WORKFLOW_RESPONSE_UNREADABLE")]
    [InlineData(Outcome.Invalid, "PRODUCT_WORKFLOW_RESPONSE_INVALID")]
    public async Task Submit_OutcomeUnknownAndTheLookupFailsToo_WaitsForTheWorker_AndTheMakerIsNotToldQueued(
        Outcome outcome, string code)
    {
        var harness = Harness.Create();
        harness.Client.Start = (_, _, _, _) => Failed<ProductIdentityWorkflowStartResult>(outcome, code);
        harness.Client.StartResult = (_, _, _) => Failed<ProductIdentityWorkflowStartResult>(outcome, code);

        var result = await Submit(harness);

        var stored = harness.Repository.Current!;
        Assert.False(result.Succeeded);
        Assert.Equal(503, result.StatusCode);
        Assert.Equal(Resubmit, result.ErrorCode);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.StartOutcomeUnknown, stored.Checkpoint);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable, stored.RecoveryDisposition);
        Assert.Equal(code, stored.LastFailureCode);
        // The maker's attempt neither counts nor moves the worker's next attempt: the worker looks it up at its
        // next cycle.
        Assert.Equal(0, stored.ServiceFaultCount);
        Assert.NotNull(stored.ServiceFaultSinceUtcTicksV1);
        Assert.Null(stored.NextAttemptAtUtcTicksV1);
        Assert.Equal(1, harness.Client.StartCalls);
        Assert.Equal(1, harness.Client.StartResultCalls);
        var line = Assert.Single(harness.Log.Entries);
        Assert.Contains("service fault at StartResult", line.Message, StringComparison.Ordinal);
        Assert.Contains("the worker's schedule is unchanged", line.Message, StringComparison.Ordinal);
        // A success this service cannot accept is a version mismatch: said as an error the first time.
        Assert.Equal(code == "PRODUCT_WORKFLOW_RESPONSE_INVALID" ? LogLevel.Error : LogLevel.Warning, line.Level);
    }

    [Fact]
    // BL-552 flow 3: a start sent in THIS request and unanswered — "nothing under its key" right after it may only mean
    // the engine is still on it. The draft is held for the settle window (before: handed back to the maker at once,
    // and an edit could leave an approval the engine was still creating without its product).
    public async Task Submit_OutcomeUnknownAndTheLookupSaysNothingYet_HoldsTheDraftForTheSettleWindow()
    {
        var harness = Harness.Create();
        harness.Client.Start = (_, _, _, _) =>
            Failed<ProductIdentityWorkflowStartResult>(Outcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT");
        harness.Client.StartResult = (_, _, _) =>
            Failed<ProductIdentityWorkflowStartResult>(Outcome.NotFound, "NOT_FOUND_NON_LEAKAGE");

        var result = await Submit(harness);

        Assert.Equal(503, result.StatusCode);
        Assert.Equal(Resubmit, result.ErrorCode);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.StartOutcomeUnknown,
            harness.Repository.Current!.Checkpoint);
        Assert.Equal(GlobalProductIdentityWorkflowProcessor.StartSettlingCode, harness.Repository.Current.LastFailureCode);
        Assert.Equal(1, harness.Client.StartCalls);
        var line = Assert.Single(harness.Log.Entries);
        Assert.Contains("service fault at Start", line.Message, StringComparison.Ordinal);
        Assert.Contains("PRODUCT_WORKFLOW_TIMEOUT", line.Message, StringComparison.Ordinal);
    }

    [Fact]
    // BL-552 flow 3: "nothing arrived", once the settle window of the last start has passed, is the engine answering
    // for this operation — the streak of faults ends and the operation is the maker's again. (On the maker's own path a
    // start is sent in the same request, so its "nothing yet" is inside the window: see the test above.)
    public async Task Recover_OutcomeUnknownAndTheLookupSaysNothingArrived_AfterTheWindow_EndsTheStreak()
    {
        var harness = Harness.Create();
        var operation = harness.CreateOperation(GlobalProductIdentityWorkflowCheckpoint.StartOutcomeUnknown);
        operation.ServiceFaultCount = 3;
        operation.ServiceFaultSinceUtcTicksV1 = DateTimeOffset.UtcNow.AddMinutes(-2).UtcTicks;
        operation.LastStartSentAtUtcTicksV1 = DateTimeOffset.UtcNow.AddMinutes(-10).UtcTicks;
        harness.Repository.Current = operation;
        harness.Client.StartResult = (_, _, _) =>
            Failed<ProductIdentityWorkflowStartResult>(Outcome.NotFound, "NOT_FOUND_NON_LEAKAGE");

        await harness.Processor.RecoverAsync(operation, "worker-1", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(30));

        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.AwaitingMakerReplay, operation.Checkpoint);
        Assert.Equal(0, operation.ServiceFaultCount);
        Assert.Null(operation.ServiceFaultSinceUtcTicksV1);
        Assert.Equal(0, harness.Client.StartCalls);
    }

    // ── the maker's resubmit ──────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Resubmit_AfterAServiceFault_StartsTheApprovalAndClearsTheFault()
    {
        var harness = Harness.Create();
        harness.Client.Start = (_, _, _, _) =>
            Failed<ProductIdentityWorkflowStartResult>(Outcome.AuthenticationRejected, "PRODUCT_WORKFLOW_IDENTITY_REJECTED");
        Assert.Equal(503, (await Submit(harness)).StatusCode);

        harness.Client.Start = (_, _, _, _) => Task.FromResult(
            ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>.Success(StartResult()));
        var second = await Submit(harness);

        Assert.True(second.Succeeded);
        Assert.True(second.IsReplay);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision, second.Operation!.Checkpoint);
        Assert.Equal(0, second.Operation.ServiceFaultCount);
        Assert.Null(second.Operation.ServiceFaultSinceUtcTicksV1);
        Assert.Equal(1, harness.Products.SubmitCount);
    }

    [Fact]
    public async Task Resubmit_WhoseOutcomeIsUnknownAgain_GoesBackToTheWorkersLookup()
    {
        var harness = Harness.Create();
        var operation = harness.CreateOperation(GlobalProductIdentityWorkflowCheckpoint.AwaitingMakerReplay);
        operation.LastFailureCode = Resubmit;
        harness.Repository.Current = operation;
        harness.Client.Start = (_, _, _, _) =>
            Failed<ProductIdentityWorkflowStartResult>(Outcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT");
        harness.Client.StartResult = (_, _, _) =>
            Failed<ProductIdentityWorkflowStartResult>(Outcome.Retryable, "PRODUCT_WORKFLOW_UNAVAILABLE");

        var result = await Submit(harness);

        Assert.Equal(503, result.StatusCode);
        Assert.Equal(Resubmit, result.ErrorCode);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.StartOutcomeUnknown,
            harness.Repository.Current!.Checkpoint);
        Assert.Null(harness.Repository.Current.LeaseOwner);
    }

    // ── worker: the decision poll (rows 2–8, 10, 20, 22 and unknown 401/403) ──────────────────────────────────────

    [Theory]
    [InlineData(Outcome.AuthenticationRejected, "PRODUCT_WORKFLOW_IDENTITY_CONFIGURATION_INVALID", Scope.AllTenants)]
    [InlineData(Outcome.AuthenticationRejected, "PRODUCT_WORKFLOW_IDENTITY_REJECTED", Scope.AllTenants)]
    [InlineData(Outcome.AuthenticationRejected, "PRODUCT_WORKFLOW_IDENTITY_NOT_GRANTED", Scope.Tenant)]
    [InlineData(Outcome.AuthenticationRejected, "PRODUCT_WORKFLOW_IDENTITY_RESPONSE_INVALID", Scope.AllTenants)]
    [InlineData(Outcome.AuthenticationRejected, "WORKFLOW_TRUSTED_CONSUMER_UNAUTHENTICATED", Scope.AllTenants)]
    [InlineData(Outcome.AuthenticationRejected, "A_401_NOBODY_HAS_SEEN_YET", Scope.AllTenants)]
    [InlineData(Outcome.Forbidden, "WORKFLOW_TRUSTED_CONSUMER_FORBIDDEN", Scope.AllTenants)]
    [InlineData(Outcome.Forbidden, "A_403_NOBODY_HAS_SEEN_YET", Scope.None)]
    [InlineData(Outcome.Invalid, "PRODUCT_WORKFLOW_RESPONSE_UNREADABLE", Scope.None)]
    [InlineData(Outcome.Retryable, "PRODUCT_WORKFLOW_IDENTITY_UNAVAILABLE", Scope.None)]
    [InlineData(Outcome.Retryable, "PRODUCT_WORKFLOW_UNAVAILABLE", Scope.None)]
    [InlineData(Outcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT", Scope.None)]
    public async Task Worker_ServiceFaultWhileAwaitingTheDecision_LeavesTheOperationWhereItIs(
        Outcome outcome, string code, Scope scope)
    {
        var harness = Harness.CreatePending();
        harness.Client.Terminal = (_, _, _) => Failed<ProductIdentityWorkflowTerminalEvidence>(outcome, code);

        var result = await Recover(harness);

        var stored = harness.Repository.Current!;
        Assert.False(result.Succeeded);
        Assert.Equal(503, result.StatusCode);
        Assert.Equal(code, result.ErrorCode);
        Assert.Equal(scope, result.FaultScope);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision, stored.Checkpoint);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable, stored.RecoveryDisposition);
        Assert.Equal(code, stored.LastFailureCode);
        Assert.Equal(1, stored.ServiceFaultCount);
        Assert.NotNull(stored.ServiceFaultSinceUtcTicksV1);
        Assert.Equal(TimeSpan.FromSeconds(30).Ticks, stored.NextAttemptAtUtcTicksV1 - stored.UpdatedAtUtcTicksV1);
        Assert.Null(stored.LeaseOwner);
        Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval, harness.Product.LifecycleStatus);
        Assert.Equal(0, harness.Products.ReconcileCount);
        var line = Assert.Single(harness.Log.Entries);
        Assert.Equal(LogLevel.Warning, line.Level);
        Assert.Contains(code, line.Message, StringComparison.Ordinal);
        Assert.Contains("service fault at TerminalEvidence", line.Message, StringComparison.Ordinal);
        Assert.Contains("worker attempt 1", line.Message, StringComparison.Ordinal);
        Assert.Contains("the worker retries", line.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(Outcome.Conflict, "WORKFLOW_TERMINAL_EVIDENCE_INCONSISTENT")]
    [InlineData(Outcome.Invalid, "PRODUCT_WORKFLOW_FAILURE_WITHOUT_REASON")]
    [InlineData(Outcome.Invalid, "WORKFLOW_TERMINAL_EVIDENCE_INVALID")]
    [InlineData(Outcome.Forbidden, "WORKFLOW_TRUSTED_CONSUMER_TENANT_MISMATCH")]
    public async Task Worker_AnAnswerAboutTheRecordWhileAwaitingTheDecision_StillQuarantines(
        Outcome outcome, string code)
    {
        var harness = Harness.CreatePending();
        harness.Client.Terminal = (_, _, _) => Failed<ProductIdentityWorkflowTerminalEvidence>(outcome, code);

        var result = await Recover(harness);

        Assert.False(result.Succeeded);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired,
            harness.Repository.Current!.Checkpoint);
        Assert.Equal(code, harness.Repository.Current.LastFailureCode);
    }

    [Theory]
    [InlineData(Outcome.AuthenticationRejected, "PRODUCT_WORKFLOW_IDENTITY_REJECTED", Scope.AllTenants)]
    [InlineData(Outcome.AuthenticationRejected, "PRODUCT_WORKFLOW_IDENTITY_NOT_GRANTED", Scope.Tenant)]
    [InlineData(Outcome.Forbidden, "WORKFLOW_TRUSTED_CONSUMER_FORBIDDEN", Scope.AllTenants)]
    [InlineData(Outcome.Invalid, "PRODUCT_WORKFLOW_RESPONSE_UNREADABLE", Scope.None)]
    [InlineData(Outcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT", Scope.None)]
    public async Task Worker_ServiceFaultWhileLookingUpAStart_LeavesTheOperationWhereItIs(
        Outcome outcome, string code, Scope scope)
    {
        var harness = Harness.Create();
        harness.Repository.Current = harness.CreateOperation(GlobalProductIdentityWorkflowCheckpoint.StartOutcomeUnknown);
        harness.Client.StartResult = (_, _, _) => Failed<ProductIdentityWorkflowStartResult>(outcome, code);

        var result = await Recover(harness);

        Assert.Equal(503, result.StatusCode);
        Assert.Equal(scope, result.FaultScope);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.StartOutcomeUnknown,
            harness.Repository.Current!.Checkpoint);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable, harness.Repository.Current.RecoveryDisposition);
        Assert.Equal(1, harness.Repository.Current.ServiceFaultCount);
        Assert.Equal(0, harness.Client.StartCalls);
    }

    // ── waiting: back-off, the error threshold, and the reset ─────────────────────────────────────────────────────

    [Fact]
    public async Task Worker_WaitingBacksOffToTheCeiling_TurnsIntoAnErrorAtTheThreshold_AndIsClearedByTheFirstAnswer()
    {
        var harness = Harness.CreatePending(
            settings: new ProductIdentityWorkflowServiceFaultSettings(TimeSpan.FromSeconds(100), 3));
        harness.Client.Terminal = (_, _, _) => Failed<ProductIdentityWorkflowTerminalEvidence>(
            Outcome.AuthenticationRejected, "PRODUCT_WORKFLOW_IDENTITY_REJECTED");
        var delays = new List<long>();
        long? since = null;

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            await Recover(harness);
            var stored = harness.Repository.Current!;
            Assert.Equal(attempt, stored.ServiceFaultCount);
            since ??= stored.ServiceFaultSinceUtcTicksV1;
            Assert.Equal(since, stored.ServiceFaultSinceUtcTicksV1);
            delays.Add((stored.NextAttemptAtUtcTicksV1!.Value - stored.UpdatedAtUtcTicksV1) / TimeSpan.TicksPerSecond);
        }

        Assert.Equal([30L, 60L, 100L, 100L], delays);
        Assert.Equal(
            [LogLevel.Warning, LogLevel.Warning, LogLevel.Error, LogLevel.Error],
            harness.Log.Entries.Select(entry => entry.Level));
        Assert.Contains("attempt 3", harness.Log.Entries[2].Message, StringComparison.Ordinal);
        Assert.Contains(new DateTimeOffset(since!.Value, TimeSpan.Zero).ToString("O"),
            harness.Log.Entries[2].Message, StringComparison.Ordinal);

        harness.Client.Terminal = (_, _, _) => Failed<ProductIdentityWorkflowTerminalEvidence>(
            Outcome.NonTerminal, "WORKFLOW_DECISION_NOT_TERMINAL");
        await Recover(harness);

        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision, harness.Repository.Current!.Checkpoint);
        Assert.Equal(0, harness.Repository.Current.ServiceFaultCount);
        Assert.Null(harness.Repository.Current.ServiceFaultSinceUtcTicksV1);
        Assert.Equal(TimeSpan.FromSeconds(30).Ticks,
            harness.Repository.Current.NextAttemptAtUtcTicksV1 - harness.Repository.Current.UpdatedAtUtcTicksV1);
        Assert.Equal(4, harness.Log.Entries.Count); // an ordinary poll is not a fault and is not logged as one
    }

    // ── withdrawal ────────────────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(Outcome.AuthenticationRejected, "PRODUCT_WORKFLOW_IDENTITY_REJECTED")]
    [InlineData(Outcome.AuthenticationRejected, "PRODUCT_WORKFLOW_IDENTITY_NOT_GRANTED")]
    [InlineData(Outcome.Forbidden, "WORKFLOW_TRUSTED_CONSUMER_FORBIDDEN")]
    [InlineData(Outcome.Invalid, "PRODUCT_WORKFLOW_RESPONSE_UNREADABLE")]
    [InlineData(Outcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT")]
    public async Task Withdraw_ServiceFaultAtThePreflight_DoesNotQuarantine_AndTheMakerIsToldToResubmit(
        Outcome outcome, string code)
    {
        var harness = Harness.CreatePending();
        harness.Client.Preflight = (_, _, _) => Failed<ProductIdentityWorkflowCancellationPreflight>(outcome, code);

        var result = await Withdraw(harness, Guid.NewGuid());

        var stored = harness.Repository.Current!;
        Assert.Equal(503, result.StatusCode);
        Assert.Equal(Resubmit, result.ErrorCode);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.WithdrawalRequested, stored.Checkpoint);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable, stored.RecoveryDisposition);
        Assert.Equal(0, stored.ServiceFaultCount);
        Assert.NotNull(stored.ServiceFaultSinceUtcTicksV1);
        Assert.True(stored.NextAttemptAtUtcTicksV1 > stored.UpdatedAtUtcTicksV1); // not before the settle window
        Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval, harness.Product.LifecycleStatus);
    }

    [Theory]
    [InlineData(Outcome.AuthenticationRejected, "PRODUCT_WORKFLOW_IDENTITY_REJECTED", Resubmit, 503, true)]
    [InlineData(Outcome.AuthenticationRejected, "WORKFLOW_TRUSTED_CONSUMER_UNAUTHENTICATED", Resubmit, 503, true)]
    [InlineData(Outcome.Forbidden, "WORKFLOW_TRUSTED_CONSUMER_FORBIDDEN", Resubmit, 503, true)]
    [InlineData(Outcome.Forbidden, "A_403_NOBODY_HAS_SEEN_YET", Resubmit, 503, true)]
    [InlineData(Outcome.AuthenticationRejected, "WORKFLOW_DELEGATED_USER_UNAUTHENTICATED",
        ProductIdentityWorkflowMakerCodes.SessionExpired, 401, false)]
    public async Task Withdraw_CancellationRefusedBeforeItWasProcessed_GoesBackToAwaitingTheDecision_WhereTheWorkerReads(
        Outcome outcome, string code, string makerCode, int status, bool serviceFault)
    {
        var harness = Harness.CreatePending();
        harness.Client.Preflight = PreflightOk;
        harness.Client.Cancel = (_, _, _, _) => Failed<TransportCancellationEvidence>(outcome, code);

        var result = await Withdraw(harness, Guid.NewGuid());

        var stored = harness.Repository.Current!;
        Assert.Equal(status, result.StatusCode);
        Assert.Equal(makerCode, result.ErrorCode);
        // An approval exists at the engine and may be decided at any moment: the operation never waits where the
        // worker does not read. The withdrawal that did not happen is forgotten, so its requester may ask again.
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision, stored.Checkpoint);
        Assert.Null(stored.WithdrawalCommandId);
        Assert.Null(stored.WithdrawalFingerprint);
        // Nothing was started, so nothing can land: the worker's schedule is exactly what it was before the request
        // (here: none). The settle window is only for a cancellation that may still land.
        Assert.Null(stored.NextAttemptAtUtcTicksV1);
        Assert.Null(stored.LeaseOwner);
        Assert.Equal(0, stored.ServiceFaultCount);
        Assert.Equal(serviceFault, stored.ServiceFaultSinceUtcTicksV1.HasValue);
        Assert.Equal(1, harness.Repository.ClaimCalls - 2); // requested, preflight, cancel — and no fourth round
        Assert.Equal(0, harness.Products.WithdrawCount);
        Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval, harness.Product.LifecycleStatus);
        Assert.Contains(code, Assert.Single(harness.Log.Entries).Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Withdraw_RefusedDuringAnOutage_AndNeverRepeated_TheApproversDecisionIsStillObserved()
    {
        var harness = Harness.CreatePending();
        harness.Client.Preflight = PreflightOk;
        harness.Client.Cancel = (_, _, _, _) => Failed<TransportCancellationEvidence>(
            Outcome.AuthenticationRejected, "PRODUCT_WORKFLOW_IDENTITY_REJECTED");
        Assert.Equal(503, (await Withdraw(harness, Guid.NewGuid())).StatusCode);

        // The requester gives up. The approver approves at the engine; the worker's next cycle reads it.
        harness.Client.Terminal = (_, _, _) => Task.FromResult(
            ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowTerminalEvidence>.Success(TerminalEvidence()));
        var recovered = await Recover(harness);

        Assert.True(recovered.Succeeded);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.Completed, harness.Repository.Current!.Checkpoint);
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, harness.Product.LifecycleStatus);
        Assert.Equal(0, harness.Repository.Current.ServiceFaultCount);
        Assert.Null(harness.Repository.Current.ServiceFaultSinceUtcTicksV1); // a decision was read: the chain answered
    }

    [Theory]
    [InlineData(Outcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT")]
    [InlineData(Outcome.Invalid, "PRODUCT_WORKFLOW_RESPONSE_UNREADABLE")]
    [InlineData(Outcome.Invalid, "PRODUCT_WORKFLOW_RESPONSE_INVALID")]
    public async Task Withdraw_CancellationOutcomeUnknown_DoesNotQuarantine_AndTheMakerIsToldToResubmit(
        Outcome outcome, string code)
    {
        var harness = Harness.CreatePending();
        harness.Client.Preflight = PreflightOk;
        harness.Client.Cancel = (_, _, _, _) => Failed<TransportCancellationEvidence>(outcome, code);

        var result = await Withdraw(harness, Guid.NewGuid());

        Assert.Equal(503, result.StatusCode);
        Assert.Equal(Resubmit, result.ErrorCode);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown,
            harness.Repository.Current!.Checkpoint);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable, harness.Repository.Current.RecoveryDisposition);
        Assert.Equal(0, harness.Repository.Current.ServiceFaultCount);
        Assert.NotNull(harness.Repository.Current.ServiceFaultSinceUtcTicksV1);
        // The cancellation may still land: the worker does not read the approval before the settle window is over.
        Assert.Equal(TimeSpan.FromSeconds(30).Ticks,
            harness.Repository.Current.NextAttemptAtUtcTicksV1 - harness.Repository.Current.UpdatedAtUtcTicksV1);
        var line = Assert.Single(harness.Log.Entries);
        Assert.Contains("the worker reads the approval", line.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("the worker retries", line.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(Outcome.Forbidden, "WORKFLOW_TRUSTED_CANCEL_REQUESTER_FORBIDDEN")]
    [InlineData(Outcome.Invalid, "WORKFLOW_TRUSTED_CANCEL_INVALID")]
    [InlineData(Outcome.Invalid, "PRODUCT_WORKFLOW_FAILURE_WITHOUT_REASON")]
    public async Task Withdraw_AnAnswerAboutTheRecord_StillQuarantines(Outcome outcome, string code)
    {
        var harness = Harness.CreatePending();
        harness.Client.Preflight = PreflightOk;
        harness.Client.Cancel = (_, _, _, _) => Failed<TransportCancellationEvidence>(outcome, code);

        var result = await Withdraw(harness, Guid.NewGuid());

        Assert.False(result.Succeeded);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired,
            harness.Repository.Current!.Checkpoint);
        Assert.Equal(code, harness.Repository.Current.LastFailureCode);
    }

    [Fact]
    public async Task Withdraw_RepeatedByTheMakerAfterAServiceFault_CancelsExactlyOnce()
    {
        var harness = Harness.CreatePending();
        var commandId = Guid.NewGuid();
        harness.Client.Preflight = PreflightOk;
        harness.Client.Cancel = (_, _, _, _) => Failed<TransportCancellationEvidence>(
            Outcome.AuthenticationRejected, "PRODUCT_WORKFLOW_IDENTITY_REJECTED");
        Assert.Equal(503, (await Withdraw(harness, commandId)).StatusCode);

        harness.Client.Cancel = CancelOk;
        var second = await Withdraw(harness, commandId);

        Assert.True(second.Succeeded);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.Completed, harness.Repository.Current!.Checkpoint);
        Assert.Equal(0, harness.Repository.Current.ServiceFaultCount);
        Assert.Equal(1, harness.Products.WithdrawCount);
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, harness.Product.LifecycleStatus);
    }

    // ── the pre-flight: all three paths, before the first write ───────────────────────────────────────────────────

    [Theory]
    [InlineData(GlobalProductIdentityWorkflowCheckpoint.Prepared)]
    [InlineData(GlobalProductIdentityWorkflowCheckpoint.StartOutcomeUnknown)]
    [InlineData(GlobalProductIdentityWorkflowCheckpoint.AwaitingMakerReplay)]
    public async Task Submit_OfAnExistingOperationThatNeedsTheTransport_WhenNotConfigured_Returns503AndWritesNothing(
        GlobalProductIdentityWorkflowCheckpoint checkpoint)
    {
        var harness = Harness.Create(transport: null, new FixedReadiness("PRODUCT_WORKFLOW_CLIENT_CONFIGURATION_INVALID"));
        harness.Repository.Current = harness.CreateOperation(checkpoint);

        var result = await Submit(harness);

        Assert.Equal(503, result.StatusCode);
        Assert.Equal("PRODUCT_WORKFLOW_CLIENT_CONFIGURATION_INVALID", result.ErrorCode);
        Assert.Equal(Scope.AllTenants, result.FaultScope);
        Assert.Equal(0, harness.Repository.ReserveCalls);
        Assert.Equal(0, harness.Repository.ClaimCalls);
        Assert.Equal(0, harness.Repository.AdvanceCalls);
        Assert.Equal(checkpoint, harness.Repository.Current.Checkpoint);
        Assert.Equal(0, harness.Client.StartCalls);
        Assert.Equal(LogLevel.Error, Assert.Single(harness.Log.Entries).Level);
    }

    [Theory]
    [InlineData(GlobalProductIdentityWorkflowCheckpoint.Prepared)]
    [InlineData(GlobalProductIdentityWorkflowCheckpoint.StartOutcomeUnknown)]
    [InlineData(GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision)]
    [InlineData(GlobalProductIdentityWorkflowCheckpoint.WithdrawalRequested)]
    public async Task Worker_WhenNotConfigured_TouchesNothing(GlobalProductIdentityWorkflowCheckpoint checkpoint)
    {
        var harness = Harness.CreatePending(new FixedReadiness("PRODUCT_WORKFLOW_IDENTITY_CONFIGURATION_INVALID"));
        harness.Repository.Current!.Checkpoint = checkpoint;

        var result = await Recover(harness);

        Assert.False(result.Succeeded);
        Assert.Equal(503, result.StatusCode);
        Assert.Equal("PRODUCT_WORKFLOW_IDENTITY_CONFIGURATION_INVALID", result.ErrorCode);
        Assert.Equal(Scope.AllTenants, result.FaultScope);
        Assert.Same(harness.Repository.Current, result.Operation);
        Assert.Equal(0, harness.Repository.ClaimCalls);
        Assert.Equal(0, harness.Repository.AdvanceCalls);
        Assert.Equal(checkpoint, harness.Repository.Current.Checkpoint);
    }

    [Fact]
    public async Task Withdraw_WhenNotConfigured_Returns503AndWritesNothing()
    {
        var harness = Harness.CreatePending(new FixedReadiness("PRODUCT_WORKFLOW_CLIENT_CONFIGURATION_INVALID"));

        var result = await Withdraw(harness, Guid.NewGuid());

        Assert.Equal(503, result.StatusCode);
        Assert.Equal("PRODUCT_WORKFLOW_CLIENT_CONFIGURATION_INVALID", result.ErrorCode);
        Assert.Equal(0, harness.Repository.ClaimCalls);
        Assert.Equal(0, harness.Repository.AdvanceCalls);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision, harness.Repository.Current!.Checkpoint);
        Assert.Null(harness.Repository.Current.WithdrawalCommandId);
    }

    // ── a repeat of an operation that needs no transport is answered from what is stored ──────────────────────────

    [Theory]
    [InlineData(GlobalProductIdentityWorkflowCheckpoint.Completed, 200, null)]
    [InlineData(GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision, 200, null)]
    [InlineData(GlobalProductIdentityWorkflowCheckpoint.AbandonedBeforeWorkflowStart, 200, null)]
    [InlineData(GlobalProductIdentityWorkflowCheckpoint.Superseded, 200, null)]
    [InlineData(GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired, 409,
        "PRODUCT_IDENTITY_WORKFLOW_RECONCILIATION_REQUIRED")]
    public async Task Submit_RepeatedForAnOperationAtRest_IsAnsweredFromTheStoredState_EvenWhenNotConfigured(
        GlobalProductIdentityWorkflowCheckpoint checkpoint, int status, string? code)
    {
        var harness = Harness.Create(transport: null, new FixedReadiness("PRODUCT_WORKFLOW_CLIENT_CONFIGURATION_INVALID"));
        harness.Repository.Current = harness.CreateOperation(checkpoint);

        var result = await Submit(harness);

        Assert.Equal(status, result.StatusCode);
        Assert.Equal(code, result.ErrorCode);
        Assert.True(result.IsReplay);
        Assert.Same(harness.Repository.Current, result.Operation);
        Assert.Equal(0, harness.Repository.ClaimCalls);
        Assert.Empty(harness.Log.Entries);
    }

    [Fact]
    public async Task Withdraw_RepeatedAfterItCompleted_IsAnsweredFromTheStoredState_EvenWhenNotConfigured()
    {
        var harness = Harness.CreatePending();
        var commandId = Guid.NewGuid();
        harness.Client.Preflight = PreflightOk;
        harness.Client.Cancel = CancelOk;
        Assert.True((await Withdraw(harness, commandId)).Succeeded);
        var stored = harness.Repository.Current!;
        var broken = Harness.Create(transport: null, new FixedReadiness("PRODUCT_WORKFLOW_CLIENT_CONFIGURATION_INVALID"));
        broken.Repository.Current = stored;
        broken.Product.WorkflowBinding = harness.Product.WorkflowBinding;
        broken.Product.LifecycleStatus = harness.Product.LifecycleStatus;
        broken.Product.Version = harness.Product.Version;
        broken.Product.AuditIntents.AddRange(harness.Product.AuditIntents);

        var replay = await Withdraw(broken, commandId);

        Assert.True(replay.Succeeded);
        Assert.Equal(200, replay.StatusCode);
        Assert.Equal(0, broken.Repository.ClaimCalls);
    }

    // ── a step that is refused after the claim gives the lease back ───────────────────────────────────────────────

    [Fact]
    public async Task Submit_RepeatedOnACheckpointThisPathDoesNotHandle_IsRefusedAndReleasesTheLease()
    {
        var harness = Harness.CreatePending();
        harness.Repository.Current!.Checkpoint = GlobalProductIdentityWorkflowCheckpoint.WithdrawalObserved;

        var result = await Submit(harness);

        Assert.Equal(409, result.StatusCode);
        Assert.Equal("PRODUCT_IDENTITY_WORKFLOW_CONCURRENCY_CONFLICT", result.ErrorCode);
        Assert.Null(harness.Repository.Current.LeaseOwner);
        Assert.Null(harness.Repository.Current.LeaseUntilUtcTicksV1);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.WithdrawalObserved, harness.Repository.Current.Checkpoint);
    }

    // ── nothing secret reaches the log ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Logs_CarryTheOperationAndTheCode_AndNeverATokenOrASecret()
    {
        // The production client and provider with a configured secret; the wire fails, so every path logs.
        var client = Microsoft.Extensions.Options.Options.Create(
            new Diten.MdmService.Infrastructure.Workflow.ProductIdentityWorkflowClientOptions
            {
                PlatformBaseUrl = "https://platform.internal/"
            });
        var identity = Microsoft.Extensions.Options.Options.Create(
            new Diten.MdmService.Infrastructure.Workflow.AuthProductIdentityWorkflowServiceIdentityProviderOptions
            {
                AuthBaseUrl = "https://auth.internal/",
                ExpectedIssuer = "issuer",
                ClientId = "workflow-client-code",
                ActiveClientSecret = "the-raw-client-secret"
            });
        var network = new RefusingNetwork();
        var transport = new Diten.MdmService.Infrastructure.Workflow.PlatformProductIdentityWorkflowClient(
            network,
            new Diten.MdmService.Infrastructure.Workflow.AuthProductIdentityWorkflowServiceIdentityProvider(
                network, identity, TimeProvider.System),
            client);
        var harness = Harness.Create(transport, new FixedReadiness(null));

        var result = await harness.Processor.StartInteractiveAsync(
            TenantId, ProductId, 0, OperationId, MakerId, "the-makers-delegated-token",
            TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(30));

        Assert.Equal(503, result.StatusCode);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.AwaitingMakerReplay,
            harness.Repository.Current!.Checkpoint);
        Assert.NotEmpty(harness.Log.Entries);
        var everything = string.Join('\n', harness.Log.Entries.Select(entry => entry.Message));
        Assert.Contains(OperationId.ToString(), everything, StringComparison.Ordinal);
        Assert.Contains("PRODUCT_WORKFLOW_IDENTITY_REJECTED", everything, StringComparison.Ordinal);
        Assert.DoesNotContain("the-raw-client-secret", everything, StringComparison.Ordinal);
        Assert.DoesNotContain("the-makers-delegated-token", everything, StringComparison.Ordinal);
        Assert.DoesNotContain("workflow-client-code", everything, StringComparison.Ordinal);
        Assert.DoesNotContain(MakerId.ToString(), everything, StringComparison.Ordinal);
        Assert.DoesNotContain("Bearer", everything, StringComparison.OrdinalIgnoreCase);
    }

    // ── fix round (P-2026-10-02-04-FIX1) ──────────────────────────────────────────────────────────────────────────

    // A success the service cannot accept (item 3): a version mismatch, not a fault of the record.
    [Fact]
    public async Task Worker_ASuccessThatBreaksTheContract_Waits_AndIsAnErrorOnEveryAttempt()
    {
        var harness = Harness.CreatePending();
        harness.Client.Terminal = (_, _, _) => Failed<ProductIdentityWorkflowTerminalEvidence>(
            Outcome.Invalid, "PRODUCT_WORKFLOW_RESPONSE_INVALID");

        var result = await Recover(harness);

        Assert.Equal(503, result.StatusCode);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision, harness.Repository.Current!.Checkpoint);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable, harness.Repository.Current.RecoveryDisposition);
        Assert.Equal(1, harness.Repository.Current.ServiceFaultCount);
        var line = Assert.Single(harness.Log.Entries);
        Assert.Equal(LogLevel.Error, line.Level);
        Assert.Contains("PRODUCT_WORKFLOW_RESPONSE_INVALID", line.Message, StringComparison.Ordinal);

        // Not "the first time": every attempt says it, at the back-off's pace.
        harness.Repository.Current.NextAttemptAtUtcTicksV1 = null;
        await Recover(harness);

        Assert.Equal(2, harness.Repository.Current.ServiceFaultCount);
        Assert.Equal(2, harness.Log.Entries.Count);
        Assert.All(harness.Log.Entries, entry => Assert.Equal(LogLevel.Error, entry.Level));
    }

    // The maker's clicks are not the worker's attempts (items 5 and 6).
    [Fact]
    public async Task TheMakersAttempts_NeitherCount_NorMoveTheWorkersNextAttempt_AndTheWorkersDo()
    {
        var harness = Harness.Create();
        var operation = harness.CreateOperation(GlobalProductIdentityWorkflowCheckpoint.StartOutcomeUnknown);
        operation.ServiceFaultCount = 2;
        operation.ServiceFaultSinceUtcTicksV1 = DateTimeOffset.UtcNow.AddMinutes(-1).UtcTicks;
        operation.UpdatedAtUtcTicksV1 = DateTimeOffset.UtcNow.UtcTicks;
        operation.NextAttemptAtUtcTicksV1 = DateTimeOffset.UtcNow.AddMinutes(1).UtcTicks;
        var since = operation.ServiceFaultSinceUtcTicksV1;
        var next = operation.NextAttemptAtUtcTicksV1;
        harness.Repository.Current = operation;
        harness.Client.Start = (_, _, _, _) =>
            Failed<ProductIdentityWorkflowStartResult>(Outcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT");
        harness.Client.StartResult = (_, _, _) =>
            Failed<ProductIdentityWorkflowStartResult>(Outcome.Retryable, "PRODUCT_WORKFLOW_UNAVAILABLE");

        for (var click = 0; click < 6; click++)
        {
            var answer = await Submit(harness);
            Assert.Equal(503, answer.StatusCode);
            Assert.Equal(Resubmit, answer.ErrorCode);
        }

        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.StartOutcomeUnknown, operation.Checkpoint);
        Assert.Equal(2, operation.ServiceFaultCount);
        Assert.Equal(since, operation.ServiceFaultSinceUtcTicksV1);
        Assert.Equal(next, operation.NextAttemptAtUtcTicksV1);
        Assert.All(harness.Log.Entries, entry => Assert.Equal(LogLevel.Warning, entry.Level));
        Assert.All(harness.Log.Entries, entry => Assert.Contains("worker attempt 2", entry.Message, StringComparison.Ordinal));

        await Recover(harness);

        Assert.Equal(3, operation.ServiceFaultCount);
        Assert.Equal(since, operation.ServiceFaultSinceUtcTicksV1);
        Assert.Equal(TimeSpan.FromSeconds(120).Ticks, operation.NextAttemptAtUtcTicksV1 - operation.UpdatedAtUtcTicksV1);
    }

    [Fact]
    public async Task AnOutageLongerThanTheLongestBackOff_IsAnError_HoweverFewWorkerAttemptsThereWere()
    {
        var harness = Harness.Create();
        var operation = harness.CreateOperation(GlobalProductIdentityWorkflowCheckpoint.AwaitingMakerReplay);
        operation.ServiceFaultSinceUtcTicksV1 = DateTimeOffset.UtcNow.AddMinutes(-16).UtcTicks; // ceiling: 15 min
        operation.UpdatedAtUtcTicksV1 = DateTimeOffset.UtcNow.AddMinutes(-1).UtcTicks; // the streak's last fault
        harness.Repository.Current = operation;
        harness.Client.Start = (_, _, _, _) => Failed<ProductIdentityWorkflowStartResult>(
            Outcome.AuthenticationRejected, "PRODUCT_WORKFLOW_IDENTITY_REJECTED");

        await Submit(harness);

        var line = Assert.Single(harness.Log.Entries);
        Assert.Equal(LogLevel.Error, line.Level);
        Assert.Contains("worker attempt 0", line.Message, StringComparison.Ordinal);
    }

    // A retry that is scheduled for a reason that is NOT a service fault answers with its own reason.
    [Fact]
    public async Task Submit_ARetryThatIsNotAServiceFault_AnswersWithItsOwnReason_NotWithResubmit()
    {
        var harness = Harness.Create();
        harness.Client.Start = (_, _, _, _) => Task.FromResult(
            ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>.Success(StartResult()));
        harness.Products.SubmitFailure = "AUDIT_INTENT_CAPACITY_EXCEEDED";

        var result = await Submit(harness);

        Assert.Equal(503, result.StatusCode);
        Assert.Equal("AUDIT_INTENT_CAPACITY_EXCEEDED", result.ErrorCode);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.WorkflowStarted, harness.Repository.Current!.Checkpoint);
        Assert.Equal(0, harness.Repository.Current.ServiceFaultCount);
        Assert.Empty(harness.Log.Entries);
    }

    // A start repeated on an operation that carries a withdrawal (item 7).
    [Theory]
    [InlineData(GlobalProductIdentityWorkflowCheckpoint.AwaitingMakerReplay)]
    [InlineData(GlobalProductIdentityWorkflowCheckpoint.WithdrawalRequested)]
    [InlineData(GlobalProductIdentityWorkflowCheckpoint.WithdrawalPreflightObserved)]
    [InlineData(GlobalProductIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown)]
    [InlineData(GlobalProductIdentityWorkflowCheckpoint.WithdrawalObserved)]
    [InlineData(GlobalProductIdentityWorkflowCheckpoint.WithdrawalApplied)]
    public async Task Submit_RepeatedOnAnOperationThatCarriesAWithdrawal_IsRefusedWithoutAnyCall(
        GlobalProductIdentityWorkflowCheckpoint checkpoint)
    {
        var harness = Harness.CreatePending();
        harness.Repository.Current!.Checkpoint = checkpoint;
        harness.Repository.Current.WithdrawalCommandId = Guid.NewGuid();
        harness.Product.Version = 0; // the submit this repeats was made against version 0

        var result = await Submit(harness);

        Assert.Equal(409, result.StatusCode);
        Assert.Equal("PRODUCT_IDENTITY_WORKFLOW_STATE_CONFLICT", result.ErrorCode);
        Assert.Equal(0, harness.Repository.ClaimCalls);
        Assert.Equal(0, harness.Repository.AdvanceCalls);
        Assert.Equal(0, harness.Client.StartCalls);
        Assert.Equal(0, harness.Client.StartResultCalls);
        Assert.Equal(checkpoint, harness.Repository.Current.Checkpoint);
    }

    // A refused step gives the lease back and leaves the worker's schedule alone — on both paths.
    [Fact]
    public async Task Worker_AStepTheStoreRefuses_GivesTheLeaseBack_AndKeepsTheSchedule()
    {
        var harness = Harness.CreatePending();
        var operation = harness.Repository.Current!;
        operation.Checkpoint = GlobalProductIdentityWorkflowCheckpoint.LocalPendingApplied;
        operation.RecoveryDisposition = ProductIdentityWorkflowRecoveryDisposition.Retryable;
        operation.NextAttemptAtUtcTicksV1 = 777;
        operation.LastFailureCode = "KEPT";
        harness.Repository.RefuseNextAdvanceTo = GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision;

        var result = await Recover(harness);

        Assert.Equal("PRODUCT_IDENTITY_WORKFLOW_CONCURRENCY_CONFLICT", result.ErrorCode);
        Assert.Null(operation.LeaseOwner);
        Assert.Null(operation.LeaseUntilUtcTicksV1);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.LocalPendingApplied, operation.Checkpoint);
        Assert.Equal(777, operation.NextAttemptAtUtcTicksV1);
        Assert.Equal("KEPT", operation.LastFailureCode);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable, operation.RecoveryDisposition);
    }

    [Fact]
    public async Task Withdraw_AStepTheStoreRefuses_GivesTheLeaseBack()
    {
        var harness = Harness.CreatePending();
        harness.Repository.RefuseNextAdvanceTo = GlobalProductIdentityWorkflowCheckpoint.WithdrawalRequested;

        var result = await Withdraw(harness, Guid.NewGuid());

        Assert.Equal(409, result.StatusCode);
        Assert.Equal("PRODUCT_IDENTITY_WORKFLOW_CONCURRENCY_CONFLICT", result.ErrorCode);
        Assert.Null(harness.Repository.Current!.LeaseOwner);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision, harness.Repository.Current.Checkpoint);
    }

    // A lease does not outlive an attempt that was interrupted (item 4).
    [Fact]
    public async Task Submit_InterruptedInTheMiddleOfAStep_GivesTheLeaseBack()
    {
        var harness = Harness.Create();
        using var source = new CancellationTokenSource();
        harness.Client.Start = (_, _, _, _) => throw new OperationCanceledException(source.Token);

        await Assert.ThrowsAsync<OperationCanceledException>(() => harness.Processor.StartInteractiveAsync(
            TenantId, ProductId, 0, OperationId, MakerId, "maker-token",
            TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(30), source.Token));

        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.StartOutcomeUnknown, harness.Repository.Current!.Checkpoint);
        Assert.Null(harness.Repository.Current.LeaseOwner);
        Assert.Null(harness.Repository.Current.LeaseUntilUtcTicksV1);
    }

    [Fact]
    public async Task Withdraw_InterruptedInTheMiddleOfAStep_GivesTheLeaseBack()
    {
        var harness = Harness.CreatePending();
        harness.Client.Preflight = (_, _, _) => throw new InvalidOperationException("the store or the wire broke");

        await Assert.ThrowsAsync<InvalidOperationException>(() => Withdraw(harness, Guid.NewGuid()));

        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.WithdrawalRequested, harness.Repository.Current!.Checkpoint);
        Assert.Null(harness.Repository.Current.LeaseOwner);
    }

    // A withdrawal the requester did not finish, met by the worker (item 2).
    [Theory]
    [InlineData(GlobalProductIdentityWorkflowCheckpoint.WithdrawalRequested)]
    [InlineData(GlobalProductIdentityWorkflowCheckpoint.WithdrawalPreflightObserved)]
    [InlineData(GlobalProductIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown)]
    [InlineData(GlobalProductIdentityWorkflowCheckpoint.AwaitingMakerReplay)]
    public async Task Worker_AnUnfinishedWithdrawalWhoseApprovalIsStillOpen_IsForgotten_AndTheDecisionIsPolledAgain(
        GlobalProductIdentityWorkflowCheckpoint checkpoint)
    {
        var harness = PendingWithdrawal(checkpoint);
        harness.Client.Preflight = PreflightOk;
        harness.Client.Terminal = (_, _, _) => Failed<ProductIdentityWorkflowTerminalEvidence>(
            Outcome.NonTerminal, "WORKFLOW_DECISION_NOT_TERMINAL");

        await Recover(harness);

        var stored = harness.Repository.Current!;
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision, stored.Checkpoint);
        Assert.Null(stored.WithdrawalCommandId);
        Assert.Null(stored.WithdrawalExpectedWorkflowInstanceVersion);
        Assert.Equal("WORKFLOW_DECISION_NOT_TERMINAL", stored.LastFailureCode); // polled in the same cycle
        Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval, harness.Product.LifecycleStatus);
    }

    [Fact]
    public async Task Worker_AnUnfinishedWithdrawalWhoseApprovalWasDecided_AppliesTheDecision()
    {
        var harness = PendingWithdrawal(GlobalProductIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown);
        harness.Client.Preflight = (_, _, _) => Failed<ProductIdentityWorkflowCancellationPreflight>(
            Outcome.NotFound, "WORKFLOW_TRUSTED_CANCEL_NOT_FOUND");
        harness.Client.Terminal = (_, _, _) => Task.FromResult(
            ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowTerminalEvidence>.Success(TerminalEvidence()));

        var result = await Recover(harness);

        Assert.True(result.Succeeded);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.Completed, harness.Repository.Current!.Checkpoint);
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, harness.Product.LifecycleStatus);
        Assert.Equal(0, harness.Products.WithdrawCount);
    }

    [Fact]
    public async Task Worker_AnUnfinishedWithdrawalThatLanded_StaysWhereTheWorkerKeepsReading_ForItsRequester()
    {
        var harness = PendingWithdrawal(GlobalProductIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown);
        harness.Client.Preflight = (_, _, _) => Failed<ProductIdentityWorkflowCancellationPreflight>(
            Outcome.NotFound, "WORKFLOW_TRUSTED_CANCEL_NOT_FOUND");
        harness.Client.Terminal = (_, _, _) => Failed<ProductIdentityWorkflowTerminalEvidence>(
            Outcome.Conflict, "WORKFLOW_TERMINAL_EVIDENCE_INCONSISTENT");

        var result = await Recover(harness);

        var stored = harness.Repository.Current!;
        Assert.Equal(503, result.StatusCode);
        Assert.Equal(ProductIdentityWorkflowMakerCodes.MakerReplayRequired, result.ErrorCode);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown, stored.Checkpoint);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable, stored.RecoveryDisposition);
        Assert.NotNull(stored.WithdrawalCommandId); // kept: the requester's repeat fetches the evidence
        Assert.Equal(TimeSpan.FromMinutes(15).Ticks, stored.NextAttemptAtUtcTicksV1 - stored.UpdatedAtUtcTicksV1);
        Assert.Null(stored.LeaseOwner);
        Assert.Contains("holds no decision", Assert.Single(harness.Log.Entries).Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Worker_AnUnfinishedWithdrawalWhoseDecisionCouldNotBeAccepted_SaysSo_NotThatItLanded()
    {
        var harness = PendingWithdrawal(GlobalProductIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown);
        harness.Client.Preflight = (_, _, _) => Failed<ProductIdentityWorkflowCancellationPreflight>(
            Outcome.NotFound, "WORKFLOW_TRUSTED_CANCEL_NOT_FOUND");
        harness.Client.Terminal = (_, _, _) => Failed<ProductIdentityWorkflowTerminalEvidence>(
            Outcome.Invalid, "WORKFLOW_TERMINAL_EVIDENCE_INVALID");

        await Recover(harness);

        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown, harness.Repository.Current!.Checkpoint);
        var line = Assert.Single(harness.Log.Entries).Message;
        Assert.Contains("could not be accepted", line, StringComparison.Ordinal);
        Assert.DoesNotContain("appears to have landed", line, StringComparison.Ordinal);
    }

    // The belt the gap wears: a withdrawal whose cancellation landed while its answer was lost IS finished by its
    // requester repeating the same command — measured with the fingerprint a real first request leaves.
    [Fact]
    public async Task Withdraw_ThatLandedWhileItsAnswerWasLost_IsFinishedByTheRequesterRepeatingTheSameCommand()
    {
        var harness = Harness.CreatePending();
        var commandId = Guid.NewGuid();
        harness.Client.Preflight = PreflightOk;
        harness.Client.Cancel = (_, _, _, _) => Failed<TransportCancellationEvidence>(Outcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT");
        Assert.Equal(503, (await Withdraw(harness, commandId)).StatusCode);

        // The worker reads the approval: closed, and it holds no decision.
        harness.Client.Preflight = (_, _, _) => Failed<ProductIdentityWorkflowCancellationPreflight>(
            Outcome.NotFound, "WORKFLOW_TRUSTED_CANCEL_NOT_FOUND");
        harness.Client.Terminal = (_, _, _) => Failed<ProductIdentityWorkflowTerminalEvidence>(
            Outcome.Conflict, "WORKFLOW_TERMINAL_EVIDENCE_INCONSISTENT");
        await Recover(harness);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown, harness.Repository.Current!.Checkpoint);

        // The requester repeats: the engine replays the cancellation it already made.
        harness.Client.Cancel = CancelOk;
        var repeated = await Withdraw(harness, commandId);

        Assert.True(repeated.Succeeded, repeated.ErrorCode);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.Completed, harness.Repository.Current.Checkpoint);
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, harness.Product.LifecycleStatus);
        Assert.Equal(1, harness.Products.WithdrawCount);
    }

    [Theory]
    [InlineData(Outcome.AuthenticationRejected, "PRODUCT_WORKFLOW_IDENTITY_REJECTED", Scope.AllTenants)]
    [InlineData(Outcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT", Scope.None)]
    public async Task Worker_AnUnfinishedWithdrawalDuringAnOutage_WaitsWhereItIs(Outcome outcome, string code, Scope scope)
    {
        var harness = PendingWithdrawal(GlobalProductIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown);
        harness.Client.Preflight = (_, _, _) => Failed<ProductIdentityWorkflowCancellationPreflight>(outcome, code);

        var result = await Recover(harness);

        Assert.Equal(503, result.StatusCode);
        Assert.Equal(scope, result.FaultScope);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown, harness.Repository.Current!.Checkpoint);
        Assert.Equal(1, harness.Repository.Current.ServiceFaultCount);
        Assert.NotNull(harness.Repository.Current.WithdrawalCommandId);
        Assert.Contains("the worker retries", Assert.Single(harness.Log.Entries).Message, StringComparison.Ordinal);
    }

    // Rows written before the withdrawal stopped parking in AwaitingMakerReplay: the requester's repeat still ends.
    [Fact]
    public async Task Withdraw_RepeatedFromAwaitingMakerReplay_WhenTheApprovalWasDecidedMeanwhile_GoesBackToTheDecision()
    {
        var harness = Harness.CreatePending();
        var commandId = Guid.NewGuid();
        harness.Client.Preflight = PreflightOk;
        harness.Client.Cancel = (_, _, _, _) => Failed<TransportCancellationEvidence>(Outcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT");
        await Withdraw(harness, commandId);
        harness.Repository.Current!.Checkpoint = GlobalProductIdentityWorkflowCheckpoint.AwaitingMakerReplay;
        harness.Client.Cancel = (_, _, _, _) => Failed<TransportCancellationEvidence>(
            Outcome.Conflict, "WORKFLOW_TRUSTED_CANCEL_CONFLICT");
        harness.Client.Preflight = (_, _, _) => Failed<ProductIdentityWorkflowCancellationPreflight>(
            Outcome.NotFound, "WORKFLOW_TRUSTED_CANCEL_NOT_FOUND"); // decided meanwhile: not open any more

        var result = await Withdraw(harness, commandId);

        Assert.Equal(409, result.StatusCode);
        Assert.Equal("WORKFLOW_TRUSTED_CANCEL_CONFLICT", result.ErrorCode);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision, harness.Repository.Current.Checkpoint);
    }

    // ── fix round 2 (P-2026-10-02-04-FIX2) ────────────────────────────────────────────────────────────────────────

    // The settle window has a floor that a short retry delay cannot shrink (item 1).
    [Fact]
    public async Task Withdraw_OutcomeUnknown_KeepsTheWorkerAwayForTheSettleFloor_HoweverShortTheRetryDelay()
    {
        var harness = Harness.CreatePending();
        harness.Client.Preflight = PreflightOk;
        harness.Client.Cancel = (_, _, _, _) => Failed<TransportCancellationEvidence>(Outcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT");

        await harness.Processor.WithdrawInteractiveAsync(TenantId, ProductId, 1, Guid.NewGuid(), MakerId,
            "REQUESTER_WITHDRAWAL", null, "maker-token", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(1));

        var stored = harness.Repository.Current!;
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown, stored.Checkpoint);
        Assert.Equal(TimeSpan.FromSeconds(30).Ticks, stored.NextAttemptAtUtcTicksV1 - stored.UpdatedAtUtcTicksV1);
    }

    // "Conflict" from the engine: still open and merely moved on, or decided / closed (item 2).
    [Fact]
    public async Task Withdraw_ConflictWhileTheApprovalIsStillOpen_IsAskedAgainWithFreshVersions_AndWithdraws()
    {
        var harness = Harness.CreatePending();
        var cancels = 0;
        var reads = 0;
        var versions = new List<(int Instance, int Task)>();
        // The approval moves on at every read: versions 3/5, then 4/6, then 5/7.
        harness.Client.Preflight = (_, request, _) => Task.FromResult(
            ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowCancellationPreflight>.Success(new(
                request.WorkflowInstanceId, request.ApprovalTaskId, request.ExpectedObjectType,
                request.ExpectedObjectId, "GP-0001", 3 + reads, 5 + reads++, "Active", "WaitingApproval")));
        harness.Client.Cancel = (tenant, request, token, ct) =>
        {
            versions.Add((request.ExpectedWorkflowInstanceVersion, request.ExpectedApprovalTaskVersion));
            return ++cancels == 1
                ? Failed<TransportCancellationEvidence>(Outcome.Conflict, "WORKFLOW_TRUSTED_CANCEL_CONFLICT")
                : CancelOk(tenant, request, token, ct);
        };

        var result = await Withdraw(harness, Guid.NewGuid());

        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Equal(2, cancels);
        // The second cancellation carries versions read AFTER the conflict — not the ones that conflicted.
        Assert.Equal([(3, 5), (5, 7)], versions);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.Completed, harness.Repository.Current!.Checkpoint);
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, harness.Product.LifecycleStatus);
        Assert.Equal(1, harness.Products.WithdrawCount);
    }

    [Theory]
    [InlineData(Outcome.NotFound, "WORKFLOW_TRUSTED_CANCEL_NOT_FOUND")]
    [InlineData(Outcome.Conflict, "WORKFLOW_TRUSTED_CANCEL_CONFLICT")]
    public async Task Withdraw_ConflictBecauseTheApprovalIsClosed_HasNothingToWithdraw_AndSaysTheSameEveryTime(
        Outcome cancelOutcome, string code)
    {
        var harness = Harness.CreatePending();
        var commandId = Guid.NewGuid();
        var preflights = 0;
        // Open when the withdrawal reads the versions; closed when it is asked again after the conflict.
        harness.Client.Preflight = (tenant, request, ct) => ++preflights == 1
            ? PreflightOk(tenant, request, ct)
            : Failed<ProductIdentityWorkflowCancellationPreflight>(Outcome.NotFound, "WORKFLOW_TRUSTED_CANCEL_NOT_FOUND");
        harness.Client.Cancel = (_, _, _, _) => Failed<TransportCancellationEvidence>(cancelOutcome, code);

        var first = await Withdraw(harness, commandId);
        var again = await Withdraw(harness, commandId);

        Assert.Equal(409, first.StatusCode);
        Assert.Equal(code, first.ErrorCode);
        Assert.Equal(409, again.StatusCode);
        Assert.Equal(code, again.ErrorCode);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision, harness.Repository.Current!.Checkpoint);
        Assert.Equal(commandId, harness.Repository.Current.WithdrawalCommandId); // kept: the same answer for a repeat
        Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval, harness.Product.LifecycleStatus);
    }

    [Fact]
    public async Task Withdraw_ConflictAndTheApprovalCannotBeAsked_IsLeftToTheWorker_NotGuessed()
    {
        var harness = Harness.CreatePending();
        var preflights = 0;
        harness.Client.Preflight = (tenant, request, ct) => ++preflights == 1
            ? PreflightOk(tenant, request, ct)
            : Failed<ProductIdentityWorkflowCancellationPreflight>(Outcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT");
        harness.Client.Cancel = (_, _, _, _) => Failed<TransportCancellationEvidence>(
            Outcome.Conflict, "WORKFLOW_TRUSTED_CANCEL_CONFLICT");

        var result = await Withdraw(harness, Guid.NewGuid());

        Assert.Equal(503, result.StatusCode);
        Assert.Equal(Resubmit, result.ErrorCode);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown, harness.Repository.Current!.Checkpoint);
        Assert.NotNull(harness.Repository.Current.WithdrawalCommandId);
    }

    [Fact]
    public async Task Withdraw_ConflictAndTheApprovalCannotBeAsked_DoesNotCountAsAWorkerAttempt()
    {
        var harness = Harness.CreatePending();
        var operation = harness.Repository.Current!;
        operation.ServiceFaultCount = 3;
        operation.ServiceFaultSinceUtcTicksV1 = DateTimeOffset.UtcNow.AddMinutes(-3).UtcTicks;
        operation.UpdatedAtUtcTicksV1 = DateTimeOffset.UtcNow.AddMinutes(-1).UtcTicks;
        var preflights = 0;
        harness.Client.Preflight = (tenant, request, ct) => ++preflights == 1
            ? PreflightOk(tenant, request, ct)
            : Failed<ProductIdentityWorkflowCancellationPreflight>(Outcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT");
        harness.Client.Cancel = (_, _, _, _) => Failed<TransportCancellationEvidence>(
            Outcome.Conflict, "WORKFLOW_TRUSTED_CANCEL_CONFLICT");

        await Withdraw(harness, Guid.NewGuid());

        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown, operation.Checkpoint);
        Assert.Equal(3, operation.ServiceFaultCount);
    }

    // ── fix round 3 (P-2026-10-02-04-FIX3) ────────────────────────────────────────────────────────────────────────

    // Item 1. A cancellation refused before it was processed says nothing about an EARLIER cancellation of the
    // same withdrawal: that one may have landed, and forgetting the withdrawal would hand a cancelled approval to
    // the decision poll.
    [Theory]
    [InlineData(Outcome.AuthenticationRejected, "WORKFLOW_DELEGATED_USER_UNAUTHENTICATED",
        ProductIdentityWorkflowMakerCodes.SessionExpired, 401)]
    [InlineData(Outcome.AuthenticationRejected, "PRODUCT_WORKFLOW_IDENTITY_REJECTED", Resubmit, 503)]
    [InlineData(Outcome.Forbidden, "WORKFLOW_TRUSTED_CONSUMER_FORBIDDEN", Resubmit, 503)]
    public async Task Withdraw_RepeatedWhileItsOutcomeIsUnknown_AndRefusedBeforeProcessing_KeepsTheWithdrawal(
        Outcome outcome, string code, string answer, int status)
    {
        var harness = Harness.CreatePending();
        var commandId = Guid.NewGuid();
        harness.Client.Preflight = PreflightOk;
        harness.Client.Cancel = (_, _, _, _) => Failed<TransportCancellationEvidence>(Outcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT");
        Assert.Equal(503, (await Withdraw(harness, commandId)).StatusCode);
        var operation = harness.Repository.Current!;
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown, operation.Checkpoint);
        var scheduled = operation.NextAttemptAtUtcTicksV1;

        harness.Client.Cancel = (_, _, _, _) => Failed<TransportCancellationEvidence>(outcome, code);
        var repeated = await Withdraw(harness, commandId);

        Assert.Equal(status, repeated.StatusCode);
        Assert.Equal(answer, repeated.ErrorCode);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown, operation.Checkpoint);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable, operation.RecoveryDisposition);
        Assert.Equal(commandId, operation.WithdrawalCommandId);
        Assert.Equal(3, operation.WithdrawalExpectedWorkflowInstanceVersion);
        Assert.Equal(scheduled, operation.NextAttemptAtUtcTicksV1); // nothing new can land: the schedule is not moved
        Assert.Null(operation.LeaseOwner);
    }

    [Fact]
    public async Task Withdraw_ResumedAfterItsVersionsWereRead_AndRefusedBeforeProcessing_KeepsTheWithdrawal()
    {
        // The first request is cut off in the middle of its cancellation: the versions are read, the cancellation
        // may be at the engine. The repeat is refused for the requester's session.
        var harness = Harness.CreatePending();
        var commandId = Guid.NewGuid();
        harness.Client.Preflight = PreflightOk;
        harness.Client.Cancel = (_, _, _, _) => throw new InvalidOperationException("the wire broke");
        await Assert.ThrowsAsync<InvalidOperationException>(() => Withdraw(harness, commandId));
        var operation = harness.Repository.Current!;
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.WithdrawalPreflightObserved, operation.Checkpoint);

        harness.Client.Cancel = (_, _, _, _) => Failed<TransportCancellationEvidence>(
            Outcome.AuthenticationRejected, "WORKFLOW_DELEGATED_USER_UNAUTHENTICATED");
        var repeated = await Withdraw(harness, commandId);

        Assert.Equal(401, repeated.StatusCode);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown, operation.Checkpoint);
        Assert.Equal(commandId, operation.WithdrawalCommandId);
    }

    [Theory]
    [InlineData(Outcome.AuthenticationRejected, "WORKFLOW_DELEGATED_USER_UNAUTHENTICATED")]
    [InlineData(Outcome.AuthenticationRejected, "PRODUCT_WORKFLOW_IDENTITY_REJECTED")]
    public async Task Withdraw_FirstAttemptRefusedBeforeProcessing_IsForgotten_AndTheWorkersScheduleIsWhatItWas(
        Outcome outcome, string code)
    {
        var harness = Harness.CreatePending();
        var operation = harness.Repository.Current!;
        var scheduled = DateTimeOffset.UtcNow.AddSeconds(5).UtcTicks; // sooner than any settle window
        operation.NextAttemptAtUtcTicksV1 = scheduled;
        harness.Client.Preflight = PreflightOk;
        harness.Client.Cancel = (_, _, _, _) => Failed<TransportCancellationEvidence>(outcome, code);

        await Withdraw(harness, Guid.NewGuid());

        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision, operation.Checkpoint);
        Assert.Null(operation.WithdrawalCommandId);
        Assert.Equal(scheduled, operation.NextAttemptAtUtcTicksV1); // not pushed out: a refusal does not starve the poll
    }

    // Item 2. One request asks again after a conflict once, and no more.
    [Fact]
    public async Task Withdraw_ASecondConflictInOneRequest_IsAnswered_AndTheWithdrawalCanBeAskedAgain()
    {
        var harness = Harness.CreatePending();
        var commandId = Guid.NewGuid();
        var cancels = 0;
        harness.Client.Preflight = PreflightOk;
        harness.Client.Cancel = (_, _, _, _) =>
        {
            cancels++;
            return Failed<TransportCancellationEvidence>(Outcome.Conflict, "WORKFLOW_TRUSTED_CANCEL_CONFLICT");
        };

        var result = await Withdraw(harness, commandId);

        var operation = harness.Repository.Current!;
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(ProductIdentityWorkflowMakerCodes.ApprovalChanging, result.ErrorCode);
        Assert.Equal(2, cancels);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision, operation.Checkpoint);
        Assert.Null(operation.WithdrawalCommandId);
        Assert.Null(operation.LeaseOwner);
        Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval, harness.Product.LifecycleStatus);

        // The repeat begins a fresh withdrawal and goes through.
        harness.Client.Cancel = CancelOk;
        var repeated = await Withdraw(harness, commandId);

        Assert.True(repeated.Succeeded, repeated.ErrorCode);
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, harness.Product.LifecycleStatus);
    }

    [Fact]
    public async Task Withdraw_AStoreThatAcknowledgesWithoutMoving_EndsAtTheStepBudget_NotInALoop()
    {
        var harness = Harness.CreatePending();
        harness.Repository.AcknowledgeWithoutMoving = int.MaxValue;
        harness.Client.Preflight = PreflightOk;

        var result = await Withdraw(harness, Guid.NewGuid());

        Assert.Equal(503, result.StatusCode);
        Assert.Equal("PRODUCT_IDENTITY_WORKFLOW_STEP_BUDGET_EXCEEDED", result.ErrorCode);
        Assert.Equal(10, harness.Repository.ClaimCalls);
    }

    [Fact]
    public async Task Withdraw_ThatWasAppliedWithItsLastStep_IsASuccess_NotAStepBudgetError()
    {
        var harness = Harness.CreatePending();
        harness.Repository.AcknowledgeWithoutMoving = 5; // five steps are lost; the five that remain are exactly enough
        harness.Client.Preflight = PreflightOk;
        harness.Client.Cancel = CancelOk;

        var result = await Withdraw(harness, Guid.NewGuid());

        Assert.Equal(10, harness.Repository.ClaimCalls);
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, harness.Product.LifecycleStatus);
        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.Completed, harness.Repository.Current!.Checkpoint);
    }

    // Item 3. The withdrawal's answer is its own: the decision poll rewrites LastFailureCode and the repeat is
    // still told what the first request was told.
    [Fact]
    public async Task Withdraw_RefusedBecauseTheApprovalIsClosed_GivesTheSameAnswer_AfterTheWorkerPolledTheDecision()
    {
        var harness = Harness.CreatePending();
        var commandId = Guid.NewGuid();
        harness.Client.Preflight = (_, _, _) => Failed<ProductIdentityWorkflowCancellationPreflight>(
            Outcome.NotFound, "WORKFLOW_TRUSTED_CANCEL_NOT_FOUND");
        var first = await Withdraw(harness, commandId);
        harness.Client.Terminal = (_, _, _) => Failed<ProductIdentityWorkflowTerminalEvidence>(
            Outcome.NonTerminal, "WORKFLOW_DECISION_NOT_TERMINAL");
        await Recover(harness);
        Assert.Equal("WORKFLOW_DECISION_NOT_TERMINAL", harness.Repository.Current!.LastFailureCode);

        var again = await Withdraw(harness, commandId);

        Assert.Equal((409, "WORKFLOW_TRUSTED_CANCEL_NOT_FOUND"), (first.StatusCode, first.ErrorCode));
        Assert.Equal((409, "WORKFLOW_TRUSTED_CANCEL_NOT_FOUND"), (again.StatusCode, again.ErrorCode));
    }

    // Item 4. The engine's reason is stored and handed out only when it is a code — never a text of any length.
    [Theory]
    [InlineData(300, false)]
    [InlineData(128, true)]
    public async Task Withdraw_RefusedBecauseTheApprovalIsClosed_NeverStoresOrReturnsAnUnboundedReason(
        int length, bool kept)
    {
        var harness = Harness.CreatePending();
        var reason = new string('X', length);
        harness.Client.Preflight = (_, _, _) => Failed<ProductIdentityWorkflowCancellationPreflight>(
            Outcome.NotFound, reason);

        var result = await Withdraw(harness, Guid.NewGuid());

        var expected = kept ? reason : ProductIdentityWorkflowMakerCodes.ApprovalClosed;
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(expected, result.ErrorCode);
        Assert.Equal(expected, harness.Repository.Current!.WithdrawalRefusalCode);
        Assert.Equal(expected, harness.Repository.Current.LastFailureCode);
    }

    // Item 6: an approval found closed is an answer (the streak ends) and nothing can land (the schedule stays).
    [Fact]
    public async Task Withdraw_RefusedBecauseTheApprovalIsClosed_EndsTheStreak_AndLeavesTheWorkersSchedule()
    {
        var harness = Harness.CreatePending();
        var operation = harness.Repository.Current!;
        var scheduled = DateTimeOffset.UtcNow.AddMinutes(12).UtcTicks;
        operation.NextAttemptAtUtcTicksV1 = scheduled;
        operation.ServiceFaultCount = 3;
        operation.ServiceFaultSinceUtcTicksV1 = DateTimeOffset.UtcNow.AddMinutes(-3).UtcTicks;
        operation.UpdatedAtUtcTicksV1 = DateTimeOffset.UtcNow.AddMinutes(-1).UtcTicks;
        harness.Client.Preflight = (_, _, _) => Failed<ProductIdentityWorkflowCancellationPreflight>(
            Outcome.NotFound, "WORKFLOW_TRUSTED_CANCEL_NOT_FOUND");

        await Withdraw(harness, Guid.NewGuid());

        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision, operation.Checkpoint);
        Assert.Equal(0, operation.ServiceFaultCount);
        Assert.Null(operation.ServiceFaultSinceUtcTicksV1);
        Assert.Equal(scheduled, operation.NextAttemptAtUtcTicksV1);
    }

    // Item 5. A request cancelled while its lease is being taken does not leave the lease behind.
    [Fact]
    public async Task Withdraw_CancelledWhileItsLeaseIsBeingTaken_DoesNotLeaveTheLeaseBehind()
    {
        var harness = Harness.CreatePending();
        var operation = harness.Repository.Current!;
        using var caller = new CancellationTokenSource();
        harness.Repository.WhileClaiming = caller.Cancel;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => harness.Processor.WithdrawInteractiveAsync(
            TenantId, ProductId, 1, Guid.NewGuid(), MakerId, "REQUESTER_WITHDRAWAL", null,
            "maker-token", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(30), caller.Token));

        Assert.Equal(1, harness.Repository.ClaimCalls);
        Assert.Null(operation.LeaseOwner);
        Assert.Null(operation.LeaseUntilUtcTicksV1);
    }

    [Fact]
    public async Task Submit_CancelledWhileItsLeaseIsBeingTaken_DoesNotLeaveTheLeaseBehind()
    {
        var harness = Harness.Create();
        var operation = harness.CreateOperation(GlobalProductIdentityWorkflowCheckpoint.AwaitingMakerReplay);
        harness.Repository.Current = operation;
        using var caller = new CancellationTokenSource();
        harness.Repository.WhileClaiming = caller.Cancel;
        harness.Client.Start = (_, _, _, ct) =>
        {
            ct.ThrowIfCancellationRequested();
            return Failed<ProductIdentityWorkflowStartResult>(Outcome.Timeout, "unreachable");
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => harness.Processor.StartInteractiveAsync(
            TenantId, ProductId, 0, OperationId, MakerId, "maker-token",
            TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(30), caller.Token));

        Assert.Equal(1, harness.Repository.ClaimCalls);
        Assert.Null(operation.LeaseOwner);
        Assert.Null(operation.LeaseUntilUtcTicksV1);
    }

    // The worker's resolution of an unfinished withdrawal, branch by branch (items 1, 3, 6).
    [Theory]
    [InlineData(Outcome.NonTerminal, "WORKFLOW_DECISION_NOT_TERMINAL")]
    [InlineData(Outcome.NotFound, "NOT_FOUND_NON_LEAKAGE")]
    [InlineData(Outcome.Incomplete, "WORKFLOW_START_NOT_COMPLETED")]
    public async Task Worker_AnUnfinishedWithdrawalWhoseApprovalIsClosedButNotYetReadable_GoesBackToTheDecisionPoll(
        Outcome evidenceOutcome, string code)
    {
        var harness = PendingWithdrawal(GlobalProductIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown);
        harness.Client.Preflight = (_, _, _) => Failed<ProductIdentityWorkflowCancellationPreflight>(
            Outcome.NotFound, "WORKFLOW_TRUSTED_CANCEL_NOT_FOUND");
        harness.Client.Terminal = (_, _, _) => Failed<ProductIdentityWorkflowTerminalEvidence>(evidenceOutcome, code);

        await Recover(harness);

        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision, harness.Repository.Current!.Checkpoint);
        Assert.Null(harness.Repository.Current.WithdrawalCommandId);
        Assert.Equal(code, harness.Repository.Current.LastFailureCode); // the decision poll ran in the same cycle
    }

    [Theory]
    [InlineData(Outcome.AuthenticationRejected, "PRODUCT_WORKFLOW_IDENTITY_REJECTED", Scope.AllTenants)]
    [InlineData(Outcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT", Scope.None)]
    public async Task Worker_AnUnfinishedWithdrawalWhoseEvidenceMeetsAnOutage_WaitsWhereItIs(
        Outcome outcome, string code, Scope scope)
    {
        var harness = PendingWithdrawal(GlobalProductIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown);
        harness.Client.Preflight = (_, _, _) => Failed<ProductIdentityWorkflowCancellationPreflight>(
            Outcome.NotFound, "WORKFLOW_TRUSTED_CANCEL_NOT_FOUND");
        harness.Client.Terminal = (_, _, _) => Failed<ProductIdentityWorkflowTerminalEvidence>(outcome, code);

        var result = await Recover(harness);

        Assert.Equal(scope, result.FaultScope);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown, harness.Repository.Current!.Checkpoint);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable, harness.Repository.Current.RecoveryDisposition);
        Assert.Equal(1, harness.Repository.Current.ServiceFaultCount);
        Assert.NotNull(harness.Repository.Current.WithdrawalCommandId);
        var line = Assert.Single(harness.Log.Entries).Message;
        Assert.Contains("service fault at TerminalEvidence", line, StringComparison.Ordinal);
        Assert.Contains("the worker retries", line, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Worker_AnUnfinishedWithdrawalThatIsNotAWholeRequest_IsQuarantined()
    {
        var harness = PendingWithdrawal(GlobalProductIdentityWorkflowCheckpoint.WithdrawalRequested);
        harness.Repository.Current!.WithdrawalReasonCode = null;

        await Recover(harness);

        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired, harness.Repository.Current.Checkpoint);
        Assert.Equal("PRODUCT_IDENTITY_WITHDRAWAL_CONTRACT_INVALID", harness.Repository.Current.LastFailureCode);
    }

    [Fact]
    public async Task Worker_AnUnfinishedWithdrawalWhosePreflightAnswersAboutTheRecord_IsQuarantined()
    {
        var harness = PendingWithdrawal(GlobalProductIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown);
        harness.Client.Preflight = (_, _, _) => Failed<ProductIdentityWorkflowCancellationPreflight>(
            Outcome.Invalid, "WORKFLOW_TRUSTED_CANCEL_PREFLIGHT_INVALID");

        await Recover(harness);

        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired, harness.Repository.Current!.Checkpoint);
        Assert.Equal("WORKFLOW_TRUSTED_CANCEL_PREFLIGHT_INVALID", harness.Repository.Current.LastFailureCode);
    }

    // Who is at the door decides what happens at WithdrawalRequested: the requester goes on, the worker resolves.
    [Fact]
    public async Task AtWithdrawalRequested_TheRequesterGoesOnToWithdraw_TheWorkerForgetsTheWithdrawal()
    {
        var requester = Harness.CreatePending();
        var commandId = Guid.NewGuid();
        requester.Client.Preflight = (_, _, _) => Failed<ProductIdentityWorkflowCancellationPreflight>(
            Outcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT");
        await Withdraw(requester, commandId);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.WithdrawalRequested, requester.Repository.Current!.Checkpoint);
        requester.Client.Preflight = PreflightOk;
        requester.Client.Cancel = CancelOk;

        var repeated = await Withdraw(requester, commandId);

        Assert.True(repeated.Succeeded, repeated.ErrorCode);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.Completed, requester.Repository.Current.Checkpoint);
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, requester.Product.LifecycleStatus);

        var worker = PendingWithdrawal(GlobalProductIdentityWorkflowCheckpoint.WithdrawalRequested);
        worker.Client.Preflight = PreflightOk;
        worker.Client.Terminal = (_, _, _) => Failed<ProductIdentityWorkflowTerminalEvidence>(
            Outcome.NonTerminal, "WORKFLOW_DECISION_NOT_TERMINAL");

        await Recover(worker);

        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision, worker.Repository.Current!.Checkpoint);
        Assert.Null(worker.Repository.Current.WithdrawalCommandId);
        Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval, worker.Product.LifecycleStatus);
    }

    // A requester's fault keeps what the worker counted — from three, not only from nothing (items 4, 5).
    [Theory]
    [InlineData(Outcome.AuthenticationRejected, "PRODUCT_WORKFLOW_IDENTITY_REJECTED", GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision)]
    [InlineData(Outcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT", GlobalProductIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown)]
    public async Task Withdraw_AServiceFaultMetByTheRequester_KeepsWhatTheWorkerCounted(
        Outcome outcome, string code, GlobalProductIdentityWorkflowCheckpoint ends)
    {
        var harness = Harness.CreatePending();
        var since = DateTimeOffset.UtcNow.AddMinutes(-3).UtcTicks;
        var scheduled = DateTimeOffset.UtcNow.AddMinutes(12).UtcTicks;
        var operation = harness.Repository.Current!;
        operation.ServiceFaultCount = 3;
        operation.ServiceFaultSinceUtcTicksV1 = since;
        operation.UpdatedAtUtcTicksV1 = DateTimeOffset.UtcNow.AddMinutes(-1).UtcTicks;
        operation.NextAttemptAtUtcTicksV1 = scheduled;
        harness.Client.Preflight = PreflightOk;
        harness.Client.Cancel = (_, _, _, _) => Failed<TransportCancellationEvidence>(outcome, code);

        await Withdraw(harness, Guid.NewGuid());

        Assert.Equal(ends, operation.Checkpoint);
        Assert.Equal(3, operation.ServiceFaultCount);
        Assert.Equal(since, operation.ServiceFaultSinceUtcTicksV1);
        Assert.Equal(scheduled, operation.NextAttemptAtUtcTicksV1); // not brought forward: the worker's 12 minutes stand
        Assert.Contains("worker attempt 3", Assert.Single(harness.Log.Entries).Message, StringComparison.Ordinal);
    }

    // An answer from the engine ends a fault streak — a refusal to the maker is an answer (item 4).
    [Fact]
    public async Task AnAnswerFromTheEngine_EvenARefusal_EndsTheFaultStreak()
    {
        var harness = Harness.Create();
        var operation = harness.CreateOperation(GlobalProductIdentityWorkflowCheckpoint.AwaitingMakerReplay);
        operation.ServiceFaultCount = 2;
        operation.ServiceFaultSinceUtcTicksV1 = DateTimeOffset.UtcNow.AddMinutes(-5).UtcTicks;
        operation.UpdatedAtUtcTicksV1 = DateTimeOffset.UtcNow.AddMinutes(-5).UtcTicks;
        harness.Repository.Current = operation;
        harness.Client.Start = (_, _, _, _) => Failed<ProductIdentityWorkflowStartResult>(
            Outcome.Forbidden, "WORKFLOW_DELEGATED_USER_FORBIDDEN");

        var result = await Submit(harness);

        Assert.Equal(403, result.StatusCode);
        Assert.Equal(0, operation.ServiceFaultCount);
        Assert.Null(operation.ServiceFaultSinceUtcTicksV1);
    }

    [Fact]
    public async Task Worker_AFaultStreakThatHasLastedLongerThanTheCeiling_IsAnError_BeforeTheAttemptThreshold()
    {
        var harness = Harness.CreatePending();
        var operation = harness.Repository.Current!;
        operation.ServiceFaultCount = 1;
        operation.ServiceFaultSinceUtcTicksV1 = DateTimeOffset.UtcNow.AddMinutes(-16).UtcTicks;
        operation.UpdatedAtUtcTicksV1 = DateTimeOffset.UtcNow.AddMinutes(-2).UtcTicks;
        harness.Client.Terminal = (_, _, _) => Failed<ProductIdentityWorkflowTerminalEvidence>(
            Outcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT");

        await Recover(harness);

        Assert.Equal(2, operation.ServiceFaultCount);
        var line = Assert.Single(harness.Log.Entries);
        Assert.Equal(LogLevel.Error, line.Level);
        Assert.Contains("worker attempt 2", line.Message, StringComparison.Ordinal);
    }

    // A lease that cannot be given back is said, and does not hide what went wrong (item 6, N7).
    [Fact]
    public async Task ALeaseThatCannotBeGivenBack_IsSaid_AndTheOriginalFailureStillSurfaces()
    {
        var harness = Harness.Create();
        harness.Repository.ThrowWhenALeaseIsGivenBack = true;
        harness.Client.Start = (_, _, _, _) => throw new InvalidOperationException("the wire broke");

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => Submit(harness));

        Assert.Equal("the wire broke", failure.Message);
        var line = Assert.Single(harness.Log.Entries);
        Assert.Equal(LogLevel.Warning, line.Level);
        Assert.Contains("could not be given back", line.Message, StringComparison.Ordinal);
        Assert.Contains("System.TimeoutException", line.Message, StringComparison.Ordinal);
        Assert.Contains(OperationId.ToString(), line.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("not answering", line.Message, StringComparison.Ordinal); // the type, never the message
    }

    // A checkpoint the withdrawal path has no step for: refused, and the lease goes back (PROC default branch).
    [Fact]
    public async Task Withdraw_RepeatedAfterTheDecisionWasObserved_IsRefused_AndGivesTheLeaseBack()
    {
        var harness = Harness.CreatePending();
        var commandId = Guid.NewGuid();
        harness.Client.Preflight = (_, _, _) => Failed<ProductIdentityWorkflowCancellationPreflight>(
            Outcome.NotFound, "WORKFLOW_TRUSTED_CANCEL_NOT_FOUND");
        Assert.Equal(409, (await Withdraw(harness, commandId)).StatusCode); // closed: the request is kept
        var operation = harness.Repository.Current!;
        operation.Checkpoint = GlobalProductIdentityWorkflowCheckpoint.DecisionObserved; // the worker read the decision
        var claims = harness.Repository.ClaimCalls;

        var result = await Withdraw(harness, commandId);

        Assert.Equal(409, result.StatusCode);
        Assert.Equal("PRODUCT_IDENTITY_WORKFLOW_STATE_CONFLICT", result.ErrorCode);
        Assert.Equal(claims + 1, harness.Repository.ClaimCalls);
        Assert.Null(operation.LeaseOwner);
        Assert.Null(operation.LeaseUntilUtcTicksV1);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.DecisionObserved, operation.Checkpoint);
    }

    // ── GSKU FIX1, shared items 13 (A3) and 14 (A1) — Global Product's behaviour changes on purpose ─────────────────

    // A3: a claim that the store never answers does not hang the request; a lease it may have written goes back.
    [Fact]
    public async Task Submit_WhoseClaimTheStoreNeverAnswers_IsAnsweredWithinTheBound_AndGivesTheLeaseBack()
    {
        var harness = Harness.Create();
        harness.Repository.Current = harness.CreateOperation(GlobalProductIdentityWorkflowCheckpoint.AwaitingMakerReplay);
        harness.Repository.ClaimsNeverAnswer = true;
        var watch = System.Diagnostics.Stopwatch.StartNew();

        var result = await harness.Processor.StartInteractiveAsync(TenantId, ProductId, 0, OperationId, MakerId,
            "maker-token", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30));

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10));
        Assert.Equal((503, ProductIdentityWorkflowClaims.StoreUnavailable), (result.StatusCode, result.ErrorCode));
        Assert.Equal(1, harness.Repository.ReleaseIfOwnedCalls);
        Assert.Null(harness.Repository.Current!.LeaseOwner);
        Assert.Null(harness.Repository.Current.LeaseUntilUtcTicksV1);
    }

    [Fact]
    public async Task Withdraw_WhoseClaimTheStoreNeverAnswers_IsAnsweredWithinTheBound_AndGivesTheLeaseBack()
    {
        var harness = Harness.CreatePending();
        harness.Repository.ClaimsNeverAnswer = true;

        var result = await harness.Processor.WithdrawInteractiveAsync(TenantId, ProductId, 1, Guid.NewGuid(), MakerId,
            "REQUESTER_WITHDRAWAL", null, "maker-token", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30));

        Assert.Equal((503, ProductIdentityWorkflowClaims.StoreUnavailable), (result.StatusCode, result.ErrorCode));
        Assert.Equal(1, harness.Repository.ReleaseIfOwnedCalls);
        Assert.Null(harness.Repository.Current!.LeaseOwner);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision, harness.Repository.Current.Checkpoint);
    }

    // A1 (a): the requester who repeats under another command is told which command their withdrawal stands under.
    [Fact]
    public async Task Withdraw_UnderAnotherCommand_IsToldTheCommandItsWithdrawalStandsUnder()
    {
        var harness = PendingWithdrawal(GlobalProductIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown);
        var stored = harness.Repository.Current!.WithdrawalCommandId;

        var result = await Withdraw(harness, Guid.NewGuid());

        Assert.Equal((409, "PRODUCT_IDENTITY_WITHDRAWAL_OPERATION_CONFLICT"), (result.StatusCode, result.ErrorCode));
        Assert.Equal(stored, result.PendingWithdrawalCommandId);
    }

    // A1 (b): a withdrawal that landed and whose requester never comes back is not parked for ever.
    [Theory]
    [InlineData(71, GlobalProductIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown, LogLevel.Warning)]
    [InlineData(72, GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired, LogLevel.Error)]
    public async Task Worker_AWithdrawalThatLandedAndWaitsForItsRequester_IsHandedToReconciliationAfterTheLimit(
        int hoursWaiting, GlobalProductIdentityWorkflowCheckpoint ends, LogLevel level)
    {
        var harness = PendingWithdrawal(GlobalProductIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown);
        var since = DateTimeOffset.UtcNow.AddHours(-hoursWaiting).AddSeconds(-1).UtcTicks;
        harness.Repository.Current!.WithdrawalAwaitingRequesterSinceUtcTicksV1 = since;
        harness.Client.Preflight = (_, _, _) => Failed<ProductIdentityWorkflowCancellationPreflight>(
            Outcome.NotFound, "WORKFLOW_TRUSTED_CANCEL_NOT_FOUND");
        harness.Client.Terminal = (_, _, _) => Failed<ProductIdentityWorkflowTerminalEvidence>(
            Outcome.Conflict, "WORKFLOW_TERMINAL_EVIDENCE_INCONSISTENT");

        await Recover(harness);

        var operation = harness.Repository.Current;
        Assert.Equal(ends, operation.Checkpoint);
        Assert.Equal(level, harness.Log.Entries[0].Level);
        if (ends == GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired)
            Assert.Contains("nobody came back", harness.Log.Entries[0].Message, StringComparison.Ordinal);
        else
            Assert.Single(harness.Log.Entries);
        if (ends == GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired)
            Assert.Equal(GlobalProductIdentityWorkflowProcessor.WithdrawalRequesterNeverReturned, operation.LastFailureCode);
        else
            Assert.Equal(since, operation.WithdrawalAwaitingRequesterSinceUtcTicksV1); // the first moment is kept
    }

    [Fact]
    public async Task Worker_TheFirstTimeAWithdrawalIsFoundWaitingForItsRequester_RecordsWhen()
    {
        var harness = PendingWithdrawal(GlobalProductIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown);
        harness.Client.Preflight = (_, _, _) => Failed<ProductIdentityWorkflowCancellationPreflight>(
            Outcome.NotFound, "WORKFLOW_TRUSTED_CANCEL_NOT_FOUND");
        harness.Client.Terminal = (_, _, _) => Failed<ProductIdentityWorkflowTerminalEvidence>(
            Outcome.Conflict, "WORKFLOW_TERMINAL_EVIDENCE_INCONSISTENT");
        var before = DateTimeOffset.UtcNow.UtcTicks;

        await Recover(harness);

        Assert.True(harness.Repository.Current!.WithdrawalAwaitingRequesterSinceUtcTicksV1 >= before);
    }

    private static Harness PendingWithdrawal(GlobalProductIdentityWorkflowCheckpoint checkpoint)
    {
        var harness = Harness.CreatePending();
        var operation = harness.Repository.Current!;
        operation.Checkpoint = checkpoint;
        operation.WithdrawalCommandId = Guid.NewGuid();
        operation.WithdrawalFingerprint = "fingerprint";
        operation.WithdrawalRequesterSubjectId = MakerId;
        operation.WithdrawalExpectedProductVersion = 1;
        operation.WithdrawalReasonCode = "REQUESTER_WITHDRAWAL";
        operation.WithdrawalExpectedWorkflowInstanceVersion = 3;
        operation.WithdrawalExpectedApprovalTaskVersion = 5;
        return harness;
    }

    private sealed class RefusingNetwork : HttpMessageHandler, IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(this, false);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized));
    }

    private static Task<GlobalProductIdentityWorkflowProcessingResult> Submit(Harness harness) =>
        harness.Processor.StartInteractiveAsync(
            TenantId, ProductId, 0, OperationId, MakerId, "maker-token",
            TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(30));

    private static Task<GlobalProductIdentityWorkflowProcessingResult> Recover(Harness harness) =>
        harness.Processor.RecoverAsync(
            harness.Repository.Current!, "worker-1", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(30));

    private static Task<GlobalProductIdentityWorkflowProcessingResult> Withdraw(Harness harness, Guid commandId) =>
        harness.Processor.WithdrawInteractiveAsync(TenantId, ProductId, 1, commandId,
            MakerId, "REQUESTER_WITHDRAWAL", null, "maker-token", TimeSpan.FromMinutes(1),
            TimeSpan.FromSeconds(30));

    private static Task<ProductIdentityWorkflowTransportResult<T>> Failed<T>(Outcome outcome, string code) =>
        Task.FromResult(ProductIdentityWorkflowTransportResult<T>.Fail(outcome, code));

    private static Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowCancellationPreflight>> PreflightOk(
        Guid tenantId, ProductIdentityWorkflowCancellationPreflightRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowCancellationPreflight>.Success(new(
            request.WorkflowInstanceId, request.ApprovalTaskId, request.ExpectedObjectType,
            request.ExpectedObjectId, "GP-0001", 3, 5, "Active", "WaitingApproval")));

    private static Task<ProductIdentityWorkflowTransportResult<TransportCancellationEvidence>> CancelOk(
        Guid tenantId, ProductIdentityWorkflowCancellationRequest request, string token,
        CancellationToken cancellationToken) =>
        Task.FromResult(ProductIdentityWorkflowTransportResult<TransportCancellationEvidence>.Success(new(
            request.WorkflowInstanceId, request.ApprovalTaskId, TemplateId, TemplateVersionId,
            request.ExpectedObjectType, request.ExpectedObjectId, "GP-0001", "Cancel", MakerId,
            request.ReasonCode, request.Comment, DateTimeOffset.UtcNow, 3, Guid.NewGuid(), "Cancelled",
            "Cancelled", 4, 6, false, "corr")));
}
