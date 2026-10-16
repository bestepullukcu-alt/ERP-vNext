using System.Text.Json.Nodes;
using Diten.ManufacturingService.Domain.Entities;
using Diten.ManufacturingService.Domain.Repositories;
using Diten.ManufacturingService.Infrastructure.Authorization;
using Diten.ManufacturingService.Persistence.Repositories;
using MongoDB.Driver;
using Xunit;

namespace Diten.ManufacturingService.Tests.Api;

/// <summary>
/// MOD-0193 acceptance (pack §16) over HTTP against the real Program and a real Mongo replica set. Skipped — with the
/// reason printed — when <c>MANUFACTURING_TEST_MONGO</c> is unset; never green by having nothing to check.
/// </summary>
public sealed class BomApiTests : IClassFixture<BomApiFixture>
{
    private readonly BomApiFactory _factory;

    public BomApiTests(BomApiFixture fixture)
    {
        Skip.If(BomApiFactory.Connection is null, $"{BomApiFactory.EnvironmentVariable} is not set — no isolated Mongo replica set.");
        _factory = fixture.Factory!;
    }

    private static readonly string[] FrozenBomViewFields =
        ["bomVersionId", "components", "contractVersion", "effectiveFrom", "effectiveTo", "itemId", "routing", "status", "version"];

    [SkippableFact]
    public async Task Draft_is_created_with_the_next_revision_and_the_frozen_fields()
    {
        var caller = new BomCaller(_factory);
        var item = Guid.NewGuid();
        var first = await caller.CreateDraft(item, (Guid.NewGuid(), "2.000", "EA", 10));
        var second = await caller.CreateDraft(item, (Guid.NewGuid(), "1", "KG", 10));

        Assert.Equal("Draft", (string)first["status"]!);
        Assert.Equal(1, (int)first["version"]!);
        Assert.Equal(2, (int)second["version"]!);
        Assert.Equal("v1", (string)first["contractVersion"]!);
        Assert.Subset(first.AsObject().Select(p => p.Key).ToHashSet(), FrozenBomViewFields.ToHashSet());
        Assert.Equal(["componentItemId", "quantity", "uomId", "position", "alternates"], first["components"]![0]!.AsObject().Select(p => p.Key).ToArray());
        Assert.Equal(["routingId", "steps"], first["routing"]!.AsObject().Select(p => p.Key).ToArray());
    }

    [SkippableFact]
    public async Task Release_supersedes_the_previous_version_in_the_same_change_and_current_follows()
    {
        var caller = new BomCaller(_factory);
        var item = Guid.NewGuid();
        var v1 = await caller.Release(await caller.CreateDraft(item, (Guid.NewGuid(), "2.000", "EA", 10)));
        var current1 = await caller.Send(HttpMethod.Get, $"/api/bom/{item}/current");
        Assert.Equal(200, current1.Status);
        Assert.Equal((string)v1["bomVersionId"]!, (string)current1.Body!["bomVersionId"]!);

        var v2 = await caller.Release(await caller.CreateDraft(item, (Guid.NewGuid(), "3.000", "EA", 10)));
        var old = await caller.Send(HttpMethod.Get, $"/api/bom/version/{v1["bomVersionId"]}");

        Assert.Equal("Effective", (string)v2["status"]!);
        Assert.Equal("Superseded", (string)old.Body!["status"]!);
        Assert.Equal((string)v2["effectiveFrom"]!, (string)old.Body!["effectiveTo"]!);
        Assert.Equal("CC-2026-001", (string)v2["changeControlRef"]!);
        var current2 = await caller.Send(HttpMethod.Get, $"/api/bom/{item}/current");
        Assert.Equal((string)v2["bomVersionId"]!, (string)current2.Body!["bomVersionId"]!);

        var effective = await _factory.Db().GetCollection<BomVersion>("mfg_boms")
            .CountDocumentsAsync(b => b.TenantId == caller.Tenant && b.ItemId == item && b.Status == BomStatus.Effective);
        Assert.Equal(1, effective);
    }

