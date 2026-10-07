using System.Net;
using System.Text;
using ClosedXML.Excel;
using Diten.MdmService.Api.ModuleRegistration;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.BrandProductContract;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.Vocabulary;
using Diten.MdmService.Persistence.Repositories;
using Diten.Shared.Core;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// WP-MDM-BRAND-ACCEPT-01 FIX3 (P-2026-10-07-BRAND-FIX3) — the third acceptance round of the Brand / Product master, on the
/// production stack (MdmHttpHost: real controllers, permission policy, pipeline, handlers, repositories, class maps, indexes
/// and transactions over a throwaway replica set). Every rule named in the round closes with a named test here.
/// </summary>
[Collection(MdmHttpCollection.Name)]
public sealed class BrandFix3HttpTests(MdmHttpHost host)
{
    private const string Read = "mdm.brands.read";
    private const string Create = "mdm.brands.create";
    private const string Update = "mdm.brands.update";
    private const string Archive = "mdm.brands.archive";
    private const string Export = "mdm.brands.export";
    private const string ProductsRead = "mdm.products.read";
    private const string ProductsCreate = "mdm.products.create";
    private const string ProductsUpdate = "mdm.products.update";
    private const string ProductsArchive = "mdm.products.archive";

    private static readonly DateTimeOffset From2025 = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _user = Guid.NewGuid();

    // ── R-2 — a failed index creation is not cached ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_failed_index_creation_is_not_cached_and_the_next_repository_retries_and_succeeds()
    {
        // A database of its own on the throwaway server (new every run; MongoTestDatabaseGuard: no per-run Guid names),
        // holding an index whose NAME the repository needs with other keys: createIndexes refuses (IndexKeySpecsConflict).
        var database = host.Database.Client.GetDatabase("diten_mdm_itest_fix3_index_retry");
        var brands = database.GetCollection<BsonDocument>("mdm_brands");
        await brands.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(
            new BsonDocument("BrandName", 1), new CreateIndexOptions { Name = "ux_mdm_brands_tenant_code" }));

        // FIX4 (F1) — the constructor no longer throws; without its unique indexes the store refuses writes (named 503).
        var refused = new BrandRepository(database, new FixedTenant(_tenant));
        Assert.Equal(BrandWriteOutcome.Unavailable, await refused.CreateWithHistoryAsync(NewBrand("IDXRETRY"), Entry()));

        await brands.Indexes.DropOneAsync("ux_mdm_brands_tenant_code");
        var now = DateTimeOffset.UtcNow;
        var clock = IndexOnce.Now;
        IndexOnce.Now = () => now + IndexOnce.RetryAfter + TimeSpan.FromSeconds(1);
        try
        {
            var repository = new BrandRepository(database, new FixedTenant(_tenant));
            Assert.Equal(BrandWriteOutcome.Written, await repository.CreateWithHistoryAsync(NewBrand("IDXRETRY"), Entry()));
        }
        finally
        {
            IndexOnce.Now = clock;
        }

