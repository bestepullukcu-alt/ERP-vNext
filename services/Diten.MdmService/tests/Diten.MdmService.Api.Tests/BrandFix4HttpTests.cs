using System.Diagnostics;
using System.Net;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.Brand;
using Diten.MdmService.Application.Features.Brand.Commands;
using Diten.MdmService.Application.Features.Brand.Handlers.CommandHandlers;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// WP-MDM-BRAND-ACCEPT-01 FIX4 (P-2026-10-07-BRAND-FIX4) — the last short round before acceptance, on the production stack
/// (MdmHttpHost) and the real stores over the throwaway replica set.
/// </summary>
[Collection(MdmHttpCollection.Name)]
public sealed class BrandFix4HttpTests(MdmHttpHost host)
{
    private const string Read = "mdm.brands.read";
    private const string Create = "mdm.brands.create";
    private const string Update = "mdm.brands.update";
    private const string ProductsRead = "mdm.products.read";
    private const string ProductsCreate = "mdm.products.create";
    private const string ProductsUpdate = "mdm.products.update";

    private static readonly DateTimeOffset From2025 = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _user = Guid.NewGuid();

    // ── F1 — a failed index creation: one attempt per window, nobody queues, writes are a named 503 ───────────────

    [Fact]
    public async Task After_a_failed_index_creation_concurrent_callers_never_queue_and_only_one_retries_per_window()
    {
        var database = host.Database.Client.GetDatabase("diten_mdm_itest_fix4_index_window");
        var attempts = 0;
        void Failing()
        {
            Interlocked.Increment(ref attempts);
            Thread.Sleep(600); // a server-selection timeout, shortened
            throw new TimeoutException("simulated: the server could not be reached");
        }

        var start = DateTimeOffset.UtcNow;
        var clock = IndexOnce.Now;
        IndexOnce.Now = () => start;
        try
        {
            Assert.False(IndexOnce.Ensure(database, "c", Failing));
            Assert.Equal(1, attempts);

            // Inside the window: ten concurrent callers, no DDL, nobody waits.
            var inside = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Task.Run(() => Timed(() => IndexOnce.Ensure(database, "c", Failing)))));
            Assert.All(inside, x => Assert.False(x.Ready));
            Assert.All(inside, x => Assert.True(x.Elapsed < TimeSpan.FromMilliseconds(300), $"waited {x.Elapsed}"));
            Assert.Equal(1, attempts);

