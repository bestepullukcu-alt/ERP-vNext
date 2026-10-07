using System.Net;
using System.Net.Http.Json;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.StockBehaviour;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// WP-MDM-ITEM-MASTER (MOD-0290-FU04) S3 over HTTP, on the host's throwaway mongod: each of the four stock-behaviour
/// doors asks its own (for now temporary) key — a caller without it is 403, with it the record moves; the doors are
/// audited by path b; a code edit without the MOD-0048 lists is the named 503 of the production registration.
/// </summary>
[Collection(MdmHttpCollection.Name)]
public sealed class StockBehaviourHttpTests(MdmHttpHost host)
{
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _user = Guid.NewGuid();

    public static TheoryData<string> Doors => new() { "gsku", "lsku", "finished-good", "gtin" };

    [Theory]
    [MemberData(nameof(Doors))]
    public async Task A_caller_without_the_doors_own_key_is_403_and_the_record_does_not_move(string door)
    {
        var records = await SeedAsync();
        var others = new[] { StockBehaviourPermissions.EditGsku, StockBehaviourPermissions.EditLsku,
                StockBehaviourPermissions.EditFinishedGood }
            .Where(key => key != KeyOf(door)).ToArray();

        var response = await SendAsync(door, records, host.Token(_tenant, _user, others));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(VersionOf(door, records), await StoredVersionAsync(door, records));
    }

    [Theory]
    [MemberData(nameof(Doors))]
    public async Task A_caller_with_the_doors_own_key_moves_the_record_and_the_command_is_audited(string door)
    {
        var records = await SeedAsync();

        var response = await SendAsync(door, records, host.Token(_tenant, _user, KeyOf(door)));

        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.Equal(VersionOf(door, records) + 1, await StoredVersionAsync(door, records));
        var recordId = door == "gsku" ? records.Gsku.Id : door == "lsku" ? records.Lsku.Id : records.FinishedGood.Id;
        Assert.Contains(host.Audit.Forwarded, forwarded => forwarded.EntityId == recordId
            && forwarded.RequestType == CommandOf(door) && forwarded.Outcome == 1);
    }