    [SkippableFact]
    public async Task Current_as_of_a_day_before_any_release_is_unknown()
    {
        var caller = new BomCaller(_factory);
        var item = Guid.NewGuid();
        await caller.Release(await caller.CreateDraft(item, (Guid.NewGuid(), "1", "EA", 10)));
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)).ToString("yyyy-MM-dd");

        var past = await caller.Send(HttpMethod.Get, $"/api/bom/{item}/current?asOfDate={yesterday}");
        var today = await caller.Send(HttpMethod.Get, $"/api/bom/{item}/current?asOfDate={DateOnly.FromDateTime(DateTime.UtcNow):yyyy-MM-dd}");

        Assert.Equal(404, past.Status);
        Assert.Equal("UNKNOWN_BOM", (string)past.Body!["error"]!["code"]!);
        Assert.Equal(200, today.Status);
    }

    [SkippableFact]
    public async Task Explode_multiplies_the_effective_bom_as_the_frozen_example_does()
    {
        var caller = new BomCaller(_factory);
        var item = Guid.NewGuid();
        var comp1 = Guid.NewGuid();
        var comp2 = Guid.NewGuid();
        var v1 = await caller.Release(await caller.CreateDraft(item, (comp1, "2.000", "EA", 10), (comp2, "0.500", "L", 20)));

        var (status, body, _) = await caller.Send(HttpMethod.Post, "/api/bom/explode", new { itemId = item, quantity = "100.000" });

        Assert.Equal(200, status);
        Assert.Equal("100.000", (string)body!["requestedQuantity"]!);
        Assert.Equal((string)v1["bomVersionId"]!, (string)body["bomVersionId"]!);
        var requirements = body["requirements"]!.AsArray().Select(r => ((Guid)r!["componentItemId"]!, (string)r["requiredQuantity"]!, (string)r["uomId"]!)).ToArray();
        Assert.Equal(new[] { (comp1, "200.000", "EA"), (comp2, "50.000", "L") }, requirements);
    }

    [SkippableFact]
    public async Task Unknown_bom_answers_the_frozen_error_shape_with_the_callers_correlation()
    {
        var caller = new BomCaller(_factory);
        var correlation = Guid.NewGuid().ToString();

        var (status, body, raw) = await caller.Send(HttpMethod.Post, "/api/bom/explode", new { itemId = Guid.NewGuid(), quantity = "1" }, correlation);

        Assert.Equal(404, status);
        Assert.Equal(["contractVersion", "error"], body!.AsObject().Select(p => p.Key).Order().ToArray());
        Assert.Equal(["code", "correlationId", "message"], body["error"]!.AsObject().Select(p => p.Key).Order().ToArray());
        Assert.Equal("UNKNOWN_BOM", (string)body["error"]!["code"]!);
        Assert.Equal(correlation, (string)body["error"]!["correlationId"]!);
        Assert.Equal(correlation, raw.Headers.GetValues("X-Correlation-Id").Single());
    }

    [SkippableFact]
    public async Task Only_a_draft_changes_and_a_stale_row_version_is_a_conflict()
    {
        var caller = new BomCaller(_factory);
        var item = Guid.NewGuid();
        var component = Guid.NewGuid();
        var draft = await caller.CreateDraft(item, (component, "1", "EA", 10));
        var id = (string)draft["bomVersionId"]!;
        var update = new { description = "Revised", components = new[] { new { componentItemId = component, quantity = "1.5", uomId = "EA", position = 10 } }, rowVersion = 1 };

        var edited = await caller.Send(HttpMethod.Put, $"/api/bom/version/{id}", update);
        var stale = await caller.Send(HttpMethod.Put, $"/api/bom/version/{id}", update);
        Assert.Equal(200, edited.Status);
        Assert.Equal(2, (int)edited.Body!["rowVersion"]!);
        Assert.Null(edited.Body["routing"]);
        Assert.Equal(409, stale.Status);
        Assert.Equal("CONCURRENCY_CONFLICT", (string)stale.Body!["error"]!["code"]!);

        var released = await caller.Release(edited.Body);
        var afterRelease = await caller.Send(HttpMethod.Put, $"/api/bom/version/{id}", update with { rowVersion = (int)released["rowVersion"]! });
        var deleteEffective = await caller.Send(HttpMethod.Delete, $"/api/bom/version/{id}?rowVersion={(int)released["rowVersion"]!}");
        Assert.Equal(409, afterRelease.Status);
        Assert.Equal("BOM_NOT_DRAFT", (string)afterRelease.Body!["error"]!["code"]!);
        Assert.Equal(409, deleteEffective.Status);
    }

    [SkippableFact]
    public async Task A_deleted_draft_disappears_and_its_revision_number_is_not_reused()
    {
        var caller = new BomCaller(_factory);
        var item = Guid.NewGuid();
        var draft = await caller.CreateDraft(item, (Guid.NewGuid(), "1", "EA", 10));

        var deleted = await caller.Send(HttpMethod.Delete, $"/api/bom/version/{draft["bomVersionId"]}?rowVersion=1");
        var read = await caller.Send(HttpMethod.Get, $"/api/bom/version/{draft["bomVersionId"]}");
        var next = await caller.CreateDraft(item, (Guid.NewGuid(), "1", "EA", 10));

        Assert.Equal(204, deleted.Status);
        Assert.Equal(404, read.Status);
        Assert.Equal(2, (int)next["version"]!);
    }

    [SkippableFact]
    public async Task Releasing_a_version_that_closes_a_loop_is_refused_and_changes_nothing()
    {
        var caller = new BomCaller(_factory);
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        await caller.Release(await caller.CreateDraft(a, (b, "1", "EA", 10)));
        await caller.Release(await caller.CreateDraft(b, (c, "1", "EA", 10)));
        var loop = await caller.CreateDraft(c, (a, "1", "EA", 10));

        var (status, body, _) = await caller.Send(HttpMethod.Post, $"/api/bom/version/{loop["bomVersionId"]}/release", new { changeControlRef = "CC-LOOP", rowVersion = 1 });
        var after = await caller.Send(HttpMethod.Get, $"/api/bom/version/{loop["bomVersionId"]}");

        Assert.Equal(409, status);
        Assert.Equal("BOM_CYCLE", (string)body!["error"]!["code"]!);
        Assert.Equal("Draft", (string)after.Body!["status"]!);
    }

    [SkippableFact]
    public async Task Self_reference_and_unknown_items_are_refused_and_write_nothing()
    {
        var caller = new BomCaller(_factory);
        var item = Guid.NewGuid();
        var unknown = Guid.NewGuid();
        _factory.Products.Unknown.Add(unknown);

        var self = await caller.Send(HttpMethod.Post, "/api/bom/versions", BomCaller.Draft(item, (item, "1", "EA", 10)));
        var missing = await caller.Send(HttpMethod.Post, "/api/bom/versions", BomCaller.Draft(item, (unknown, "1", "EA", 10)));

        Assert.Equal(422, self.Status);
        Assert.Equal("SELF_REFERENCE", (string)self.Body!["error"]!["code"]!);
        Assert.Equal(422, missing.Status);
        Assert.Equal("UNKNOWN_ITEM", (string)missing.Body!["error"]!["code"]!);
        Assert.Equal(0, await _factory.Db().GetCollection<BomVersion>("mfg_boms").CountDocumentsAsync(x => x.TenantId == caller.Tenant));
        Assert.Equal(0, await _factory.Db().GetCollection<BomHistoryEntry>("mfg_bom_history").CountDocumentsAsync(x => x.TenantId == caller.Tenant));
    }

    [SkippableFact]
    public async Task Another_tenant_or_legal_entity_never_sees_or_changes_a_bom()
    {
        var owner = new BomCaller(_factory);
        var item = Guid.NewGuid();
        var draft = await owner.CreateDraft(item, (Guid.NewGuid(), "1", "EA", 10));
        await owner.Release(await owner.CreateDraft(Guid.NewGuid(), (Guid.NewGuid(), "1", "EA", 10)));
        var id = (string)draft["bomVersionId"]!;

        foreach (var stranger in new[] { new BomCaller(_factory), new BomCaller(_factory, tenant: owner.Tenant) })
        {
            Assert.Equal(404, (await stranger.Send(HttpMethod.Get, $"/api/bom/version/{id}")).Status);
            Assert.Equal(404, (await stranger.Send(HttpMethod.Get, $"/api/bom/version/{id}/history")).Status);
            Assert.Equal(404, (await stranger.Send(HttpMethod.Post, $"/api/bom/version/{id}/release", new { changeControlRef = "CC-X", rowVersion = 1 })).Status);
            Assert.Equal(404, (await stranger.Send(HttpMethod.Delete, $"/api/bom/version/{id}?rowVersion=1")).Status);
            Assert.Equal(404, (await stranger.Send(HttpMethod.Get, $"/api/bom/{item}/current")).Status);
            var list = await stranger.Send(HttpMethod.Get, "/api/bom/versions");
            Assert.Equal(0, (long)list.Body!["data"]!["total"]!);
        }

        var mine = await owner.Send(HttpMethod.Get, $"/api/bom/version/{id}");
        Assert.Equal("Draft", (string)mine.Body!["status"]!);
        Assert.Equal(2, (long)(await owner.Send(HttpMethod.Get, "/api/bom/versions")).Body!["data"]!["total"]!);
    }

    [SkippableFact]
    public async Task Every_write_leaves_a_history_entry_readable_on_the_record()
    {
        var caller = new BomCaller(_factory);
        var item = Guid.NewGuid();
        var component = Guid.NewGuid();
        var v1 = await caller.Release(await caller.CreateDraft(item, (component, "1", "EA", 10)));
        var draft = await caller.CreateDraft(item, (component, "1", "EA", 10));
        var correlation = Guid.NewGuid().ToString();
        var edited = await caller.Send(HttpMethod.Put, $"/api/bom/version/{draft["bomVersionId"]}",
            new { description = "Changed", components = new[] { new { componentItemId = component, quantity = "2", uomId = "EA", position = 10 } }, rowVersion = 1 }, correlation);
        await caller.Release(edited.Body!, "CC-2026-009");

        var history = await caller.Send(HttpMethod.Get, $"/api/bom/version/{draft["bomVersionId"]}/history");
        var entries = history.Body!["entries"]!.AsArray();
        Assert.Equal(["Created", "Updated", "Released"], entries.Select(e => (string)e!["operation"]!).ToArray());
        var updated = entries[1]!;
        Assert.Equal(["description", "components", "routing"], updated["changedFields"]!.AsArray().Select(f => (string)f!).ToArray());
        Assert.Equal(correlation, (string)updated["correlationId"]!);
        Assert.Equal(caller.Actor, (Guid)updated["actorId"]!);
        Assert.Equal("BOM Tester", (string)updated["actorDisplayName"]!);
        Assert.Equal("CC-2026-009", (string)entries[2]!["changeControlRef"]!);

        var superseded = await caller.Send(HttpMethod.Get, $"/api/bom/version/{v1["bomVersionId"]}/history");
        Assert.Equal(["Created", "Released", "Superseded"], superseded.Body!["entries"]!.AsArray().Select(e => (string)e!["operation"]!).ToArray());
    }

    [SkippableFact]
    public async Task When_the_history_cannot_be_written_the_bom_is_not_written_either()
    {
        var tenant = Guid.NewGuid();
        var le = Guid.NewGuid();
        var bom = new BomVersion { TenantId = tenant, LegalEntityId = le, ItemId = Guid.NewGuid(), RevisionNo = 1, Version = 1,
            Components = [new BomComponentLine { ComponentItemId = Guid.NewGuid(), Quantity = "1", UomId = "EA", Position = 10 }] };
        var duplicateId = Guid.NewGuid();
        var history = new[] { duplicateId, duplicateId }.Select(id => new BomHistoryEntry
        {
            Id = id, TenantId = tenant, LegalEntityId = le, BomVersionId = bom.Id, ItemId = bom.ItemId, Operation = BomHistoryOperation.Created,
            OccurredAtUtc = DateTimeOffset.UtcNow
        }).ToList();
        var journal = new BomHistoryJournal(_factory.Db());

        var result = await journal.CommitAsync(new BomChangeSet(tenant, le, bom, [], history), CancellationToken.None);

        Assert.Equal(BomCommitResult.Conflict, result);
        Assert.Equal(0, await _factory.Db().GetCollection<BomVersion>("mfg_boms").CountDocumentsAsync(x => x.TenantId == tenant));
        Assert.Equal(0, await _factory.Db().GetCollection<BomHistoryEntry>("mfg_bom_history").CountDocumentsAsync(x => x.TenantId == tenant));
    }

    [SkippableFact]
    public async Task Two_simultaneous_releases_of_one_item_leave_exactly_one_effective_version()
    {
        var caller = new BomCaller(_factory);
        var item = Guid.NewGuid();
        var drafts = new[]
        {
            await caller.CreateDraft(item, (Guid.NewGuid(), "1", "EA", 10)),
            await caller.CreateDraft(item, (Guid.NewGuid(), "2", "EA", 10)),
            await caller.CreateDraft(item, (Guid.NewGuid(), "3", "EA", 10))
        };

        var results = await Task.WhenAll(drafts.Select(d => caller.Send(HttpMethod.Post, $"/api/bom/version/{d["bomVersionId"]}/release", new { changeControlRef = "CC-RACE", rowVersion = 1 })));

        Assert.All(results, r => Assert.True(r.Status is 200 or 409, $"release answered {r.Status}: {r.Body}"));
        Assert.Contains(results, r => r.Status == 200);
        var effective = await _factory.Db().GetCollection<BomVersion>("mfg_boms")
            .CountDocumentsAsync(b => b.TenantId == caller.Tenant && b.ItemId == item && b.Status == BomStatus.Effective);
        Assert.Equal(1, effective);
    }

    [SkippableFact]
    public async Task Missing_token_is_401_and_missing_permission_is_403()
    {
        var reader = new BomCaller(_factory, permissions: [BomPermissions.Read]);
        var anonymous = await reader.Send(HttpMethod.Get, "/api/bom/versions", authenticated: false);
        var create = await reader.Send(HttpMethod.Post, "/api/bom/versions", BomCaller.Draft(Guid.NewGuid(), (Guid.NewGuid(), "1", "EA", 10)));
        var list = await reader.Send(HttpMethod.Get, "/api/bom/versions");

        Assert.Equal(401, anonymous.Status);
        Assert.Equal(403, create.Status);
        Assert.Equal(200, list.Status);
    }

    [SkippableFact]
    public async Task Malformed_input_answers_invalid_request_in_the_contract_shape()
    {
        var caller = new BomCaller(_factory);
        var floatQuantity = await caller.Send(HttpMethod.Post, "/api/bom/versions", BomCaller.Draft(Guid.NewGuid(), (Guid.NewGuid(), "1e3", "EA", 10)));
        var badUuid = await caller.Send(HttpMethod.Post, "/api/bom/versions", "{\"itemId\":\"not-a-uuid\",\"components\":[]}");
        var noLines = await caller.Send(HttpMethod.Post, "/api/bom/versions", new { itemId = Guid.NewGuid(), components = Array.Empty<object>() });
        var duplicatePosition = await caller.Send(HttpMethod.Post, "/api/bom/versions", BomCaller.Draft(Guid.NewGuid(), (Guid.NewGuid(), "1", "EA", 10), (Guid.NewGuid(), "1", "EA", 10)));

        foreach (var r in new[] { floatQuantity, badUuid, noLines, duplicatePosition })
        {
            Assert.Equal(400, r.Status);
            Assert.Equal("INVALID_REQUEST", (string)r.Body!["error"]!["code"]!);
            Assert.False(string.IsNullOrWhiteSpace((string)r.Body!["error"]!["correlationId"]!));
        }
    }
}

