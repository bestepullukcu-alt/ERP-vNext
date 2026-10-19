using System.Net;
using System.Text.Json;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Handlers.CommandHandlers;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using Diten.MdmService.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// WP-MDM-FG-ACCEPT-01 FIX1 part B (P-2026-10-07-FG-FIX1-B): the FG insert writes its reservation in its own transaction,
/// the burn gate is open for FG with the settled-cancellation rule, the race is closed (the LSKU Item05c pattern), and
/// part A's review findings, over HTTP / the real handlers and a throwaway replica-set mongod.
/// </summary>
public sealed partial class FgFix1HttpTests
{
    // ---------------------------------------------------------------------------------------------------- items 1–3

    // (a) the burn read "no live row" and is held at its barrier; the FG insert (its own transaction, writing the
    // reservation) commits meanwhile → the burn is refused. (b) a burn that committed first → the insert is refused.
    // Never a live Finished Good with a burned code.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Item03_A_burn_and_an_fg_insert_racing_never_leave_a_live_row_with_a_burned_code(bool burnReadsFirst)
    {
        var gsku = await ApprovedGskuAsync();
        var (captured, fingerprint) = await CutOffBeforeTheRowAsync(gsku);
        var code = await CodeAsync(captured.CodeReservationId);
        var codes = new CodeReservationRepository(host.Database, new Tenant(_tenant));
        var rows = new FinishedGoodRepository(host.Database, new Tenant(_tenant));
        ReservationOperationResult? burned = null;
        FinishedGoodCreateResult? inserted = null;

        if (burnReadsFirst)
        {
            codes.AfterBoundRowCheckForTests = async () => inserted = await rows.CreateDraftWithAdmissionAsync(captured, fingerprint);
            burned = await codes.BurnIdentityBindingAsync(code.Id, captured.Id, code.Version, "fg-race:burn", "fg-race", "race", "TEST");
        }
        else
        {
            burned = await codes.BurnIdentityBindingAsync(code.Id, captured.Id, code.Version, "fg-race:burn", "fg-race", "race", "TEST");
            inserted = await rows.CreateDraftWithAdmissionAsync(captured, fingerprint);
        }

        var finalCode = await CodeAsync(code.Id);
        var finalRow = await host.Database.GetCollection<FinishedGood>("mdm_finished_goods")
            .Find(x => x.Id == captured.Id).FirstOrDefaultAsync();
        Assert.False(finalRow is not null && !finalRow.IsDeleted && finalCode.BindingState == CodeReservationBindingState.Burned,
            "a live Finished Good with a burned code");
        if (burnReadsFirst)
        {
            Assert.True(inserted!.Succeeded, inserted.ErrorCode);
            Assert.False(burned!.Succeeded);
            // FG FIX2 item 2 (F5): the burn is refused BECAUSE the insert's reservation write conflicted with its own.
            Assert.Equal("CONCURRENCY_CONFLICT", burned.ErrorCode);
            Assert.NotNull(finalRow);
        }
        else
        {
            Assert.True(burned!.Succeeded, burned.ErrorCode);
            Assert.False(inserted!.Succeeded);
            // Refused before its transaction (the code is no longer live) or inside it (the burn won the touch).
            Assert.Contains(inserted.ErrorCode, new[] { "CODE_RESERVATION_REQUIRED", "CODE_RESERVATION_BURNED" });
            Assert.Null(finalRow);
        }
    }

