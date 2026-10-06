using System.Reflection;
using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using Xunit;
using DomainCancellation = Diten.MdmService.Domain.ValueObjects.ProductIdentityWorkflowCancellationEvidence;
using EngineCancellation = Diten.MdmService.Application.Contracts.Workflow.ProductIdentityWorkflowCancellationEvidence;
using IdentityStoreRules = Diten.MdmService.Persistence.Repositories.LskuIdentityWorkflowOperationRepository;
using RetirementStoreRules = Diten.MdmService.Persistence.Repositories.LskuRetirementRequestOperationRepository;

namespace Diten.MdmService.Application.Tests;

/*
 * LSKU FIX3 item 1 (b) — the transition census, driven instead of listed.
 *
 * Each processor is DRIVEN through every starting checkpoint of its enum × every engine / provider / store answer it can
 * meet × with and without the maker's delegated token (and, for the identity flow, with and without withdrawal fields).
 * A recording fake store notes every (claimed checkpoint → next checkpoint) the processor asks it to write — including
 * the identity store's own multi-step writers (AbandonBeforeStartAsync, ApplyDecisionNotApplicableAsync,
 * ApplyWithdrawalAsync) — and applies the REAL store's rules to each write (the table, the coherence rule, never
 * "manual + retryable"): a write the store would refuse is a red test, not a silent false.
 *
 * The assertion: the recorded pairs (a != b) are EXACTLY the pairs the store's table allows (a != b). A pair the table
 * allows that no processor path writes is dead permission; a pair the processor writes that the table lacks is a refused
 * transition in production. Both are listed on failure.
 */
public sealed class LskuTransitionMatrixTests
{
    // Pairs the table allows that no current processor path can write and that only records written by older code can
    // be in. Kept minimal: every pair reachable by driving from a seeded (legacy) record is driven, not listed here.
    // Empty today — the retirement flow's AwaitingMakerReplay rows (only on records written before FIX1) are seeded as
    // legacy records and driven from there; nothing else needed a pass.
    private static readonly HashSet<(LskuIdentityWorkflowCheckpoint, LskuIdentityWorkflowCheckpoint)> IdentityLegacyOnly = [];
    private static readonly HashSet<(LskuRetirementRequestCheckpoint, LskuRetirementRequestCheckpoint)> RetirementLegacyOnly = [];

    // The matrix runs once per processor; both of its tests read the same census.
    private static readonly Lazy<Task<Census<LskuIdentityWorkflowCheckpoint>>> IdentityRun = new(RunIdentityMatrixAsync);
    private static readonly Lazy<Task<Census<LskuRetirementRequestCheckpoint>>> RetirementRun = new(RunRetirementMatrixAsync);

    // The table: recorded pairs (a != b) == the store's allowed pairs (a != b); no write to a pair the table refuses.
    [Fact]
    public async Task Identity_processor_writes_exactly_the_store_transition_table() =>
        AssertCensus(await IdentityRun.Value, IdentityStoreRules.IsAllowedTransition, IdentityLegacyOnly,
            "LSKU identity", everyStoreRule: false);

    // Every write the processor makes passes ALL the real store's rules (table + IsCoherentMutation + never
    // "manual + retryable") — a write the store would refuse is a silent false in production.
    [Fact]
    public async Task Identity_processor_writes_only_mutations_the_real_store_accepts() =>
        AssertCensus(await IdentityRun.Value, IdentityStoreRules.IsAllowedTransition, IdentityLegacyOnly,
            "LSKU identity", everyStoreRule: true);

    [Fact]
    public async Task Retirement_processor_writes_exactly_the_store_transition_table() =>
        AssertCensus(await RetirementRun.Value, RetirementStoreRules.Allowed, RetirementLegacyOnly,
            "LSKU retirement request", everyStoreRule: false);

    [Fact]
    public async Task Retirement_processor_writes_only_mutations_the_real_store_accepts() =>
        AssertCensus(await RetirementRun.Value, RetirementStoreRules.Allowed, RetirementLegacyOnly,
            "LSKU retirement request", everyStoreRule: true);

    private static async Task<Census<LskuIdentityWorkflowCheckpoint>> RunIdentityMatrixAsync()
    {
        var census = new Census<LskuIdentityWorkflowCheckpoint>();
        foreach (var (seedName, seed) in IdentitySeeds())
        foreach (var (knobName, knob) in IdentityKnobs())
        foreach (var entry in Enum.GetValues<IdentityEntry>())
        {
            census.Scenario = $"{seedName} | {knobName} | {entry}";
            census.Cases++;
            var world = new IdWorld(census);
            seed(world);
            knob(world.Answers);
            world.Op.FailedAttemptCount = world.Answers.FailedAttempts ?? world.Op.FailedAttemptCount;
            try
            {
                await RunIdentityAsync(world, entry);
            }
            catch (StoreRefusedException)
            {
                // already on the census
            }
            catch (Exception exception)
            {
                census.Errors.Add($"{census.Scenario}: {exception.GetType().Name}: {exception.Message}");
            }
        }
        return census;
    }

    private static async Task<Census<LskuRetirementRequestCheckpoint>> RunRetirementMatrixAsync()
    {
        var census = new Census<LskuRetirementRequestCheckpoint>();
        foreach (var (seedName, seed) in RetirementSeeds())
        foreach (var (knobName, knob) in RetirementKnobs())
        foreach (var entry in Enum.GetValues<RetirementEntry>())
        {
            census.Scenario = $"{seedName} | {knobName} | {entry}";
            census.Cases++;
            var world = new RetWorld(census);
            seed(world);
            knob(world.Answers);
            world.Op.FailedAttemptCount = world.Answers.FailedAttempts ?? world.Op.FailedAttemptCount;
            try
            {
                await RunRetirementAsync(world, entry);
            }
            catch (StoreRefusedException)
            {
            }
            catch (Exception exception)
            {
                census.Errors.Add($"{census.Scenario}: {exception.GetType().Name}: {exception.Message}");
            }
        }
        return census;
    }

    // ------------------------------------------------------------------------------------------------------------------
    // The census and its assertion
    // ------------------------------------------------------------------------------------------------------------------

    private sealed class StoreRefusedException(string message) : Exception(message);

    private sealed record Refusal(string Scenario, string Kind, bool TableAllows);

    private sealed class Census<T> where T : struct, Enum
    {
        public HashSet<(T From, T To)> Pairs { get; } = [];
        public Dictionary<(T From, T To), string> FirstSeen { get; } = [];
        public List<Refusal> Refusals { get; } = [];
        public List<string> Errors { get; } = [];
        public string Scenario { get; set; } = string.Empty;
        public int Cases { get; set; }

        public void Record(T from, T to)
        {
            if (Pairs.Add((from, to))) FirstSeen[(from, to)] = Scenario;
        }

        public StoreRefusedException Refuse(string kind, bool tableAllows)
        {
            Refusals.Add(new(Scenario, kind, tableAllows));
            return new StoreRefusedException($"{Scenario}: {kind}");
        }
    }

    private static void AssertCensus<T>(Census<T> census, Func<T, T, bool> table,
        HashSet<(T, T)> legacyOnly, string flow, bool everyStoreRule) where T : struct, Enum
    {
        var all = Enum.GetValues<T>();
        var allowed = all.SelectMany(a => all.Where(b => !a.Equals(b) && table(a, b)).Select(b => (a, b))).ToHashSet();
        var recorded = census.Pairs.Where(p => !p.From.Equals(p.To)).Select(p => (p.From, p.To)).ToHashSet();
        var missingFromRecorded = allowed.Except(recorded).Except(legacyOnly).OrderBy(p => p.ToString()).ToArray();
        var missingFromTable = recorded.Except(allowed).OrderBy(p => p.ToString()).ToArray();
        var legacyReached = legacyOnly.Intersect(recorded).ToArray();
        var legacyNotInTable = legacyOnly.Except(allowed).ToArray();
        var refusals = census.Refusals.Where(r => everyStoreRule || !r.TableAllows).ToArray();

        var problems = new List<string>();
        if (missingFromRecorded.Length > 0)
            problems.Add("table allows, no processor path writes (missing from recorded):\n  "
                + string.Join("\n  ", missingFromRecorded.Select(p => $"{p.Item1} -> {p.Item2}")));
        if (missingFromTable.Length > 0)
            problems.Add("processor writes, table refuses (missing from table):\n  "
                + string.Join("\n  ", missingFromTable.Select(p => $"{p.Item1} -> {p.Item2}   [first: {census.FirstSeen[p]}]")));
        if (legacyReached.Length > 0)
            problems.Add("listed as legacy-only but driven (remove from the legacy set):\n  "
                + string.Join("\n  ", legacyReached.Select(p => $"{p.Item1} -> {p.Item2}")));
        if (legacyNotInTable.Length > 0)
            problems.Add("listed as legacy-only but not in the table:\n  "
                + string.Join("\n  ", legacyNotInTable.Select(p => $"{p.Item1} -> {p.Item2}")));
        if (refusals.Length > 0)
            problems.Add($"writes the real store's rules refuse ({refusals.Length}), by kind [count, first case]:\n  "
                + string.Join("\n  ", refusals.GroupBy(r => r.Kind)
                    .Select(g => $"{g.Key}   [{g.Count()}x, first: {g.First().Scenario}]")));
        if (census.Errors.Count > 0)
            problems.Add($"scenarios that threw ({census.Errors.Count}; first 25):\n  "
                + string.Join("\n  ", census.Errors.Take(25)));

        Assert.True(problems.Count == 0,
            $"{flow}: {census.Cases} matrix cases, {recorded.Count} recorded pairs, {allowed.Count} table pairs.\n"
            + string.Join("\n", problems));
    }

    // ------------------------------------------------------------------------------------------------------------------
    // Shared engine answers
    // ------------------------------------------------------------------------------------------------------------------

    private static ProductIdentityWorkflowTransportResult<T> Fail<T>(ProductIdentityWorkflowTransportOutcome outcome,
        string code) => ProductIdentityWorkflowTransportResult<T>.Fail(outcome, code);

    // Every non-success answer the transport can give, with the codes the shared fault policy tells apart.
    private static IEnumerable<(string Name, ProductIdentityWorkflowTransportResult<T> Answer)> Faults<T>() =>
    [
        ("NotFound", Fail<T>(ProductIdentityWorkflowTransportOutcome.NotFound, "WORKFLOW_NOT_FOUND")),
        ("Incomplete", Fail<T>(ProductIdentityWorkflowTransportOutcome.Incomplete, "WORKFLOW_INCOMPLETE")),
        ("NonTerminal", Fail<T>(ProductIdentityWorkflowTransportOutcome.NonTerminal, "WORKFLOW_DECISION_NOT_TERMINAL")),
        ("Timeout", Fail<T>(ProductIdentityWorkflowTransportOutcome.Timeout, "WORKFLOW_TIMEOUT")),
        ("Retryable", Fail<T>(ProductIdentityWorkflowTransportOutcome.Retryable, "WORKFLOW_UNAVAILABLE")),
        ("Conflict", Fail<T>(ProductIdentityWorkflowTransportOutcome.Conflict, "WORKFLOW_TERMINAL_EVIDENCE_INCONSISTENT")),
        ("Conflict/closed-without-decision", Fail<T>(ProductIdentityWorkflowTransportOutcome.Conflict,
            ProductIdentityWorkflowFaultPolicy.ClosedWithoutDecision)),
        ("Conflict/template-not-published", Fail<T>(ProductIdentityWorkflowTransportOutcome.Conflict,
            "WORKFLOW_TEMPLATE_NOT_PUBLISHED")),
        ("Conflict/trusted-cancel", Fail<T>(ProductIdentityWorkflowTransportOutcome.Conflict,
            "WORKFLOW_TRUSTED_CANCEL_CONFLICT")),
        ("Invalid", Fail<T>(ProductIdentityWorkflowTransportOutcome.Invalid, "WORKFLOW_REQUEST_INVALID")),
        ("Invalid/unreadable", Fail<T>(ProductIdentityWorkflowTransportOutcome.Invalid,
            ProductIdentityWorkflowFaultPolicy.ResponseUnreadable)),
        ("Invalid/response-invalid", Fail<T>(ProductIdentityWorkflowTransportOutcome.Invalid,
            ProductIdentityWorkflowFaultPolicy.ResponseInvalid)),
        ("Invalid/candidates-required", Fail<T>(ProductIdentityWorkflowTransportOutcome.Invalid,
            "WORKFLOW_ASSIGNMENT_CANDIDATES_REQUIRED")),
        ("Forbidden/delegated-user", Fail<T>(ProductIdentityWorkflowTransportOutcome.Forbidden,
            "WORKFLOW_DELEGATED_USER_FORBIDDEN")),
        ("Forbidden/tenant-mismatch", Fail<T>(ProductIdentityWorkflowTransportOutcome.Forbidden,
            "WORKFLOW_TRUSTED_CONSUMER_TENANT_MISMATCH")),
        ("Forbidden/consumer", Fail<T>(ProductIdentityWorkflowTransportOutcome.Forbidden,
            "WORKFLOW_TRUSTED_CONSUMER_FORBIDDEN")),
        ("Forbidden/cancel-requester", Fail<T>(ProductIdentityWorkflowTransportOutcome.Forbidden,
            "WORKFLOW_TRUSTED_CANCEL_REQUESTER_FORBIDDEN")),
        ("Forbidden/other", Fail<T>(ProductIdentityWorkflowTransportOutcome.Forbidden, "WORKFLOW_FORBIDDEN")),
        ("AuthenticationRejected/delegated-user", Fail<T>(ProductIdentityWorkflowTransportOutcome.AuthenticationRejected,
            "WORKFLOW_DELEGATED_USER_UNAUTHENTICATED")),
        ("AuthenticationRejected/not-granted", Fail<T>(ProductIdentityWorkflowTransportOutcome.AuthenticationRejected,
            ProductIdentityWorkflowFaultPolicy.IdentityNotGranted)),
        ("AuthenticationRejected/other", Fail<T>(ProductIdentityWorkflowTransportOutcome.AuthenticationRejected,
            "WORKFLOW_UNAUTHENTICATED")),
        ("Success/no-value", new ProductIdentityWorkflowTransportResult<T>(
            ProductIdentityWorkflowTransportOutcome.Success, default, null))
    ];

