using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Infrastructure.ReferenceData;
using Diten.MdmService.Persistence.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// WP-PLT-ITEM-LISTS-01 end to end at MDM: an item is created over HTTP through the REAL item-list client; Platform's
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

    [Fact]
    public async Task An_item_is_created_through_the_real_client_on_platforms_contract_answer()
    {
        var platform = new ContractEndpoint();
        host.ItemReferences.Override = new PlatformItemReferenceResolverClient(new HttpClient(platform),
            host.Factory.Services.GetRequiredService<IHttpContextAccessor>(),
            Options.Create(new VerifiedGskuResolverOptions
            {
                PlatformBaseAddress = new Uri("http://platform.test/"), Timeout = TimeSpan.FromSeconds(2),
                CredentialIdentifier = "resolver-id", CredentialSecret = "resolver-secret"
            }), Microsoft.Extensions.Logging.Abstractions.NullLogger<PlatformItemReferenceResolverClient>.Instance);
        var client = host.Client(host.Token(_tenant, _user, "mdm.items.create"));

        var response = await client.PostAsJsonAsync("api/mdm/items", new
        {
            idempotencyKey = "ITEM-" + Guid.NewGuid().ToString("N"),
            name = "Lactose through Platform",
            materialType = "RAW_MATERIAL",
            baseUomCode = "KGM",
            uomConversions = new[] { new { fromUom = "KGM", toUom = "GRM", numerator = "1000", denominator = "1" } },
            storageConditionCodes = new[] { "AMBIENT_15_25", "PROTECT_FROM_LIGHT" },
            lotControlled = true,
            stockable = true,
            shelfLifeDays = 730
        });

        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        Assert.Equal(1, platform.Calls);
        Assert.Equal("Bearer", platform.AuthorizationScheme);
        Assert.Equal("resolver-id", platform.CredentialId);
        var id = (await Envelope.ReadAsync(response)).GetProperty("data").GetGuid();
        var item = await host.Database.GetCollection<Item>(ItemRepository.CollectionName).Find(x => x.Id == id).SingleAsync();
        Assert.Equal(Guid.Parse("c3f1d0a4-6b2e-4f0a-9d71-2e5b7a1c0001"), item.BaseUomSelection!.CatalogVersionId);
        Assert.Equal("TEMPERATURE", item.StorageConditionKinds!["AMBIENT_15_25"]);
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

    // Platform's handler, as recorded by its contract test: the answer for exactly this request.
    private sealed class ContractEndpoint : HttpMessageHandler
    {
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
            var contract = fixture["request"]!["selections"]!.AsArray()
                .Select(x => ((string)x!["set_code"]!, (string)x["value_code"]!)).ToHashSet();
            return asked.SetEquals(contract) && request.RequestUri!.AbsolutePath == "/" + PlatformItemReferenceResolverClient.RelativePath
                ? new HttpResponseMessage(HttpStatusCode.OK)
                    { Content = JsonContent.Create(fixture["response"]) }
                : new HttpResponseMessage(HttpStatusCode.InternalServerError);
        }

        private static string ContractFixturePath()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "services")))
                directory = directory.Parent;
            return Path.Combine(directory!.FullName, "services", "Diten.Platform", "tests", "Diten.Platform.Application.Tests",
                "BusinessReferenceData", "Fixtures", "item-universal-resolve.contract.json");
        }
    }
}
