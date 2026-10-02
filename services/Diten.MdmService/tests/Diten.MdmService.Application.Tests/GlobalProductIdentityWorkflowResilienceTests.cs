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
        Assert.Equal(1, stored.ServiceFaultCount);
        Assert.NotNull(stored.ServiceFaultSinceUtcTicksV1);
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
        Assert.Contains("Start", line.Message, StringComparison.Ordinal);
        Assert.Contains("attempt 1", line.Message, StringComparison.Ordinal);
        Assert.Contains("maker", line.Message, StringComparison.Ordinal);
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
    [InlineData(Outcome.Invalid, "PRODUCT_WORKFLOW_RESPONSE_INVALID")]
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
        Assert.Equal(1, stored.ServiceFaultCount);
        Assert.True(stored.NextAttemptAtUtcTicksV1 > stored.UpdatedAtUtcTicksV1);
        Assert.Equal(1, harness.Client.StartCalls);
        Assert.Equal(1, harness.Client.StartResultCalls);
        Assert.Contains("worker", Assert.Single(harness.Log.Entries).Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Submit_OutcomeUnknownAndTheLookupSaysNothingArrived_IsTheMakersToResubmit()
    {
        var harness = Harness.Create();
        harness.Client.Start = (_, _, _, _) =>
            Failed<ProductIdentityWorkflowStartResult>(Outcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT");
        harness.Client.StartResult = (_, _, _) =>
            Failed<ProductIdentityWorkflowStartResult>(Outcome.NotFound, "NOT_FOUND_NON_LEAKAGE");

        var result = await Submit(harness);

        Assert.Equal(503, result.StatusCode);
        Assert.Equal(Resubmit, result.ErrorCode);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.AwaitingMakerReplay,
            harness.Repository.Current!.Checkpoint);
        Assert.Equal(0, harness.Repository.Current.ServiceFaultCount); // the chain answered
        Assert.Equal(1, harness.Client.StartCalls);
        Assert.Contains("maker's to resubmit", Assert.Single(harness.Log.Entries).Message, StringComparison.Ordinal);
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
        Assert.Contains("TerminalEvidence", line.Message, StringComparison.Ordinal);
        Assert.Contains("worker", line.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(Outcome.Conflict, "WORKFLOW_TERMINAL_EVIDENCE_INCONSISTENT")]
    [InlineData(Outcome.Invalid, "PRODUCT_WORKFLOW_RESPONSE_INVALID")]
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
        Assert.Equal(1, stored.ServiceFaultCount);
        Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval, harness.Product.LifecycleStatus);
    }

    [Theory]
    [InlineData(Outcome.AuthenticationRejected, "PRODUCT_WORKFLOW_IDENTITY_REJECTED", Resubmit, 503, 1)]
    [InlineData(Outcome.AuthenticationRejected, "WORKFLOW_TRUSTED_CONSUMER_UNAUTHENTICATED", Resubmit, 503, 1)]
    [InlineData(Outcome.Forbidden, "WORKFLOW_TRUSTED_CONSUMER_FORBIDDEN", Resubmit, 503, 1)]
    [InlineData(Outcome.AuthenticationRejected, "WORKFLOW_DELEGATED_USER_UNAUTHENTICATED",
        ProductIdentityWorkflowMakerCodes.SessionExpired, 401, 0)]
    public async Task Withdraw_CancellationRefusedBeforeItWasProcessed_DoesNotQuarantine_AndIsTheMakersToRepeat(
        Outcome outcome, string code, string makerCode, int status, int faultCount)
    {
        var harness = Harness.CreatePending();
        harness.Client.Preflight = PreflightOk;
        harness.Client.Cancel = (_, _, _, _) => Failed<TransportCancellationEvidence>(outcome, code);

        var result = await Withdraw(harness, Guid.NewGuid());

        var stored = harness.Repository.Current!;
        Assert.Equal(status, result.StatusCode);
        Assert.Equal(makerCode, result.ErrorCode);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.AwaitingMakerReplay, stored.Checkpoint);
        Assert.Equal(makerCode, stored.LastFailureCode);
        Assert.Equal(faultCount, stored.ServiceFaultCount);
        Assert.Equal(0, harness.Products.WithdrawCount);
        Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval, harness.Product.LifecycleStatus);
    }

    [Theory]
    [InlineData(Outcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT")]
    [InlineData(Outcome.Invalid, "PRODUCT_WORKFLOW_RESPONSE_UNREADABLE")]
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
        Assert.Equal(1, harness.Repository.Current.ServiceFaultCount);
    }

    [Theory]
    [InlineData(Outcome.Forbidden, "WORKFLOW_TRUSTED_CANCEL_REQUESTER_FORBIDDEN")]
    [InlineData(Outcome.Invalid, "WORKFLOW_TRUSTED_CANCEL_INVALID")]
    [InlineData(Outcome.Invalid, "PRODUCT_WORKFLOW_RESPONSE_INVALID")]
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

    // ── who is paced by the retry schedule ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TheMaker_IsNotPacedByTheRetrySchedule_TheWorkerIs()
    {
        var submit = Harness.Create();
        submit.Client.Start = (_, _, _, _) => Task.FromResult(
            ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>.Success(StartResult()));
        await Submit(submit);

        var withdraw = Harness.CreatePending();
        withdraw.Client.Preflight = PreflightOk;
        withdraw.Client.Cancel = CancelOk;
        await Withdraw(withdraw, Guid.NewGuid());

        var worker = Harness.CreatePending();
        worker.Client.Terminal = (_, _, _) => Failed<ProductIdentityWorkflowTerminalEvidence>(
            Outcome.NonTerminal, "WORKFLOW_DECISION_NOT_TERMINAL");
        await Recover(worker);

        Assert.NotEmpty(submit.Repository.ClaimsIgnoringSchedule);
        Assert.All(submit.Repository.ClaimsIgnoringSchedule, Assert.True);
        Assert.NotEmpty(withdraw.Repository.ClaimsIgnoringSchedule);
        Assert.All(withdraw.Repository.ClaimsIgnoringSchedule, Assert.True);
        Assert.NotEmpty(worker.Repository.ClaimsIgnoringSchedule);
        Assert.All(worker.Repository.ClaimsIgnoringSchedule, Assert.False);
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