    private static readonly (string Name, string? Code)[] LskuWriteCodes =
    [
        ("AUDIT_INTENT_CAPACITY_REACHED", "AUDIT_INTENT_CAPACITY_REACHED"),
        ("AUDIT_INTENT_CAPACITY_EXCEEDED", "AUDIT_INTENT_CAPACITY_EXCEEDED"),
        ("LSKU_CONCURRENCY_CONFLICT", "LSKU_CONCURRENCY_CONFLICT"),
        ("LSKU_IDENTITY_VERSION_CONFLICT", "LSKU_IDENTITY_VERSION_CONFLICT"),
        ("LSKU_IDENTITY_STATE_CONFLICT", "LSKU_IDENTITY_STATE_CONFLICT"),
        ("STORE_UNAVAILABLE", ProductIdentityStore.Unavailable),
        ("LEASE_BUSY", "LSKU_LIFECYCLE_BUSY"),
        ("INCONSISTENT", Inconsistent)
    ];

    private static readonly string[] StoreEndWriteCodes =
    [
        "AUDIT_INTENT_CAPACITY_REACHED", "LSKU_CONCURRENCY_CONFLICT", "LSKU_IDENTITY_STATE_CONFLICT",
        "LSKU_IDENTITY_WORKFLOW_CONCURRENCY_CONFLICT", ProductIdentityStore.Unavailable, "LSKU_IDENTITY_NOT_FOUND"
    ];

    private const string Inconsistent = "__INCONSISTENT_READBACK__";

    private static IEnumerable<(string Name, VerifiedMarketReferenceResolveResult Answer)> MarketAnswers() =>
    [
        ("200", MarketOk()),
        ("200/contract-conflict", VerifiedMarketReferenceResolveResult.Success(new(
            "market", "DE", CurrentMarketVersionId, 2, "LATEST", CurrentMarketTime))),
        ("404/not-offered", VerifiedMarketReferenceResolveResult.Fail(404, "REFERENCE_MARKET_NOT_FOUND")),
        ("404/other", VerifiedMarketReferenceResolveResult.Fail(404, "MARKET_404")),
        ("409", VerifiedMarketReferenceResolveResult.Fail(409, "REFERENCE_CONTRACT_MISMATCH")),
        ("401", VerifiedMarketReferenceResolveResult.Fail(401, "MARKET_401")),
        ("403", VerifiedMarketReferenceResolveResult.Fail(403, "MARKET_403")),
        ("503", VerifiedMarketReferenceResolveResult.Fail(503, "MARKET_503")),
        ("504", VerifiedMarketReferenceResolveResult.Fail(504, "MARKET_504"))
    ];

    private static VerifiedMarketReferenceResolveResult MarketOk() =>
        VerifiedMarketReferenceResolveResult.Success(new("market", "TR", CurrentMarketVersionId, 2, "LATEST",
            CurrentMarketTime));

    // ------------------------------------------------------------------------------------------------------------------
    // LSKU identity: entries, seeds, knobs
    // ------------------------------------------------------------------------------------------------------------------

    private enum IdentityEntry
    {
        WorkerRecover,          // no delegated token
        MakerSubmitRepeat,      // StartInteractiveAsync with the maker's token
        MakerWithdraw,          // WithdrawInteractiveAsync with the maker's token
        DraftCancel             // CloseForDraftCancelAsync
    }

    private static async Task RunIdentityAsync(IdWorld world, IdentityEntry entry)
    {
        var processor = world.Processor();
        switch (entry)
        {
            case IdentityEntry.WorkerRecover:
                await processor.RecoverAsync(world.Store.Read(), "worker-1", Lease, Retry);
                break;
            case IdentityEntry.MakerSubmitRepeat:
                await processor.StartInteractiveAsync(TenantId, LskuId, world.Op.ExpectedLskuVersion, OperationId,
                    MakerId, Token, Lease, Retry);
                break;
            case IdentityEntry.MakerWithdraw:
                await processor.WithdrawInteractiveAsync(TenantId, LskuId, 1, CommandId, MakerId, WithdrawReason,
                    WithdrawComment, Token, Lease, Retry);
                break;
            case IdentityEntry.DraftCancel:
                await processor.CloseForDraftCancelAsync(world.Store.Read(), "draft-cancel", Lease, default);
                break;
        }
    }

    private static IEnumerable<(string Name, Action<IdWorld> Seed)> IdentitySeeds()
    {
        // Before the start: the LSKU is a draft at version 0.
        yield return ("Prepared", _ => { });
        yield return ("StartOutcomeUnknown/never-sent", w => w.At(LskuIdentityWorkflowCheckpoint.StartOutcomeUnknown));
        yield return ("StartOutcomeUnknown/sent-long-ago", w =>
        {
            w.At(LskuIdentityWorkflowCheckpoint.StartOutcomeUnknown);
            w.Op.LastStartSentAtUtcTicksV1 = LongAgo.UtcTicks;
        });
        yield return ("StartOutcomeUnknown/sent-recently", w =>
        {
            w.At(LskuIdentityWorkflowCheckpoint.StartOutcomeUnknown);
            w.Op.LastStartSentAtUtcTicksV1 = Recently.UtcTicks;
        });
        yield return ("StartOutcomeUnknown/legacy-unmarked", w =>
        {
            w.At(LskuIdentityWorkflowCheckpoint.StartOutcomeUnknown);
            w.Op.LastStartSentAtUtcTicksV1 = null;
        });
        foreach (var (name, sent) in new (string, long?)[]
                 {
                     ("never-sent", 0), ("sent-long-ago", LongAgo.UtcTicks), ("sent-recently", Recently.UtcTicks),
                     ("legacy-unmarked", null)
                 })
        {
            yield return ($"AwaitingMakerReplay/submit/{name}", w =>
            {
                w.At(LskuIdentityWorkflowCheckpoint.AwaitingMakerReplay,
                    ProductIdentityWorkflowRecoveryDisposition.AwaitingMakerReplay,
                    ProductIdentityWorkflowMakerCodes.MakerReplayRequired);
                w.Op.LastStartSentAtUtcTicksV1 = sent;
            });
        }

        // Started: the start proof is on record.
        yield return ("WorkflowStarted", w => { w.StartProof(); w.At(LskuIdentityWorkflowCheckpoint.WorkflowStarted); });
        yield return ("WorkflowStarted/incomplete-proof", w =>
        {
            w.StartProof();
            w.Op.AssignmentSnapshotId = null;
            w.At(LskuIdentityWorkflowCheckpoint.WorkflowStarted);
        });
        yield return ("LocalPendingApplied", w =>
        {
            w.StartProof(); w.Pending(); w.At(LskuIdentityWorkflowCheckpoint.LocalPendingApplied);
        });
        yield return ("AwaitingDecision", w =>
        {
            w.StartProof(); w.Pending(); w.At(LskuIdentityWorkflowCheckpoint.AwaitingDecision);
        });
        yield return ("AwaitingDecision/no-instance", w =>
        {
            w.StartProof(); w.Pending(); w.Op.WorkflowInstanceId = null;
            w.At(LskuIdentityWorkflowCheckpoint.AwaitingDecision);
        });
        yield return ("AwaitingDecision/withdrawal-fields-left", w =>
        {
            w.StartProof(); w.Pending(); w.WithdrawalRequest(); w.WithdrawalPreflight();
            w.At(LskuIdentityWorkflowCheckpoint.AwaitingDecision);
        });

        foreach (var kind in new[] { ProductIdentityDecisionKind.Approved, ProductIdentityDecisionKind.Rejected })
        {
            yield return ($"DecisionObserved/{kind}", w =>
            {
                w.StartProof(); w.Pending(); w.Decision(kind); w.At(LskuIdentityWorkflowCheckpoint.DecisionObserved);
            });
            yield return ($"ApprovalValidated/{kind}", w =>
            {
                w.StartProof(); w.Pending(); w.Decision(kind);
                if (kind == ProductIdentityDecisionKind.Approved) w.ApprovalProof();
                w.At(LskuIdentityWorkflowCheckpoint.ApprovalValidated);
            });
            yield return ($"DecisionApplied/{kind}", w =>
            {
                w.StartProof(); w.Pending(); w.Decision(kind);
                if (kind == ProductIdentityDecisionKind.Approved) w.ApprovalProof();
                w.Decided();
                w.At(LskuIdentityWorkflowCheckpoint.DecisionApplied);
            });
        }
        yield return ("DecisionObserved/evidence-incomplete", w =>
        {
            w.StartProof(); w.Pending(); w.Decision(ProductIdentityDecisionKind.Approved);
            w.Op.DecisionActorSubjectId = null;
            w.At(LskuIdentityWorkflowCheckpoint.DecisionObserved);
        });
        yield return ("ApprovalValidated/approved-without-proof", w =>
        {
            w.StartProof(); w.Pending(); w.Decision(ProductIdentityDecisionKind.Approved);
            w.At(LskuIdentityWorkflowCheckpoint.ApprovalValidated);
        });
        yield return ("DecisionApplied/readback-differs", w =>
        {
            w.StartProof(); w.Pending(); w.Decision(ProductIdentityDecisionKind.Rejected);
            w.At(LskuIdentityWorkflowCheckpoint.DecisionApplied);
        });

        // Stuck: every way the requester's repeat can find it.
        yield return ("Manual/contract-mismatch", w =>
        {
            w.StartProof(); w.Pending(); w.Decision(ProductIdentityDecisionKind.Approved);
            w.Stuck("REFERENCE_CONTRACT_MISMATCH");
        });
        foreach (var (name, sent) in new (string, long?)[]
                 {
                     ("never-sent", 0), ("sent-long-ago", LongAgo.UtcTicks), ("sent-recently", Recently.UtcTicks),
                     ("legacy-unmarked", null)
                 })
        {
            yield return ($"Manual/not-started/{name}", w =>
            {
                w.Stuck("LSKU_IDENTITY_WORKFLOW_START_PROOF_INVALID");
                w.Op.LastStartSentAtUtcTicksV1 = sent;
            });
        }
        yield return ("Manual/started/lsku-pending-ours", w =>
        {
            w.StartProof(); w.Pending(); w.Stuck("LSKU_IDENTITY_WORKFLOW_EVIDENCE_CONFLICT");
        });
        yield return ("Manual/started/lsku-pending-ours/decision-recorded", w =>
        {
            w.StartProof(); w.Pending(); w.Decision(ProductIdentityDecisionKind.Rejected);
            w.Stuck("LSKU_IDENTITY_WORKFLOW_DECISION_INCONSISTENT");
        });
        yield return ("Manual/started/lsku-draft-not-ours", w =>
        {
            w.StartProof(); w.Stuck("LSKU_IDENTITY_WORKFLOW_LOCAL_PENDING_INCONSISTENT");
        });
        foreach (var kind in new[] { ProductIdentityDecisionKind.Approved, ProductIdentityDecisionKind.Rejected })
        {
            yield return ($"Manual/started/lsku-decided-ours/{kind}", w =>
            {
                w.StartProof(); w.Pending(); w.Decision(kind);
                if (kind == ProductIdentityDecisionKind.Approved) w.ApprovalProof();
                w.Decided();
                w.Stuck("LSKU_IDENTITY_WORKFLOW_DECISION_INCONSISTENT");
            });
        }
        yield return ("Manual/started/lsku-elsewhere", w =>
        {
            w.StartProof(); w.Pending(); w.Lsku.LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved;
            w.Lsku.Version = 2; w.Stuck("LSKU_IDENTITY_WORKFLOW_DECISION_INCONSISTENT");
        });
        yield return ("Manual/disposition-none", w =>
        {
            w.StartProof(); w.Pending();
            w.At(LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired,
                ProductIdentityWorkflowRecoveryDisposition.None, "LSKU_IDENTITY_WORKFLOW_RECONCILIATION_REQUIRED");
        });

        // Withdrawal: the LSKU is pending (version 1) under this operation's approval.
        yield return ("WithdrawalRequested", w =>
        {
            w.StartProof(); w.Pending(); w.WithdrawalRequest();
            w.At(LskuIdentityWorkflowCheckpoint.WithdrawalRequested);
        });
        yield return ("WithdrawalPreflightObserved", w =>
        {
            w.StartProof(); w.Pending(); w.WithdrawalRequest(); w.WithdrawalPreflight();
            w.At(LskuIdentityWorkflowCheckpoint.WithdrawalPreflightObserved);
        });
        yield return ("WithdrawalOutcomeUnknown", w =>
        {
            w.StartProof(); w.Pending(); w.WithdrawalRequest(); w.WithdrawalPreflight();
            w.Op.WithdrawalUnansweredSinceUtcTicksV1 = LongAgo.UtcTicks;
            w.At(LskuIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown,
                ProductIdentityWorkflowRecoveryDisposition.Retryable, "WORKFLOW_TIMEOUT");
        });
        yield return ("AwaitingMakerReplay/withdrawal", w =>
        {
            w.StartProof(); w.Pending(); w.WithdrawalRequest(); w.WithdrawalPreflight();
            w.Op.WithdrawalUnansweredSinceUtcTicksV1 = LongAgo.UtcTicks;
            w.At(LskuIdentityWorkflowCheckpoint.AwaitingMakerReplay,
                ProductIdentityWorkflowRecoveryDisposition.AwaitingMakerReplay,
                "LSKU_IDENTITY_WORKFLOW_MAKER_REPLAY_REQUIRED");
        });
        yield return ("WithdrawalObserved", w =>
        {
            w.StartProof(); w.Pending(); w.WithdrawalRequest(); w.WithdrawalPreflight(); w.WithdrawalEvidence();
            w.At(LskuIdentityWorkflowCheckpoint.WithdrawalObserved);
        });
        yield return ("WithdrawalObserved/evidence-incomplete", w =>
        {
            w.StartProof(); w.Pending(); w.WithdrawalRequest(); w.WithdrawalPreflight(); w.WithdrawalEvidence();
            w.Op.WithdrawalTransitionLogId = null;
            w.At(LskuIdentityWorkflowCheckpoint.WithdrawalObserved);
        });
        yield return ("WithdrawalApplied", w =>
        {
            w.StartProof(); w.Pending(); w.WithdrawalRequest(); w.WithdrawalPreflight(); w.WithdrawalEvidence();
            w.Lsku.LifecycleStatus = ProductIdentityLifecycleStatus.Draft; w.Lsku.Version = 2;
            w.At(LskuIdentityWorkflowCheckpoint.WithdrawalApplied);
        });

        // Terminal: the processor never claims them (they must stay silent).
        foreach (var terminal in new[]
                 {
                     LskuIdentityWorkflowCheckpoint.Completed, LskuIdentityWorkflowCheckpoint.AbandonedBeforeWorkflowStart,
                     LskuIdentityWorkflowCheckpoint.Superseded, LskuIdentityWorkflowCheckpoint.DecisionNotApplicable
                 })
        {
            yield return ($"{terminal}", w => { w.StartProof(); w.Pending(); w.At(terminal); });
        }
    }

