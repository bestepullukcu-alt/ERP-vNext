using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Xunit;
using Outcome = Diten.MdmService.Application.Contracts.Workflow.ProductIdentityWorkflowTransportOutcome;
using StoreRules = Diten.MdmService.Persistence.Repositories.GlobalProductIdentityWorkflowOperationRepository;
using TransportCancellationEvidence = Diten.MdmService.Application.Contracts.Workflow.ProductIdentityWorkflowCancellationEvidence;

namespace Diten.MdmService.Application.Tests;

/*
 * BL-552 flow 3 FIX1 item 9 — the Global Product identity transition census, driven instead of listed (the LSKU FIX3
 * LskuTransitionMatrixTests pattern).
 *
 * The processor is DRIVEN from every seeded checkpoint × every engine / store answer it can meet × the maker's call,
 * the worker's and the requester's withdrawal — and then once more from wherever that left it. The fake store applies
 * the REAL store's rules (GlobalProductIdentityWorkflowOperationRepository.Accepts: the table, manual reconciliation
 * only as manual, awaiting-decision → completed only by ending) to every write and reports it.
 *
 * The assertions: (1) no write the processor makes is one the real store refuses (a silent false in production);
 * (2) the pairs the processor writes (a != b) are EXACTLY the pairs the table allows (a != b) — a pair the table allows
 * that no path writes is dead permission, a pair it lacks is a refused transition. Both are listed on failure.
 */
public sealed partial class GlobalProductIdentityWorkflowProcessorTests
{
    // Pairs the table allows that no current processor path writes and only rows written by older code can need.
    // Empty: every allowed pair is reached by driving.
    private static readonly HashSet<(GlobalProductIdentityWorkflowCheckpoint, GlobalProductIdentityWorkflowCheckpoint)>
        IdentityLegacyOnly = [];

    private static readonly Lazy<Task<IdentityCensus>> IdentityCensusRun = new(RunIdentityCensusAsync);

    [Fact]
    public async Task Identity_processor_writes_only_mutations_the_real_store_accepts()
    {
        var census = await IdentityCensusRun.Value;

        Assert.True(census.Errors.Count == 0, "Scenarios that threw:\n" + string.Join("\n", census.Errors.Take(20)));
        Assert.True(census.Refused.Count == 0, "Writes the real store refuses:\n" + string.Join("\n",
            census.Refused.Select(x => $"{x.Key.Item1} → {x.Key.Item2} ({x.Value})").Take(30)));
    }

    [Fact]
    public async Task Identity_processor_writes_exactly_the_store_transition_table()
    {
        var census = await IdentityCensusRun.Value;
        var allowed = (
            from a in Enum.GetValues<GlobalProductIdentityWorkflowCheckpoint>()
            from b in Enum.GetValues<GlobalProductIdentityWorkflowCheckpoint>()
            where a != b && StoreRules.IsAllowedTransition(a, b)
            select (a, b)).ToHashSet();
        var written = census.Written.Keys.Where(x => x.Item1 != x.Item2).ToHashSet();

        var dead = allowed.Except(written).Except(IdentityLegacyOnly).OrderBy(x => x).ToList();
        var missing = written.Except(allowed).OrderBy(x => x).ToList();

        Assert.True(census.Cases > 1000, $"The census ran only {census.Cases} scenarios.");
        Assert.True(dead.Count == 0 && missing.Count == 0,
            "Dead permissions (allowed, never written):\n" + string.Join("\n", dead.Select(x => $"  {x.Item1} → {x.Item2}"))
            + "\nWritten but not in the table:\n" + string.Join("\n", missing.Select(x => $"  {x.Item1} → {x.Item2}")));
    }

    private sealed class IdentityCensus
    {
        public int Cases { get; set; }
        public string Scenario { get; set; } = string.Empty;
        public Dictionary<(GlobalProductIdentityWorkflowCheckpoint, GlobalProductIdentityWorkflowCheckpoint), string> Written { get; } = [];
        public Dictionary<(GlobalProductIdentityWorkflowCheckpoint, GlobalProductIdentityWorkflowCheckpoint), string> Refused { get; } = [];
        public List<string> Errors { get; } = [];
    }

