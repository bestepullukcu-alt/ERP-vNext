using System.Net;
using System.Net.Http.Json;
using Diten.MdmService.Api.Services.ProductItemSkuMaster;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts.Audit;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Audit;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
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
public sealed class FgFix1HttpTests(MdmHttpHost host)
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

    // ---------------------------------------------------------------------------------------------------- helpers

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

    private async Task<Guid> ApprovedGskuAsync()
    {
        var product = await SeedProductAsync();
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