    private static IEnumerable<(string Name, Action<IdAnswers> Knob)> IdentityKnobs()
    {
        yield return ("baseline", _ => { });
        yield return ("start=Success/invalid-proof", a => a.Start =
            ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>.Success(
                IdStartResult() with { ObjectRef = "OTHER" }));
        foreach (var (name, answer) in Faults<ProductIdentityWorkflowStartResult>())
            yield return ($"start={name}", a => a.Start = answer);
        yield return ("lookup=Success/invalid-proof", a => a.Lookup =
            ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>.Success(
                IdStartResult() with { ApprovalTaskId = Guid.Empty }));
        foreach (var (name, answer) in Faults<ProductIdentityWorkflowStartResult>())
            yield return ($"lookup={name}", a => a.Lookup = answer);
        yield return ("start+lookup=Timeout", a =>
        {
            a.Start = Fail<ProductIdentityWorkflowStartResult>(ProductIdentityWorkflowTransportOutcome.Timeout, "T");
            a.Lookup = Fail<ProductIdentityWorkflowStartResult>(ProductIdentityWorkflowTransportOutcome.Timeout, "T");
        });
        yield return ("start=Timeout,lookup=NotFound", a =>
        {
            a.Start = Fail<ProductIdentityWorkflowStartResult>(ProductIdentityWorkflowTransportOutcome.Timeout, "T");
            a.Lookup = Fail<ProductIdentityWorkflowStartResult>(ProductIdentityWorkflowTransportOutcome.NotFound, "N");
        });

        foreach (var (name, evidence) in IdTerminalVariants())
            yield return ($"terminal={name}", a => a.Terminal =
                ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowTerminalEvidence>.Success(evidence));
        foreach (var (name, answer) in Faults<ProductIdentityWorkflowTerminalEvidence>())
            yield return ($"terminal={name}", a => a.Terminal = answer);

        foreach (var (name, answer) in Faults<ProductIdentityWorkflowCancellationPreflight>())
            yield return ($"preflight={name}", a => a.Preflight = answer);
        foreach (var (name, answer) in Faults<EngineCancellation>())
            yield return ($"cancel={name}", a => a.Cancel = answer);

        foreach (var (name, answer) in MarketAnswers())
        {
            yield return ($"market={name}", a => a.Market = [answer]);
            yield return ($"market=200,then {name}", a => a.Market = [MarketOk(), answer]);
        }
        yield return ("gsku=not-approved", a => a.GskuApproved = false);
        yield return ("lsku=missing", a => a.LskuMissing = true);
        yield return ("fence=refused", a => a.FenceSucceeds = false);
        foreach (var (name, code) in LskuWriteCodes)
        {
            yield return ($"submit-write={name}", a => a.SubmitWrite = code);
            yield return ($"decision-write={name}", a => a.DecisionWrite = code);
        }
        foreach (var code in StoreEndWriteCodes)
        {
            yield return ($"abandon-write={code}", a => a.AbandonWrite = code);
            yield return ($"not-applicable-write={code}", a => a.NotApplicableWrite = code);
            yield return ($"withdrawal-write={code}", a => a.WithdrawalWrite = code);
        }
        yield return ("streak=at-limit", a => a.FailedAttempts = ProductIdentityRecoveryStreak.DefaultQuarantineAfterFailedAttempts - 1);
        yield return ("streak=at-limit,lsku-write=LSKU_CONCURRENCY_CONFLICT", a =>
        {
            a.FailedAttempts = ProductIdentityRecoveryStreak.DefaultQuarantineAfterFailedAttempts - 1;
            a.SubmitWrite = "LSKU_CONCURRENCY_CONFLICT"; a.DecisionWrite = "LSKU_CONCURRENCY_CONFLICT";
        });
        yield return ("streak=at-limit,terminal=Timeout", a =>
        {
            a.FailedAttempts = ProductIdentityRecoveryStreak.DefaultQuarantineAfterFailedAttempts - 1;
            a.Terminal = Fail<ProductIdentityWorkflowTerminalEvidence>(ProductIdentityWorkflowTransportOutcome.Timeout, "T");
        });
    }

    private static IEnumerable<(string Name, ProductIdentityWorkflowTerminalEvidence Evidence)> IdTerminalVariants()
    {
        var approve = IdTerminal(ProductIdentityDecisionKind.Approved);
        yield return ("Approve", approve);
        yield return ("Reject", IdTerminal(ProductIdentityDecisionKind.Rejected));
        yield return ("self-approval", approve with { ActorUserId = MakerId.ToString("D") });
        yield return ("wrong-task", approve with { ApprovalTaskId = Guid.NewGuid() });
        yield return ("unknown-action", approve with { TerminalAction = "Escalate" });
        yield return ("reject-without-reason", IdTerminal(ProductIdentityDecisionKind.Rejected) with { ReasonCode = null });
        yield return ("actor-unreadable", approve with { ActorUserId = "not-a-guid" });
    }

    // ------------------------------------------------------------------------------------------------------------------
    // LSKU identity: the world (operation, LSKU, recording store, engine, provider)
    // ------------------------------------------------------------------------------------------------------------------

    private sealed class IdAnswers
    {
        public ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult> Start { get; set; } =
            ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>.Success(IdStartResult());
        public ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult> Lookup { get; set; } =
            ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>.Success(IdStartResult());
        public ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowTerminalEvidence> Terminal { get; set; } =
            ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowTerminalEvidence>.Success(
                IdTerminal(ProductIdentityDecisionKind.Approved));
        public ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowCancellationPreflight> Preflight { get; set; } =
            ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowCancellationPreflight>.Success(new(
                WorkflowId, TaskId, "lsku", LskuId.ToString("D"), "LS-0001", 3, 4, "Running", "Pending"));
        public ProductIdentityWorkflowTransportResult<EngineCancellation> Cancel { get; set; } =
            ProductIdentityWorkflowTransportResult<EngineCancellation>.Success(new(
                WorkflowId, TaskId, TemplateId, TemplateVersionId, "lsku", LskuId.ToString("D"), "LS-0001",
                "Cancel", MakerId, WithdrawReason, WithdrawComment, CancelAt, 5, CancelLogId, "Cancelled",
                "Cancelled", 4, 5, false, null));
        public List<VerifiedMarketReferenceResolveResult> Market { get; set; } = [MarketOk()];
        public bool GskuApproved { get; set; } = true;
        public bool LskuMissing { get; set; }
        public bool FenceSucceeds { get; set; } = true;
        public string? SubmitWrite { get; set; }
        public string? DecisionWrite { get; set; }
        public string? AbandonWrite { get; set; }
        public string? NotApplicableWrite { get; set; }
        public string? WithdrawalWrite { get; set; }
        public int? FailedAttempts { get; set; }
    }

    private sealed class IdWorld
    {
        public IdWorld(Census<LskuIdentityWorkflowCheckpoint> census)
        {
            Op = new LskuIdentityWorkflowOperation
            {
                Id = OperationId, TenantId = TenantId, OperationId = OperationId, LskuId = LskuId, GskuId = GskuId,
                ProductDefinitionRevisionId = RevisionId, ExpectedLskuVersion = 0, MakerSubjectId = MakerId,
                WorkflowTemplateId = TemplateId, CandidatePrincipalIds = [ApproverId], ReasonCode = "LSKU_APPROVAL",
                CommentRequired = true, EvidenceRequired = true, ObjectType = "lsku",
                ObjectId = LskuId.ToString("D"), ObjectRef = "LS-0001", MarketCode = "TR",
                MarketSelection = Selection(CreateMarketVersionId, 1, CreateMarketTime),
                StartIdempotencyKey = $"lsku-identity:{TenantId:D}:{OperationId:D}",
                OperationFingerprint = new string('a', 64),
                Checkpoint = LskuIdentityWorkflowCheckpoint.Prepared,
                RecoveryDisposition = ProductIdentityWorkflowRecoveryDisposition.None,
                TemporalStorageVersion = LskuIdentityWorkflowOperation.CurrentTemporalStorageVersion,
                LastStartSentAtUtcTicksV1 = 0,
                CreatedAtUtcTicksV1 = LongAgo.UtcTicks, UpdatedAtUtcTicksV1 = LongAgo.UtcTicks
            };
            Lsku = new Lsku
            {
                Id = LskuId, TenantId = TenantId, GskuId = GskuId, CanonicalCode = "LS-0001",
                CreationCommandId = "create-lsku", MarketCode = "TR",
                MarketSelection = Selection(CreateMarketVersionId, 1, CreateMarketTime),
                LifecycleStatus = ProductIdentityLifecycleStatus.Draft, Version = 0
            };
            Store = new IdStore(this, census);
        }

        public LskuIdentityWorkflowOperation Op { get; }
        public Lsku Lsku { get; }
        public IdAnswers Answers { get; } = new();
        public IdStore Store { get; }
        public int MarketCalls { get; set; }

