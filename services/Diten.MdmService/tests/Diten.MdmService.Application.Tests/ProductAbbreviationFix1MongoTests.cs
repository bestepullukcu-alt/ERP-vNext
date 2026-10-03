using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Commands;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Services;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Persistence.Repositories;
using Diten.Shared.Core;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Application.Tests;

/// <summary>
/// WP-MDM-ABB-ACCEPT-01 FIX1 — the register's decision rules measured against a real (test-owned, replica-set) Mongo
/// through the production workflow and repositories.
/// </summary>
public sealed class ProductAbbreviationFix1MongoTests(Audit.AuditIntentTemporalMongoFixture mongo)
    : IClassFixture<Audit.AuditIntentTemporalMongoFixture>
{
    private static readonly string Maker = Guid.NewGuid().ToString("D");
    private static readonly string Maker2 = Guid.NewGuid().ToString("D");
    private static readonly string Checker = Guid.NewGuid().ToString("D");
    private static readonly string Checker2 = Guid.NewGuid().ToString("D");

    // ── B1: a correction is approved only while it is still requested, in one transaction ─────────────────────

    [Fact]
    public async Task Approving_a_cancelled_correction_leaves_the_active_code_active()
    {
        await using var scope = await Scope.CreateAsync(mongo.ReplicaConnectionString);
        var (active, correction) = await ActiveWithCorrectionAsync(scope, "AAA", "BBB");
        var cancelled = await scope.Workflow(Maker2).CancelAsync(
            new CancelProductAbbreviationAllocationCommand(correction.Id, correction.Version, Key()), default);
        Assert.True(cancelled.IsSuccessful);

        var approved = await scope.Workflow(Checker).ApproveAsync(
            new ApproveProductAbbreviationAllocationCommand(
                correction.Id, cancelled.Data!.Version, Key(), ExpectedFormerVersion: active.Version), default);

        Assert.False(approved.IsSuccessful);
        Assert.Equal("ABBREVIATION_NOT_PENDING", Assert.Single(approved.Errors));
        await AssertUntouchedAsync(scope, active);
    }

    [Fact]
    public async Task Approving_a_rejected_correction_leaves_the_active_code_active()
    {
        await using var scope = await Scope.CreateAsync(mongo.ReplicaConnectionString);
        var (active, correction) = await ActiveWithCorrectionAsync(scope, "CCA", "CCB");
        var rejected = await scope.Workflow(Checker2).RejectAsync(
            new RejectProductAbbreviationAllocationCommand(correction.Id, correction.Version, Key(), "no"), default);
        Assert.True(rejected.IsSuccessful);

        var approved = await scope.Workflow(Checker).ApproveAsync(
            new ApproveProductAbbreviationAllocationCommand(
                correction.Id, rejected.Data!.Version, Key(), ExpectedFormerVersion: active.Version), default);

        Assert.False(approved.IsSuccessful);
        Assert.Equal("ABBREVIATION_NOT_PENDING", Assert.Single(approved.Errors));
        await AssertUntouchedAsync(scope, active);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_stale_correction_approval_changes_nothing(bool staleReplacement)
    {
        await using var scope = await Scope.CreateAsync(mongo.ReplicaConnectionString);
        var (active, correction) = await ActiveWithCorrectionAsync(scope, staleReplacement ? "STA" : "STC", "STB");

        var approved = await scope.Workflow(Checker).ApproveAsync(
            new ApproveProductAbbreviationAllocationCommand(
                correction.Id,
                staleReplacement ? correction.Version + 3 : correction.Version,
                Key(),
                ExpectedFormerVersion: staleReplacement ? active.Version : active.Version + 3), default);

        Assert.False(approved.IsSuccessful);
        Assert.Equal("CONCURRENCY_CONFLICT", Assert.Single(approved.Errors));
        await AssertUntouchedAsync(scope, active);
        Assert.Equal(ProductAbbreviationLifecycleStatus.REQUESTED,
            (await scope.Register.GetByIdAsync(correction.Id))!.LifecycleStatus);
    }

    [Fact]
    public async Task Two_concurrent_approvals_retire_the_code_once_and_activate_its_replacement_once()
    {
        await using var scope = await Scope.CreateAsync(mongo.ReplicaConnectionString);
        var (active, correction) = await ActiveWithCorrectionAsync(scope, "TWA", "TWB");

        var results = await Task.WhenAll(
            scope.Workflow(Checker).ApproveAsync(new ApproveProductAbbreviationAllocationCommand(
                correction.Id, correction.Version, Key(), ExpectedFormerVersion: active.Version), default),
            scope.Workflow(Checker2).ApproveAsync(new ApproveProductAbbreviationAllocationCommand(
                correction.Id, correction.Version, Key(), ExpectedFormerVersion: active.Version), default));

        Assert.Equal(1, results.Count(x => x.IsSuccessful));
        await AssertCorrectedOnceAsync(scope, active, correction);
    }

    [Fact]
    public async Task An_approval_racing_a_cancel_never_leaves_the_product_without_its_code()
    {
        for (var round = 0; round < 5; round++)
        {
            await using var scope = await Scope.CreateAsync(mongo.ReplicaConnectionString);
            var (active, correction) = await ActiveWithCorrectionAsync(scope, $"RA{(char)('A' + round)}", $"RB{(char)('A' + round)}");

            await Task.WhenAll(
                scope.Workflow(Checker).ApproveAsync(new ApproveProductAbbreviationAllocationCommand(
                    correction.Id, correction.Version, Key(), ExpectedFormerVersion: active.Version), default),
                scope.Workflow(Maker2).CancelAsync(new CancelProductAbbreviationAllocationCommand(
                    correction.Id, correction.Version, Key()), default));

            var former = (await scope.Register.GetByIdAsync(active.Id))!;
            var replacement = (await scope.Register.GetByIdAsync(correction.Id))!;
            Assert.True(
                former.LifecycleStatus == ProductAbbreviationLifecycleStatus.ACTIVE
                    && replacement.LifecycleStatus == ProductAbbreviationLifecycleStatus.CANCELLED
                || former.LifecycleStatus == ProductAbbreviationLifecycleStatus.RETIRED
                    && replacement.LifecycleStatus == ProductAbbreviationLifecycleStatus.ACTIVE,
                $"former {former.LifecycleStatus}, replacement {replacement.LifecycleStatus}");
            Assert.Single(await scope.Register.GetByGlobalProductIdAsync(active.GlobalProductId),
                x => x.LifecycleStatus == ProductAbbreviationLifecycleStatus.ACTIVE);
        }
    }

    [Fact]
    public async Task A_correction_approval_writes_both_history_rows_and_both_audit_intents_and_replays_cleanly()
    {
        await using var scope = await Scope.CreateAsync(mongo.ReplicaConnectionString);
        var (active, correction) = await ActiveWithCorrectionAsync(scope, "HSA", "HSB");
        var key = Key();
        var command = new ApproveProductAbbreviationAllocationCommand(
            correction.Id, correction.Version, key, ExpectedFormerVersion: active.Version, Reason: "typo");

        var approved = await scope.Workflow(Checker).ApproveAsync(command, default);
        var replay = await scope.Workflow(Checker).ApproveAsync(command, default);

        Assert.True(approved.IsSuccessful);
        Assert.True(replay.IsSuccessful);
        await AssertCorrectedOnceAsync(scope, active, correction);
        var former = (await scope.Register.GetByIdAsync(active.Id))!;
        var replacement = (await scope.Register.GetByIdAsync(correction.Id))!;
        var formerRow = Assert.Single(await scope.History.GetForRegisterEntryAsync(former.Id),
            x => x.EventType == ProductAbbreviationHistoryEventType.CORRECTION_APPROVED);
        var replacementRow = Assert.Single(await scope.History.GetForRegisterEntryAsync(replacement.Id),
            x => x.EventType == ProductAbbreviationHistoryEventType.CORRECTION_APPROVED);
        Assert.Equal(key + ":former", formerRow.IdempotencyKey);
        Assert.Equal(former.Version, formerRow.PostVersion);
        Assert.Equal(key, replacementRow.IdempotencyKey);
        Assert.Equal(replacement.Version, replacementRow.PostVersion);
        Assert.Single(former.AuditIntents, x => x.Operation == ProductAuditOperation.ProductAbbreviationCorrectionApproved
                                                && x.PostVersion == former.Version);
        Assert.Single(replacement.AuditIntents, x => x.Operation == ProductAuditOperation.ProductAbbreviationCorrectionApproved
                                                     && x.PostVersion == replacement.Version);
    }

    [Fact]
    public async Task A_correction_approval_whose_evidence_cannot_be_written_changes_nothing()
    {
        // FIX1 B1(b) — atomicity, measured deterministically: the replacement's history row is already taken (its
        // deterministic id), so the transaction's evidence write fails AFTER the old code was retired inside it.
        // Nothing may survive: the old code stays active, the correction stays requested.
        await using var scope = await Scope.CreateAsync(mongo.ReplicaConnectionString);
        var (active, correction) = await ActiveWithCorrectionAsync(scope, "ATA", "ATB");
        var key = Key();
        Assert.True(await scope.History.AppendIfAbsentAsync(new ProductAbbreviationHistoryEntry
        {
            Id = DeterministicGuid($"{scope.Tenant:N}|history|{correction.Id:N}|{ProductAbbreviationHistoryEventType.CORRECTION_APPROVED}|{key}"),
            RegisterEntryId = Guid.NewGuid(),
            GlobalProductId = Guid.NewGuid(),
            NormalizedAbbreviation = "XXX",
            EventType = ProductAbbreviationHistoryEventType.ALLOCATION_REQUESTED,
            CanonicalHumanSubjectId = Maker,
            ActorType = "tenant_user",
            IdempotencyKey = Key(),
            CorrelationId = "blocker",
            EvidenceHash = "blocker",
            OccurredAtUtc = DateTimeOffset.UtcNow
        }));

        var approved = await scope.Workflow(Checker).ApproveAsync(new ApproveProductAbbreviationAllocationCommand(
            correction.Id, correction.Version, key, ExpectedFormerVersion: active.Version), default);

        Assert.False(approved.IsSuccessful);
        await AssertUntouchedAsync(scope, active);
        var stored = (await scope.Register.GetByIdAsync(correction.Id))!;
        Assert.Equal(ProductAbbreviationLifecycleStatus.REQUESTED, stored.LifecycleStatus);
        Assert.Equal(correction.Version, stored.Version);
    }

    [Fact]
    public async Task A_correction_approval_interrupted_after_the_old_code_retired_is_finished_by_the_same_key()
    {
        // The state the pre-FIX1 sequence could leave behind: the former entry retired under the approval's key, the
        // replacement still requested. Repeating the approval with that key finishes it instead of refusing it.
        await using var scope = await Scope.CreateAsync(mongo.ReplicaConnectionString);
        var (active, correction) = await ActiveWithCorrectionAsync(scope, "INA", "INB");
        var key = Key();
        var retired = await scope.Register.TransitionAsync(
            active.Id, active.Version, ProductAbbreviationLifecycleStatus.ACTIVE, ProductAbbreviationLifecycleStatus.RETIRED,
            Checker, key + ":former", null, DateTimeOffset.UtcNow);
        Assert.True(retired.Succeeded);

        var finished = await scope.Workflow(Checker).ApproveAsync(new ApproveProductAbbreviationAllocationCommand(
            correction.Id, correction.Version, key, ExpectedFormerVersion: retired.Entry!.Version), default);

        Assert.True(finished.IsSuccessful);
        Assert.Equal(ProductAbbreviationLifecycleStatus.ACTIVE, (await scope.Register.GetByIdAsync(correction.Id))!.LifecycleStatus);
        Assert.Single(await scope.History.GetForRegisterEntryAsync(active.Id),
            x => x.EventType == ProductAbbreviationHistoryEventType.CORRECTION_APPROVED);
        Assert.Single(await scope.History.GetForRegisterEntryAsync(correction.Id),
            x => x.EventType == ProductAbbreviationHistoryEventType.CORRECTION_APPROVED);
    }

    [Fact]
    public async Task A_correction_approval_without_the_former_version_is_refused_and_changes_nothing()
    {
        await using var scope = await Scope.CreateAsync(mongo.ReplicaConnectionString);
        var (active, correction) = await ActiveWithCorrectionAsync(scope, "FVA", "FVB");

        var approved = await scope.Workflow(Checker).ApproveAsync(
            new ApproveProductAbbreviationAllocationCommand(correction.Id, correction.Version, Key()), default);

        Assert.False(approved.IsSuccessful);
        Assert.Equal("ABBREVIATION_FORMER_EXPECTED_VERSION_REQUIRED", Assert.Single(approved.Errors));
        await AssertUntouchedAsync(scope, active);
    }

    // ── B2 / S2: the page sees every entry; a product with a code gets no second request ────────────────────────

    [Fact]
    public async Task The_product_list_shows_every_entry_with_its_status_and_who_asked()
    {
        await using var scope = await Scope.CreateAsync(mongo.ReplicaConnectionString);
        var product = Guid.NewGuid();
        var refused = await RequestAsync(scope, Maker, product, "LRA");
        Assert.True((await scope.Workflow(Checker).RejectAsync(
            new RejectProductAbbreviationAllocationCommand(refused.Id, refused.Version, Key(), "no"), default)).IsSuccessful);
        var active = await ApproveAsync(scope, await RequestAsync(scope, Maker, product, "LRB"));
        var correction = await CorrectAsync(scope, Maker2, active, "LRC");

        var list = await scope.Workflow(Maker2).ListByGlobalProductAsync(product, default);

        Assert.True(list.IsSuccessful);
        Assert.Equal(3, list.Data!.Count);
        var rows = list.Data.ToDictionary(x => x.Id);
        Assert.Equal(ProductAbbreviationLifecycleStatus.REJECTED, rows[refused.Id].LifecycleStatus);
        Assert.Equal(ProductAbbreviationLifecycleStatus.ACTIVE, rows[active.Id].LifecycleStatus);
        Assert.True(rows[active.Id].CorrectionPending);
        Assert.False(rows[active.Id].RequestedByCurrentUser);
        Assert.Equal(ProductAbbreviationLifecycleStatus.REQUESTED, rows[correction.Id].LifecycleStatus);
        Assert.True(rows[correction.Id].RequestedByCurrentUser);
        Assert.Equal(active.Id, rows[correction.Id].ReplacesEntryId);
    }

    [Fact]
    public async Task A_request_for_a_product_with_a_pending_or_active_code_is_refused_before_the_code_is_consumed()
    {
        await using var scope = await Scope.CreateAsync(mongo.ReplicaConnectionString);
        var product = Guid.NewGuid();
        var pending = await RequestAsync(scope, Maker, product, "PDA");

        var whilePending = await scope.Workflow(Maker2).RequestAsync(
            new RequestProductAbbreviationAllocationCommand(product, "PDB", Key()), default);
        await ApproveAsync(scope, pending);
        var whileActive = await scope.Workflow(Maker2).RequestAsync(
            new RequestProductAbbreviationAllocationCommand(product, "PDC", Key()), default);

        Assert.Equal("ABBREVIATION_PRODUCT_ALREADY_HAS_ENTRY", Assert.Single(whilePending.Errors));
        Assert.Equal(409, whilePending.StatusCode);
        Assert.Equal("ABBREVIATION_PRODUCT_ALREADY_HAS_ENTRY", Assert.Single(whileActive.Errors));
        // Neither code was consumed: another product can still take both.
        Assert.True((await RequestResultAsync(scope, Maker, Guid.NewGuid(), "PDB")).IsSuccessful);
        Assert.True((await RequestResultAsync(scope, Maker, Guid.NewGuid(), "PDC")).IsSuccessful);
    }

    [Fact]
    public async Task A_request_for_a_product_that_does_not_exist_is_refused_and_consumes_no_code()
    {
        await using var scope = await Scope.CreateAsync(mongo.ReplicaConnectionString);

        var result = await scope.Workflow(Maker, productsExist: false).RequestAsync(
            new RequestProductAbbreviationAllocationCommand(Guid.NewGuid(), "NXP", Key()), default);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("GLOBAL_PRODUCT_NOT_FOUND", Assert.Single(result.Errors));
        Assert.True((await RequestResultAsync(scope, Maker, Guid.NewGuid(), "NXP")).IsSuccessful);
    }

    [Fact]
    public async Task Sending_a_rejected_request_again_is_refused_as_closed_not_reported_as_success()
    {
        await using var scope = await Scope.CreateAsync(mongo.ReplicaConnectionString);
        var product = Guid.NewGuid();
        var key = Key();
        var command = new RequestProductAbbreviationAllocationCommand(product, "CLS", key);
        var requested = await scope.Workflow(Maker).RequestAsync(command, default);
        Assert.True((await scope.Workflow(Checker).RejectAsync(new RejectProductAbbreviationAllocationCommand(
            requested.Data!.Entry.Id, requested.Data.Entry.Version, Key(), "no"), default)).IsSuccessful);

        var again = await scope.Workflow(Maker).RequestAsync(command, default);

        Assert.False(again.IsSuccessful);
        Assert.Equal(409, again.StatusCode);
        Assert.Equal("ABBREVIATION_REQUEST_ALREADY_CLOSED", Assert.Single(again.Errors));
    }

    // ── B3: one pending change per code ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_second_correction_while_one_waits_is_refused()
    {
        await using var scope = await Scope.CreateAsync(mongo.ReplicaConnectionString);
        var (active, _) = await ActiveWithCorrectionAsync(scope, "SCA", "SCB");

        var second = await scope.Workflow(Maker).InitiateCorrectionAsync(new InitiateProductAbbreviationCorrectionCommand(
            active.Id, active.Version, "SCC", Key(), "again"), default);

        Assert.Equal("ABBREVIATION_CORRECTION_PENDING", Assert.Single(second.Errors));
        Assert.Equal(409, second.StatusCode);
    }

    [Fact]
    public async Task A_retirement_request_while_a_correction_waits_is_refused()
    {
        await using var scope = await Scope.CreateAsync(mongo.ReplicaConnectionString);
        var (active, _) = await ActiveWithCorrectionAsync(scope, "RCA", "RCB");

        var retire = await scope.Workflow(Maker).RequestRetirementAsync(
            new RequestProductAbbreviationRetirementCommand(active.Id, active.Version, Key(), "retire"), default);

        Assert.Equal("ABBREVIATION_CORRECTION_PENDING", Assert.Single(retire.Errors));
        var stored = (await scope.Register.GetByIdAsync(active.Id))!;
        Assert.Null(stored.RetirementRequestId);
    }

    [Fact]
    public async Task A_correction_while_a_retirement_waits_is_refused()
    {
        await using var scope = await Scope.CreateAsync(mongo.ReplicaConnectionString);
        var active = await ApproveAsync(scope, await RequestAsync(scope, Maker, Guid.NewGuid(), "CRA"));
        var retire = await scope.Workflow(Maker).RequestRetirementAsync(
            new RequestProductAbbreviationRetirementCommand(active.Id, active.Version, Key(), "retire"), default);
        Assert.True(retire.IsSuccessful);

        var correction = await scope.Workflow(Maker2).InitiateCorrectionAsync(new InitiateProductAbbreviationCorrectionCommand(
            active.Id, retire.Data!.Version, "CRB", Key(), "fix"), default);

        Assert.Equal("ABBREVIATION_RETIREMENT_PENDING", Assert.Single(correction.Errors));
    }

    [Fact]
    public async Task A_claim_whose_correction_was_cancelled_blocks_neither_a_new_correction_nor_a_retirement()
    {
        await using var scope = await Scope.CreateAsync(mongo.ReplicaConnectionString);
        var (active, correction) = await ActiveWithCorrectionAsync(scope, "CLA", "CLB");
        Assert.True((await scope.Workflow(Maker2).CancelAsync(new CancelProductAbbreviationAllocationCommand(
            correction.Id, correction.Version, Key()), default)).IsSuccessful);

        var next = await scope.Workflow(Maker).InitiateCorrectionAsync(new InitiateProductAbbreviationCorrectionCommand(
            active.Id, active.Version, "CLC", Key(), "again"), default);
        Assert.True(next.IsSuccessful);
        Assert.True((await scope.Workflow(Maker).CancelAsync(new CancelProductAbbreviationAllocationCommand(
            next.Data!.Entry.Id, next.Data.Entry.Version, Key()), default)).IsSuccessful);
        var retire = await scope.Workflow(Maker).RequestRetirementAsync(
            new RequestProductAbbreviationRetirementCommand(active.Id, active.Version, Key(), "retire"), default);

        Assert.True(retire.IsSuccessful);
    }

    [Fact]
    public async Task An_orphaned_correction_can_still_be_rejected_and_cancelled()
    {
        // Built the way pre-FIX1 code could build it: a correction and a retirement of the same code, the retirement
        // approved first. The correction is an orphan; closing it must work (the cleanup path).
        await using var scope = await Scope.CreateAsync(mongo.ReplicaConnectionString);
        var active = await ApproveAsync(scope, await RequestAsync(scope, Maker, Guid.NewGuid(), "ORA"));
        var orphanA = await InsertOrphanCandidateAsync(scope, active, "ORB");
        var orphanB = await InsertOrphanCandidateAsync(scope, active, "ORC");
        var retire = await scope.Workflow(Maker).RequestRetirementAsync(
            new RequestProductAbbreviationRetirementCommand(active.Id, active.Version, Key(), "retire"), default);
        Assert.True((await scope.Workflow(Checker).ApproveRetirementAsync(new ApproveProductAbbreviationRetirementCommand(
            active.Id, retire.Data!.Version, (await scope.Register.GetByIdAsync(active.Id))!.RetirementRequestId!, Key()),
            default)).IsSuccessful);

        var rejected = await scope.Workflow(Checker).RejectAsync(
            new RejectProductAbbreviationAllocationCommand(orphanA.Id, orphanA.Version, Key(), "orphan"), default);
        var cancelled = await scope.Workflow(Maker2).CancelAsync(
            new CancelProductAbbreviationAllocationCommand(orphanB.Id, orphanB.Version, Key()), default);

        Assert.True(rejected.IsSuccessful);
        Assert.True(cancelled.IsSuccessful);
    }

    // ── S3: one key, one entry ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_key_used_for_one_entry_is_refused_for_another_and_changes_nothing()
    {
        await using var scope = await Scope.CreateAsync(mongo.ReplicaConnectionString);
        var x = await RequestAsync(scope, Maker, Guid.NewGuid(), "KYA");
        var y = await RequestAsync(scope, Maker, Guid.NewGuid(), "KYB");
        var key = Key();
        Assert.True((await scope.Workflow(Checker).ApproveAsync(
            new ApproveProductAbbreviationAllocationCommand(x.Id, x.Version, key), default)).IsSuccessful);

        var reused = await scope.Workflow(Checker).ApproveAsync(
            new ApproveProductAbbreviationAllocationCommand(y.Id, y.Version, key), default);

        Assert.False(reused.IsSuccessful);
        Assert.Equal(409, reused.StatusCode);
        Assert.Equal("ABBREVIATION_IDEMPOTENCY_KEY_REUSED", Assert.Single(reused.Errors));
        var storedY = (await scope.Register.GetByIdAsync(y.Id))!;
        Assert.Equal(ProductAbbreviationLifecycleStatus.REQUESTED, storedY.LifecycleStatus);
        Assert.DoesNotContain(await scope.History.GetForRegisterEntryAsync(y.Id),
            row => row.EventType == ProductAbbreviationHistoryEventType.ALLOCATION_APPROVED);
    }

    // ── S1: a 202 is finished by sending the same command again ─────────────────────────────────────────────

    [Fact]
    public async Task A_decision_whose_history_write_failed_is_finished_by_repeating_it_with_the_same_key()
    {
        await using var scope = await Scope.CreateAsync(mongo.ReplicaConnectionString);
        var entry = await RequestAsync(scope, Maker, Guid.NewGuid(), "HFA");
        var failOnce = 1;
        var history = Decorate<IProductAbbreviationHistoryRepository>(scope.History, (method, _) =>
            method.Name == nameof(IProductAbbreviationHistoryRepository.AppendIfAbsentAsync) && failOnce-- > 0
                ? Task.FromResult(false)
                : null);
        var command = new ApproveProductAbbreviationAllocationCommand(entry.Id, entry.Version, Key());

        var first = await scope.Workflow(Checker, history: history).ApproveAsync(command, default);
        var again = await scope.Workflow(Checker).ApproveAsync(command, default);

        Assert.Equal(202, first.StatusCode);
        Assert.Equal("ABBREVIATION_EVIDENCE_RECONCILIATION_REQUIRED", Assert.Single(first.Errors));
        Assert.True(again.IsSuccessful);
        var stored = (await scope.Register.GetByIdAsync(entry.Id))!;
        Assert.Single(await scope.History.GetForRegisterEntryAsync(entry.Id),
            row => row.EventType == ProductAbbreviationHistoryEventType.ALLOCATION_APPROVED);
        Assert.Single(stored.AuditIntents, x => x.Operation == ProductAuditOperation.ProductAbbreviationAllocationApproved);
    }

    [Fact]
    public async Task A_request_whose_register_write_failed_is_finished_by_repeating_it_with_the_same_key()
    {
        await using var scope = await Scope.CreateAsync(mongo.ReplicaConnectionString);
        var failOnce = 1;
        var register = Decorate<IProductAbbreviationRegisterRepository>(scope.Register, (method, _) =>
            method.Name == nameof(IProductAbbreviationRegisterRepository.InsertRequestedAsync) && failOnce-- > 0
                ? Task.FromResult(new ProductAbbreviationRegisterWriteResult(false, null, "TRANSIENT"))
                : null);
        var command = new RequestProductAbbreviationAllocationCommand(Guid.NewGuid(), "RFA", Key());

        var first = await scope.Workflow(Maker, register: register).RequestAsync(command, default);
        var again = await scope.Workflow(Maker).RequestAsync(command, default);

        Assert.Equal(202, first.StatusCode);
        Assert.Equal("ABBREVIATION_REGISTER_RECONCILIATION_REQUIRED", Assert.Single(first.Errors));
        Assert.True(again.IsSuccessful);
        Assert.Equal(ProductAbbreviationLifecycleStatus.REQUESTED, again.Data!.Entry.LifecycleStatus);
        Assert.Single(await scope.History.GetForRegisterEntryAsync(again.Data.Entry.Id),
            row => row.EventType == ProductAbbreviationHistoryEventType.ALLOCATION_REQUESTED);
    }

    [Fact]
    public async Task An_audit_intent_for_a_version_the_entry_has_moved_past_is_still_appended()
    {
        await using var scope = await Scope.CreateAsync(mongo.ReplicaConnectionString);
        var active = await ApproveAsync(scope, await RequestAsync(scope, Maker, Guid.NewGuid(), "LTA"));
        var moved = await scope.Workflow(Maker).RequestRetirementAsync(
            new RequestProductAbbreviationRetirementCommand(active.Id, active.Version, Key(), "retire"), default);
        Assert.True(moved.IsSuccessful);
        var entry = (await scope.Register.GetByIdAsync(active.Id))!;
        var missed = new ProductAbbreviationHistoryEntry
        {
            Id = Guid.NewGuid(),
            TenantId = scope.Tenant,
            RegisterEntryId = entry.Id,
            GlobalProductId = entry.GlobalProductId,
            NormalizedAbbreviation = entry.NormalizedAbbreviation,
            EventType = ProductAbbreviationHistoryEventType.ALLOCATION_APPROVED,
            BeforeStatus = ProductAbbreviationLifecycleStatus.REQUESTED,
            AfterStatus = ProductAbbreviationLifecycleStatus.ACTIVE,
            CanonicalHumanSubjectId = Checker,
            ActorType = "tenant_user",
            IdempotencyKey = Key(),
            CorrelationId = Guid.NewGuid().ToString("D"),
            EvidenceHash = "late",
            OccurredAtUtc = DateTimeOffset.UtcNow,
            PostVersion = active.Version
        };
        var intent = ProductAbbreviationAuditIntentFactory.Create(entry, missed);

        Assert.True(entry.Version > active.Version);
        Assert.Equal(active.Version, intent.PostVersion);
        Assert.True(await scope.Register.AppendAuditIntentIfAbsentAsync(entry.Id, intent));
    }

    // ── S10: evidence in the order it happened ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task History_comes_back_in_the_order_it_happened_whatever_the_ids_and_insertion_order()
    {
        await using var scope = await Scope.CreateAsync(mongo.ReplicaConnectionString);
        var entryId = Guid.NewGuid();
        var earlier = new DateTimeOffset(2026, 10, 3, 9, 0, 0, TimeSpan.Zero);
        // Inserted later-first, and the later row carries the SMALLER id: neither insertion nor id order is time order.
        foreach (var (id, at, type) in new[]
                 {
                     (Guid.Parse("00000000-0000-0000-0000-000000000001"), earlier.AddMinutes(5), ProductAbbreviationHistoryEventType.ALLOCATION_APPROVED),
                     (Guid.Parse("ffffffff-ffff-ffff-ffff-fffffffffff1"), earlier, ProductAbbreviationHistoryEventType.ALLOCATION_REQUESTED)
                 })
        {
            Assert.True(await scope.History.AppendIfAbsentAsync(new ProductAbbreviationHistoryEntry
            {
                Id = id,
                RegisterEntryId = entryId,
                GlobalProductId = Guid.NewGuid(),
                NormalizedAbbreviation = "ORD",
                EventType = type,
                CanonicalHumanSubjectId = Maker,
                ActorType = "tenant_user",
                IdempotencyKey = Key(),
                CorrelationId = "order",
                EvidenceHash = "order",
                OccurredAtUtc = at
            }));
        }

        var rows = await scope.History.GetForRegisterEntryAsync(entryId);

        Assert.Equal(
            [ProductAbbreviationHistoryEventType.ALLOCATION_REQUESTED, ProductAbbreviationHistoryEventType.ALLOCATION_APPROVED],
            rows.Select(x => x.EventType).ToArray());
    }

    // ── Rules that had no test (FIX1 list) ──────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_retirement_maker_checker_is_the_retirement_requester_not_the_original_requester(bool approve)
    {
        // Maker requested the code, Checker approved it, Maker2 asked to retire it. Maker — the ORIGINAL requester —
        // may decide the retirement; Maker2 may not.
        await using var scope = await Scope.CreateAsync(mongo.ReplicaConnectionString);
        var active = await ApproveAsync(scope, await RequestAsync(scope, Maker, Guid.NewGuid(), approve ? "MCA" : "MCB"));
        var retire = await scope.Workflow(Maker2).RequestRetirementAsync(
            new RequestProductAbbreviationRetirementCommand(active.Id, active.Version, Key(), "retire"), default);
        var requestId = (await scope.Register.GetByIdAsync(active.Id))!.RetirementRequestId!;

        async Task<Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>> Decide(string subject)
            => approve
                ? await scope.Workflow(subject).ApproveRetirementAsync(new ApproveProductAbbreviationRetirementCommand(
                    active.Id, retire.Data!.Version, requestId, Key()), default)
                : await scope.Workflow(subject).RejectRetirementAsync(new RejectProductAbbreviationRetirementCommand(
                    active.Id, retire.Data!.Version, requestId, Key(), "keep"), default);

        var byRequester = await Decide(Maker2);
        var byOriginalMaker = await Decide(Maker);

        Assert.Equal("ABBREVIATION_MAKER_CHECKER_VIOLATION", Assert.Single(byRequester.Errors));
        Assert.True(byOriginalMaker.IsSuccessful, string.Join(",", byOriginalMaker.Errors));
    }

    [Fact]
    public async Task A_retirement_decision_for_another_request_id_is_refused_and_changes_nothing()
    {
        await using var scope = await Scope.CreateAsync(mongo.ReplicaConnectionString);
        var active = await ApproveAsync(scope, await RequestAsync(scope, Maker, Guid.NewGuid(), "RIA"));
        var retire = await scope.Workflow(Maker).RequestRetirementAsync(
            new RequestProductAbbreviationRetirementCommand(active.Id, active.Version, Key(), "retire"), default);

        var approved = await scope.Workflow(Checker).ApproveRetirementAsync(new ApproveProductAbbreviationRetirementCommand(
            active.Id, retire.Data!.Version, "some-other-request", Key()), default);

        Assert.Equal("ABBREVIATION_RETIREMENT_REQUEST_NOT_FOUND", Assert.Single(approved.Errors));
        Assert.Equal(ProductAbbreviationLifecycleStatus.ACTIVE, (await scope.Register.GetByIdAsync(active.Id))!.LifecycleStatus);
    }

    [Fact]
    public async Task A_correction_to_the_same_value_is_refused_as_unchanged()
    {
        await using var scope = await Scope.CreateAsync(mongo.ReplicaConnectionString);
        var active = await ApproveAsync(scope, await RequestAsync(scope, Maker, Guid.NewGuid(), "SVA"));

        var same = await scope.Workflow(Maker).InitiateCorrectionAsync(new InitiateProductAbbreviationCorrectionCommand(
            active.Id, active.Version, "sva", Key(), "same"), default);

        Assert.Equal("ABBREVIATION_CORRECTION_VALUE_UNCHANGED", Assert.Single(same.Errors));
    }

    [Fact]
    public async Task A_second_retirement_request_does_not_replace_the_pending_one()
    {
        await using var scope = await Scope.CreateAsync(mongo.ReplicaConnectionString);
        var active = await ApproveAsync(scope, await RequestAsync(scope, Maker, Guid.NewGuid(), "SRA"));
        var first = await scope.Register.RequestRetirementAsync(
            active.Id, active.Version, "retirement-1", Maker, "retire-1", "r", DateTimeOffset.UtcNow);
        Assert.True(first.Succeeded);

        var second = await scope.Register.RequestRetirementAsync(
            active.Id, first.Entry!.Version, "retirement-2", Maker2, "retire-2", "r", DateTimeOffset.UtcNow);

        Assert.False(second.Succeeded);
        Assert.Equal("CONCURRENCY_CONFLICT", second.ErrorCode);
        Assert.Equal("retirement-1", (await scope.Register.GetByIdAsync(active.Id))!.RetirementRequestId);
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>A repository-level approval with evidence built the way the workflow builds it.</summary>
    internal static ProductAbbreviationCorrectionApproval Approval(
        Guid formerId,
        int expectedFormer,
        Guid replacementId,
        int expectedReplacement,
        string key)
    {
        var checker = Guid.Parse("4b8f2d8e-6f0c-4b8a-9a1e-0d6f3a9c2b11").ToString("D");
        var at = new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);
        ProductAbbreviationHistoryEntry Row(ProductAbbreviationRegisterEntry entry, string rowKey,
            ProductAbbreviationLifecycleStatus before, ProductAbbreviationLifecycleStatus after) => new()
        {
            Id = DeterministicGuid($"{entry.TenantId:N}|history|{entry.Id:N}|CORRECTION_APPROVED|{rowKey}"),
            TenantId = entry.TenantId,
            RegisterEntryId = entry.Id,
            GlobalProductId = entry.GlobalProductId,
            NormalizedAbbreviation = entry.NormalizedAbbreviation,
            EventType = ProductAbbreviationHistoryEventType.CORRECTION_APPROVED,
            BeforeStatus = before,
            AfterStatus = after,
            CanonicalHumanSubjectId = checker,
            ActorType = "tenant_user",
            IdempotencyKey = rowKey,
            CorrelationId = rowKey,
            EvidenceHash = rowKey,
            OccurredAtUtc = at,
            PostVersion = entry.Version
        };
        return new(formerId, expectedFormer, replacementId, expectedReplacement, checker, key, "reason", at,
            (former, replacement) =>
            {
                var formerRow = Row(former, key + ":former", ProductAbbreviationLifecycleStatus.ACTIVE,
                    ProductAbbreviationLifecycleStatus.RETIRED);
                var replacementRow = Row(replacement, key, ProductAbbreviationLifecycleStatus.REQUESTED,
                    ProductAbbreviationLifecycleStatus.ACTIVE);
                return new(formerRow, ProductAbbreviationAuditIntentFactory.Create(former, formerRow),
                    replacementRow, ProductAbbreviationAuditIntentFactory.Create(replacement, replacementRow));
            });
    }

    private static async Task AssertUntouchedAsync(Scope scope, ProductAbbreviationRegisterEntry active)
    {
        var stored = (await scope.Register.GetByIdAsync(active.Id))!;
        Assert.Equal(ProductAbbreviationLifecycleStatus.ACTIVE, stored.LifecycleStatus);
        Assert.Equal(active.Version, stored.Version);
        Assert.DoesNotContain(await scope.History.GetForRegisterEntryAsync(active.Id),
            row => row.EventType == ProductAbbreviationHistoryEventType.CORRECTION_APPROVED);
    }

    private static async Task AssertCorrectedOnceAsync(
        Scope scope,
        ProductAbbreviationRegisterEntry active,
        ProductAbbreviationRegisterEntry correction)
    {
        Assert.Equal(ProductAbbreviationLifecycleStatus.RETIRED, (await scope.Register.GetByIdAsync(active.Id))!.LifecycleStatus);
        Assert.Equal(ProductAbbreviationLifecycleStatus.ACTIVE, (await scope.Register.GetByIdAsync(correction.Id))!.LifecycleStatus);
        Assert.Single(await scope.History.GetForRegisterEntryAsync(active.Id),
            row => row.EventType == ProductAbbreviationHistoryEventType.CORRECTION_APPROVED);
        Assert.Single(await scope.Register.GetByGlobalProductIdAsync(active.GlobalProductId),
            x => x.LifecycleStatus == ProductAbbreviationLifecycleStatus.ACTIVE);
    }

    private static async Task<(ProductAbbreviationRegisterEntry Active, ProductAbbreviationRegisterEntry Correction)>
        ActiveWithCorrectionAsync(Scope scope, string current, string replacement)
    {
        var active = await ApproveAsync(scope, await RequestAsync(scope, Maker, Guid.NewGuid(), current));
        var correction = await CorrectAsync(scope, Maker2, active, replacement);
        return ((await scope.Register.GetByIdAsync(active.Id))!, correction);
    }

    private static async Task<ProductAbbreviationRegisterEntry> RequestAsync(
        Scope scope, string maker, Guid product, string abbreviation)
    {
        var result = await RequestResultAsync(scope, maker, product, abbreviation);
        Assert.True(result.IsSuccessful, string.Join(",", result.Errors));
        return (await scope.Register.GetByIdAsync(result.Data!.Entry.Id))!;
    }

    private static Task<Response<ProductAbbreviationRegisterModels.ProductAbbreviationAllocationResultDto>> RequestResultAsync(
        Scope scope, string maker, Guid product, string abbreviation)
        => scope.Workflow(maker).RequestAsync(new RequestProductAbbreviationAllocationCommand(product, abbreviation, Key()), default);

    private static async Task<ProductAbbreviationRegisterEntry> ApproveAsync(Scope scope, ProductAbbreviationRegisterEntry entry)
    {
        var result = await scope.Workflow(Checker).ApproveAsync(
            new ApproveProductAbbreviationAllocationCommand(entry.Id, entry.Version, Key()), default);
        Assert.True(result.IsSuccessful, string.Join(",", result.Errors));
        return (await scope.Register.GetByIdAsync(entry.Id))!;
    }

    private static async Task<ProductAbbreviationRegisterEntry> CorrectAsync(
        Scope scope, string maker, ProductAbbreviationRegisterEntry active, string replacement)
    {
        var result = await scope.Workflow(maker).InitiateCorrectionAsync(new InitiateProductAbbreviationCorrectionCommand(
            active.Id, active.Version, replacement, Key(), "fix"), default);
        Assert.True(result.IsSuccessful, string.Join(",", result.Errors));
        return (await scope.Register.GetByIdAsync(result.Data!.Entry.Id))!;
    }

    /// <summary>A REQUESTED correction written straight to the register, bypassing FIX1's one-pending-change claim.</summary>
    private static async Task<ProductAbbreviationRegisterEntry> InsertOrphanCandidateAsync(
        Scope scope, ProductAbbreviationRegisterEntry active, string abbreviation)
    {
        var key = Key();
        var entryId = Guid.NewGuid();
        var ledger = await scope.Ledger.AllocateAsync(new ProductAbbreviationAllocationLedger
        {
            Id = Guid.NewGuid(),
            NormalizedAbbreviation = abbreviation,
            GlobalProductId = active.GlobalProductId,
            RegisterEntryId = entryId,
            IdempotencyKey = key,
            PayloadHash = key,
            AllocatedByCanonicalSubjectId = Maker2,
            AllocatedAtUtc = DateTimeOffset.UtcNow
        });
        Assert.True(ledger.Succeeded);
        var inserted = await scope.Register.InsertRequestedAsync(new ProductAbbreviationRegisterEntry
        {
            Id = entryId,
            GlobalProductId = active.GlobalProductId,
            NormalizedAbbreviation = abbreviation,
            AllocationLedgerId = ledger.Ledger!.Id,
            AllocationIdempotencyKey = key,
            RequestedByCanonicalSubjectId = Maker2,
            RequestedAtUtc = DateTimeOffset.UtcNow,
            ReplacesEntryId = active.Id
        });
        Assert.True(inserted.Succeeded);
        return inserted.Entry!;
    }

    private static string Key() => "fix1-" + Guid.NewGuid().ToString("N");

    private static Guid DeterministicGuid(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        Span<byte> guidBytes = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guidBytes);
        guidBytes[7] = (byte)((guidBytes[7] & 0x0F) | 0x50);
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);
        return new Guid(guidBytes);
    }

    /// <summary>Calls <paramref name="inner"/> unless <paramref name="intercept"/> answers first (non-null).</summary>
    internal static T Decorate<T>(T inner, Func<MethodInfo, object?[]?, object?> intercept) where T : class
    {
        var proxy = DispatchProxy.Create<T, Decorator>();
        var decorator = (Decorator)(object)proxy;
        decorator.Inner = inner;
        decorator.Intercept = intercept;
        return proxy;
    }

    public class Decorator : DispatchProxy
    {
        public object Inner { get; set; } = null!;
        public Func<MethodInfo, object?[]?, object?> Intercept { get; set; } = null!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => Intercept(targetMethod!, args) ?? targetMethod!.Invoke(Inner, args);
    }

    public class ProductsProxy : DispatchProxy
    {
        public bool Exist { get; set; } = true;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod!.Name switch
        {
            nameof(IGlobalProductRepository.GetByIdAsync) => Task.FromResult<GlobalProduct?>(
                Exist ? new GlobalProduct { Id = (Guid)args![0]!, CanonicalCode = "GP-FIX1", GlobalProductName = "Fix1" } : null),
            _ => throw new InvalidOperationException($"Unexpected product repository call: {targetMethod.Name}")
        };
    }

    private sealed record Actor(Guid Tenant, string Subject) : IProductAbbreviationActorContext
    {
        public Guid TenantId => Tenant;
        public bool TenantIsResolved => true;
        public bool IsAuthenticated => true;
        public string ActorType => "tenant_user";
        public string CanonicalHumanSubjectId => Subject;
        public IReadOnlySet<string> GrantedPermissions => ProductAbbreviationPermissions.All;
        public string CorrelationId => Guid.NewGuid().ToString("D");
    }

    private sealed class Scope : IAsyncDisposable
    {
        private Scope(IMongoDatabase database) => Database = database;

        public Guid Tenant { get; } = Guid.NewGuid();
        public IMongoDatabase Database { get; }
        public ProductAbbreviationRegisterRepository Register => new(Database, Context());
        public ProductAbbreviationHistoryRepository History => new(Database, Context());
        public ProductAbbreviationAllocationLedgerRepository Ledger => new(Database, Context());

        public static async Task<Scope> CreateAsync(string connectionString)
        {
            var settings = MongoClientSettings.FromConnectionString(connectionString);
            settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
            settings.ConnectTimeout = TimeSpan.FromSeconds(5);
#pragma warning disable CS0618
            settings.GuidRepresentation = MongoDB.Bson.GuidRepresentation.Standard;
#pragma warning restore CS0618
            var database = new MongoClient(settings).GetDatabase("diten_mdm_abb_register_itest");
            await database.RunCommandAsync<MongoDB.Bson.BsonDocument>(new MongoDB.Bson.BsonDocument("ping", 1));
            return new Scope(database);
        }

        public ProductAbbreviationWorkflow Workflow(
            string subject,
            bool productsExist = true,
            IProductAbbreviationHistoryRepository? history = null,
            IProductAbbreviationRegisterRepository? register = null)
        {
            var actor = new Actor(Tenant, subject);
            var products = DispatchProxy.Create<IGlobalProductRepository, ProductsProxy>();
            ((ProductsProxy)(object)products).Exist = productsExist;
            return new ProductAbbreviationWorkflow(
                register ?? Register,
                Ledger,
                history ?? History,
                products,
                actor,
                new ProductAbbreviationAuthorization(actor));
        }

        private TenantContext Context()
        {
            var context = new TenantContext();
            context.SetTenant(Tenant);
            return context;
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var name in new[]
                     {
                         "mdm_product_abbreviation_register", "mdm_product_abbreviation_history",
                         "mdm_product_abbreviation_allocation_ledger", "mdm_product_abbreviation_command_keys"
                     })
            {
                await Database.GetCollection<MongoDB.Bson.BsonDocument>(name).DeleteManyAsync(
                    Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("TenantId", Tenant));
            }
        }
    }
}
