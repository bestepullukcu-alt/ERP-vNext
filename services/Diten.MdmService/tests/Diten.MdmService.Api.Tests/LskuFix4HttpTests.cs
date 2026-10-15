using System.Net;
using System.Security.Claims;
using System.Text;
using Diten.MdmService.Api.Services.ProductItemSkuMaster;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using Diten.MdmService.Infrastructure.ReferenceData;
using Diten.MdmService.Persistence.Repositories;
using Diten.Shared.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// WP-MDM-LSKU-ACCEPT-01 FIX4 (P-2026-10-07-LSKU-FIX4) over HTTP / the real handlers and a throwaway replica-set mongod.
/// </summary>
public sealed partial class LskuFix3HttpTests
{
    // ---------------------------------------------------------------------------------------------------- FIX4 item 1

    // "A wait never burns": every insert attempt meets a transient transaction error (a failover, a network cut, a write
    // conflict) — the insert is retried with backoff and, once out of attempts, the outcome stays ambiguous (202): the
    // code is not burned, the admission is kept, no row. (Formerly: 3 instant tries, LSKU_DUPLICATE_CONFLICT, burned.)
    [Fact]
    public async Task Fix4_Item01_A_transient_insert_error_to_the_last_attempt_is_ambiguous_and_burns_nothing()
    {
        var gsku = await ApprovedGskuAsync();
        var key = "TT-" + Guid.NewGuid().ToString("N");
        var admin = host.Database.Client.GetDatabase("admin");
        var armed = false;
        var rows = new LskuRepository(host.Database, new Tenant(_tenant))
        {
            BeforeReservationWriteForTests = async () =>
            {
                if (armed) return;
                armed = true;
                await admin.RunCommandAsync<BsonDocument>(new BsonDocument
                {
                    { "configureFailPoint", "failCommand" },
                    { "mode", new BsonDocument("times", LskuRepository.InsertAttempts) },
                    { "data", new BsonDocument
                        {
                            { "failCommands", new BsonArray { "insert" } },
                            { "errorCode", 112 },
                            { "errorLabels", new BsonArray { "TransientTransactionError" } }
                        }
                    }
                });
            }
        };
        Response<Diten.MdmService.Application.Features.ProductItemSkuMaster.ProductItemSkuMasterModels.LskuDraftDto> result;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            using var scope = TenantScope();
            result = await CreateHandler(scope, lskus: rows).Handle(CreateCommand(gsku, "TT", key), CancellationToken.None);
        }
        finally
        {
            await admin.RunCommandAsync<BsonDocument>(new BsonDocument
            {
                { "configureFailPoint", "failCommand" }, { "mode", "off" }
            });
        }