public sealed class BomListAndExportTests : IClassFixture<BomApiFixture>
{
    private readonly BomApiFactory _factory;

    public BomListAndExportTests(BomApiFixture fixture)
    {
        Skip.If(BomApiFactory.Connection is null, $"{BomApiFactory.EnvironmentVariable} is not set — no isolated Mongo replica set.");
        _factory = fixture.Factory!;
    }

    private async Task<(BomCaller Caller, Guid Effective, Guid Draft)> Seed()
    {
        var caller = new BomCaller(_factory);
        var effective = Guid.NewGuid();
        await caller.Release(await caller.CreateDraft(effective, (Guid.NewGuid(), "1", "EA", 10)));
        var draft = Guid.NewGuid();
        for (var i = 0; i < 3; i++)
        {
            await caller.CreateDraft(draft, (Guid.NewGuid(), "1", "EA", 10));
        }

        return (caller, effective, draft);
    }

    [SkippableFact]
    public async Task List_speaks_the_server_mode_contract_total_filtered_and_a_page()
    {
        var (caller, _, draft) = await Seed();

        var page = await caller.Send(HttpMethod.Get, "/api/bom/versions?start=1&length=2&orderBy=version&orderDir=asc&status=Draft");
        var data = page.Body!["data"]!;

        Assert.Equal(200, page.Status);
        Assert.Equal(4, (long)data["total"]!);
        Assert.Equal(3, (long)data["filteredTotal"]!);
        var items = data["items"]!.AsArray();
        Assert.Equal([2, 3], items.Select(i => (int)i!["version"]!).ToArray());
        Assert.All(items, i => Assert.Equal(draft, (Guid)i!["itemId"]!));
    }