    // Item 2: a (pending) code bound to a LIVE Finished Good is not burned; once its cancellation is SETTLED it can be.
    // (A confirmed code is never burned at all — the store's binding rule; the gate decides only for a pending one.)
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Item02_The_gate_keeps_a_live_fgs_code_and_burns_one_whose_cancellation_is_settled(bool settled)
    {
        var gsku = await ApprovedGskuAsync();
        var fg = await PendingCodeFgAsync(gsku);
        if (settled)
        {
            // Cancelled with its confirm refused (the code stays pending), then audited.
            var row0 = await FgAsync(fg);
            using (var scope = TenantScope())
            {
                var cancel = ActivatorUtilities.CreateInstance<CancelFinishedGoodDraftHandler>(scope.ServiceProvider,
                    Intercept<ICodeReservationRepository>(scope.ServiceProvider.GetRequiredService<ICodeReservationRepository>(),
                        (method, _) => method.Name == nameof(ICodeReservationRepository.ConfirmIdentityBindingAsync)
                            ? Task.FromResult(new ReservationOperationResult(false, null, "CONCURRENCY_CONFLICT")) : null),
                    new LifecycleActor(_maker));
                await cancel.Handle(new CancelFinishedGoodDraftCommand(fg, row0.Version, Guid.NewGuid(), "CREATED_BY_MISTAKE"),
                    CancellationToken.None);
            }
            await DeliverAuditAsync();
            Assert.Equal("Cancelled", await DispositionAsync(fg));
        }
        var row = await FgAsync(fg);
        var code = await CodeAsync(row.CodeReservationId);
        Assert.Equal(CodeReservationBindingState.PendingIdentityWrite, code.BindingState);

        var burned = await new CodeReservationRepository(host.Database, new Tenant(_tenant)).BurnIdentityBindingAsync(
            code.Id, fg, code.Version, "fg-gate:burn", "fg-gate", "gate", "TEST");

        if (settled)
        {
            Assert.True(burned.Succeeded, burned.ErrorCode);
            Assert.Equal(CodeReservationBindingState.Burned, (await CodeAsync(code.Id)).BindingState);
        }
        else
        {
            Assert.False(burned.Succeeded);
            Assert.Equal(CodeReservationBurnCodes.BoundRowExists, burned.ErrorCode);
        }
    }

    // ---------------------------------------------------------------------------------------------------- item 6 (R1 c)