            // The window is over: ten concurrent callers — exactly ONE retries; the others answer at once, not after it.
            IndexOnce.Now = () => start + IndexOnce.RetryAfter + TimeSpan.FromSeconds(1);
            var after = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Task.Run(() => Timed(() => IndexOnce.Ensure(database, "c", Failing)))));
            Assert.All(after, x => Assert.False(x.Ready));
            Assert.Equal(2, attempts);
            Assert.True(after.Count(x => x.Elapsed < TimeSpan.FromMilliseconds(300)) >= 9, string.Join(", ", after.Select(x => x.Elapsed)));

            // The next window: no DDL again until it is over.
            IndexOnce.Now = () => start + IndexOnce.RetryAfter + TimeSpan.FromSeconds(5);
            Assert.False(IndexOnce.Ensure(database, "c", Failing));
            Assert.Equal(2, attempts);
        }
        finally
        {
            IndexOnce.Now = clock;
        }
    }

    [Fact]
    public async Task A_brand_write_while_the_indexes_cannot_be_created_is_a_named_503_never_a_500()
    {
        var database = host.Database.Client.GetDatabase("diten_mdm_itest_fix4_index_conflict");
        await database.GetCollection<BsonDocument>("mdm_brands").Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(
            new BsonDocument("BrandName", 1), new CreateIndexOptions { Name = "ux_mdm_brands_tenant_code" }));

        var handler = new CreateBrandHandler(new BrandRepository(database, new FixedTenant(_tenant)));
        var answer = await handler.Handle(new CreateBrandCommand(
            new BrandWriteRequest("F1BRAND", "F1 brand", "active", null, null, null, null, From2025, null, null)), CancellationToken.None);

        Assert.Equal(503, answer.StatusCode);
        Assert.StartsWith("store_unavailable:", Assert.Single(answer.Errors!));
    }

    // ── K1 — two edits from one read adding the same pair: the loser is a version conflict ────────────────────────

    [Fact]
    public async Task Two_concurrent_brand_edits_from_one_read_adding_the_same_pair_lose_as_a_version_conflict()
    {
        var client = host.Client(host.Token(_tenant, _user, Read, Create, Update));
        var id = await CreateBrandAsync(client, Code("K1B"));
        var pair = new { sourceSystem = "SAP", externalId = "K1-SAME", isPrimary = true };

        var answers = await Task.WhenAll(Enumerable.Range(0, 2).Select(i => Send(client, HttpMethod.Put, $"/api/mdm/brands/{id}",
            BrandBody(Code("K1B"), name: $"Edit {i}", expectedVersion: 0, references: [pair]))));

        Assert.Single(answers, x => x.StatusCode == HttpStatusCode.NoContent);
        var loser = Assert.Single(answers, x => x.StatusCode == HttpStatusCode.Conflict);
        Assert.StartsWith("version_conflict:", Envelope.FirstError(await Envelope.ReadAsync(loser)));
    }

    [Fact]
    public async Task Two_concurrent_product_edits_from_one_read_adding_the_same_pair_lose_as_a_version_conflict()
    {
        var client = host.Client(host.Token(_tenant, _user, ProductsRead, ProductsCreate, ProductsUpdate, Read));
        var id = await CreateProductAsync(client, Code("K1P"));
        var pair = new { sourceSystem = "SAP", externalId = "K1P-SAME", isPrimary = true };

        var answers = await Task.WhenAll(Enumerable.Range(0, 2).Select(i => Send(client, HttpMethod.Put, $"/api/mdm/products/{id}",
            ProductBody(Code("K1P"), description: $"Edit {i}", expectedVersion: 0, references: [pair]))));

        Assert.Single(answers, x => x.StatusCode == HttpStatusCode.NoContent);
        var loser = Assert.Single(answers, x => x.StatusCode == HttpStatusCode.Conflict);
        Assert.StartsWith("version_conflict:", Envelope.FirstError(await Envelope.ReadAsync(loser)));
    }

    // ── K2 — the stores' version condition, measured deterministically (CT's surviving R2) ──────────────────────

    [Fact]
    public async Task The_product_store_refuses_an_edit_and_an_archive_on_an_older_version_and_reserves_nothing_for_them()
    {
        var store = new ProductRepository(host.Database, new FixedTenant(_tenant));
        var product = NewProduct(Code("K2P"));
        Assert.Equal(BrandWriteOutcome.Written, await store.CreateWithReservationsAsync(product));

        var first = Copy(product, Ref("K2P-A"));
        Assert.Equal(BrandWriteOutcome.Written, await store.UpdateWithReservationsAsync(first, 0, [Ref("K2P-A")], []));

        // Same read (version 0), after the version moved on: nothing written, nothing reserved.
        var stale = Copy(product, Ref("K2P-B"));
        Assert.Equal(BrandWriteOutcome.Stale, await store.UpdateWithReservationsAsync(stale, 0, [Ref("K2P-B")], []));
        Assert.Equal(["K2P-A"], await HeldAsync(product.Id));
        Assert.Equal(1, (await store.GetByIdAsync(product.Id))!.Version);

        // A removal releases the pair.
        Assert.Equal(BrandWriteOutcome.Written, await store.UpdateWithReservationsAsync(Copy(product), 1, [], [Ref("K2P-A")]));
        Assert.Empty(await HeldAsync(product.Id));

        // An archive on an older version: nothing written; on the current one: archived, nothing held.
        Assert.Equal(BrandWriteOutcome.Written, await store.UpdateWithReservationsAsync(Copy(product, Ref("K2P-C")), 2, [Ref("K2P-C")], []));
        Assert.Equal(BrandWriteOutcome.Stale, await store.ArchiveWithReservationsAsync(Archived(product, Ref("K2P-C")), 1));
        Assert.False((await store.GetByIdAsync(product.Id))!.IsArchived);
        Assert.Equal(BrandWriteOutcome.Written, await store.ArchiveWithReservationsAsync(Archived(product, Ref("K2P-C")), 3));
        Assert.True((await store.GetByIdAsync(product.Id))!.IsArchived);
        Assert.Empty(await HeldAsync(product.Id));
    }

    [Fact]
    public async Task The_brand_store_refuses_an_edit_and_an_archive_on_an_older_version_and_reserves_nothing_for_them()
    {
        var store = new BrandRepository(host.Database, new FixedTenant(_tenant));
        var brand = NewBrand(Code("K2B"));
        Assert.Equal(BrandWriteOutcome.Written, await store.CreateWithHistoryAsync(brand, Entry()));

        var first = NewBrand(Code("K2B"));
        first.Id = brand.Id;
        first.ExternalReferences = [Ref("K2B-A")];
        Assert.Equal(BrandWriteOutcome.Written, await store.UpdateEditableFieldsWithHistoryAsync(first, 0, Entry(), [Ref("K2B-A")], []));

        var stale = NewBrand(Code("K2B"));
        stale.Id = brand.Id;
        stale.ExternalReferences = [Ref("K2B-B")];
        Assert.Equal(BrandWriteOutcome.Stale, await store.UpdateEditableFieldsWithHistoryAsync(stale, 0, Entry(), [Ref("K2B-B")], []));
        Assert.Equal(["K2B-A"], await HeldAsync(brand.Id));

        var archive = NewBrand(Code("K2B"));
        archive.Id = brand.Id;
        archive.BrandStatus = "archived";
        archive.ExternalReferences = [Ref("K2B-A")];
        Assert.Equal(BrandWriteOutcome.Stale, await store.ArchiveWithHistoryAsync(archive, 0, Entry()));
        Assert.False((await store.GetByIdAsync(brand.Id))!.IsArchived);
        Assert.Equal(BrandWriteOutcome.Written, await store.ArchiveWithHistoryAsync(archive, 1, Entry()));
        Assert.Empty(await HeldAsync(brand.Id));
    }

    // ── helpers ────────────────────────────────────────────────────────────────────────────────────────────────

    private static (bool Ready, TimeSpan Elapsed) Timed(Func<bool> call)
    {
        var watch = Stopwatch.StartNew();
        var ready = call();
        return (ready, watch.Elapsed);
    }

    private string Code(string stem) => $"{stem}-{_tenant.ToString("N")[..6]}".ToUpperInvariant();

    private static BrandProductExternalReference Ref(string id) => new() { SourceSystem = "SAP", ExternalId = id, IsPrimary = true };

    private Product NewProduct(string code) => new()
    {
        ProductCode = code,
        ProductName = $"Product {code}",
        ProductStatus = "active",
        EffectiveFrom = From2025
    };

    private static Product Copy(Product source, params BrandProductExternalReference[] references) => new()
    {
        Id = source.Id,
        ProductCode = source.ProductCode,
        ProductName = source.ProductName,
        ProductStatus = source.ProductStatus,
        EffectiveFrom = source.EffectiveFrom,
        CreatedAt = source.CreatedAt,
        ExternalReferences = references.ToList()
    };

    private static Product Archived(Product source, params BrandProductExternalReference[] references)
    {
        var copy = Copy(source, references);
        copy.ProductStatus = "archived";
        copy.ArchivedAt = DateTimeOffset.UtcNow;
        return copy;
    }

    private static Brand NewBrand(string code) => new()
    {
        BrandCode = code,
        BrandName = $"Brand {code}",
        BrandStatus = "active",
        EffectiveFrom = From2025
    };

    private static BrandHistoryEntry Entry() => new()
    {
        EventType = BrandHistoryEntry.EventUpdated,
        CorrelationId = Guid.NewGuid().ToString("D"),
        OccurredAtUtc = DateTimeOffset.UtcNow
    };

    private async Task<List<string>> HeldAsync(Guid recordId)
        => (await host.Database.GetCollection<ExternalReferenceReservation>(ExternalReferenceReservations.CollectionName)
                .Find(x => x.TenantId == _tenant && x.RecordId == recordId).ToListAsync())
            .Select(x => x.ExternalIdKey)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

    private static object BrandBody(string code, string? name = null, int? expectedVersion = null, object[]? references = null) => new
    {
        brandCode = code,
        brandName = name ?? $"Brand {code.Split('-')[0]}",
        brandStatus = "active",
        effectiveFrom = From2025,
        externalReferences = references,
        expectedVersion
    };

    private static object ProductBody(string code, string? description = null, int? expectedVersion = null, object[]? references = null) => new
    {
        productCode = code,
        productName = $"Product {code}",
        productStatus = "active",
        description,
        effectiveFrom = From2025,
        externalReferences = references,
        expectedVersion
    };

    private static Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string path, object? body)
        => client.SendAsync(new HttpRequestMessage(method, path) { Content = body is null ? null : Envelope.Json(body) });

    private static async Task<Guid> CreateBrandAsync(HttpClient client, string code)
    {
        var response = await Send(client, HttpMethod.Post, "/api/mdm/brands", BrandBody(code));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await Envelope.ReadAsync(response)).GetProperty("data").GetGuid();
    }

    private static async Task<Guid> CreateProductAsync(HttpClient client, string code)
    {
        var response = await Send(client, HttpMethod.Post, "/api/mdm/products", ProductBody(code));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await Envelope.ReadAsync(response)).GetProperty("data").GetGuid();
    }

    private sealed class FixedTenant(Guid tenantId) : ITenantContext
    {
        public Guid TenantId { get; private set; } = tenantId;
        public bool IsResolved => true;
        public void SetTenant(Guid tenantId) => TenantId = tenantId;
    }
}
