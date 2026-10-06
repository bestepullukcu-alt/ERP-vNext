using System.Net;
using System.Net.Http.Json;
using Diten.MdmService.Api.Services.ProductItemSkuMaster;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using Diten.MdmService.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// WP-MDM-LSKU-ACCEPT-01 FIX3 — items 2–8, 14 and M2–M6 and CT's L9 over HTTP; FIX2's / FIX1's description follows:
/// WP-MDM-LSKU-ACCEPT-01 FIX2 — items 1–13 and CT's sabotage gaps (L2–L15) over HTTP; FIX1's description follows:
/// WP-MDM-LSKU-ACCEPT-01 FIX1 — the LSKU blockers over HTTP against the real MDM Api and a throwaway mongod (the GSKU
/// round's host): what the maker and the worker meet, read back from the store. Engine answers are the ones the real
/// client produces (FakePlatformCodeGuardTests holds every fed code to Platform's own).
/// </summary>
[Collection(MdmHttpCollection.Name)]
public sealed partial class LskuFix3HttpTests(MdmHttpHost host)
{
    private static readonly string[] MakerPermissions =
    [
        "mdm.gskus.read", "mdm.gskus.create", "mdm.gskus.update", "mdm.gskus.submit", "mdm.gskus.withdraw",
        "mdm.gskus.request-correction", "mdm.gskus.request-retirement",
        "mdm.lskus.read", "mdm.lskus.create", "mdm.lskus.submit", "mdm.lskus.withdraw", "mdm.lskus.request-retirement"
    ];

    private const string RecoverKey = "mdm.product-identity.lifecycle-operations.recover";

    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _maker = Guid.NewGuid();

    // ---------------------------------------------------------------------------------------------------- item 2

