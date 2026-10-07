using System.Net;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.CommandHandlers;
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
/// WP-MDM-FG-ACCEPT-01 FIX2 (P-2026-10-07-FG-FIX2): the FG insert's transient / unknown / unavailable paths, the
/// in-transaction refusal of the race, the gate on legacy and pending-audit Finished Goods, and the refusals the same
/// key can still finish — over HTTP / the real handlers and a throwaway replica-set mongod.
/// </summary>
public sealed partial class FgFix1HttpTests
{
    // ---------------------------------------------------------------------------------------------------- item 1

    // Every insert attempt meets a transient transaction error: retried with backoff, then ambiguous (202) — the code
    // stays pending, the admission held, no row.
    [Fact]
    public async Task Fix2_Item01_A_transient_insert_error_to_the_last_attempt_is_ambiguous_and_burns_nothing()
    {
        var gsku = await ApprovedGskuAsync();
        var key = "FG-" + Guid.NewGuid().ToString("N");
        var armed = false;
        var rows = new FinishedGoodRepository(host.Database, new Tenant(_tenant))
        {
            BeforeReservationWriteForTests = async () =>
            {
                if (armed) return;
                armed = true;
                await FailPointAsync("insert", 112, "TransientTransactionError", FinishedGoodRepository.InsertAttempts);
            }
        };
        var clock = System.Diagnostics.Stopwatch.StartNew();
        int status;
        try
        {
            using var scope = TenantScope();
            status = (await CreateHandler(scope, finishedGoods: rows).Handle(Command(gsku, key), CancellationToken.None)).StatusCode;
        }
        finally { await FailPointOffAsync(); }

        Assert.True(armed);
        Assert.Equal(202, status);
        Assert.True(clock.Elapsed >= TimeSpan.FromMilliseconds(650), $"no backoff: {clock.Elapsed.TotalMilliseconds} ms");
        await AssertPendingHeldNoRowAsync(gsku, key.ToUpperInvariant());
    }

    // A commit whose result is unknown (UnknownTransactionCommitResult): ambiguous (202), nothing burned.
    [Fact]
    public async Task Fix2_Item01_A_commit_with_an_unknown_result_is_ambiguous_and_burns_nothing()
    {
        var gsku = await ApprovedGskuAsync();
        var key = "FG-" + Guid.NewGuid().ToString("N");
        var armed = false;
        var rows = new FinishedGoodRepository(host.Database, new Tenant(_tenant))
        {
            BeforeReservationWriteForTests = async () =>
            {
                if (armed) return;
                armed = true;
                // 91 ShutdownInProgress: a server error only the commit-unknown label classifies.
                await FailPointAsync("commitTransaction", 91, "UnknownTransactionCommitResult", 2);
            }
        };
        int status;
        try
        {
            using var scope = TenantScope();
            status = (await CreateHandler(scope, finishedGoods: rows).Handle(Command(gsku, key), CancellationToken.None)).StatusCode;
        }
        finally { await FailPointOffAsync(); }

        Assert.True(armed);
        Assert.Equal(202, status);
        await AssertPendingHeldNoRowAsync(gsku, key.ToUpperInvariant());
    }

    // A definite write refusal whose burn cannot run (no transactions) → 503, the admission kept with the live code.
    [Fact]
    public async Task Fix2_Item01_A_burn_without_transactions_is_a_503()
    {
        var gsku = await ApprovedGskuAsync();
        var key = "FG-" + Guid.NewGuid().ToString("N");
        using var scope = TenantScope();
        var handler = CreateHandler(scope,
            reservations: Intercept<ICodeReservationRepository>(scope.ServiceProvider.GetRequiredService<ICodeReservationRepository>(),
                (method, _) => method.Name == nameof(ICodeReservationRepository.BurnIdentityBindingAsync)
                    ? Task.FromResult(new ReservationOperationResult(false, null, CodeReservationBurnCodes.TransactionUnavailable))
                    : null),
            finishedGoods: Intercept<IFinishedGoodRepository>(scope.ServiceProvider.GetRequiredService<IFinishedGoodRepository>(),
                (method, _) => method.Name == nameof(IFinishedGoodRepository.CreateDraftWithAdmissionAsync)
                    ? Task.FromResult(new FinishedGoodCreateResult(false, null, "FINISHED_GOOD_WRITE_REFUSED")) : null));

        var refused = await handler.Handle(Command(gsku, key), CancellationToken.None);

        Assert.Equal(503, refused.StatusCode);
        Assert.Contains(CodeReservationBurnCodes.TransactionUnavailable, refused.Errors);
        Assert.Contains(key.ToUpperInvariant(), (await GskuAsync(gsku)).ChildCreationAdmissions.Select(x => x.CreationCommandId));
    }