    private enum IdentityEntry { MakerSubmit, Worker, RequesterWithdrawal }

    private static async Task<IdentityCensus> RunIdentityCensusAsync()
    {
        var census = new IdentityCensus();
        foreach (var (seedName, seed) in IdentitySeeds())
        foreach (var (firstKnobName, firstKnob) in IdentityKnobs())
        foreach (var firstEntry in Enum.GetValues<IdentityEntry>())
        foreach (var (secondKnobName, secondKnob) in IdentitySecondKnobs())
        foreach (var secondEntry in Enum.GetValues<IdentityEntry>())
        {
            census.Cases++;
            census.Scenario = $"{seedName} | {firstKnobName}/{firstEntry} | {secondKnobName}/{secondEntry}";
            var harness = Harness.Create();
            harness.Repository.StoreRule = StoreRules.Accepts;
            harness.Repository.OnWrite = (from, mutation, accepted) =>
            {
                var pair = (from, mutation.NextCheckpoint);
                if (accepted) census.Written.TryAdd(pair, census.Scenario);
                else census.Refused.TryAdd(pair, census.Scenario);
            };
            try
            {
                seed(harness);
                firstKnob(harness);
                await DriveAsync(harness, firstEntry);
                secondKnob(harness);
                await DriveAsync(harness, secondEntry);
            }
            catch (Exception exception) when (exception is not Xunit.Sdk.XunitException)
            {
                census.Errors.Add($"{census.Scenario}: {exception.GetType().Name}: {exception.Message}");
            }
        }
        return census;
    }

    private static Task DriveAsync(Harness harness, IdentityEntry entry) => entry switch
    {
        IdentityEntry.MakerSubmit => harness.Processor.StartInteractiveAsync(TenantId, ProductId, 0, OperationId,
            MakerId, "maker-token", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(30)),
        IdentityEntry.Worker => harness.Repository.Current is { } current
            ? harness.Processor.RecoverAsync(current, "census-worker", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(30))
            : Task.FromResult<GlobalProductIdentityWorkflowProcessingResult>(null!),
        _ => harness.Processor.WithdrawInteractiveAsync(TenantId, ProductId, 1, CensusWithdrawalCommand, MakerId,
            "REQUESTER_WITHDRAWAL", null, "maker-token", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(30))
    };

    private static readonly Guid CensusWithdrawalCommand = Guid.Parse("80000000-0000-0000-0000-0000000000c1");

