using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.Web;
using Diten.Web.Controllers.CRM;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-CL-FE-4 — the country version pages (read opens them read-only, manage drives the writes, nothing without read)
/// and the claim code suggestion CLM-{PRODUCT NAME}-{NN} (the live finding was a prefix built from the MDM canonical
/// code, "CLM-GP000000000001-01").
/// </summary>
public sealed class ClaimsCountryVersionPageTests
{
    private static readonly Guid TenantId = Guid.Parse("bbbbbbbb-1111-2222-3333-444444444444");

    // ============================================================ pages

    [Fact]
    public void Create_page_hands_claim_and_country_to_the_script()
    {
        var claimId = Guid.NewGuid();
        var view = Assert.IsType<ViewResult>(Controller(Gateway("{}"), "crm.claim.read", "crm.claim.manage").CountryVersionCreate(claimId, " uz "));

        Assert.Equal("~/Views/CRM/Claims/CountryVersion.cshtml", view.ViewName);
        Assert.Equal(claimId.ToString(), view.ViewData["ClaimId"]);
        Assert.Equal("UZ", view.ViewData["CountryCode"]);
        Assert.Equal(string.Empty, view.ViewData["CountryVersionId"]);
        Assert.Equal(true, view.ViewData["CanManageClaims"]);
    }

    [Fact]
    public void A_reader_gets_the_edit_page_read_only()
    {
        var id = Guid.NewGuid();
        var view = Assert.IsType<ViewResult>(Controller(Gateway("{}"), "crm.claim.read").CountryVersionEdit(id));

        Assert.Equal(id.ToString(), view.ViewData["CountryVersionId"]);
        Assert.Equal(false, view.ViewData["CanManageClaims"]);
    }

    [Fact]
    public void Without_read_the_pages_are_a_plain_403()
    {
        var none = Controller(Gateway("{}"));
        Assert.Equal(403, Assert.IsType<StatusCodeResult>(none.CountryVersionCreate(Guid.NewGuid(), "TR")).StatusCode);
        Assert.Equal(403, Assert.IsType<StatusCodeResult>(none.CountryVersionEdit(Guid.NewGuid())).StatusCode);
    }

    // ============================================================ code suggestion

    [Theory]
    [InlineData("ALMIBA", "CLM-ALMIBA-")]
    [InlineData("  almiba  ", "CLM-ALMIBA-")]
    [InlineData("Tutukon Forte 500 mg", "CLM-TUTUKON-FORTE-500-MG-")]
    [InlineData("Özbek İlaç / Şurup", "CLM-OZBEK-ILAC-SURUP-")]
    [InlineData("L-karnitin (oral)", "CLM-L-KARNITIN-ORAL-")]
    public void Prefix_comes_from_the_product_name(string name, string expected)
        => Assert.Equal(expected, ClaimCodeSuggestion.Prefix(name));

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("—— / ——")]
    public void No_usable_name_gives_no_suggestion(string? name) => Assert.Null(ClaimCodeSuggestion.Suggest(name, []));

    [Fact]
    public void Nn_is_the_next_number_after_the_highest_existing_code_of_the_prefix()
    {
        string[] existing = ["CLM-ALMIBA-01", "clm-almiba-03", "CLM-ALMIBA-X", "CLM-ALMIBAX-09", "CLM-TUTUKON-07"];

        Assert.Equal("CLM-ALMIBA-04", ClaimCodeSuggestion.Suggest("Almiba", existing));
        Assert.Equal("CLM-NEW-01", ClaimCodeSuggestion.Suggest("new", existing));
        Assert.Equal("CLM-ALMIBA-100", ClaimCodeSuggestion.Suggest("ALMIBA", ["CLM-ALMIBA-99"]));
    }

    [Fact]
    public async Task Code_suggestion_endpoint_reads_the_tenant_codes_and_needs_manage()
    {
        var gateway = Gateway("""{"data":{"items":[{"claimCode":"CLM-ALMIBA-01"},{"claimCode":"CLM-ALMIBA-02"}],"total":2}}""");
        var result = await Controller(gateway, "crm.claim.read", "crm.claim.manage").CodeSuggestion("Almiba", default);
        var json = JsonDocument.Parse(JsonSerializer.Serialize(Assert.IsType<OkObjectResult>(result).Value)).RootElement;
        Assert.Equal("CLM-ALMIBA-03", json.GetProperty("data").GetProperty("code").GetString());

        var reader = await Controller(Gateway("{}"), "crm.claim.read").CodeSuggestion("Almiba", default);
        Assert.Equal(403, Assert.IsType<ObjectResult>(reader).StatusCode);
    }

    // ============================================================ helpers

    private static StubGateway Gateway(string body) => new(body);

    private static ClaimsController Controller(StubGateway gateway, params string[] permissions)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = "http://gateway.test" }).Build();
        var controller = new ClaimsController(new HttpClient(gateway), configuration, new KeyLocalizer(),
            NullLogger<ClaimsController>.Instance);
        var claims = new List<Claim> { new("tenantId", TenantId.ToString()) };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) }
        };
        controller.ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary());
        return controller;
    }

    private sealed class StubGateway(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }

    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
