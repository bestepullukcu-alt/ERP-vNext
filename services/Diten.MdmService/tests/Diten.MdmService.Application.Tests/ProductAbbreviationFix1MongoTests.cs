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
    public async Task A_code_retired_under_a_key_ending_in_former_is_not_finished_by_that_key()
    {
        // FIX2 A5 — the FIX1 path that "finished" such a state recognised it only by the ':former' suffix of a key a
        // client could choose. It is gone: a correction whose code is no longer ACTIVE is refused.
        await using var scope = await Scope.CreateAsync(mongo.ReplicaConnectionString);
        var (active, correction) = await ActiveWithCorrectionAsync(scope, "INA", "INB");
        var key = Key();
        // The half-done state a pre-FIX2 crash could leave: the code retired under "<key>:former", its correction still
        // requested. No production path writes it any more (FIX3 item 8), so it is written straight to the collection.
        await scope.Database.GetCollection<ProductAbbreviationRegisterEntry>("mdm_product_abbreviation_register")
            .UpdateOneAsync(x => x.Id == active.Id, Builders<ProductAbbreviationRegisterEntry>.Update
                .Set(x => x.LifecycleStatus, ProductAbbreviationLifecycleStatus.RETIRED)
                .Set(x => x.LastDecisionByCanonicalSubjectId, Checker)
                .Set(x => x.LastDecisionIdempotencyKey, key + ":former")
                .Inc(x => x.Version, 1));

        var finished = await scope.Workflow(Checker).ApproveAsync(new ApproveProductAbbreviationAllocationCommand(
            correction.Id, correction.Version, key, ExpectedFormerVersion: active.Version + 1), default);

        Assert.Equal("ABBREVIATION_CORRECTION_FORMER_NOT_ACTIVE", Assert.Single(finished.Errors));
        Assert.Equal(ProductAbbreviationLifecycleStatus.REQUESTED, (await scope.Register.GetByIdAsync(correction.Id))!.LifecycleStatus);
    }

    [Theory]
    [InlineData("X:former")]
    [InlineData("X:replacement")]
    public async Task A_key_ending_in_a_suffix_the_server_derives_is_refused(string key)
    {
        await using var scope = await Scope.CreateAsync(mongo.ReplicaConnectionString);
        var entry = await RequestAsync(scope, Maker, Guid.NewGuid(), key.EndsWith("former") ? "SFA" : "SFB");

        var approved = await scope.Workflow(Checker).ApproveAsync(
            new ApproveProductAbbreviationAllocationCommand(entry.Id, entry.Version, key), default);

        Assert.Equal(400, approved.StatusCode);
        Assert.Equal("ABBREVIATION_IDEMPOTENCY_KEY_INVALID", Assert.Single(approved.Errors));
        Assert.Equal(ProductAbbreviationLifecycleStatus.REQUESTED, (await scope.Register.GetByIdAsync(entry.Id))!.LifecycleStatus);
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
    public async Task An_allocation_decision_whose_history_cannot_be_written_changes_nothing()
    {
        // FIX2 A1 — the decision and its history row are one transaction now: when the row cannot be written, the
        // decision is not written either (before FIX2 the entry became ACTIVE and answered 202 without evidence).
        await using var scope = await Scope.CreateAsync(mongo.ReplicaConnectionString);
        var entry = await RequestAsync(scope, Maker, Guid.NewGuid(), "HFA");
        var key = Key();
        Assert.True(await scope.History.AppendIfAbsentAsync(Blocker(
            DeterministicGuid($"{scope.Tenant:N}|history|{entry.Id:N}|{ProductAbbreviationHistoryEventType.ALLOCATION_APPROVED}|{key}"))));

        var approved = await scope.Workflow(Checker).ApproveAsync(
            new ApproveProductAbbreviationAllocationCommand(entry.Id, entry.Version, key), default);

        Assert.False(approved.IsSuccessful);
        var stored = (await scope.Register.GetByIdAsync(entry.Id))!;
        Assert.Equal(ProductAbbreviationLifecycleStatus.REQUESTED, stored.LifecycleStatus);
        Assert.Equal(entry.Version, stored.Version);
        Assert.DoesNotContain(stored.AuditIntents, x => x.Operation == ProductAuditOperation.ProductAbbreviationAllocationApproved);
    }


    [Fact]
    public async Task A_request_whose_register_write_failed_is_finished_by_repeating_it_with_the_same_key()
    {
        await using var scope = await Scope.CreateAsync(mongo.ReplicaConnectionString);
        var failOnce = 1;
        var register = Decorate<IProductAbbreviationRegisterRepository>(scope.Register, (method, _) =>
            method.Name == nameof(IProductAbbreviationRegisterRepository.RequestEntryAsync) && failOnce-- > 0
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
        var firstKey = Key();
        var first = await scope.Workflow(Maker).RequestRetirementAsync(
            new RequestProductAbbreviationRetirementCommand(active.Id, active.Version, firstKey, "r"), default);
        Assert.True(first.IsSuccessful, string.Join(",", first.Errors));

        var second = await scope.Workflow(Maker2).RequestRetirementAsync(
            new RequestProductAbbreviationRetirementCommand(active.Id, first.Data!.Version, Key(), "r"), default);

        Assert.False(second.IsSuccessful);
        Assert.Equal("CONCURRENCY_CONFLICT", Assert.Single(second.Errors));
        Assert.Equal(firstKey, (await scope.Register.GetByIdAsync(active.Id))!.RetirementRequestId);
    }

    // ══ FIX2 ═══════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task A_decision_under_the_same_key_by_another_approver_is_refused_and_stays_with_the_first()
    {
        // FIX2 A1 (b) — the Task Center's key carries no person: A and B on the same version send the same key.
        await using var scope = await Scope.CreateAsync(mongo.ReplicaConnectionString);
        var entry = await RequestAsync(scope, Maker, Guid.NewGuid(), "DBA");
        var key = Key();
        var command = new ApproveProductAbbreviationAllocationCommand(entry.Id, entry.Version, key);

        var first = await scope.Workflow(Checker).ApproveAsync(command, default);
        var second = await scope.Workflow(Checker2).ApproveAsync(command, default);
        var firstAgain = await scope.Workflow(Checker).ApproveAsync(command, default);

        Assert.True(first.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
        Assert.Equal("ABBREVIATION_ALREADY_DECIDED_BY_ANOTHER", Assert.Single(second.Errors));
        Assert.True(firstAgain.IsSuccessful);
        await AssertDecidedByAsync(scope, entry.Id, ProductAbbreviationHistoryEventType.ALLOCATION_APPROVED, Checker);
    }

    [Fact]
    public async Task A_correction_approval_under_the_same_key_by_another_approver_is_refused_not_reported_as_approved()
    {
        // FIX2 A1 (a) — before, B got 200 "approved" for A's decision, with no history row of B's own.
        await using var scope = await Scope.CreateAsync(mongo.ReplicaConnectionString);
        var (active, correction) = await ActiveWithCorrectionAsync(scope, "DCA", "DCB");
        var command = new ApproveProductAbbreviationAllocationCommand(
            correction.Id, correction.Version, Key(), ExpectedFormerVersion: active.Version);

        Assert.True((await scope.Workflow(Checker).ApproveAsync(command, default)).IsSuccessful);
        var second = await scope.Workflow(Checker2).ApproveAsync(command, default);

        Assert.Equal("ABBREVIATION_ALREADY_DECIDED_BY_ANOTHER", Assert.Single(second.Errors));
        Assert.Equal(Checker, (await scope.Register.GetByIdAsync(correction.Id))!.LastDecisionByCanonicalSubjectId);
    }

    [Fact]
    public async Task Two_approvers_clicking_together_leave_one_decision_whose_record_history_and_intent_name_the_same_person()
    {
        // FIX2 A1 (c) — the decision, its history row and its audit intent are one transaction: whoever wins, all three
        // say the same person (before, B's history row could land while the record said A).
        for (var round = 0; round < 5; round++)
        {
            await using var scope = await Scope.CreateAsync(mongo.ReplicaConnectionString);
            var entry = await RequestAsync(scope, Maker, Guid.NewGuid(), $"CC{(char)('A' + round)}");
            var command = new ApproveProductAbbreviationAllocationCommand(entry.Id, entry.Version, Key());

            var results = await Task.WhenAll(
                scope.Workflow(Checker).ApproveAsync(command, default),
                scope.Workflow(Checker2).ApproveAsync(command, default));

            Assert.Single(results, x => x.IsSuccessful);
            Assert.DoesNotContain(results, x => x.StatusCode == 503);
            var decider = (await scope.Register.GetByIdAsync(entry.Id))!.LastDecisionByCanonicalSubjectId!;
            await AssertDecidedByAsync(scope, entry.Id, ProductAbbreviationHistoryEventType.ALLOCATION_APPROVED, decider);
        }
    }

    [Fact]
    public async Task A_correction_requested_while_another_one_is_being_written_is_refused_and_only_one_waits()
    {
        // FIX2 A2 — the window: M1 has bound its key and taken its code from the ledger, but its claim and its entry
        // are not written yet. M2 runs completely in that window. M1's claim and entry go in one transaction, which
        // sees M2's pending correction and refuses: one correction waits, never two.
        await using var scope = await Scope.CreateAsync(mongo.ReplicaConnectionString);
        var active = await ApproveAsync(scope, await RequestAsync(scope, Maker, Guid.NewGuid(), "WIA"));
        Response<ProductAbbreviationRegisterModels.ProductAbbreviationAllocationResultDto>? second = null;
        var real = scope.Register;
        var held = Decorate<IProductAbbreviationRegisterRepository>(real, (method, args) =>
        {
            if (method.Name != nameof(IProductAbbreviationRegisterRepository.RequestEntryAsync) || second is not null)
            {
                return null;
            }

            async Task<ProductAbbreviationRegisterWriteResult> AfterTheOtherOneAsync()
            {
                second = await scope.Workflow(Maker2).InitiateCorrectionAsync(new InitiateProductAbbreviationCorrectionCommand(
                active.Id, active.Version, "WIC", Key(), "second"), default);
                return await real.RequestEntryAsync((ProductAbbreviationRequestWrite)args![0]!, (CancellationToken)args[1]!);
            }

            return AfterTheOtherOneAsync();
        });

        var first = await scope.Workflow(Maker, register: held).InitiateCorrectionAsync(
            new InitiateProductAbbreviationCorrectionCommand(active.Id, active.Version, "WIB", Key(), "first"), default);

        Assert.True(second!.IsSuccessful, string.Join(",", second.Errors));
        Assert.Equal("ABBREVIATION_CORRECTION_PENDING", Assert.Single(first.Errors));
        Assert.Single(await scope.Register.GetByGlobalProductIdAsync(active.GlobalProductId),
            x => x.LifecycleStatus == ProductAbbreviationLifecycleStatus.REQUESTED);
    }

    [Fact]
    public async Task A_retirement_requested_while_a_correction_is_being_written_is_refused()
    {
        // FIX2 A2 — the same window for a retirement: the correction completes between the retirement's own read and
        // its write; the retirement's transaction sees the pending correction and refuses (it no longer slips through
        // to be silently erased later by the correction's approval).
        await using var scope = await Scope.CreateAsync(mongo.ReplicaConnectionString);
        var active = await ApproveAsync(scope, await RequestAsync(scope, Maker, Guid.NewGuid(), "WRA"));
        Response<ProductAbbreviationRegisterModels.ProductAbbreviationAllocationResultDto>? correction = null;
        var real = scope.Register;
        var held = Decorate<IProductAbbreviationRegisterRepository>(real, (method, args) =>
        {
            if (method.Name != nameof(IProductAbbreviationRegisterRepository.DecideAsync) || correction is not null)
            {
                return null;
            }

            async Task<ProductAbbreviationRegisterWriteResult> AfterTheOtherOneAsync()
            {
                correction = await scope.Workflow(Maker2).InitiateCorrectionAsync(new InitiateProductAbbreviationCorrectionCommand(
                active.Id, active.Version, "WRB", Key(), "fix"), default);
                return await real.DecideAsync((ProductAbbreviationDecision)args![0]!, (CancellationToken)args[1]!);
            }

            return AfterTheOtherOneAsync();
        });

        var retire = await scope.Workflow(Maker, register: held).RequestRetirementAsync(
            new RequestProductAbbreviationRetirementCommand(active.Id, active.Version, Key(), "retire"), default);

        Assert.True(correction!.IsSuccessful, string.Join(",", correction.Errors));
        Assert.Equal("ABBREVIATION_CORRECTION_PENDING", Assert.Single(retire.Errors));
        Assert.Null((await scope.Register.GetByIdAsync(active.Id))!.RetirementRequestId);
    }

    [Fact]
    public async Task A_correction_requested_while_a_retirement_is_being_written_is_refused_as_retirement_pending()
    {
        await using var scope = await Scope.CreateAsync(mongo.ReplicaConnectionString);
        var active = await ApproveAsync(scope, await RequestAsync(scope, Maker, Guid.NewGuid(), "WTA"));
        Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>? retire = null;
        var real = scope.Register;
        var held = Decorate<IProductAbbreviationRegisterRepository>(real, (method, args) =>
        {
            if (method.Name != nameof(IProductAbbreviationRegisterRepository.RequestEntryAsync) || retire is not null)
            {
                return null;
            }

            async Task<ProductAbbreviationRegisterWriteResult> AfterTheOtherOneAsync()
            {
                retire = await scope.Workflow(Maker2).RequestRetirementAsync(
                new RequestProductAbbreviationRetirementCommand(active.Id, active.Version, Key(), "retire"), default);
                return await real.RequestEntryAsync((ProductAbbreviationRequestWrite)args![0]!, (CancellationToken)args[1]!);
            }

            return AfterTheOtherOneAsync();
        });

        var correction = await scope.Workflow(Maker, register: held).InitiateCorrectionAsync(
            new InitiateProductAbbreviationCorrectionCommand(active.Id, active.Version, "WTB", Key(), "fix"), default);

        Assert.True(retire!.IsSuccessful, string.Join(",", retire.Errors));
        Assert.Equal("ABBREVIATION_RETIREMENT_PENDING", Assert.Single(correction.Errors));
    }

    [Fact]
    public async Task A_claim_naming_a_pending_correction_of_another_code_blocks_nothing()
    {
        // FIX2 A2 — a claim counts only when the entry it names replaces THIS code.
        await using var scope = await Scope.CreateAsync(mongo.ReplicaConnectionString);
        var (_, elsewhere) = await ActiveWithCorrectionAsync(scope, "OCA", "OCB");
        var active = await ApproveAsync(scope, await RequestAsync(scope, Maker, Guid.NewGuid(), "OCC"));
        await scope.Database.GetCollection<ProductAbbreviationRegisterEntry>("mdm_product_abbreviation_register")
            .UpdateOneAsync(x => x.Id == active.Id, Builders<ProductAbbreviationRegisterEntry>.Update
                .Set(x => x.PendingCorrectionEntryId, elsewhere.Id));

        var correction = await scope.Workflow(Maker2).InitiateCorrectionAsync(new InitiateProductAbbreviationCorrectionCommand(
            active.Id, active.Version, "OCD", Key(), "fix"), default);
        var retire = await scope.Workflow(Maker).RequestRetirementAsync(
            new RequestProductAbbreviationRetirementCommand(elsewhere.ReplacesEntryId!.Value,
                (await scope.Register.GetByIdAsync(elsewhere.ReplacesEntryId!.Value))!.Version, Key(), "r"), default);

        Assert.True(correction.IsSuccessful, string.Join(",", correction.Errors));
        Assert.Equal("ABBREVIATION_CORRECTION_PENDING", Assert.Single(retire.Errors));
    }

    [Fact]
    public async Task Two_requests_for_one_product_written_together_leave_one_pending_request()
    {
        // FIX2 item 6 — the "nothing pending for this product" check and the insert are one transaction.
        await using var scope = await Scope.CreateAsync(mongo.ReplicaConnectionString);
        var product = Guid.NewGuid();
        Response<ProductAbbreviationRegisterModels.ProductAbbreviationAllocationResultDto>? second = null;
        var real = scope.Register;
        var held = Decorate<IProductAbbreviationRegisterRepository>(real, (method, args) =>
        {
            if (method.Name != nameof(IProductAbbreviationRegisterRepository.RequestEntryAsync) || second is not null)
            {
                return null;
            }

            async Task<ProductAbbreviationRegisterWriteResult> AfterTheOtherOneAsync()
            {
                second = await RequestResultAsync(scope, Maker2, product, "TPB");
                return await real.RequestEntryAsync((ProductAbbreviationRequestWrite)args![0]!, (CancellationToken)args[1]!);
            }

            return AfterTheOtherOneAsync();
        });

        var first = await scope.Workflow(Maker, register: held).RequestAsync(
            new RequestProductAbbreviationAllocationCommand(product, "TPA", Key()), default);

        Assert.True(second!.IsSuccessful, string.Join(",", second.Errors));
        Assert.Equal("ABBREVIATION_PRODUCT_ALREADY_HAS_ENTRY", Assert.Single(first.Errors));
        Assert.Single(await scope.Register.GetByGlobalProductIdAsync(product));
    }

    [Fact]
    public async Task One_key_cannot_serve_two_commands_on_the_same_entry()
    {
        // FIX2 item 6 — the key is bound to the entry AND the command.
        await using var scope = await Scope.CreateAsync(mongo.ReplicaConnectionString);
        var entry = await RequestAsync(scope, Maker, Guid.NewGuid(), "OKA");
        var key = Key();
        Assert.Equal(409, (await scope.Workflow(Checker).ApproveAsync(
            new ApproveProductAbbreviationAllocationCommand(entry.Id, entry.Version + 5, key), default)).StatusCode);

        var reject = await scope.Workflow(Checker).RejectAsync(
            new RejectProductAbbreviationAllocationCommand(entry.Id, entry.Version, key, "no"), default);

        Assert.Equal("ABBREVIATION_IDEMPOTENCY_KEY_REUSED", Assert.Single(reject.Errors));
        Assert.Equal(ProductAbbreviationLifecycleStatus.REQUESTED, (await scope.Register.GetByIdAsync(entry.Id))!.LifecycleStatus);
    }

    [Fact]
    public async Task Repeating_a_pending_request_and_a_pending_correction_with_their_keys_is_a_replay_not_a_refusal()
    {
        // The open-entry check ignores the request's own entry (REG Ne(Id, except)); the claim check ignores the
        // correction's own entry (WF claimed != correctionEntryId).
        await using var scope = await Scope.CreateAsync(mongo.ReplicaConnectionString);
        var product = Guid.NewGuid();
        var request = new RequestProductAbbreviationAllocationCommand(product, "RPA", Key());
        Assert.True((await scope.Workflow(Maker).RequestAsync(request, default)).IsSuccessful);
        var requestAgain = await scope.Workflow(Maker).RequestAsync(request, default);
        var active = await ApproveAsync(scope, (await scope.Register.GetByGlobalProductIdAsync(product)).Single());
        var correction = new InitiateProductAbbreviationCorrectionCommand(active.Id, active.Version, "RPB", Key(), "fix");
        Assert.True((await scope.Workflow(Maker2).InitiateCorrectionAsync(correction, default)).IsSuccessful);

        var correctionAgain = await scope.Workflow(Maker2).InitiateCorrectionAsync(correction, default);

        Assert.True(requestAgain.IsSuccessful, string.Join(",", requestAgain.Errors));
        Assert.True(requestAgain.Data!.IsReplay);
        Assert.True(correctionAgain.IsSuccessful, string.Join(",", correctionAgain.Errors));
    }

    [Fact]
    public async Task Pending_work_is_paged_in_the_order_it_started_waiting_without_gaps_or_repeats()
    {
        // FIX2 A3 / FIX3 item 4 — the order is the one the PRODUCTION writes store: a request stamps when it was made, a
        // retirement request stamps when the retirement was asked for (not when its code was first requested). The
        // codes to retire are made first, so a retirement that kept its old stamp would jump to the front.
        await using var scope = await Scope.CreateAsync(mongo.ReplicaConnectionString);
        var actives = new List<ProductAbbreviationRegisterEntry>();
        for (var i = 0; i < 8; i++)
        {
            actives.Add(await ApproveAsync(scope, await RequestAsync(scope, Maker, Guid.NewGuid(), $"PA{(char)('A' + i)}")));
        }

        var waitingOrder = new List<Guid>();
        for (var i = 0; i < 24; i++)
        {
            if (i % 3 == 1)
            {
                var active = actives[i / 3];
                var retire = await scope.Workflow(Maker2).RequestRetirementAsync(
                    new RequestProductAbbreviationRetirementCommand(active.Id, active.Version, Key(), "r"), default);
                Assert.True(retire.IsSuccessful, string.Join(",", retire.Errors));
                waitingOrder.Add(active.Id);
            }
            else
            {
                waitingOrder.Add((await RequestAsync(scope, Maker, Guid.NewGuid(), $"PW{(char)('A' + i)}")).Id);
            }
        }

        var seen = new List<Guid>();
        ProductAbbreviationPendingCursor? cursor = null;
        // Bounded: a cursor that does not move forward must fail here, not loop (24 rows need 5 pages + 1 empty).
        for (var pages = 0; pages < 8; pages++)
        {
            var page = await scope.Register.GetPendingWorkItemsPageAsync(cursor, 5);
            if (page.Count == 0) break;
            Assert.DoesNotContain(page, x => seen.Contains(x.Id));
            seen.AddRange(page.Select(x => x.Id));
            cursor = new(page[^1].PendingSinceUtcTicks, page[^1].Id);
        }

        Assert.Equal(waitingOrder, seen);
    }

    // ══ FIX3 ═══════════════════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Two_requests_for_one_product_both_past_the_open_entry_check_leave_one_and_the_other_is_refused()
    {
        // FIX3 item 1 — A has passed the "nothing open for this product" check and written the product's slot inside its
        // transaction; it is held there, uncommitted. B runs the same check (A's work is invisible to it) and must still
        // lose: its own slot write conflicts with A's. Without the slot (or with a slot per entry) both would commit.
        await using var scope = await Scope.CreateAsync(mongo.ReplicaConnectionString);
        var product = Guid.NewGuid();
        var barrier = new MongoBarrier();
        var held = scope.RegisterOn(MongoTaps.HoldFirst(scope.Database, "mdm_product_abbreviation_register",
            nameof(IMongoCollection<ProductAbbreviationRegisterEntry>.InsertOneAsync), barrier));

        var first = Task.Run(() => scope.Workflow(Maker, register: held).RequestAsync(
            new RequestProductAbbreviationAllocationCommand(product, "SLA", Key()), default));
        await barrier.Reached;
        var second = await RequestResultAsync(scope, Maker2, product, "SLB");
        barrier.Release();
        var firstResult = await first;

        Assert.True(firstResult.IsSuccessful, string.Join(",", firstResult.Errors));
        Assert.Equal(409, second.StatusCode);
        Assert.Single(await scope.Register.GetByGlobalProductIdAsync(product));
    }

    [Fact]
    public async Task A_request_that_loses_the_product_race_takes_no_code_and_the_code_can_be_requested_again()
    {
        // FIX3 item 3 — B loses the product race above; before FIX3 its code was already in the ledger for good.
        await using var scope = await Scope.CreateAsync(mongo.ReplicaConnectionString);
        var product = Guid.NewGuid();
        var barrier = new MongoBarrier();
        var held = scope.RegisterOn(MongoTaps.HoldFirst(scope.Database, "mdm_product_abbreviation_register",
            nameof(IMongoCollection<ProductAbbreviationRegisterEntry>.InsertOneAsync), barrier));

        var first = Task.Run(() => scope.Workflow(Maker, register: held).RequestAsync(
            new RequestProductAbbreviationAllocationCommand(product, "LCA", Key()), default));
        await barrier.Reached;
        var loser = await RequestResultAsync(scope, Maker2, product, "LCB");
        barrier.Release();
        Assert.True((await first).IsSuccessful);
        Assert.Equal(409, loser.StatusCode);

        var again = await RequestResultAsync(scope, Maker2, Guid.NewGuid(), "LCB");

        Assert.True(again.IsSuccessful, string.Join(",", again.Errors));
        Assert.Equal(1, await scope.Database
            .GetCollection<ProductAbbreviationAllocationLedger>("mdm_product_abbreviation_allocation_ledger")
            .CountDocumentsAsync(x => x.TenantId == scope.Tenant && x.NormalizedAbbreviation == "LCB"));
    }

    [Fact]
    public async Task Two_checkers_approving_one_correction_with_one_key_the_held_one_is_told_another_decided()
    {
        // FIX3 item 2 — both have passed every check; A is held right before its first write, B approves and commits.
        // A then finds the correction applied under the same key by someone else: refused, never "approved" (GxP).
        await using var scope = await Scope.CreateAsync(mongo.ReplicaConnectionString);
        var (active, correction) = await ActiveWithCorrectionAsync(scope, "CKA", "CKB");
        var command = new ApproveProductAbbreviationAllocationCommand(
            correction.Id, correction.Version, Key(), ExpectedFormerVersion: active.Version);
        var barrier = new MongoBarrier();
        var held = scope.RegisterOn(MongoTaps.HoldFirst(scope.Database, "mdm_product_abbreviation_register",
            nameof(IMongoCollection<ProductAbbreviationRegisterEntry>.UpdateOneAsync), barrier));

        var first = Task.Run(() => scope.Workflow(Checker, register: held).ApproveAsync(command, default));
        await barrier.Reached;
        var second = await scope.Workflow(Checker2).ApproveAsync(command, default);
        barrier.Release();
        var firstResult = await first;

        Assert.True(second.IsSuccessful, string.Join(",", second.Errors));
        Assert.False(firstResult.IsSuccessful);
        Assert.Equal(409, firstResult.StatusCode);
        Assert.Equal(ProductAbbreviationRegisterRepository.AlreadyDecidedByAnother, Assert.Single(firstResult.Errors));
        Assert.Equal(Checker2, (await scope.Register.GetByIdAsync(correction.Id))!.LastDecisionByCanonicalSubjectId);
        await AssertCorrectedOnceAsync(scope, active, correction);
    }

    [Fact]
    public async Task A_transaction_error_after_another_checker_decided_is_answered_from_the_stored_decision()
    {
        // FIX3 item 5 (the lost-race mapping) — A's write fails with a TransientTransactionError while B's approval has
        // committed. The answer comes from the stored state: B decided under this key, so A is told so (409).
        await using var scope = await Scope.CreateAsync(mongo.ReplicaConnectionString);
        var (active, correction) = await ActiveWithCorrectionAsync(scope, "LRA", "LRB");
        var command = new ApproveProductAbbreviationAllocationCommand(
            correction.Id, correction.Version, Key(), ExpectedFormerVersion: active.Version);
        var barrier = new MongoBarrier();
        var held = scope.RegisterOn(MongoTaps.HoldFirst(scope.Database, "mdm_product_abbreviation_register",
            nameof(IMongoCollection<ProductAbbreviationRegisterEntry>.UpdateOneAsync), barrier,
            thenThrow: () => MongoTaps.Labelled(251, "NoSuchTransaction", "TransientTransactionError")));

        var first = Task.Run(() => scope.Workflow(Checker, register: held).ApproveAsync(command, default));
        await barrier.Reached;
        Assert.True((await scope.Workflow(Checker2).ApproveAsync(command, default)).IsSuccessful);
        barrier.Release();
        var firstResult = await first;

        Assert.Equal(409, firstResult.StatusCode);
        Assert.Equal(ProductAbbreviationRegisterRepository.AlreadyDecidedByAnother, Assert.Single(firstResult.Errors));
    }

    [Fact]
    public async Task A_transient_labelled_outage_that_changed_nothing_is_503_not_a_conflict()
    {
        // FIX3 item 5 — a network or primary failure carries the TransientTransactionError label too; with nothing
        // stored differently it is an outage (503, retry later), not "someone changed it" (409, reload).
        await using var scope = await Scope.CreateAsync(mongo.ReplicaConnectionString);
        var (active, correction) = await ActiveWithCorrectionAsync(scope, "OUA", "OUB");
        var held = scope.RegisterOn(MongoTaps.HoldFirst(scope.Database, "mdm_product_abbreviation_register",
            nameof(IMongoCollection<ProductAbbreviationRegisterEntry>.UpdateOneAsync), barrier: null,
            thenThrow: () => MongoTaps.Labelled(91, "ShutdownInProgress", "TransientTransactionError")));

        var approved = await scope.Workflow(Checker, register: held).ApproveAsync(new ApproveProductAbbreviationAllocationCommand(
            correction.Id, correction.Version, Key(), ExpectedFormerVersion: active.Version), default);

        Assert.Equal(503, approved.StatusCode);
        Assert.Equal(ProductAbbreviationRegisterRepository.PersistenceUnavailable, Assert.Single(approved.Errors));
        await AssertUntouchedAsync(scope, active);
    }

    [Fact]
    public async Task A_decision_whose_commit_result_is_unknown_but_landed_is_reported_as_mine()
    {
        // FIX3 item 5 (UnknownTransactionCommitResult) — the commit reached the server and its answer was lost. The
        // stored state shows my decision under my key: success, not 503 and not a conflict.
        await using var scope = await Scope.CreateAsync(mongo.ReplicaConnectionString);
        var entry = await RequestAsync(scope, Maker, Guid.NewGuid(), "UKA");
        var register = scope.RegisterOn(MongoTaps.CommitThenThrow(scope.Database,
            () => MongoTaps.Labelled(50, "MaxTimeMSExpired", "UnknownTransactionCommitResult")));

        var approved = await scope.Workflow(Checker, register: register).ApproveAsync(
            new ApproveProductAbbreviationAllocationCommand(entry.Id, entry.Version, Key()), default);

        Assert.True(approved.IsSuccessful, string.Join(",", approved.Errors));
        await AssertDecidedByAsync(scope, entry.Id, ProductAbbreviationHistoryEventType.ALLOCATION_APPROVED, Checker);
    }

    [Fact]
    public async Task A_session_that_cannot_be_opened_is_an_outage_answer_not_an_exception()
    {
        // FIX3 item 5 — opening the session is inside the guard (before, a TimeoutException there was a 500).
        await using var scope = await Scope.CreateAsync(mongo.ReplicaConnectionString);
        var entry = await RequestAsync(scope, Maker, Guid.NewGuid(), "SOA");
        var register = scope.RegisterOn(MongoTaps.SessionFails(scope.Database, () => new TimeoutException("no server")));

        var approved = await scope.Workflow(Checker, register: register).ApproveAsync(
            new ApproveProductAbbreviationAllocationCommand(entry.Id, entry.Version, Key()), default);

        Assert.Equal(503, approved.StatusCode);
        Assert.Equal(ProductAbbreviationRegisterRepository.PersistenceUnavailable, Assert.Single(approved.Errors));
    }

    [Fact]
    public async Task On_a_standalone_server_every_abbreviation_write_answers_transactions_unavailable()
    {
        // FIX3 item 6 — before, NotSupportedException escaped every ABB write as a 500.
        await using var scope = await Scope.CreateAsync(mongo.StandaloneConnectionString);
        var product = Guid.NewGuid();

        var request = await scope.Workflow(Maker).RequestAsync(
            new RequestProductAbbreviationAllocationCommand(product, "SAA", Key()), default);

        Assert.Equal(503, request.StatusCode);
        Assert.Equal(ProductAbbreviationRegisterRepository.TransactionsUnavailable, Assert.Single(request.Errors));
        Assert.Empty(await scope.Register.GetByGlobalProductIdAsync(product));
    }

    [Fact]
    public async Task The_startup_check_warns_once_on_a_standalone_server_and_stays_silent_on_a_replica_set()
    {
        // FIX3 item 6 — one warning line, and the service still starts.
        var standalone = await RunTransactionCheckAsync(mongo.StandaloneConnectionString);
        var replica = await RunTransactionCheckAsync(mongo.ReplicaConnectionString);
        var unreachable = await RunTransactionCheckAsync("mongodb://127.0.0.1:1/?serverSelectionTimeoutMS=300&connectTimeoutMS=300");

        Assert.Single(standalone, line => line.Contains(
            Diten.MdmService.Api.Services.ProductAbbreviationRegister.ProductAbbreviationTransactionSupportCheck.UnavailableEvent));
        Assert.Empty(replica);
        Assert.Single(unreachable, line => line.Contains(
            Diten.MdmService.Api.Services.ProductAbbreviationRegister.ProductAbbreviationTransactionSupportCheck.UncheckedEvent));
    }

    [Fact]
    public async Task Pending_work_written_before_its_order_was_stored_is_given_one_and_nothing_is_lost_after_page_one()
    {
        // FIX3 item 9 — rows from before FIX2 carry no PendingSinceUtcTicks. Before, they sorted first and the cursor of
        // page one (order 0) could not place them: everything after the first page vanished from the Task Center.
        await using var scope = await Scope.CreateAsync(mongo.ReplicaConnectionString);
        var collection = scope.Database.GetCollection<ProductAbbreviationRegisterEntry>("mdm_product_abbreviation_register");
        var raw = scope.Database.GetCollection<MongoDB.Bson.BsonDocument>("mdm_product_abbreviation_register");
        var start = new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.FromHours(3));
        var expected = new List<(Guid Id, long Ticks)>();
        for (var i = 0; i < 12; i++)
        {
            var at = start.AddMinutes(i * 7).ToOffset(TimeSpan.FromHours(i % 3 == 0 ? -5 : 3));
            var retiring = i % 4 == 2;
            var entry = new ProductAbbreviationRegisterEntry
            {
                Id = Guid.NewGuid(),
                TenantId = scope.Tenant,
                GlobalProductId = Guid.NewGuid(),
                NormalizedAbbreviation = $"BF{(char)('A' + i)}",
                AllocationLedgerId = Guid.NewGuid(),
                AllocationIdempotencyKey = Key(),
                LifecycleStatus = retiring ? ProductAbbreviationLifecycleStatus.ACTIVE : ProductAbbreviationLifecycleStatus.REQUESTED,
                RequestedByCanonicalSubjectId = Maker,
                // A retirement waits from when it was asked for, long after its code was first requested.
                RequestedAtUtc = retiring ? at.AddDays(-30) : at,
                RetirementRequestId = retiring ? Key() : null,
                RetirementRequestedAtUtc = retiring ? at : null
            };
            await collection.InsertOneAsync(entry);
            await raw.UpdateOneAsync(
                Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id", entry.Id),
                Builders<MongoDB.Bson.BsonDocument>.Update.Unset(nameof(ProductAbbreviationRegisterEntry.PendingSinceUtcTicks)));
            expected.Add((entry.Id, at.UtcTicks));
        }

        var seen = new List<Guid>();
        ProductAbbreviationPendingCursor? cursor = null;
        for (var pages = 0; pages < 8; pages++)
        {
            var page = await scope.Register.GetPendingWorkItemsPageAsync(cursor, 5);
            if (page.Count == 0) break;
            seen.AddRange(page.Select(x => x.Id));
            cursor = new(page[^1].PendingSinceUtcTicks, page[^1].Id);
        }

        Assert.Equal(expected.OrderBy(x => x.Ticks).Select(x => x.Id), seen);
        foreach (var (id, ticks) in expected)
        {
            Assert.Equal(ticks, (await scope.Register.GetByIdAsync(id))!.PendingSinceUtcTicks);
        }
    }

    [Fact]
    public async Task A_page_of_pending_work_reads_only_pending_rows_however_many_decided_rows_the_tenant_has()
    {
        // FIX3 item 10 — the pending order lives in partial indexes holding only waiting rows; measured on the server
        // (the profiler's docsExamined for the page query), not by reading the index definition back.
        await using var scope = await Scope.CreateAsync(mongo.ReplicaConnectionString);
        var collection = scope.Database.GetCollection<ProductAbbreviationRegisterEntry>("mdm_product_abbreviation_register");
        _ = scope.Register;
        var decided = Enumerable.Range(0, 60).Select(i => new ProductAbbreviationRegisterEntry
        {
            Id = Guid.NewGuid(), TenantId = scope.Tenant, GlobalProductId = Guid.NewGuid(),
            NormalizedAbbreviation = $"D{i:D3}", AllocationLedgerId = Guid.NewGuid(), AllocationIdempotencyKey = Key(),
            LifecycleStatus = i % 2 == 0 ? ProductAbbreviationLifecycleStatus.REJECTED : ProductAbbreviationLifecycleStatus.ACTIVE,
            RequestedByCanonicalSubjectId = Maker, RequestedAtUtc = DateTimeOffset.UtcNow, PendingSinceUtcTicks = 1000 + i
        });
        await collection.InsertManyAsync(decided);
        for (var i = 0; i < 3; i++)
        {
            await RequestAsync(scope, Maker, Guid.NewGuid(), $"PI{(char)('A' + i)}");
        }

        await scope.Database.RunCommandAsync<MongoDB.Bson.BsonDocument>(new MongoDB.Bson.BsonDocument("profile", 2));
        try
        {
            var page = await scope.Register.GetPendingWorkItemsPageAsync(new(0, Guid.Empty), 50);
            Assert.Equal(3, page.Count);
        }
        finally
        {
            await scope.Database.RunCommandAsync<MongoDB.Bson.BsonDocument>(new MongoDB.Bson.BsonDocument("profile", 0));
        }

        var profiled = await scope.Database.GetCollection<MongoDB.Bson.BsonDocument>("system.profile")
            .Find(Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("ns", "diten_mdm_abb_register_itest.mdm_product_abbreviation_register")
                  & Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("op", "query"))
            .Sort(Builders<MongoDB.Bson.BsonDocument>.Sort.Descending("ts"))
            .FirstAsync();
        // Keys as well as documents: an index that holds every row can filter on its trailing status key and still fetch
        // only 3 documents, but it walks the 60 decided rows' keys to do so.
        Assert.True(profiled["docsExamined"].ToInt64() <= 3, profiled.ToString());
        Assert.True(profiled["keysExamined"].ToInt64() <= 10, profiled.ToString());
    }

    [Fact]
    public async Task Another_person_repeating_a_request_key_is_refused_and_the_request_stays_the_first_persons()
    {
        // FIX3 item 11 — before, B repeating A's key got "pending" (and an evidence write that could never match: 202
        // forever). A repeat is a replay only for the person who asked.
        await using var scope = await Scope.CreateAsync(mongo.ReplicaConnectionString);
        var request = new RequestProductAbbreviationAllocationCommand(Guid.NewGuid(), "RKA", Key());
        var first = await scope.Workflow(Maker).RequestAsync(request, default);
        Assert.True(first.IsSuccessful);
        var historyBefore = await scope.History.GetForRegisterEntryAsync(first.Data!.Entry.Id);

        var other = await scope.Workflow(Maker2).RequestAsync(request, default);
        var mine = await scope.Workflow(Maker).RequestAsync(request, default);

        Assert.Equal(409, other.StatusCode);
        Assert.Equal("ABBREVIATION_IDEMPOTENCY_KEY_REUSED", Assert.Single(other.Errors));
        Assert.True(mine.IsSuccessful);
        Assert.True(mine.Data!.IsReplay);
        Assert.Equal(Maker, (await scope.Register.GetByIdAsync(first.Data.Entry.Id))!.RequestedByCanonicalSubjectId);
        Assert.Equal(historyBefore.Count, (await scope.History.GetForRegisterEntryAsync(first.Data.Entry.Id)).Count);
    }

    [Fact]
    public async Task A_correction_asked_against_a_version_the_code_has_left_is_refused_inside_its_transaction()
    {
        // FIX3 item 13 — the workflow checked the version once, before; in between a retirement was asked and turned
        // down (the code is ACTIVE again with no claim, only its version moved). The claim write now names the version.
        await using var scope = await Scope.CreateAsync(mongo.ReplicaConnectionString);
        var active = await ApproveAsync(scope, await RequestAsync(scope, Maker, Guid.NewGuid(), "CVA"));
        var real = scope.Register;
        var moved = false;
        var held = Decorate<IProductAbbreviationRegisterRepository>(real, (method, args) =>
        {
            if (method.Name != nameof(IProductAbbreviationRegisterRepository.RequestEntryAsync) || moved)
            {
                return null;
            }

            async Task<ProductAbbreviationRegisterWriteResult> AfterTheCodeMovedAsync()
            {
                moved = true;
                var retireKey = Key();
                var retire = await scope.Workflow(Maker2).RequestRetirementAsync(
                    new RequestProductAbbreviationRetirementCommand(active.Id, active.Version, retireKey, "r"), default);
                Assert.True(retire.IsSuccessful, string.Join(",", retire.Errors));
                Assert.True((await scope.Workflow(Checker).RejectRetirementAsync(new RejectProductAbbreviationRetirementCommand(
                    active.Id, retire.Data!.Version, retireKey, Key(), "no"), default)).IsSuccessful);
                return await real.RequestEntryAsync((ProductAbbreviationRequestWrite)args![0]!, (CancellationToken)args[1]!);
            }

            return AfterTheCodeMovedAsync();
        });

        var correction = await scope.Workflow(Maker, register: held).InitiateCorrectionAsync(
            new InitiateProductAbbreviationCorrectionCommand(active.Id, active.Version, "CVB", Key(), "fix"), default);

        Assert.Equal(409, correction.StatusCode);
        Assert.Equal("CONCURRENCY_CONFLICT", Assert.Single(correction.Errors));
        var stored = (await scope.Register.GetByIdAsync(active.Id))!;
        Assert.Null(stored.PendingCorrectionEntryId);
        Assert.DoesNotContain(await scope.Register.GetByGlobalProductIdAsync(active.GlobalProductId),
            x => x.LifecycleStatus == ProductAbbreviationLifecycleStatus.REQUESTED);
    }

    [Fact]
    public async Task The_policy_batch_read_answers_only_this_tenants_policies()
    {
        // FIX3 item 7 — GetByGlobalProductIdsAsync (the projection's one read) is held to the tenant filter on Mongo.
        await using var scope = await Scope.CreateAsync(mongo.ReplicaConnectionString);
        var otherTenant = Guid.NewGuid();
        var mine = Guid.NewGuid();
        var theirs = Guid.NewGuid();
        var policies = scope.Database.GetCollection<ProductLegalEntityScopePolicy>("mdm_product_legal_entity_scope_policies");
        await policies.InsertManyAsync([
            ProductLegalEntityScopePolicy.Create(scope.Tenant, mine, Guid.NewGuid(), ProductLegalEntityScopeMode.Scoped,
                [Guid.NewGuid()], Guid.NewGuid(), DateTimeOffset.UtcNow),
            ProductLegalEntityScopePolicy.Create(otherTenant, theirs, Guid.NewGuid(), ProductLegalEntityScopeMode.Scoped,
                [Guid.NewGuid()], Guid.NewGuid(), DateTimeOffset.UtcNow)
        ]);
        try
        {
            var context = new TenantContext();
            context.SetTenant(scope.Tenant);
            var found = await new ProductLegalEntityScopePolicyRepository(scope.Database, context)
                .GetByGlobalProductIdsAsync([mine, theirs]);

            Assert.Equal([mine], found.Select(x => x.GlobalProductId).ToArray());
        }
        finally
        {
            await policies.DeleteManyAsync(x => x.TenantId == scope.Tenant || x.TenantId == otherTenant);
        }
    }

    private static async Task<List<string>> RunTransactionCheckAsync(string connectionString)
    {
        var settings = MongoClientSettings.FromConnectionString(connectionString);
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(2);
#pragma warning disable CS0618
        settings.GuidRepresentation = MongoDB.Bson.GuidRepresentation.Standard;
#pragma warning restore CS0618
        var database = new MongoClient(settings).GetDatabase("diten_mdm_abb_register_itest");
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddScoped(services, _ => database);
        await using var provider = Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions
            .BuildServiceProvider(services);
        var logger = new LineLogger<Diten.MdmService.Api.Services.ProductAbbreviationRegister.ProductAbbreviationTransactionSupportCheck>();
        var check = new Diten.MdmService.Api.Services.ProductAbbreviationRegister.ProductAbbreviationTransactionSupportCheck(
            Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                .GetRequiredService<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>(provider),
            logger);
        await check.StartAsync(CancellationToken.None);
        return logger.Lines;
    }

    private sealed class LineLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
    {
        public List<string> Lines { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId,
            TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Lines.Add(formatter(state, exception));
    }

    private static async Task AssertDecidedByAsync(
        Scope scope,
        Guid entryId,
        ProductAbbreviationHistoryEventType eventType,
        string decider)
    {
        var stored = (await scope.Register.GetByIdAsync(entryId))!;
        Assert.Equal(decider, stored.LastDecisionByCanonicalSubjectId);
        Assert.Equal(decider, Assert.Single(await scope.History.GetForRegisterEntryAsync(entryId),
            row => row.EventType == eventType).CanonicalHumanSubjectId);
        var operation = eventType == ProductAbbreviationHistoryEventType.ALLOCATION_APPROVED
            ? ProductAuditOperation.ProductAbbreviationAllocationApproved
            : ProductAuditOperation.ProductAbbreviationCorrectionApproved;
        Assert.Equal(decider, Assert.Single(stored.AuditIntents, x => x.Operation == operation).ActorId);
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

    /// <summary>A REQUESTED correction written straight to the collection, bypassing FIX1's one-pending-change claim
    /// (the state pre-FIX1 code could leave; no production path writes it).</summary>
    private static async Task<ProductAbbreviationRegisterEntry> InsertOrphanCandidateAsync(
        Scope scope, ProductAbbreviationRegisterEntry active, string abbreviation)
    {
        var entry = new ProductAbbreviationRegisterEntry
        {
            Id = Guid.NewGuid(),
            TenantId = scope.Tenant,
            GlobalProductId = active.GlobalProductId,
            NormalizedAbbreviation = abbreviation,
            AllocationLedgerId = Guid.NewGuid(),
            AllocationIdempotencyKey = Key(),
            LifecycleStatus = ProductAbbreviationLifecycleStatus.REQUESTED,
            RequestedByCanonicalSubjectId = Maker2,
            RequestedAtUtc = DateTimeOffset.UtcNow,
            ReplacesEntryId = active.Id
        };
        await scope.Database.GetCollection<ProductAbbreviationRegisterEntry>("mdm_product_abbreviation_register")
            .InsertOneAsync(entry);
        return entry;
    }

    private static string Key() => "fix1-" + Guid.NewGuid().ToString("N");

    /// <summary>A history row occupying an id the code under test will want, so that write fails inside its transaction.</summary>
    private static ProductAbbreviationHistoryEntry Blocker(Guid id) => new()
    {
        Id = id,
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
    };

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
        /// <summary>FIX3 — the same repository over a tapped database (see <see cref="MongoTaps"/>).</summary>
        public ProductAbbreviationRegisterRepository RegisterOn(IMongoDatabase database) => new(database, Context());
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
                         "mdm_product_abbreviation_allocation_ledger", "mdm_product_abbreviation_command_keys",
                         "mdm_product_abbreviation_product_slots"
                     })
            {
                await Database.GetCollection<MongoDB.Bson.BsonDocument>(name).DeleteManyAsync(
                    Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("TenantId", Tenant));
            }
        }
    }
}