    [SkippableFact]
    public async Task Search_matches_the_description_or_an_exact_item_id()
    {
        var (caller, effective, _) = await Seed();

        var byItem = await caller.Send(HttpMethod.Get, $"/api/bom/versions?search={effective}");
        var byText = await caller.Send(HttpMethod.Get, "/api/bom/versions?search=FORM");
        var none = await caller.Send(HttpMethod.Get, "/api/bom/versions?search=no-such-thing");

        Assert.Equal(1, (long)byItem.Body!["data"]!["filteredTotal"]!);
        Assert.Equal(4, (long)byText.Body!["data"]!["filteredTotal"]!);
        Assert.Equal(0, (long)none.Body!["data"]!["filteredTotal"]!);
    }

    [SkippableFact]
    public async Task An_unknown_order_key_or_status_is_refused_not_ignored()
    {
        var caller = new BomCaller(_factory);
        var badOrder = await caller.Send(HttpMethod.Get, "/api/bom/versions?orderBy=tenantId");
        var badStatus = await caller.Send(HttpMethod.Get, "/api/bom/versions?status=Archived");
        var badLength = await caller.Send(HttpMethod.Get, "/api/bom/versions?length=1000");

        foreach (var r in new[] { badOrder, badStatus, badLength })
        {
            Assert.Equal(400, r.Status);
            Assert.Equal("INVALID_REQUEST", (string)r.Body!["error"]!["code"]!);
        }
    }

