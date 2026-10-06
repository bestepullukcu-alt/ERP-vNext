using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.Brand;
using Diten.MdmService.Application.Features.Brand.Commands;
using Diten.MdmService.Application.Features.Brand.Handlers.CommandHandlers;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Persistence.Configurations;
using Diten.MdmService.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// WP-MDM-BRAND-ACCEPT-01 FIX2 — the second acceptance round of the Brand (and, where shared, Product) master, measured on
/// the production stack (MdmHttpHost: real controllers, permission policy, pipeline, handlers, repositories, class maps,
/// indexes and transactions over a throwaway replica set). Each test makes its own tenant.
/// </summary>
[Collection(MdmHttpCollection.Name)]
public sealed class BrandFix2HttpTests(MdmHttpHost host)
{
    private const string Read = "mdm.brands.read";
    private const string Create = "mdm.brands.create";
    private const string Update = "mdm.brands.update";
    private const string Archive = "mdm.brands.archive";
    private const string Export = "mdm.brands.export";
    private const string ProductsRead = "mdm.products.read";
    private const string ProductsCreate = "mdm.products.create";
    private const string ProductsUpdate = "mdm.products.update";

    private static readonly DateTimeOffset From2025 = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _user = Guid.NewGuid();

    // ── item 1 — the server export (BL-452) behind its own key ───────────────────────────────────────────────────