    // Each seed: an operation at one checkpoint with the facts that checkpoint carries, the product as it stands.
    private static IEnumerable<(string, Action<Harness>)> IdentitySeeds()
    {
        yield return ("none", _ => { });
        foreach (var old in new[] { false, true })
        {
            var age = old ? "old" : "new";
            yield return ($"Prepared/{age}", h => Seed(h, GlobalProductIdentityWorkflowCheckpoint.Prepared, old));
            yield return ($"StartOutcomeUnknown/{age}", h => Seed(h, GlobalProductIdentityWorkflowCheckpoint.StartOutcomeUnknown, old));
            yield return ($"AwaitingMakerReplay/{age}", h => Seed(h, GlobalProductIdentityWorkflowCheckpoint.AwaitingMakerReplay, old,
                disposition: ProductIdentityWorkflowRecoveryDisposition.AwaitingMakerReplay));
            yield return ($"Manual-before-start/{age}", h => Seed(h, GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired, old,
                disposition: ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired,
                code: GlobalProductIdentityWorkflowProcessor.StartProofInvalid));
        }
        yield return ("Prepared/streak-at-limit", h =>
        {
            Seed(h, GlobalProductIdentityWorkflowCheckpoint.StartOutcomeUnknown, old: true);
            h.Repository.Current!.FailedAttemptCount = 1_000;
        });
        yield return ("WorkflowStarted", h => Seed(h, GlobalProductIdentityWorkflowCheckpoint.WorkflowStarted, true, started: true));
        yield return ("LocalPendingApplied", h => Seed(h, GlobalProductIdentityWorkflowCheckpoint.LocalPendingApplied, true,
            started: true, pending: true));
        yield return ("AwaitingDecision", h => Seed(h, GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision, true,
            started: true, pending: true));
        yield return ("DecisionObserved", h => Seed(h, GlobalProductIdentityWorkflowCheckpoint.DecisionObserved, true,
            started: true, pending: true, decided: true));
        yield return ("DecisionApplied/unapplied", h => Seed(h, GlobalProductIdentityWorkflowCheckpoint.DecisionApplied, true,
            started: true, pending: true, decided: true));
        yield return ("LocalPendingApplied/draft", h => Seed(h, GlobalProductIdentityWorkflowCheckpoint.LocalPendingApplied, true,
            started: true));
        yield return ("DecisionApplied", h =>
        {
            Seed(h, GlobalProductIdentityWorkflowCheckpoint.DecisionApplied, true, started: true, pending: true, decided: true);
            h.ApplyDecisionBeforeRecovery(h.Repository.Current!);
        });
        yield return ("Manual-after-start/draft", h => Seed(h, GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired, true,
            started: true, disposition: ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired,
            code: GlobalProductIdentityWorkflowProcessor.EvidenceConflict));
        yield return ("Manual-after-start/pending", h => Seed(h, GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired, true,
            started: true, pending: true, disposition: ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired,
            code: GlobalProductIdentityWorkflowProcessor.EvidenceConflict));
        yield return ("Manual-after-start/decided", h => Seed(h, GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired, true,
            started: true, pending: true, decided: true,
            disposition: ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired,
            code: GlobalProductIdentityWorkflowProcessor.EvidenceConflict));
        yield return ("Completed", h => Seed(h, GlobalProductIdentityWorkflowCheckpoint.Completed, true, started: true));
        yield return ("AbandonedBeforeWorkflowStart", h => Seed(h, GlobalProductIdentityWorkflowCheckpoint.AbandonedBeforeWorkflowStart, true));
        yield return ("Superseded", h => Seed(h, GlobalProductIdentityWorkflowCheckpoint.Superseded, true));
    }

    private static void Seed(Harness harness, GlobalProductIdentityWorkflowCheckpoint checkpoint, bool old,
        bool started = false, bool pending = false, bool decided = false,
        ProductIdentityWorkflowRecoveryDisposition disposition = ProductIdentityWorkflowRecoveryDisposition.None,
        string? code = null)
    {
        var operation = harness.CreateOperation(checkpoint);
        operation.RecoveryDisposition = disposition;
        operation.LastFailureCode = code;
        operation.CreatedAtUtcTicksV1 = (old ? DateTimeOffset.UtcNow.AddMinutes(-10) : DateTimeOffset.UtcNow).UtcTicks;
        if (started) Harness.AddStartProof(operation);
        if (decided) Harness.AddDecisionProof(operation);
        harness.Repository.Current = operation;
        if (pending) harness.MakeProductPending(operation);
    }