    // A provider that keeps refusing THIS SERVICE (403): the approval waits, bounded and counted — at the limit it is
    // still waiting where it was, Retryable, with RETRY_LIMIT_REACHED, and the LSKU_IDENTITY gauge counts it; once the
    // cause is removed it completes and the streak resets.
    [Fact]
    public async Task Item02_A_provider_refusing_the_service_waits_to_its_limit_counted_then_completes()
    {
        var gsku = await ApprovedGskuAsync();
        var lsku = await CreatedLskuAsync(gsku, "AM");
        var version = (await LskuAsync(lsku)).Version;
        Assert.Equal(HttpStatusCode.Accepted, (await SubmitAsync(lsku, version)).StatusCode);
        host.Markets.Refusals["AM"] = (403, "REFERENCE_PROVIDER_FORBIDDEN");
        try
        {
            host.Engine.Approve(host.Engine.Open("lsku", lsku.ToString("D")), host.Approver);
            LskuIdentityWorkflowOperation operation = await IdentityOperationAsync(lsku, version);
            for (var cycle = 0; cycle < 45 && operation.LastFailureCode != LskuIdentityWorkflowProcessor.RetryLimitReached; cycle++)
            {
                await DueNowAsync(lsku);
                await host.RunAsync<LskuIdentityWorkflowRecoveryRunner>(runner => runner.RunCycleAsync());
                operation = await IdentityOperationAsync(lsku, version);
            }

            Assert.Equal(LskuIdentityWorkflowProcessor.RetryLimitReached, operation.LastFailureCode);
            Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable, operation.RecoveryDisposition);
            Assert.Equal(LskuIdentityWorkflowCheckpoint.DecisionObserved, operation.Checkpoint);
            Assert.True(operation.FailedAttemptCount >= ProductIdentityRecoveryStreak.DefaultQuarantineAfterFailedAttempts);
            await host.RunAsync<LskuIdentityWorkflowRecoveryRunner>(runner => runner.RunCycleAsync());
            Assert.True(host.Factory.Services.GetRequiredService<ProductIdentityRecoveryMetrics>()
                .Current(LskuIdentityWorkflowRecoveryRunner.Flow) >= 1);
            Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval, (await LskuAsync(lsku)).LifecycleStatus);
        }
        finally
        {
            host.Markets.Refusals.TryRemove("AM", out _);
        }

        await DueNowAsync(lsku);
        var done = await IdentityCyclesUntilAsync(lsku, version, op => op.Checkpoint == LskuIdentityWorkflowCheckpoint.Completed);
        Assert.Equal(0, done.FailedAttemptCount);
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, (await LskuAsync(lsku)).LifecycleStatus);
    }

    // R3: a withdrawal whose cancel went unanswered and that only its requester can send again is chased by a person
    // once it is older than the threshold — parked for the maker (AMR), or a legacy WOU row without the new field.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Item02_An_unanswered_withdrawal_past_the_threshold_needs_a_person(bool parkedForMaker)
    {
        var gsku = await ApprovedGskuAsync();
        var lsku = await CreatedLskuAsync(gsku, parkedForMaker ? "WA" : "WB");
        var version = (await LskuAsync(lsku)).Version;
        Assert.Equal(HttpStatusCode.Accepted, (await SubmitAsync(lsku, version)).StatusCode);
        host.Engine.NextCancelFails = ProductIdentityWorkflowTransportOutcome.Timeout;
        await SendAsync(HttpMethod.Post, $"/api/lskus/{lsku:D}/identity-approval/withdraw",
            new { expectedVersion = version + 1, reasonCode = "CHANGED_MIND" }, MakerToken());
        if (parkedForMaker)
            await IdentityCyclesUntilAsync(lsku, version, op => op.Checkpoint == LskuIdentityWorkflowCheckpoint.AwaitingMakerReplay);
        var gauge = host.Factory.Services.GetRequiredService<LskuIdentityGauge>();
        var metrics = host.Factory.Services.GetRequiredService<ProductIdentityRecoveryMetrics>();
        await gauge.RecordAsync(CancellationToken.None);
        var before = metrics.NeedingAPerson(LskuIdentityWorkflowRecoveryRunner.Flow) ?? 0;
        var operations = host.Database.GetCollection<BsonDocument>("mdm_lsku_identity_workflow_operations");
        var filter = new BsonDocument("LskuId", new BsonBinaryData(lsku, GuidRepresentation.Standard));
        var old = DateTimeOffset.UtcNow.AddHours(-2).UtcTicks;
        await operations.UpdateOneAsync(filter, parkedForMaker
            ? new BsonDocument("$set", new BsonDocument("WithdrawalUnansweredSinceUtcTicksV1", old))
            // A legacy WOU row (written before the field): it ages from its last update.
            : new BsonDocument
            {
                { "$set", new BsonDocument { { "Checkpoint", (int)LskuIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown },
                                             { "UpdatedAtUtcTicksV1", old } } },
                { "$unset", new BsonDocument("WithdrawalUnansweredSinceUtcTicksV1", "") }
            });

        await gauge.RecordAsync(CancellationToken.None);

        Assert.Equal(before + 1, metrics.NeedingAPerson(LskuIdentityWorkflowRecoveryRunner.Flow));
    }

    // ---------------------------------------------------------------------------------------------------- items 3 / 15

    // The closure is ONE write: no separate fence release can fail between it and the cancel (it is never called) —
    // the submit ends with its own audit intent, the draft is cancelled.
    [Fact]
    public async Task Item03_The_draft_cancel_closes_a_parked_submit_in_one_write_with_its_own_audit_intent()
    {
        var (lsku, version) = await ParkedNeverSentAsync("CA");
        using var scope = TenantScope();
        var handler = CancelHandler(scope, lskus: Intercept<ILskuRepository>(scope.ServiceProvider.GetRequiredService<ILskuRepository>(),
            (method, _) => method.Name == nameof(ILskuRepository.ReleaseIdentitySubmitFenceAsync)
                ? throw new IOException("the separate release is never needed") : null));

        var cancelled = await handler.Handle(new(lsku, version, Guid.NewGuid(), "NOT_NEEDED"), CancellationToken.None);

        Assert.True(cancelled.IsSuccessful, string.Join(",", cancelled.Errors));
        var closed = await IdentityOperationAsync(lsku, version);
        Assert.Equal(LskuIdentityWorkflowCheckpoint.AbandonedBeforeWorkflowStart, closed.Checkpoint);
        Assert.Equal(LskuIdentityWorkflowProcessor.ClosedByDraftCancel, closed.LastFailureCode);
        var row = await host.Database.GetCollection<Lsku>("mdm_lskus").Find(x => x.Id == lsku).SingleAsync();
        Assert.True(row.IsDeleted);
        Assert.Null(row.ActiveLifecycleOperation);
        Assert.Single(row.AuditIntents, x => x.Operation == ProductAuditOperation.LskuIdentitySubmitAbandoned);
        Assert.Single(row.AuditIntents, x => x.Operation == ProductAuditOperation.LskuDraftCancelled);
    }

    // A fence still held by a submit that has ENDED (left by an older, two-write closure): the cancel gives it back;
    // a release that fails once fails that cancel, and the same cancel again succeeds.
    [Fact]
    public async Task Item03_A_fence_left_by_an_ended_submit_is_given_back_by_the_cancel()
    {
        var (lsku, version) = await ParkedNeverSentAsync("CB");
        var operationId = (await IdentityOperationAsync(lsku, version)).OperationId;
        // As the FIX2 two-write closure left it after a crash between its writes: the submit ended, its fence held.
        await host.Database.GetCollection<LskuIdentityWorkflowOperation>("mdm_lsku_identity_workflow_operations")
            .UpdateOneAsync(x => x.OperationId == operationId, Builders<LskuIdentityWorkflowOperation>.Update
                .Set(x => x.Checkpoint, LskuIdentityWorkflowCheckpoint.AbandonedBeforeWorkflowStart)
                .Set(x => x.RecoveryDisposition, ProductIdentityWorkflowRecoveryDisposition.None));
        Assert.NotNull((await LskuAsync(lsku)).ActiveLifecycleOperation);
        var calls = 0;
        var key = Guid.NewGuid();
        using (var scope = TenantScope())
        {
            var failing = CancelHandler(scope, lskus: Intercept<ILskuRepository>(scope.ServiceProvider.GetRequiredService<ILskuRepository>(),
                (method, _) => method.Name == nameof(ILskuRepository.ReleaseIdentitySubmitFenceAsync) && calls++ == 0
                    ? throw new IOException("cut off between the two writes") : null));
            await Assert.ThrowsAsync<IOException>(() =>
                failing.Handle(new(lsku, version, key, "NOT_NEEDED"), CancellationToken.None));
        }

        var again = await SendAsync(HttpMethod.Post, $"/api/lskus/{lsku:D}/draft/cancel",
            new { expectedVersion = version, reasonCode = "NOT_NEEDED" }, MakerToken(), key);

        Assert.True(again.StatusCode == HttpStatusCode.OK, await again.Content.ReadAsStringAsync());
        Assert.True((await host.Database.GetCollection<Lsku>("mdm_lskus").Find(x => x.Id == lsku).SingleAsync()).IsDeleted);
    }

    // ---------------------------------------------------------------------------------------------------- item 4

    // The cancel read the submit as "never sent"; before its claim the maker's replay sent a start and parked it
    // again. The decision is made UNDER the claim on the fresh document: the start may be live — refused.
    [Fact]
    public async Task Item04_A_maker_replay_between_the_cancels_read_and_its_claim_refuses_the_cancel()
    {
        var (lsku, version) = await ParkedNeverSentAsync("CC");
        var submitKey = _parkedKeys[lsku];
        using var scope = TenantScope();
        var real = scope.ServiceProvider.GetRequiredService<ILskuIdentityWorkflowOperationRepository>();
        var raced = false;
        var operations = Intercept<ILskuIdentityWorkflowOperationRepository>(real, (method, args) =>
        {
            if (method.Name != nameof(ILskuIdentityWorkflowOperationRepository.TryClaimAsync) || raced) return null;
            raced = true;
            return RaceThenClaimAsync();
            async Task<LskuIdentityWorkflowClaim?> RaceThenClaimAsync()
            {
                host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Forbidden, "WORKFLOW_TRUSTED_CONSUMER_FORBIDDEN", false);
                await SendAsync(HttpMethod.Post, $"/api/lskus/{lsku:D}/submit", new { expectedVersion = version }, MakerToken(), submitKey);
                return await real.TryClaimAsync((LskuIdentityWorkflowClaimRequest)args![0]!, (CancellationToken)args[1]!);
            }
        });
        var processor = ActivatorUtilities.CreateInstance<LskuIdentityWorkflowProcessor>(scope.ServiceProvider, operations);
        var handler = CancelHandler(scope, processor: processor);

        var cancel = await handler.Handle(new(lsku, version, Guid.NewGuid(), "NOT_NEEDED"), CancellationToken.None);

        Assert.True(raced);
        Assert.Equal(409, cancel.StatusCode);
        Assert.Contains("LSKU_SUBMIT_IN_FLIGHT", cancel.Errors);
        var operation = await IdentityOperationAsync(lsku, version);
        Assert.NotEqual(LskuIdentityWorkflowCheckpoint.AbandonedBeforeWorkflowStart, operation.Checkpoint);
        Assert.True(operation.LastStartSentAtUtcTicksV1 > 0);
        Assert.False((await LskuAsync(lsku)).IsDeleted);
    }

    // ---------------------------------------------------------------------------------------------------- item 5

    // The key's code and admission belong to the request (G1, M1) of this maker. The same key from another request —
    // another GSKU or market, another user — is IDEMPOTENCY_KEY_CONFLICT and burns nothing; a provider 503 on the
    // maker's own repeat burns nothing either; the right repeat then goes on to 201.
    [Fact]
    public async Task Item05_Only_the_same_request_and_a_definite_refusal_burn_the_keys_code()
    {
        var g1 = await ApprovedGskuAsync();
        var draftGsku = await DraftGskuAsync();
        var key = Guid.NewGuid();
        var commandId = key.ToString("D").ToUpperInvariant();
        using (var scope = TenantScope())
        {
            var reservations = scope.ServiceProvider.GetRequiredService<ICodeReservationRepository>();
            // The maker's own code (the HTTP actor is the token's subject).
            var reserved = await reservations.ReserveAsync(CodeBearingEntityType.Lsku, commandId, _maker.ToString("D"), commandId);
            Assert.True((await reservations.ConsumeForIdentityAsync(reserved.Id, CodeBearingEntityType.Lsku, Guid.NewGuid(),
                reserved.Version, commandId, _maker.ToString("D"), commandId)).Succeeded);
        }
        await PushAdmissionAsync(g1, commandId, "MA", DateTimeOffset.UtcNow.AddMinutes(-1));

        var otherGsku = await CreateLskuAsync(draftGsku, "MB", key);
        var otherUser = await SendAsync(HttpMethod.Post, "/api/lskus/drafts", new { gskuId = draftGsku, marketCode = "MA" },
            host.Token(_tenant, Guid.NewGuid(), MakerPermissions), key);
        host.Markets.Refusals["MA"] = (503, "REFERENCE_PROVIDER_UNAVAILABLE");
        HttpResponseMessage waiting;
        try { waiting = await CreateLskuAsync(g1, "MA", key); }
        finally { host.Markets.Refusals.TryRemove("MA", out _); }

        Assert.Equal("IDEMPOTENCY_KEY_CONFLICT", Envelope.FirstError(await Envelope.ReadAsync(otherGsku)));
        Assert.Equal("IDEMPOTENCY_KEY_CONFLICT", Envelope.FirstError(await Envelope.ReadAsync(otherUser)));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, waiting.StatusCode);
        var code = await host.Database.GetCollection<CodeReservation>("mdm_code_reservations")
            .Find(x => x.TenantId == _tenant && x.ReservationCommandId == commandId).SingleAsync();
        Assert.Equal(CodeReservationBindingState.PendingIdentityWrite, code.BindingState);
        Assert.Contains(commandId, await AdmissionsAsync(g1));

        var right = await CreateLskuAsync(g1, "MA", key);

        Assert.True(right.StatusCode == HttpStatusCode.Created, await right.Content.ReadAsStringAsync());
    }

    // ---------------------------------------------------------------------------------------------------- item 7

    // The gauge's worker is registered by the REAL Program (captured before the host removes hosted services), on by
    // default, and run through real DI it records — with every LSKU worker left out.
    [Fact]
    public async Task Item07_The_gauge_worker_is_registered_on_by_default_and_records()
    {
        Assert.Contains(typeof(GskuChildAdmissionGaugeWorker), host.RegisteredHostedServices);
        Assert.True(host.Factory.Services.GetRequiredService<IOptions<GskuChildAdmissionGaugeOptions>>().Value.Enabled);
        var gsku = await ApprovedGskuAsync();
        await PushAdmissionAsync(gsku, "GAUGE-" + Guid.NewGuid().ToString("N"), "GA", DateTimeOffset.UtcNow.AddHours(-2));
        var worker = ActivatorUtilities.CreateInstance<GskuChildAdmissionGaugeWorker>(host.Factory.Services);
        var metrics = host.Factory.Services.GetRequiredService<ProductIdentityRecoveryMetrics>();

        await worker.StartAsync(CancellationToken.None);
        try
        {
            for (var attempt = 0; attempt < 40 && (metrics.Current(GskuChildAdmissionGauge.Flow) ?? 0) < 1; attempt++)
                await Task.Delay(100);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }

        Assert.True(metrics.Current(GskuChildAdmissionGauge.Flow) >= 1);
        Assert.NotNull(metrics.Current(LskuIdentityWorkflowRecoveryRunner.Flow));
    }

    // ---------------------------------------------------------------------------------------------------- item 8

    // The reservation writes 0 (= tracked, nothing sent): without it every new submit would read as "legacy, may have
    // been sent" and the proven-not-sent path would never run.
    [Fact]
    public async Task Item08_A_reserved_submit_starts_with_the_never_sent_mark()
    {
        var gsku = await ApprovedGskuAsync();
        var lsku = await LskuAsync(await CreatedLskuAsync(gsku, "RV"));
        var parent = await GskuAsync(gsku);
        var revision = await host.Database.GetCollection<ProductDefinitionRevision>("mdm_product_definition_revisions")
            .Find(x => x.Id == parent.ProductDefinitionRevisionId).SingleAsync();

        var plan = host.Factory.Services.GetRequiredService<LskuIdentityWorkflowStartRequestFactory>()
            .Create(_tenant, lsku, parent, revision, Guid.NewGuid(), lsku.Version, _maker);

        Assert.Equal(0, plan.Operation.LastStartSentAtUtcTicksV1);
    }

    // The proven-not-sent branch closes WITHOUT asking the engine (a lookup failure scripted for it stays unused).
    [Fact]
    public async Task Item08_A_never_sent_submit_is_closed_without_a_lookup()
    {
        var (lsku, version) = await ParkedNeverSentAsync("CD");
        host.Engine.NextLookupFailure = (ProductIdentityWorkflowTransportOutcome.Forbidden, "WORKFLOW_TRUSTED_CONSUMER_TENANT_MISMATCH");
        try
        {
            var cancel = await SendAsync(HttpMethod.Post, $"/api/lskus/{lsku:D}/draft/cancel",
                new { expectedVersion = version, reasonCode = "NOT_NEEDED" }, MakerToken());

            Assert.True(cancel.StatusCode == HttpStatusCode.OK, await cancel.Content.ReadAsStringAsync());
            Assert.NotNull(host.Engine.NextLookupFailure);
        }
        finally
        {
            host.Engine.NextLookupFailure = null;
        }
    }

    // A cancel that threw after its commit (before its burn and release) is finished by the same cancel again; a burn
    // the store refuses is named and keeps the admission.
    [Fact]
    public async Task Item08_A_cancel_cut_off_after_its_commit_is_finished_by_its_repeat_and_a_refused_burn_is_named()
    {
        var gsku = await ApprovedGskuAsync();
        var first = await PendingCreationAsync(gsku, "CE");
        var key = Guid.NewGuid();
        using (var scope = TenantScope())
        {
            var throwing = CancelHandler(scope, reservations: Intercept<ICodeReservationRepository>(
                scope.ServiceProvider.GetRequiredService<ICodeReservationRepository>(),
                (method, _) => method.Name == nameof(ICodeReservationRepository.BurnIdentityBindingAsync)
                    ? throw new IOException("cut off after the commit") : null));
            await Assert.ThrowsAsync<IOException>(() =>
                throwing.Handle(new(first.Lsku, first.Version, key, "NOT_NEEDED"), CancellationToken.None));
        }
        Assert.True((await host.Database.GetCollection<Lsku>("mdm_lskus").Find(x => x.Id == first.Lsku).SingleAsync()).IsDeleted);

        var repeat = await SendAsync(HttpMethod.Post, $"/api/lskus/{first.Lsku:D}/draft/cancel",
            new { expectedVersion = first.Version, reasonCode = "NOT_NEEDED" }, MakerToken(), key);

        Assert.True(repeat.StatusCode == HttpStatusCode.OK, await repeat.Content.ReadAsStringAsync());
        Assert.True((await Envelope.ReadAsync(repeat)).GetProperty("data").GetProperty("isReplay").GetBoolean());
        Assert.Equal(CodeReservationBindingState.Burned, (await ReservationOfAsync(first.Lsku)).BindingState);
        Assert.DoesNotContain(first.Command, await AdmissionsAsync(gsku));

        var second = await PendingCreationAsync(gsku, "CF");
        using (var scope = TenantScope())
        {
            var refusing = CancelHandler(scope, reservations: Intercept<ICodeReservationRepository>(
                scope.ServiceProvider.GetRequiredService<ICodeReservationRepository>(),
                (method, _) => method.Name == nameof(ICodeReservationRepository.BurnIdentityBindingAsync)
                    ? Task.FromResult(new ReservationOperationResult(false, null, "CONCURRENCY_CONFLICT")) : null));
            var refused = await refusing.Handle(new(second.Lsku, second.Version, Guid.NewGuid(), "NOT_NEEDED"), CancellationToken.None);

            Assert.Equal(409, refused.StatusCode);
            Assert.Contains(Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.CommandHandlers
                .CreateLskuDraftHandler.BurnRefused, refused.Errors);
        }
        Assert.Contains(second.Command, await AdmissionsAsync(gsku));
    }

    // needing_a_person is ONLY what the requester's repeat cannot settle: a stuck cause the repeat settles is not in it,
    // nor is a submit parked for its maker (that is manual_reconciliation only).
    [Fact]
    public async Task Item08_Needing_a_person_leaves_out_what_the_requesters_repeat_settles()
    {
        var gauge = host.Factory.Services.GetRequiredService<LskuIdentityGauge>();
        var metrics = host.Factory.Services.GetRequiredService<ProductIdentityRecoveryMetrics>();
        await gauge.RecordAsync(CancellationToken.None);
        var manualBefore = metrics.Current(LskuIdentityWorkflowRecoveryRunner.Flow) ?? 0;
        var personBefore = metrics.NeedingAPerson(LskuIdentityWorkflowRecoveryRunner.Flow) ?? 0;
        var settled = await ParkedNeverSentAsync("CG");
        await ParkAsync(settled.Lsku, "LSKU_IDENTITY_WORKFLOW_EVIDENCE_CONFLICT");
        await ParkedNeverSentAsync("CH");

        await gauge.RecordAsync(CancellationToken.None);

        Assert.Equal(manualBefore + 2, metrics.Current(LskuIdentityWorkflowRecoveryRunner.Flow));
        Assert.Equal(personBefore, metrics.NeedingAPerson(LskuIdentityWorkflowRecoveryRunner.Flow));
    }

    // The store's fence alone refuses: the handler's own check is bypassed (it sees no unfinished submit) while a live
    // submit holds the fence — the cancel is still refused, and the fence of a LIVE submit is never "healed".
    [Fact]
    public async Task Item08_The_fence_of_a_live_submit_refuses_the_cancel_on_its_own()
    {
        var gsku = await ApprovedGskuAsync();
        var lsku = await CreatedLskuAsync(gsku, "CI");
        var version = (await LskuAsync(lsku)).Version;
        await InFlightAsync(lsku, version);
        using var scope = TenantScope();
        var handler = CancelHandler(scope, operations: Intercept<ILskuIdentityWorkflowOperationRepository>(
            scope.ServiceProvider.GetRequiredService<ILskuIdentityWorkflowOperationRepository>(),
            (method, _) => method.Name == nameof(ILskuIdentityWorkflowOperationRepository.GetUnfinishedForLskuAsync)
                ? Task.FromResult<IReadOnlyList<LskuIdentityWorkflowOperation>>([]) : null));

        var cancel = await handler.Handle(new(lsku, version, Guid.NewGuid(), "NOT_NEEDED"), CancellationToken.None);

        Assert.Equal(409, cancel.StatusCode);
        Assert.Contains("LSKU_SUBMIT_IN_FLIGHT", cancel.Errors);
        Assert.NotNull((await LskuAsync(lsku)).ActiveLifecycleOperation);
    }

    // CT L9: the HANDLER's refusal on its own — the fence is gone (as an older record may have it), the submit's start
    // may be live (unanswered): the cancel is refused by the handler, the draft and the submit stay as they were.
    [Fact]
    public async Task L09_The_handler_refuses_a_cancel_while_a_submit_it_cannot_close_is_unfinished()
    {
        var gsku = await ApprovedGskuAsync();
        var lsku = await CreatedLskuAsync(gsku, "CJ");
        var version = (await LskuAsync(lsku)).Version;
        await InFlightAsync(lsku, version);
        await host.Database.GetCollection<Lsku>("mdm_lskus").UpdateOneAsync(x => x.Id == lsku,
            Builders<Lsku>.Update.Set(x => x.ActiveLifecycleOperation, null));
        var before = await IdentityOperationAsync(lsku, version);

        var cancel = await SendAsync(HttpMethod.Post, $"/api/lskus/{lsku:D}/draft/cancel",
            new { expectedVersion = version, reasonCode = "NOT_NEEDED" }, MakerToken());

        Assert.Equal(HttpStatusCode.Conflict, cancel.StatusCode);
        Assert.Equal("LSKU_SUBMIT_IN_FLIGHT", Envelope.FirstError(await Envelope.ReadAsync(cancel)));
        var row = await LskuAsync(lsku);
        Assert.False(row.IsDeleted);
        Assert.Equal(version, row.Version);
        Assert.Equal(before.Checkpoint, (await IdentityOperationAsync(lsku, version)).Checkpoint);
    }

    // ---------------------------------------------------------------------------------------------------- M2 / M3 / M4

    // M2: a legacy submit (no start-sent mark) updated a moment ago may have just sent its start — the settle window
    // runs from its last update; it is not closed on "nothing under this key" at once.
    [Fact]
    public async Task M2_A_legacy_submit_updated_a_moment_ago_is_not_closed_inside_the_window()
    {
        var (lsku, version) = await ParkedNeverSentAsync("CK");
        await host.Database.GetCollection<BsonDocument>("mdm_lsku_identity_workflow_operations").UpdateOneAsync(
            new BsonDocument("LskuId", new BsonBinaryData(lsku, GuidRepresentation.Standard)),
            new BsonDocument
            {
                { "$unset", new BsonDocument("LastStartSentAtUtcTicksV1", "") },
                { "$set", new BsonDocument("UpdatedAtUtcTicksV1", DateTimeOffset.UtcNow.UtcTicks) }
            });

        var cancel = await SendAsync(HttpMethod.Post, $"/api/lskus/{lsku:D}/draft/cancel",
            new { expectedVersion = version, reasonCode = "NOT_NEEDED" }, MakerToken());

        Assert.Equal(HttpStatusCode.Conflict, cancel.StatusCode);
        Assert.Equal("LSKU_SUBMIT_IN_FLIGHT", Envelope.FirstError(await Envelope.ReadAsync(cancel)));
        Assert.Equal(LskuIdentityWorkflowCheckpoint.AwaitingMakerReplay, (await IdentityOperationAsync(lsku, version)).Checkpoint);
    }

    // M3: the worker's pass over an unanswered withdrawal parks it for its maker in ONE write and ends; over a parked
    // one it writes nothing (formerly ~10 AMR→AMR rewrites to its step budget, 503).
    [Fact]
    public async Task M3_The_worker_parks_an_unanswered_withdrawal_in_one_write_and_then_leaves_it()
    {
        var gsku = await ApprovedGskuAsync();
        var lsku = await CreatedLskuAsync(gsku, "CL");
        var version = (await LskuAsync(lsku)).Version;
        Assert.Equal(HttpStatusCode.Accepted, (await SubmitAsync(lsku, version)).StatusCode);
        host.Engine.NextCancelFails = ProductIdentityWorkflowTransportOutcome.Timeout;
        await SendAsync(HttpMethod.Post, $"/api/lskus/{lsku:D}/identity-approval/withdraw",
            new { expectedVersion = version + 1, reasonCode = "CHANGED_MIND" }, MakerToken());
        Assert.Equal(LskuIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown, (await IdentityOperationAsync(lsku, version)).Checkpoint);
        await DueNowAsync(lsku);
        using var scope = TenantScope();
        var writes = 0;
        var operations = Intercept<ILskuIdentityWorkflowOperationRepository>(
            scope.ServiceProvider.GetRequiredService<ILskuIdentityWorkflowOperationRepository>(), (method, _) =>
            {
                if (method.Name == nameof(ILskuIdentityWorkflowOperationRepository.AdvanceAsync)) writes++;
                return null;
            });
        var processor = ActivatorUtilities.CreateInstance<LskuIdentityWorkflowProcessor>(scope.ServiceProvider, operations);

        var first = await processor.RecoverAsync(await IdentityOperationAsync(lsku, version), "m3", TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1));
        var afterFirst = writes;
        await processor.RecoverAsync(await IdentityOperationAsync(lsku, version), "m3", TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1));

        Assert.Equal(1, afterFirst);
        Assert.Equal(1, writes);
        Assert.Equal(LskuIdentityWorkflowCheckpoint.AwaitingMakerReplay, first.Operation!.Checkpoint);
        Assert.NotNull((await IdentityOperationAsync(lsku, version)).WithdrawalUnansweredSinceUtcTicksV1);
    }

    // M4: the identity gauge records on the always-on worker — the identity worker need not run; a count that fails
    // says so (count_failed = 1) instead of leaving a stale gauge.
    [Fact]
    public async Task M4_The_identity_gauge_records_without_its_worker_and_names_a_failed_count()
    {
        var metrics = new ProductIdentityRecoveryMetrics();
        var good = new LskuIdentityGauge(host.Factory.Services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<LskuIdentityGauge>.Instance, metrics);
        await good.RecordAsync(CancellationToken.None);
        Assert.NotNull(metrics.Current(LskuIdentityWorkflowRecoveryRunner.Flow));
        Assert.Equal(0, metrics.CountFailed(LskuIdentityWorkflowRecoveryRunner.Flow));

        var broken = new ServiceCollection()
            .AddScoped<ILskuIdentityWorkflowTenantPartitionDiscovery>(_ => Intercept<ILskuIdentityWorkflowTenantPartitionDiscovery>(
                null!, (_, _) => throw new TimeoutException("store unavailable")))
            .BuildServiceProvider();
        await new LskuIdentityGauge(broken.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<LskuIdentityGauge>.Instance, metrics).RecordAsync(CancellationToken.None);

        Assert.Equal(1, metrics.CountFailed(LskuIdentityWorkflowRecoveryRunner.Flow));
    }

    // ---------------------------------------------------------------------------------------------------- M5 / M6

    [Fact]
    public async Task M5_The_admission_census_has_its_partial_index()
    {
        _ = new GskuRepository(host.Database, new Tenant(_tenant));
        var indexes = await (await host.Database.GetCollection<Gsku>("mdm_gskus").Indexes.ListAsync()).ToListAsync();

        var census = Assert.Single(indexes, x => x["name"] == "ix_mdm_gskus_child_admission_acquired");
        Assert.True(census.Contains("partialFilterExpression"));
    }

    // M6: an admission held only by a code reserved long ago and never consumed, and one whose code is bound to a
    // cancelled (deleted) row: the repair burns each code, then releases the admission.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task M6_The_repair_closes_a_never_consumed_code_and_a_code_of_a_cancelled_row(bool neverConsumed)
    {
        var gsku = await ApprovedGskuAsync();
        string commandId;
        Guid reservationId;
        if (neverConsumed)
        {
            commandId = "NEVER-" + Guid.NewGuid().ToString("N").ToUpperInvariant();
            using var scope = TenantScope();
            reservationId = (await scope.ServiceProvider.GetRequiredService<ICodeReservationRepository>()
                .ReserveAsync(CodeBearingEntityType.Lsku, commandId, MakerActor, commandId)).Id;
            await PushAdmissionAsync(gsku, commandId, "CM", DateTimeOffset.UtcNow.AddHours(-2));
        }
        else
        {
            var pending = await PendingCreationAsync(gsku, "CN");
            commandId = pending.Command;
            reservationId = (await ReservationOfAsync(pending.Lsku)).Id;
            // The cancel committed and was cut off before its burn: the row deleted, its code still pending.
            await host.Database.GetCollection<Lsku>("mdm_lskus").UpdateOneAsync(x => x.Id == pending.Lsku,
                Builders<Lsku>.Update.Set(x => x.IsDeleted, true));
            await host.Database.GetCollection<Gsku>("mdm_gskus").UpdateOneAsync(x => x.Id == gsku,
                Builders<Gsku>.Update.Set("ChildCreationAdmissions.$[a].AcquiredAtUtc", DateTimeOffset.UtcNow.AddHours(-2)),
                new UpdateOptions { ArrayFilters = [new BsonDocumentArrayFilterDefinition<BsonDocument>(
                    new BsonDocument("a.CreationCommandId", commandId))] });
        }
        await host.Database.GetCollection<CodeReservation>("mdm_code_reservations").UpdateOneAsync(x => x.Id == reservationId,
            Builders<CodeReservation>.Update.Set(x => x.ReservedAt, DateTimeOffset.UtcNow.AddHours(-2)));

        var repair = await host.Client(host.Token(_tenant, Guid.NewGuid(), [RecoverKey]))
            .PostAsync("/api/lskus/operator/child-admission-repairs", null);

        Assert.True(repair.IsSuccessStatusCode, await repair.Content.ReadAsStringAsync());
        Assert.DoesNotContain(commandId, await AdmissionsAsync(gsku));
        Assert.Equal(CodeReservationBindingState.Burned, (await host.Database.GetCollection<CodeReservation>("mdm_code_reservations")
            .Find(x => x.Id == reservationId).SingleAsync()).BindingState);
    }

    // ---------------------------------------------------------------------------------------------------- item 5 (c)

    // (a) a live row bound to the code: the burn is refused by name and writes nothing; (c) a row that was cancelled
    // (deleted) does not hold its code: it is burned.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Item05c_A_code_with_a_live_row_is_not_burned_and_one_of_a_cancelled_row_is(bool rowCancelled)
    {
        var gsku = await ApprovedGskuAsync();
        var lsku = await CreatedLskuAsync(gsku, rowCancelled ? "BC" : "BD");
        var row = await LskuAsync(lsku);
        var codes = host.Database.GetCollection<CodeReservation>("mdm_code_reservations");
        await codes.UpdateOneAsync(x => x.Id == row.CodeReservationId,
            Builders<CodeReservation>.Update.Set(x => x.BindingState, CodeReservationBindingState.PendingIdentityWrite));
        if (rowCancelled)
            await host.Database.GetCollection<Lsku>("mdm_lskus").UpdateOneAsync(x => x.Id == lsku,
                Builders<Lsku>.Update.Set(x => x.IsDeleted, true));
        var code = await codes.Find(x => x.Id == row.CodeReservationId).SingleAsync();
        var repository = new CodeReservationRepository(host.Database, new Tenant(_tenant));

        var burned = await repository.BurnIdentityBindingAsync(code.Id, lsku, code.Version, "fix3-5c:" + lsku.ToString("N"),
            MakerActor, "fix3-5c", "TEST");

        var after = await codes.Find(x => x.Id == code.Id).SingleAsync();
        if (rowCancelled)
        {
            Assert.True(burned.Succeeded, burned.ErrorCode);
            Assert.Equal(CodeReservationBindingState.Burned, after.BindingState);
        }
        else
        {
            Assert.False(burned.Succeeded);
            Assert.Equal(CodeReservationBurnCodes.BoundRowExists, burned.ErrorCode);
            Assert.Equal(CodeReservationBindingState.PendingIdentityWrite, after.BindingState);
            Assert.Equal(code.Version, after.Version);
        }
    }

    // (b) the burn and the insert race, both orders, at a barrier inside each transaction — never a live row with a
    // burned code: burn-read → insert commits → the burn is refused; insert begins → burn commits → the insert is refused.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Item05c_A_burn_and_an_insert_racing_never_leave_a_live_row_with_a_burned_code(bool burnReadsFirst)
    {
        var gsku = await ApprovedGskuAsync();
        var (captured, fingerprint) = await CutOffBeforeTheRowAsync(gsku, burnReadsFirst ? "BE" : "BF");
        var code = await host.Database.GetCollection<CodeReservation>("mdm_code_reservations")
            .Find(x => x.Id == captured.CodeReservationId).SingleAsync();
        var codes = new CodeReservationRepository(host.Database, new Tenant(_tenant));
        var rows = new LskuRepository(host.Database, new Tenant(_tenant));
        ReservationOperationResult? burned = null;
        LskuCreateResult? inserted = null;

        if (burnReadsFirst)
        {
            codes.AfterBoundRowCheckForTests = async () => inserted = await rows.CreateDraftWithAdmissionAsync(captured, fingerprint);
            burned = await codes.BurnIdentityBindingAsync(code.Id, captured.Id, code.Version, "fix3-race:burn", MakerActor, "race", "TEST");
        }
        else
        {
            rows.BeforeReservationWriteForTests = async () => burned = await codes.BurnIdentityBindingAsync(
                code.Id, captured.Id, code.Version, "fix3-race:burn", MakerActor, "race", "TEST");
            inserted = await rows.CreateDraftWithAdmissionAsync(captured, fingerprint);
        }

        var finalCode = await host.Database.GetCollection<CodeReservation>("mdm_code_reservations")
            .Find(x => x.Id == code.Id).SingleAsync();
        var finalRow = await host.Database.GetCollection<Lsku>("mdm_lskus").Find(x => x.Id == captured.Id).FirstOrDefaultAsync();
        Assert.False(finalRow is not null && finalCode.BindingState == CodeReservationBindingState.Burned);
        if (burnReadsFirst)
        {
            Assert.True(inserted!.Succeeded, inserted.ErrorCode);
            Assert.False(burned!.Succeeded);
            Assert.NotNull(finalRow);
        }
        else
        {
            Assert.True(burned!.Succeeded, burned.ErrorCode);
            Assert.False(inserted!.Succeeded);
            Assert.Equal("CODE_RESERVATION_BURNED", inserted.ErrorCode);
            Assert.Null(finalRow);
        }
    }

    // (d) fail-closed: a store that cannot run transactions (a plain mongod) refuses the burn and writes nothing.
    [Fact]
    public async Task Item05c_Without_transactions_the_burn_is_refused_and_nothing_is_written()
    {
        var standalone = host.StandaloneDatabase;
        var identity = Guid.NewGuid();
        var reservation = new CodeReservation
        {
            Id = Guid.NewGuid(), TenantId = _tenant, EntityType = CodeBearingEntityType.Lsku, ReservedCode = "LS-STANDALONE",
            ReservationState = CodeReservationState.Consumed, BindingState = CodeReservationBindingState.PendingIdentityWrite,
            ReservationCommandId = "STANDALONE-" + identity.ToString("N"), ConsumedEntityId = identity,
            ReservedAt = DateTimeOffset.UtcNow, ReservedByActorId = MakerActor, Version = 2
        };
        await standalone.GetCollection<CodeReservation>("mdm_code_reservations").InsertOneAsync(reservation);

        var burned = await new CodeReservationRepository(standalone, new Tenant(_tenant)).BurnIdentityBindingAsync(
            reservation.Id, identity, reservation.Version, "standalone:burn", MakerActor, "standalone", "TEST");

        Assert.False(burned.Succeeded);
        Assert.Equal(CodeReservationBurnCodes.TransactionUnavailable, burned.ErrorCode);
        var after = await standalone.GetCollection<CodeReservation>("mdm_code_reservations").Find(x => x.Id == reservation.Id).SingleAsync();
        Assert.Equal(CodeReservationBindingState.PendingIdentityWrite, after.BindingState);
        Assert.Equal(2, after.Version);
    }

    // A creation cut off after its code was consumed and before its row: the row it would have written, captured.
    private async Task<(Lsku Row, string Fingerprint)> CutOffBeforeTheRowAsync(Guid gsku, string market)
    {
        using var scope = TenantScope();
        Lsku? captured = null;
        string? fingerprint = null;
        var handler = ActivatorUtilities.CreateInstance<
            Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.CommandHandlers.CreateLskuDraftHandler>(
            scope.ServiceProvider,
            Intercept<ILskuRepository>(scope.ServiceProvider.GetRequiredService<ILskuRepository>(), (method, args) =>
            {
                if (method.Name != nameof(ILskuRepository.CreateDraftWithAdmissionAsync)) return null;
                captured = (Lsku)args![0]!;
                fingerprint = (string)args[1]!;
                throw new IOException("cut off before the row");
            }),
            new HandlerActor());
        await Assert.ThrowsAsync<IOException>(() => handler.Handle(new(new Diten.MdmService.Application.Features
            .ProductItemSkuMaster.ProductItemSkuMasterModels.CreateLskuDraftRequest
            {
                GskuId = gsku, MarketCode = market, IdempotencyKey = "CUT-" + Guid.NewGuid().ToString("N")
            }), CancellationToken.None));
        return (captured!, fingerprint!);
    }

    // ---------------------------------------------------------------------------------------------------- helpers (FIX3)

    private const string MakerActor = "lsku-fix3-maker";
    private readonly Dictionary<Guid, Guid> _parkedKeys = [];

    // A draft whose submit is parked for its maker with "nothing was sent" proven (0): the engine refused the start
    // before processing it (a delegated-user refusal), then the mark is set to the proven 0.
    private async Task<(Guid Lsku, int Version)> ParkedNeverSentAsync(string market)
    {
        var gsku = await ApprovedGskuAsync();
        var lsku = await CreatedLskuAsync(gsku, market);
        var version = (await LskuAsync(lsku)).Version;
        var key = Guid.NewGuid();
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Forbidden, "WORKFLOW_DELEGATED_USER_FORBIDDEN", false);
        await SendAsync(HttpMethod.Post, $"/api/lskus/{lsku:D}/submit", new { expectedVersion = version }, MakerToken(), key);
        Assert.Equal(LskuIdentityWorkflowCheckpoint.AwaitingMakerReplay, (await IdentityOperationAsync(lsku, version)).Checkpoint);
        await host.Database.GetCollection<LskuIdentityWorkflowOperation>("mdm_lsku_identity_workflow_operations")
            .UpdateOneAsync(x => x.TenantId == _tenant && x.LskuId == lsku,
                Builders<LskuIdentityWorkflowOperation>.Update.Set(x => x.LastStartSentAtUtcTicksV1, 0L));
        _parkedKeys[lsku] = key;
        return (lsku, version);
    }

    // A submit whose start may be live (sent, unanswered, the engine silent): StartOutcomeUnknown, its fence held.
    private async Task InFlightAsync(Guid lsku, int version)
    {
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Timeout, "FAKE_ENGINE_START_TIMEOUT", false);
        host.Engine.LookupFailure = ProductIdentityWorkflowTransportOutcome.Timeout;
        try { await SubmitAsync(lsku, version); }
        finally { host.Engine.LookupFailure = null; }
        Assert.Equal(LskuIdentityWorkflowCheckpoint.StartOutcomeUnknown, (await IdentityOperationAsync(lsku, version)).Checkpoint);
    }

    // A creation answered 202 (row written, its code pending, its admission held).
    private async Task<(Guid Lsku, int Version, string Command)> PendingCreationAsync(Guid gsku, string market)
    {
        var lsku = await CreatedLskuAsync(gsku, market);
        var row = await LskuAsync(lsku);
        await host.Database.GetCollection<CodeReservation>("mdm_code_reservations").UpdateOneAsync(x => x.Id == row.CodeReservationId,
            Builders<CodeReservation>.Update.Set(x => x.BindingState, CodeReservationBindingState.PendingIdentityWrite));
        await PushAdmissionAsync(gsku, row.CreationCommandId, market, DateTimeOffset.UtcNow.AddMinutes(-1));
        return (lsku, row.Version, row.CreationCommandId);
    }

    private async Task<CodeReservation> ReservationOfAsync(Guid lsku)
    {
        var row = await host.Database.GetCollection<Lsku>("mdm_lskus").Find(x => x.Id == lsku).SingleAsync();
        return await host.Database.GetCollection<CodeReservation>("mdm_code_reservations")
            .Find(x => x.Id == row.CodeReservationId).SingleAsync();
    }

    private async Task<Guid> DraftGskuAsync()
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved);
        var created = await SendAsync(HttpMethod.Post, "/api/gskus/drafts",
            new { globalProductId = product, packQuantity = 1m, packUomCode = "C62" }, MakerToken());
        Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        return (await Envelope.ReadAsync(created)).GetProperty("data").GetProperty("gskuId").GetGuid();
    }

    private Task DueNowAsync(Guid lsku) =>
        host.Database.GetCollection<LskuIdentityWorkflowOperation>("mdm_lsku_identity_workflow_operations").UpdateOneAsync(
            x => x.TenantId == _tenant && x.LskuId == lsku,
            Builders<LskuIdentityWorkflowOperation>.Update.Set(x => x.NextAttemptAtUtcTicksV1, null));

    private Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Handlers.CommandHandlers.CancelLskuDraftHandler
        CancelHandler(IServiceScope scope, ILskuRepository? lskus = null, ICodeReservationRepository? reservations = null,
            ILskuIdentityWorkflowOperationRepository? operations = null, LskuIdentityWorkflowProcessor? processor = null)
    {
        var args = new List<object> { new LifecycleActor(_maker) };
        if (lskus is not null) args.Add(lskus);
        if (reservations is not null) args.Add(reservations);
        if (operations is not null) args.Add(operations);
        if (processor is not null) args.Add(processor);
        return ActivatorUtilities.CreateInstance<
            Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Handlers.CommandHandlers.CancelLskuDraftHandler>(
            scope.ServiceProvider, args.ToArray());
    }

    private sealed class LifecycleActor(Guid subject) : Diten.MdmService.Application.Contracts.IProductIdentityLifecycleActorContext
    {
        public bool TryResolveCanonicalHumanSubject(out Guid subjectId) { subjectId = subject; return true; }
        public bool HasPermission(string permission) => true;
    }

    // ---------------------------------------------------------------------------------------------------- helpers (FIX2)
    private IServiceScope TenantScope()
    {
        var scope = host.Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(_tenant);
        return scope;
    }

    private static Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.CommandHandlers.CreateLskuDraftHandler
        CreateHandler(IServiceScope scope, ICodeReservationRepository? reservations = null, ILskuRepository? lskus = null) =>
        ActivatorUtilities.CreateInstance<
            Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.CommandHandlers.CreateLskuDraftHandler>(
            scope.ServiceProvider,
            reservations ?? scope.ServiceProvider.GetRequiredService<ICodeReservationRepository>(),
            lskus ?? scope.ServiceProvider.GetRequiredService<ILskuRepository>(),
            new HandlerActor());

    private static Diten.MdmService.Application.Features.ProductItemSkuMaster.Commands.CreateLskuDraftCommand CreateCommand(
        Guid gsku, string market, string key) =>
        new(new Diten.MdmService.Application.Features.ProductItemSkuMaster.ProductItemSkuMasterModels.CreateLskuDraftRequest
        {
            GskuId = gsku, MarketCode = market, IdempotencyKey = key
        });

    private sealed class HandlerActor : Diten.MdmService.Application.Contracts.IProductIdentityActorContext
    {
        public string ActorId => "lsku-fix2-handler-test";
    }

    // A repository that forwards every call to the real one unless the interceptor answers it (non-null) or throws.
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


    private async Task<(Guid Lsku, int Version, Guid Key)> RejectedThenStuckAsync(string market)
    {
        var gsku = await ApprovedGskuAsync();
        var lsku = await CreatedLskuAsync(gsku, market);
        var version = (await LskuAsync(lsku)).Version;
        var key = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.Accepted,
            (await SendAsync(HttpMethod.Post, $"/api/lskus/{lsku:D}/submit", new { expectedVersion = version }, MakerToken(), key)).StatusCode);
        host.Engine.Reject(host.Engine.Open("lsku", lsku.ToString("D")), host.Approver);
        await IdentityCyclesUntilAsync(lsku, version, op => op.Checkpoint == LskuIdentityWorkflowCheckpoint.Completed);
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, (await LskuAsync(lsku)).LifecycleStatus);
        // Stuck after the rejection was applied (a lost lease before verification): back to manual reconciliation.
        await ParkAsync(lsku, StuckCause);
        return (lsku, version, key);
    }

    // A cause no step of the decision path writes, so a test can tell "stayed stuck" from "moved on and came back".
    private const string StuckCause = "LSKU_IDENTITY_WORKFLOW_EVIDENCE_CONFLICT";

    private Task ParkAsync(Guid lsku, string cause) =>
        host.Database.GetCollection<LskuIdentityWorkflowOperation>("mdm_lsku_identity_workflow_operations").UpdateOneAsync(
            x => x.TenantId == _tenant && x.LskuId == lsku,
            Builders<LskuIdentityWorkflowOperation>.Update
                .Set(x => x.Checkpoint, LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired)
                .Set(x => x.RecoveryDisposition, ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired)
                .Set(x => x.LastFailureCode, cause).Set(x => x.NextAttemptAtUtcTicksV1, null)
                .Set(x => x.LeaseOwner, null).Set(x => x.LeaseUntilUtcTicksV1, null));

    // The LSKU at its audit-intent cap: clones of its own first intent (each field as the store wrote it).
    private async Task FillAuditIntentsAsync(Guid lsku)
    {
        var raw = host.Database.GetCollection<MongoDB.Bson.BsonDocument>("mdm_lskus");
        var id = new MongoDB.Bson.BsonDocument("_id", new MongoDB.Bson.BsonBinaryData(lsku, MongoDB.Bson.GuidRepresentation.Standard));
        var document = await raw.Find(id).SingleAsync();
        var template = document["AuditIntents"].AsBsonArray[0].AsBsonDocument;
        var full = new MongoDB.Bson.BsonArray(Enumerable.Range(0, AuditIntentLimits.MaxPerAggregate).Select(i =>
        {
            var clone = template.DeepClone().AsBsonDocument;
            clone["IntentId"] = new MongoDB.Bson.BsonBinaryData(Guid.NewGuid(), MongoDB.Bson.GuidRepresentation.Standard);
            clone["IdempotencyKey"] = template["IdempotencyKey"].AsString + ":filler-" + i;
            return clone;
        }));
        await raw.UpdateOneAsync(id, new MongoDB.Bson.BsonDocument("$set", new MongoDB.Bson.BsonDocument("AuditIntents", full)));
    }

    // ---------------------------------------------------------------------------------------------------- helpers

    private string MakerToken() => host.Token(_tenant, _maker, MakerPermissions);

    private async Task<Guid> SeedProductAsync(ProductIdentityLifecycleStatus status)
    {
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await host.Database.GetCollection<GlobalProduct>("mdm_global_products").InsertOneAsync(new GlobalProduct
        {
            Id = id, TenantId = _tenant, CanonicalCode = $"GP-{id:N}"[..16],
            GlobalProductName = $"Product {id:N}", GlobalProductNameNormalized = $"PRODUCT {id:N}".ToUpperInvariant(),
            CodeReservationId = Guid.NewGuid(), LifecycleStatus = status, CreatedAt = now, UpdatedAt = now
        });
        return id;
    }

    private async Task<Guid> ApprovedGskuAsync(Guid? product = null)
    {
        product ??= await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved);
        var created = await SendAsync(HttpMethod.Post, "/api/gskus/drafts",
            new { globalProductId = product, packQuantity = 1m, packUomCode = "C62" }, MakerToken());
        Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var data = (await Envelope.ReadAsync(created)).GetProperty("data");
        var gsku = data.GetProperty("gskuId").GetGuid();
        var submit = await SendAsync(HttpMethod.Post, $"/api/gskus/{gsku:D}/submit",
            new { expectedVersion = data.GetProperty("version").GetInt32() }, MakerToken());
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

    private Task<HttpResponseMessage> CreateLskuAsync(Guid gsku, string market, Guid? key = null) =>
        SendAsync(HttpMethod.Post, "/api/lskus/drafts", new { gskuId = gsku, marketCode = market }, MakerToken(), key);

    private async Task<Guid> CreatedLskuAsync(Guid gsku, string market)
    {
        var response = await CreateLskuAsync(gsku, market);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await Envelope.ReadAsync(response)).GetProperty("data").GetProperty("lskuId").GetGuid();
    }

    private async Task<(Guid Id, int Version)> ApprovedLskuAsync(string market)
    {
        var gsku = await ApprovedGskuAsync();
        var lsku = await CreatedLskuAsync(gsku, market);
        var version = (await LskuAsync(lsku)).Version;
        Assert.Equal(HttpStatusCode.Accepted, (await SubmitAsync(lsku, version)).StatusCode);
        host.Engine.Approve(host.Engine.Open("lsku", lsku.ToString("D")), host.Approver);
        await IdentityCyclesUntilAsync(lsku, version, op => op.Checkpoint == LskuIdentityWorkflowCheckpoint.Completed);
        var approved = await LskuAsync(lsku);
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, approved.LifecycleStatus);
        return (lsku, approved.Version);
    }

    private Task<HttpResponseMessage> SubmitAsync(Guid lsku, int version) =>
        SendAsync(HttpMethod.Post, $"/api/lskus/{lsku:D}/submit", new { expectedVersion = version }, MakerToken());

    private Task<HttpResponseMessage> RequestRetirementAsync(Guid lsku, int version) =>
        SendAsync(HttpMethod.Post, $"/api/lskus/{lsku:D}/retirement-requests",
            new { expectedVersion = version, requestReason = "No longer sold in this market" }, MakerToken());

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object body, string token,
        Guid? key = null)
    {
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", (key ?? Guid.NewGuid()).ToString("D"));
        return await host.Client(token).SendAsync(request);
    }

    private Task<Lsku> LskuAsync(Guid id) =>
        host.Database.GetCollection<Lsku>("mdm_lskus").Find(x => x.Id == id && x.TenantId == _tenant).SingleAsync();

    private Task<Gsku> GskuAsync(Guid id) =>
        host.Database.GetCollection<Gsku>("mdm_gskus").Find(x => x.Id == id && x.TenantId == _tenant).SingleAsync();

    private async Task<string[]> AdmissionsAsync(Guid gsku) =>
        (await GskuAsync(gsku)).ChildCreationAdmissions.Select(x => x.CreationCommandId).ToArray();

    private Task<LskuIdentityWorkflowOperation> IdentityOperationAsync(Guid lsku, int expectedVersion) =>
        host.Database.GetCollection<LskuIdentityWorkflowOperation>("mdm_lsku_identity_workflow_operations")
            .Find(x => x.TenantId == _tenant && x.LskuId == lsku && x.ExpectedLskuVersion == expectedVersion).SingleAsync();

    // The open retirement-request approval of THIS LSKU (other tests leave theirs open on the shared engine).
    private async Task<FakeWorkflowEngine.Instance> RetirementInstanceAsync(Guid lsku)
    {
        var operation = await host.Database.GetCollection<LskuRetirementRequestOperation>(
                "mdm_lsku_retirement_request_operations")
            .Find(x => x.TenantId == _tenant && x.LskuId == lsku && x.IsActive).SingleAsync();
        return host.Engine.Open("LskuRetirementRequest", operation.OperationId.ToString("D"));
    }

    private Task PushAdmissionAsync(Guid gsku, string command, string market, DateTimeOffset acquiredAt) =>
        host.Database.GetCollection<Gsku>("mdm_gskus").UpdateOneAsync(x => x.Id == gsku,
            Builders<Gsku>.Update.Push(x => x.ChildCreationAdmissions, new GskuChildCreationAdmission
            {
                ChildKind = GskuChildIdentityKind.Lsku, CreationCommandId = command,
                RequestFingerprint = GskuChildCreationAdmission.ComputeRequestFingerprint(
                    gsku, GskuChildIdentityKind.Lsku, command, market),
                AcquiredAtUtc = acquiredAt.ToUniversalTime()
            }));

    private async Task<LskuIdentityWorkflowOperation> IdentityCyclesUntilAsync(Guid lsku, int expectedVersion,
        Func<LskuIdentityWorkflowOperation, bool> done)
    {
        var operation = await IdentityOperationAsync(lsku, expectedVersion);
        for (var attempt = 0; attempt < 40 && !done(operation); attempt++)
        {
            await host.RunAsync<LskuIdentityWorkflowRecoveryRunner>(runner => runner.RunCycleAsync());
            operation = await IdentityOperationAsync(lsku, expectedVersion);
            if (!done(operation)) await Task.Delay(250);
        }
        Assert.True(done(operation), $"Not reached: {operation.Checkpoint} {operation.LastFailureCode}");
        return operation;
    }

    private sealed class Tenant(Guid tenantId) : ITenantContext
    {
        public Guid TenantId { get; private set; } = tenantId;
        public bool IsResolved => TenantId != Guid.Empty;
        public void SetTenant(Guid tenantId) => TenantId = tenantId;
    }
}
