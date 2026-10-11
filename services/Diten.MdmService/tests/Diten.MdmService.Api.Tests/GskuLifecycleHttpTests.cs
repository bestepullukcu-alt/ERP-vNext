using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.MdmService.Api.Services.ProductItemSkuMaster;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// WP-MDM-GSKU-ACCEPT-01 item 12 — the GSKU lifecycle over HTTP against the real MDM Api: who may call (401 / 403 /
/// another tenant's record), the whole chain a maker and an approver walk (create → submit → approve → correction →
/// retirement request), and one HTTP scenario for each of the blockers B1–B6 the unit tests already pin.
/// </summary>
[Collection(MdmHttpCollection.Name)]
public sealed partial class GskuLifecycleHttpTests(MdmHttpHost host)
{
    private static readonly string[] MakerPermissions =
    [
        "mdm.gskus.read", "mdm.gskus.create", "mdm.gskus.update", "mdm.gskus.submit", "mdm.gskus.withdraw",
        "mdm.gskus.request-correction", "mdm.gskus.request-retirement"
    ];

    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _maker = Guid.NewGuid();

    [Fact]
    public async Task A_request_without_a_token_is_refused_with_401()
    {
        // Through the Gateway every call carries the tenant header; without it the tenant middleware answers 400 first.
        var client = host.Client(null);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", _tenant.ToString("D"));
        var response = await client.GetAsync($"/api/gskus/{Guid.NewGuid():D}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_token_without_the_read_permission_is_refused_with_403()
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved);
        var (gsku, _) = await CreateAsync(product);

        var response = await host.Client(host.Token(_tenant, _maker, "mdm.gskus.create")).GetAsync($"/api/gskus/{gsku:D}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_gsku_of_another_tenant_is_not_found()
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved);
        var (gsku, _) = await CreateAsync(product);

        var response = await host.Client(host.Token(Guid.NewGuid(), _maker, MakerPermissions)).GetAsync($"/api/gskus/{gsku:D}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.Client(MakerToken()).GetAsync($"/api/gskus/{gsku:D}")).StatusCode);
    }

    [Fact]
    public async Task Create_submit_approve_correct_and_request_retirement_walk_through_the_real_api()
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved);
        var (gsku, version) = await CreateAsync(product);

