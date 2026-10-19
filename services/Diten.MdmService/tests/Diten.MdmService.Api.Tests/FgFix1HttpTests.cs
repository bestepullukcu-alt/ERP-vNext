using System.Net;
using System.Net.Http.Json;
using Diten.MdmService.Api.Services.ProductItemSkuMaster;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts.Audit;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Audit;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Handlers.CommandHandlers;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using Diten.Shared.Core;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// WP-MDM-FG-ACCEPT-01 FIX1 item 1 (BL-559; MOD-0290 s21.2 / s21.5) over HTTP and a throwaway mongod: a Draft Finished
/// Good blocks its GSKU's correction and retirement request; its audited Draft cancellation frees the GSKU only once the
/// cancellation's intent is delivered and compacted into a receipt that validates (the real delivery processor and
/// repository; only the central client is a fake that accepts). Pending, forged and legacy records keep blocking.
/// </summary>
[Collection(MdmHttpCollection.Name)]
public sealed partial class FgFix1HttpTests(MdmHttpHost host)
{
    private static readonly string[] MakerPermissions =
    [
        "mdm.gskus.read", "mdm.gskus.create", "mdm.gskus.update", "mdm.gskus.submit",
        "mdm.gskus.request-correction", "mdm.gskus.request-retirement",
        "mdm.finished-goods.read", "mdm.finished-goods.create"
    ];

    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _maker = Guid.NewGuid();
    private string _trace = string.Empty;

    // ---------------------------------------------------------------------------------------------------- item 1

    [Theory]
    [InlineData("retirement")]
    [InlineData("correction")]
    public async Task Item01_A_draft_fg_blocks_its_gsku_until_its_cancellation_is_audited(string request)
    {
        var gsku = await ApprovedGskuAsync();
        var fg = await CreatedFgAsync(gsku);

        Assert.Equal("DEPENDENT_IDENTITIES_EXIST", await RefusedAsync(gsku, request));

        var cancel = await CancelAsync(fg, (await FgAsync(fg)).Version);
        Assert.True(cancel.StatusCode == HttpStatusCode.OK, await cancel.Content.ReadAsStringAsync());
        Assert.Equal(FinishedGoodCancellationDisposition.PendingAudit,
            (FinishedGoodCancellationDisposition)(await Envelope.ReadAsync(cancel)).GetProperty("data").GetProperty("disposition").GetInt32());
        var stored = await FgAsync(fg);
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, stored.LifecycleStatus);
        Assert.False(stored.IsDeleted);
        Assert.Single(stored.AuditIntents, x => x.Operation == ProductAuditOperation.FinishedGoodDraftCancelled);
        // Pending its audit: still a blocker.
        Assert.Equal("DEPENDENT_IDENTITIES_EXIST", await RefusedAsync(gsku, request));
        Assert.Equal("PendingAudit", await DispositionAsync(fg));

        await DeliverAuditAsync();

