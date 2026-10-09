using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.ValueObjects;
using Diten.MdmService.Infrastructure.ReferenceData;
using Diten.MdmService.Persistence.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// WP-PLT-ITEM-LISTS-01 / -02 end to end at MDM: an item is created over HTTP through the REAL item-list client; Platform's
/// side is the contract answer its own handler test produces (the shared fixture), served on the wire — the caller's
/// token and the resolver credential are checked on the way.
/// </summary>
[Collection(MdmHttpCollection.Name)]
public sealed class ItemListsHttpTests(MdmHttpHost host) : IAsyncLifetime
{
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _user = Guid.NewGuid();

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        host.ItemReferences.Reset();
        return Task.CompletedTask;
    }

    // WP-PLT-ITEM-LISTS-02: a vitamin measured in IU — 100 000 IU per gram as an EXPLICIT conversion (IU ↔ mass is
    // substance-specific, never derived). A new item takes ITEM-UNIVERSAL-V2's item-uom proof (version 2).
    [Fact]
    public async Task An_item_in_iu_is_created_through_the_real_client_on_platforms_contract_answer()
    {
        var platform = new ContractEndpoint();
        host.ItemReferences.Override = RealClient(platform);
        var client = host.Client(host.Token(_tenant, _user, "mdm.items.create"));

        var response = await client.PostAsJsonAsync("api/mdm/items", new
        {
            idempotencyKey = "ITEM-" + Guid.NewGuid().ToString("N"),
            name = "Cholecalciferol concentrate 100 000 IU/g through Platform",
            materialType = "RAW_MATERIAL",
            baseUomCode = "KGM",
            uomConversions = new[]
            {
                new { fromUom = "KGM", toUom = "GRM", numerator = "1000", denominator = "1" },
                new { fromUom = "GRM", toUom = "IU", numerator = "100000", denominator = "1" }
            },
            storageConditionCodes = new[] { "AMBIENT_15_25", "PROTECT_FROM_LIGHT" },
            lotControlled = true,
            stockable = true,
            shelfLifeDays = 730
        });

        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        Assert.Equal(1, platform.Calls);
        Assert.Equal("Bearer", platform.AuthorizationScheme);
        Assert.Equal("resolver-id", platform.CredentialId);
        Assert.Contains(("item-uom", "IU"), platform.Asked);
        var id = (await Envelope.ReadAsync(response)).GetProperty("data").GetGuid();
        var item = await host.Database.GetCollection<Item>(ItemRepository.CollectionName).Find(x => x.Id == id).SingleAsync();
        var (uomId, uomNumber) = ContractVersion(JsonNode.Parse(File.ReadAllText(ContractEndpoint.ContractFixturePath()))!, "item-uom");
        Assert.Equal(uomId, item.BaseUomSelection!.CatalogVersionId);
        Assert.Equal(uomNumber, item.BaseUomSelection.CatalogVersionNumber);
        Assert.Equal(2, uomNumber);
        Assert.Contains(item.UomConversions, c => c.FromUom == "GRM" && c.ToUom == "IU" && c.Numerator == "100000");
        Assert.Equal("TEMPERATURE", item.StorageConditionKinds!["AMBIENT_15_25"]);
    }

    // WP-PLT-ITEM-LISTS-02: an item saved under ITEM-UNIVERSAL-V1 stays trusted after V2. Its stored proofs are V1's
    // (item-uom version 1, the id the contract names as previous); an edit that adds GRM and IU asks Platform for those two
    // only — the V2 answer — and the base unit keeps its V1 proof. One request holds an old proof and new values, and the
    // mixed-version rule (per answer) is not touched: the answer itself is all V2.
    [Fact]
    public async Task An_item_saved_under_v1_keeps_its_v1_proof_and_takes_iu_from_v2()
    {
        var client = host.Client(host.Token(_tenant, _user, "mdm.items.create", "mdm.items.update", "mdm.items.read"));
        var created = await client.PostAsJsonAsync("api/mdm/items", new
        {
            idempotencyKey = "ITEM-" + Guid.NewGuid().ToString("N"), name = "Retinol palmitate saved under V1",
            materialType = "RAW_MATERIAL", baseUomCode = "KGM", storageConditionCodes = new[] { "AMBIENT_15_25" },
            lotControlled = true, stockable = true, shelfLifeDays = 730
        });
        Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var id = (await Envelope.ReadAsync(created)).GetProperty("data").GetGuid();
        var items = host.Database.GetCollection<Item>(ItemRepository.CollectionName);
        // The store as V1 left it: every proof carries the V1 version of its list.
        var fixture = JsonNode.Parse(File.ReadAllText(ContractEndpoint.ContractFixturePath()))!;
        var v1 = fixture["previous_versions"]!["item-uom"]![0]!;
        var v1Uom = Guid.Parse((string)v1["catalog_version_id"]!);
        // FIX1: storage-condition and material-type did not change in V2 — their contract answer IS their V1 proof.
        var (storageId, storageNumber) = ContractVersion(fixture, "storage-condition");
        var (materialId, materialNumber) = ContractVersion(fixture, "material-type");
        var resolvedUnderV1 = new DateTimeOffset(2026, 10, 8, 9, 0, 0, TimeSpan.Zero);
        await items.UpdateOneAsync(x => x.Id == id, Builders<Item>.Update
            .Set(x => x.BaseUomSelection, Proof("item-uom", "KGM", v1Uom, (int)v1["catalog_version_number"]!, resolvedUnderV1))
            .Set(x => x.MaterialTypeSelection, Proof("material-type", "RAW_MATERIAL", materialId, materialNumber, resolvedUnderV1))
            .Set(x => x.StorageConditionSelections, new List<ReferenceCatalogSelection> { Proof("storage-condition",
                "AMBIENT_15_25", storageId, storageNumber, resolvedUnderV1) }));
        var version = (await items.Find(x => x.Id == id).SingleAsync()).Version;

        var platform = new ContractEndpoint { AnswerAnySubset = true };
        host.ItemReferences.Override = RealClient(platform);
        var updated = await client.PutAsJsonAsync($"api/mdm/items/{id}", new
        {
            expectedVersion = version, name = "Retinol palmitate saved under V1", materialType = "RAW_MATERIAL",
            baseUomCode = "KGM",
            uomConversions = new[]
            {
                new { fromUom = "KGM", toUom = "GRM", numerator = "1000", denominator = "1" },
                new { fromUom = "GRM", toUom = "IU", numerator = "1700000", denominator = "1" }
            },
            storageConditionCodes = new[] { "AMBIENT_15_25" }, lotControlled = true, stockable = true, shelfLifeDays = 730
        });

        Assert.True(updated.StatusCode == HttpStatusCode.OK, await updated.Content.ReadAsStringAsync());
        Assert.Equal(1, platform.Calls);
        Assert.Equal(new[] { ("item-uom", "GRM"), ("item-uom", "IU") }, platform.Asked.Order());
        var item = await items.Find(x => x.Id == id).SingleAsync();
        Assert.Equal(v1Uom, item.BaseUomSelection!.CatalogVersionId);
        Assert.Equal(1, item.BaseUomSelection.CatalogVersionNumber);
        Assert.Equal(resolvedUnderV1, item.BaseUomSelection.ResolvedAtUtc);
        Assert.Equal(storageId, Assert.Single(item.StorageConditionSelections).CatalogVersionId);
        Assert.Contains(item.UomConversions, c => c.FromUom == "GRM" && c.ToUom == "IU");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"api/mdm/items/{id}")).StatusCode);
    }

    private PlatformItemReferenceResolverClient RealClient(ContractEndpoint platform) => new(new HttpClient(platform),
        host.Factory.Services.GetRequiredService<IHttpContextAccessor>(),
        Options.Create(new VerifiedGskuResolverOptions
        {
            PlatformBaseAddress = new Uri("https://platform.test/"), Timeout = TimeSpan.FromSeconds(2),
            CredentialIdentifier = "resolver-id", CredentialSecret = "resolver-secret"
        }), Microsoft.Extensions.Logging.Abstractions.NullLogger<PlatformItemReferenceResolverClient>.Instance);

    // The version a set's selections carry in the contract answer (one version per set — the client's own rule).
    private static (Guid Id, int Number) ContractVersion(JsonNode fixture, string set)
    {
        var selection = fixture["response"]!["data"]!["selections"]!.AsArray().First(x => (string)x!["set_code"]! == set)!;
        return (Guid.Parse((string)selection["catalog_version_id"]!), (int)selection["catalog_version_number"]!);
    }

    private static ReferenceCatalogSelection Proof(string set, string value, Guid versionId, int versionNumber,
        DateTimeOffset resolvedAt) => new()
    {
        SetCode = set, ValueCode = value, CatalogVersionId = versionId, CatalogVersionNumber = versionNumber,
        ResolutionMode = ReferenceCatalogResolutionMode.Latest, ResolvedAtUtc = resolvedAt
    };

    // FIX1 R3: the start-up warning is registered by the real Program (the host removes hosted services but records them).
    [Fact]
    public void The_start_up_warning_is_registered()
    {
        Assert.Contains(typeof(Diten.MdmService.Api.ReferenceData.ItemReferenceProviderStartupWarning), host.RegisteredHostedServices);
    }

    // CT decision A: without configuration an item create fails closed with the named cause — never "no list, pass".
    [Fact]
    public async Task Without_configuration_an_item_create_is_503_with_the_named_cause()
    {
        var platform = new ContractEndpoint();
        host.ItemReferences.Override = new PlatformItemReferenceResolverClient(new HttpClient(platform),
            host.Factory.Services.GetRequiredService<IHttpContextAccessor>(), Options.Create(new VerifiedGskuResolverOptions()),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<PlatformItemReferenceResolverClient>.Instance);
        var client = host.Client(host.Token(_tenant, _user, "mdm.items.create"));

        var response = await client.PostAsJsonAsync("api/mdm/items", new
        {
            idempotencyKey = "ITEM-" + Guid.NewGuid().ToString("N"), name = "Unconfigured", materialType = "RAW_MATERIAL",
            baseUomCode = "KGM", storageConditionCodes = new[] { "AMBIENT_15_25" }, lotControlled = true, stockable = true,
            shelfLifeDays = 730
        });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var errors = (await Envelope.ReadAsync(response)).GetProperty("errors").EnumerateArray().Select(x => x.GetString()).ToArray();
        Assert.Equal(new[] { "ITEM_REFERENCE_LIST_UNAVAILABLE", PlatformItemReferenceResolverClient.ConfigurationInvalid }, errors);
        Assert.Equal(0, platform.Calls);
        Assert.False(await host.Database.GetCollection<Item>(ItemRepository.CollectionName).Find(x => x.TenantId == _tenant).AnyAsync());
    }

    // Platform's handler, as recorded by its contract test: the answer for exactly this request. AnswerAnySubset: the
    // same recorded selections for any subset of the contract request (an edit asks only its new codes).
    private sealed class ContractEndpoint : HttpMessageHandler
    {
        public bool AnswerAnySubset { get; init; }
        public IReadOnlyList<(string, string)> Asked { get; private set; } = [];
        public int Calls { get; private set; }
        public string? AuthorizationScheme { get; private set; }
        public string? CredentialId { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++;
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            CredentialId = request.Headers.TryGetValues(PlatformItemReferenceResolverClient.CredentialIdHeader, out var id)
                ? id.Single() : null;
            var fixture = JsonNode.Parse(File.ReadAllText(ContractFixturePath()))!;
            var asked = JsonNode.Parse(await request.Content!.ReadAsStringAsync(token))!["selections"]!.AsArray()
                .Select(x => ((string)x!["set_code"]!, (string)x["value_code"]!)).ToHashSet();
            Asked = asked.ToList();
            var contract = fixture["request"]!["selections"]!.AsArray()
                .Select(x => ((string)x!["set_code"]!, (string)x["value_code"]!)).ToHashSet();
            var matches = AnswerAnySubset ? asked.Count > 0 && asked.IsSubsetOf(contract) : asked.SetEquals(contract);
            if (!matches || request.RequestUri!.AbsolutePath != "/" + PlatformItemReferenceResolverClient.RelativePath)
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            var response = fixture["response"]!.DeepClone();
            var selections = response["data"]!["selections"]!.AsArray();
            foreach (var selection in selections.Where(x => !asked.Contains(((string)x!["set_code"]!, (string)x["value_code"]!))).ToList())
                selections.Remove(selection);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(response) };
        }

        internal static string ContractFixturePath()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "services")))
                directory = directory.Parent;
            return Path.Combine(directory!.FullName, "services", "Diten.Platform", "tests", "Diten.Platform.Application.Tests",
                "BusinessReferenceData", "Fixtures", "item-universal-resolve.contract.json");
        }
    }
}
