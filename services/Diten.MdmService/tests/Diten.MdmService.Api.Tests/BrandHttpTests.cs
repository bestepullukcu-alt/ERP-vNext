using System.Net;
using System.Text.Json;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// WP-MDM-BRAND-ACCEPT-01 FIX1 — the Brand (and, where the rule is shared, Product) master measured on the PRODUCTION
/// stack: the real controllers, permission policy, MediatR pipeline (audit forwarding included), handlers, repositories,
/// class maps and unique indexes, over a throwaway replica-set mongod (MdmHttpHost). Every test makes its own tenant, so
/// tests never see each other's rows.
/// </summary>
[Collection(MdmHttpCollection.Name)]
public sealed class BrandHttpTests(MdmHttpHost host)
{
    private const string Read = "mdm.brands.read";
    private const string Create = "mdm.brands.create";
    private const string Update = "mdm.brands.update";
    private const string Archive = "mdm.brands.archive";
    private const string ProductsRead = "mdm.products.read";
    private const string ProductsCreate = "mdm.products.create";
    private const string ProductsUpdate = "mdm.products.update";
    private const string ProductsArchive = "mdm.products.archive";

    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _user = Guid.NewGuid();

    // ── item 1 — every write leaves an append-only record; the forwarder failing loses none ─────────────────────────

    [Fact]
    public async Task Every_brand_write_leaves_one_history_entry_with_actor_correlation_and_before_after()
    {
        var client = Steward();
        var id = await CreateAsync(client, Code("HIST"), correlationId: "corr-create");
        var updated = await SendAsync(client, HttpMethod.Put, $"/api/mdm/brands/{id}",
            Body(Code("HIST"), name: "Renamed", expectedVersion: 0), "corr-update");
        Assert.Equal(HttpStatusCode.NoContent, updated.StatusCode);
        var archived = await SendAsync(client, HttpMethod.Post, $"/api/mdm/brands/{id}/archive", null, "corr-archive");
        Assert.Equal(HttpStatusCode.NoContent, archived.StatusCode);

        var history = await HistoryAsync(id);
        Assert.Equal([BrandHistoryEntry.EventCreated, BrandHistoryEntry.EventUpdated, BrandHistoryEntry.EventArchived],
            history.Select(x => x.EventType));
        Assert.All(history, x => Assert.Equal(_user.ToString("D"), x.Actor));
        Assert.Equal(["corr-create", "corr-update", "corr-archive"], history.Select(x => x.CorrelationId));
        Assert.Equal([0, 1, 2], history.Select(x => x.ResultVersion));

        // The edit's entry says what the brand WAS and what it BECAME.
        Assert.Null(history[0].Before);
        Assert.Equal("Brand HIST", history[1].Before!.BrandName);
        Assert.Equal("Renamed", history[1].After.BrandName);
        Assert.False(history[2].Before!.IsArchived);
        Assert.True(history[2].After.IsArchived);
    }

    [Fact]
    public async Task A_failed_hand_over_to_platform_loses_no_brand_history()
    {
        host.Audit.ThrowOnForward = true;
        try
        {
            var id = await CreateAsync(Steward(), Code("FWDFAIL"));
            Assert.Single(await HistoryAsync(id));
        }
        finally
        {
            host.Audit.ThrowOnForward = false;
        }
    }

    [Fact]
    public async Task Brand_writes_are_also_handed_to_the_central_audit_trail()
    {
        var client = Steward();
        var id = await CreateAsync(client, Code("CENTRAL"));
        Assert.Equal(HttpStatusCode.NoContent,
            (await SendAsync(client, HttpMethod.Post, $"/api/mdm/brands/{id}/archive", null)).StatusCode);

        var forwarded = host.Audit.Forwarded.Where(x => x.EntityId == id).ToList();
        Assert.Contains(forwarded, x => x.RequestType == "CreateBrandCommand" && x.EntityType == "Brand");
        Assert.Contains(forwarded, x => x.RequestType == "ArchiveBrandCommand" && x.EntityType == "Brand");
    }

