using System.Net;
using System.Text;
using Diten.InventoryService.Application.Common.Upstream;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Infrastructure.Upstream;
using Xunit;

namespace Diten.InventoryService.Tests.Api;

/// <summary>
/// WP-MVP1-FIX-LIVE2 (owner step MOD-0173, upstream reads): the two DISPLAY reads the evidence packs use — the SKU code
/// (<c>SkuRef.canonicalCode</c>) and the legal entity's code + name (MDM <c>GET /api/legal-entities/{id}</c>). Every answer
/// that is not a clean 200 of the same id is null ("okunamadı"); a name is never invented, and the membership check
/// (<see cref="LegalEntityMasterHttpClient.ValidateAsync"/>) is untouched.
/// </summary>
public sealed class UpstreamDisplayReadsTests
{
    private static readonly Guid Entity = Guid.Parse("7a1d2c3b-4e5f-4a6b-8c7d-9e0f1a2b3c4d");

    [Fact]
    public async Task The_sku_code_is_read_from_canonicalCode()
    {
        var sku = Guid.NewGuid();
        var json = $$"""{ "skuId": "{{sku}}", "skuLevel": "LSKU", "itemId": "{{Guid.NewGuid()}}", "canonicalCode": "LS-0042" }""";
        var read = await new ProductMasterHttpClient(Http(HttpStatusCode.OK, json, "http://product-master.test/")).GetSkuAsync(sku, SkuLevel.Lsku);

        Assert.Equal("LS-0042", read!.CanonicalCode);
    }

    [Fact]
    public async Task A_sku_without_canonicalCode_has_no_code_and_nothing_is_invented()
    {
        var sku = Guid.NewGuid();
        var json = $$"""{ "skuId": "{{sku}}", "skuLevel": "LSKU", "itemId": "{{Guid.NewGuid()}}", "lotControlled": true }""";
        var read = await new ProductMasterHttpClient(Http(HttpStatusCode.OK, json, "http://product-master.test/")).GetSkuAsync(sku, SkuLevel.Lsku);

        Assert.Null(read!.CanonicalCode);
        Assert.True(read.LotControlled);
    }

    [Fact]
    public async Task The_legal_entity_name_is_the_display_name_with_its_code_on_the_detail_path()
    {
        var handler = new Answer(HttpStatusCode.OK, Detail(Entity, "GM-EU", "Grand Medical Europe B.V.", "Grand Medical EU"));
        var name = await new LegalEntityMasterHttpClient(new HttpClient(handler) { BaseAddress = new Uri("http://gateway.test/api/legal-entities/") })
            .GetLegalEntityNameAsync(Entity);

        Assert.Equal(new LegalEntityName(Entity, "GM-EU", "Grand Medical EU"), name);
        Assert.Equal($"http://gateway.test/api/legal-entities/{Entity:D}", handler.Asked!.RequestUri!.ToString());
        Assert.Equal(HttpMethod.Get, handler.Asked.Method);
    }

    [Fact]
    public async Task Without_a_display_name_the_legal_name_is_used()
    {
        var name = await Client(HttpStatusCode.OK, Detail(Entity, "GM-EU", "Grand Medical Europe B.V.", null)).GetLegalEntityNameAsync(Entity);

        Assert.Equal("Grand Medical Europe B.V.", name!.Name);
    }

    public static TheoryData<HttpStatusCode, string> Unreadable => new()
    {
        { HttpStatusCode.NotFound, "{}" },
        { HttpStatusCode.Forbidden, "{}" },
        { HttpStatusCode.Unauthorized, "{}" },
        { HttpStatusCode.InternalServerError, "{}" },
        { HttpStatusCode.ServiceUnavailable, "{}" },
        { HttpStatusCode.OK, "not json" },
        { HttpStatusCode.OK, """{ "data": null }""" },
        { HttpStatusCode.OK, Detail(Guid.NewGuid(), "OTHER", "Another company", null) },
        { HttpStatusCode.OK, Detail(Entity, "", "Grand Medical Europe B.V.", null) },
        { HttpStatusCode.OK, Detail(Entity, "GM-EU", "", null) }
    };

    [Theory]
    [MemberData(nameof(Unreadable))]
    public async Task Every_answer_that_is_not_a_clean_name_is_null_and_never_invented(HttpStatusCode status, string body)
    {
        Assert.Null(await Client(status, body).GetLegalEntityNameAsync(Entity));
    }

    [Fact]
    public async Task An_unreachable_master_is_null_not_an_exception()
    {
        var client = new LegalEntityMasterHttpClient(new HttpClient(new Refusing()) { BaseAddress = new Uri("http://gateway.test/api/legal-entities/") });

        Assert.Null(await client.GetLegalEntityNameAsync(Entity));
    }

    [Fact]
    public async Task The_membership_check_is_unchanged_and_still_fails_closed()
    {
        var lookup = $$"""{ "data": { "legalEntityId": "{{Entity}}", "referenceable": true } }""";
        Assert.Equal(LegalEntityMembership.Active, await Client(HttpStatusCode.OK, lookup).ValidateAsync(Entity));
        Assert.Equal(LegalEntityMembership.NotReferenceable, await Client(HttpStatusCode.NotFound, "{}").ValidateAsync(Entity));
        Assert.Equal(LegalEntityMembership.CallerRefused, await Client(HttpStatusCode.Forbidden, "{}").ValidateAsync(Entity));
        await Assert.ThrowsAsync<LegalEntityMasterUnavailableException>(() => Client(HttpStatusCode.InternalServerError, "{}").ValidateAsync(Entity));
    }

    private static string Detail(Guid id, string code, string legalName, string? displayName)
        => $$"""{ "data": { "legalEntityId": "{{id}}", "code": "{{code}}", "legalName": "{{legalName}}", "displayName": {{(displayName is null ? "null" : $"\"{displayName}\"")}} } }""";

    private static LegalEntityMasterHttpClient Client(HttpStatusCode status, string body)
        => new(Http(status, body, "http://gateway.test/api/legal-entities/"));

    private static HttpClient Http(HttpStatusCode status, string body, string baseAddress)
        => new(new Answer(status, body)) { BaseAddress = new Uri(baseAddress) };

    private sealed class Answer(HttpStatusCode status, string json) : HttpMessageHandler
    {
        public HttpRequestMessage? Asked { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Asked = request;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }
    }

    private sealed class Refusing : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("connection refused");
    }
}