        public LskuIdentityWorkflowProcessor Processor()
        {
            var gskus = DispatchProxy.Create<IGskuRepository, MatrixProxy>();
            ((MatrixProxy)(object)gskus).Handler = (method, args) => method.Name == "GetByIdAsync"
                ? Task.FromResult<Gsku?>((Guid)args![0]! == GskuId
                    ? new Gsku
                    {
                        Id = GskuId, TenantId = TenantId, ProductDefinitionRevisionId = RevisionId,
                        LifecycleStatus = Answers.GskuApproved
                            ? ProductIdentityLifecycleStatus.IdentityApproved
                            : ProductIdentityLifecycleStatus.PendingIdentityApproval
                    }
                    : null)
                : throw new NotSupportedException(method.Name);
            var revisions = DispatchProxy.Create<IProductDefinitionRevisionRepository, MatrixProxy>();
            ((MatrixProxy)(object)revisions).Handler = (method, args) => method.Name == "GetByIdAsync"
                ? Task.FromResult<ProductDefinitionRevision?>((Guid)args![0]! == RevisionId
                    ? new ProductDefinitionRevision
                    {
                        Id = RevisionId, TenantId = TenantId,
                        LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved
                    }
                    : null)
                : throw new NotSupportedException(method.Name);
            return new LskuIdentityWorkflowProcessor(Store, new IdLskus(this), gskus, revisions, new IdMarket(this),
                new IdEngine(this),
                new LskuIdentityWorkflowStartRequestFactory(
                    new(TemplateId, null, [ApproverId], "LSKU_APPROVAL", true, true, null), Clock),
                Clock);
        }

        public void At(LskuIdentityWorkflowCheckpoint checkpoint,
            ProductIdentityWorkflowRecoveryDisposition disposition = ProductIdentityWorkflowRecoveryDisposition.None,
            string? code = null)
        {
            Op.Checkpoint = checkpoint;
            Op.RecoveryDisposition = disposition;
            Op.LastFailureCode = code;
        }

        public void Stuck(string code) => At(LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired,
            ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired, code);

        public void StartProof()
        {
            Op.WorkflowInstanceId = WorkflowId; Op.WorkflowTemplateVersionId = TemplateVersionId;
            Op.ApprovalTaskId = TaskId; Op.AssignmentSnapshotId = SnapshotId; Op.StartTransitionLogId = StartLogId;
            Op.WorkflowStartedAtUtcTicksV1 = StartedAt.UtcTicks; Op.LastStartSentAtUtcTicksV1 = LongAgo.UtcTicks;
        }

        public void Pending()
        {
            Lsku.Version = 1;
            Lsku.LifecycleStatus = ProductIdentityLifecycleStatus.PendingIdentityApproval;
            Lsku.IdentityWorkflowBinding = new ProductIdentityWorkflowBinding
            {
                WorkflowInstanceId = WorkflowId, WorkflowTemplateId = TemplateId,
                WorkflowTemplateVersionId = TemplateVersionId, ApprovalTaskId = TaskId,
                AssignmentSnapshotId = SnapshotId, StartTransitionLogId = StartLogId, ObjectType = "lsku",
                ObjectId = LskuId, ObjectRef = "LS-0001", SubmitterSubjectId = MakerId,
                StartIdempotencyKey = Op.StartIdempotencyKey, StartRequestFingerprint = Op.OperationFingerprint,
                SubmittedAtUtc = StartedAt
            };
        }

        public void Decision(ProductIdentityDecisionKind kind)
        {
            var approved = kind == ProductIdentityDecisionKind.Approved;
            Op.DecisionTransitionLogId = DecisionLogId; Op.DecisionKind = kind;
            Op.DecisionObservedAtUtcTicksV1 = DecisionAt.UtcTicks; Op.DecisionActorSubjectId = ApproverId;
            Op.DecisionReasonCode = approved ? null : "REJECTED"; Op.DecisionObjectType = "lsku";
            Op.DecisionObjectId = LskuId.ToString("D"); Op.DecisionObjectRef = "LS-0001";
            Op.DecisionWorkflowTemplateId = TemplateId; Op.DecisionWorkflowTemplateVersionId = TemplateVersionId;
            Op.DecisionTaskStatus = approved ? "Approved" : "Rejected";
            Op.DecisionInstanceStatus = approved ? "Completed" : "Rejected";
            Op.DecisionTransitionSequence = 2; Op.DecisionAtUtcTicksV1 = DecisionAt.UtcTicks;
        }

        public void ApprovalProof()
        {
            var selection = Selection(CurrentMarketVersionId, 2, CurrentMarketTime);
            var validatedAt = Recently.UtcTicks;
            Op.ApprovalMarketSelection = selection;
            Op.MarketValidatedAtUtcTicksV1 = validatedAt;
            Op.ApprovalMarketProofFingerprint = (string)PrivateStatic("ApprovalProofFingerprint",
                TenantId, OperationId, LskuId, "TR", Op.DecisionTransitionSequence, selection, validatedAt);
        }

        // The decision as the LSKU store would hold it after ReconcileIdentityDecisionAsync.
        public void Decided()
        {
            var evidence = DecisionEvidence(Op);
            Lsku.Version = 2;
            Lsku.LifecycleStatus = evidence.Decision == ProductIdentityDecisionKind.Approved
                ? ProductIdentityLifecycleStatus.IdentityApproved
                : ProductIdentityLifecycleStatus.Draft;
            Lsku.IdentityWorkflowBinding!.TerminalDecision = evidence;
            Lsku.AuditIntents.Add(LskuIdentityLifecycleAuditIntentFactory.CreateDecision(Lsku, 1, evidence));
        }

        public void WithdrawalRequest()
        {
            Op.WithdrawalCommandId = CommandId; Op.WithdrawalRequesterSubjectId = MakerId;
            Op.WithdrawalExpectedLskuVersion = 1; Op.WithdrawalReasonCode = WithdrawReason;
            Op.WithdrawalComment = WithdrawComment;
            Op.WithdrawalFingerprint = (string)PrivateStatic("WithdrawalFingerprint", TenantId, LskuId, 1, CommandId,
                MakerId, WithdrawReason, WithdrawComment, Lsku.IdentityWorkflowBinding!);
        }

        public void WithdrawalPreflight()
        {
            Op.WithdrawalExpectedWorkflowInstanceVersion = 3; Op.WithdrawalExpectedApprovalTaskVersion = 4;
            Op.WithdrawalObjectRef = "LS-0001";
        }

        public void WithdrawalEvidence()
        {
            Op.WithdrawalTransitionLogId = CancelLogId; Op.WithdrawalObservedAtUtcTicksV1 = CancelAt.UtcTicks;
            Op.WithdrawalTransitionSequence = 5; Op.WithdrawalResultWorkflowInstanceVersion = 4;
            Op.WithdrawalResultApprovalTaskVersion = 5; Op.WithdrawalTaskStatus = "Cancelled";
            Op.WithdrawalInstanceStatus = "Cancelled";
        }
    }