    [SkippableFact]
    public async Task Export_writes_every_matching_row_with_the_requested_columns_in_the_callers_language()
    {
        var (caller, _, _) = await Seed();

        var (status, _, raw) = await caller.Send(HttpMethod.Get, "/api/bom/versions/export?format=csv&columns=version,status&status=Draft&orderBy=version&orderDir=asc");
        var turkish = await SendWithLanguage(caller, "/api/bom/versions/export?format=csv&columns=status&status=Effective", "tr-TR");

        Assert.Equal(200, status);
        Assert.Equal("text/csv", raw.Content.Headers.ContentType!.MediaType);
        var lines = (await raw.Content.ReadAsStringAsync()).TrimStart('\uFEFF').Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd('\r')).ToArray();
        Assert.Equal(["Version,Status", "1,Draft", "2,Draft", "3,Draft"], lines);
        Assert.Contains("Yürürlükte", turkish);
        Assert.DoesNotContain("Effective", turkish);
    }

    [SkippableFact]
    public async Task Export_refuses_an_unknown_column_and_needs_its_own_permission()
    {
        var caller = new BomCaller(_factory);
        var reader = new BomCaller(_factory, permissions: [Diten.ManufacturingService.Infrastructure.Authorization.BomPermissions.Read]);

        var leak = await caller.Send(HttpMethod.Get, "/api/bom/versions/export?columns=tenantId");
        var denied = await reader.Send(HttpMethod.Get, "/api/bom/versions/export");

        Assert.Equal(400, leak.Status);
        Assert.Equal("EXPORT_COLUMNS_INVALID", (string)leak.Body!["error"]!["code"]!);
        Assert.Equal(403, denied.Status);
    }

    private async Task<string> SendWithLanguage(BomCaller caller, string path, string language)
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", BomApiFactory.Token(caller.Tenant, caller.LegalEntity, caller.Actor));
        request.Headers.TryAddWithoutValidation("X-Tenant-Id", caller.Tenant.ToString());
        request.Headers.TryAddWithoutValidation("X-Legal-Entity-Id", caller.LegalEntity.ToString());
        request.Headers.TryAddWithoutValidation("Accept-Language", language);
        using var response = await client.SendAsync(request);
        Assert.Equal(200, (int)response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }
}

public sealed class BomApiFixture : IDisposable
{
    public BomApiFactory? Factory { get; } = BomApiFactory.Connection is null ? null : new BomApiFactory();

    public void Dispose() => Factory?.Dispose();
}