        Assert.True(armed);
        // The retries waited (backoff 100 + 200 + 400 ms between the four attempts), not three instant tries.
        Assert.True(clock.Elapsed >= TimeSpan.FromMilliseconds(650), $"no backoff: {clock.Elapsed.TotalMilliseconds} ms");
        Assert.True(result.StatusCode is 202 or 503, $"{result.StatusCode} {string.Join(',', result.Errors)}");
        var code = await host.Database.GetCollection<CodeReservation>("mdm_code_reservations")
            .Find(x => x.TenantId == _tenant && x.ReservationCommandId == key.ToUpperInvariant()).SingleAsync();
        Assert.Equal(CodeReservationState.Consumed, code.ReservationState);
        Assert.Equal(CodeReservationBindingState.PendingIdentityWrite, code.BindingState);
        Assert.Contains(key.ToUpperInvariant(), await AdmissionsAsync(gsku));
        Assert.False(await host.Database.GetCollection<Lsku>("mdm_lskus")
            .Find(x => x.TenantId == _tenant && x.CreationCommandId == key.ToUpperInvariant()).AnyAsync());
    }

    // Fifty inserts of the same GSKU + market race (each with its own consumed code): exactly one row; every other
    // answer is the definite "this market already exists" — never a duplicate-conflict or an ambiguous outcome.
    [Fact]
    public async Task Fix4_Item01_Fifty_racing_inserts_of_one_market_get_only_market_already_exists()
    {
        var gsku = await ApprovedGskuAsync();
        var (template, _) = await CutOffBeforeTheRowAsync(gsku, "RX");
        var codes = host.Database.GetCollection<BsonDocument>("mdm_code_reservations");
        var templateCode = await codes.Find(new BsonDocument("_id",
            new BsonBinaryData(template.CodeReservationId, GuidRepresentation.Standard))).SingleAsync();
        var candidates = new List<(Lsku Row, string Fingerprint)>();
        for (var index = 0; index < 50; index++)
        {
            var id = Guid.NewGuid();
            var codeId = Guid.NewGuid();
            var canonical = $"{template.CanonicalCode}-{index:D2}";
            var command = $"RACE-{index:D2}-{Guid.NewGuid():N}".ToUpperInvariant();
            var code = templateCode.DeepClone().AsBsonDocument;
            code["_id"] = new BsonBinaryData(codeId, GuidRepresentation.Standard);
            code["ConsumedEntityId"] = new BsonBinaryData(id, GuidRepresentation.Standard);
            code["ReservedCode"] = canonical;
            code["ReservationCommandId"] = command;
            code["ConsumeCommandId"] = command;
            await codes.InsertOneAsync(code);
            var row = BsonSerializerClone(template);
            row.Id = id;
            row.CodeReservationId = codeId;
            row.CanonicalCode = canonical;
            row.CreationCommandId = command;
            foreach (var intent in row.AuditIntents)
            {
                intent.AggregateId = id;
                intent.IntentId = Guid.NewGuid();
            }
            // Each racer holds its own admission (written directly: the 32-admission cap is the creation path's, not
            // the insert's), as a creation that reached its insert does.
            await PushAdmissionAsync(gsku, command, row.MarketCode, DateTimeOffset.UtcNow);
            candidates.Add((row, GskuChildCreationAdmission.ComputeRequestFingerprint(
                gsku, GskuChildIdentityKind.Lsku, command, row.MarketCode)));
        }

        var results = await Task.WhenAll(candidates.Select(candidate =>
            new LskuRepository(host.Database, new Tenant(_tenant))
                .CreateDraftWithAdmissionAsync(candidate.Row, candidate.Fingerprint)));

        Assert.Single(results, x => x.Succeeded);
        var refusals = results.Where(x => !x.Succeeded).ToArray();
        Assert.Equal(49, refusals.Length);
        Assert.All(refusals, refusal =>
        {
            Assert.Equal(LskuCreateConflictKind.IdentityKey, refusal.ConflictKind);
            Assert.False(refusal.WriteOutcomeAmbiguous);
        });
        Assert.Equal(1L, await host.Database.GetCollection<Lsku>("mdm_lskus")
            .CountDocumentsAsync(x => x.TenantId == _tenant && x.GskuId == gsku && x.MarketCode == "RX"));
    }

    private static Lsku BsonSerializerClone(Lsku source) =>
        MongoDB.Bson.Serialization.BsonSerializer.Deserialize<Lsku>(source.ToBsonDocument());

    // ---------------------------------------------------------------------------------------------------- FIX4 item 2

    // A creation cut off after its code was consumed: its repeat meets a non-JSON 404 from the market resolver (an
    // ingress page) — the REAL create-time client answers 503 and the command's code stays pending (never burned).
    [Fact]
    public async Task Fix4_Item02_A_proxy_404_on_the_repeat_never_burns_the_commands_own_code()
    {
        var gsku = await ApprovedGskuAsync();
        var (captured, _) = await CutOffBeforeTheRowAsync(gsku, "NH");
        using var scope = TenantScope();
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "actor")], "Bearer"))
        };
        context.Request.Headers.Authorization = "Bearer delegated-market-jwt";
        var client = new PlatformVerifiedMarketResolverClient(
            new HttpClient(new HtmlNotFound()),
            new HttpContextAccessor { HttpContext = context },
            Options.Create(new VerifiedMarketResolverOptions
            {
                PlatformBaseAddress = new Uri("https://platform.internal/"), Timeout = TimeSpan.FromSeconds(1),
                CredentialIdentifier = "resolver-id", CredentialSecret = "resolver-secret"
            }));
        var handler = ActivatorUtilities.CreateInstance<
            Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.CommandHandlers.CreateLskuDraftHandler>(
            scope.ServiceProvider, (Diten.MdmService.Application.Contracts.ReferenceData.IVerifiedMarketReferenceResolver)client,
            new HandlerActor());

        var repeat = await handler.Handle(CreateCommand(gsku, "NH", captured.CreationCommandId), CancellationToken.None);

        Assert.Equal(503, repeat.StatusCode);
        var code = await host.Database.GetCollection<CodeReservation>("mdm_code_reservations")
            .Find(x => x.Id == captured.CodeReservationId).SingleAsync();
        Assert.Equal(CodeReservationBindingState.PendingIdentityWrite, code.BindingState);
    }

    private sealed class HtmlNotFound : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("<html><body>404 Not Found</body></html>", Encoding.UTF8, "text/html")
            });
    }

    // ---------------------------------------------------------------------------------------------------- FIX4 item 3

    // The transactional burn is safe only for a kind whose insert writes its reservation in its own transaction (LSKU).
    // A Finished Good / GSKU / GP code is refused outright — nothing burned.
    [Theory]
    // FG FIX1 part B: the FG insert now writes its reservation in its own transaction, so the FG kind is open
    // (FgFix1HttpTests.Item02_The_gate_… / Item03_A_burn_and_an_fg_insert_racing_…); GSKU / GP stay refused.
    [InlineData(CodeBearingEntityType.Gsku)]
    [InlineData(CodeBearingEntityType.GlobalProduct)]
    public async Task Fix4_Item03_A_burn_of_a_kind_whose_insert_is_not_transactional_is_refused(CodeBearingEntityType kind)
    {
        var gsku = await ApprovedGskuAsync();
        var (captured, _) = await CutOffBeforeTheRowAsync(gsku, kind switch
        {
            CodeBearingEntityType.FinishedGood => "KF", CodeBearingEntityType.Gsku => "KG", _ => "KP"
        });
        var codes = host.Database.GetCollection<CodeReservation>("mdm_code_reservations");
        await codes.UpdateOneAsync(x => x.Id == captured.CodeReservationId,
            Builders<CodeReservation>.Update.Set(x => x.EntityType, kind));
        var before = await codes.Find(x => x.Id == captured.CodeReservationId).SingleAsync();

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new CodeReservationRepository(host.Database, new Tenant(_tenant)).BurnIdentityBindingAsync(
                before.Id, captured.Id, before.Version, "fix4-kind:burn", MakerActor, "fix4-kind", "TEST"));

        Assert.Contains(CodeReservationRepository.BurnKindNotTransactional, refused.Message);
        var after = await codes.Find(x => x.Id == captured.CodeReservationId).SingleAsync();
        Assert.Equal(CodeReservationBindingState.PendingIdentityWrite, after.BindingState);
        Assert.Equal(before.Version, after.Version);
    }

    // ---------------------------------------------------------------------------------------------------- FIX4 item 5

    // A legacy submit (no start-sent mark) last updated an hour ago, the engine has nothing under its key: the draft
    // cancel closes it — the window runs from the update read BEFORE the claim (the claim itself writes "now").
    // With M2 (updated a moment ago → refused) this pair measures the rule from both sides.
    [Fact]
    public async Task Fix4_Item05_A_legacy_submit_updated_an_hour_ago_is_closed_by_the_draft_cancel()
    {
        var (lsku, version) = await ParkedNeverSentAsync("CQ");
        await host.Database.GetCollection<BsonDocument>("mdm_lsku_identity_workflow_operations").UpdateOneAsync(
            new BsonDocument("LskuId", new BsonBinaryData(lsku, GuidRepresentation.Standard)),
            new BsonDocument
            {
                { "$unset", new BsonDocument("LastStartSentAtUtcTicksV1", "") },
                { "$set", new BsonDocument("UpdatedAtUtcTicksV1", DateTimeOffset.UtcNow.AddHours(-1).UtcTicks) }
            });

        var cancel = await SendAsync(HttpMethod.Post, $"/api/lskus/{lsku:D}/draft/cancel",
            new { expectedVersion = version, reasonCode = "NOT_NEEDED" }, MakerToken());

        Assert.True(cancel.StatusCode == HttpStatusCode.OK, await cancel.Content.ReadAsStringAsync());
        var operation = await IdentityOperationAsync(lsku, version);
        Assert.Equal(LskuIdentityWorkflowCheckpoint.AbandonedBeforeWorkflowStart, operation.Checkpoint);
        Assert.Equal(LskuIdentityWorkflowProcessor.ClosedByDraftCancel, operation.LastFailureCode);
    }

    // ---------------------------------------------------------------------------------------------------- FIX4 items 6, 10 (c), 10 (d)

    // At the retry limit: ONE Error line for the operation (the runner, newly stuck); the maker's interactive repeat
    // keeps the limit (not counted: the streak is kept) and records the cause; later worker passes write no new Error;
    // the limit is counted as manual_reconciliation and NOT as needing_a_person.
    [Fact]
    public async Task Fix4_Item06_The_retry_limit_is_logged_once_and_an_interactive_repeat_keeps_it()
    {
        var gsku = await ApprovedGskuAsync();
        var lsku = await CreatedLskuAsync(gsku, "LO");
        var version = (await LskuAsync(lsku)).Version;
        var submitKey = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.Accepted, (await SendAsync(HttpMethod.Post, $"/api/lskus/{lsku:D}/submit",
            new { expectedVersion = version }, MakerToken(), submitKey)).StatusCode);
        long NeedingAPerson()
        {
            using var counting = TenantScope();
            return counting.ServiceProvider.GetRequiredService<ILskuIdentityWorkflowTenantPartitionDiscovery>()
                .CountNeedingAPersonAsync().GetAwaiter().GetResult();
        }
        var needingBefore = NeedingAPerson();
        host.Markets.Refusals["LO"] = (403, "REFERENCE_PROVIDER_FORBIDDEN");
        try
        {
            host.Engine.Approve(host.Engine.Open("lsku", lsku.ToString("D")), host.Approver);
            var operation = await IdentityOperationAsync(lsku, version);
            for (var cycle = 0; cycle < 45 && operation.LastFailureCode != LskuIdentityWorkflowProcessor.RetryLimitReached; cycle++)
            {
                await DueNowAsync(lsku);
                await host.RunAsync<LskuIdentityWorkflowRecoveryRunner>(runner => runner.RunCycleAsync());
                operation = await IdentityOperationAsync(lsku, version);
            }
            Assert.Equal(LskuIdentityWorkflowProcessor.RetryLimitReached, operation.LastFailureCode);
            var streak = operation.FailedAttemptCount;

            // The maker's interactive repeat (not counted) — due now, so it is processed, not answered "scheduled".
            await DueNowAsync(lsku);
            await SendAsync(HttpMethod.Post, $"/api/lskus/{lsku:D}/submit", new { expectedVersion = version }, MakerToken(),
                submitKey);
            var afterRepeat = await IdentityOperationAsync(lsku, version);
            Assert.True(afterRepeat.UpdatedAtUtcTicksV1 > operation.UpdatedAtUtcTicksV1, "the repeat was not processed");
            Assert.Equal(LskuIdentityWorkflowProcessor.RetryLimitReached, afterRepeat.LastFailureCode);
            Assert.Equal(streak, afterRepeat.FailedAttemptCount);
            Assert.False(string.IsNullOrEmpty(afterRepeat.RetryLimitCauseCode));

            for (var cycle = 0; cycle < 2; cycle++)
            {
                await DueNowAsync(lsku);
                await host.RunAsync<LskuIdentityWorkflowRecoveryRunner>(runner => runner.RunCycleAsync());
            }

            var errors = host.Logs.Entries.Count(entry => entry.Level == LogLevel.Error
                && entry.Message.Contains(operation.OperationId.ToString(), StringComparison.OrdinalIgnoreCase));
            Assert.Equal(1, errors);
            // 10 (d): a wait at its limit is manual_reconciliation, never needing_a_person.
            Assert.Equal(LskuIdentityWorkflowProcessor.RetryLimitReached, (await IdentityOperationAsync(lsku, version)).LastFailureCode);
            Assert.Equal(needingBefore, NeedingAPerson());
        }
        finally
        {
            host.Markets.Refusals.TryRemove("LO", out _);
        }
    }

    // ---------------------------------------------------------------------------------------------------- FIX4 items 7, 8, 10 (e)

    // A cancel that does not close the submit (inside the settle window) gives the lease back at once; and a cancel by
    // ANOTHER user that does close it records that user as the closure's actor.
    [Fact]
    public async Task Fix4_Item07_A_refused_close_releases_the_lease_and_Item08_the_closure_names_the_canceller()
    {
        var (lsku, version) = await ParkedNeverSentAsync("CR");
        await host.Database.GetCollection<BsonDocument>("mdm_lsku_identity_workflow_operations").UpdateOneAsync(
            new BsonDocument("LskuId", new BsonBinaryData(lsku, GuidRepresentation.Standard)),
            new BsonDocument("$set", new BsonDocument("LastStartSentAtUtcTicksV1", DateTimeOffset.UtcNow.UtcTicks)));

        var inWindow = await SendAsync(HttpMethod.Post, $"/api/lskus/{lsku:D}/draft/cancel",
            new { expectedVersion = version, reasonCode = "NOT_NEEDED" }, MakerToken());

        Assert.Equal(HttpStatusCode.Conflict, inWindow.StatusCode);
        var held = await IdentityOperationAsync(lsku, version);
        Assert.Null(held.LeaseOwner);
        Assert.Null(held.LeaseUntilUtcTicksV1);

        await host.Database.GetCollection<LskuIdentityWorkflowOperation>("mdm_lsku_identity_workflow_operations")
            .UpdateOneAsync(x => x.TenantId == _tenant && x.LskuId == lsku,
                Builders<LskuIdentityWorkflowOperation>.Update.Set(x => x.LastStartSentAtUtcTicksV1, 0L));
        var canceller = Guid.NewGuid();
        var closed = await SendAsync(HttpMethod.Post, $"/api/lskus/{lsku:D}/draft/cancel",
            new { expectedVersion = version, reasonCode = "NOT_NEEDED" }, host.Token(_tenant, canceller, MakerPermissions));

        Assert.True(closed.StatusCode == HttpStatusCode.OK, await closed.Content.ReadAsStringAsync());
        var row = await host.Database.GetCollection<Lsku>("mdm_lskus").Find(x => x.Id == lsku).SingleAsync();
        var abandoned = Assert.Single(row.AuditIntents, x => x.Operation == ProductAuditOperation.LskuIdentitySubmitAbandoned);
        Assert.Equal(canceller.ToString("D"), abandoned.ActorId);
    }

    // ---------------------------------------------------------------------------------------------------- FIX4 item 12

    // The LSKU identity gauges have their own switch: with the admission count switched off they still record; with
    // their own switch off they do not.
    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    public async Task Fix4_Item12_The_identity_gauges_have_their_own_switch(bool admissionOn, bool identityOn, bool recorded)
    {
        using var metrics = new ProductIdentityRecoveryMetrics();
        var services = host.Factory.Services;
        var identity = new LskuIdentityGauge(services.GetRequiredService<IServiceScopeFactory>(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<LskuIdentityGauge>.Instance, metrics);
        var worker = new GskuChildAdmissionGaugeWorker(services.GetRequiredService<GskuChildAdmissionGauge>(),
            Options.Create(new GskuChildAdmissionGaugeOptions { Enabled = admissionOn, LskuIdentityEnabled = identityOn }),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<GskuChildAdmissionGaugeWorker>.Instance, identity);

        await worker.RunOnceAsync(CancellationToken.None);

        Assert.Equal(recorded, metrics.Current(LskuIdentityWorkflowRecoveryRunner.Flow).HasValue);
    }

    // ---------------------------------------------------------------------------------------------------- FIX4 item 10 (a), (b)

    // (a) The closure committed and the cancel was cut off after it: the SAME cancel (its old version) is completed.
    [Fact]
    public async Task Fix4_Item10a_A_cancel_cut_off_after_the_closure_is_completed_by_its_repeat_at_the_old_version()
    {
        var (lsku, version) = await ParkedNeverSentAsync("CS");
        using (var scope = TenantScope())
        {
            var cut = CancelHandler(scope, Intercept<ILskuRepository>(scope.ServiceProvider.GetRequiredService<ILskuRepository>(),
                (method, _) => method.Name == nameof(ILskuRepository.CancelDraftAsync)
                    ? throw new IOException("cut off after the closure") : null));
            await Assert.ThrowsAsync<IOException>(() => cut.Handle(new(lsku, version, Guid.NewGuid(), "NOT_NEEDED"),
                CancellationToken.None));
        }
        Assert.Equal(version + 1, (await LskuAsync(lsku)).Version);

        var repeat = await SendAsync(HttpMethod.Post, $"/api/lskus/{lsku:D}/draft/cancel",
            new { expectedVersion = version, reasonCode = "NOT_NEEDED" }, MakerToken());

        Assert.True(repeat.StatusCode == HttpStatusCode.OK, await repeat.Content.ReadAsStringAsync());
        Assert.True((await host.Database.GetCollection<Lsku>("mdm_lskus").Find(x => x.Id == lsku).SingleAsync()).IsDeleted);
    }

    // (b) A fence whose submit operation no longer exists at all (holder is null) holds nothing: the cancel gives it back.
    [Fact]
    public async Task Fix4_Item10b_A_fence_whose_operation_is_gone_is_given_back_by_the_cancel()
    {
        var gsku = await ApprovedGskuAsync();
        var lsku = await CreatedLskuAsync(gsku, "CT");
        var version = (await LskuAsync(lsku)).Version;
        await host.Database.GetCollection<Lsku>("mdm_lskus").UpdateOneAsync(x => x.Id == lsku,
            Builders<Lsku>.Update.Set(x => x.ActiveLifecycleOperation,
                new LskuActiveLifecycleOperationBinding(LskuLifecycleOperationKind.IdentitySubmit, Guid.NewGuid(), version)));

        var cancel = await SendAsync(HttpMethod.Post, $"/api/lskus/{lsku:D}/draft/cancel",
            new { expectedVersion = version, reasonCode = "NOT_NEEDED" }, MakerToken());

        Assert.True(cancel.StatusCode == HttpStatusCode.OK, await cancel.Content.ReadAsStringAsync());
    }
}