        var indexes = await (await brands.Indexes.ListAsync()).ToListAsync();
        Assert.Contains(indexes, x => x["name"] == "ux_mdm_brands_tenant_code" && x.GetValue("unique", false).ToBoolean());
    }

    [Fact]
    public void Index_ddl_that_throws_runs_again_on_the_next_call_and_a_success_runs_once()
    {
        var database = host.Database.Client.GetDatabase("diten_mdm_itest_fix3_index_once");
        var calls = 0;
        var start = DateTimeOffset.UtcNow;
        var clock = IndexOnce.Now;
        IndexOnce.Now = () => start;
        try
        {
            Assert.False(IndexOnce.Ensure(database, "c", () => { calls++; throw new InvalidOperationException("first createIndexes failed"); }));
            // FIX4 (F1) — inside the retry window: no DDL at all, not ready.
            Assert.False(IndexOnce.Ensure(database, "c", () => calls++));
            Assert.Equal(1, calls);

            IndexOnce.Now = () => start + IndexOnce.RetryAfter + TimeSpan.FromSeconds(1);
            Assert.True(IndexOnce.Ensure(database, "c", () => calls++));
            Assert.True(IndexOnce.Ensure(database, "c", () => calls++));
            Assert.Equal(2, calls);
        }
        finally
        {
            IndexOnce.Now = clock;
        }
    }

    // ── R-3 — product writes are conditional on the version they were made on ─────────────────────────────────────

    [Fact]
    public async Task Two_concurrent_product_edits_from_one_read_write_one_and_leave_no_orphan_reservation()
    {
        var client = ProductSteward();
        var product = await CreateProductAsync(client, Code("PRACE3"));
        var first = new { sourceSystem = "SAP", externalId = "R3-X", isPrimary = true };
        var second = new { sourceSystem = "SAP", externalId = "R3-Y", isPrimary = true };

        var answers = await Task.WhenAll(
            SendAsync(client, HttpMethod.Put, $"/api/mdm/products/{product}", ProductBody(Code("PRACE3"), references: [first], expectedVersion: 0)),
            SendAsync(client, HttpMethod.Put, $"/api/mdm/products/{product}", ProductBody(Code("PRACE3"), references: [second], expectedVersion: 0)));

        Assert.Single(answers, x => x.StatusCode == HttpStatusCode.NoContent);
        var loser = Assert.Single(answers, x => x.StatusCode == HttpStatusCode.Conflict);
        Assert.StartsWith("version_conflict:", Envelope.FirstError(await Envelope.ReadAsync(loser)));

        // The stored product carries exactly one of the two, and the reservations hold exactly what it carries.
        var stored = (await GetProductAsync(client, product)).GetProperty("externalReferences");
        var kept = Assert.Single(stored.EnumerateArray()).GetProperty("externalId").GetString();
        var held = await ReservationsOfAsync(product);
        Assert.Equal([kept!.ToUpperInvariant()], held);

        // The pair the loser tried is free: another product takes it.
        var lost = kept == "R3-X" ? second : first;
        Assert.Equal(HttpStatusCode.Created,
            (await SendAsync(client, HttpMethod.Post, "/api/mdm/products", ProductBody(Code("PTAKER3"), references: [lost]))).StatusCode);
    }

    [Fact]
    public async Task A_product_edit_that_does_not_say_which_read_it_was_made_on_is_a_400_and_an_older_one_a_409()
    {
        var client = ProductSteward();
        var product = await CreateProductAsync(client, Code("PNOVER"));

        var missing = await SendAsync(client, HttpMethod.Put, $"/api/mdm/products/{product}", ProductBody(Code("PNOVER"), description: "x"));
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.StartsWith("expected_version_required:", Envelope.FirstError(await Envelope.ReadAsync(missing)));

        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Put, $"/api/mdm/products/{product}",
            ProductBody(Code("PNOVER"), description: "first", expectedVersion: 0))).StatusCode);
        var stale = await SendAsync(client, HttpMethod.Put, $"/api/mdm/products/{product}", ProductBody(Code("PNOVER"), description: "second", expectedVersion: 0));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.StartsWith("version_conflict:", Envelope.FirstError(await Envelope.ReadAsync(stale)));
        Assert.Equal("first", (await GetProductAsync(client, product)).GetProperty("description").GetString());
    }

    [Fact]
    public async Task A_legacy_product_without_version_or_archive_fields_is_edited_at_version_0_and_then_archived()
    {
        // A document as an early slice wrote it: no Version, no IsArchived element at all.
        var id = Guid.NewGuid();
        await host.Database.GetCollection<BsonDocument>("mdm_products").InsertOneAsync(new BsonDocument
        {
            { "_id", new BsonBinaryData(id, GuidRepresentation.Standard) },
            { "TenantId", new BsonBinaryData(_tenant, GuidRepresentation.Standard) },
            { "IsDeleted", false },
            { "ProductCode", Code("PLEGACY") },
            { "ProductName", "Legacy product" },
            { "ProductStatus", "active" },
            { "EffectiveFrom", new BsonArray { From2025.UtcTicks, 0 } },
            { "ExternalReferences", new BsonArray() }
        });

        var client = host.Client(host.Token(_tenant, _user, ProductsRead, ProductsCreate, ProductsUpdate, ProductsArchive, Read));
        Assert.Equal(0, (await GetProductAsync(client, id)).GetProperty("version").GetInt32());
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Put, $"/api/mdm/products/{id}",
            ProductBody(Code("PLEGACY"), description: "edited", expectedVersion: 0))).StatusCode);
        Assert.Equal(1, (await GetProductAsync(client, id)).GetProperty("version").GetInt32());
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Post, $"/api/mdm/products/{id}/archive", null)).StatusCode);
        Assert.True((await GetProductAsync(client, id)).GetProperty("isArchived").GetBoolean());
    }

    [Fact]
    public async Task A_concurrent_product_edit_and_archive_end_archived_and_the_edit_never_unarchives()
    {
        var client = host.Client(host.Token(_tenant, _user, ProductsRead, ProductsCreate, ProductsUpdate, ProductsArchive, Read));
        for (var round = 0; round < 5; round++)
        {
            var product = await CreateProductAsync(client, Code($"PEDARC{round}"));
            await Task.WhenAll(
                SendAsync(client, HttpMethod.Put, $"/api/mdm/products/{product}", ProductBody(Code($"PEDARC{round}"), description: "racing", expectedVersion: 0)),
                SendAsync(client, HttpMethod.Post, $"/api/mdm/products/{product}/archive", null));

            Assert.True((await GetProductAsync(client, product)).GetProperty("isArchived").GetBoolean());
        }
    }

    [Fact]
    public async Task The_product_detail_names_its_brand_and_whether_it_still_links_even_for_a_reader_without_brand_read()
    {
        var steward = Steward();
        var brand = await CreateAsync(steward, Code("LINKED3"));
        var product = await CreateProductAsync(ProductSteward(), Code("PLINKED3"), brandId: brand);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(steward, HttpMethod.Put, $"/api/mdm/brands/{brand}",
            BrandBody(Code("LINKED3"), status: "inactive", expectedVersion: 0))).StatusCode);

        var productOnly = host.Client(host.Token(_tenant, _user, ProductsRead));
        var linked = (await GetProductAsync(productOnly, product)).GetProperty("linkedBrand");
        Assert.Equal(brand, linked.GetProperty("brandId").GetGuid());
        Assert.Equal(Code("LINKED3"), linked.GetProperty("brandCode").GetString());
        Assert.False(linked.GetProperty("isLinkable").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden, (await productOnly.GetAsync($"/api/mdm/brands/{brand}")).StatusCode);
    }

    // ── R-5 — validity dates are calendar days ─────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(3)]
    [InlineData(-5)]
    public void An_end_day_entered_at_any_offset_is_linkable_all_of_that_day_and_not_the_next(int offsetHours)
    {
        var endDay = new DateTimeOffset(2026, 10, 7, 0, 0, 0, TimeSpan.FromHours(offsetHours));
        var brand = ActiveBrand(From2025, endDay);

        Assert.True(brand.IsLinkableAt(new DateTimeOffset(2026, 10, 7, 0, 0, 1, TimeSpan.Zero)));
        Assert.True(brand.IsLinkableAt(new DateTimeOffset(2026, 10, 7, 23, 59, 59, TimeSpan.Zero)));
        Assert.False(brand.IsLinkableAt(new DateTimeOffset(2026, 10, 8, 0, 0, 1, TimeSpan.Zero)));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(-5)]
    public void A_start_day_entered_at_any_offset_never_opens_a_day_early(int offsetHours)
    {
        var startDay = new DateTimeOffset(2026, 10, 7, 0, 0, 0, TimeSpan.FromHours(offsetHours));
        var brand = ActiveBrand(startDay, null);

        Assert.False(brand.IsLinkableAt(new DateTimeOffset(2026, 10, 6, 23, 59, 59, TimeSpan.Zero)));
        Assert.True(brand.IsLinkableAt(new DateTimeOffset(2026, 10, 7, 0, 0, 1, TimeSpan.Zero)));
    }

    [Fact]
    public async Task A_brand_whose_last_day_is_today_entered_at_plus_three_hours_still_links_and_is_stored_as_that_day()
    {
        var today = DateTimeOffset.UtcNow.UtcDateTime.Date;
        var endToday = new DateTimeOffset(DateTime.SpecifyKind(today, DateTimeKind.Unspecified), TimeSpan.FromHours(3)); // 21:00 UTC yesterday
        var brand = await CreateAsync(Steward(), Code("ENDTODAY"), effectiveTo: endToday);

        Assert.Equal(HttpStatusCode.Created, (await SendAsync(ProductSteward(), HttpMethod.Post, "/api/mdm/products",
            ProductBody(Code("PENDTODAY"), brandId: brand))).StatusCode);
        var stored = (await GetAsync(Steward(), brand)).GetProperty("effectiveTo").GetDateTimeOffset();
        Assert.Equal(new DateTimeOffset(today, TimeSpan.Zero), stored);
    }

    // ── R-6 — rules that had no test ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_brand_export_of_more_than_50000_rows_is_a_413_and_no_file()
    {
        var tenant = Guid.NewGuid();
        var rows = Enumerable.Range(0, 50_001).Select(i => new Brand
        {
            TenantId = tenant,
            BrandCode = $"CAP-{i:D6}",
            BrandName = $"Cap {i}",
            BrandStatus = "active",
            EffectiveFrom = From2025
        });
        await host.Database.GetCollection<Brand>("mdm_brands").InsertManyAsync(rows, new InsertManyOptions { IsOrdered = false });

        var response = await host.Client(host.Token(tenant, _user, Read, Export)).GetAsync("/api/mdm/brands/export?format=csv");

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Contains("EXPORT_TOO_LARGE", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task An_edit_that_removes_a_reference_releases_it_for_another_brand()
    {
        var steward = Steward();
        var holder = await CreateAsync(steward, Code("RELA"), references: [new { sourceSystem = "SAP", externalId = "REL-X", isPrimary = true }]);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(steward, HttpMethod.Put, $"/api/mdm/brands/{holder}",
            BrandBody(Code("RELA"), expectedVersion: 0, references: []))).StatusCode);

        Assert.Equal(HttpStatusCode.Created, (await SendAsync(steward, HttpMethod.Post, "/api/mdm/brands",
            BrandBody(Code("RELB"), references: [new { sourceSystem = "SAP", externalId = "REL-X", isPrimary = true }]))).StatusCode);
    }

    [Fact]
    public async Task A_product_edit_that_removes_a_reference_releases_it_for_another_product()
    {
        var client = ProductSteward();
        var reference = new { sourceSystem = "SAP", externalId = "PREL-X", isPrimary = true };
        var holder = await CreateProductAsync(client, Code("PRELA"), references: [reference]);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Put, $"/api/mdm/products/{holder}",
            ProductBody(Code("PRELA"), references: [], expectedVersion: 0))).StatusCode);

        Assert.Equal(HttpStatusCode.Created,
            (await SendAsync(client, HttpMethod.Post, "/api/mdm/products", ProductBody(Code("PRELB"), references: [reference]))).StatusCode);
    }

    [Fact]
    public async Task Six_concurrent_brand_edits_adding_one_reference_write_exactly_one()
    {
        var steward = Steward();
        var ids = new List<Guid>();
        for (var i = 0; i < 6; i++) ids.Add(await CreateAsync(steward, Code($"UREF{i}")));
        var reference = new { sourceSystem = "SAP", externalId = "UPD-RACE", isPrimary = true };

        var answers = await Task.WhenAll(ids.Select((id, i) => SendAsync(steward, HttpMethod.Put, $"/api/mdm/brands/{id}",
            BrandBody(Code($"UREF{i}"), expectedVersion: 0, references: [reference]))));

        Assert.Single(answers, x => x.StatusCode == HttpStatusCode.NoContent);
        Assert.Equal(5, answers.Count(x => x.StatusCode == HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task Six_concurrent_product_edits_adding_one_reference_write_exactly_one()
    {
        var client = ProductSteward();
        var ids = new List<Guid>();
        for (var i = 0; i < 6; i++) ids.Add(await CreateProductAsync(client, Code($"PUREF{i}")));
        var reference = new { sourceSystem = "SAP", externalId = "PUPD-RACE", isPrimary = true };

        var answers = await Task.WhenAll(ids.Select((id, i) => SendAsync(client, HttpMethod.Put, $"/api/mdm/products/{id}",
            ProductBody(Code($"PUREF{i}"), references: [reference], expectedVersion: 0))));

        Assert.Single(answers, x => x.StatusCode == HttpStatusCode.NoContent);
        Assert.Equal(5, answers.Count(x => x.StatusCode == HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task The_brand_export_holds_this_tenants_brands_only()
    {
        var other = Guid.NewGuid();
        var foreignCode = $"FOREIGN-{other.ToString("N")[..6]}".ToUpperInvariant();
        Assert.Equal(HttpStatusCode.Created, (await SendAsync(host.Client(host.Token(other, _user, Read, Create)),
            HttpMethod.Post, "/api/mdm/brands", BrandBody(foreignCode))).StatusCode);
        await CreateAsync(Steward(), Code("MINE"));

        var file = Encoding.UTF8.GetString(await (await Steward(Export).GetAsync("/api/mdm/brands/export?format=csv&includeArchived=true")).Content.ReadAsByteArrayAsync());

        Assert.Contains(Code("MINE"), file);
        Assert.DoesNotContain(foreignCode, file);
    }

    [Theory]
    [InlineData("=1+1")]
    [InlineData("+cmd")]
    [InlineData("-cmd")]
    [InlineData("@SUM(A1)")]
    [InlineData("\tlead")]
    [InlineData("\rlead")]
    public async Task A_brand_name_that_starts_like_a_formula_is_neutralised_in_csv_and_text_in_xlsx(string name)
    {
        // Written straight to the store: the API trims the name, so a leading TAB / CR can only come from data.
        var brand = NewBrand(Code($"FX{(int)name[0]:D3}"));
        brand.TenantId = _tenant;
        brand.BrandName = name;
        await host.Database.GetCollection<Brand>("mdm_brands").InsertOneAsync(brand);

        var csv = Encoding.UTF8.GetString(await (await Steward(Export).GetAsync("/api/mdm/brands/export?format=csv&columns=brandCode,brandName")).Content.ReadAsByteArrayAsync());
        Assert.Contains("'" + name, csv);
        Assert.DoesNotContain("," + name, csv.Replace("\"", string.Empty));

        var xlsx = await (await Steward(Export).GetAsync("/api/mdm/brands/export?format=xlsx&columns=brandCode,brandName")).Content.ReadAsByteArrayAsync();
        using var workbook = new XLWorkbook(new MemoryStream(xlsx));
        var sheet = workbook.Worksheets.First();
        var row = sheet.RowsUsed().Single(x => x.Cell(1).GetString() == brand.BrandCode);
        var cell = row.Cell(2);
        // XML normalises a line break inside a cell (CR is read back as LF); the value is still the name, as text.
        Assert.Equal(name.Replace('\r', '\n'), cell.GetString());
        Assert.Equal(XLDataType.Text, cell.DataType);
        Assert.Equal("@", cell.Style.NumberFormat.Format);
        Assert.False(cell.HasFormula);
    }

    // ── M-1 — on a server without transactions the reservation comes first ────────────────────────────────────────

    [Fact]
    public async Task In_development_without_transactions_a_held_reference_refuses_the_brand_before_anything_is_written()
    {
        var dev = new BrandProductStoreOptions { AllowNonTransactionalWrites = true };
        var repository = new BrandRepository(host.StandaloneDatabase, new FixedTenant(_tenant), dev);
        var holder = NewBrand(Code("DEVHOLD"));
        holder.ExternalReferences = [new BrandProductExternalReference { SourceSystem = "SAP", ExternalId = "DEV-M1", IsPrimary = true }];
        Assert.Equal(BrandWriteOutcome.Written, await repository.CreateWithHistoryAsync(holder, Entry()));

        var taker = NewBrand(Code("DEVTAKE"));
        taker.ExternalReferences = [new BrandProductExternalReference { SourceSystem = "sap", ExternalId = "dev-m1", IsPrimary = true }];
        Assert.Equal(BrandWriteOutcome.ReferenceInUse, await repository.CreateWithHistoryAsync(taker, Entry()));
        Assert.Null(await repository.GetByIdAsync(taker.Id));

        // The edit path too: nothing changes on a brand whose added pair is held.
        var editor = NewBrand(Code("DEVEDIT"));
        Assert.Equal(BrandWriteOutcome.Written, await repository.CreateWithHistoryAsync(editor, Entry()));
        var proposed = NewBrand(Code("DEVEDIT"));
        proposed.Id = editor.Id;
        proposed.BrandName = "Must not land";
        proposed.ExternalReferences = [new BrandProductExternalReference { SourceSystem = "SAP", ExternalId = "DEV-M1", IsPrimary = true }];
        Assert.Equal(BrandWriteOutcome.ReferenceInUse, await repository.UpdateEditableFieldsWithHistoryAsync(
            proposed, 0, Entry(), proposed.ExternalReferences, []));
        var stored = await repository.GetByIdAsync(editor.Id);
        Assert.Equal(0, stored!.Version);
        Assert.Equal(editor.BrandName, stored.BrandName);
    }

    // ── M-2 — a commit whose result stays unknown is named, never a 500 ───────────────────────────────────────────

    [Fact]
    public async Task A_commit_whose_result_stays_unknown_after_five_tries_is_the_ambiguous_outcome()
    {
        var commits = 0;
        var outcome = await MasterDataAtomicWrite.RunAsync(host.Database.Client, new BrandProductStoreOptions(), null,
            _ => Task.FromResult(BrandWriteOutcome.Written), CancellationToken.None,
            (_, _) =>
            {
                commits++;
                var unknown = new MongoException("simulated lost commit acknowledgement");
                unknown.AddErrorLabel("UnknownTransactionCommitResult");
                throw unknown;
            });

        Assert.Equal(BrandWriteOutcome.Ambiguous, outcome);
        Assert.Equal(MasterDataAtomicWrite.MaxCommitAttempts, commits);
    }

    [Fact]
    public void The_ambiguous_outcome_answers_a_named_503_write_outcome_unknown()
    {
        var answer = BrandProductFailures.OutcomeUnknown<NoContent>();
        Assert.Equal(503, answer.StatusCode);
        Assert.False(answer.IsSuccessful);
        Assert.StartsWith("write_outcome_unknown:", Assert.Single(answer.Errors!));
        Assert.Contains(BrandProductReasonCodes.WriteOutcomeUnknown, BrandProductReasonCodes.All);
    }

    // ── M-3 — an archived brand releases its references, in its history ───────────────────────────────────────────

    [Fact]
    public async Task Archiving_a_brand_releases_its_references_records_them_and_frees_them_for_another_brand()
    {
        var steward = Steward();
        var reference = new { sourceSystem = "SAP", externalId = "ARC-X", isPrimary = true };
        var archived = await CreateAsync(steward, Code("ARCA"), references: [reference]);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(steward, HttpMethod.Post, $"/api/mdm/brands/{archived}/archive", null)).StatusCode);

        var entry = Assert.Single(await Repository().GetHistoryAsync(archived), x => x.EventType == BrandHistoryEntry.EventArchived);
        Assert.Equal(["ARC-X"], entry.ReleasedExternalReferences.Select(x => x.ExternalId));
        Assert.Empty(await ReservationsOfAsync(archived));

        Assert.Equal(HttpStatusCode.Created,
            (await SendAsync(steward, HttpMethod.Post, "/api/mdm/brands", BrandBody(Code("ARCB"), references: [reference]))).StatusCode);
    }

    [Fact]
    public async Task Archiving_a_product_releases_its_references_for_another_product()
    {
        var client = host.Client(host.Token(_tenant, _user, ProductsRead, ProductsCreate, ProductsUpdate, ProductsArchive, Read));
        var reference = new { sourceSystem = "SAP", externalId = "PARC-X", isPrimary = true };
        var archived = await CreateProductAsync(client, Code("PARCA"), references: [reference]);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Post, $"/api/mdm/products/{archived}/archive", null)).StatusCode);

        Assert.Equal(HttpStatusCode.Created,
            (await SendAsync(client, HttpMethod.Post, "/api/mdm/products", ProductBody(Code("PARCB"), references: [reference]))).StatusCode);
    }

    // ── M-5 — the contract states the module's nine keys ──────────────────────────────────────────────────────────

    [Fact]
    public async Task The_contract_lists_exactly_the_brand_module_manifest_keys()
    {
        var contract = (await Envelope.ReadAsync(await Steward().GetAsync("/api/mdm/brand-products/contract"))).GetProperty("data");
        var listed = contract.GetProperty("permissions").EnumerateArray().Select(x => x.GetString()!).ToHashSet(StringComparer.Ordinal);
        var declared = new BrandProductMasterManifestProvider().GetManifest().Pages
            .SelectMany(page => page.Actions.Select(action => action.PermissionKey).Append(page.RequiredPermission))
            .ToHashSet(StringComparer.Ordinal);

        Assert.True(listed.SetEquals(declared), $"contract [{string.Join(", ", listed)}] vs manifest [{string.Join(", ", declared)}]");
        Assert.DoesNotContain("import-export-engine-out-of-scope",
            contract.GetProperty("limitations").EnumerateArray().Select(x => x.GetString()));
    }

    // ── M-6 — the duplicate check keeps the two parts of a pair apart ──────────────────────────────────────────────

    [Fact]
    public async Task Two_references_whose_parts_only_concatenate_alike_are_not_a_duplicate()
    {
        var response = await SendAsync(Steward(), HttpMethod.Post, "/api/mdm/brands", BrandBody(Code("PAIRKEY"), references:
        [
            new { sourceSystem = "SAP", externalId = "1X", isPrimary = true },
            new { sourceSystem = "SAP1", externalId = "X", isPrimary = true }
        ]));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // ── M-8 — the file is not cached, and the export leaves a central event ───────────────────────────────────────

    [Fact]
    public async Task The_brand_export_file_is_no_store_and_leaves_a_central_export_event()
    {
        await CreateAsync(Steward(), Code("NOSTORE"));
        var correlation = Guid.NewGuid();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/mdm/brands/export?format=csv");
        request.Headers.Add("X-Correlation-Id", correlation.ToString("D"));

        var response = await Steward(Export).SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var forwarded = Assert.Single(host.Audit.Forwarded, x => x.CorrelationId == correlation);
        Assert.Equal("BrandRegisterExport", forwarded.RequestType);
        Assert.Equal(9, forwarded.Category);   // DataExport
        Assert.Equal(10, forwarded.Operation); // Export
    }

    // ── helpers ────────────────────────────────────────────────────────────────────────────────────────────────

    private string Code(string stem) => $"{stem}-{_tenant.ToString("N")[..6]}".ToUpperInvariant();

    private HttpClient Steward(params string[] extra) =>
        host.Client(host.Token(_tenant, _user, [Read, Create, Update, Archive, .. extra]));

    private HttpClient ProductSteward() => host.Client(host.Token(_tenant, _user, ProductsRead, ProductsCreate, ProductsUpdate, Read));

    private static object BrandBody(
        string code, string? name = null, string status = "active", int? expectedVersion = null, object[]? references = null,
        DateTimeOffset? effectiveTo = null) => new
    {
        brandCode = code,
        brandName = name ?? $"Brand {code.Split('-')[0]}",
        brandStatus = status,
        description = (string?)null,
        ownerCompanyId = (Guid?)null,
        businessUnitId = (Guid?)null,
        therapeuticAreaId = (Guid?)null,
        effectiveFrom = From2025,
        effectiveTo,
        externalReferences = references,
        expectedVersion
    };

    private static object ProductBody(
        string code, Guid? brandId = null, object[]? references = null, string? description = null, int? expectedVersion = null) => new
    {
        productCode = code,
        productName = $"Product {code}",
        productStatus = "active",
        brandId,
        description,
        effectiveFrom = From2025,
        externalReferences = references,
        expectedVersion
    };

    private async Task<Guid> CreateAsync(HttpClient client, string code, object[]? references = null, DateTimeOffset? effectiveTo = null)
    {
        var response = await SendAsync(client, HttpMethod.Post, "/api/mdm/brands", BrandBody(code, references: references, effectiveTo: effectiveTo));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await Envelope.ReadAsync(response)).GetProperty("data").GetGuid();
    }

    private static async Task<Guid> CreateProductAsync(HttpClient client, string code, Guid? brandId = null, object[]? references = null)
    {
        var response = await SendAsync(client, HttpMethod.Post, "/api/mdm/products", ProductBody(code, brandId, references));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await Envelope.ReadAsync(response)).GetProperty("data").GetGuid();
    }

    private static async Task<System.Text.Json.JsonElement> GetAsync(HttpClient client, Guid id)
    {
        var response = await client.GetAsync($"/api/mdm/brands/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await Envelope.ReadAsync(response)).GetProperty("data");
    }

    private static async Task<System.Text.Json.JsonElement> GetProductAsync(HttpClient client, Guid id)
    {
        var response = await client.GetAsync($"/api/mdm/products/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await Envelope.ReadAsync(response)).GetProperty("data");
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, object? body)
    {
        var request = new HttpRequestMessage(method, path) { Content = body is null ? null : Envelope.Json(body) };
        return client.SendAsync(request);
    }

    private async Task<List<string>> ReservationsOfAsync(Guid recordId)
        => (await host.Database.GetCollection<ExternalReferenceReservation>(ExternalReferenceReservations.CollectionName)
                .Find(x => x.TenantId == _tenant && x.RecordId == recordId).ToListAsync())
            .Select(x => x.ExternalIdKey)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

    private BrandRepository Repository() => new(host.Database, new FixedTenant(_tenant));

    private static Brand ActiveBrand(DateTimeOffset from, DateTimeOffset? to) => new()
    {
        BrandCode = "CAL",
        BrandName = "Calendar",
        BrandStatus = BrandProductVocabulary.StatusActive,
        EffectiveFrom = from,
        EffectiveTo = to
    };

    private static Brand NewBrand(string code) => new()
    {
        BrandCode = code,
        BrandName = $"Brand {code}",
        BrandStatus = "active",
        EffectiveFrom = From2025
    };

    private static BrandHistoryEntry Entry() => new()
    {
        EventType = BrandHistoryEntry.EventCreated,
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