    // A Finished Good cut off with its admission held, cancelled with its settling step refused (409 NOT_SETTLED): the
    // REPLAY of its create key finishes the creation first (completes the admission), then answers 409 cancelled.
    [Fact]
    public async Task Item06_The_create_key_of_a_cancelled_fg_completes_its_admission_before_the_409()
    {
        var gsku = await ApprovedGskuAsync();
        var key = "FG-" + Guid.NewGuid().ToString("N");
        using (var scope = TenantScope())
        {
            var handler = CreateHandler(scope, gskus: Intercept<IGskuRepository>(
                scope.ServiceProvider.GetRequiredService<IGskuRepository>(),
                (method, _) => method.Name == nameof(IGskuRepository.CompleteChildCreationAdmissionAsync)
                    ? throw new IOException("store went away") : null));
            await Assert.ThrowsAsync<IOException>(() => handler.Handle(Command(gsku, key), CancellationToken.None));
        }
        var fg = await host.Database.GetCollection<FinishedGood>("mdm_finished_goods")
            .Find(x => x.TenantId == _tenant && x.CreationCommandId == key.ToUpperInvariant()).SingleAsync();
        using (var scope = TenantScope())
        {
            var cancel = ActivatorUtilities.CreateInstance<CancelFinishedGoodDraftHandler>(scope.ServiceProvider,
                Intercept<IGskuRepository>(scope.ServiceProvider.GetRequiredService<IGskuRepository>(),
                    (method, _) => method.Name == nameof(IGskuRepository.CompleteChildCreationAdmissionAsync)
                        ? Task.FromResult(new GskuChildCreationAdmissionResult(false, false, null, "GSKU_CHILD_ADMISSION_CONFLICT"))
                        : null),
                new LifecycleActor(_maker));
            var refused = await cancel.Handle(new CancelFinishedGoodDraftCommand(fg.Id, fg.Version, Guid.NewGuid(), "CREATED_BY_MISTAKE"),
                CancellationToken.None);
            Assert.Equal(409, refused.StatusCode);
            Assert.Contains(CancelFinishedGoodDraftHandler.CreationNotSettled, refused.Errors);
        }
        Assert.Contains(key.ToUpperInvariant(), (await GskuAsync(gsku)).ChildCreationAdmissions.Select(x => x.CreationCommandId));

        var replay = await CreateFgAsync(gsku, key);

        Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);
        Assert.Equal("FINISHED_GOOD_DRAFT_CANCELLED", Envelope.FirstError(await Envelope.ReadAsync(replay)));
        Assert.DoesNotContain(key.ToUpperInvariant(), (await GskuAsync(gsku)).ChildCreationAdmissions.Select(x => x.CreationCommandId));
    }

    // ---------------------------------------------------------------------------------------------------- item 7

    // A crash between ReserveAsync and ConsumeForIdentityAsync: the code stays Reserved and keeps the admission (the
    // release refuses while it is live). Past the cutoff the FG repair consumes it as an FG code, burns it (through the
    // gate) and releases the admission — the named way out.
    [Fact]
    public async Task Item07_A_crash_at_consume_leaves_a_reserved_code_that_the_repair_burns_by_age_then_releases()
    {
        var gsku = await ApprovedGskuAsync();
        var key = "FG-" + Guid.NewGuid().ToString("N");
        var commandId = key.ToUpperInvariant();
        using (var scope = TenantScope())
        {
            var handler = CreateHandler(scope, reservations: Intercept<ICodeReservationRepository>(
                scope.ServiceProvider.GetRequiredService<ICodeReservationRepository>(),
                (method, _) => method.Name == nameof(ICodeReservationRepository.ConsumeForIdentityAsync)
                    ? throw new IOException("store went away") : null));
            await Assert.ThrowsAsync<IOException>(() => handler.Handle(Command(gsku, key), CancellationToken.None));
        }
        var codes = host.Database.GetCollection<CodeReservation>("mdm_code_reservations");
        var code = await codes.Find(x => x.TenantId == _tenant && x.ReservationCommandId == commandId).SingleAsync();
        Assert.Equal(CodeReservationState.Reserved, code.ReservationState);
        Assert.Contains(commandId, (await GskuAsync(gsku)).ChildCreationAdmissions.Select(x => x.CreationCommandId));
        Assert.Equal(0, await RepairAsync()); // not yet past the cutoff
        await AgeAsync(gsku, commandId);
        await codes.UpdateOneAsync(x => x.Id == code.Id,
            Builders<CodeReservation>.Update.Set(x => x.ReservedAt, DateTimeOffset.UtcNow.AddHours(-2)));

        Assert.Equal(1, await RepairAsync());

        Assert.DoesNotContain(commandId, (await GskuAsync(gsku)).ChildCreationAdmissions.Select(x => x.CreationCommandId));
        var after = await CodeAsync(code.Id);
        Assert.Equal(CodeReservationState.Consumed, after.ReservationState);
        Assert.Equal(CodeReservationBindingState.Burned, after.BindingState);
    }

    // ---------------------------------------------------------------------------------------------------- item 8 (legacy)

    // A Finished Good written before IsDeleted existed (the field missing): it is read (m5), listed in an Enforced tenant,
    // and its cancel completes its admission (the durable-fingerprint read uses Ne(true)).
    [Fact]
    public async Task Item08_A_fg_without_IsDeleted_is_read_listed_enforced_and_its_cancel_completes()
    {
        var product = await SeedProductAsync();
        var gsku = await ApprovedGskuAsync(product);
        var key = "FG-" + Guid.NewGuid().ToString("N");
        using (var scope = TenantScope())
        {
            var handler = CreateHandler(scope, gskus: Intercept<IGskuRepository>(
                scope.ServiceProvider.GetRequiredService<IGskuRepository>(),
                (method, _) => method.Name == nameof(IGskuRepository.CompleteChildCreationAdmissionAsync)
                    ? throw new IOException("store went away") : null));
            await Assert.ThrowsAsync<IOException>(() => handler.Handle(Command(gsku, key), CancellationToken.None));
        }
        var fg = await host.Database.GetCollection<FinishedGood>("mdm_finished_goods")
            .Find(x => x.TenantId == _tenant && x.CreationCommandId == key.ToUpperInvariant()).SingleAsync();
        await RawCollection.UpdateOneAsync(IdFilter(fg.Id), new BsonDocument("$unset", new BsonDocument("IsDeleted", "")));

        var detail = await host.Client(MakerToken()).GetAsync($"/api/finished-goods/{fg.Id:D}");
        var cancel = await CancelAsync(fg.Id, fg.Version);
        await EnforceScopeAsync(product);
        var list = await IdsAsync(await host.Client(MakerToken()).GetAsync("/api/finished-goods?pageSize=100"));

        Assert.True(detail.IsSuccessStatusCode, await detail.Content.ReadAsStringAsync());
        Assert.True(cancel.StatusCode == HttpStatusCode.OK, await cancel.Content.ReadAsStringAsync());
        Assert.DoesNotContain(key.ToUpperInvariant(), (await GskuAsync(gsku)).ChildCreationAdmissions.Select(x => x.CreationCommandId));
        Assert.Contains(fg.Id, list);
    }

    // ---------------------------------------------------------------------------------------------------- item 9 (m7)

    // The create key of a cancelled Draft is a terminal conflict in the REPOSITORY, on both paths: the existing row read
    // by its command, and the duplicate-key path (another command meeting the cancelled row by its code).
    [Theory]
    [InlineData("existing")]
    [InlineData("duplicate")]
    public async Task Item09_The_repository_answers_a_cancelled_drafts_create_as_cancelled(string path)
    {
        var gsku = await ApprovedGskuAsync();
        var fg = await CreatedFgAsync(gsku);
        Assert.Equal(HttpStatusCode.OK, (await CancelAsync(fg, (await FgAsync(fg)).Version)).StatusCode);
        var row = await FgAsync(fg);
        var again = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<FinishedGood>(row.ToBsonDocument());
        again.DraftCancellation = null;
        again.IsDeleted = false;
        again.AuditIntents = [again.AuditIntents.First(x => x.Operation != ProductAuditOperation.FinishedGoodDraftCancelled)];
        again.AuditIntentReceipts = [];
        if (path == "duplicate")
        {
            again.CreationCommandId = "FG-DUP-" + Guid.NewGuid().ToString("N").ToUpperInvariant();
            await host.Database.GetCollection<Gsku>("mdm_gskus").UpdateOneAsync(x => x.Id == gsku,
                Builders<Gsku>.Update.Push(x => x.ChildCreationAdmissions, new GskuChildCreationAdmission
                {
                    ChildKind = GskuChildIdentityKind.FinishedGood, CreationCommandId = again.CreationCommandId,
                    RequestFingerprint = GskuChildCreationAdmission.ComputeRequestFingerprint(
                        gsku, GskuChildIdentityKind.FinishedGood, again.CreationCommandId),
                    AcquiredAtUtc = DateTimeOffset.UtcNow
                }));
        }

        var result = await new FinishedGoodRepository(host.Database, new Tenant(_tenant)).CreateDraftWithAdmissionAsync(again,
            GskuChildCreationAdmission.ComputeRequestFingerprint(gsku, GskuChildIdentityKind.FinishedGood, again.CreationCommandId));

        Assert.False(result.Succeeded);
        Assert.Equal("FINISHED_GOOD_DRAFT_CANCELLED", result.ErrorCode);
    }

    // ---------------------------------------------------------------------------------------------------- K1, items 11–13

    // K1: the cancel's confirm is refused because the CREATOR confirmed the code to this Finished Good meanwhile — the
    // code is re-read: confirmed to this FG is settled, the cancel answers 200 (no false 409).
    [Fact]
    public async Task K1_A_confirm_lost_to_the_creators_own_confirm_is_not_a_409()
    {
        var gsku = await ApprovedGskuAsync();
        var fg = await PendingCodeFgAsync(gsku);
        var row = await FgAsync(fg);
        using var scope = TenantScope();
        var real = scope.ServiceProvider.GetRequiredService<ICodeReservationRepository>();
        var cancel = ActivatorUtilities.CreateInstance<CancelFinishedGoodDraftHandler>(scope.ServiceProvider,
            Intercept<ICodeReservationRepository>(real, (method, args) =>
            {
                if (method.Name != nameof(ICodeReservationRepository.ConfirmIdentityBindingAsync)) return null;
                // The creator's confirm wins the race (the real write), then this confirm is refused.
                var code = real.GetByIdAsync(row.CodeReservationId).GetAwaiter().GetResult()!;
                real.ConfirmIdentityBindingAsync(code.Id, fg, code.Version, "creator:confirm", "creator", "creator")
                    .GetAwaiter().GetResult();
                return Task.FromResult(new ReservationOperationResult(false, null, "CONCURRENCY_CONFLICT"));
            }),
            new LifecycleActor(_maker));

        var result = await cancel.Handle(new CancelFinishedGoodDraftCommand(fg, row.Version, Guid.NewGuid(), "CREATED_BY_MISTAKE"),
            CancellationToken.None);

        Assert.True(result.StatusCode == 200, string.Join(',', result.Errors));
        Assert.Equal(CodeReservationBindingState.Confirmed, (await CodeAsync(row.CodeReservationId)).BindingState);
    }

    // Item 11 (CT F1): a pending code consumed by ANOTHER identity is never confirmed to this Finished Good by its cancel.
    [Fact]
    public async Task Item11_The_cancel_never_confirms_a_code_consumed_by_another_identity()
    {
        var gsku = await ApprovedGskuAsync();
        var fg = await PendingCodeFgAsync(gsku);
        var row = await FgAsync(fg);
        var other = Guid.NewGuid();
        await host.Database.GetCollection<CodeReservation>("mdm_code_reservations").UpdateOneAsync(
            x => x.Id == row.CodeReservationId, Builders<CodeReservation>.Update.Set(x => x.ConsumedEntityId, other));

        var confirms = 0;
        using (var scope = TenantScope())
        {
            // Measured at the handler: the store would refuse a mismatched confirm too (a second layer), so the rule
            // is that the cancel never even ASKS the store to confirm a code consumed by another identity.
            var cancel = ActivatorUtilities.CreateInstance<CancelFinishedGoodDraftHandler>(scope.ServiceProvider,
                Intercept<ICodeReservationRepository>(scope.ServiceProvider.GetRequiredService<ICodeReservationRepository>(),
                    (method, _) =>
                    {
                        if (method.Name == nameof(ICodeReservationRepository.ConfirmIdentityBindingAsync)) confirms++;
                        return null;
                    }),
                new LifecycleActor(_maker));
            await cancel.Handle(new CancelFinishedGoodDraftCommand(fg, row.Version, Guid.NewGuid(), "CREATED_BY_MISTAKE"),
                CancellationToken.None);
        }

        Assert.Equal(0, confirms);
        var code = await CodeAsync(row.CodeReservationId);
        Assert.Equal(CodeReservationBindingState.PendingIdentityWrite, code.BindingState);
        Assert.Equal(other, code.ConsumedEntityId);
    }

    // Item 12 (CT F2): the admission completion refused → 409 FINISHED_GOOD_CREATION_NOT_SETTLED; the SAME cancel again
    // finishes it (200, admission gone).
    [Fact]
    public async Task Item12_A_refused_completion_is_named_and_the_same_cancel_finishes_it()
    {
        var gsku = await ApprovedGskuAsync();
        var fg = await PendingCodeFgAsync(gsku);
        var row = await FgAsync(fg);
        var operation = Guid.NewGuid();
        using (var scope = TenantScope())
        {
            var cancel = ActivatorUtilities.CreateInstance<CancelFinishedGoodDraftHandler>(scope.ServiceProvider,
                Intercept<IGskuRepository>(scope.ServiceProvider.GetRequiredService<IGskuRepository>(),
                    (method, _) => method.Name == nameof(IGskuRepository.CompleteChildCreationAdmissionAsync)
                        ? Task.FromResult(new GskuChildCreationAdmissionResult(false, false, null, "GSKU_CHILD_ADMISSION_CONFLICT"))
                        : null),
                new LifecycleActor(_maker));
            var refused = await cancel.Handle(new CancelFinishedGoodDraftCommand(fg, row.Version, operation, "CREATED_BY_MISTAKE"),
                CancellationToken.None);
            Assert.Equal(409, refused.StatusCode);
            Assert.Contains(CancelFinishedGoodDraftHandler.CreationNotSettled, refused.Errors);
        }
        Assert.Contains(row.CreationCommandId, (await GskuAsync(gsku)).ChildCreationAdmissions.Select(x => x.CreationCommandId));

        var again = await CancelAsync(fg, row.Version, operation);

        Assert.True(again.StatusCode == HttpStatusCode.OK, await again.Content.ReadAsStringAsync());
        Assert.DoesNotContain(row.CreationCommandId, (await GskuAsync(gsku)).ChildCreationAdmissions.Select(x => x.CreationCommandId));
    }

    // Item 13 (CT F3 + F4): a cancellation PENDING its audit is not an end. (row path, F4) the repair leaves the admission
    // of a cancelled-pending FG; (code path, F3) a code bound to a cancelled-pending FG — reached without a row of the
    // command — holds its admission: the repair does not even treat it as an orphan.
    [Theory]
    [InlineData("row")]
    [InlineData("code")]
    public async Task Item13_A_cancellation_pending_its_audit_still_holds(string path)
    {
        var gsku = await ApprovedGskuAsync();
        var key = "FG-" + Guid.NewGuid().ToString("N");
        var commandId = key.ToUpperInvariant();
        // A creation cut off with its row written, its code pending and its admission held.
        using (var scope = TenantScope())
        {
            var handler = CreateHandler(scope, reservations: Intercept<ICodeReservationRepository>(
                scope.ServiceProvider.GetRequiredService<ICodeReservationRepository>(),
                (method, _) => method.Name == nameof(ICodeReservationRepository.ConfirmIdentityBindingAsync)
                    ? throw new IOException("store went away") : null));
            await Assert.ThrowsAsync<IOException>(() => handler.Handle(Command(gsku, key), CancellationToken.None));
        }
        var fg = await host.Database.GetCollection<FinishedGood>("mdm_finished_goods")
            .Find(x => x.TenantId == _tenant && x.CreationCommandId == commandId).SingleAsync();
        // Its cancellation commits but stays PendingAudit; its settle steps are refused (code pending, admission held).
        using (var scope = TenantScope())
        {
            var cancel = ActivatorUtilities.CreateInstance<CancelFinishedGoodDraftHandler>(scope.ServiceProvider,
                Intercept<ICodeReservationRepository>(scope.ServiceProvider.GetRequiredService<ICodeReservationRepository>(),
                    (method, _) => method.Name == nameof(ICodeReservationRepository.ConfirmIdentityBindingAsync)
                        ? Task.FromResult(new ReservationOperationResult(false, null, "CONCURRENCY_CONFLICT")) : null),
                new LifecycleActor(_maker));
            Assert.Equal(409, (await cancel.Handle(new CancelFinishedGoodDraftCommand(fg.Id, fg.Version, Guid.NewGuid(),
                "CREATED_BY_MISTAKE"), CancellationToken.None)).StatusCode);
        }
        Assert.Equal("PendingAudit", await DispositionAsync(fg.Id));
        if (path == "row")
            // Only the ROW can hold here: its code is no longer live (burned), so the code path holds nothing — a
            // cancellation pending its audit must keep the admission by itself.
            await host.Database.GetCollection<CodeReservation>("mdm_code_reservations").UpdateOneAsync(
                x => x.Id == fg.CodeReservationId,
                Builders<CodeReservation>.Update.Set(x => x.BindingState, CodeReservationBindingState.Burned));
        if (path == "code")
            // The row is no longer found under the admission's command — only its code leads to it.
            await RawCollection.UpdateOneAsync(IdFilter(fg.Id),
                new BsonDocument("$set", new BsonDocument("CreationCommandId", "FG-ELSEWHERE-" + Guid.NewGuid().ToString("N"))));
        await AgeAsync(gsku, commandId);

        var repair = await host.Client(host.Token(_tenant, Guid.NewGuid(), [RecoverKey]))
            .PostAsync("/api/finished-goods/operator/child-admission-repairs", null);

        Assert.True(repair.IsSuccessStatusCode, await repair.Content.ReadAsStringAsync());
        var data = (await Envelope.ReadAsync(repair)).GetProperty("data");
        Assert.DoesNotContain(data.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("creationCommandId").GetString() == commandId);
        Assert.Contains(commandId, (await GskuAsync(gsku)).ChildCreationAdmissions.Select(x => x.CreationCommandId));
        if (path == "code")
            Assert.Equal(CodeReservationBindingState.PendingIdentityWrite, (await CodeAsync(fg.CodeReservationId)).BindingState);
    }

    // K2: a SETTLED-cancelled Finished Good whose code is still pending: the repair's named exit burns that code (through
    // the gate — the row holds nothing any more) and releases the admission; the code is never left pending for good.
    [Fact]
    public async Task K2_The_repair_burns_the_pending_code_of_a_settled_cancelled_fg_and_releases()
    {
        var gsku = await ApprovedGskuAsync();
        var key = "FG-" + Guid.NewGuid().ToString("N");
        var commandId = key.ToUpperInvariant();
        using (var scope = TenantScope())
        {
            var handler = CreateHandler(scope, reservations: Intercept<ICodeReservationRepository>(
                scope.ServiceProvider.GetRequiredService<ICodeReservationRepository>(),
                (method, _) => method.Name == nameof(ICodeReservationRepository.ConfirmIdentityBindingAsync)
                    ? throw new IOException("store went away") : null));
            await Assert.ThrowsAsync<IOException>(() => handler.Handle(Command(gsku, key), CancellationToken.None));
        }
        var fg = await host.Database.GetCollection<FinishedGood>("mdm_finished_goods")
            .Find(x => x.TenantId == _tenant && x.CreationCommandId == commandId).SingleAsync();
        using (var scope = TenantScope())
        {
            var cancel = ActivatorUtilities.CreateInstance<CancelFinishedGoodDraftHandler>(scope.ServiceProvider,
                Intercept<ICodeReservationRepository>(scope.ServiceProvider.GetRequiredService<ICodeReservationRepository>(),
                    (method, _) => method.Name == nameof(ICodeReservationRepository.ConfirmIdentityBindingAsync)
                        ? Task.FromResult(new ReservationOperationResult(false, null, "CONCURRENCY_CONFLICT")) : null),
                new LifecycleActor(_maker));
            await cancel.Handle(new CancelFinishedGoodDraftCommand(fg.Id, fg.Version, Guid.NewGuid(), "CREATED_BY_MISTAKE"),
                CancellationToken.None);
        }
        await DeliverAuditAsync();
        Assert.Equal("Cancelled", await DispositionAsync(fg.Id));
        await AgeAsync(gsku, commandId);

        Assert.Equal(1, await RepairAsync());

        Assert.Equal(CodeReservationBindingState.Burned, (await CodeAsync(fg.CodeReservationId)).BindingState);
        Assert.DoesNotContain(commandId, (await GskuAsync(gsku)).ChildCreationAdmissions.Select(x => x.CreationCommandId));
    }

    // ---------------------------------------------------------------------------------------------------- Web review (MDM side)

    // Found by the Web review: the row and its code are written and the admission completion is refused — the answer is
    // 202 reconciliation (the same key finishes it), never a 409 that sends the user to a reload's NEW key (a second
    // Finished Good). The same key then completes it: 201, one row, the admission gone.
    [Fact]
    public async Task A_refused_completion_after_the_row_is_written_is_202_and_the_same_key_finishes_it()
    {
        var gsku = await ApprovedGskuAsync();
        var key = "FG-" + Guid.NewGuid().ToString("N");
        using (var scope = TenantScope())
        {
            var handler = CreateHandler(scope, gskus: Intercept<IGskuRepository>(
                scope.ServiceProvider.GetRequiredService<IGskuRepository>(),
                (method, _) => method.Name == nameof(IGskuRepository.CompleteChildCreationAdmissionAsync)
                    ? Task.FromResult(new GskuChildCreationAdmissionResult(false, false, null, "GSKU_CHILD_ADMISSION_CONFLICT"))
                    : null));
            var first = await handler.Handle(Command(gsku, key), CancellationToken.None);
            Assert.Equal(202, first.StatusCode);
            Assert.Contains("FINISHED_GOOD_BINDING_RECONCILIATION_REQUIRED", first.Errors);
        }

        var replay = await CreateFgAsync(gsku, key);

        Assert.True(replay.StatusCode == HttpStatusCode.Created, await replay.Content.ReadAsStringAsync());
        Assert.Equal(1L, await host.Database.GetCollection<FinishedGood>("mdm_finished_goods")
            .CountDocumentsAsync(x => x.TenantId == _tenant && x.CreationCommandId == key.ToUpperInvariant()));
        Assert.DoesNotContain(key.ToUpperInvariant(), (await GskuAsync(gsku)).ChildCreationAdmissions.Select(x => x.CreationCommandId));
    }

    // ---------------------------------------------------------------------------------------------------- helpers (part B)

    // A creation cut off after its code was consumed and before its row: the row it would have written, captured.
    private async Task<(FinishedGood Row, string Fingerprint)> CutOffBeforeTheRowAsync(Guid gsku)
    {
        using var scope = TenantScope();
        FinishedGood? captured = null;
        string? fingerprint = null;
        var handler = CreateHandler(scope, finishedGoods: Intercept<IFinishedGoodRepository>(
            scope.ServiceProvider.GetRequiredService<IFinishedGoodRepository>(), (method, args) =>
            {
                if (method.Name != nameof(IFinishedGoodRepository.CreateDraftWithAdmissionAsync)) return null;
                captured = (FinishedGood)args![0]!;
                fingerprint = (string)args[1]!;
                throw new IOException("cut off before the row");
            }));
        await Assert.ThrowsAsync<IOException>(() => handler.Handle(Command(gsku, "FG-" + Guid.NewGuid().ToString("N")),
            CancellationToken.None));
        return (captured!, fingerprint!);
    }

    // A Finished Good whose row is written and whose code is still pending (the confirm cut off), admission held.
    private async Task<Guid> PendingCodeFgAsync(Guid gsku)
    {
        var key = "FG-" + Guid.NewGuid().ToString("N");
        using (var scope = TenantScope())
        {
            var handler = CreateHandler(scope, reservations: Intercept<ICodeReservationRepository>(
                scope.ServiceProvider.GetRequiredService<ICodeReservationRepository>(),
                (method, _) => method.Name == nameof(ICodeReservationRepository.ConfirmIdentityBindingAsync)
                    ? throw new IOException("store went away") : null));
            await Assert.ThrowsAsync<IOException>(() => handler.Handle(Command(gsku, key), CancellationToken.None));
        }
        return (await host.Database.GetCollection<FinishedGood>("mdm_finished_goods")
            .Find(x => x.TenantId == _tenant && x.CreationCommandId == key.ToUpperInvariant()).SingleAsync()).Id;
    }

    private sealed class Tenant(Guid tenantId) : ITenantContext
    {
        public Guid TenantId { get; private set; } = tenantId;
        public bool IsResolved => TenantId != Guid.Empty;
        public void SetTenant(Guid tenantId) => TenantId = tenantId;
    }
}