        var submit = await SubmitAsync(gsku, version);
        Assert.Equal(HttpStatusCode.Accepted, submit.StatusCode);
        Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval, (await DetailAsync(gsku)).Status);

        host.Engine.Approve(host.Engine.Open("gsku", gsku.ToString("D")), host.Approver);
        var approved = await IdentityCyclesUntilAsync(gsku, ProductIdentityLifecycleStatus.IdentityApproved);
        Assert.Contains("REQUEST_CORRECTION", approved.Actions);

        var correction = await PostAsync($"/api/gskus/{gsku:D}/correction-requests",
            new { expectedGskuVersion = approved.Version, packQuantity = 2.5m, packUomCode = "KGM" });
        Assert.True(correction.IsSuccessStatusCode, await correction.Content.ReadAsStringAsync());
        host.Engine.Approve(host.Engine.Latest("GskuCorrection"), host.Approver);
        var corrected = await CyclesUntilAsync(gsku, detail => detail.Uom == "KGM",
            () => host.RunAsync<GskuCorrectionRecoveryRunner>(runner => runner.RunCycleAsync()));
        Assert.Equal(2.5m, corrected.Quantity);
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, corrected.Status);

        var retirement = await PostAsync($"/api/gskus/{gsku:D}/retirement-requests",
            new { expectedGskuVersion = corrected.Version, requestReason = "PRODUCT_DISCONTINUED" });
        Assert.True(retirement.IsSuccessStatusCode, await retirement.Content.ReadAsStringAsync());
        host.Engine.Approve(host.Engine.Latest("GskuRetirementRequest"), host.Approver);
        var retired = await CyclesUntilAsync(gsku, detail => detail.Status == ProductIdentityLifecycleStatus.Retired,
            async () =>
            {
                await host.RunAsync<GskuRetirementRequestRecoveryRunner>(runner => runner.RunCycleAsync());
                await host.RunAsync<FirstGskuIdentityWorkflowRecoveryRunner>(runner => runner.RunCycleAsync());
            });
        Assert.Empty(retired.Actions.Intersect(["EDIT", "SUBMIT", "REQUEST_CORRECTION", "REQUEST_RETIREMENT"]));
    }

    // B1: a withdrawal returns the pair to Draft and leaves nothing behind — the maker submits again.
    [Fact]
    public async Task B1_A_withdrawn_pair_is_a_draft_again_and_can_be_submitted_again()
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved);
        var (gsku, version) = await CreateAsync(product);
        Assert.Equal(HttpStatusCode.Accepted, (await SubmitAsync(gsku, version)).StatusCode);
        var pending = await DetailAsync(gsku);

        var withdraw = await PostAsync($"/api/gskus/{gsku:D}/identity-approval/withdraw",
            new { expectedGskuVersion = pending.Version, reasonCode = "MAKER_CORRECTION" });

        Assert.True(withdraw.StatusCode == HttpStatusCode.OK, await withdraw.Content.ReadAsStringAsync());
        var draft = await DetailAsync(gsku);
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, draft.Status);
        Assert.Contains("SUBMIT", draft.Actions);
        Assert.Equal(HttpStatusCode.Accepted, (await SubmitAsync(gsku, draft.Version)).StatusCode);
    }

    // B2 (a): no submit before the parent Global Product is approved — refused before anything starts.
    [Fact]
    public async Task B2_Submit_under_a_draft_global_product_is_refused_and_starts_nothing()
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.Draft);
        var (gsku, version) = await CreateAsync(product);
        var starts = host.Engine.StartCalls;

        var response = await SubmitAsync(gsku, version);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("GSKU_PARENT_NOT_APPROVED", Envelope.FirstError(await Envelope.ReadAsync(response)));
        Assert.Equal(starts, host.Engine.StartCalls);
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, (await DetailAsync(gsku)).Status);
    }

    // B2 (b): the parent changed between submit and decision — the approval is not applied, the pair is a draft again,
    // and the reader is told why.
    [Fact]
    public async Task B2_An_approval_whose_parent_was_withdrawn_meanwhile_returns_the_pair_to_draft_with_its_reason()
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved);
        var (gsku, version) = await CreateAsync(product);
        Assert.Equal(HttpStatusCode.Accepted, (await SubmitAsync(gsku, version)).StatusCode);
        await host.Database.GetCollection<GlobalProduct>("mdm_global_products").UpdateOneAsync(
            x => x.Id == product, Builders<GlobalProduct>.Update.Set(x => x.LifecycleStatus, ProductIdentityLifecycleStatus.Draft));

        host.Engine.Approve(host.Engine.Open("gsku", gsku.ToString("D")), host.Approver);
        var draft = await IdentityCyclesUntilAsync(gsku, ProductIdentityLifecycleStatus.Draft);

        Assert.Equal("FIRST_GSKU_IDENTITY_PARENT_NOT_APPROVED", draft.NotAppliedReason);
        Assert.Contains("EDIT", draft.Actions);
    }

    // B3: a correction the engine refuses to start leaves no lock — the maker's next request is accepted.
    [Fact]
    public async Task B3_A_correction_the_engine_refuses_releases_the_lock_and_the_next_request_is_accepted()
    {
        var approved = await ApprovedGskuAsync();
        host.Engine.NextStartFails = ProductIdentityWorkflowTransportOutcome.Forbidden;

        var refused = await PostAsync($"/api/gskus/{approved.Id:D}/correction-requests",
            new { expectedGskuVersion = approved.Version, packQuantity = 3m, packUomCode = "C62" });
        Assert.False(refused.IsSuccessStatusCode);

        var current = await DetailAsync(approved.Id);
        Assert.Contains("REQUEST_CORRECTION", current.Actions);
        var again = await PostAsync($"/api/gskus/{approved.Id:D}/correction-requests",
            new { expectedGskuVersion = current.Version, packQuantity = 3m, packUomCode = "C62" });
        Assert.True(again.IsSuccessStatusCode, await again.Content.ReadAsStringAsync());
    }

    // B4: one lifecycle operation at a time — a retirement request while a correction is in flight is refused.
    [Fact]
    public async Task B4_A_retirement_request_while_a_correction_is_in_flight_is_refused()
    {
        var approved = await ApprovedGskuAsync();
        var correction = await PostAsync($"/api/gskus/{approved.Id:D}/correction-requests",
            new { expectedGskuVersion = approved.Version, packQuantity = 4m, packUomCode = "C62" });
        Assert.True(correction.IsSuccessStatusCode, await correction.Content.ReadAsStringAsync());
        var locked = await DetailAsync(approved.Id);

        var retirement = await PostAsync($"/api/gskus/{approved.Id:D}/retirement-requests",
            new { expectedGskuVersion = locked.Version, requestReason = "PRODUCT_DISCONTINUED" });

        Assert.Equal(HttpStatusCode.Conflict, retirement.StatusCode);
        Assert.DoesNotContain("REQUEST_RETIREMENT", locked.Actions);
    }

    // B5: direct retirement is for the system only — a human who still holds the key is refused by the endpoint.
    [Fact]
    public async Task B5_A_human_holding_the_direct_retire_permission_is_refused_as_system_only()
    {
        var approved = await ApprovedGskuAsync();

        var response = await SendAsync(HttpMethod.Post, $"/api/gskus/{approved.Id:D}/retire",
            new { expectedVersion = approved.Version, reasonCode = "X" },
            host.Token(_tenant, _maker, [.. MakerPermissions, "mdm.gskus.retire"]));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("GSKU_DIRECT_RETIREMENT_SYSTEM_ONLY", Envelope.FirstError(await Envelope.ReadAsync(response)));
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, (await DetailAsync(approved.Id)).Status);
    }

    // B6 / S1: in an Enforced tenant the worker still applies decisions — for products in scope; the others wait.
    [Fact]
    public async Task B6_In_an_enforced_tenant_the_worker_applies_an_in_scope_decision_and_leaves_the_other_waiting()
    {
        var inScope = await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved);
        var outOfScope = await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved);
        var (first, firstVersion) = await CreateAsync(inScope);
        var (second, secondVersion) = await CreateAsync(outOfScope);
        Assert.Equal(HttpStatusCode.Accepted, (await SubmitAsync(first, firstVersion)).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await SubmitAsync(second, secondVersion)).StatusCode);

        var rollout = ProductLegalEntityScopeRolloutState.CreatePreparation(_tenant, Guid.NewGuid(), Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddMinutes(-1));
        rollout.Mode = ProductLegalEntityScopeRolloutMode.Enforced;
        await host.Database.GetCollection<ProductLegalEntityScopeRolloutState>("mdm_product_legal_entity_scope_rollout_states")
            .InsertOneAsync(rollout);
        await host.Database.GetCollection<ProductLegalEntityScopePolicy>("mdm_product_legal_entity_scope_policies")
            .InsertOneAsync(ProductLegalEntityScopePolicy.Create(_tenant, inScope, Guid.NewGuid(),
                ProductLegalEntityScopeMode.GroupWide, [], Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-1)));

        host.Engine.Approve(host.Engine.Open("gsku", first.ToString("D")), host.Approver);
        host.Engine.Approve(host.Engine.Open("gsku", second.ToString("D")), host.Approver);
        var gskus = host.Database.GetCollection<Gsku>("mdm_gskus");
        for (var cycle = 0; cycle < 40; cycle++)
        {
            await host.RunAsync<FirstGskuIdentityWorkflowRecoveryRunner>(runner => runner.RunCycleAsync());
            if ((await gskus.Find(x => x.Id == first).SingleAsync()).LifecycleStatus == ProductIdentityLifecycleStatus.IdentityApproved)
                break;
            await Task.Delay(250);
        }

        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, (await gskus.Find(x => x.Id == first).SingleAsync()).LifecycleStatus);
        Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval, (await gskus.Find(x => x.Id == second).SingleAsync()).LifecycleStatus);
    }

    // B7: no edit while a submit is in flight — the engine's answer is unknown, the draft stays as submitted.
    [Fact]
    public async Task B7_An_edit_while_the_submit_is_in_flight_is_refused()
    {
        // In flight = the engine's answer to the start is lost AND the engine cannot (yet) say what it holds. (FIX2: a
        // start the engine answered "nothing arrived" for is not in flight any more — see F1_… for that way out.)
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved);
        var (gsku, version) = await CreateAsync(product);
        host.Engine.NextStartFails = ProductIdentityWorkflowTransportOutcome.Timeout;
        host.Engine.LookupFailure = ProductIdentityWorkflowTransportOutcome.Timeout;
        try
        {
            var submit = await SubmitAsync(gsku, version);
            Assert.False(submit.StatusCode is HttpStatusCode.OK, await submit.Content.ReadAsStringAsync());
            var draft = await DetailAsync(gsku);
            Assert.Equal(ProductIdentityLifecycleStatus.Draft, draft.Status);
            Assert.Equal("SUBMIT_IN_FLIGHT", draft.SubmitState);

            var edit = await SendAsync(HttpMethod.Put, $"/api/gskus/{gsku:D}",
                new { expectedVersion = draft.Version, packQuantity = 7m, packUomCode = "C62" }, MakerToken());

            Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);
            Assert.Equal("GSKU_IDENTITY_SUBMIT_IN_FLIGHT", Envelope.FirstError(await Envelope.ReadAsync(edit)));
            Assert.DoesNotContain("EDIT", draft.Actions);
        }
        finally
        {
            host.Engine.LookupFailure = null;
        }
        // Its way out is automatic: once the engine can answer, the worker learns nothing arrived and the draft is the
        // maker's again (resubmit or edit).
        await CyclesUntilAsync(gsku, detail => detail.SubmitState == "RESUBMIT_REQUIRED",
            () => host.RunAsync<FirstGskuIdentityWorkflowRecoveryRunner>(runner => runner.RunCycleAsync()));
    }

    // The operation store's index shape, counted on this host's throwaway mongod (moved here from the Mongo tests:
    // the shared test database collects every branch's indexes, and a per-run database there is forbidden). Five
    // since GSKU FIX1 — the fifth answers "is a submit in flight" for an edit (B7), added under its own name: an
    // existing index's keys are never changed. Every index leads with TenantId, and no stored field holds a secret.
    [Fact]
    public async Task The_identity_operation_store_owns_exactly_five_tenant_led_indexes_and_stores_no_secret()
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved);
        var (gsku, version) = await CreateAsync(product);
        Assert.Equal(HttpStatusCode.Accepted, (await SubmitAsync(gsku, version)).StatusCode);
        var collection = host.Database.GetCollection<MongoDB.Bson.BsonDocument>("mdm_first_gsku_identity_workflow_operations");

        var owned = (await (await collection.Indexes.ListAsync()).ToListAsync())
            .Where(x => x["name"].AsString.Contains("first_gsku_identity_workflow", StringComparison.Ordinal)).ToArray();
        var stored = await collection.Find(new MongoDB.Bson.BsonDocument("TenantId",
            new MongoDB.Bson.BsonBinaryData(_tenant, MongoDB.Bson.GuidRepresentation.Standard))).SingleAsync();

        Assert.Equal(5, owned.Length);
        Assert.Contains(owned, index => index["name"].AsString == "ix_mdm_first_gsku_identity_workflow_gsku_unfinished");
        Assert.All(owned, index => Assert.Equal("TenantId", index["key"].AsBsonDocument.GetElement(0).Name));
        Assert.True(stored["CreatedAtUtcTicksV1"].IsInt64);
        Assert.DoesNotContain(stored.Names, n => n.Contains("Token", StringComparison.OrdinalIgnoreCase)
            || n.Contains("Secret", StringComparison.OrdinalIgnoreCase)
            || n.Contains("Credential", StringComparison.OrdinalIgnoreCase)
            || n.Contains("Authorization", StringComparison.OrdinalIgnoreCase));
    }

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

    private async Task<(Guid Id, int Version)> CreateAsync(Guid product)
    {
        var response = await PostAsync("/api/gskus/drafts", new { globalProductId = product, packQuantity = 1m, packUomCode = "C62" });
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var data = (await Envelope.ReadAsync(response)).GetProperty("data");
        return (data.GetProperty("gskuId").GetGuid(), data.GetProperty("version").GetInt32());
    }

    private Task<HttpResponseMessage> SubmitAsync(Guid gsku, int version) =>
        PostAsync($"/api/gskus/{gsku:D}/submit", new { expectedVersion = version });

    private Task<HttpResponseMessage> PostAsync(string path, object body) =>
        SendAsync(HttpMethod.Post, path, body, MakerToken());

    // Every write carries a fresh Idempotency-Key unless a test repeats one, as the Web proxy does for the same submit.
    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object body, string token,
        Guid? key = null)
    {
        // TEST ADAPTATION (S3b, R-04): this older flow opts in to the lot / serial flags a submit now requires.
        await S3bSubmitFlags.FillForLegacyFlowAsync(host, path);
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", (key ?? Guid.NewGuid()).ToString("D"));
        return await host.Client(token).SendAsync(request);
    }

    private async Task<Detail> DetailAsync(Guid gsku)
    {
        var response = await host.Client(MakerToken()).GetAsync($"/api/gskus/{gsku:D}");
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var data = (await Envelope.ReadAsync(response)).GetProperty("data");
        return new(
            (ProductIdentityLifecycleStatus)data.GetProperty("lifecycleStatus").GetInt32(),
            data.GetProperty("gskuVersion").GetInt32(),
            data.GetProperty("packQuantity").GetDecimal(),
            data.GetProperty("packUomCode").GetString()!,
            data.GetProperty("availableActions").EnumerateArray().Select(x => x.GetString()!).ToArray(),
            data.TryGetProperty("identityNotAppliedReason", out var reason) && reason.ValueKind == JsonValueKind.String
                ? reason.GetString() : null,
            data.TryGetProperty("identitySubmitState", out var state) && state.ValueKind == JsonValueKind.String
                ? state.GetString() : null,
            data.TryGetProperty("lifecycleRequestState", out var requestState) && requestState.ValueKind == JsonValueKind.String
                ? requestState.GetString() : null);
    }

    private async Task<(Guid Id, int Version)> ApprovedGskuAsync()
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved);
        var (gsku, version) = await CreateAsync(product);
        Assert.Equal(HttpStatusCode.Accepted, (await SubmitAsync(gsku, version)).StatusCode);
        host.Engine.Approve(host.Engine.Open("gsku", gsku.ToString("D")), host.Approver);
        var approved = await IdentityCyclesUntilAsync(gsku, ProductIdentityLifecycleStatus.IdentityApproved);
        return (gsku, approved.Version);
    }

    private Task<Detail> IdentityCyclesUntilAsync(Guid gsku, ProductIdentityLifecycleStatus status) =>
        CyclesUntilAsync(gsku, detail => detail.Status == status,
            () => host.RunAsync<FirstGskuIdentityWorkflowRecoveryRunner>(runner => runner.RunCycleAsync()));

    // A worker cycle, then a look — until the decision is applied (the retry schedule is one second in this host).
    private async Task<Detail> CyclesUntilAsync(Guid gsku, Func<Detail, bool> done, Func<Task> cycle)
    {
        Detail detail = await DetailAsync(gsku);
        for (var attempt = 0; attempt < 40 && !done(detail); attempt++)
        {
            await cycle();
            detail = await DetailAsync(gsku);
            if (!done(detail)) await Task.Delay(250);
        }
        Assert.True(done(detail), $"Not reached: {detail}");
        return detail;
    }

    private sealed record Detail(
        ProductIdentityLifecycleStatus Status,
        int Version,
        decimal Quantity,
        string Uom,
        IReadOnlyList<string> Actions,
        string? NotAppliedReason,
        string? SubmitState = null,
        string? LifecycleRequestState = null);
}
