using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.Web.Controllers;
using Diten.Web.Controllers.Commercial;
using Diten.Web.Services.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-VW-W6 · W6-FIX-a — through the REAL web tier (routing, the token bridge, the cookie challenge, the controller):
/// <list type="bullet">
/// <item>G-5 — the four action routes (price list · import · rule · order) answer: 403 without the key, the CRM is reached
/// with it — never 404 (<c>{action}</c> is MVC's reserved route value and made every one of them 404);</item>
/// <item>G-13 — a signed-out e-mail link goes to the sign-in page WITH its tenant (a valid guid only) and its ReturnUrl;
/// after sign-in only a page of this site is returned to;</item>
/// <item>G-4 — the price list edit sends no legal entity and locks the company field (the real form.js under node).</item>
/// </list>
/// Nothing leaves the web tier: the gateway is a recording handler, Platform points at a closed local port.
/// </summary>
public sealed class W6FixaWebTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    // Test-only signing material for a token this test makes and this test's web host checks; not a credential.
    private const string SigningKey = "w6-fixa-http-test-signing-key-0123456789abcdef-0123";
    private const string Issuer = "w6-fixa-http-test";
    private const string Audience = "w6-fixa-http-test";
    private const string ClosedPort = "http://127.0.0.1:9";
    private const string TenantText = "97c5e1b2-0000-4000-8000-000000000001";
    private static readonly string Id = "11111111-2222-3333-4444-555555555555";

    private readonly WebApplicationFactory<Program> _host;
    private readonly RecordingGateway _gateway = new();

    public W6FixaWebTests(WebApplicationFactory<Program> factory)
    {
        _host = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("JwtSettings:Secret", SigningKey);
            builder.UseSetting("JwtSettings:Issuer", Issuer);
            builder.UseSetting("JwtSettings:Audience", Audience);
            builder.UseSetting("GatewayUrl", ClosedPort);
            builder.UseSetting("PlatformServiceUrl", ClosedPort);
            builder.ConfigureTestServices(services =>
                services.AddTransient(_ => new HttpClient(_gateway, disposeHandler: false)));
        });
    }

    public void Dispose() => _host.Dispose();

    // ═══ G-5 · the action routes ═════════════════════════════════════════════════════════════════════════════════

    public static TheoryData<string, string, string> ActionRoutes() => new()
    {
        { "/Commercial/Pricing/api/price-lists/" + Id + "/submit", CommercialPricingController.ListsSubmit, "/api/commercial/price-lists/" + Id + "/submit" },
        { "/Commercial/Pricing/api/imports/" + Id + "/convert", CommercialPricingController.ListsImport, "/api/commercial/price-list-imports/" + Id + "/convert" },
        { "/Commercial/Pricing/api/rules/" + Id + "/submit", CommercialPricingController.RulesSubmit, "/api/commercial/pricing-rules/" + Id + "/submit" },
        { "/Commercial/PreOrders/api/orders/" + Id + "/submit", PreOrdersController.ActionKeys["submit"], "/api/commercial/pre-orders/" + Id + "/submit" }
    };

    [Theory]
    [MemberData(nameof(ActionRoutes))]
    public async Task G5_an_action_route_without_its_key_answers_403_never_404_and_reaches_nothing(string url, string key, string crmTail)
    {
        _ = key; _ = crmTail;
        using var client = Client();

        using var response = await PostAsync(client, url, Token("some.other.key"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(_gateway.Urls);
    }

    [Theory]
    [MemberData(nameof(ActionRoutes))]
    public async Task G5_an_action_route_with_its_key_reaches_the_crm_path_of_its_verb(string url, string key, string crmTail)
    {
        using var client = Client();

        using var response = await PostAsync(client, url, Token(key));

        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
        var sent = Assert.Single(_gateway.Urls);
        Assert.Equal(crmTail, sent);
    }

    [Fact]
    public async Task G5_an_unknown_verb_is_still_404_and_reaches_nothing()
    {
        using var client = Client();

        using var response = await PostAsync(client, "/Commercial/PreOrders/api/orders/" + Id + "/explode", Token(PreOrdersController.ActionKeys["submit"]));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(_gateway.Urls);
    }

    [Fact]
    public void G5_no_route_template_in_the_web_project_has_an_action_parameter()
    {
        var offenders = Directory.EnumerateFiles(Path.Combine(WebRoot(), "Controllers"), "*.cs", SearchOption.AllDirectories)
            .SelectMany(file => File.ReadLines(file).Select((line, i) => (file, line, i)))
            .Where(x => System.Text.RegularExpressions.Regex.IsMatch(x.line, @"\[(Http\w+|Route)\(""[^""]*\{action[:}?]"))
            .Select(x => Path.GetFileName(x.file) + ":" + (x.i + 1))
            .ToList();
        Assert.Empty(offenders);
    }

    // ═══ G-13 · the e-mail link signs in to its tenant ═════════════════════════════════════════════════════════════

    [Fact]
    public async Task G13_a_signed_out_approval_link_goes_to_the_sign_in_page_with_its_tenant_and_return_url()
    {
        using var client = Client();
        var link = "/Commercial/PreOrders/ApprovalLink?token=x&tenantId=" + TenantText;

        using var response = await GetAsync(client, link, token: null);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        var (path, query) = Split(response.Headers.Location!);
        Assert.Equal("/account/login", path, ignoreCase: true);
        Assert.Equal(TenantText, query["tenantId"].Single());
        Assert.Equal(link, query["ReturnUrl"].Single());                                       // the way back is kept
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData(TenantText + "&tenantId=" + TenantText)]                                                  // two of them: none is carried
    public async Task G13_an_invalid_tenant_is_not_carried_and_the_redirect_is_todays(string tenant)
    {
        using var client = Client();
        var link = "/Commercial/PreOrders/ApprovalLink?token=x&tenantId=" + tenant;

        using var response = await GetAsync(client, link, token: null);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        var (path, query) = Split(response.Headers.Location!);
        Assert.Equal("/account/login", path, ignoreCase: true);
        Assert.False(query.ContainsKey("tenantId"));
        Assert.Equal(link, query["ReturnUrl"].Single());
    }

    [Fact]
    public async Task G13_without_a_tenant_the_redirect_is_todays()
    {
        using var client = Client();

        using var response = await GetAsync(client, "/Commercial/PreOrders/ApprovalLink?token=x", token: null);

        var (_, query) = Split(response.Headers.Location!);
        Assert.False(query.ContainsKey("tenantId"));
        Assert.Equal("/Commercial/PreOrders/ApprovalLink?token=x", query["ReturnUrl"].Single());
    }

    [Theory]
    [InlineData("/Commercial/PreOrders/ApprovalLink?token=x&tenantId=" + TenantText, "/Commercial/PreOrders/ApprovalLink?token=x&tenantId=" + TenantText)]
    [InlineData("//evil.example/x", "/WorkCenterNext")]
    [InlineData("/\\evil.example/x", "/WorkCenterNext")]
    [InlineData("https://evil.example/x", "/WorkCenterNext")]
    public async Task G13_a_signed_in_reader_returns_only_to_a_page_of_this_site(string returnUrl, string expected)
    {
        // The sign-in page is outside the token bridge (it never refreshes there), so the signed-in reader is the
        // application's own AccountController, built from the host's services, with a tenant user on its request.
        using var scope = _host.Services.CreateScope();
        var controller = ActivatorUtilities.CreateInstance<AccountController>(scope.ServiceProvider);
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
            {
                RequestServices = scope.ServiceProvider,
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("actor_type", "tenant_user"), new Claim("tenant_id", TenantText)], "fixture"))
            }
        };

        var result = await controller.Login(null, returnUrl, CancellationToken.None);

        Assert.Equal(expected, Assert.IsType<Microsoft.AspNetCore.Mvc.RedirectResult>(result).Url);
    }

    [Theory]
    [InlineData("/Commercial/PreOrders/Details/1?tenantId=2", true)]
    [InlineData("/", true)]
    [InlineData("//evil.example", false)]
    [InlineData("/\\evil.example", false)]
    [InlineData("https://evil.example", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("WorkCenterNext", false)]
    [InlineData("/x\r\nLocation: https://evil.example", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void G13_the_return_address_is_a_page_of_this_site_or_the_default(string? returnUrl, bool kept)
    {
        Assert.Equal(kept, LoginReturn.IsLocal(returnUrl));
        Assert.Equal(kept ? returnUrl : "/WorkCenterNext", AccountController.ResolveReturnUrl(returnUrl, "/WorkCenterNext"));
    }

    // ═══ G-4 · the price list edit sends no legal entity ═════════════════════════════════════════════════════════

    [Fact]
    public void G4_the_price_list_edit_sends_no_legal_entity_and_locks_the_company_while_the_create_still_sends_it()
    {
        if (RunNode("process.stdout.write('ok')") is null) return;
        var edit = JsonDocument.Parse(RunNode(FormHarness(editId: Id))!).RootElement;
        var put = edit.GetProperty("calls").EnumerateArray().Single(c => c.GetProperty("m").GetString() == "PUT");
        Assert.EndsWith("/price-lists/" + Id, put.GetProperty("u").GetString());
        Assert.False(put.GetProperty("b").TryGetProperty("legalEntityId", out _), put.GetProperty("b").ToString());
        Assert.Equal("Liste A", put.GetProperty("b").GetProperty("name").GetString());       // the rest of the head still goes
        Assert.Equal(7, put.GetProperty("b").GetProperty("expectedVersion").GetInt32());
        Assert.True(edit.GetProperty("companyLocked").GetBoolean());

        var create = JsonDocument.Parse(RunNode(FormHarness(editId: null))!).RootElement;
        var post = create.GetProperty("calls").EnumerateArray().Single(c => c.GetProperty("m").GetString() == "POST");
        Assert.Equal("le-1", post.GetProperty("b").GetProperty("legalEntityId").GetString());     // KORU — the create names it
        Assert.False(create.GetProperty("companyLocked").GetBoolean());
    }

    private static string FormHarness(string? editId)
    {
        string P(string rel) => Path.Combine(WebRoot(), "wwwroot", "assets", "js", "Commercial", rel).Replace("\\", "/");
        return @"
globalThis.window = globalThis;
const els = {};
const el = (id) => els[id] || (els[id] = { id, value: '', disabled: false, innerHTML: '', textContent: '', attrs: {}, handlers: {},
  classList: { toggle() {}, remove() {}, add() {} }, getAttribute(n) { return this.attrs[n] ?? null; },
  addEventListener(t, f) { this.handlers[t] = f; }, querySelectorAll() { return []; }, scrollIntoView() {} });
el('pr-form-root').attrs['data-id'] = " + (editId is null ? "null" : "'" + editId + "'") + @";
let ready = null;
globalThis.document = { documentElement: { lang: 'tr' }, getElementById: el, addEventListener(t, f) { ready = f; } };
globalThis.location = { href: '' };
const calls = [];
window.L10n = {};
window.PreOrdersUi = {
  request: async (m, u, b) => { calls.push({ m, u, b: b === undefined ? null : JSON.parse(JSON.stringify(b)) });
    if (m === 'GET' && /\/items/.test(u)) return { ok: true, body: { data: { items: [] } } };
    if (m === 'GET') return { ok: true, body: { data: { id: '" + Id + @"', name: 'Liste A', legalEntityId: 'le-1', currency: 'TRY', kind: 'official', validFrom: '2026-11-01', status: 'draft', version: 7 } } };
    return { ok: true, body: { data: { id: 'new-1' } } }; },
  data: (r) => r && r.body ? r.body.data : null, toast() {}, errorText() { return 'err'; }, esc: (s) => String(s ?? ''), bidi: (s) => String(s ?? '')
};
window.PricingCore = { itemInput: () => ({ input: {} }) };
window.PricingUi = { api: '/Commercial/Pricing/api', fillCompanies: async (sel, selected) => { sel.value = selected || ''; }, skuPicker() {} };
require('" + P("PriceLists/form.js") + @"');
(async () => {
  await ready();
  if (!" + (editId is null ? "false" : "true") + @") { el('pr-company').value = 'le-1'; }
  el('pr-name').value = 'Liste A'; el('pr-currency').value = 'TRY'; el('pr-from').value = '2026-11-01';
  await el('pr-save').handlers.click();
  process.stdout.write(JSON.stringify({ calls, companyLocked: el('pr-company').disabled }));
})().catch((e) => { console.error(e && e.stack || e); process.exit(1); });
";
    }

    // ═══ the wire ═════════════════════════════════════════════════════════════════════════════════════════════════

    private HttpClient Client() =>
        _host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string url, string token)
        => SendAsync(client, HttpMethod.Post, url, token, new StringContent("{}", Encoding.UTF8, "application/json"));

    private static Task<HttpResponseMessage> GetAsync(HttpClient client, string url, string? token)
        => SendAsync(client, HttpMethod.Get, url, token, null);

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string url, string? token, HttpContent? content)
    {
        using var request = new HttpRequestMessage(method, url) { Content = content };
        if (token is not null)
        {
            request.Headers.Add("Cookie", $"access_token={token}");
        }

        request.Headers.Add("Accept", method == HttpMethod.Get ? "text/html" : "application/json");
        return await client.SendAsync(request);
    }

    private static (string Path, Dictionary<string, Microsoft.Extensions.Primitives.StringValues> Query) Split(Uri location)
    {
        var text = location.OriginalString;
        var at = text.IndexOf('?');
        var path = new Uri(new Uri("http://localhost"), at < 0 ? text : text[..at]).AbsolutePath;
        return (path, at < 0 ? [] : QueryHelpers.ParseQuery(text[at..]));
    }

    private static string Token(params string[] permissions)
    {
        var claims = new List<Claim>
        {
            new("sub", "7b2c51f0-0000-4000-8000-0000000000a1"),
            new("tenant_id", TenantText),
            new("tenantId", TenantText),
            new("actor_type", "tenant_user")
        };
        claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256)));
    }

    private static string? RunNode(string code)
    {
        var temp = Path.Combine(Path.GetTempPath(), "w6fixa-" + Guid.NewGuid().ToString("N") + ".js");
        File.WriteAllText(temp, code, new UTF8Encoding(false));
        try
        {
            using var process = Process.Start(new ProcessStartInfo("node", "\"" + temp + "\"")
            {
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, StandardOutputEncoding = Encoding.UTF8
            });
            if (process is null) return null;
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(20000);
            Assert.True(process.ExitCode == 0, error);
            return output;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
        finally
        {
            File.Delete(temp);
        }
    }

    private static string WebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Diten.Web.csproj"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("repo root not found"), "frontend", "Diten.Web");
    }

    /// <summary>Records every URL a controller sends to the gateway and answers 200 with an empty data envelope.</summary>
    private sealed class RecordingGateway : HttpMessageHandler
    {
        private readonly List<string> _urls = [];
        public IReadOnlyList<string> Urls { get { lock (_urls) return _urls.ToList(); } }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.Host != "127.0.0.1" || request.RequestUri.Port != 9 || !request.RequestUri.AbsolutePath.StartsWith("/api/commercial/", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            }

            lock (_urls) _urls.Add(request.RequestUri.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"isSuccessful\":true,\"data\":{}}", Encoding.UTF8, "application/json")
            });
        }
    }
}