        var disposition = await DispositionAsync(fg);
        Assert.True(disposition == "Cancelled", disposition + " " + _trace);
        var accepted = await RequestAsync(gsku, request);
        Assert.True(accepted.IsSuccessStatusCode, await accepted.Content.ReadAsStringAsync());
    }

    // The flag-free rule: a receipt that does not match the decision (here its evidence) is not the end — it blocks.
    [Fact]
    public async Task Item01_A_receipt_that_does_not_match_the_cancellation_keeps_blocking()
    {
        var gsku = await ApprovedGskuAsync();
        var fg = await CreatedFgAsync(gsku);
        Assert.Equal(HttpStatusCode.OK, (await CancelAsync(fg, (await FgAsync(fg)).Version)).StatusCode);
        await DeliverAuditAsync();
        Assert.Equal("Cancelled", await DispositionAsync(fg));
        var intentId = (await FgAsync(fg)).DraftCancellation!.IntentId;
        await host.Database.GetCollection<FinishedGood>("mdm_finished_goods").UpdateOneAsync(
            x => x.Id == fg,
            Builders<FinishedGood>.Update.Set("AuditIntentReceipts.$[r].EvidenceHash", "FORGED"),
            new UpdateOptions
            {
                ArrayFilters = [new BsonDocumentArrayFilterDefinition<BsonDocument>(
                    new BsonDocument("r.IntentId", new BsonBinaryData(intentId, GuidRepresentation.Standard)))]
            });

        Assert.Equal("PendingAudit", await DispositionAsync(fg));
        Assert.Equal("DEPENDENT_IDENTITIES_EXIST", await RefusedAsync(gsku, "retirement"));
    }

    [Fact]
    public async Task Item01_An_active_sibling_still_blocks_after_the_other_is_cancelled()
    {
        var gsku = await ApprovedGskuAsync();
        var cancelled = await CreatedFgAsync(gsku);
        await CreatedFgAsync(gsku);
        Assert.Equal(HttpStatusCode.OK, (await CancelAsync(cancelled, (await FgAsync(cancelled)).Version)).StatusCode);
        await DeliverAuditAsync();

        Assert.Equal("Cancelled", await DispositionAsync(cancelled));
        Assert.Equal("DEPENDENT_IDENTITIES_EXIST", await RefusedAsync(gsku, "retirement"));
    }

    [Fact]
    public async Task Item01_The_same_cancel_is_a_replay_and_the_create_key_never_brings_the_draft_back()
    {
        var gsku = await ApprovedGskuAsync();
        var createKey = "FG-" + Guid.NewGuid().ToString("N");
        var created = await CreateFgAsync(gsku, createKey);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var fg = (await Envelope.ReadAsync(created)).GetProperty("data").GetProperty("finishedGoodId").GetGuid();
        var version = (await FgAsync(fg)).Version;
        var key = Guid.NewGuid();

        var first = await CancelAsync(fg, version, key);
        var again = await CancelAsync(fg, version, key);
        var other = await CancelAsync(fg, version);
        var recreate = await CreateFgAsync(gsku, createKey);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.True((await Envelope.ReadAsync(again)).GetProperty("data").GetProperty("isReplay").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, other.StatusCode);
        Assert.Equal("FINISHED_GOOD_DRAFT_ALREADY_CANCELLED", Envelope.FirstError(await Envelope.ReadAsync(other)));
        Assert.Equal(HttpStatusCode.Conflict, recreate.StatusCode);
        Assert.Equal("FINISHED_GOOD_DRAFT_CANCELLED", Envelope.FirstError(await Envelope.ReadAsync(recreate)));
        var stored = await FgAsync(fg);
        Assert.Equal(version + 1, stored.Version);
        Assert.Single(stored.AuditIntents, x => x.Operation == ProductAuditOperation.FinishedGoodDraftCancelled);
    }

    [Fact]
    public async Task Item01_A_cancel_needs_the_key_a_reason_and_the_current_version()
    {
        var gsku = await ApprovedGskuAsync();
        var fg = await CreatedFgAsync(gsku);
        var version = (await FgAsync(fg)).Version;
        var readOnly = host.Token(_tenant, _maker, ["mdm.finished-goods.read"]);

        var forbidden = await SendAsync($"/api/finished-goods/{fg:D}/cancel-draft",
            new { expectedVersion = version, reasonCode = "CREATED_BY_MISTAKE" }, readOnly, Guid.NewGuid());
        var noReason = await SendAsync($"/api/finished-goods/{fg:D}/cancel-draft",
            new { expectedVersion = version, reasonCode = "" }, MakerToken(), Guid.NewGuid());
        var controlChars = await SendAsync($"/api/finished-goods/{fg:D}/cancel-draft",
            new { expectedVersion = version, reasonCode = "a\nb" }, MakerToken(), Guid.NewGuid());
        var stale = await CancelAsync(fg, version + 5);

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, controlChars.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("CONCURRENCY_CONFLICT", Envelope.FirstError(await Envelope.ReadAsync(stale)));
        Assert.Null((await FgAsync(fg)).DraftCancellation);
    }

    // Legacy rule: a Finished Good written before the cancellation existed (no DraftCancellation field at all) still
    // blocks its GSKU, is cancelled like any other, and frees it once audited.
    [Fact]
    public async Task Item01_A_finished_good_without_the_new_fields_blocks_is_cancelled_and_frees_its_gsku()
    {
        var gsku = await ApprovedGskuAsync();
        var fg = await CreatedFgAsync(gsku);
        await host.Database.GetCollection<BsonDocument>("mdm_finished_goods").UpdateOneAsync(
            new BsonDocument("_id", new BsonBinaryData(fg, GuidRepresentation.Standard)),
            new BsonDocument("$unset", new BsonDocument("DraftCancellation", "")));

        Assert.Equal("DEPENDENT_IDENTITIES_EXIST", await RefusedAsync(gsku, "retirement"));
        var raw = await host.Database.GetCollection<BsonDocument>("mdm_finished_goods")
            .Find(new BsonDocument("_id", new BsonBinaryData(fg, GuidRepresentation.Standard))).SingleAsync();
        var cancel = await CancelAsync(fg, raw["Version"].AsInt32);
        Assert.True(cancel.StatusCode == HttpStatusCode.OK, await cancel.Content.ReadAsStringAsync());
        await DeliverAuditAsync();

        var accepted = await RequestAsync(gsku, "retirement");
        Assert.True(accepted.IsSuccessStatusCode, await accepted.Content.ReadAsStringAsync());
    }

    // Principle B: a cancellation still waiting for its receipt after the cutoff is counted (its GSKU is blocked);
    // once audited it no longer is.
    [Fact]
    public async Task Item01_A_cancellation_waiting_for_its_audit_is_counted_until_it_is_finished()
    {
        var gsku = await ApprovedGskuAsync();
        var fg = await CreatedFgAsync(gsku);
        Assert.Equal(HttpStatusCode.OK, (await CancelAsync(fg, (await FgAsync(fg)).Version)).StatusCode);
        await host.Database.GetCollection<FinishedGood>("mdm_finished_goods").UpdateOneAsync(x => x.Id == fg,
            Builders<FinishedGood>.Update.Set(x => x.DraftCancellation!.RequestedAtUtc, DateTimeOffset.UtcNow.AddHours(-1)));
        var gauge = host.Factory.Services.GetRequiredService<GskuChildAdmissionGauge>();

        var pending = await gauge.RecordFinishedGoodCancellationsAsync(CancellationToken.None);
        await DeliverAuditAsync();
        var after = await gauge.RecordFinishedGoodCancellationsAsync(CancellationToken.None);

        Assert.True(pending >= 1);
        Assert.Equal(pending - 1, after);
        Assert.Equal(after, host.Factory.Services.GetRequiredService<ProductIdentityRecoveryMetrics>()
            .Current(GskuChildAdmissionGauge.FinishedGoodCancellationFlow));
    }

    // ---------------------------------------------------------------------------------------------------- items 2 / 3

    // Crash right after the admission (before any code is ours): the exit by exception gives the admission back.
    [Fact]
    public async Task Item02_A_crash_after_the_admission_gives_it_back()
    {
        var gsku = await ApprovedGskuAsync();
        var key = "FG-" + Guid.NewGuid().ToString("N");
        using var scope = TenantScope();
        var handler = CreateHandler(scope, reservations: Intercept<ICodeReservationRepository>(
            scope.ServiceProvider.GetRequiredService<ICodeReservationRepository>(),
            (method, _) => method.Name == nameof(ICodeReservationRepository.ReserveAsync)
                ? throw new IOException("store went away") : null));

        await Assert.ThrowsAsync<IOException>(() => handler.Handle(Command(gsku, key), CancellationToken.None));

        Assert.Empty((await GskuAsync(gsku)).ChildCreationAdmissions);
    }

    // Crash after the code was consumed (the FG write cut off): the admission stays with the live code (never released
    // next to something that may still become the child); past the cutoff the FG operator repair burns that code first,
    // then releases — and the gauge counted it meanwhile.
    [Fact]
    public async Task Item02_A_crash_after_the_code_is_consumed_keeps_the_admission_until_the_repair_burns_and_releases()
    {
        var gsku = await ApprovedGskuAsync();
        var key = "FG-" + Guid.NewGuid().ToString("N");
        var commandId = key.ToUpperInvariant();
        using (var scope = TenantScope())
        {
            var handler = CreateHandler(scope, finishedGoods: Intercept<IFinishedGoodRepository>(
                scope.ServiceProvider.GetRequiredService<IFinishedGoodRepository>(),
                (method, _) => method.Name == nameof(IFinishedGoodRepository.CreateDraftWithAdmissionAsync)
                    ? throw new IOException("store went away") : null));
            await Assert.ThrowsAsync<IOException>(() => handler.Handle(Command(gsku, key), CancellationToken.None));
        }
        Assert.Contains(commandId, (await GskuAsync(gsku)).ChildCreationAdmissions.Select(x => x.CreationCommandId));
        var gauge = host.Factory.Services.GetRequiredService<GskuChildAdmissionGauge>();
        await AgeAsync(gsku, commandId);
        Assert.True(await gauge.RecordAsync(CancellationToken.None) >= 1);

        var repair = await host.Client(host.Token(_tenant, Guid.NewGuid(), [RecoverKey]))
            .PostAsync("/api/finished-goods/operator/child-admission-repairs", null);

        Assert.True(repair.IsSuccessStatusCode, await repair.Content.ReadAsStringAsync());
        Assert.Equal(1, (await Envelope.ReadAsync(repair)).GetProperty("data").GetProperty("released").GetInt32());
        Assert.DoesNotContain(commandId, (await GskuAsync(gsku)).ChildCreationAdmissions.Select(x => x.CreationCommandId));
        var code = await host.Database.GetCollection<CodeReservation>("mdm_code_reservations")
            .Find(x => x.TenantId == _tenant && x.ReservationCommandId == commandId).SingleAsync();
        Assert.Equal(CodeReservationBindingState.Burned, code.BindingState);
    }

    // Item 3: crash after the FG row is written and before its code is confirmed — the same command again confirms the
    // code and completes the admission: 201, the admission gone (formerly: 202 for good, the admission never removed).
    [Fact]
    public async Task Item03_A_crash_between_the_write_and_the_confirmation_is_finished_by_the_replay()
    {
        var gsku = await ApprovedGskuAsync();
        var key = "FG-" + Guid.NewGuid().ToString("N");
        using (var scope = TenantScope())
        {
            var handler = CreateHandler(scope, reservations: Intercept<ICodeReservationRepository>(
                scope.ServiceProvider.GetRequiredService<ICodeReservationRepository>(),
                (method, _) => method.Name == nameof(ICodeReservationRepository.ConfirmIdentityBindingAsync)
                    ? throw new IOException("store went away") : null));
            await Assert.ThrowsAsync<IOException>(() => handler.Handle(Command(gsku, key), CancellationToken.None));
        }
        Assert.NotEmpty((await GskuAsync(gsku)).ChildCreationAdmissions);

        var replay = await CreateFgAsync(gsku, key);

        Assert.True(replay.StatusCode == HttpStatusCode.Created, await replay.Content.ReadAsStringAsync());
        Assert.Empty((await GskuAsync(gsku)).ChildCreationAdmissions);
        var fg = (await Envelope.ReadAsync(replay)).GetProperty("data").GetProperty("finishedGoodId").GetGuid();
        var reservationId = (await FgAsync(fg)).CodeReservationId;
        var code = await host.Database.GetCollection<CodeReservation>("mdm_code_reservations")
            .Find(x => x.Id == reservationId).SingleAsync();
        Assert.Equal(CodeReservationBindingState.Confirmed, code.BindingState);
    }

    // A write the store definitely refuses: the consumed code is burned and the admission goes back — repeated failures
    // no longer pile up toward the 32-admission cap that would stop every child creation of the GSKU.
    [Fact]
    public async Task Item02_A_refused_write_burns_its_code_and_failures_do_not_accumulate_admissions()
    {
        var gsku = await ApprovedGskuAsync();
        for (var attempt = 0; attempt < 3; attempt++)
        {
            using var scope = TenantScope();
            var handler = CreateHandler(scope, finishedGoods: Intercept<IFinishedGoodRepository>(
                scope.ServiceProvider.GetRequiredService<IFinishedGoodRepository>(),
                (method, _) => method.Name == nameof(IFinishedGoodRepository.CreateDraftWithAdmissionAsync)
                    ? Task.FromResult(new FinishedGoodCreateResult(false, null, "FINISHED_GOOD_WRITE_REFUSED")) : null));
            var key = "FG-" + Guid.NewGuid().ToString("N");

            var refused = await handler.Handle(Command(gsku, key), CancellationToken.None);

            Assert.Equal(409, refused.StatusCode);
            var code = await host.Database.GetCollection<CodeReservation>("mdm_code_reservations")
                .Find(x => x.TenantId == _tenant && x.ReservationCommandId == key.ToUpperInvariant()).SingleAsync();
            Assert.Equal(CodeReservationBindingState.Burned, code.BindingState);
        }
        Assert.Empty((await GskuAsync(gsku)).ChildCreationAdmissions);
        Assert.Equal(HttpStatusCode.Created, (await CreateFgAsync(gsku, "FG-" + Guid.NewGuid().ToString("N"))).StatusCode);
    }

    // ---------------------------------------------------------------------------------------------------- review R1

    // R1: a Finished Good whose creation was cut off with its parent admission still held (its code confirmed, or still
    // pending) — the cancel settles that creation (the code bound to the retained row, the admission completed), so
    // once audited the GSKU's retirement request goes through instead of being re-locked by the admission.
    [Theory]
    [InlineData("confirmed")]
    [InlineData("pending")]
    public async Task R1_A_cancel_settles_a_creation_cut_off_with_its_admission_held(string codeState)
    {
        var gsku = await ApprovedGskuAsync();
        var key = "FG-" + Guid.NewGuid().ToString("N");
        using (var scope = TenantScope())
        {
            var handler = codeState == "pending"
                ? CreateHandler(scope, reservations: Intercept<ICodeReservationRepository>(
                    scope.ServiceProvider.GetRequiredService<ICodeReservationRepository>(),
                    (method, _) => method.Name == nameof(ICodeReservationRepository.ConfirmIdentityBindingAsync)
                        ? throw new IOException("store went away") : null))
                : CreateHandler(scope, gskus: Intercept<IGskuRepository>(
                    scope.ServiceProvider.GetRequiredService<IGskuRepository>(),
                    (method, _) => method.Name == nameof(IGskuRepository.CompleteChildCreationAdmissionAsync)
                        ? throw new IOException("store went away") : null));
            await Assert.ThrowsAsync<IOException>(() => handler.Handle(Command(gsku, key), CancellationToken.None));
        }
        Assert.NotEmpty((await GskuAsync(gsku)).ChildCreationAdmissions);
        var fg = await host.Database.GetCollection<FinishedGood>("mdm_finished_goods")
            .Find(x => x.TenantId == _tenant && x.CreationCommandId == key.ToUpperInvariant()).SingleAsync();
        Assert.Equal(codeState == "pending" ? CodeReservationBindingState.PendingIdentityWrite : CodeReservationBindingState.Confirmed,
            (await CodeAsync(fg.CodeReservationId)).BindingState);

        var cancel = await CancelAsync(fg.Id, fg.Version);
        Assert.True(cancel.StatusCode == HttpStatusCode.OK, await cancel.Content.ReadAsStringAsync());
        await DeliverAuditAsync();

        Assert.Equal("Cancelled", await DispositionAsync(fg.Id));
        Assert.Empty((await GskuAsync(gsku)).ChildCreationAdmissions);
        Assert.Equal(CodeReservationBindingState.Confirmed, (await CodeAsync(fg.CodeReservationId)).BindingState);
        var accepted = await RequestAsync(gsku, "retirement");
        Assert.True(accepted.IsSuccessStatusCode, await accepted.Content.ReadAsStringAsync());
    }

    // R1 (b)/(c): the cancel committed but its settling step was cut off — the admission stays. Once the cancellation
    // is audited, the FG (and the code bound to it) holds nothing; the FG operator repair releases the admission.
    [Fact]
    public async Task R1_A_settled_cancelled_fg_does_not_hold_its_admission_and_the_repair_releases_it()
    {
        var gsku = await ApprovedGskuAsync();
        var key = "FG-" + Guid.NewGuid().ToString("N");
        var commandId = key.ToUpperInvariant();
        using (var scope = TenantScope())
        {
            var handler = CreateHandler(scope, gskus: Intercept<IGskuRepository>(
                scope.ServiceProvider.GetRequiredService<IGskuRepository>(),
                (method, _) => method.Name == nameof(IGskuRepository.CompleteChildCreationAdmissionAsync)
                    ? throw new IOException("store went away") : null));
            await Assert.ThrowsAsync<IOException>(() => handler.Handle(Command(gsku, key), CancellationToken.None));
        }
        var fg = await host.Database.GetCollection<FinishedGood>("mdm_finished_goods")
            .Find(x => x.TenantId == _tenant && x.CreationCommandId == commandId).SingleAsync();
        using (var scope = TenantScope())
        {
            var cancel = ActivatorUtilities.CreateInstance<CancelFinishedGoodDraftHandler>(scope.ServiceProvider,
                Intercept<IGskuRepository>(scope.ServiceProvider.GetRequiredService<IGskuRepository>(),
                    (method, _) => method.Name == nameof(IGskuRepository.CompleteChildCreationAdmissionAsync)
                        ? throw new IOException("store went away") : null),
                new LifecycleActor(_maker));
            await Assert.ThrowsAsync<IOException>(() => cancel.Handle(
                new CancelFinishedGoodDraftCommand(fg.Id, fg.Version, Guid.NewGuid(), "CREATED_BY_MISTAKE"),
                CancellationToken.None));
        }
        Assert.Contains(commandId, (await GskuAsync(gsku)).ChildCreationAdmissions.Select(x => x.CreationCommandId));
        await AgeAsync(gsku, commandId);
        // Pending its audit, the cancelled FG still holds: the repair leaves the admission.
        Assert.Equal(0, await RepairAsync());
        await DeliverAuditAsync();

        Assert.Equal(1, await RepairAsync());
        Assert.Empty((await GskuAsync(gsku)).ChildCreationAdmissions);
        Assert.Equal(CodeReservationBindingState.Confirmed, (await CodeAsync(fg.CodeReservationId)).BindingState);
    }

    // ---------------------------------------------------------------------------------------------------- review R2

    // Every clause of the receipt is checked: one that does not match leaves the cancellation PendingAudit (blocking).
    [Theory]
    [InlineData("TenantId")]
    [InlineData("SourceService")]
    [InlineData("IdempotencyKey")]
    [InlineData("ContractVersion")]
    [InlineData("CentralAcknowledgement")]
    [InlineData("CompactReceiptReference")]
    [InlineData("CentralIdempotencyKey")]
    public async Task R2_Each_receipt_clause_that_does_not_match_keeps_the_cancellation_pending(string clause)
    {
        var (gsku, fg) = await AuditedCancellationAsync();
        BsonValue value = clause switch
        {
            "TenantId" => new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard),
            "CentralAcknowledgement" or "CompactReceiptReference" => " ",
            _ => "FORGED"
        };
        await SetReceiptAsync(fg, clause, value);

        Assert.Equal("PendingAudit", await DispositionAsync(fg));
        Assert.Equal("DEPENDENT_IDENTITIES_EXIST", await RefusedAsync(gsku, "retirement"));
    }

    [Theory]
    [InlineData("duplicate-receipt")]
    [InlineData("intent-and-receipt")]
    public async Task R2_Two_receipts_or_an_intent_next_to_its_receipt_keep_the_cancellation_pending(string shape)
    {
        var gsku = await ApprovedGskuAsync();
        var fg = await CreatedFgAsync(gsku);
        Assert.Equal(HttpStatusCode.OK, (await CancelAsync(fg, (await FgAsync(fg)).Version)).StatusCode);
        var before = await RawAsync(fg);
        var intentId = (await FgAsync(fg)).DraftCancellation!.IntentId;
        var intent = before["AuditIntents"].AsBsonArray.Single(x =>
            x["IntentId"].AsGuid == intentId);
        await DeliverAuditAsync();
        Assert.Equal("Cancelled", await DispositionAsync(fg));
        var receipt = (await RawAsync(fg))["AuditIntentReceipts"].AsBsonArray.Single(x => x["IntentId"].AsGuid == intentId);

        await RawCollection.UpdateOneAsync(IdFilter(fg), shape == "duplicate-receipt"
            ? new BsonDocument("$push", new BsonDocument("AuditIntentReceipts", receipt))
            : new BsonDocument("$push", new BsonDocument("AuditIntents", intent)));

        Assert.Equal("PendingAudit", await DispositionAsync(fg));
        Assert.Equal("DEPENDENT_IDENTITIES_EXIST", await RefusedAsync(gsku, "retirement"));
    }

    // A decision without its evidence (no intent id / key / hash) is never Cancelled, whatever receipt sits next to it.
    [Theory]
    [InlineData("IntentId")]
    [InlineData("IdempotencyKey")]
    [InlineData("EvidenceHash")]
    public async Task R2_A_decision_without_its_evidence_keeps_blocking(string fact)
    {
        var (gsku, fg) = await AuditedCancellationAsync();
        BsonValue empty = fact == "IntentId" ? new BsonBinaryData(Guid.Empty, GuidRepresentation.Standard) : "";
        await RawCollection.UpdateOneAsync(IdFilter(fg), new BsonDocument("$set",
            new BsonDocument("DraftCancellation." + fact, empty)));

        Assert.Equal("PendingAudit", await DispositionAsync(fg));
        Assert.Equal("DEPENDENT_IDENTITIES_EXIST", await RefusedAsync(gsku, "retirement"));
    }

    // m6: the decision's facts are derived again — a forged fingerprint next to a valid receipt is not Cancelled.
    [Fact]
    public async Task M6_A_junk_fingerprint_with_a_valid_receipt_keeps_the_cancellation_pending()
    {
        var (gsku, fg) = await AuditedCancellationAsync();
        await RawCollection.UpdateOneAsync(IdFilter(fg), new BsonDocument("$set",
            new BsonDocument("DraftCancellation.Fingerprint", new string('0', 64))));

        Assert.Equal("PendingAudit", await DispositionAsync(fg));
        Assert.Equal("DEPENDENT_IDENTITIES_EXIST", await RefusedAsync(gsku, "retirement"));
    }

    // CAS Eq(DraftCancellation, null): a second cancel at the CURRENT version with a new key is refused — one decision.
    [Fact]
    public async Task R2_A_second_cancel_at_the_current_version_with_a_new_key_is_refused()
    {
        var fg = await CreatedFgAsync(await ApprovedGskuAsync());
        Assert.Equal(HttpStatusCode.OK, (await CancelAsync(fg, (await FgAsync(fg)).Version)).StatusCode);
        var current = await FgAsync(fg);

        var second = await CancelAsync(fg, current.Version);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("FINISHED_GOOD_DRAFT_ALREADY_CANCELLED", Envelope.FirstError(await Envelope.ReadAsync(second)));
        var after = await FgAsync(fg);
        Assert.Equal(current.Version, after.Version);
        Assert.Equal(current.DraftCancellation!.OperationId, after.DraftCancellation!.OperationId);
        Assert.Single(after.AuditIntents, x => x.Operation == ProductAuditOperation.FinishedGoodDraftCancelled);
    }

    // Same key, different facts (reason) — and m1: same key, another actor — are conflicts, never a replay.
    [Theory]
    [InlineData("reason")]
    [InlineData("actor")]
    public async Task R2_The_same_key_with_other_facts_or_another_actor_is_a_conflict(string differs)
    {
        var fg = await CreatedFgAsync(await ApprovedGskuAsync());
        var version = (await FgAsync(fg)).Version;
        var key = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.OK, (await CancelAsync(fg, version, key)).StatusCode);

        var other = differs == "reason"
            ? await SendAsync($"/api/finished-goods/{fg:D}/cancel-draft",
                new { expectedVersion = version, reasonCode = "DUPLICATE" }, MakerToken(), key)
            : await SendAsync($"/api/finished-goods/{fg:D}/cancel-draft",
                new { expectedVersion = version, reasonCode = "CREATED_BY_MISTAKE" },
                host.Token(_tenant, Guid.NewGuid(), MakerPermissions), key);

        Assert.Equal(HttpStatusCode.Conflict, other.StatusCode);
        Assert.Equal("IDEMPOTENCY_KEY_CONFLICT", Envelope.FirstError(await Envelope.ReadAsync(other)));
        var stored = (await FgAsync(fg)).DraftCancellation!;
        Assert.Equal(_maker, stored.RequesterSubjectId);
        Assert.Equal("CREATED_BY_MISTAKE", stored.ReasonCode);
    }

    [Fact]
    public async Task R2_A_full_audit_intent_list_refuses_the_cancel_with_503()
    {
        var fg = await CreatedFgAsync(await ApprovedGskuAsync());
        var raw = await RawAsync(fg);
        var template = raw["AuditIntents"].AsBsonArray[0].AsBsonDocument;
        var full = new BsonArray(Enumerable.Range(0, AuditIntentLimits.MaxPerAggregate).Select(_ =>
        {
            var copy = template.DeepClone().AsBsonDocument;
            copy["IntentId"] = new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard);
            return copy;
        }));
        await RawCollection.UpdateOneAsync(IdFilter(fg), new BsonDocument("$set", new BsonDocument("AuditIntents", full)));

        var refused = await CancelAsync(fg, raw["Version"].AsInt32);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, refused.StatusCode);
        Assert.Equal("AUDIT_INTENT_CAPACITY_REACHED", Envelope.FirstError(await Envelope.ReadAsync(refused)));
        Assert.Null((await FgAsync(fg)).DraftCancellation);
    }

    [Theory]
    [InlineData("other-tenant")]
    [InlineData("out-of-scope")]
    public async Task R2_A_cancel_from_another_tenant_or_out_of_scope_is_not_found(string where)
    {
        var fg = await CreatedFgAsync(await ApprovedGskuAsync());
        var version = (await FgAsync(fg)).Version;
        var token = MakerToken();
        if (where == "other-tenant")
            token = host.Token(Guid.NewGuid(), _maker, MakerPermissions);
        else
            await EnforceScopeAsync(await SeedProductAsync());

        var refused = await SendAsync($"/api/finished-goods/{fg:D}/cancel-draft",
            new { expectedVersion = version, reasonCode = "CREATED_BY_MISTAKE" }, token, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
        Assert.Equal("FINISHED_GOOD_NOT_FOUND", Envelope.FirstError(await Envelope.ReadAsync(refused)));
        Assert.Null((await FgAsync(fg)).DraftCancellation);
    }

    // Request shape: a reason over 128, an unknown field, a missing or malformed Idempotency-Key, and m2 (int.MaxValue).
    [Theory]
    [InlineData("long-reason", "FINISHED_GOOD_DRAFT_CANCEL_INVALID")]
    [InlineData("unmapped", "FINISHED_GOOD_DRAFT_CANCEL_INVALID")]
    [InlineData("no-key", "IDEMPOTENCY_KEY_INVALID")]
    [InlineData("bad-key", "IDEMPOTENCY_KEY_INVALID")]
    [InlineData("max-version", "FINISHED_GOOD_DRAFT_CANCEL_INVALID")]
    public async Task R2_A_malformed_cancel_is_a_400(string shape, string code)
    {
        var fg = await CreatedFgAsync(await ApprovedGskuAsync());
        var version = (await FgAsync(fg)).Version;
        object body = shape switch
        {
            "long-reason" => new { expectedVersion = version, reasonCode = new string('R', 129) },
            "unmapped" => new { expectedVersion = version, reasonCode = "CREATED_BY_MISTAKE", force = true },
            "max-version" => new { expectedVersion = int.MaxValue, reasonCode = "CREATED_BY_MISTAKE" },
            _ => new { expectedVersion = version, reasonCode = "CREATED_BY_MISTAKE" }
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/finished-goods/{fg:D}/cancel-draft")
        {
            Content = JsonContent.Create(body)
        };
        if (shape == "bad-key") request.Headers.Add("Idempotency-Key", "not-a-guid");
        else if (shape != "no-key") request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));

        var refused = await host.Client(MakerToken()).SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal(code, Envelope.FirstError(await Envelope.ReadAsync(refused)));
        Assert.Null((await FgAsync(fg)).DraftCancellation);
    }

    // The gauge counts only cancellations that waited past the 15-minute threshold: a fresh one is not counted.
    [Fact]
    public async Task R2_A_fresh_pending_cancellation_is_not_counted_by_the_gauge()
    {
        var fg = await CreatedFgAsync(await ApprovedGskuAsync());
        var gauge = host.Factory.Services.GetRequiredService<GskuChildAdmissionGauge>();
        var before = await gauge.RecordFinishedGoodCancellationsAsync(CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, (await CancelAsync(fg, (await FgAsync(fg)).Version)).StatusCode);
        var fresh = await gauge.RecordFinishedGoodCancellationsAsync(CancellationToken.None);
        await host.Database.GetCollection<FinishedGood>("mdm_finished_goods").UpdateOneAsync(x => x.Id == fg,
            Builders<FinishedGood>.Update.Set(x => x.DraftCancellation!.RequestedAtUtc,
                DateTimeOffset.UtcNow - GskuChildAdmissionGauge.StaleAfter - TimeSpan.FromMinutes(1)));
        var stale = await gauge.RecordFinishedGoodCancellationsAsync(CancellationToken.None);

        Assert.Equal("PendingAudit", await DispositionAsync(fg));
        Assert.Equal(before, fresh);
        Assert.Equal(before + 1, stale);
    }

    // The blocker reads Ne(IsDeleted, true): a document without the field blocks; a deleted one does not.
    [Theory]
    [InlineData("unset", true)]
    [InlineData("deleted", false)]
    public async Task R2_The_blocker_reads_a_missing_IsDeleted_as_live_and_a_deleted_row_as_gone(string shape, bool blocks)
    {
        var gsku = await ApprovedGskuAsync();
        var fg = await CreatedFgAsync(gsku);
        await RawCollection.UpdateOneAsync(IdFilter(fg), shape == "unset"
            ? new BsonDocument("$unset", new BsonDocument("IsDeleted", ""))
            : new BsonDocument("$set", new BsonDocument("IsDeleted", true)));

        var response = await RequestAsync(gsku, "retirement");

        if (blocks)
        {
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("DEPENDENT_IDENTITIES_EXIST", Envelope.FirstError(await Envelope.ReadAsync(response)));
        }
        else Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }

    // CT decision (ownerless drafts): anyone with the key and the scope may cancel another maker's draft; the
    // decision and its intent carry the canceller.
    [Fact]
    public async Task A_draft_of_another_maker_is_cancelled_and_the_canceller_is_recorded()
    {
        var fg = await CreatedFgAsync(await ApprovedGskuAsync());
        var canceller = Guid.NewGuid();

        var cancel = await SendAsync($"/api/finished-goods/{fg:D}/cancel-draft",
            new { expectedVersion = (await FgAsync(fg)).Version, reasonCode = "CREATED_BY_MISTAKE" },
            host.Token(_tenant, canceller, MakerPermissions), Guid.NewGuid());

        Assert.True(cancel.StatusCode == HttpStatusCode.OK, await cancel.Content.ReadAsStringAsync());
        var stored = await FgAsync(fg);
        Assert.Equal(canceller, stored.DraftCancellation!.RequesterSubjectId);
        Assert.Equal(canceller.ToString("D"),
            stored.AuditIntents.Single(x => x.Operation == ProductAuditOperation.FinishedGoodDraftCancelled).ActorId);
    }

    // ---------------------------------------------------------------------------------------------------- item 7

    // Item 7: a key with a control character is the caller's error — 400 at the door (validator) and 400 from the real
    // store behind the handler (no admission, no code); a store that does not answer is 503, never 202.
    [Fact]
    public async Task Item07_A_control_character_key_is_a_400_at_the_door_and_in_the_store()
    {
        var gsku = await ApprovedGskuAsync();

        var door = await CreateFgAsync(gsku, "a\nb");
        Response<Diten.MdmService.Application.Features.ProductItemSkuMaster.ProductItemSkuMasterModels.FinishedGoodDraftDto> store;
        using (var scope = TenantScope())
            store = await CreateHandler(scope).Handle(Command(gsku, "a\nb"), CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, door.StatusCode);
        Assert.Contains("IDEMPOTENCY_KEY_INVALID", await door.Content.ReadAsStringAsync());
        Assert.Equal(400, store.StatusCode);
        Assert.Contains("IDEMPOTENCY_KEY_INVALID", store.Errors);
        Assert.Empty((await GskuAsync(gsku)).ChildCreationAdmissions);
        Assert.False(await host.Database.GetCollection<CodeReservation>("mdm_code_reservations")
            .Find(x => x.TenantId == _tenant && x.EntityType == CodeBearingEntityType.FinishedGood).AnyAsync());
    }

    [Fact]
    public async Task Item07_A_store_that_does_not_answer_is_a_503_not_a_reconciliation()
    {
        var gsku = await ApprovedGskuAsync();
        using var scope = TenantScope();
        var handler = CreateHandler(scope, finishedGoods: Intercept<IFinishedGoodRepository>(
            scope.ServiceProvider.GetRequiredService<IFinishedGoodRepository>(),
            (method, _) => method.Name == nameof(IFinishedGoodRepository.BindCreationAttemptAsync)
                ? Task.FromResult(FinishedGoodCreationAttemptResult.Unavailable()) : null));

        var refused = await handler.Handle(Command(gsku, "FG-" + Guid.NewGuid().ToString("N")), CancellationToken.None);

        Assert.Equal(503, refused.StatusCode);
        Assert.Contains(ProductIdentityStore.Unavailable, refused.Errors);
        Assert.Empty((await GskuAsync(gsku)).ChildCreationAdmissions);
    }

    // ---------------------------------------------------------------------------------------------------- item 8

    // Item 8: in an Enforced tenant every FG door reads the product scope — the list and the selector show only what is
    // in scope, and the detail, the create and the cancel answer an out-of-scope Finished Good / GSKU as not found.
    [Fact]
    public async Task Item08_In_an_enforced_tenant_every_finished_good_door_keeps_to_the_product_scope()
    {
        var inProduct = await SeedProductAsync();
        var outProduct = await SeedProductAsync();
        var inGsku = await ApprovedGskuAsync(inProduct);
        var outGsku = await ApprovedGskuAsync(outProduct);
        var inFg = await CreatedFgAsync(inGsku);
        var outFg = await CreatedFgAsync(outGsku);
        await EnforceScopeAsync(inProduct);
        var client = host.Client(MakerToken());

        var list = await IdsAsync(await client.GetAsync("/api/finished-goods?pageSize=100"));
        var selector = await IdsAsync(await client.GetAsync("/api/finished-goods/gsku-selector?pageSize=100"));
        var inDetail = await client.GetAsync($"/api/finished-goods/{inFg:D}");
        var outDetail = await client.GetAsync($"/api/finished-goods/{outFg:D}");
        var outCreate = await CreateFgAsync(outGsku, "FG-" + Guid.NewGuid().ToString("N"));
        var inCreate = await CreateFgAsync(inGsku, "FG-" + Guid.NewGuid().ToString("N"));
        var outCancel = await CancelAsync(outFg, (await FgAsync(outFg)).Version);
        var inCancel = await CancelAsync(inFg, (await FgAsync(inFg)).Version);

        Assert.Contains(inFg, list);
        Assert.DoesNotContain(outFg, list);
        Assert.Contains(inGsku, selector);
        Assert.DoesNotContain(outGsku, selector);
        Assert.Equal(HttpStatusCode.OK, inDetail.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, outDetail.StatusCode);
        Assert.Equal("FINISHED_GOOD_NOT_FOUND", Envelope.FirstError(await Envelope.ReadAsync(outDetail)));
        Assert.Equal(HttpStatusCode.NotFound, outCreate.StatusCode);
        Assert.Equal("GSKU_NOT_REFERENCEABLE", Envelope.FirstError(await Envelope.ReadAsync(outCreate)));
        Assert.True(inCreate.StatusCode == HttpStatusCode.Created, await inCreate.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, outCancel.StatusCode);
        Assert.Equal("FINISHED_GOOD_NOT_FOUND", Envelope.FirstError(await Envelope.ReadAsync(outCancel)));
        Assert.True(inCancel.StatusCode == HttpStatusCode.OK, await inCancel.Content.ReadAsStringAsync());
        Assert.Null((await FgAsync(outFg)).DraftCancellation);
    }

    private static async Task<Guid[]> IdsAsync(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await Envelope.ReadAsync(response)).GetProperty("data").GetProperty("items").EnumerateArray()
            .Select(x => x.GetProperty("id").GetGuid()).ToArray();
    }

    // ---------------------------------------------------------------------------------------------------- helpers

    private const string RecoverKey = "mdm.product-identity.lifecycle-operations.recover";

    private Task AgeAsync(Guid gsku, string commandId) => Task.WhenAll(
        host.Database.GetCollection<Gsku>("mdm_gskus").UpdateOneAsync(
            x => x.Id == gsku,
            Builders<Gsku>.Update.Set("ChildCreationAdmissions.$[a].AcquiredAtUtc", DateTimeOffset.UtcNow.AddHours(-2)),
            new UpdateOptions { ArrayFilters = [new BsonDocumentArrayFilterDefinition<BsonDocument>(
                new BsonDocument("a.CreationCommandId", commandId))] }),
        host.Database.GetCollection<CodeReservation>("mdm_code_reservations").UpdateOneAsync(
            x => x.TenantId == _tenant && x.ReservationCommandId == commandId,
            Builders<CodeReservation>.Update.Set(x => x.ConsumedAt, DateTimeOffset.UtcNow.AddHours(-2))));

    private IServiceScope TenantScope()
    {
        var scope = host.Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(_tenant);
        return scope;
    }

    private static Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.CommandHandlers.CreateFinishedGoodDraftHandler
        CreateHandler(IServiceScope scope, ICodeReservationRepository? reservations = null,
            IFinishedGoodRepository? finishedGoods = null, IGskuRepository? gskus = null) =>
        ActivatorUtilities.CreateInstance<
            Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.CommandHandlers.CreateFinishedGoodDraftHandler>(
            scope.ServiceProvider,
            reservations ?? scope.ServiceProvider.GetRequiredService<ICodeReservationRepository>(),
            finishedGoods ?? scope.ServiceProvider.GetRequiredService<IFinishedGoodRepository>(),
            gskus ?? scope.ServiceProvider.GetRequiredService<IGskuRepository>(),
            new HandlerActor());

    private sealed class LifecycleActor(Guid subject) : Diten.MdmService.Application.Contracts.IProductIdentityLifecycleActorContext
    {
        public bool TryResolveCanonicalHumanSubject(out Guid value) { value = subject; return true; }
        public bool HasPermission(string permission) => MakerPermissions.Contains(permission);
    }

    private async Task<int> RepairAsync()
    {
        var repair = await host.Client(host.Token(_tenant, Guid.NewGuid(), [RecoverKey]))
            .PostAsync("/api/finished-goods/operator/child-admission-repairs", null);
        Assert.True(repair.IsSuccessStatusCode, await repair.Content.ReadAsStringAsync());
        return (await Envelope.ReadAsync(repair)).GetProperty("data").GetProperty("released").GetInt32();
    }

    private Task<CodeReservation> CodeAsync(Guid id) =>
        host.Database.GetCollection<CodeReservation>("mdm_code_reservations").Find(x => x.Id == id).SingleAsync();

    private IMongoCollection<BsonDocument> RawCollection => host.Database.GetCollection<BsonDocument>("mdm_finished_goods");

    private static BsonDocument IdFilter(Guid id) => new("_id", new BsonBinaryData(id, GuidRepresentation.Standard));

    private Task<BsonDocument> RawAsync(Guid fg) => RawCollection.Find(IdFilter(fg)).SingleAsync();

    private async Task<(Guid Gsku, Guid Fg)> AuditedCancellationAsync()
    {
        var gsku = await ApprovedGskuAsync();
        var fg = await CreatedFgAsync(gsku);
        Assert.Equal(HttpStatusCode.OK, (await CancelAsync(fg, (await FgAsync(fg)).Version)).StatusCode);
        await DeliverAuditAsync();
        Assert.Equal("Cancelled", await DispositionAsync(fg));
        return (gsku, fg);
    }

    private async Task SetReceiptAsync(Guid fg, string field, BsonValue value)
    {
        var intentId = (await FgAsync(fg)).DraftCancellation!.IntentId;
        await RawCollection.UpdateOneAsync(IdFilter(fg),
            new BsonDocument("$set", new BsonDocument("AuditIntentReceipts.$[r]." + field, value)),
            new UpdateOptions
            {
                ArrayFilters = [new BsonDocumentArrayFilterDefinition<BsonDocument>(
                    new BsonDocument("r.IntentId", new BsonBinaryData(intentId, GuidRepresentation.Standard)))]
            });
    }

    private async Task EnforceScopeAsync(Guid onlyProduct)
    {
        // The subject belongs to one active Legal Entity of the tenant (the list reads the candidates; none = nothing).
        var legalEntity = Guid.NewGuid();
        await host.Database.GetCollection<LegalEntity>("mdm_legal_entities").InsertOneAsync(new LegalEntity
        {
            Id = legalEntity, TenantId = _tenant, Code = "LE-" + legalEntity.ToString("N")[..8], LegalName = "Scope",
            OperationalStatus = LegalEntityOperationalStatus.Active, IsDeleted = false
        });
        host.Scopes.Answer(_tenant, legalEntity);
        var rollout = ProductLegalEntityScopeRolloutState.CreatePreparation(_tenant, Guid.NewGuid(), Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddMinutes(-1));
        rollout.Mode = ProductLegalEntityScopeRolloutMode.Enforced;
        await host.Database.GetCollection<ProductLegalEntityScopeRolloutState>("mdm_product_legal_entity_scope_rollout_states")
            .InsertOneAsync(rollout);
        await host.Database.GetCollection<ProductLegalEntityScopePolicy>("mdm_product_legal_entity_scope_policies")
            .InsertOneAsync(ProductLegalEntityScopePolicy.Create(_tenant, onlyProduct, Guid.NewGuid(),
                ProductLegalEntityScopeMode.GroupWide, [], Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-1)));
    }

    private static Diten.MdmService.Application.Features.ProductItemSkuMaster.Commands.CreateFinishedGoodDraftCommand Command(
        Guid gsku, string key) =>
        new(new Diten.MdmService.Application.Features.ProductItemSkuMaster.ProductItemSkuMasterModels.CreateFinishedGoodDraftRequest
        {
            GskuId = gsku, IdempotencyKey = key
        });

    private sealed class HandlerActor : Diten.MdmService.Application.Contracts.IProductIdentityActorContext
    {
        public string ActorId => "fg-fix1-handler-test";
    }

    private static T Intercept<T>(T inner, Func<System.Reflection.MethodInfo, object?[]?, object?> interceptor) where T : class
    {
        var proxy = System.Reflection.DispatchProxy.Create<T, InterceptProxy>();
        var state = (InterceptProxy)(object)proxy;
        state.Inner = inner;
        state.Interceptor = interceptor;
        return proxy;
    }

    public class InterceptProxy : System.Reflection.DispatchProxy
    {
        public object Inner { get; set; } = null!;
        public Func<System.Reflection.MethodInfo, object?[]?, object?> Interceptor { get; set; } = null!;

        protected override object? Invoke(System.Reflection.MethodInfo? targetMethod, object?[]? args)
        {
            try
            {
                return Interceptor(targetMethod!, args) ?? targetMethod!.Invoke(Inner, args);
            }
            catch (System.Reflection.TargetInvocationException exception) when (exception.InnerException is not null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }
    }



    private string MakerToken() => host.Token(_tenant, _maker, MakerPermissions);

    private async Task<string> RefusedAsync(Guid gsku, string request)
    {
        var response = await RequestAsync(gsku, request);
        Assert.True(response.StatusCode == HttpStatusCode.Conflict, await response.Content.ReadAsStringAsync());
        return Envelope.FirstError(await Envelope.ReadAsync(response))!;
    }

    private async Task<HttpResponseMessage> RequestAsync(Guid gsku, string request)
    {
        var version = (await GskuAsync(gsku)).Version;
        return request == "retirement"
            ? await SendAsync($"/api/gskus/{gsku:D}/retirement-requests",
                new { expectedGskuVersion = version, requestReason = "PRODUCT_DISCONTINUED" }, MakerToken(), Guid.NewGuid())
            : await SendAsync($"/api/gskus/{gsku:D}/correction-requests",
                new { expectedGskuVersion = version, packQuantity = 2.5m, packUomCode = "KGM" }, MakerToken(), Guid.NewGuid());
    }

    private Task<HttpResponseMessage> CancelAsync(Guid fg, int version, Guid? key = null) =>
        SendAsync($"/api/finished-goods/{fg:D}/cancel-draft",
            new { expectedVersion = version, reasonCode = "CREATED_BY_MISTAKE" }, MakerToken(), key ?? Guid.NewGuid());

    private async Task<string> DispositionAsync(Guid fg)
    {
        var raw = await host.Database.GetCollection<BsonDocument>("mdm_finished_goods")
            .Find(new BsonDocument("_id", new BsonBinaryData(fg, GuidRepresentation.Standard))).SingleAsync();
        var trace = _lastDelivery + " intents=" + string.Join(";", raw["AuditIntents"].AsBsonArray.Select(x =>
                $"{x["Operation"]}:{x["DeliveryState"]}:{(x.AsBsonDocument.Contains("LastError") ? x["LastError"] : "")}:{(x.AsBsonDocument.Contains("ContractVersion") ? x["ContractVersion"] : "-")}"))
            + " receipts=" + raw["AuditIntentReceipts"].AsBsonArray.Count;
        Console.WriteLine("FG-TRACE " + trace);
        _trace = trace;
        var detail = await host.Client(MakerToken()).GetAsync($"/api/finished-goods/{fg:D}");
        Assert.True(detail.IsSuccessStatusCode, await detail.Content.ReadAsStringAsync());
        return ((FinishedGoodCancellationDisposition)(await Envelope.ReadAsync(detail)).GetProperty("data")
            .GetProperty("cancellationDisposition").GetInt32()).ToString();
    }

    // The cancellation intents of this tenant go the way the G4 delivery takes them: the production repository claims
    // each one and acknowledges-and-compacts it with the acceptance a central log answers (the receipt is written by
    // the production code). Skipped: the HTTP send and the claimed-payload read, which the repository only serves once
    // the temporal-storage cutover is active (an operational step, global to the database the host's tests share).
    private string _lastDelivery = string.Empty;

    private async Task DeliverAuditAsync()
    {
        using var scope = host.Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(_tenant);
        var repository = scope.ServiceProvider.GetRequiredService<IAuditIntentDeliveryRepository>();
        var work = await repository.DiscoverEligibleAsync(500);
        var compacted = 0;
        foreach (var item in work.Where(x => x.Locator.AggregateType == AuditAggregateType.FinishedGood))
        {
            var fg = await FgAsync(item.Locator.AggregateId);
            if (fg.DraftCancellation?.IntentId != item.Locator.IntentId) continue;
            var claim = await repository.TryClaimAsync(item.Locator, item.ClaimGeneration, "fg-fix1-test", TimeSpan.FromSeconds(30));
            Assert.NotNull(claim);
            var acknowledgement = "fg-fix1-ack-" + item.Locator.IntentId.ToString("N");
            Assert.True(await repository.AcknowledgeAndCompactAsync(claim!, new AuditIntentAcknowledgement(
                acknowledgement,
                AuditIntentContract.BuildCentralIdempotencyKey(_tenant, item.Locator.IntentId,
                    AuditIntentDeliveryProcessor.RequiredContractVersion),
                AuditIntentDeliveryProcessor.RequiredContractVersion, DateTimeOffset.UtcNow), acknowledgement));
            compacted++;
        }
        _lastDelivery = $"compacted={compacted} of {work.Count}";
    }

    private Task<HttpResponseMessage> CreateFgAsync(Guid gsku, string key) =>
        SendAsync("/api/finished-goods/drafts", new { gskuId = gsku, idempotencyKey = key }, MakerToken(), null);

    private async Task<Guid> CreatedFgAsync(Guid gsku)
    {
        var response = await CreateFgAsync(gsku, "FG-" + Guid.NewGuid().ToString("N"));
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await Envelope.ReadAsync(response)).GetProperty("data").GetProperty("finishedGoodId").GetGuid();
    }

    private async Task<Guid> SeedProductAsync()
    {
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await host.Database.GetCollection<GlobalProduct>("mdm_global_products").InsertOneAsync(new GlobalProduct
        {
            Id = id, TenantId = _tenant, CanonicalCode = $"GP-{id:N}"[..16],
            GlobalProductName = $"Product {id:N}", GlobalProductNameNormalized = $"PRODUCT {id:N}".ToUpperInvariant(),
            CodeReservationId = Guid.NewGuid(), LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved,
            CreatedAt = now, UpdatedAt = now
        });
        return id;
    }

    private async Task<Guid> ApprovedGskuAsync(Guid? ofProduct = null)
    {
        var product = ofProduct ?? await SeedProductAsync();
        var created = await SendAsync("/api/gskus/drafts",
            new { globalProductId = product, packQuantity = 1m, packUomCode = "C62" }, MakerToken(), Guid.NewGuid());
        Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var data = (await Envelope.ReadAsync(created)).GetProperty("data");
        var gsku = data.GetProperty("gskuId").GetGuid();
        var submit = await SendAsync($"/api/gskus/{gsku:D}/submit",
            new { expectedVersion = data.GetProperty("version").GetInt32() }, MakerToken(), Guid.NewGuid());
        Assert.True(submit.StatusCode == HttpStatusCode.Accepted, await submit.Content.ReadAsStringAsync());
        host.Engine.Approve(host.Engine.Open("gsku", gsku.ToString("D")), host.Approver);
        for (var cycle = 0; cycle < 40; cycle++)
        {
            await host.RunAsync<FirstGskuIdentityWorkflowRecoveryRunner>(runner => runner.RunCycleAsync());
            if ((await GskuAsync(gsku)).LifecycleStatus == ProductIdentityLifecycleStatus.IdentityApproved) return gsku;
            await Task.Delay(250);
        }
        throw new Xunit.Sdk.XunitException("The GSKU was not approved.");
    }

    private async Task<HttpResponseMessage> SendAsync(string path, object body, string token, Guid? key)
    {
        // TEST ADAPTATION (S3b, R-04): this older flow opts in to the lot / serial flags a submit now requires.
        await S3bSubmitFlags.FillForLegacyFlowAsync(host, path);
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        if (key.HasValue) request.Headers.Add("Idempotency-Key", key.Value.ToString("D"));
        return await host.Client(token).SendAsync(request);
    }

    private Task<Gsku> GskuAsync(Guid id) =>
        host.Database.GetCollection<Gsku>("mdm_gskus").Find(x => x.Id == id && x.TenantId == _tenant).SingleAsync();

    private Task<FinishedGood> FgAsync(Guid id) =>
        host.Database.GetCollection<FinishedGood>("mdm_finished_goods")
            .Find(x => x.Id == id && x.TenantId == _tenant).SingleAsync();
}
