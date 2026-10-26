using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.Web.Controllers;
using Diten.Web.Views.MasterDataManagement.GlobalProducts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// MOD-0290-FU05 slice 3 — the Global Product screen's proxy for the brand. The real controller actions are called with
/// MDM as a recording handler: the form allow-lists take BrandId (and refuse its code / name and a malformed id), MDM
/// always receives the brand field, the list query passes only a GUID brandId, brand-options and {id}/brand reach MDM's
/// own routes, and MDM's three brand refusals reach the reader as this screen's sentences — never as MDM's code.
/// </summary>
public sealed class GlobalProductBrandProxyTests
{
    private const string Gateway = "https://gateway-fixture.invalid";
    private const string Read = "mdm.global-products.read";
    private const string Create = "mdm.global-products.create";
    private const string Update = "mdm.global-products.update";
    private const string UpdateBrand = "mdm.global-products.update-brand";
    private static readonly Guid Product = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid BrandA = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid BrandB = Guid.Parse("44444444-4444-4444-4444-444444444444");

    // ── create ────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_accepts_BrandId_in_its_allow_list_and_hands_it_to_MDM()
    {
        var wire = CreateWire();

        var result = await Controller(wire, [Create], Form(("GlobalProductName", "Product"), ("BrandId", BrandA.ToString("D"))))
            .Create(new() { GlobalProductName = "Product" }, CancellationToken.None);

        Assert.Equal(StatusCodes.Status201Created, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
        var draft = wire.Requests.Single(request => request.Url.EndsWith("/api/global-products/drafts"));
        Assert.Equal(BrandA.ToString("D"), JsonDocument.Parse(draft.Body).RootElement.GetProperty("brandId").GetString());
    }

    [Fact]
    public async Task Create_with_an_empty_BrandId_sends_no_brand()
    {
        var wire = CreateWire();

        await Controller(wire, [Create], Form(("GlobalProductName", "Product"), ("BrandId", "")))
            .Create(new() { GlobalProductName = "Product" }, CancellationToken.None);

        var draft = wire.Requests.Single(request => request.Url.EndsWith("/api/global-products/drafts"));
        Assert.Equal(JsonValueKind.Null, JsonDocument.Parse(draft.Body).RootElement.GetProperty("brandId").ValueKind);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("{33333333-3333-3333-3333-333333333333}")]
    public async Task Create_with_a_malformed_BrandId_is_400_and_MDM_is_never_asked(string brandId)
    {
        var wire = CreateWire();

        var result = await Controller(wire, [Create], Form(("GlobalProductName", "Product"), ("BrandId", brandId)))
            .Create(new() { GlobalProductName = "Product" }, CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
        Assert.Empty(wire.Requests);
    }

    [Theory]
    [InlineData("BrandCode")]
    [InlineData("BrandName")]
    public async Task Create_with_a_brand_code_or_name_field_is_400(string field)
    {
        var wire = CreateWire();

        var result = await Controller(wire, [Create],
                Form(("GlobalProductName", "Product"), ("BrandId", BrandA.ToString("D")), (field, "X")))
            .Create(new() { GlobalProductName = "Product" }, CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
        Assert.Empty(wire.Requests);
    }

    [Fact]
    public async Task Create_without_the_BrandId_field_is_400()
    {
        var wire = CreateWire();

        var result = await Controller(wire, [Create], Form(("GlobalProductName", "Product")))
            .Create(new() { GlobalProductName = "Product" }, CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
        Assert.Empty(wire.Requests);
    }

    [Fact]
    public async Task A_brand_refusal_in_the_create_flow_is_this_screens_sentence_not_MDMs_code()
    {
        var wire = new RecordingHandler(request => request.RequestUri!.AbsolutePath.EndsWith("/code-reservations")
            ? (HttpStatusCode.Created, "{\"data\":{\"reservationId\":\"55555555-5555-5555-5555-555555555555\",\"reservedCode\":\"GP-1\",\"version\":1}}")
            : (HttpStatusCode.Conflict, "{\"isSuccessful\":false,\"statusCode\":409,\"errors\":[\"brand_not_linkable: Only an active brand inside its validity window can receive new product links.\"]}"));

        var result = await Controller(wire, [Create], Form(("GlobalProductName", "Product"), ("BrandId", BrandA.ToString("D"))))
            .Create(new() { GlobalProductName = "Product" }, CancellationToken.None);

        var refusal = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, refusal.StatusCode);
        Assert.Equal(["ErrorBrandNotLinkable"], Errors(refusal));
    }

    // ── draft edit ────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateDraft_accepts_BrandId_and_always_writes_the_field_to_MDM()
    {
        var wire = EnvelopeWire("{\"id\":\"11111111-1111-1111-1111-111111111111\",\"lifecycleStatus\":\"Draft\",\"version\":1}");

        var linked = await Controller(wire, [Update],
                Form(("ExpectedVersion", "0"), ("GlobalProductName", "Product"), ("BrandId", BrandA.ToString("D"))))
            .UpdateDraft(Product, 0, "Product", CancellationToken.None);
        var cleared = await Controller(wire, [Update],
                Form(("ExpectedVersion", "0"), ("GlobalProductName", "Product"), ("BrandId", "")))
            .UpdateDraft(Product, 0, "Product", CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, Assert.IsType<ContentResult>(linked).StatusCode);
        Assert.Equal(StatusCodes.Status200OK, Assert.IsType<ContentResult>(cleared).StatusCode);
        Assert.Equal(BrandA.ToString("D"), JsonDocument.Parse(wire.Requests[0].Body).RootElement.GetProperty("brandId").GetString());
        var clearedBody = JsonDocument.Parse(wire.Requests[1].Body).RootElement;
        Assert.True(clearedBody.TryGetProperty("brandId", out var none), "the field is always sent");
        Assert.Equal(JsonValueKind.Null, none.ValueKind);
    }

    [Fact]
    public async Task UpdateDraft_with_a_malformed_BrandId_is_400_and_the_brand_is_not_removed()
    {
        var wire = EnvelopeWire("{}");

        var result = await Controller(wire, [Update],
                Form(("ExpectedVersion", "0"), ("GlobalProductName", "Product"), ("BrandId", "abc")))
            .UpdateDraft(Product, 0, "Product", CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
        Assert.Empty(wire.Requests);
    }

    [Theory]
    [InlineData("BrandCode")]
    [InlineData("BrandName")]
    public async Task UpdateDraft_with_a_brand_code_or_name_field_is_400(string field)
    {
        var wire = EnvelopeWire("{}");

        var result = await Controller(wire, [Update],
                Form(("ExpectedVersion", "0"), ("GlobalProductName", "Product"), ("BrandId", ""), (field, "X")))
            .UpdateDraft(Product, 0, "Product", CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
        Assert.Empty(wire.Requests);
    }

    [Fact]
    public async Task Two_different_brands_make_two_different_idempotency_keys_and_the_same_brand_the_same_key()
    {
        var wire = EnvelopeWire("{\"id\":\"11111111-1111-1111-1111-111111111111\",\"lifecycleStatus\":\"Draft\",\"version\":1}");
        var principal = Principal([Update]);

        foreach (var brand in new[] { BrandA, BrandB, BrandA })
            await Controller(wire, principal, Form(("ExpectedVersion", "0"), ("GlobalProductName", "Product"), ("BrandId", brand.ToString("D"))))
                .UpdateDraft(Product, 0, "Product", CancellationToken.None);
        await Controller(wire, principal, Form(("ExpectedVersion", "0"), ("GlobalProductName", "Product"), ("BrandId", "")))
            .UpdateDraft(Product, 0, "Product", CancellationToken.None);

        var keys = wire.Requests.Select(request => request.IdempotencyKey).ToArray();
        Assert.NotEqual(keys[0], keys[1]);
        Assert.Equal(keys[0], keys[2]);
        Assert.NotEqual(keys[0], keys[3]);
        Assert.NotEqual(keys[1], keys[3]);
    }

    // ── list and brand options ────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("?start=0&length=10&brandId=33333333-3333-3333-3333-333333333333", "&brandId=33333333-3333-3333-3333-333333333333")]
    [InlineData("?start=0&length=10&brandId=33333333333333333333333333333333", "&brandId=33333333-3333-3333-3333-333333333333")]
    [InlineData("?start=0&length=10&brandId=abc", null)]
    [InlineData("?start=0&length=10&brandId=", null)]
    public async Task The_list_query_passes_a_GUID_brandId_and_drops_anything_else(string query, string? expected)
    {
        var wire = new RecordingHandler(_ => (HttpStatusCode.OK, "{\"data\":{\"items\":[],\"totalCount\":0}}"));

        await Controller(wire, [Read], null, query).List(CancellationToken.None);

        var url = Assert.Single(wire.Requests).Url;
        if (expected is null)
            Assert.DoesNotContain("brandId", url);
        else
            Assert.EndsWith(expected, url);
    }

    [Theory]
    [InlineData(Create)]
    [InlineData(Update)]
    [InlineData(UpdateBrand)]
    public async Task Brand_options_reach_MDMs_route_with_only_search_and_paging(string key)
    {
        var wire = new RecordingHandler(_ => (HttpStatusCode.OK, "{\"data\":{\"items\":[],\"totalCount\":0}}"));

        await Controller(wire, [key], null, "?search=abc&pageNumber=2&pageSize=20&tenantId=evil").BrandOptions(CancellationToken.None);

        Assert.Equal($"{Gateway}/api/global-products/brand-options?search=abc&pageNumber=2&pageSize=20", Assert.Single(wire.Requests).Url);
    }

    [Fact]
    public async Task Brand_options_without_a_Global_Product_write_key_are_403_and_MDM_is_never_asked()
    {
        var wire = new RecordingHandler(_ => (HttpStatusCode.OK, "{}"));

        var result = await Controller(wire, [Read, "mdm.brands.read"], null, "").BrandOptions(CancellationToken.None);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
        Assert.Empty(wire.Requests);
    }

    // ── the approved product's brand ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Change_brand_reaches_MDMs_brand_route_with_the_version_and_the_brand()
    {
        var wire = EnvelopeWire("{\"id\":\"11111111-1111-1111-1111-111111111111\",\"lifecycleStatus\":\"IdentityApproved\",\"version\":5,\"changed\":true}");

        var result = await Controller(wire, [UpdateBrand], Form(("ExpectedVersion", "4"), ("BrandId", BrandA.ToString("D"))))
            .ChangeBrand(Product, 4, CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, Assert.IsType<ContentResult>(result).StatusCode);
        var request = Assert.Single(wire.Requests);
        Assert.Equal("POST", request.Method);
        Assert.Equal($"{Gateway}/api/global-products/{Product:D}/brand", request.Url);
        var body = JsonDocument.Parse(request.Body).RootElement;
        Assert.Equal(4, body.GetProperty("expectedVersion").GetInt32());
        Assert.Equal(BrandA.ToString("D"), body.GetProperty("brandId").GetString());
        Assert.True(Guid.TryParseExact(request.IdempotencyKey, "D", out _));
    }

    [Fact]
    public async Task Change_brand_with_no_brand_sends_null_and_another_brand_makes_another_key()
    {
        var wire = EnvelopeWire("{\"id\":\"11111111-1111-1111-1111-111111111111\",\"lifecycleStatus\":\"IdentityApproved\",\"version\":5}");
        var principal = Principal([UpdateBrand]);

        await Controller(wire, principal, Form(("ExpectedVersion", "4"), ("BrandId", ""))).ChangeBrand(Product, 4, CancellationToken.None);
        await Controller(wire, principal, Form(("ExpectedVersion", "4"), ("BrandId", BrandB.ToString("D")))).ChangeBrand(Product, 4, CancellationToken.None);

        Assert.Equal(JsonValueKind.Null, JsonDocument.Parse(wire.Requests[0].Body).RootElement.GetProperty("brandId").ValueKind);
        Assert.NotEqual(wire.Requests[0].IdempotencyKey, wire.Requests[1].IdempotencyKey);
    }

    [Fact]
    public async Task Change_brand_without_the_update_brand_claim_is_403_and_MDM_is_never_asked()
    {
        var wire = EnvelopeWire("{}");

        var result = await Controller(wire, [Read, Create, Update], Form(("ExpectedVersion", "4"), ("BrandId", BrandA.ToString("D"))))
            .ChangeBrand(Product, 4, CancellationToken.None);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
        Assert.Empty(wire.Requests);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("abc")]
    [InlineData("BrandName")]
    public async Task A_malformed_change_brand_form_is_400(string kind)
    {
        var wire = EnvelopeWire("{}");
        var form = kind switch
        {
            "missing" => Form(("ExpectedVersion", "4")),
            "abc" => Form(("ExpectedVersion", "4"), ("BrandId", "abc")),
            _ => Form(("ExpectedVersion", "4"), ("BrandId", BrandA.ToString("D")), (kind, "X"))
        };

        var result = await Controller(wire, [UpdateBrand], form).ChangeBrand(Product, 4, CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
        Assert.Empty(wire.Requests);
    }

    // ── MDM's brand refusals → sentences ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task brand_not_found_404_is_the_brand_sentence_not_the_generic_not_found()
    {
        var result = await RefusedChangeAsync(HttpStatusCode.NotFound, "brand_not_found: Brand not found in this tenant.");

        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal(["ErrorBrandNotFound"], Errors(result));
        Assert.NotEqual(["ErrorNotFound"], Errors(result));
    }

    [Fact]
    public async Task brand_archived_409_is_the_brand_sentence_not_the_generic_conflict()
    {
        var result = await RefusedChangeAsync(HttpStatusCode.Conflict, "brand_archived: An archived brand cannot receive new product links.");

        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal(["ErrorBrandArchived"], Errors(result));
    }

    [Fact]
    public async Task brand_not_linkable_409_is_the_brand_sentence_not_the_generic_conflict()
    {
        var result = await RefusedChangeAsync(HttpStatusCode.Conflict,
            "brand_not_linkable: Only an active brand inside its validity window can receive new product links.");

        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal(["ErrorBrandNotLinkable"], Errors(result));
        Assert.NotEqual(["ErrorConflict"], Errors(result));
    }

    [Fact]
    public async Task A_brand_refusal_on_the_draft_edit_is_the_brand_sentence_too()
    {
        var wire = new RecordingHandler(_ => (HttpStatusCode.Conflict,
            "{\"isSuccessful\":false,\"statusCode\":409,\"errors\":[\"brand_archived: An archived brand cannot receive new product links.\"]}"));

        var result = await Controller(wire, [Update],
                Form(("ExpectedVersion", "0"), ("GlobalProductName", "Product"), ("BrandId", BrandA.ToString("D"))))
            .UpdateDraft(Product, 0, "Product", CancellationToken.None);

        Assert.Equal(["ErrorBrandArchived"], Errors(Assert.IsAssignableFrom<ObjectResult>(result)));
    }

    private static async Task<ObjectResult> RefusedChangeAsync(HttpStatusCode status, string error)
    {
        var wire = new RecordingHandler(_ => (status,
            $"{{\"isSuccessful\":false,\"statusCode\":{(int)status},\"errors\":[{JsonSerializer.Serialize(error)}]}}"));
        var result = await Controller(wire, [UpdateBrand], Form(("ExpectedVersion", "4"), ("BrandId", BrandA.ToString("D"))))
            .ChangeBrand(Product, 4, CancellationToken.None);
        return Assert.IsAssignableFrom<ObjectResult>(result);
    }

    // ── fixtures ──────────────────────────────────────────────────────────────────────────────────────────────────

    private static RecordingHandler CreateWire() => new(request => request.RequestUri!.AbsolutePath.EndsWith("/code-reservations")
        ? (HttpStatusCode.Created, "{\"data\":{\"reservationId\":\"55555555-5555-5555-5555-555555555555\",\"reservedCode\":\"GP-1\",\"version\":1}}")
        : (HttpStatusCode.Created, "{\"data\":{\"globalProductId\":\"11111111-1111-1111-1111-111111111111\",\"canonicalCode\":\"GP-1\",\"globalProductName\":\"Product\",\"lifecycleStatus\":\"Draft\",\"version\":0}}"));

    private static RecordingHandler EnvelopeWire(string data) =>
        new(_ => (HttpStatusCode.OK, $"{{\"isSuccessful\":true,\"statusCode\":200,\"data\":{data}}}"));

    private static IReadOnlyList<string> Errors(ObjectResult result)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(result.Value));
        return document.RootElement.GetProperty("errors").EnumerateArray().Select(error => error.GetString()!).ToArray();
    }

    private static Dictionary<string, StringValues> Form(params (string Key, string Value)[] fields)
    {
        var form = new Dictionary<string, StringValues> { ["__RequestVerificationToken"] = "fixture" };
        foreach (var (key, value) in fields)
            form[key] = value;
        return form;
    }

    private static GlobalProductsController Controller(
        RecordingHandler wire, string[] permissions, Dictionary<string, StringValues>? form, string query = "") =>
        Controller(wire, Principal(permissions), form, query);

    private static GlobalProductsController Controller(
        RecordingHandler wire, ClaimsPrincipal principal, Dictionary<string, StringValues>? form, string query = "")
    {
        var gateway = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["GatewayUrl"] = Gateway }).Build();
        var context = new DefaultHttpContext { User = principal };
        // Synthetic fixture credential: only seen by the recording handler, never sent to a network.
        context.Request.Headers.Cookie = "access_token=synthetic-gp-brand";
        context.Request.QueryString = new QueryString(query);
        if (form is not null)
        {
            context.Request.ContentType = "application/x-www-form-urlencoded";
            context.Request.Form = new FormCollection(form);
        }
        return new GlobalProductsController(new HttpClient(wire), gateway,
            new EchoLocalizer<Diten.Web.SharedResource>(), new EchoLocalizer<GlobalProductsIndex>(),
            NullLogger<GlobalProductsController>.Instance)
        { ControllerContext = new ControllerContext { HttpContext = context } };
    }

    private static readonly Guid Tenant = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid Subject = Guid.Parse("77777777-7777-7777-7777-777777777777");

    private static ClaimsPrincipal Principal(string[] permissions) => new(new ClaimsIdentity(
        new[]
        {
            new Claim("tenant_id", Tenant.ToString("D")),
            new Claim("actor_type", "tenant_user"),
            new Claim("sub", Subject.ToString("D"))
        }.Concat(permissions.Select(permission => new Claim("permission", permission))),
        "fixture"));

    private sealed record Recorded(string Method, string Url, string Body, string? IdempotencyKey);

    private sealed class RecordingHandler(Func<HttpRequestMessage, (HttpStatusCode Status, string Body)> answer) : HttpMessageHandler
    {
        public List<Recorded> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new(request.Method.Method, request.RequestUri!.OriginalString, body,
                request.Headers.TryGetValues("Idempotency-Key", out var keys) ? keys.Single() : null));
            var (status, content) = answer(request);
            return new HttpResponseMessage(status) { Content = new StringContent(content, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class EchoLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