    // ── item 2 — an edit made on an older read is refused and writes nothing ───────────────────────────────────────

    [Fact]
    public async Task Two_edits_from_the_same_read_the_second_is_a_409_and_the_first_value_stays()
    {
        var client = Steward();
        var id = await CreateAsync(client, Code("STALE"));

        var first = await SendAsync(client, HttpMethod.Put, $"/api/mdm/brands/{id}", Body(Code("STALE"), name: "First", expectedVersion: 0));
        var second = await SendAsync(client, HttpMethod.Put, $"/api/mdm/brands/{id}", Body(Code("STALE"), name: "Second", expectedVersion: 0));

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.StartsWith("version_conflict:", Envelope.FirstError(await Envelope.ReadAsync(second)));
        var stored = await GetAsync(client, id);
        Assert.Equal("First", stored.GetProperty("brandName").GetString());
        Assert.Equal(1, stored.GetProperty("version").GetInt32());
        Assert.Equal(2, (await HistoryAsync(id)).Count);
    }

    [Fact]
    public async Task An_edit_that_does_not_say_which_read_it_was_made_on_is_a_400()
    {
        var client = Steward();
        var id = await CreateAsync(client, Code("NOVER"));
        var response = await SendAsync(client, HttpMethod.Put, $"/api/mdm/brands/{id}", Body(Code("NOVER"), name: "X"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.StartsWith("expected_version_required:", Envelope.FirstError(await Envelope.ReadAsync(response)));
    }

    [Fact]
    public async Task The_store_refuses_an_edit_made_on_an_older_version_even_past_the_handlers_check()
    {
        // The handler compares versions first; this is the store's own conditional write, which is what holds when two
        // edits pass that check at the same moment.
        var repository = Repository();
        var brand = NewBrand(Code("CAS"));
        await repository.CreateWithHistoryAsync(brand, Entry(BrandHistoryEntry.EventCreated));
        var first = NewBrand(Code("CAS"));
        first.Id = brand.Id;
        first.BrandName = "First";
        var second = NewBrand(Code("CAS"));
        second.Id = brand.Id;
        second.BrandName = "Second";

        Assert.Equal(BrandWriteOutcome.Written, await repository.UpdateEditableFieldsWithHistoryAsync(first, 0, Entry(BrandHistoryEntry.EventUpdated)));
        Assert.Equal(BrandWriteOutcome.Stale, await repository.UpdateEditableFieldsWithHistoryAsync(second, 0, Entry(BrandHistoryEntry.EventUpdated)));
        Assert.Equal("First", (await repository.GetByIdAsync(brand.Id))!.BrandName);
        Assert.Equal(2, (await repository.GetHistoryAsync(brand.Id)).Count);
    }

    [Fact]
    public async Task An_edit_that_lands_after_an_archive_never_brings_the_brand_back()
    {
        // Straight at the store, the way two requests interleave: the edit was read before the archive landed.
        var repository = Repository();
        var brand = NewBrand(Code("RACE"));
        Assert.Equal(BrandWriteOutcome.Written, await repository.CreateWithHistoryAsync(brand, Entry(BrandHistoryEntry.EventCreated)));
        brand.IsArchived = true;
        brand.ArchivedAt = DateTimeOffset.UtcNow;
        Assert.Equal(BrandWriteOutcome.Written, await repository.ArchiveWithHistoryAsync(brand, 0, Entry(BrandHistoryEntry.EventArchived)));

        var edit = NewBrand(Code("RACE"));
        edit.Id = brand.Id;
        edit.BrandName = "Edited after the archive";
        // Even on the version the archive left, an edit of an archived brand is refused.
        Assert.Equal(BrandWriteOutcome.Stale, await repository.UpdateEditableFieldsWithHistoryAsync(edit, 1, Entry(BrandHistoryEntry.EventUpdated)));

        var stored = await repository.GetByIdAsync(brand.Id);
        Assert.True(stored!.IsArchived);
        Assert.NotEqual("Edited after the archive", stored.BrandName);
    }

    // ── item 9 — concurrent creates of one code: one 201, the rest 409, never a 500 ───────────────────────────────

    [Fact]
    public async Task Concurrent_creates_of_one_brand_code_answer_one_201_and_409s_never_a_500()
    {
        var client = Steward();
        var code = Code("RACECODE");
        var answers = await Task.WhenAll(Enumerable.Range(0, 6)
            .Select(_ => SendAsync(client, HttpMethod.Post, "/api/mdm/brands", Body(code))));

        Assert.Single(answers, x => x.StatusCode == HttpStatusCode.Created);
        Assert.All(answers.Where(x => x.StatusCode != HttpStatusCode.Created), x => Assert.Equal(HttpStatusCode.Conflict, x.StatusCode));
        foreach (var refused in answers.Where(x => x.StatusCode == HttpStatusCode.Conflict))
        {
            Assert.StartsWith("brand_code_duplicate:", Envelope.FirstError(await Envelope.ReadAsync(refused)));
        }
    }

    [Fact]
    public async Task Concurrent_creates_of_one_product_code_answer_one_201_and_409s_never_a_500()
    {
        var client = host.Client(host.Token(_tenant, _user, ProductsCreate, ProductsRead));
        var code = Code("PRACE");
        var answers = await Task.WhenAll(Enumerable.Range(0, 6)
            .Select(_ => SendAsync(client, HttpMethod.Post, "/api/mdm/products", ProductBody(code))));

        Assert.Single(answers, x => x.StatusCode == HttpStatusCode.Created);
        Assert.All(answers.Where(x => x.StatusCode != HttpStatusCode.Created), x => Assert.Equal(HttpStatusCode.Conflict, x.StatusCode));
    }

    [Fact]
    public async Task The_store_refuses_a_second_brand_with_the_same_code_even_past_the_pre_check()
    {
        // The unique index itself, without the handler's ExistsByCode pre-check in front of it.
        var repository = Repository();
        var code = Code("INDEX");
        Assert.Equal(BrandWriteOutcome.Written, await repository.CreateWithHistoryAsync(NewBrand(code), Entry(BrandHistoryEntry.EventCreated)));
        Assert.Equal(BrandWriteOutcome.DuplicateCode, await repository.CreateWithHistoryAsync(NewBrand(code), Entry(BrandHistoryEntry.EventCreated)));
    }

    // ── item 10 — where a reference came from is never re-stamped; a pair another record carries is named ───────

    [Fact]
    public async Task Editing_a_brand_never_rewrites_when_its_external_reference_was_imported()
    {
        var client = Steward();
        var imported = new DateTimeOffset(2024, 3, 1, 8, 0, 0, TimeSpan.Zero);
        var reference = new { sourceSystem = "SAP", externalId = "B-1", externalCode = (string?)null, externalName = (string?)null, importedAt = imported, isPrimary = true };
        var id = await CreateAsync(client, Code("IMPORT"), references: [reference]);

        var withoutImportedAt = new { sourceSystem = "SAP", externalId = "B-1", externalCode = (string?)null, externalName = (string?)null, importedAt = (DateTimeOffset?)null, isPrimary = true };
        for (var version = 0; version < 2; version++)
        {
            var edit = await SendAsync(client, HttpMethod.Put, $"/api/mdm/brands/{id}",
                Body(Code("IMPORT"), name: $"Edit {version}", expectedVersion: version, references: [withoutImportedAt]));
            Assert.Equal(HttpStatusCode.NoContent, edit.StatusCode);
        }

        var stored = (await GetAsync(client, id)).GetProperty("externalReferences")[0].GetProperty("importedAt").GetDateTimeOffset();
        Assert.Equal(imported, stored);
    }

    [Fact]
    public async Task An_external_reference_another_brand_carries_is_a_409_that_names_that_brand()
    {
        var client = Steward();
        var owner = Code("OWNER");
        await CreateAsync(client, owner, references: [new { sourceSystem = "SAP", externalId = "SHARED", isPrimary = true }]);

        var response = await SendAsync(client, HttpMethod.Post, "/api/mdm/brands",
            Body(Code("TAKER"), references: [new { sourceSystem = "sap", externalId = "shared", isPrimary = true }]));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = Envelope.FirstError(await Envelope.ReadAsync(response));
        Assert.StartsWith("external_reference_in_use:", error);
        Assert.Contains(owner, error);
    }

    [Fact]
    public async Task An_external_reference_another_product_carries_is_a_409()
    {
        var client = host.Client(host.Token(_tenant, _user, ProductsCreate, ProductsRead));
        var reference = new { sourceSystem = "SAP", externalId = "P-SHARED", isPrimary = true };
        Assert.Equal(HttpStatusCode.Created,
            (await SendAsync(client, HttpMethod.Post, "/api/mdm/products", ProductBody(Code("PFIRST"), [reference]))).StatusCode);

        var response = await SendAsync(client, HttpMethod.Post, "/api/mdm/products", ProductBody(Code("PSECOND"), [reference]));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.StartsWith("external_reference_in_use:", Envelope.FirstError(await Envelope.ReadAsync(response)));
    }

    // ── item 11 — the contract reads with either module's read key ─────────────────────────────────────────────

    [Theory]
    [InlineData(Read, HttpStatusCode.OK)]
    [InlineData(ProductsRead, HttpStatusCode.OK)]
    [InlineData(Create, HttpStatusCode.Forbidden)]
    public async Task The_brand_product_contract_is_read_with_either_read_key(string permission, HttpStatusCode expected)
    {
        var response = await host.Client(host.Token(_tenant, _user, permission)).GetAsync("/api/mdm/brand-products/contract");
        Assert.Equal(expected, response.StatusCode);
    }

    // ── item 12 — each verb needs exactly its own key ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Each_brand_verb_needs_exactly_its_own_permission()
    {
        var id = await CreateAsync(Steward(), Code("VERB"));
        var everyOtherKey = new[] { Read, Create, Update };

        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(host.Client(host.Token(_tenant, _user, everyOtherKey)),
            HttpMethod.Post, $"/api/mdm/brands/{id}/archive", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(host.Client(host.Token(_tenant, _user, Read, Archive)),
            HttpMethod.Put, $"/api/mdm/brands/{id}", Body(Code("VERB"), expectedVersion: 0))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(host.Client(host.Token(_tenant, _user, Read, Update, Archive)),
            HttpMethod.Post, "/api/mdm/brands", Body(Code("VERB2")))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client(host.Token(_tenant, _user, Create, Update, Archive))
            .GetAsync($"/api/mdm/brands/{id}")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(host.Client(host.Token(_tenant, _user, Archive)),
            HttpMethod.Post, $"/api/mdm/brands/{id}/archive", null)).StatusCode);
    }

    // ── item 12 — tenant isolation, on the real store ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Another_tenant_reads_edits_and_archives_none_of_this_tenants_brands()
    {
        var id = await CreateAsync(Steward(), Code("ISO"));
        var stranger = host.Client(host.Token(Guid.NewGuid(), Guid.NewGuid(), Read, Update, Archive));

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/mdm/brands/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(stranger, HttpMethod.Put, $"/api/mdm/brands/{id}",
            Body(Code("ISO"), name: "Hijacked", expectedVersion: 0))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(stranger, HttpMethod.Post, $"/api/mdm/brands/{id}/archive", null)).StatusCode);
        Assert.Equal(0, (await Envelope.ReadAsync(await stranger.GetAsync("/api/mdm/brands"))).GetProperty("data").GetProperty("totalCount").GetInt32());