    [Fact]
    public async Task Without_a_token_the_door_is_401()
    {
        var records = await SeedAsync();
        var response = await SendAsync("gsku", records, null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task An_unknown_field_or_a_missing_idempotency_key_is_400()
    {
        var records = await SeedAsync();
        var client = host.Client(host.Token(_tenant, _user, StockBehaviourPermissions.EditGsku));
        client.DefaultRequestHeaders.Add("X-Tenant-Id", _tenant.ToString("D"));

        var unknown = await Put(client, $"api/gskus/{records.Gsku.Id:D}/stock-behaviour",
            new { expectedVersion = records.Gsku.Version, lotControlled = true, gtin = "00012345678905" }, Guid.NewGuid());
        var noKey = await Put(client, $"api/gskus/{records.Gsku.Id:D}/stock-behaviour",
            new { expectedVersion = records.Gsku.Version, lotControlled = true }, null);

        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noKey.StatusCode);
    }

    [Fact]
    public async Task A_code_edit_without_the_item_lists_is_the_named_503_of_the_production_registration()
    {
        var records = await SeedAsync();
        var client = host.Client(host.Token(_tenant, _user, StockBehaviourPermissions.EditGsku));
        client.DefaultRequestHeaders.Add("X-Tenant-Id", _tenant.ToString("D"));

        var response = await Put(client, $"api/gskus/{records.Gsku.Id:D}/stock-behaviour",
            new { expectedVersion = records.Gsku.Version, baseUomCode = "C62" }, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(StockBehaviourErrors.ReferenceListUnavailable, Envelope.FirstError(await Envelope.ReadAsync(response)));
    }

    private static string KeyOf(string door) => door switch
    {
        "gsku" => StockBehaviourPermissions.EditGsku,
        "lsku" => StockBehaviourPermissions.EditLsku,
        _ => StockBehaviourPermissions.EditFinishedGood
    };

    private static string CommandOf(string door) => door switch
    {
        "gsku" => nameof(SetGskuStockBehaviourCommand),
        "lsku" => nameof(SetLskuStockBehaviourCommand),
        "finished-good" => nameof(SetFinishedGoodStockBehaviourCommand),
        _ => nameof(AssignFinishedGoodGtinCommand)
    };

    private static int VersionOf(string door, Records records) => door switch
    {
        "gsku" => records.Gsku.Version,
        "lsku" => records.Lsku.Version,
        _ => records.FinishedGood.Version
    };

    private async Task<int> StoredVersionAsync(string door, Records records) => door switch
    {
        "gsku" => (await host.Database.GetCollection<Gsku>("mdm_gskus").Find(x => x.Id == records.Gsku.Id).SingleAsync()).Version,
        "lsku" => (await host.Database.GetCollection<Lsku>("mdm_lskus").Find(x => x.Id == records.Lsku.Id).SingleAsync()).Version,
        _ => (await host.Database.GetCollection<FinishedGood>("mdm_finished_goods").Find(x => x.Id == records.FinishedGood.Id).SingleAsync()).Version
    };

    private Task<HttpResponseMessage> SendAsync(string door, Records records, string? token)
    {
        var client = host.Client(token);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", _tenant.ToString("D"));
        return door switch
        {
            "gsku" => Put(client, $"api/gskus/{records.Gsku.Id:D}/stock-behaviour",
                new { expectedVersion = records.Gsku.Version, lotControlled = true }, Guid.NewGuid()),
            "lsku" => Put(client, $"api/lskus/{records.Lsku.Id:D}/stock-behaviour",
                new { expectedVersion = records.Lsku.Version, serialControlled = true }, Guid.NewGuid()),
            "finished-good" => Put(client, $"api/finished-goods/{records.FinishedGood.Id:D}/stock-behaviour",
                new { expectedVersion = records.FinishedGood.Version, lotControlled = true, serialControlled = true }, Guid.NewGuid()),
            _ => Put(client, $"api/finished-goods/{records.FinishedGood.Id:D}/gtin",
                new { expectedVersion = records.FinishedGood.Version, gtin = UniqueGtin() }, Guid.NewGuid())
        };
    }

    private static Task<HttpResponseMessage> Put(HttpClient client, string path, object body, Guid? idempotencyKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, path) { Content = JsonContent.Create(body) };
        if (idempotencyKey is { } key) request.Headers.Add("Idempotency-Key", key.ToString("D"));
        return client.SendAsync(request);
    }

    // A fresh valid GTIN-14 per call (the tenant is fresh too; the index is per tenant).
    private static string UniqueGtin()
    {
        var body = string.Concat(Guid.NewGuid().ToString("N").Where(char.IsAsciiDigit).Take(13)).PadLeft(13, '7');
        var sum = 0;
        for (var i = 0; i < 13; i++) sum += (body[12 - i] - '0') * (i % 2 == 0 ? 3 : 1);
        return body + (char)('0' + (10 - sum % 10) % 10);
    }

    private sealed record Records(Gsku Gsku, Lsku Lsku, FinishedGood FinishedGood);

    private async Task<Records> SeedAsync()
    {
        var database = host.Database;
        var product = new GlobalProduct
        {
            Id = Guid.NewGuid(), TenantId = _tenant, CanonicalCode = "GP-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
            GlobalProductName = "Stock Door", GlobalProductNameNormalized = "STOCK DOOR " + Guid.NewGuid().ToString("N"),
            CodeReservationId = Guid.NewGuid(), LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved
        };
        var revision = new ProductDefinitionRevision
        {
            Id = Guid.NewGuid(), TenantId = _tenant, GlobalProductId = product.Id, RevisionIdentifier = "REV-001",
            CreationCommandId = "REV:" + Guid.NewGuid().ToString("N"), LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved
        };
        var gsku = new Gsku
        {
            Id = Guid.NewGuid(), TenantId = _tenant, ProductDefinitionRevisionId = revision.Id,
            CanonicalCode = "GS-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(), CodeReservationId = Guid.NewGuid(),
            CreationCommandId = Guid.NewGuid().ToString("N"), PackApplicabilityCode = "SCALAR_QUANTITY_APPLIES",
            PackQuantity = 30m, PackUomCode = "C62", LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved, Version = 2
        };
        var lsku = new Lsku
        {
            Id = Guid.NewGuid(), TenantId = _tenant, GskuId = gsku.Id,
            CanonicalCode = "LS-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(), CodeReservationId = Guid.NewGuid(),
            CreationCommandId = Guid.NewGuid().ToString("N"), MarketCode = "M" + Guid.NewGuid().ToString("N")[..6],
            LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved, Version = 1
        };
        var finishedGood = new FinishedGood
        {
            Id = Guid.NewGuid(), TenantId = _tenant, GskuId = gsku.Id,
            CanonicalCode = "FG-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(), CodeReservationId = Guid.NewGuid(),
            CreationCommandId = Guid.NewGuid().ToString("N"), LifecycleStatus = ProductIdentityLifecycleStatus.Draft, Version = 1
        };
        await database.GetCollection<GlobalProduct>("mdm_global_products").InsertOneAsync(product);
        await database.GetCollection<ProductDefinitionRevision>("mdm_product_definition_revisions").InsertOneAsync(revision);
        await database.GetCollection<Gsku>("mdm_gskus").InsertOneAsync(gsku);
        await database.GetCollection<Lsku>("mdm_lskus").InsertOneAsync(lsku);
        await database.GetCollection<FinishedGood>("mdm_finished_goods").InsertOneAsync(finishedGood);
        return new(gsku, lsku, finishedGood);
    }
}