    private static object PrivateStatic(string name, params object?[] args) =>
        typeof(LskuIdentityWorkflowProcessor).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, args)!;

    private static ProductIdentityWorkflowDecisionEvidence DecisionEvidence(LskuIdentityWorkflowOperation op) => new()
    {
        Decision = op.DecisionKind!.Value, WorkflowInstanceId = op.WorkflowInstanceId!.Value,
        ApprovalTaskId = op.ApprovalTaskId!.Value, WorkflowTemplateId = op.DecisionWorkflowTemplateId!.Value,
        WorkflowTemplateVersionId = op.DecisionWorkflowTemplateVersionId!.Value,
        ObjectType = op.DecisionObjectType ?? string.Empty, ObjectId = Guid.Parse(op.DecisionObjectId!),
        ObjectRef = op.DecisionObjectRef ?? string.Empty, DecisionActorSubjectId = op.DecisionActorSubjectId!.Value,
        ReasonCode = op.DecisionReasonCode,
        DecisionAtUtc = new DateTimeOffset(op.DecisionAtUtcTicksV1!.Value, TimeSpan.Zero),
        TransitionSequence = op.DecisionTransitionSequence!.Value, TaskStatus = op.DecisionTaskStatus ?? string.Empty,
        InstanceStatus = op.DecisionInstanceStatus ?? string.Empty
    };

    // The identity store: records every pair the processor asks for and applies the real store's rules to it.
    private sealed class IdStore(IdWorld world, Census<LskuIdentityWorkflowCheckpoint> census)
        : ILskuIdentityWorkflowOperationRepository
    {
        private LskuIdentityWorkflowOperation Stored => world.Op;

        // Every read is a copy (as from the database): the processor's in-hand document goes stale like the real one.
        public LskuIdentityWorkflowOperation Read() => Copy(Stored);

        public Task<LskuIdentityWorkflowReserveResult> ReserveAsync(LskuIdentityWorkflowOperation operation,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<LskuIdentityWorkflowOperation?> GetByOperationIdAsync(Guid operationId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Stored.OperationId == operationId ? Read() : null);

        public Task<LskuIdentityWorkflowOperation?> GetByStartIdempotencyKeyAsync(string key,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Stored.StartIdempotencyKey == key ? Read() : null);

        public Task<LskuIdentityWorkflowClaim?> TryClaimAsync(LskuIdentityWorkflowClaimRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request.OperationId != Stored.OperationId || request.OperationFingerprint != Stored.OperationFingerprint
                || !request.EligibleCheckpoints.Contains(Stored.Checkpoint)
                || Stored.NextAttemptAtUtcTicksV1 > request.NowUtcTicks
                || Stored.LeaseOwner is not null && Stored.LeaseUntilUtcTicksV1 > request.NowUtcTicks)
                return Task.FromResult<LskuIdentityWorkflowClaim?>(null);
            Stored.LeaseOwner = request.LeaseOwner;
            Stored.LeaseUntilUtcTicksV1 = request.LeaseUntilUtcTicks;
            Stored.UpdatedAtUtcTicksV1 = request.NowUtcTicks; // the real claim stamps the document
            Stored.LeaseGeneration++;
            return Task.FromResult<LskuIdentityWorkflowClaim?>(new(Stored.TenantId, Stored.OperationId, Stored.LskuId,
                Stored.MarketCode, Stored.DecisionTransitionLogId, Stored.DecisionTransitionSequence,
                Stored.OperationFingerprint, request.LeaseOwner, Stored.LeaseGeneration, Stored.Checkpoint,
                request.LeaseUntilUtcTicks));
        }

        public Task<LskuIdentityWorkflowRecoverablePage> DiscoverRecoverableAsync(long nowUtcTicks, int limit,
            LskuIdentityWorkflowRecoveryCursor? after = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> AdvanceAsync(LskuIdentityWorkflowClaim claim, LskuIdentityWorkflowCheckpointMutation m,
            CancellationToken cancellationToken = default)
        {
            census.Record(claim.Checkpoint, m.NextCheckpoint);
            if (m.UpdatedAtUtcTicks <= 0
                || !IdentityStoreRules.IsAllowedTransition(claim.Checkpoint, m.NextCheckpoint)
                || !IdentityStoreRules.IsCoherentMutation(claim, m)
                || m.NextCheckpoint == LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired
                   && m.RecoveryDisposition == ProductIdentityWorkflowRecoveryDisposition.Retryable)
                throw census.Refuse($"store refuses {claim.Checkpoint} -> {m.NextCheckpoint} "
                    + $"({m.RecoveryDisposition}, {m.LastFailureCode}); allowed="
                    + $"{IdentityStoreRules.IsAllowedTransition(claim.Checkpoint, m.NextCheckpoint)}, coherent="
                    + $"{IdentityStoreRules.IsCoherentMutation(claim, m)}",
                    IdentityStoreRules.IsAllowedTransition(claim.Checkpoint, m.NextCheckpoint));
            if (!Owns(claim, m.UpdatedAtUtcTicks)) return Task.FromResult(false);
            Apply(Stored, m);
            return Task.FromResult(true);
        }

        public Task<LskuIdentityWithdrawalWriteResult> ApplyWithdrawalAsync(LskuIdentityWorkflowClaim claim,
            LskuIdentityWorkflowOperation operation, DomainCancellation cancellationEvidence, LocalAuditIntent auditIntent,
            long updatedAtUtcTicks, CancellationToken cancellationToken = default) =>
            EndWrite(claim, updatedAtUtcTicks, LskuIdentityWorkflowCheckpoint.WithdrawalApplied,
                [LskuIdentityWorkflowCheckpoint.WithdrawalObserved], world.Answers.WithdrawalWrite, null, () =>
                {
                    world.Lsku.LifecycleStatus = ProductIdentityLifecycleStatus.Draft;
                    world.Lsku.IdentityWorkflowBinding!.CancellationEvidence = cancellationEvidence;
                    world.Lsku.Version++;
                    world.Lsku.AuditIntents.Add(auditIntent);
                });

        public Task<LskuIdentityWithdrawalWriteResult> AbandonBeforeStartAsync(LskuIdentityWorkflowClaim claim,
            LskuIdentityWorkflowOperation operation, string reasonCode, LocalAuditIntent auditIntent,
            long updatedAtUtcTicks, CancellationToken cancellationToken = default) =>
            // FIX3 item 4 (the real store's filter): the end is written only on the start-sent mark it was decided on.
            EndWrite(claim, updatedAtUtcTicks, LskuIdentityWorkflowCheckpoint.AbandonedBeforeWorkflowStart,
                [
                    LskuIdentityWorkflowCheckpoint.StartOutcomeUnknown, LskuIdentityWorkflowCheckpoint.AwaitingMakerReplay,
                    LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired
                ],
                Stored.LastStartSentAtUtcTicksV1 != operation.LastStartSentAtUtcTicksV1
                    ? "LSKU_IDENTITY_WORKFLOW_CONCURRENCY_CONFLICT"
                    : world.Answers.AbandonWrite,
                reasonCode, () =>
                {
                    world.Lsku.Version++;
                    world.Lsku.ActiveLifecycleOperation = null;
                    world.Lsku.AuditIntents.Add(auditIntent);
                });

        public Task<LskuIdentityWithdrawalWriteResult> ApplyDecisionNotApplicableAsync(LskuIdentityWorkflowClaim claim,
            LskuIdentityWorkflowOperation operation, string reasonCode, LocalAuditIntent auditIntent,
            long updatedAtUtcTicks, CancellationToken cancellationToken = default) =>
            EndWrite(claim, updatedAtUtcTicks, LskuIdentityWorkflowCheckpoint.DecisionNotApplicable,
                [
                    LskuIdentityWorkflowCheckpoint.AwaitingDecision, LskuIdentityWorkflowCheckpoint.DecisionObserved,
                    LskuIdentityWorkflowCheckpoint.ApprovalValidated
                ],
                world.Answers.NotApplicableWrite, reasonCode, () =>
                {
                    world.Lsku.LifecycleStatus = ProductIdentityLifecycleStatus.Draft;
                    world.Lsku.Version++;
                    world.Lsku.AuditIntents.Add(auditIntent);
                });

        // The store's own multi-step writers: the pair (claimed checkpoint → its end) is recorded and held to the
        // table and to the writer's own from-list, then the configured outcome is answered.
        private Task<LskuIdentityWithdrawalWriteResult> EndWrite(LskuIdentityWorkflowClaim claim, long updatedAt,
            LskuIdentityWorkflowCheckpoint end, LskuIdentityWorkflowCheckpoint[] from, string? failure,
            string? reasonCode, Action lskuWrite)
        {
            census.Record(claim.Checkpoint, end);
            if (!IdentityStoreRules.IsAllowedTransition(claim.Checkpoint, end) || !from.Contains(claim.Checkpoint))
                throw census.Refuse($"store refuses {claim.Checkpoint} -> {end} (allowed="
                    + $"{IdentityStoreRules.IsAllowedTransition(claim.Checkpoint, end)}, writer accepts "
                    + $"{string.Join('/', from)})", IdentityStoreRules.IsAllowedTransition(claim.Checkpoint, end));
            if (failure is not null)
                return Task.FromResult(new LskuIdentityWithdrawalWriteResult(false, false, world.Lsku, failure));
            if (!Owns(claim, updatedAt))
                return Task.FromResult(new LskuIdentityWithdrawalWriteResult(false, false, world.Lsku,
                    "LSKU_IDENTITY_WORKFLOW_CONCURRENCY_CONFLICT"));
            lskuWrite();
            Stored.Checkpoint = end;
            Stored.RecoveryDisposition = ProductIdentityWorkflowRecoveryDisposition.None;
            Stored.NextAttemptAtUtcTicksV1 = null;
            Stored.LastFailureCode = reasonCode;
            Stored.FailedAttemptCount = 0;
            Stored.LeaseOwner = null;
            Stored.LeaseUntilUtcTicksV1 = null;
            Stored.UpdatedAtUtcTicksV1 = updatedAt;
            return Task.FromResult(new LskuIdentityWithdrawalWriteResult(true, false, world.Lsku, null));
        }

        private bool Owns(LskuIdentityWorkflowClaim claim, long updatedAt) =>
            Stored.Checkpoint == claim.Checkpoint && Stored.LeaseOwner == claim.LeaseOwner
            && Stored.LeaseGeneration == claim.LeaseGeneration && Stored.LeaseUntilUtcTicksV1 > updatedAt;

        // What LskuIdentityWorkflowOperationRepository.AdvanceAsync's update sets.
        private static void Apply(LskuIdentityWorkflowOperation o, LskuIdentityWorkflowCheckpointMutation m)
        {
            o.Checkpoint = m.NextCheckpoint; o.RecoveryDisposition = m.RecoveryDisposition;
            o.NextAttemptAtUtcTicksV1 = m.NextAttemptAtUtcTicksV1; o.LastFailureCode = m.LastFailureCode;
            o.UpdatedAtUtcTicksV1 = m.UpdatedAtUtcTicks;
            if (m.WorkflowInstanceId.HasValue) o.WorkflowInstanceId = m.WorkflowInstanceId;
            if (m.WorkflowTemplateId.HasValue) o.WorkflowTemplateId = m.WorkflowTemplateId;
            if (m.WorkflowTemplateVersionId.HasValue) o.WorkflowTemplateVersionId = m.WorkflowTemplateVersionId;
            if (m.ApprovalTaskId.HasValue) o.ApprovalTaskId = m.ApprovalTaskId;
            if (m.AssignmentSnapshotId.HasValue) o.AssignmentSnapshotId = m.AssignmentSnapshotId;
            if (m.StartTransitionLogId.HasValue) o.StartTransitionLogId = m.StartTransitionLogId;
            if (m.WorkflowStartedAtUtcTicksV1.HasValue) o.WorkflowStartedAtUtcTicksV1 = m.WorkflowStartedAtUtcTicksV1;
            if (m.DecisionTransitionLogId.HasValue) o.DecisionTransitionLogId = m.DecisionTransitionLogId;
            if (m.DecisionKind.HasValue) o.DecisionKind = m.DecisionKind;
            if (m.DecisionObservedAtUtcTicksV1.HasValue) o.DecisionObservedAtUtcTicksV1 = m.DecisionObservedAtUtcTicksV1;
            if (m.DecisionActorSubjectId.HasValue) o.DecisionActorSubjectId = m.DecisionActorSubjectId;
            if (m.DecisionReasonCode is not null) o.DecisionReasonCode = m.DecisionReasonCode;
            if (m.DecisionObjectType is not null) o.DecisionObjectType = m.DecisionObjectType;
            if (m.DecisionObjectId is not null) o.DecisionObjectId = m.DecisionObjectId;
            if (m.DecisionObjectRef is not null) o.DecisionObjectRef = m.DecisionObjectRef;
            if (m.DecisionWorkflowTemplateId.HasValue) o.DecisionWorkflowTemplateId = m.DecisionWorkflowTemplateId;
            if (m.DecisionWorkflowTemplateVersionId.HasValue)
                o.DecisionWorkflowTemplateVersionId = m.DecisionWorkflowTemplateVersionId;
            if (m.DecisionTaskStatus is not null) o.DecisionTaskStatus = m.DecisionTaskStatus;
            if (m.DecisionInstanceStatus is not null) o.DecisionInstanceStatus = m.DecisionInstanceStatus;
            if (m.DecisionTransitionSequence.HasValue) o.DecisionTransitionSequence = m.DecisionTransitionSequence;
            if (m.DecisionAtUtcTicksV1.HasValue) o.DecisionAtUtcTicksV1 = m.DecisionAtUtcTicksV1;
            if (m.ApprovalMarketSelection is not null) o.ApprovalMarketSelection = m.ApprovalMarketSelection;
            if (m.MarketValidatedAtUtcTicksV1.HasValue) o.MarketValidatedAtUtcTicksV1 = m.MarketValidatedAtUtcTicksV1;
            if (m.ApprovalMarketProofFingerprint is not null)
                o.ApprovalMarketProofFingerprint = m.ApprovalMarketProofFingerprint;
            if (m.WithdrawalCommandId.HasValue) o.WithdrawalCommandId = m.WithdrawalCommandId;
            if (m.WithdrawalFingerprint is not null) o.WithdrawalFingerprint = m.WithdrawalFingerprint;
            if (m.WithdrawalRequesterSubjectId.HasValue) o.WithdrawalRequesterSubjectId = m.WithdrawalRequesterSubjectId;
            if (m.WithdrawalExpectedLskuVersion.HasValue) o.WithdrawalExpectedLskuVersion = m.WithdrawalExpectedLskuVersion;
            if (m.WithdrawalReasonCode is not null) o.WithdrawalReasonCode = m.WithdrawalReasonCode;
            if (m.WithdrawalComment is not null) o.WithdrawalComment = m.WithdrawalComment;
            if (m.WithdrawalExpectedWorkflowInstanceVersion.HasValue)
                o.WithdrawalExpectedWorkflowInstanceVersion = m.WithdrawalExpectedWorkflowInstanceVersion;
            if (m.WithdrawalExpectedApprovalTaskVersion.HasValue)
                o.WithdrawalExpectedApprovalTaskVersion = m.WithdrawalExpectedApprovalTaskVersion;
            if (m.WithdrawalTransitionLogId.HasValue) o.WithdrawalTransitionLogId = m.WithdrawalTransitionLogId;
            if (m.WithdrawalObservedAtUtcTicksV1.HasValue) o.WithdrawalObservedAtUtcTicksV1 = m.WithdrawalObservedAtUtcTicksV1;
            if (m.WithdrawalTransitionSequence.HasValue) o.WithdrawalTransitionSequence = m.WithdrawalTransitionSequence;
            if (m.WithdrawalResultWorkflowInstanceVersion.HasValue)
                o.WithdrawalResultWorkflowInstanceVersion = m.WithdrawalResultWorkflowInstanceVersion;
            if (m.WithdrawalResultApprovalTaskVersion.HasValue)
                o.WithdrawalResultApprovalTaskVersion = m.WithdrawalResultApprovalTaskVersion;
            if (m.WithdrawalTaskStatus is not null) o.WithdrawalTaskStatus = m.WithdrawalTaskStatus;
            if (m.WithdrawalInstanceStatus is not null) o.WithdrawalInstanceStatus = m.WithdrawalInstanceStatus;
            if (m.WithdrawalObjectRef is not null) o.WithdrawalObjectRef = m.WithdrawalObjectRef;
            if (m.ReleaseLease) { o.LeaseOwner = null; o.LeaseUntilUtcTicksV1 = null; }
            if (m.WithdrawalUnansweredSinceUtcTicksV1.HasValue && !m.ClearWithdrawal)
                o.WithdrawalUnansweredSinceUtcTicksV1 = m.WithdrawalUnansweredSinceUtcTicksV1;
            if (m.ClearWithdrawal)
            {
                o.WithdrawalUnansweredSinceUtcTicksV1 = null; o.WithdrawalCommandId = null; o.WithdrawalFingerprint = null;
                o.WithdrawalRequesterSubjectId = null; o.WithdrawalExpectedLskuVersion = null;
                o.WithdrawalReasonCode = null; o.WithdrawalComment = null;
                o.WithdrawalExpectedWorkflowInstanceVersion = null; o.WithdrawalExpectedApprovalTaskVersion = null;
                o.WithdrawalTransitionLogId = null; o.WithdrawalObservedAtUtcTicksV1 = null;
                o.WithdrawalTransitionSequence = null; o.WithdrawalResultWorkflowInstanceVersion = null;
                o.WithdrawalResultApprovalTaskVersion = null; o.WithdrawalTaskStatus = null;
                o.WithdrawalInstanceStatus = null; o.WithdrawalObjectRef = null;
            }
            if (m.ClearDecision)
            {
                o.DecisionTransitionLogId = null; o.DecisionKind = null; o.DecisionObservedAtUtcTicksV1 = null;
                o.DecisionActorSubjectId = null; o.DecisionReasonCode = null; o.DecisionObjectType = null;
                o.DecisionObjectId = null; o.DecisionObjectRef = null; o.DecisionWorkflowTemplateId = null;
                o.DecisionWorkflowTemplateVersionId = null; o.DecisionTaskStatus = null; o.DecisionInstanceStatus = null;
                o.DecisionTransitionSequence = null; o.DecisionAtUtcTicksV1 = null; o.ApprovalMarketSelection = null;
                o.MarketValidatedAtUtcTicksV1 = null; o.ApprovalMarketProofFingerprint = null;
            }
            if (m.LastStartSentAtUtcTicksV1.HasValue) o.LastStartSentAtUtcTicksV1 = m.LastStartSentAtUtcTicksV1;
            o.FailedAttemptCount = m.Streak switch
            {
                RecoveryStreakWrite.Increment => o.FailedAttemptCount + 1,
                RecoveryStreakWrite.Keep => o.FailedAttemptCount,
                _ => 0
            };
        }
    }

    private sealed class IdLskus(IdWorld world) : ILskuRepository
    {
        private Lsku Lsku => world.Lsku;

        public Task<Lsku?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(world.Answers.LskuMissing || id != LskuId ? null : Lsku);

        public Task<LskuLifecycleWriteResult> FenceIdentitySubmitAsync(Guid id, int expectedVersion, Guid operationId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(world.Answers.FenceSucceeds
                ? new LskuLifecycleWriteResult(true, Lsku)
                : new LskuLifecycleWriteResult(false, Lsku, "LSKU_IDENTITY_SUBMIT_FENCE_REFUSED"));

        public Task<LskuLifecycleWriteResult> SubmitIdentityAsync(Guid id, int expectedVersion,
            ProductIdentityWorkflowBinding workflowBinding, LocalAuditIntent auditIntent,
            CancellationToken cancellationToken = default)
        {
            var code = world.Answers.SubmitWrite;
            if (code == Inconsistent) return Task.FromResult(new LskuLifecycleWriteResult(true, Lsku));
            if (code is not null) return Task.FromResult(new LskuLifecycleWriteResult(false, Lsku, code));
            Lsku.Version = expectedVersion + 1;
            Lsku.LifecycleStatus = ProductIdentityLifecycleStatus.PendingIdentityApproval;
            Lsku.IdentityWorkflowBinding = workflowBinding;
            Lsku.ActiveLifecycleOperation = null;
            Lsku.AuditIntents.Add(auditIntent);
            return Task.FromResult(new LskuLifecycleWriteResult(true, Lsku));
        }

        public Task<LskuLifecycleWriteResult> ReconcileIdentityDecisionAsync(Guid id, int expectedVersion,
            ProductIdentityWorkflowDecisionEvidence decisionEvidence, LocalAuditIntent auditIntent,
            CancellationToken cancellationToken = default)
        {
            var code = world.Answers.DecisionWrite;
            if (code == Inconsistent) return Task.FromResult(new LskuLifecycleWriteResult(true, Lsku));
            if (code is not null) return Task.FromResult(new LskuLifecycleWriteResult(false, Lsku, code));
            Lsku.Version = expectedVersion + 1;
            Lsku.LifecycleStatus = decisionEvidence.Decision == ProductIdentityDecisionKind.Approved
                ? ProductIdentityLifecycleStatus.IdentityApproved
                : ProductIdentityLifecycleStatus.Draft;
            Lsku.IdentityWorkflowBinding!.TerminalDecision = decisionEvidence;
            Lsku.AuditIntents.Add(auditIntent);
            return Task.FromResult(new LskuLifecycleWriteResult(true, Lsku));
        }

        public Task<Lsku?> GetByCreationCommandIdAsync(string id, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<Lsku?> GetByReservationIdAsync(Guid id, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<Lsku?> GetByIdentityKeyAsync(Guid id, string market, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<LskuCreateResult> CreateDraftAsync(Lsku value, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class IdEngine(IdWorld world) : IProductIdentityWorkflowClient
    {
        public Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>> StartAsync(Guid tenantId,
            ProductIdentityWorkflowStartRequest request, string delegatedUserToken,
            CancellationToken cancellationToken = default) => Task.FromResult(world.Answers.Start);

        public Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>> GetStartResultAsync(
            Guid tenantId, ProductIdentityWorkflowStartResultRequest request,
            CancellationToken cancellationToken = default) => Task.FromResult(world.Answers.Lookup);

        public Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowTerminalEvidence>>
            GetTerminalEvidenceAsync(Guid tenantId, ProductIdentityWorkflowTerminalEvidenceRequest request,
                CancellationToken cancellationToken = default) => Task.FromResult(world.Answers.Terminal);

        public Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowCancellationPreflight>>
            GetCancellationPreflightAsync(Guid tenantId, ProductIdentityWorkflowCancellationPreflightRequest request,
                CancellationToken cancellationToken = default) => Task.FromResult(world.Answers.Preflight);

        public Task<ProductIdentityWorkflowTransportResult<EngineCancellation>> CancelAsync(Guid tenantId,
            ProductIdentityWorkflowCancellationRequest request, string delegatedUserToken,
            CancellationToken cancellationToken = default) => Task.FromResult(world.Answers.Cancel);
    }

    private sealed class IdMarket(IdWorld world) : IWorkflowVerifiedMarketReferenceResolver
    {
        public Task<VerifiedMarketReferenceResolveResult> ResolveLatestAsync(Guid tenantId, string marketCode,
            CancellationToken cancellationToken = default)
        {
            var answers = world.Answers.Market;
            var answer = answers[Math.Min(world.MarketCalls, answers.Count - 1)];
            world.MarketCalls++;
            return Task.FromResult(answer);
        }
    }

    public class MatrixProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[]?, object?> Handler { get; set; } = null!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Handler(targetMethod!, args);
    }

    private static ProductIdentityWorkflowStartResult IdStartResult() => new(
        WorkflowId, TemplateId, TemplateVersionId, TaskId, SnapshotId, StartLogId,
        "LS-0001", "Running", "Approval", "Review", StartedAt, null, false, null);

    private static ProductIdentityWorkflowTerminalEvidence IdTerminal(ProductIdentityDecisionKind decision) => new(
        WorkflowId, TaskId, TemplateId, TemplateVersionId, "lsku", LskuId.ToString("D"), "LS-0001",
        decision == ProductIdentityDecisionKind.Approved ? "Approve" : "Reject", ApproverId.ToString("D"),
        decision == ProductIdentityDecisionKind.Rejected ? "REJECTED" : null, DecisionAt, 2,
        decision == ProductIdentityDecisionKind.Approved ? "Approved" : "Rejected",
        decision == ProductIdentityDecisionKind.Approved ? "Completed" : "Rejected", null);

    // ------------------------------------------------------------------------------------------------------------------
    // LSKU retirement request: entries, seeds, knobs, world
    // ------------------------------------------------------------------------------------------------------------------

    private enum RetirementEntry
    {
        WorkerRecover,     // no delegated token
        MakerRepeat        // StartInteractiveAsync with the maker's token
    }

    private static async Task RunRetirementAsync(RetWorld world, RetirementEntry entry)
    {
        var processor = world.Processor();
        var execution = new LskuRetirementRequestExecutionConfiguration(Lease, Retry);
        if (entry == RetirementEntry.WorkerRecover)
            await processor.RecoverAsync(world.Store.Read(), "worker-1", execution, default);
        else
            await processor.StartInteractiveAsync(TenantId, LskuId, world.Op.BaseLskuVersion, RetirementOperationId,
                MakerId, RetirementReason, Token, execution, default);
    }

    private static IEnumerable<(string Name, Action<RetWorld> Seed)> RetirementSeeds()
    {
        yield return ("Prepared", _ => { });
        foreach (var (name, sent, updated) in new (string, long?, long)[]
                 {
                     ("sent-long-ago", LongAgo.UtcTicks, LongAgo.UtcTicks),
                     ("sent-recently", Recently.UtcTicks, LongAgo.UtcTicks),
                     ("legacy-unmarked", null, LongAgo.UtcTicks),
                     ("legacy-unmarked-recent", null, Recently.UtcTicks)
                 })
        {
            yield return ($"StartOutcomeUnknown/{name}", w =>
            {
                w.Locked(); w.At(LskuRetirementRequestCheckpoint.StartOutcomeUnknown);
                w.Op.LastStartSentAtUtcTicksV1 = sent; w.Op.UpdatedAtUtcTicksV1 = updated;
            });
            // Legacy: requests parked for their maker before FIX1 (the processor no longer writes this checkpoint).
            yield return ($"AwaitingMakerReplay/legacy/locked/{name}", w =>
            {
                w.Locked();
                w.At(LskuRetirementRequestCheckpoint.AwaitingMakerReplay,
                    ProductIdentityWorkflowRecoveryDisposition.AwaitingMakerReplay, "LSKU_RETIREMENT_MAKER_REPLAY_REQUIRED");
                w.Op.LastStartSentAtUtcTicksV1 = sent; w.Op.UpdatedAtUtcTicksV1 = updated;
            });
            yield return ($"AwaitingMakerReplay/legacy/unlocked/{name}", w =>
            {
                w.At(LskuRetirementRequestCheckpoint.AwaitingMakerReplay,
                    ProductIdentityWorkflowRecoveryDisposition.AwaitingMakerReplay, "LSKU_RETIREMENT_MAKER_REPLAY_REQUIRED");
                w.Op.LastStartSentAtUtcTicksV1 = sent; w.Op.UpdatedAtUtcTicksV1 = updated;
            });
            yield return ($"Manual/not-started/{name}", w =>
            {
                w.Locked(); w.Stuck("LSKU_RETIREMENT_START_LOOKUP_FAILED");
                w.Op.LastStartSentAtUtcTicksV1 = sent; w.Op.UpdatedAtUtcTicksV1 = updated;
            });
        }
        yield return ("WorkflowStarted", w => { w.Locked(); w.Started(); w.At(LskuRetirementRequestCheckpoint.WorkflowStarted); });
        yield return ("AwaitingDecision", w => { w.Locked(); w.Started(); w.At(LskuRetirementRequestCheckpoint.AwaitingDecision); });
        yield return ("AwaitingDecision/no-instance", w => { w.Locked(); w.At(LskuRetirementRequestCheckpoint.AwaitingDecision); });
        foreach (var kind in new[] { ProductIdentityDecisionKind.Approved, ProductIdentityDecisionKind.Rejected })
        {
            yield return ($"DecisionObserved/{kind}", w =>
            {
                w.Locked(); w.Started(); w.Decision(kind); w.At(LskuRetirementRequestCheckpoint.DecisionObserved);
            });
            yield return ($"DecisionApplied/{kind}", w =>
            {
                w.Locked(); w.Started(); w.Decision(kind); w.Applied();
                w.At(LskuRetirementRequestCheckpoint.DecisionApplied);
            });
            yield return ($"Manual/decision-recorded/{kind}", w =>
            {
                w.Locked(); w.Started(); w.Decision(kind); w.Stuck(LskuRetirementRequestWorkflowProcessor.LskuDrift);
            });
        }
        yield return ("DecisionObserved/actor-missing", w =>
        {
            w.Locked(); w.Started(); w.Decision(ProductIdentityDecisionKind.Approved);
            w.Op.DecisionActorSubjectId = null; w.At(LskuRetirementRequestCheckpoint.DecisionObserved);
        });
        yield return ("DecisionApplied/readback-differs", w =>
        {
            w.Locked(); w.Started(); w.Decision(ProductIdentityDecisionKind.Approved);
            w.At(LskuRetirementRequestCheckpoint.DecisionApplied);
        });
        yield return ("Manual/started", w =>
        {
            w.Locked(); w.Started(); w.Stuck(LskuRetirementRequestWorkflowProcessor.EvidenceConflict);
        });
        yield return ("Manual/ended", w =>
        {
            w.At(LskuRetirementRequestCheckpoint.ManualReconciliationRequired,
                ProductIdentityWorkflowRecoveryDisposition.None, LskuRetirementRequestWorkflowProcessor.StartNotReceived);
        });
        yield return ("Completed", w =>
        {
            w.Locked(); w.Started(); w.Decision(ProductIdentityDecisionKind.Approved); w.Applied();
            w.At(LskuRetirementRequestCheckpoint.Completed);
        });
    }

    private static IEnumerable<(string Name, Action<RetAnswers> Knob)> RetirementKnobs()
    {
        yield return ("baseline", _ => { });
        yield return ("start=Success/invalid-proof", a => a.Start =
            ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>.Success(
                RetStartResult() with { ObjectRef = "OTHER" }));
        foreach (var (name, answer) in Faults<ProductIdentityWorkflowStartResult>())
            yield return ($"start={name}", a => a.Start = answer);
        yield return ("lookup=Success/invalid-proof", a => a.Lookup =
            ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>.Success(
                RetStartResult() with { ApprovalTaskId = Guid.Empty }));
        foreach (var (name, answer) in Faults<ProductIdentityWorkflowStartResult>())
            yield return ($"lookup={name}", a => a.Lookup = answer);
        yield return ("lookup=NotFound,start=Timeout", a =>
        {
            a.Lookup = Fail<ProductIdentityWorkflowStartResult>(ProductIdentityWorkflowTransportOutcome.NotFound, "N");
            a.Start = Fail<ProductIdentityWorkflowStartResult>(ProductIdentityWorkflowTransportOutcome.Timeout, "T");
        });
        yield return ("lookup=NotFound,start=Forbidden", a =>
        {
            a.Lookup = Fail<ProductIdentityWorkflowStartResult>(ProductIdentityWorkflowTransportOutcome.NotFound, "N");
            a.Start = Fail<ProductIdentityWorkflowStartResult>(ProductIdentityWorkflowTransportOutcome.Forbidden,
                "WORKFLOW_DELEGATED_USER_FORBIDDEN");
        });
        yield return ("lookup=NotFound,start=NotFound", a =>
        {
            a.Lookup = Fail<ProductIdentityWorkflowStartResult>(ProductIdentityWorkflowTransportOutcome.NotFound, "N");
            a.Start = Fail<ProductIdentityWorkflowStartResult>(ProductIdentityWorkflowTransportOutcome.NotFound, "N");
        });
        yield return ("lookup=NotFound,start=Conflict", a =>
        {
            a.Lookup = Fail<ProductIdentityWorkflowStartResult>(ProductIdentityWorkflowTransportOutcome.NotFound, "N");
            a.Start = Fail<ProductIdentityWorkflowStartResult>(ProductIdentityWorkflowTransportOutcome.Conflict, "C");
        });

        var approve = RetTerminal(true);
        foreach (var (name, evidence) in new (string, ProductIdentityWorkflowTerminalEvidence)[]
                 {
                     ("Approve", approve), ("Reject", RetTerminal(false)),
                     ("self-approval", approve with { ActorUserId = MakerId.ToString("D") }),
                     ("wrong-task", approve with { ApprovalTaskId = Guid.NewGuid() }),
                     ("unknown-action", approve with { TerminalAction = "Escalate" }),
                     ("actor-unreadable", approve with { ActorUserId = "not-a-guid" })
                 })
            yield return ($"terminal={name}", a => a.Terminal =
                ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowTerminalEvidence>.Success(evidence));
        foreach (var (name, answer) in Faults<ProductIdentityWorkflowTerminalEvidence>())
            yield return ($"terminal={name}", a => a.Terminal = answer);

        foreach (var code in new[] { "LSKU_CONCURRENCY_CONFLICT", "LSKU_LIFECYCLE_OPERATION_ACTIVE", "AUDIT_INTENT_CAPACITY_REACHED" })
            yield return ($"acquire-write={code}", a => a.AcquireWrite = code);
        foreach (var code in new[]
                 {
                     "AUDIT_INTENT_CAPACITY_REACHED", "LSKU_CONCURRENCY_CONFLICT",
                     LskuRetirementRequestWorkflowProcessor.ReleaseConflict, "LSKU_IDENTITY_STATE_CONFLICT",
                     ProductIdentityStore.Unavailable
                 })
            yield return ($"release-write={code}", a => a.ReleaseWrite = code);
        foreach (var (name, code) in LskuWriteCodes.Where(x => x.Code != Inconsistent))
            yield return ($"decision-write={name}", a => a.DecisionWrite = code);
        yield return ("lsku=missing", a => a.LskuMissing = true);
        yield return ("streak=at-limit", a => a.FailedAttempts = ProductIdentityRecoveryStreak.DefaultQuarantineAfterFailedAttempts - 1);
        yield return ("streak=at-limit,lookup=Conflict", a =>
        {
            a.FailedAttempts = ProductIdentityRecoveryStreak.DefaultQuarantineAfterFailedAttempts - 1;
            a.Lookup = Fail<ProductIdentityWorkflowStartResult>(ProductIdentityWorkflowTransportOutcome.Conflict, "C");
        });
        yield return ("streak=at-limit,release-write=conflict", a =>
        {
            a.FailedAttempts = ProductIdentityRecoveryStreak.DefaultQuarantineAfterFailedAttempts - 1;
            a.ReleaseWrite = LskuRetirementRequestWorkflowProcessor.ReleaseConflict;
        });
        yield return ("streak=at-limit,decision-write=LSKU_CONCURRENCY_CONFLICT", a =>
        {
            a.FailedAttempts = ProductIdentityRecoveryStreak.DefaultQuarantineAfterFailedAttempts - 1;
            a.DecisionWrite = "LSKU_CONCURRENCY_CONFLICT";
        });
        yield return ("streak=at-limit,terminal=NotFound", a =>
        {
            a.FailedAttempts = ProductIdentityRecoveryStreak.DefaultQuarantineAfterFailedAttempts - 1;
            a.Terminal = Fail<ProductIdentityWorkflowTerminalEvidence>(ProductIdentityWorkflowTransportOutcome.NotFound, "N");
        });
    }

    private sealed class RetAnswers
    {
        public ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult> Start { get; set; } =
            ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>.Success(RetStartResult());
        public ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult> Lookup { get; set; } =
            ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>.Success(RetStartResult());
        public ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowTerminalEvidence> Terminal { get; set; } =
            ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowTerminalEvidence>.Success(RetTerminal(true));
        public string? AcquireWrite { get; set; }
        public string? ReleaseWrite { get; set; }
        public string? DecisionWrite { get; set; }
        public bool LskuMissing { get; set; }
        public int? FailedAttempts { get; set; }
    }

    private sealed class RetWorld
    {
        public RetWorld(Census<LskuRetirementRequestCheckpoint> census)
        {
            Op = new LskuRetirementRequestOperation
            {
                Id = RetirementOperationId, TenantId = TenantId, OperationId = RetirementOperationId, LskuId = LskuId,
                BaseLskuVersion = 4, MakerSubjectId = MakerId, RequestReason = RetirementReason,
                WorkflowTemplateId = TemplateId, CandidatePrincipalIds = [ApproverId], ReasonCode = "RETIRE",
                ObjectType = LskuRetirementRequestWorkflowStartRequestFactory.ObjectType,
                ObjectId = RetirementOperationId.ToString("D"), ObjectRef = "LS-1",
                StartIdempotencyKey = $"lsku-retirement:{TenantId:D}:{RetirementOperationId:D}",
                Checkpoint = LskuRetirementRequestCheckpoint.Prepared,
                RecoveryDisposition = ProductIdentityWorkflowRecoveryDisposition.None,
                CreatedAtUtcTicksV1 = LongAgo.UtcTicks, UpdatedAtUtcTicksV1 = LongAgo.UtcTicks
            };
            Op.OperationFingerprint = LskuRetirementRequestWorkflowStartRequestFactory.ComputeFingerprint(Op);
            Lsku = new Lsku
            {
                Id = LskuId, TenantId = TenantId, GskuId = GskuId, CanonicalCode = "LS-1", MarketCode = "TR",
                LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved, Version = 4
            };
            Store = new RetStore(this, census);
        }

        public LskuRetirementRequestOperation Op { get; }
        public Lsku Lsku { get; }
        public RetAnswers Answers { get; } = new();
        public RetStore Store { get; }
        public LskuActiveLifecycleOperationBinding Binding =>
            new(LskuLifecycleOperationKind.Retirement, RetirementOperationId, 4);

        public LskuRetirementRequestWorkflowProcessor Processor() => new(Store, new RetLskus(this), new RetEngine(this),
            new LskuRetirementRequestWorkflowStartRequestFactory(
                new(TemplateId, null, [ApproverId], "RETIRE", false, false, null), Clock),
            Clock);

        public void At(LskuRetirementRequestCheckpoint checkpoint,
            ProductIdentityWorkflowRecoveryDisposition disposition = ProductIdentityWorkflowRecoveryDisposition.None,
            string? code = null)
        {
            Op.Checkpoint = checkpoint; Op.RecoveryDisposition = disposition; Op.LastFailureCode = code;
            Op.IsActive = !(checkpoint == LskuRetirementRequestCheckpoint.Completed
                || checkpoint == LskuRetirementRequestCheckpoint.ManualReconciliationRequired
                && disposition == ProductIdentityWorkflowRecoveryDisposition.None);
        }

        public void Stuck(string code) => At(LskuRetirementRequestCheckpoint.ManualReconciliationRequired,
            ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired, code);

        // The LSKU's lock taken by AcquireLifecycleOperationAsync (version base + 1).
        public void Locked()
        {
            Lsku.Version = 5;
            Lsku.ActiveLifecycleOperation = Binding;
            Lsku.AuditIntents.Add(LskuIdentityLifecycleAuditIntentFactory.CreateRetirementOperation(Lsku, 4,
                RetirementOperationId, MakerId, ProductAuditOperation.LskuRetirementRequested, RetirementReason,
                LongAgo));
        }

        public void Started()
        {
            Op.WorkflowInstanceId = WorkflowId; Op.WorkflowTemplateVersionId = TemplateVersionId;
            Op.ApprovalTaskId = TaskId; Op.AssignmentSnapshotId = SnapshotId; Op.StartTransitionLogId = StartLogId;
            Op.WorkflowStartedAtUtcTicksV1 = StartedAt.UtcTicks; Op.LastStartSentAtUtcTicksV1 = LongAgo.UtcTicks;
        }

        public void Decision(ProductIdentityDecisionKind kind)
        {
            var approved = kind == ProductIdentityDecisionKind.Approved;
            Op.DecisionKind = kind; Op.DecisionActorSubjectId = ApproverId; Op.DecisionReasonCode = "DECISION";
            Op.DecisionAtUtcTicksV1 = DecisionAt.UtcTicks; Op.DecisionTransitionSequence = 3;
            Op.DecisionTaskStatus = approved ? "Approved" : "Rejected";
            Op.DecisionInstanceStatus = approved ? "Completed" : "Rejected";
        }

        // The decision as ApplyRetirementDecisionAsync leaves the LSKU (version base + 2, the lock gone).
        public void Applied()
        {
            var approved = Op.DecisionKind == ProductIdentityDecisionKind.Approved;
            Lsku.AuditIntents.Add(LskuIdentityLifecycleAuditIntentFactory.CreateRetirementOperation(Lsku, 5,
                RetirementOperationId, Op.DecisionActorSubjectId!.Value,
                approved ? ProductAuditOperation.LskuIdentityRetired : ProductAuditOperation.LskuRetirementRejected,
                RetirementReason, new DateTimeOffset(Op.DecisionAtUtcTicksV1!.Value, TimeSpan.Zero)));
            Lsku.Version = 6;
            Lsku.ActiveLifecycleOperation = null;
            Lsku.LifecycleStatus = approved
                ? ProductIdentityLifecycleStatus.Retired
                : ProductIdentityLifecycleStatus.IdentityApproved;
        }
    }

    private sealed class RetStore(RetWorld world, Census<LskuRetirementRequestCheckpoint> census)
        : ILskuRetirementRequestOperationRepository
    {
        private LskuRetirementRequestOperation Stored => world.Op;

        public LskuRetirementRequestOperation Read() => Copy(Stored);

        public Task<GlobalProductIdentityWorkflowTenantPartitionPage> DiscoverTenantPartitionsAsync(Guid? afterTenantId,
            int limit, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<LskuRetirementRequestReserveResult> ReserveAsync(LskuRetirementRequestOperation operation,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<LskuRetirementRequestOperation?> GetByOperationIdAsync(Guid operationId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Stored.OperationId == operationId ? Read() : null);

        public Task<LskuRetirementRequestClaim?> TryClaimAsync(Guid operationId, string fingerprint,
            IReadOnlyCollection<LskuRetirementRequestCheckpoint> checkpoints, string owner, long nowUtcTicks,
            long untilUtcTicks, CancellationToken cancellationToken = default)
        {
            if (operationId != Stored.OperationId || fingerprint != Stored.OperationFingerprint
                || !checkpoints.Contains(Stored.Checkpoint) || Stored.NextAttemptAtUtcTicksV1 > nowUtcTicks
                || Stored.LeaseOwner is not null && Stored.LeaseUntilUtcTicksV1 > nowUtcTicks)
                return Task.FromResult<LskuRetirementRequestClaim?>(null);
            Stored.LeaseOwner = owner;
            Stored.LeaseUntilUtcTicksV1 = untilUtcTicks;
            Stored.LeaseGeneration++;
            return Task.FromResult<LskuRetirementRequestClaim?>(new(Stored.TenantId, Stored.OperationId,
                Stored.OperationFingerprint, owner, Stored.LeaseGeneration, Stored.Checkpoint, untilUtcTicks));
        }

        public Task<bool> AdvanceAsync(LskuRetirementRequestClaim claim, LskuRetirementRequestMutation m,
            CancellationToken cancellationToken = default)
        {
            census.Record(claim.Checkpoint, m.NextCheckpoint);
            if (m.UpdatedAtUtcTicks <= 0 || claim.LeaseUntilUtcTicksV1 <= m.UpdatedAtUtcTicks
                || !RetirementStoreRules.Allowed(claim.Checkpoint, m.NextCheckpoint)
                || m.NextCheckpoint == LskuRetirementRequestCheckpoint.ManualReconciliationRequired
                   && m.RecoveryDisposition == ProductIdentityWorkflowRecoveryDisposition.Retryable)
                throw census.Refuse($"store refuses {claim.Checkpoint} -> {m.NextCheckpoint} "
                    + $"({m.RecoveryDisposition}, {m.LastFailureCode})",
                    RetirementStoreRules.Allowed(claim.Checkpoint, m.NextCheckpoint));
            if (Stored.Checkpoint != claim.Checkpoint || Stored.LeaseOwner != claim.LeaseOwner
                || Stored.LeaseGeneration != claim.LeaseGeneration
                || Stored.LeaseUntilUtcTicksV1 != claim.LeaseUntilUtcTicksV1)
                return Task.FromResult(false);
            var o = Stored;
            o.Checkpoint = m.NextCheckpoint; o.RecoveryDisposition = m.RecoveryDisposition;
            o.NextAttemptAtUtcTicksV1 = m.NextAttemptAtUtcTicksV1; o.LastFailureCode = m.LastFailureCode;
            o.UpdatedAtUtcTicksV1 = m.UpdatedAtUtcTicks;
            if (m.WorkflowInstanceId.HasValue) o.WorkflowInstanceId = m.WorkflowInstanceId;
            if (m.WorkflowTemplateId.HasValue) o.WorkflowTemplateId = m.WorkflowTemplateId;
            if (m.WorkflowTemplateVersionId.HasValue) o.WorkflowTemplateVersionId = m.WorkflowTemplateVersionId;
            if (m.ApprovalTaskId.HasValue) o.ApprovalTaskId = m.ApprovalTaskId;
            if (m.AssignmentSnapshotId.HasValue) o.AssignmentSnapshotId = m.AssignmentSnapshotId;
            if (m.StartTransitionLogId.HasValue) o.StartTransitionLogId = m.StartTransitionLogId;
            if (m.WorkflowStartedAtUtcTicksV1.HasValue) o.WorkflowStartedAtUtcTicksV1 = m.WorkflowStartedAtUtcTicksV1;
            if (m.DecisionKind.HasValue) o.DecisionKind = m.DecisionKind;
            if (m.DecisionActorSubjectId.HasValue) o.DecisionActorSubjectId = m.DecisionActorSubjectId;
            if (m.DecisionReasonCode is not null) o.DecisionReasonCode = m.DecisionReasonCode;
            if (m.DecisionAtUtcTicksV1.HasValue) o.DecisionAtUtcTicksV1 = m.DecisionAtUtcTicksV1;
            if (m.DecisionTransitionSequence.HasValue) o.DecisionTransitionSequence = m.DecisionTransitionSequence;
            if (m.DecisionTaskStatus is not null) o.DecisionTaskStatus = m.DecisionTaskStatus;
            if (m.DecisionInstanceStatus is not null) o.DecisionInstanceStatus = m.DecisionInstanceStatus;
            if (m.NextCheckpoint == LskuRetirementRequestCheckpoint.Completed
                || m.NextCheckpoint == LskuRetirementRequestCheckpoint.ManualReconciliationRequired
                && m.RecoveryDisposition == ProductIdentityWorkflowRecoveryDisposition.None)
                o.IsActive = false;
            o.FailedAttemptCount = m.Streak switch
            {
                RecoveryStreakWrite.Increment => o.FailedAttemptCount + 1,
                RecoveryStreakWrite.Keep => o.FailedAttemptCount,
                _ => 0
            };
            if (m.LastStartSentAtUtcTicksV1.HasValue) o.LastStartSentAtUtcTicksV1 = m.LastStartSentAtUtcTicksV1;
            if (m.ReleaseLease) { o.LeaseOwner = null; o.LeaseUntilUtcTicksV1 = null; }
            return Task.FromResult(true);
        }

        public Task<LskuRetirementRequestPage> DiscoverRecoverableAsync(long nowUtcTicks, int limit,
            Guid? afterOperationId = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task ReleaseIfOwnedAsync(Guid operationId, string leaseOwner, long leaseUntilUtcTicks,
            CancellationToken cancellationToken = default)
        {
            if (Stored.LeaseOwner == leaseOwner && Stored.LeaseUntilUtcTicksV1 == leaseUntilUtcTicks)
            {
                Stored.LeaseOwner = null;
                Stored.LeaseUntilUtcTicksV1 = null;
            }
            return Task.CompletedTask;
        }
    }

    private sealed class RetLskus(RetWorld world) : ILskuRepository
    {
        private Lsku Lsku => world.Lsku;

        public Task<Lsku?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(world.Answers.LskuMissing || id != LskuId ? null : Lsku);

        public Task<LskuLifecycleWriteResult> AcquireLifecycleOperationAsync(Guid id, int expectedVersion,
            LskuActiveLifecycleOperationBinding binding, LocalAuditIntent auditIntent,
            CancellationToken cancellationToken = default)
        {
            if (world.Answers.AcquireWrite is { } code)
                return Task.FromResult(new LskuLifecycleWriteResult(false, Lsku, code));
            if (Lsku.ActiveLifecycleOperation == binding)
                return Task.FromResult(new LskuLifecycleWriteResult(true, Lsku, IsReplay: true));
            Lsku.ActiveLifecycleOperation = binding;
            Lsku.Version = expectedVersion + 1;
            Lsku.AuditIntents.Add(auditIntent);
            return Task.FromResult(new LskuLifecycleWriteResult(true, Lsku));
        }

        public Task<LskuLifecycleWriteResult> ApplyRetirementDecisionAsync(Guid id, int expectedVersion,
            LskuActiveLifecycleOperationBinding binding, bool approved, LocalAuditIntent auditIntent,
            CancellationToken cancellationToken = default)
        {
            if (world.Answers.DecisionWrite is { } code)
                return Task.FromResult(new LskuLifecycleWriteResult(false, Lsku, code));
            Lsku.LifecycleStatus = approved
                ? ProductIdentityLifecycleStatus.Retired
                : ProductIdentityLifecycleStatus.IdentityApproved;
            Lsku.ActiveLifecycleOperation = null;
            Lsku.Version = expectedVersion + 1;
            Lsku.AuditIntents.Add(auditIntent);
            return Task.FromResult(new LskuLifecycleWriteResult(true, Lsku));
        }

        public Task<LskuLifecycleWriteResult> ReleaseLifecycleOperationAsync(Guid id, int expectedVersion,
            LskuActiveLifecycleOperationBinding binding, LocalAuditIntent auditIntent,
            CancellationToken cancellationToken = default)
        {
            if (world.Answers.ReleaseWrite is { } code)
                return Task.FromResult(new LskuLifecycleWriteResult(false, Lsku, code));
            Lsku.ActiveLifecycleOperation = null;
            Lsku.Version = expectedVersion + 1;
            Lsku.AuditIntents.Add(auditIntent);
            return Task.FromResult(new LskuLifecycleWriteResult(true, Lsku));
        }

        public Task<Lsku?> GetByCreationCommandIdAsync(string id, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<Lsku?> GetByReservationIdAsync(Guid id, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<Lsku?> GetByIdentityKeyAsync(Guid id, string market, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<LskuCreateResult> CreateDraftAsync(Lsku value, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class RetEngine(RetWorld world) : IProductIdentityWorkflowClient
    {
        public Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>> StartAsync(Guid tenantId,
            ProductIdentityWorkflowStartRequest request, string delegatedUserToken,
            CancellationToken cancellationToken = default) => Task.FromResult(world.Answers.Start);

        public Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>> GetStartResultAsync(
            Guid tenantId, ProductIdentityWorkflowStartResultRequest request,
            CancellationToken cancellationToken = default) => Task.FromResult(world.Answers.Lookup);

        public Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowTerminalEvidence>>
            GetTerminalEvidenceAsync(Guid tenantId, ProductIdentityWorkflowTerminalEvidenceRequest request,
                CancellationToken cancellationToken = default) => Task.FromResult(world.Answers.Terminal);
    }

    private static ProductIdentityWorkflowStartResult RetStartResult() => new(
        WorkflowId, TemplateId, TemplateVersionId, TaskId, SnapshotId, StartLogId,
        "LS-1", "Running", "Approval", "Review", StartedAt, null, false, null);

    private static ProductIdentityWorkflowTerminalEvidence RetTerminal(bool approved) => new(
        WorkflowId, TaskId, TemplateId, TemplateVersionId, LskuRetirementRequestWorkflowStartRequestFactory.ObjectType,
        RetirementOperationId.ToString("D"), "LS-1", approved ? "Approve" : "Reject", ApproverId.ToString("D"),
        "DECISION", DecisionAt, 3, approved ? "Approved" : "Rejected", approved ? "Completed" : "Rejected", null);

    // ------------------------------------------------------------------------------------------------------------------
    // Fixtures
    // ------------------------------------------------------------------------------------------------------------------

    private static T Copy<T>(T source) where T : new()
    {
        var copy = new T();
        foreach (var property in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0))
            property.SetValue(copy, property.GetValue(source));
        return copy;
    }

    private static ReferenceCatalogSelection Selection(Guid versionId, int version, DateTimeOffset time) => new()
    {
        SetCode = "market", ValueCode = "TR", CatalogVersionId = versionId, CatalogVersionNumber = version,
        ResolutionMode = ReferenceCatalogResolutionMode.Latest, ResolvedAtUtc = time
    };

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static readonly FixedClock Clock = new();
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Retry = TimeSpan.FromSeconds(5);
    private const string Token = "delegated-token";
    private const string WithdrawReason = "CHANGED_MIND";
    private const string WithdrawComment = "no longer needed";
    private const string RetirementReason = "Obsolete identity";
    private static readonly DateTimeOffset Now = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset LongAgo = Now.AddHours(-1);
    private static readonly DateTimeOffset Recently = Now.AddSeconds(-10);
    private static readonly DateTimeOffset StartedAt = Now.AddMinutes(-50);
    private static readonly DateTimeOffset DecisionAt = Now.AddMinutes(-5);
    private static readonly DateTimeOffset CancelAt = Now.AddMinutes(-4);
    private static readonly DateTimeOffset CreateMarketTime = Now.AddDays(-2);
    private static readonly DateTimeOffset CurrentMarketTime = Now.AddMinutes(-2);
    private static readonly Guid TenantId = Guid.Parse("10000000-0000-0000-0000-00000000000a");
    private static readonly Guid LskuId = Guid.Parse("20000000-0000-0000-0000-00000000000a");
    private static readonly Guid GskuId = Guid.Parse("30000000-0000-0000-0000-00000000000a");
    private static readonly Guid RevisionId = Guid.Parse("40000000-0000-0000-0000-00000000000a");
    private static readonly Guid OperationId = Guid.Parse("50000000-0000-0000-0000-00000000000a");
    private static readonly Guid RetirementOperationId = Guid.Parse("50000000-0000-0000-0000-00000000000b");
    private static readonly Guid MakerId = Guid.Parse("60000000-0000-0000-0000-00000000000a");
    private static readonly Guid ApproverId = Guid.Parse("70000000-0000-0000-0000-00000000000a");
    private static readonly Guid TemplateId = Guid.Parse("80000000-0000-0000-0000-00000000000a");
    private static readonly Guid TemplateVersionId = Guid.Parse("90000000-0000-0000-0000-00000000000a");
    private static readonly Guid WorkflowId = Guid.Parse("a0000000-0000-0000-0000-00000000000a");
    private static readonly Guid TaskId = Guid.Parse("b0000000-0000-0000-0000-00000000000a");
    private static readonly Guid SnapshotId = Guid.Parse("c0000000-0000-0000-0000-00000000000a");
    private static readonly Guid StartLogId = Guid.Parse("d0000000-0000-0000-0000-00000000000a");
    private static readonly Guid DecisionLogId = Guid.Parse("e0000000-0000-0000-0000-00000000000a");
    private static readonly Guid CancelLogId = Guid.Parse("e0000000-0000-0000-0000-00000000000b");
    private static readonly Guid CommandId = Guid.Parse("e0000000-0000-0000-0000-00000000000c");
    private static readonly Guid CreateMarketVersionId = Guid.Parse("f0000000-0000-0000-0000-00000000000a");
    private static readonly Guid CurrentMarketVersionId = Guid.Parse("f0000000-0000-0000-0000-00000000000b");
}