    // Engine and store answers for the first call.
    private static IEnumerable<(string, Action<Harness>)> IdentityKnobs()
    {
        yield return ("happy", h => Engine(h));
        yield return ("timeouts", h => Engine(h, all: (Outcome.Timeout, "WORKFLOW_TIMEOUT")));
        yield return ("not-found", h => Engine(h, start: (Outcome.Timeout, "WORKFLOW_TIMEOUT"),
            all: (Outcome.NotFound, "WORKFLOW_NOT_FOUND")));
        yield return ("record-answer", h => Engine(h, all: (Outcome.Conflict, "WORKFLOW_START_RECOVERY_CONFLICT")));
        yield return ("incomplete", h => Engine(h, start: (Outcome.Timeout, "WORKFLOW_TIMEOUT"),
            all: (Outcome.Incomplete, "WORKFLOW_INCOMPLETE")));
        yield return ("start-forbidden", h => Engine(h, start: (Outcome.Forbidden, "WORKFLOW_DELEGATED_USER_FORBIDDEN"),
            all: (Outcome.NotFound, "WORKFLOW_NOT_FOUND")));
        yield return ("start-answer-lost", h => Engine(h, start: (Outcome.Timeout, "WORKFLOW_TIMEOUT")));
        yield return ("closed-undecided", h => Engine(h, terminal: (Outcome.Conflict,
            ProductIdentityWorkflowFaultPolicy.ClosedWithoutDecision)));
        yield return ("inconsistent", h => Engine(h, terminal: (Outcome.Conflict, "WORKFLOW_TERMINAL_EVIDENCE_INCONSISTENT")));
        yield return ("still-open", h => Engine(h, terminal: (Outcome.NonTerminal, "WORKFLOW_DECISION_NOT_TERMINAL")));
        yield return ("self-approved", h => Engine(h, decidedBy: MakerId));
        yield return ("not-a-decision", h => Engine(h, action: "Withdraw"));
        yield return ("rejected", h => Engine(h, action: "Reject"));
        yield return ("cancel-lost", h => Engine(h, cancel: (Outcome.Timeout, "WORKFLOW_TIMEOUT")));
        yield return ("cancel-refused", h => Engine(h, cancel: (Outcome.Conflict, "WORKFLOW_CANCELLATION_CONFLICT")));
        yield return ("cancel-session-expired", h => Engine(h,
            cancel: (Outcome.AuthenticationRejected, "WORKFLOW_DELEGATED_USER_UNAUTHENTICATED")));
        yield return ("preflight-session-expired", h => Engine(h,
            preflight: (Outcome.AuthenticationRejected, "WORKFLOW_DELEGATED_USER_UNAUTHENTICATED")));
        yield return ("cancel-forbidden", h => Engine(h, cancel: (Outcome.Forbidden, "WORKFLOW_DELEGATED_USER_FORBIDDEN")));
        yield return ("cancel-invalid", h => Engine(h, cancel: (Outcome.Invalid, "WORKFLOW_CANCELLATION_INVALID")));
        yield return ("withdraw-write-lost", h =>
        {
            Engine(h);
            h.Products.WithdrawWithoutWriting = true;
        });
        yield return ("preflight-closed", h => Engine(h, preflight: (Outcome.Conflict, "WORKFLOW_INSTANCE_NOT_ACTIVE")));
        foreach (var refusal in new[] { "AUDIT_INTENT_CAPACITY_EXCEEDED", "PRODUCT_IDENTITY_CONCURRENCY_CONFLICT",
                     "PRODUCT_IDENTITY_STATE_CONFLICT" })
            yield return ($"store:{refusal}", h =>
            {
                Engine(h);
                h.Products.SubmitFailure = refusal;
                h.Products.WriteFailure = refusal;
            });
    }