    // ---------------------------------------------------------------------------------------------------- item 2

    // The burn lands BETWEEN the insert's pre-check and its transaction's reservation write: the insert is refused
    // inside its transaction with exactly CODE_RESERVATION_BURNED, and no row is left.
    [Fact]
    public async Task Fix2_Item02_A_burn_between_the_precheck_and_the_transaction_is_refused_inside_it()
    {
        var gsku = await ApprovedGskuAsync();
        var (captured, fingerprint) = await CutOffBeforeTheRowAsync(gsku);
        var code = await CodeAsync(captured.CodeReservationId);
        ReservationOperationResult? burned = null;
        var rows = new FinishedGoodRepository(host.Database, new Tenant(_tenant))
        {
            BeforeReservationWriteForTests = async () => burned ??= await new CodeReservationRepository(host.Database,
                new Tenant(_tenant)).BurnIdentityBindingAsync(code.Id, captured.Id, code.Version, "fg-fix2:burn", "fg-fix2",
                "race", "TEST")
        };

        var inserted = await rows.CreateDraftWithAdmissionAsync(captured, fingerprint);

        Assert.True(burned!.Succeeded, burned.ErrorCode);
        Assert.False(inserted.Succeeded);
        Assert.Equal("CODE_RESERVATION_BURNED", inserted.ErrorCode);
        Assert.Null(await host.Database.GetCollection<FinishedGood>("mdm_finished_goods")
            .Find(x => x.Id == captured.Id).FirstOrDefaultAsync());
    }

    // ---------------------------------------------------------------------------------------------------- items 3 + 5 (CT P3)

    // The gate on a pending code: a legacy Finished Good (no IsDeleted field) holds it; one whose cancellation is
    // PENDING its audit holds it; once settled it does not.
    [Theory]
    [InlineData("legacy", false)]
    [InlineData("pending-audit", false)]
    [InlineData("settled", true)]
    public async Task Fix2_Item03_05_The_gate_holds_a_legacy_or_pending_audit_fgs_code_and_releases_a_settled_one(
        string shape, bool burns)
    {
        var gsku = await ApprovedGskuAsync();
        var fg = await PendingCodeFgAsync(gsku);
        if (shape == "legacy")
            await RawCollection.UpdateOneAsync(IdFilter(fg), new BsonDocument("$unset", new BsonDocument("IsDeleted", "")));
        else
        {
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
            if (shape == "settled") await DeliverAuditAsync();
            Assert.Equal(shape == "settled" ? "Cancelled" : "PendingAudit", await DispositionAsync(fg));
        }
        var raw = await RawAsync(fg);
        var code = await CodeAsync(raw["CodeReservationId"].AsGuid);
        Assert.Equal(CodeReservationBindingState.PendingIdentityWrite, code.BindingState);

        var burned = await new CodeReservationRepository(host.Database, new Tenant(_tenant)).BurnIdentityBindingAsync(
            code.Id, fg, code.Version, "fg-fix2-gate:burn", "fg-fix2", "gate", "TEST");

        if (burns)
            Assert.True(burned.Succeeded, burned.ErrorCode);
        else
        {
            Assert.False(burned.Succeeded);
            Assert.Equal(CodeReservationBurnCodes.BoundRowExists, burned.ErrorCode);
        }
    }

    // ---------------------------------------------------------------------------------------------------- item 4 (MDM) + K3

    // A transient consume refusal is said as one the SAME key finishes (the code stays reserved for this command); the
    // repeat with the same key consumes it and completes: one row.
    [Fact]
    public async Task Fix2_Item04_A_transient_consume_refusal_names_the_same_key_and_its_repeat_completes()
    {
        var gsku = await ApprovedGskuAsync();
        var key = "FG-" + Guid.NewGuid().ToString("N");
        using (var scope = TenantScope())
        {
            var handler = CreateHandler(scope, reservations: Intercept<ICodeReservationRepository>(
                scope.ServiceProvider.GetRequiredService<ICodeReservationRepository>(),
                (method, _) => method.Name == nameof(ICodeReservationRepository.ConsumeForIdentityAsync)
                    ? Task.FromResult(new ReservationOperationResult(false, null, "CONCURRENCY_CONFLICT")) : null));
            var refused = await handler.Handle(Command(gsku, key), CancellationToken.None);
            Assert.Equal(409, refused.StatusCode);
            Assert.Equal(["CONCURRENCY_CONFLICT", CreateFinishedGoodDraftHandler.RetrySameKey], refused.Errors);
        }

        var repeat = await CreateFgAsync(gsku, key);

        Assert.True(repeat.StatusCode == HttpStatusCode.Created, await repeat.Content.ReadAsStringAsync());
        Assert.Equal(1L, await host.Database.GetCollection<FinishedGood>("mdm_finished_goods")
            .CountDocumentsAsync(x => x.TenantId == _tenant && x.CreationCommandId == key.ToUpperInvariant()));
        Assert.Single(await host.Database.GetCollection<CodeReservation>("mdm_code_reservations")
            .Find(x => x.TenantId == _tenant && x.ReservationCommandId == key.ToUpperInvariant()).ToListAsync());
    }