    [Fact]
    public async Task The_brand_export_is_every_matching_row_in_the_readers_language_behind_its_own_key()
    {
        var client = Steward(Export);
        await CreateAsync(client, Code("EXA"), name: "Alpha");
        await CreateAsync(client, Code("EXB"), name: "Beta", status: "draft");
        var archived = await CreateAsync(client, Code("EXC"), name: "Gamma");
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Post, $"/api/mdm/brands/{archived}/archive", null)).StatusCode);

        var request = new HttpRequestMessage(HttpMethod.Get,
            "/api/mdm/brands/export?format=csv&columns=brandCode,brandName,brandStatus,isArchived&orderBy=brandName&orderDir=desc");
        request.Headers.Add("Accept-Language", "tr");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("text/csv", response.Content.Headers.ContentType?.ToString());
        var lines = Encoding.UTF8.GetString(await response.Content.ReadAsByteArrayAsync()).TrimStart('﻿')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(x => x.TrimEnd('\r')).ToList();
        Assert.Equal("Marka Kodu,Marka Adı,Marka Durumu,Arşivlendi", lines[0]);
        // Archived brands are left out unless asked — exactly as the list; ordered as the reader ordered.
        Assert.Equal(3, lines.Count);
        Assert.Contains("Beta", lines[1]);
        Assert.Contains("Taslak", lines[1]);
        Assert.Contains("Alpha", lines[2]);
        Assert.Contains("Aktif", lines[2]);
        Assert.Contains("Hayır", lines[2]);
    }

    [Theory]
    [InlineData("format=pdf", "EXPORT_FORMAT_INVALID")]
    [InlineData("format=csv&columns=brandCode,secretColumn", "EXPORT_COLUMNS_INVALID")]
    public async Task A_brand_export_the_server_cannot_honour_is_a_400_with_its_code(string query, string code)
    {
        var response = await Steward(Export).GetAsync($"/api/mdm/brands/export?{query}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(code, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Reading_brands_is_not_enough_to_export_them()
    {
        var reader = host.Client(host.Token(_tenant, _user, Read, Create, Update, Archive));
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.GetAsync("/api/mdm/brands/export?format=csv")).StatusCode);
    }

    // ── item 2 — a NEW link only to an active brand inside its window; existing links are untouched ───────────────

    [Theory]
    [InlineData("draft", 0, null, "brand_not_linkable")]
    [InlineData("inactive", 0, null, "brand_not_linkable")]
    [InlineData("active", 0, -1, "brand_not_linkable")]
    [InlineData("active", 2, null, "brand_not_linkable")]
    public async Task A_new_product_link_to_a_brand_that_is_not_linkable_is_a_409_with_its_code(
        string status, int fromDaysAhead, int? toDaysAhead, string expectedCode)
    {
        var today = DateTimeOffset.UtcNow.Date;
        var from = fromDaysAhead == 0 ? From2025 : new DateTimeOffset(today.AddDays(fromDaysAhead), TimeSpan.Zero);
        DateTimeOffset? to = toDaysAhead is { } days ? new DateTimeOffset(today.AddDays(days), TimeSpan.Zero) : null;
        var brand = await CreateAsync(Steward(), Code("NOLINK"), status: status, effectiveFrom: from, effectiveTo: to);

        var response = await SendAsync(ProductSteward(), HttpMethod.Post, "/api/mdm/products", ProductBody(Code("PNOLINK"), brandId: brand));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.StartsWith($"{expectedCode}:", Envelope.FirstError(await Envelope.ReadAsync(response)));
    }

    [Fact]
    public async Task A_new_product_link_to_an_archived_brand_keeps_its_own_code_and_an_active_brand_inside_its_window_links()
    {
        var steward = Steward();
        var active = await CreateAsync(steward, Code("LINKOK"), effectiveTo: new DateTimeOffset(DateTimeOffset.UtcNow.Date, TimeSpan.Zero));
        Assert.Equal(HttpStatusCode.Created,
            (await SendAsync(ProductSteward(), HttpMethod.Post, "/api/mdm/products", ProductBody(Code("PLINKOK"), brandId: active))).StatusCode);

        var archived = await CreateAsync(steward, Code("LINKARC"));
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(steward, HttpMethod.Post, $"/api/mdm/brands/{archived}/archive", null)).StatusCode);
        var refused = await SendAsync(ProductSteward(), HttpMethod.Post, "/api/mdm/products", ProductBody(Code("PLINKARC"), brandId: archived));
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.StartsWith("brand_archived:", Envelope.FirstError(await Envelope.ReadAsync(refused)));
    }

    [Fact]
    public async Task A_product_already_linked_to_a_brand_that_stopped_being_linkable_still_saves_its_own_edits()
    {
        var steward = Steward();
        var brand = await CreateAsync(steward, Code("WASLINK"));
        var product = await CreateProductAsync(ProductSteward(), Code("PWASLINK"), brandId: brand);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(steward, HttpMethod.Put, $"/api/mdm/brands/{brand}",
            BrandBody(Code("WASLINK"), status: "inactive", expectedVersion: 0))).StatusCode);

        var edit = await SendAsync(ProductSteward(), HttpMethod.Put, $"/api/mdm/products/{product}",
            ProductBody(Code("PWASLINK"), brandId: brand, description: "Edited after the brand went inactive"));
        Assert.Equal(HttpStatusCode.NoContent, edit.StatusCode);

        var newLink = await SendAsync(ProductSteward(), HttpMethod.Post, "/api/mdm/products", ProductBody(Code("PNEWLINK"), brandId: brand));
        Assert.Equal(HttpStatusCode.Conflict, newLink.StatusCode);
        Assert.StartsWith("brand_not_linkable:", Envelope.FirstError(await Envelope.ReadAsync(newLink)));
    }

    // ── item 5 (R4) — an edit is checked only for the references it ADDS ─────────────────────────────────────────

    [Fact]
    public async Task A_description_edit_on_a_brand_that_legacy_data_left_sharing_a_reference_saves()
    {
        var shared = new BrandProductExternalReference { SourceSystem = "SAP", ExternalId = "X", IsPrimary = true, ImportedAt = From2025 };
        var first = await InsertRawBrandAsync(Code("RAWA"), shared);
        await InsertRawBrandAsync(Code("RAWB"), shared);

        var edit = await SendAsync(Steward(), HttpMethod.Put, $"/api/mdm/brands/{first}",
            BrandBody(Code("RAWA"), description: "Only the description changes", expectedVersion: 0,
                references: [new { sourceSystem = "SAP", externalId = "X", isPrimary = true }]));

        Assert.Equal(HttpStatusCode.NoContent, edit.StatusCode);
    }

    [Fact]
    public async Task An_edit_that_adds_a_reference_another_brand_carries_is_still_a_409()
    {
        var steward = Steward();
        await CreateAsync(steward, Code("HOLDER"), references: [new { sourceSystem = "SAP", externalId = "HELD", isPrimary = true }]);
        var adder = await CreateAsync(steward, Code("ADDER"));

        var edit = await SendAsync(steward, HttpMethod.Put, $"/api/mdm/brands/{adder}",
            BrandBody(Code("ADDER"), expectedVersion: 0, references: [new { sourceSystem = "SAP", externalId = "HELD", isPrimary = true }]));

        Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);
        Assert.StartsWith("external_reference_in_use:", Envelope.FirstError(await Envelope.ReadAsync(edit)));
    }

    [Fact]
    public async Task A_description_edit_on_a_product_that_legacy_data_left_sharing_a_reference_saves()
    {
        var shared = new BrandProductExternalReference { SourceSystem = "SAP", ExternalId = "PX", IsPrimary = true, ImportedAt = From2025 };
        var first = await InsertRawProductAsync(Code("PRAWA"), shared);
        await InsertRawProductAsync(Code("PRAWB"), shared);

        var edit = await SendAsync(ProductSteward(), HttpMethod.Put, $"/api/mdm/products/{first}",
            ProductBody(Code("PRAWA"), description: "Only the description changes",
                references: [new { sourceSystem = "SAP", externalId = "PX", isPrimary = true }]));

        Assert.Equal(HttpStatusCode.NoContent, edit.StatusCode);
    }

    // ── item 6 (R5) — the reference is reserved in the write's transaction ──────────────────────────────────────

    [Fact]
    public async Task Six_concurrent_brand_creates_with_different_codes_and_one_reference_write_exactly_one()
    {
        var client = Steward();
        var reference = new { sourceSystem = "SAP", externalId = "RACE-REF", isPrimary = true };
        var answers = await Task.WhenAll(Enumerable.Range(0, 6)
            .Select(i => SendAsync(client, HttpMethod.Post, "/api/mdm/brands", BrandBody(Code($"RREF{i}"), references: [reference]))));

        Assert.Single(answers, x => x.StatusCode == HttpStatusCode.Created);
        Assert.Equal(5, answers.Count(x => x.StatusCode == HttpStatusCode.Conflict));
        foreach (var refused in answers.Where(x => x.StatusCode == HttpStatusCode.Conflict))
        {
            Assert.StartsWith("external_reference_in_use:", Envelope.FirstError(await Envelope.ReadAsync(refused)));
        }

        Assert.Single(await RawBrandsCarryingAsync("SAP", "RACE-REF"));
    }

    [Fact]
    public async Task Six_concurrent_product_creates_with_different_codes_and_one_reference_write_exactly_one()
    {
        var client = ProductSteward();
        var reference = new { sourceSystem = "SAP", externalId = "PRACE-REF", isPrimary = true };
        var answers = await Task.WhenAll(Enumerable.Range(0, 6)
            .Select(i => SendAsync(client, HttpMethod.Post, "/api/mdm/products", ProductBody(Code($"PRREF{i}"), references: [reference]))));

        Assert.Single(answers, x => x.StatusCode == HttpStatusCode.Created);
        Assert.Equal(5, answers.Count(x => x.StatusCode == HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task The_reservation_index_is_unique_and_legacy_shared_references_hold_no_reservation()
    {
        var shared = new BrandProductExternalReference { SourceSystem = "SAP", ExternalId = "LEGACY", IsPrimary = true, ImportedAt = From2025 };
        await InsertRawBrandAsync(Code("LEGA"), shared);
        await InsertRawBrandAsync(Code("LEGB"), shared);
        await CreateAsync(Steward(), Code("LEGNEW"), references: [new { sourceSystem = "SAP", externalId = "FRESH", isPrimary = true }]);

        var reservations = host.Database.GetCollection<BsonDocument>(ExternalReferenceReservations.CollectionName);
        var indexes = await (await reservations.Indexes.ListAsync()).ToListAsync();
        Assert.Contains(indexes, x => x["name"] == ExternalReferenceReservations.UniqueIndexName && x.GetValue("unique", false).ToBoolean());
        var mine = await reservations.Find(new BsonDocument("TenantId", new BsonBinaryData(_tenant, GuidRepresentation.Standard))).ToListAsync();
        Assert.Equal(["FRESH"], mine.Select(x => x["ExternalIdKey"].AsString));
    }

    // ── item 7 (R2) — no transaction support: refused outside Development, ordered writes in Development ─────────

    [Fact]
    public async Task On_a_standalone_mongod_a_brand_write_is_refused_and_nothing_is_written()
    {
        var repository = new BrandRepository(host.StandaloneDatabase, new FixedTenant(_tenant));
        var brand = NewBrand(Code("STANDALONE"));

        Assert.Equal(BrandWriteOutcome.Unavailable, await repository.CreateWithHistoryAsync(brand, Entry(BrandHistoryEntry.EventCreated)));
        Assert.Null(await repository.GetByIdAsync(brand.Id));
        Assert.Empty(await repository.GetHistoryAsync(brand.Id));

        var products = new ProductRepository(host.StandaloneDatabase, new FixedTenant(_tenant));
        var product = new Product { ProductCode = Code("PSTANDALONE"), ProductName = "P", ProductStatus = "active", EffectiveFrom = From2025 };
        Assert.Equal(BrandWriteOutcome.Unavailable, await products.CreateWithReservationsAsync(product));
        Assert.Null(await products.GetByIdAsync(product.Id));
    }

    [Fact]
    public async Task On_a_standalone_mongod_the_brand_handler_answers_503_store_unavailable()
    {
        var handler = new CreateBrandHandler(new BrandRepository(host.StandaloneDatabase, new FixedTenant(_tenant)));
        var answer = await handler.Handle(new CreateBrandCommand(WriteRequest(Code("STAND503")), _user.ToString("D")), CancellationToken.None);

        Assert.Equal(503, answer.StatusCode);
        Assert.StartsWith("store_unavailable:", Assert.Single(answer.Errors!));
    }

    [Fact]
    public async Task In_development_a_standalone_mongod_keeps_writing_the_brand_and_then_its_history()
    {
        var repository = new BrandRepository(host.StandaloneDatabase, new FixedTenant(_tenant),
            new BrandProductStoreOptions { AllowNonTransactionalWrites = true });
        var brand = NewBrand(Code("DEVSTAND"));

        Assert.Equal(BrandWriteOutcome.Written, await repository.CreateWithHistoryAsync(brand, Entry(BrandHistoryEntry.EventCreated)));
        Assert.NotNull(await repository.GetByIdAsync(brand.Id));
        Assert.Single(await repository.GetHistoryAsync(brand.Id));
    }

    // ── item 8 (R1) — the record and its history are ONE transaction ────────────────────────────────────────────

    [Fact]
    public async Task A_history_row_that_cannot_be_written_leaves_the_edited_brand_exactly_as_it_was()
    {
        var repository = Repository();
        var brand = NewBrand(Code("TXUPD"));
        Assert.Equal(BrandWriteOutcome.Written, await repository.CreateWithHistoryAsync(brand, Entry(BrandHistoryEntry.EventCreated)));
        await SeedHistoryRowAsync(brand.Id, resultVersion: 1);

        var edit = NewBrand(Code("TXUPD"));
        edit.Id = brand.Id;
        edit.BrandName = "Must not land";
        await Assert.ThrowsAnyAsync<MongoException>(() =>
            repository.UpdateEditableFieldsWithHistoryAsync(edit, 0, Entry(BrandHistoryEntry.EventUpdated)));

        var stored = await repository.GetByIdAsync(brand.Id);
        Assert.Equal(brand.BrandName, stored!.BrandName);
        Assert.Equal(0, stored.Version);
    }

    [Fact]
    public async Task A_history_row_that_cannot_be_written_leaves_the_archived_brand_unarchived()
    {
        var repository = Repository();
        var brand = NewBrand(Code("TXARC"));
        Assert.Equal(BrandWriteOutcome.Written, await repository.CreateWithHistoryAsync(brand, Entry(BrandHistoryEntry.EventCreated)));
        await SeedHistoryRowAsync(brand.Id, resultVersion: 1);

        var archived = NewBrand(Code("TXARC"));
        archived.Id = brand.Id;
        archived.BrandStatus = "archived";
        archived.ArchivedAt = DateTimeOffset.UtcNow;
        await Assert.ThrowsAnyAsync<MongoException>(() =>
            repository.ArchiveWithHistoryAsync(archived, 0, Entry(BrandHistoryEntry.EventArchived)));

        var stored = await repository.GetByIdAsync(brand.Id);
        Assert.False(stored!.IsArchived);
        Assert.Equal(brand.BrandName, stored.BrandName);
        Assert.Equal(0, stored.Version);
    }

    [Fact]
    public async Task A_history_row_that_cannot_be_written_leaves_no_created_brand_and_no_reservation()
    {
        var repository = Repository();
        var brand = NewBrand(Code("TXCRE"));
        brand.ExternalReferences = [new BrandProductExternalReference { SourceSystem = "SAP", ExternalId = "TXCRE", IsPrimary = true }];
        await SeedHistoryRowAsync(brand.Id, resultVersion: 0);

        await Assert.ThrowsAnyAsync<MongoException>(() =>
            repository.CreateWithHistoryAsync(brand, Entry(BrandHistoryEntry.EventCreated)));

        Assert.Null(await repository.GetByIdAsync(brand.Id));
        var reservations = host.Database.GetCollection<ExternalReferenceReservation>(ExternalReferenceReservations.CollectionName);
        Assert.Equal(0, await reservations.CountDocumentsAsync(x => x.TenantId == _tenant && x.RecordId == brand.Id));
    }

    // ── item 9 (R3) — reference uniqueness stops at the tenant boundary (CT BR1 / BR2) ─────────────────────────

    [Fact]
    public async Task Another_tenants_reference_never_blocks_this_tenants_brand_or_product_and_is_never_named()
    {
        // Tenant A's code sorts FIRST, so a lookup that forgot the tenant would name it.
        var tenantA = Guid.NewGuid();
        var codeA = $"AAA-{tenantA.ToString("N")[..6]}".ToUpperInvariant();
        var a = host.Client(host.Token(tenantA, _user, Read, Create, ProductsCreate, ProductsRead));
        var reference = new { sourceSystem = "SAP", externalId = "CROSS-X", isPrimary = true };
        Assert.Equal(HttpStatusCode.Created, (await SendAsync(a, HttpMethod.Post, "/api/mdm/brands", BrandBody(codeA, references: [reference]))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await SendAsync(a, HttpMethod.Post, "/api/mdm/products", ProductBody(codeA, references: [reference]))).StatusCode);

        var b = host.Client(host.Token(_tenant, _user, Read, Create, ProductsCreate, ProductsRead));
        var firstB = $"ZZZ1-{_tenant.ToString("N")[..6]}".ToUpperInvariant();
        Assert.Equal(HttpStatusCode.Created, (await SendAsync(b, HttpMethod.Post, "/api/mdm/brands", BrandBody(firstB, references: [reference]))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await SendAsync(b, HttpMethod.Post, "/api/mdm/products", ProductBody(firstB, references: [reference]))).StatusCode);

        var secondBrand = await SendAsync(b, HttpMethod.Post, "/api/mdm/brands", BrandBody($"ZZZ2-{_tenant.ToString("N")[..6]}".ToUpperInvariant(), references: [reference]));
        Assert.Equal(HttpStatusCode.Conflict, secondBrand.StatusCode);
        var message = Envelope.FirstError(await Envelope.ReadAsync(secondBrand))!;
        Assert.Contains(firstB, message);
        Assert.DoesNotContain(codeA, message);

        var secondProduct = await SendAsync(b, HttpMethod.Post, "/api/mdm/products", ProductBody($"ZZZ2-{_tenant.ToString("N")[..6]}".ToUpperInvariant(), references: [reference]));
        Assert.Equal(HttpStatusCode.Conflict, secondProduct.StatusCode);
        Assert.DoesNotContain(codeA, Envelope.FirstError(await Envelope.ReadAsync(secondProduct))!);
    }

    // ── CT BR5 — the reference match is the WHOLE value (case and outer whitespace aside) ────────────────────────

    [Fact]
    public async Task A_reference_that_only_starts_like_a_taken_one_is_free_and_case_or_whitespace_differences_are_the_same_one()
    {
        var steward = Steward();
        await CreateAsync(steward, Code("PREFIX"), references: [new { sourceSystem = "SAP", externalId = "X1", isPrimary = true }]);

        Assert.Equal(HttpStatusCode.Created, (await SendAsync(steward, HttpMethod.Post, "/api/mdm/brands",
            BrandBody(Code("SHORTER"), references: [new { sourceSystem = "SAP", externalId = "X", isPrimary = true }]))).StatusCode);

        var same = await SendAsync(steward, HttpMethod.Post, "/api/mdm/brands",
            BrandBody(Code("SAMEONE"), references: [new { sourceSystem = " sap ", externalId = " x1 ", isPrimary = true }]));
        Assert.Equal(HttpStatusCode.Conflict, same.StatusCode);
        Assert.StartsWith("external_reference_in_use:", Envelope.FirstError(await Envelope.ReadAsync(same)));
    }

    // ── item 10 (R6) — concurrent writes on one brand ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Eight_concurrent_edits_from_one_read_write_exactly_one()
    {
        var client = Steward();
        var id = await CreateAsync(client, Code("EIGHTPUT"));
        var answers = await Task.WhenAll(Enumerable.Range(0, 8).Select(i =>
            SendAsync(client, HttpMethod.Put, $"/api/mdm/brands/{id}", BrandBody(Code("EIGHTPUT"), name: $"Edit {i}", expectedVersion: 0))));

        Assert.Single(answers, x => x.StatusCode == HttpStatusCode.NoContent);
        Assert.Equal(7, answers.Count(x => x.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(2, (await Repository().GetHistoryAsync(id)).Count);
    }

    [Fact]
    public async Task Eight_concurrent_archives_all_succeed_and_leave_one_archive_entry()
    {
        var client = Steward();
        var id = await CreateAsync(client, Code("EIGHTARC"));
        var answers = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            SendAsync(client, HttpMethod.Post, $"/api/mdm/brands/{id}/archive", null)));

        Assert.All(answers, x => Assert.Equal(HttpStatusCode.NoContent, x.StatusCode));
        Assert.Single(await Repository().GetHistoryAsync(id), x => x.EventType == BrandHistoryEntry.EventArchived);
    }

    [Fact]
    public async Task A_concurrent_edit_and_archive_end_archived_with_an_unbroken_history_chain()
    {
        var client = Steward();
        for (var round = 0; round < 5; round++)
        {
            var id = await CreateAsync(client, Code($"EDARC{round}"));
            await Task.WhenAll(
                SendAsync(client, HttpMethod.Put, $"/api/mdm/brands/{id}", BrandBody(Code($"EDARC{round}"), name: "Racing edit", expectedVersion: 0)),
                SendAsync(client, HttpMethod.Post, $"/api/mdm/brands/{id}/archive", null));

            var stored = await Repository().GetByIdAsync(id);
            Assert.True(stored!.IsArchived);
            var history = await Repository().GetHistoryAsync(id);
            Assert.Equal(Enumerable.Range(0, history.Count), history.Select(x => x.ResultVersion));
            Assert.Equal(BrandHistoryEntry.EventArchived, history[^1].EventType);
            Assert.Equal(stored.Version, history[^1].ResultVersion);
        }
    }

    // ── item 11 (M1) — the class maps the start depends on ──────────────────────────────────────────────────────

    [Fact]
    public async Task A_product_stored_with_a_field_this_version_does_not_know_still_reads()
    {
        var client = ProductSteward();
        var id = await CreateProductAsync(client, Code("PFUTURE"));
        await host.Database.GetCollection<BsonDocument>("mdm_products").UpdateOneAsync(
            new BsonDocument("_id", new BsonBinaryData(id, GuidRepresentation.Standard)),
            new BsonDocument("$set", new BsonDocument("FieldFromALaterVersion", "x")));

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/mdm/products/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/mdm/products")).StatusCode);
    }

    [Fact]
    public void Every_brand_and_product_class_map_is_registered_and_ignores_extra_elements()
    {
        BrandProductClassMaps.EnsureForwardCompatible();
        Assert.All(BrandProductClassMaps.ForwardCompatibleTypes, type =>
            Assert.True(MongoDB.Bson.Serialization.BsonClassMap.LookupClassMap(type).IgnoreExtraElements, type.Name));
    }

    // ── item 12 (M2) — one correlation id, normalised once, shared by the history row and the central audit ─────

    [Theory]
    [InlineData("N")]
    [InlineData("B")]
    public async Task The_history_row_and_the_central_audit_event_carry_the_same_guid_correlation(string format)
    {
        var correlation = Guid.NewGuid();
        var id = await CreateAsync(Steward(), Code($"CORR{format}"), correlationId: correlation.ToString(format).ToUpperInvariant());

        Assert.Equal(correlation.ToString("D"), Assert.Single(await Repository().GetHistoryAsync(id)).CorrelationId);
        Assert.Equal(correlation, Assert.Single(host.Audit.Forwarded, x => x.EntityId == id).CorrelationId);
    }

    [Fact]
    public async Task A_correlation_that_is_not_a_guid_becomes_one_guid_shared_by_the_history_row_and_the_audit_event()
    {
        var id = await CreateAsync(Steward(), Code("CORRBAD"), correlationId: "not-a-guid");

        var stored = Assert.Single(await Repository().GetHistoryAsync(id)).CorrelationId;
        Assert.True(Guid.TryParseExact(stored, "D", out var parsed));
        Assert.Equal(parsed, Assert.Single(host.Audit.Forwarded, x => x.EntityId == id).CorrelationId);
    }

    // ── item 13 (M3) — the trail is ordered by the version chain, not by a clock reading ────────────────────────

    [Fact]
    public async Task The_history_is_ordered_by_version_even_when_the_clock_readings_say_otherwise()
    {
        var client = Steward();
        var id = await CreateAsync(client, Code("ORDER"));
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Put, $"/api/mdm/brands/{id}",
            BrandBody(Code("ORDER"), name: "Second", expectedVersion: 0))).StatusCode);

        // The later version carries an EARLIER instant, in non-UTC offsets. A DateTimeOffset is stored as [ticks, offset
        // minutes] and MongoDB sorts an array by its smallest element, so BOTH elements of version 1 are made smaller than
        // version 0's: a sort on OccurredAtUtc puts version 1 first by any reading.
        var history = host.Database.GetCollection<BrandHistoryEntry>(BrandRepository.HistoryCollectionName);
        await history.UpdateOneAsync(x => x.BrandId == id && x.ResultVersion == 1,
            Builders<BrandHistoryEntry>.Update.Set(x => x.OccurredAtUtc, new DateTimeOffset(2020, 1, 1, 9, 0, 0, TimeSpan.FromHours(-5))));
        await history.UpdateOneAsync(x => x.BrandId == id && x.ResultVersion == 0,
            Builders<BrandHistoryEntry>.Update.Set(x => x.OccurredAtUtc, new DateTimeOffset(2024, 1, 1, 9, 0, 0, TimeSpan.FromHours(3))));

        Assert.Equal([0, 1], (await Repository().GetHistoryAsync(id)).Select(x => x.ResultVersion));
    }

    // ── item 14 (M5) — a field rule names its field ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_description_over_2000_characters_is_a_400_naming_the_field()
    {
        var response = await SendAsync(Steward(), HttpMethod.Post, "/api/mdm/brands",
            BrandBody(Code("LONGDESC"), description: new string('d', 2001)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.StartsWith("field_invalid: Description", Envelope.FirstError(await Envelope.ReadAsync(response)));
    }

    // ── item 17 (M8) — no brand handler takes the generic whole-document writes ─────────────────────────────────

    [Fact]
    public void No_handler_calls_the_generic_create_update_or_delete_of_the_brand_repository()
    {
        var forbidden = new[] { "CreateAsync", "UpdateAsync", "DeleteAsync" };
        var offenders = BrandRepositoryCalls
            .Where(call => forbidden.Contains(call.Method.Name))
            .Select(call => $"{call.Caller} → {call.Method.DeclaringType?.Name}.{call.Method.Name}")
            .ToList();

        Assert.Empty(offenders);
        // The scan is live: it does find the targeted writes the handlers DO make.
        Assert.Contains(BrandRepositoryCalls, call => call.Method.Name == nameof(IBrandRepository.ArchiveWithHistoryAsync));
    }

    // ── item 18 (M10) — the transaction policy ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_write_that_keeps_meeting_transient_transaction_errors_gives_up_as_unavailable_after_ten_attempts()
    {
        var attempts = 0;
        var outcome = await MasterDataAtomicWrite.RunAsync(host.Database.Client, new BrandProductStoreOptions(), null, _ =>
        {
            attempts++;
            var transient = new MongoException("simulated write conflict");
            transient.AddErrorLabel("TransientTransactionError");
            throw transient;
        }, CancellationToken.None);

        Assert.Equal(BrandWriteOutcome.Unavailable, outcome);
        Assert.Equal(MasterDataAtomicWrite.MaxTransactionAttempts, attempts);
        Assert.Equal(10, attempts);
    }

    [Fact]
    public void Brand_and_product_writes_run_with_snapshot_reads_and_majority_writes()
    {
        var options = (MongoDB.Driver.TransactionOptions)typeof(MasterDataAtomicWrite)
            .GetField("Options", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        Assert.Equal(ReadConcern.Snapshot, options.ReadConcern);
        Assert.Equal(WriteConcern.WMajority, options.WriteConcern);
        Assert.Equal(ReadPreference.Primary, options.ReadPreference);
    }

    // ── item 19 (M11) — index DDL once per process, not per request ─────────────────────────────────────────────

    [Fact]
    public async Task Repositories_made_per_request_do_not_repeat_the_index_ddl()
    {
        await CreateAsync(Steward(), Code("IDXONCE"));
        var before = IndexOnce.Runs;
        for (var i = 0; i < 25; i++)
        {
            _ = Repository();
            _ = new ProductRepository(host.Database, new FixedTenant(_tenant));
        }

        Assert.Equal(before, IndexOnce.Runs);
    }

    // ── helpers ────────────────────────────────────────────────────────────────────────────────────────────────

    private static readonly IReadOnlyList<(string Caller, MethodBase Method)> BrandRepositoryCalls = ScanBrandRepositoryCalls();

    /// <summary>Every call / callvirt in the Application assembly whose target is declared on a brand repository type.</summary>
    private static IReadOnlyList<(string Caller, MethodBase Method)> ScanBrandRepositoryCalls()
    {
        var assembly = typeof(CreateBrandHandler).Assembly;
        var calls = new List<(string, MethodBase)>();
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        foreach (var type in assembly.GetTypes())
        {
            foreach (var method in type.GetMethods(all).Cast<MethodBase>().Concat(type.GetConstructors(all)))
            {
                var il = method.GetMethodBody()?.GetILAsByteArray();
                if (il is null) continue;
                for (var i = 0; i + 4 < il.Length; i++)
                {
                    if (il[i] != 0x28 && il[i] != 0x6F) continue; // call, callvirt
                    MethodBase? target;
                    try
                    {
                        target = method.Module.ResolveMethod(BitConverter.ToInt32(il, i + 1),
                            type.IsGenericType ? type.GetGenericArguments() : null,
                            method.IsGenericMethod ? method.GetGenericArguments() : null);
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    var declaring = target?.DeclaringType;
                    if (declaring is null) continue;
                    var isBrandRepository = declaring == typeof(IBrandRepository)
                        || (declaring.IsGenericType && declaring.GetGenericTypeDefinition() == typeof(IRepository<>)
                            && declaring.GetGenericArguments()[0] == typeof(Brand));
                    if (isBrandRepository) calls.Add(($"{type.FullName}.{method.Name}", target!));
                }
            }
        }

        return calls;
    }

    private string Code(string stem) => $"{stem}-{_tenant.ToString("N")[..6]}".ToUpperInvariant();

    private HttpClient Steward(params string[] extra) =>
        host.Client(host.Token(_tenant, _user, [Read, Create, Update, Archive, .. extra]));

    private HttpClient ProductSteward() => host.Client(host.Token(_tenant, _user, ProductsRead, ProductsCreate, ProductsUpdate, Read));

    private static object BrandBody(
        string code, string? name = null, string status = "active", string? description = null, int? expectedVersion = null,
        object[]? references = null, DateTimeOffset? effectiveFrom = null, DateTimeOffset? effectiveTo = null) => new
    {
        brandCode = code,
        brandName = name ?? $"Brand {code.Split('-')[0]}",
        brandStatus = status,
        description,
        ownerCompanyId = (Guid?)null,
        businessUnitId = (Guid?)null,
        therapeuticAreaId = (Guid?)null,
        effectiveFrom = effectiveFrom ?? From2025,
        effectiveTo,
        externalReferences = references,
        expectedVersion
    };

    private static BrandWriteRequest WriteRequest(string code) =>
        new(code, $"Brand {code}", "active", null, null, null, null, From2025, null, null);

    private static object ProductBody(string code, Guid? brandId = null, object[]? references = null, string? description = null) => new
    {
        productCode = code,
        productName = $"Product {code}",
        productStatus = "active",
        brandId,
        description,
        effectiveFrom = From2025,
        externalReferences = references
    };

    private async Task<Guid> CreateAsync(
        HttpClient client, string code, string? name = null, string status = "active", object[]? references = null,
        DateTimeOffset? effectiveFrom = null, DateTimeOffset? effectiveTo = null, string? correlationId = null)
    {
        var response = await SendAsync(client, HttpMethod.Post, "/api/mdm/brands",
            BrandBody(code, name, status, references: references, effectiveFrom: effectiveFrom, effectiveTo: effectiveTo), correlationId);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await Envelope.ReadAsync(response)).GetProperty("data").GetGuid();
    }

    private static async Task<Guid> CreateProductAsync(HttpClient client, string code, Guid? brandId = null)
    {
        var response = await SendAsync(client, HttpMethod.Post, "/api/mdm/products", ProductBody(code, brandId));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await Envelope.ReadAsync(response)).GetProperty("data").GetGuid();
    }

    private static Task<HttpResponseMessage> SendAsync(
        HttpClient client, HttpMethod method, string path, object? body, string? correlationId = null)
    {
        var request = new HttpRequestMessage(method, path) { Content = body is null ? null : Envelope.Json(body) };
        if (correlationId is not null) request.Headers.TryAddWithoutValidation("X-Correlation-Id", correlationId);
        return client.SendAsync(request);
    }

    /// <summary>A brand as legacy data left it: written straight to the collection, with no reservation and no history.</summary>
    private async Task<Guid> InsertRawBrandAsync(string code, BrandProductExternalReference reference)
    {
        var brand = NewBrand(code);
        brand.TenantId = _tenant;
        brand.ExternalReferences = [Copy(reference)];
        await host.Database.GetCollection<Brand>("mdm_brands").InsertOneAsync(brand);
        return brand.Id;
    }

    private async Task<Guid> InsertRawProductAsync(string code, BrandProductExternalReference reference)
    {
        var product = new Product
        {
            TenantId = _tenant,
            ProductCode = code,
            ProductName = $"Product {code}",
            ProductStatus = "active",
            EffectiveFrom = From2025,
            ExternalReferences = [Copy(reference)]
        };
        await host.Database.GetCollection<Product>("mdm_products").InsertOneAsync(product);
        return product.Id;
    }

    private static BrandProductExternalReference Copy(BrandProductExternalReference x) => new()
    {
        SourceSystem = x.SourceSystem,
        ExternalId = x.ExternalId,
        IsPrimary = x.IsPrimary,
        ImportedAt = x.ImportedAt
    };

    private async Task<IReadOnlyList<Brand>> RawBrandsCarryingAsync(string source, string externalId)
        => await host.Database.GetCollection<Brand>("mdm_brands")
            .Find(x => x.TenantId == _tenant && x.ExternalReferences.Any(r => r.SourceSystem == source && r.ExternalId == externalId))
            .ToListAsync();

    private Task SeedHistoryRowAsync(Guid brandId, int resultVersion)
        => host.Database.GetCollection<BrandHistoryEntry>(BrandRepository.HistoryCollectionName).InsertOneAsync(new BrandHistoryEntry
        {
            TenantId = _tenant,
            BrandId = brandId,
            ResultVersion = resultVersion,
            EventType = BrandHistoryEntry.EventUpdated,
            CorrelationId = Guid.NewGuid().ToString("D"),
            OccurredAtUtc = DateTimeOffset.UtcNow,
            After = new BrandAuditSnapshot()
        });

    private BrandRepository Repository() => new(host.Database, new FixedTenant(_tenant));

    private static Brand NewBrand(string code) => new()
    {
        BrandCode = code,
        BrandName = $"Brand {code}",
        BrandStatus = "active",
        EffectiveFrom = From2025
    };

    private static BrandHistoryEntry Entry(string eventType) => new()
    {
        EventType = eventType,
        CorrelationId = Guid.NewGuid().ToString("D"),
        OccurredAtUtc = DateTimeOffset.UtcNow
    };

    private sealed class FixedTenant(Guid tenantId) : ITenantContext
    {
        public Guid TenantId { get; private set; } = tenantId;
        public bool IsResolved => true;
        public void SetTenant(Guid tenantId) => TenantId = tenantId;
    }
}