    // The second call meets either the same world or a healed one.
    private static IEnumerable<(string, Action<Harness>)> IdentitySecondKnobs()
    {
        yield return ("same", _ => { });
        yield return ("healed", h =>
        {
            Engine(h);
            h.Products.SubmitFailure = null;
            h.Products.WriteFailure = null;
        });
        yield return ("closed-undecided", h => Engine(h, terminal: (Outcome.Conflict,
            ProductIdentityWorkflowFaultPolicy.ClosedWithoutDecision)));
        yield return ("record-answer", h => Engine(h, all: (Outcome.Conflict, "WORKFLOW_START_RECOVERY_CONFLICT")));
        yield return ("invalid", h => Engine(h, all: (Outcome.Invalid, "WORKFLOW_REQUEST_INVALID")));
        yield return ("cancel-lost", h => Engine(h, cancel: (Outcome.Timeout, "WORKFLOW_TIMEOUT")));
        yield return ("cancel-refused", h => Engine(h, cancel: (Outcome.Conflict, "WORKFLOW_CANCELLATION_CONFLICT")));
        yield return ("preflight-lost", h => Engine(h, preflight: (Outcome.Timeout, "WORKFLOW_TIMEOUT")));
        foreach (var (engineName, engine) in new (string, Action<Harness>)[]
                 {
                     ("happy", h => Engine(h)),
                     ("timeouts", h => Engine(h, all: (Outcome.Timeout, "WORKFLOW_TIMEOUT"))),
                     ("cancel-lost", h => Engine(h, cancel: (Outcome.Timeout, "WORKFLOW_TIMEOUT"))),
                     ("cancel-refused", h => Engine(h, cancel: (Outcome.Conflict, "WORKFLOW_CANCELLATION_CONFLICT"))),
                     ("preflight-lost", h => Engine(h, preflight: (Outcome.Timeout, "WORKFLOW_TIMEOUT")))
                 })
            yield return ($"legacy-parked/{engineName}", h =>
            {
                engine(h);
                h.Products.SubmitFailure = null;
                h.Products.WriteFailure = null;
                LegacyPark(h);
            });
    }

    // A row an earlier version parked: a withdrawal waiting in AwaitingMakerReplay (written before the withdrawal
    // stopped parking there) — what this version's repeat and worker do with it.
    private static void LegacyPark(Harness h)
    {
        if (h.Repository.Current is { WithdrawalCommandId: not null } parked
            && parked.Checkpoint is GlobalProductIdentityWorkflowCheckpoint.WithdrawalRequested
                or GlobalProductIdentityWorkflowCheckpoint.WithdrawalPreflightObserved
                or GlobalProductIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown)
        {
            parked.Checkpoint = GlobalProductIdentityWorkflowCheckpoint.AwaitingMakerReplay;
            parked.RecoveryDisposition = ProductIdentityWorkflowRecoveryDisposition.AwaitingMakerReplay;
        }
    }

    private static void Engine(Harness harness,
        (Outcome Outcome, string Code)? all = null,
        (Outcome Outcome, string Code)? start = null,
        (Outcome Outcome, string Code)? terminal = null,
        (Outcome Outcome, string Code)? preflight = null,
        (Outcome Outcome, string Code)? cancel = null,
        Guid? decidedBy = null,
        string action = "Approve")
    {
        var client = harness.Client;
        var startFault = start ?? all;
        client.Start = (_, _, _, _) => startFault is { } f
            ? Failed<ProductIdentityWorkflowStartResult>(f.Outcome, f.Code)
            : Task.FromResult(ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>.Success(StartResult()));
        client.StartResult = (_, _, _) => all is { } f
            ? Failed<ProductIdentityWorkflowStartResult>(f.Outcome, f.Code)
            : Task.FromResult(ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>.Success(StartResult()));
        var terminalFault = terminal ?? all;
        client.Terminal = (_, _, _) => terminalFault is { } f
            ? Failed<ProductIdentityWorkflowTerminalEvidence>(f.Outcome, f.Code)
            : Task.FromResult(ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowTerminalEvidence>.Success(
                TerminalEvidence() with
                {
                    TerminalAction = action,
                    ActorUserId = (decidedBy ?? ApproverId).ToString("D"),
                    TaskStatus = action == "Reject" ? "Rejected" : "Approved",
                    InstanceStatus = action == "Reject" ? "Rejected" : "Completed"
                }));
        var preflightFault = preflight ?? all;
        client.Preflight = (tenantId, request, token) => preflightFault is { } f
            ? Failed<ProductIdentityWorkflowCancellationPreflight>(f.Outcome, f.Code)
            : PreflightOk(tenantId, request, token);
        var cancelFault = cancel ?? all;
        client.Cancel = (tenantId, request, delegatedToken, token) => cancelFault is { } f
            ? Failed<TransportCancellationEvidence>(f.Outcome, f.Code)
            : CancelOk(tenantId, request, delegatedToken, token);
    }
}