        var stored = await GetAsync(Steward(), id);
        Assert.Equal("Brand ISO", stored.GetProperty("brandName").GetString());
        Assert.False(stored.GetProperty("isArchived").GetBoolean());
    }

    [Fact]
    public async Task Another_tenant_may_use_the_same_brand_code()
    {
        var code = Code("TWICE");
        await CreateAsync(Steward(), code);
        var other = host.Client(host.Token(Guid.NewGuid(), _user, Read, Create));
        Assert.Equal(HttpStatusCode.Created, (await SendAsync(other, HttpMethod.Post, "/api/mdm/brands", Body(code))).StatusCode);
    }

    [Fact]
    public async Task The_store_never_writes_a_brand_of_another_tenant()
    {
        // RepositoryBase's tenant filter, measured where the handler's own read cannot hide it.
        var mine = Repository();
        var brand = NewBrand(Code("FOREIGN"));
        await mine.CreateWithHistoryAsync(brand, Entry(BrandHistoryEntry.EventCreated));

        var stranger = new BrandRepository(host.Database, new FixedTenant(Guid.NewGuid()));
        Assert.Null(await stranger.GetByIdAsync(brand.Id));
        var edit = NewBrand(Code("FOREIGN"));
        edit.Id = brand.Id;
        edit.BrandName = "Hijacked";
        Assert.Equal(BrandWriteOutcome.Stale, await stranger.UpdateEditableFieldsWithHistoryAsync(edit, 0, Entry(BrandHistoryEntry.EventUpdated)));
        Assert.False(await stranger.UpdateAsync(edit));

        var stored = await mine.GetByIdAsync(brand.Id);
        Assert.Equal(brand.BrandName, stored!.BrandName);
        Assert.Equal(_tenant, stored.TenantId);
    }

    // ── item 12 — the class maps and the indexes the store depends on ─────────────────────────────────────────

    [Fact]
    public async Task Brand_ids_are_stored_as_standard_uuids_and_the_history_beside_them()
    {
        var businessUnit = Guid.NewGuid();
        var id = await CreateAsync(Steward(), Code("BSON"), businessUnitId: businessUnit);

        var raw = await host.Database.GetCollection<BsonDocument>("mdm_brands")
            .Find(new BsonDocument("_id", new BsonBinaryData(id, GuidRepresentation.Standard))).FirstOrDefaultAsync();
        Assert.NotNull(raw);
        Assert.Equal(BsonBinarySubType.UuidStandard, raw["BusinessUnitId"].AsBsonBinaryData.SubType);
        Assert.Equal(BsonBinarySubType.UuidStandard, raw["TenantId"].AsBsonBinaryData.SubType);

        var history = await host.Database.GetCollection<BsonDocument>(BrandRepository.HistoryCollectionName)
            .Find(new BsonDocument("BrandId", new BsonBinaryData(id, GuidRepresentation.Standard))).ToListAsync();
        Assert.Single(history);
    }

    [Fact]
    public async Task A_brand_stored_with_a_field_this_version_does_not_know_still_reads()
    {
        // The class maps' forward-compatibility guard (SetIgnoreExtraElements): a document written by a later version
        // must still deserialise, not fail every read of the register.
        var client = Steward();
        var id = await CreateAsync(client, Code("FUTURE"));
        await host.Database.GetCollection<BsonDocument>("mdm_brands").UpdateOneAsync(
            new BsonDocument("_id", new BsonBinaryData(id, GuidRepresentation.Standard)),
            new BsonDocument("$set", new BsonDocument("FieldFromALaterVersion", "x")));

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/mdm/brands/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/mdm/brands")).StatusCode);
    }

    [Fact]
    public async Task The_brand_code_and_history_indexes_exist()
    {
        await CreateAsync(Steward(), Code("IDX"));
        var brandIndexes = await (await host.Database.GetCollection<BsonDocument>("mdm_brands").Indexes.ListAsync()).ToListAsync();
        Assert.Contains(brandIndexes, x => x["name"] == "ux_mdm_brands_tenant_code" && x.GetValue("unique", false).ToBoolean());
        var historyIndexes = await (await host.Database.GetCollection<BsonDocument>(BrandRepository.HistoryCollectionName).Indexes.ListAsync()).ToListAsync();
        Assert.Contains(historyIndexes, x => x["name"] == "ux_mdm_brand_history_tenant_brand_version" && x.GetValue("unique", false).ToBoolean());
    }

    // ── item 12 — the update and archive rules, through the real pipeline ─────────────────────────────────────

    [Theory]
    [InlineData("archived", null, "archived_status_not_assignable", HttpStatusCode.BadRequest)]
    [InlineData("retired", null, "invalid_brand_status", HttpStatusCode.BadRequest)]
    [InlineData("active", "2020-01-01", "invalid_effective_window", HttpStatusCode.BadRequest)]
    public async Task An_edit_breaking_a_brand_rule_is_refused_with_its_code(
        string status, string? effectiveTo, string reasonCode, HttpStatusCode expected)
    {
        var client = Steward();
        var id = await CreateAsync(client, Code("RULE"));
        var response = await SendAsync(client, HttpMethod.Put, $"/api/mdm/brands/{id}",
            Body(Code("RULE"), status: status, expectedVersion: 0,
                effectiveTo: effectiveTo is null ? null : DateTimeOffset.Parse(effectiveTo, System.Globalization.CultureInfo.InvariantCulture)));
        Assert.Equal(expected, response.StatusCode);
        Assert.StartsWith($"{reasonCode}:", Envelope.FirstError(await Envelope.ReadAsync(response)));
    }

    [Fact]
    public async Task An_edit_with_one_external_reference_twice_is_a_409()
    {
        var client = Steward();
        var id = await CreateAsync(client, Code("DUPREF"));
        var twice = new object[] { new { sourceSystem = "SAP", externalId = "X", isPrimary = false }, new { sourceSystem = "sap", externalId = "x", isPrimary = false } };
        var response = await SendAsync(client, HttpMethod.Put, $"/api/mdm/brands/{id}", Body(Code("DUPREF"), expectedVersion: 0, references: twice));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.StartsWith("external_reference_duplicate:", Envelope.FirstError(await Envelope.ReadAsync(response)));
    }

    [Fact]
    public async Task Writing_a_brand_that_does_not_exist_is_a_404()
    {
        var client = Steward();
        var missing = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(client, HttpMethod.Put, $"/api/mdm/brands/{missing}",
            Body(Code("GONE"), expectedVersion: 0))).StatusCode);
        var archive = await SendAsync(client, HttpMethod.Post, $"/api/mdm/brands/{missing}/archive", null);
        Assert.Equal(HttpStatusCode.NotFound, archive.StatusCode);
        Assert.StartsWith("brand_not_found:", Envelope.FirstError(await Envelope.ReadAsync(archive)));
    }

    [Fact]
    public async Task A_status_is_stored_lower_case_and_an_empty_reference_is_no_reference()
    {
        var client = Steward();
        var id = await CreateAsync(client, Code("NORM"), status: "ACTIVE", businessUnitId: Guid.Empty);
        var stored = await GetAsync(client, id);
        Assert.Equal("active", stored.GetProperty("brandStatus").GetString());
        Assert.Equal(JsonValueKind.Null, stored.GetProperty("businessUnitId").ValueKind);
    }

    // ── item 6 — the register's server-side page; a read without paging is unchanged ─────────────────────────────

    [Fact]
    public async Task A_read_without_length_returns_every_brand_even_with_the_crm_pickers_page_size()
    {
        var client = Steward();
        foreach (var name in new[] { "C", "A", "B" }) await CreateAsync(client, Code($"ALL{name}"), name: $"Name {name}");

        var all = (await Envelope.ReadAsync(await client.GetAsync("/api/mdm/brands?pageSize=1"))).GetProperty("data");
        Assert.Equal(3, all.GetProperty("items").GetArrayLength());
        Assert.Equal(3, all.GetProperty("totalCount").GetInt32());
        Assert.Equal(["Name A", "Name B", "Name C"], all.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("brandName").GetString()));
        Assert.Equal(JsonValueKind.Null, all.GetProperty("archivedWithLiveProducts").ValueKind);
    }

    [Fact]
    public async Task A_paged_read_is_sorted_and_cut_on_the_server()
    {
        var client = Steward();
        foreach (var name in new[] { "C", "A", "B", "D" }) await CreateAsync(client, Code($"PG{name}"), name: $"Page {name}");

        var page = (await Envelope.ReadAsync(await client.GetAsync("/api/mdm/brands?start=1&length=2&orderBy=brandName&orderDir=desc"))).GetProperty("data");
        Assert.Equal(["Page C", "Page B"], page.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("brandName").GetString()));
        Assert.Equal(4, page.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, page.GetProperty("start").GetInt32());
        Assert.Equal(2, page.GetProperty("length").GetInt32());
    }

    [Theory]
    [InlineData("length=0")]
    [InlineData("length=101")]
    [InlineData("length=10&start=-1")]
    [InlineData("length=10&orderBy=tenantId")]
    [InlineData("length=10&orderBy=brandName&orderDir=sideways")]
    public async Task A_paged_read_the_server_cannot_honour_is_a_400(string query)
    {
        var response = await Steward().GetAsync($"/api/mdm/brands?{query}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.StartsWith("list_query_invalid:", Envelope.FirstError(await Envelope.ReadAsync(response)));
    }

    [Fact]
    public async Task The_list_filters_by_business_unit_and_therapeutic_area_on_the_server()
    {
        var client = Steward();
        var unit = Guid.NewGuid();
        var area = Guid.NewGuid();
        await CreateAsync(client, Code("BU1"), businessUnitId: unit);
        await CreateAsync(client, Code("TA1"), therapeuticAreaId: area);
        await CreateAsync(client, Code("NONE"));

        var byUnit = (await Envelope.ReadAsync(await client.GetAsync($"/api/mdm/brands?businessUnitId={unit}"))).GetProperty("data");
        Assert.Equal([Code("BU1")], byUnit.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("brandCode").GetString()));
        var byArea = (await Envelope.ReadAsync(await client.GetAsync($"/api/mdm/brands?therapeuticAreaId={area}"))).GetProperty("data");
        Assert.Equal([Code("TA1")], byArea.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("brandCode").GetString()));
    }

    // ── item 3 — what an archive leaves in use is counted ─────────────────────────────────────────────────────

    [Fact]
    public async Task The_detail_counts_live_products_and_the_register_counts_archived_brands_still_in_use()
    {
        var client = host.Client(host.Token(_tenant, _user, Read, Create, Update, Archive, ProductsCreate, ProductsRead, ProductsArchive));
        var inUse = await CreateAsync(client, Code("INUSE"));
        var unused = await CreateAsync(client, Code("UNUSED"));
        // An archived brand whose every product is archived too is NOT "still in use".
        var allArchived = await CreateAsync(client, Code("ALLGONE"));
        var onlyProduct = await Envelope.ReadAsync(await SendAsync(client, HttpMethod.Post, "/api/mdm/products", ProductBody(Code("P3"), brandId: allArchived)));
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Post,
            $"/api/mdm/products/{onlyProduct.GetProperty("data").GetGuid()}/archive", null)).StatusCode);
        await SendAsync(client, HttpMethod.Post, "/api/mdm/products", ProductBody(Code("P1"), brandId: inUse));
        var archivedProduct = await Envelope.ReadAsync(await SendAsync(client, HttpMethod.Post, "/api/mdm/products", ProductBody(Code("P2"), brandId: inUse)));
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Post,
            $"/api/mdm/products/{archivedProduct.GetProperty("data").GetGuid()}/archive", null)).StatusCode);

        Assert.Equal(1, (await GetAsync(client, inUse)).GetProperty("liveProductCount").GetInt32());

        await SendAsync(client, HttpMethod.Post, $"/api/mdm/brands/{inUse}/archive", null);
        await SendAsync(client, HttpMethod.Post, $"/api/mdm/brands/{unused}/archive", null);
        await SendAsync(client, HttpMethod.Post, $"/api/mdm/brands/{allArchived}/archive", null);
        var page = (await Envelope.ReadAsync(await client.GetAsync("/api/mdm/brands?length=10&includeArchived=true"))).GetProperty("data");
        Assert.Equal(1, page.GetProperty("archivedWithLiveProducts").GetInt32());
    }

    [Fact]
    public async Task The_brand_products_read_leaves_archived_products_out_unless_asked()
    {
        var client = host.Client(host.Token(_tenant, _user, Read, Create, ProductsCreate, ProductsRead, ProductsArchive));
        var brand = await CreateAsync(client, Code("PRODS"));
        await SendAsync(client, HttpMethod.Post, "/api/mdm/products", ProductBody(Code("LIVE"), brandId: brand));
        var gone = await Envelope.ReadAsync(await SendAsync(client, HttpMethod.Post, "/api/mdm/products", ProductBody(Code("GONEP"), brandId: brand)));
        await SendAsync(client, HttpMethod.Post, $"/api/mdm/products/{gone.GetProperty("data").GetGuid()}/archive", null);

        var live = (await Envelope.ReadAsync(await client.GetAsync($"/api/mdm/brands/{brand}/products"))).GetProperty("data");
        var all = (await Envelope.ReadAsync(await client.GetAsync($"/api/mdm/brands/{brand}/products?includeArchived=true"))).GetProperty("data");
        Assert.Equal(1, live.GetArrayLength());
        Assert.Equal(2, all.GetArrayLength());
    }

    // ── helpers ────────────────────────────────────────────────────────────────────────────────────────────────

    private string Code(string stem) => $"{stem}-{_tenant.ToString("N")[..6]}".ToUpperInvariant();

    private HttpClient Steward() => host.Client(host.Token(_tenant, _user, Read, Create, Update, Archive));

    private static object Body(
        string code, string? name = null, string status = "active", int? expectedVersion = null, object[]? references = null,
        Guid? businessUnitId = null, Guid? therapeuticAreaId = null, DateTimeOffset? effectiveTo = null) => new
    {
        brandCode = code,
        brandName = name ?? $"Brand {code.Split('-')[0]}",
        brandStatus = status,
        description = (string?)null,
        ownerCompanyId = (Guid?)null,
        businessUnitId,
        therapeuticAreaId,
        effectiveFrom = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
        effectiveTo,
        externalReferences = references,
        expectedVersion
    };

    private static object ProductBody(string code, object[]? references = null, Guid? brandId = null) => new
    {
        productCode = code,
        productName = $"Product {code}",
        productStatus = "active",
        brandId,
        effectiveFrom = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
        externalReferences = references
    };

    private async Task<Guid> CreateAsync(
        HttpClient client, string code, string? name = null, string status = "active", object[]? references = null,
        Guid? businessUnitId = null, Guid? therapeuticAreaId = null, string? correlationId = null)
    {
        var response = await SendAsync(client, HttpMethod.Post, "/api/mdm/brands",
            Body(code, name, status, references: references, businessUnitId: businessUnitId, therapeuticAreaId: therapeuticAreaId), correlationId);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await Envelope.ReadAsync(response)).GetProperty("data").GetGuid();
    }

    private static async Task<JsonElement> GetAsync(HttpClient client, Guid id)
    {
        var response = await client.GetAsync($"/api/mdm/brands/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await Envelope.ReadAsync(response)).GetProperty("data");
    }

    private static Task<HttpResponseMessage> SendAsync(
        HttpClient client, HttpMethod method, string path, object? body, string? correlationId = null)
    {
        var request = new HttpRequestMessage(method, path) { Content = body is null ? null : Envelope.Json(body) };
        if (correlationId is not null) request.Headers.Add("X-Correlation-Id", correlationId);
        return client.SendAsync(request);
    }

    private BrandRepository Repository() => new(host.Database, new FixedTenant(_tenant));

    private Task<IReadOnlyList<BrandHistoryEntry>> HistoryAsync(Guid brandId) => Repository().GetHistoryAsync(brandId);

    private static Brand NewBrand(string code) => new()
    {
        BrandCode = code,
        BrandName = $"Brand {code}",
        BrandStatus = "active",
        EffectiveFrom = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero)
    };

    private static BrandHistoryEntry Entry(string eventType) => new()
    {
        EventType = eventType,
        CorrelationId = "store-test",
        OccurredAtUtc = DateTimeOffset.UtcNow
    };

    private sealed class FixedTenant(Guid tenantId) : ITenantContext
    {
        public Guid TenantId { get; private set; } = tenantId;
        public bool IsResolved => true;
        public void SetTenant(Guid tenantId) => TenantId = tenantId;
    }
}