    // A definite consume refusal is NOT marked (a new key is right there).
    [Fact]
    public async Task Fix2_Item04_A_definite_consume_refusal_is_not_marked_for_the_same_key()
    {
        var gsku = await ApprovedGskuAsync();
        using var scope = TenantScope();
        var handler = CreateHandler(scope, reservations: Intercept<ICodeReservationRepository>(
            scope.ServiceProvider.GetRequiredService<ICodeReservationRepository>(),
            (method, _) => method.Name == nameof(ICodeReservationRepository.ConsumeForIdentityAsync)
                ? Task.FromResult(new ReservationOperationResult(false, null, "IDENTITY_RESERVATION_CONFLICT")) : null));

        var refused = await handler.Handle(Command(gsku, "FG-" + Guid.NewGuid().ToString("N")), CancellationToken.None);

        Assert.Equal(409, refused.StatusCode);
        Assert.DoesNotContain(CreateFinishedGoodDraftHandler.RetrySameKey, refused.Errors);
    }

    // K3: the row of this command is written and a later scope check refuses it (the product left the user's scope
    // meanwhile) — a refusal the same key can still finish: marked, so the screen keeps the key.
    [Fact]
    public async Task Fix2_K3_A_refusal_after_the_row_is_written_is_marked_for_the_same_key()
    {
        var product = await SeedProductAsync();
        var gsku = await ApprovedGskuAsync(product);
        var key = "FG-" + Guid.NewGuid().ToString("N");
        using (var scope = TenantScope())
        {
            var handler = CreateHandler(scope, reservations: Intercept<ICodeReservationRepository>(
                scope.ServiceProvider.GetRequiredService<ICodeReservationRepository>(),
                (method, _) => method.Name == nameof(ICodeReservationRepository.ConfirmIdentityBindingAsync)
                    ? throw new IOException("store went away") : null));
            await Assert.ThrowsAsync<IOException>(() => handler.Handle(Command(gsku, key), CancellationToken.None));
        }
        await EnforceScopeAsync(await SeedProductAsync()); // the FG's product is now out of scope

        var replay = await CreateFgAsync(gsku, key);

        Assert.Equal(HttpStatusCode.NotFound, replay.StatusCode);
        var body = await replay.Content.ReadAsStringAsync();
        Assert.Contains(CreateFinishedGoodDraftHandler.RetrySameKey, body);
    }

    // ---------------------------------------------------------------------------------------------------- helpers (FIX2)

    private async Task FailPointAsync(string command, int errorCode, string label, int times) =>
        await host.Database.Client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument
        {
            { "configureFailPoint", "failCommand" },
            { "mode", new BsonDocument("times", times) },
            { "data", new BsonDocument
                {
                    { "failCommands", new BsonArray { command } },
                    { "errorCode", errorCode },
                    { "errorLabels", new BsonArray { label } }
                }
            }
        });

    private async Task FailPointOffAsync() =>
        await host.Database.Client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument
        {
            { "configureFailPoint", "failCommand" }, { "mode", "off" }
        });

    private async Task AssertPendingHeldNoRowAsync(Guid gsku, string commandId)
    {
        var code = await host.Database.GetCollection<CodeReservation>("mdm_code_reservations")
            .Find(x => x.TenantId == _tenant && x.ReservationCommandId == commandId).SingleAsync();
        Assert.Equal(CodeReservationBindingState.PendingIdentityWrite, code.BindingState);
        Assert.Contains(commandId, (await GskuAsync(gsku)).ChildCreationAdmissions.Select(x => x.CreationCommandId));
        Assert.False(await host.Database.GetCollection<FinishedGood>("mdm_finished_goods")
            .Find(x => x.TenantId == _tenant && x.CreationCommandId == commandId).AnyAsync());
    }
}
