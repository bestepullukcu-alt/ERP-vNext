using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using Diten.Web.Controllers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-EMAIL-LANG-01 delivery 2 — My profile. <c>/Profile/api/me</c> and <c>/Profile/api/preferences</c> forward to the
/// gateway's <c>api/auth/me</c> and <c>api/auth/me/preferences</c> with the caller's own token; the write carries ONLY the
/// language, and a saved language also becomes the screen-language cookie (CT decision 1 = A).
/// </summary>
public sealed class ProfileControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Gateway = "http://gateway.test";
    private const string CultureCookie = ".AspNetCore.Culture";
    private static readonly Guid TenantId = Guid.Parse("77777777-7777-7777-7777-777777777777");

    private readonly WebApplicationFactory<Program> _factory;

    public ProfileControllerTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task The_profile_read_goes_to_the_users_own_record()
    {
        // FIX2 — no longer verbatim: projected to the page's four fields (see The_profile_read_reaches_the_browser_as_four_fields_only).
        const string me = """{"data":{"id":"1","email":"jean@acme.test","firstName":"Jean","lastName":"Dupont","preferredLanguage":"fr"},"isSuccessful":true,"statusCode":200}""";
        var gateway = new CapturingGateway(HttpStatusCode.OK, me);
        var (controller, _) = ControllerWith(gateway, "GET");

        var result = Assert.IsType<ContentResult>(await controller.Me());

        Assert.Equal(200, result.StatusCode);
        Assert.Equal("""{"data":{"firstName":"Jean","lastName":"Dupont","email":"jean@acme.test","preferredLanguage":"fr"},"isSuccessful":true,"statusCode":200}""", result.Content);
        Assert.Equal($"{Gateway}/api/auth/me", gateway.LastUri);
        Assert.Equal(TenantId.ToString("D"), gateway.LastTenantHeader);
        Assert.StartsWith("Bearer ", gateway.LastAuthorization);
    }

    [Fact]
    public async Task Only_the_language_is_sent_whatever_else_the_body_holds()
    {
        var gateway = new CapturingGateway(HttpStatusCode.OK, Saved("es"));
        var (controller, _) = ControllerWith(gateway, "PUT",
            """{"preferredLanguage":"es","userId":"00000000-0000-0000-0000-000000000009","tenantId":"x","firstName":"Changed","isActive":false}""");

        await controller.SavePreferences();

        Assert.Equal(HttpMethod.Put, gateway.LastMethod);
        Assert.Equal($"{Gateway}/api/auth/me/preferences", gateway.LastUri);
        Assert.Equal("""{"preferredLanguage":"es"}""", gateway.LastBody);
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("ar")]
    public async Task A_saved_language_also_becomes_the_screen_language_cookie(string language)
    {
        var (controller, context) = ControllerWith(new CapturingGateway(HttpStatusCode.OK, Saved(language)), "PUT",
            $$"""{"preferredLanguage":"{{language}}"}""");

        var result = Assert.IsType<ContentResult>(await controller.SavePreferences());

        Assert.Equal(200, result.StatusCode);
        var cookie = SetCookie(context);
        Assert.NotNull(cookie);
        Assert.Contains(Uri.EscapeDataString($"c={language}|uic={language}"), cookie);
        Assert.Contains("path=/", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("de")]
    [InlineData("fr-FR")]
    [InlineData("<script>")]
    public async Task Profile_saved_language_is_only_one_of_the_seven(string answered)
    {
        // Whatever an answer says, the screen-language cookie is only ever one of the seven tenant languages.
        var (controller, context) = ControllerWith(new CapturingGateway(HttpStatusCode.OK, Saved(answered)), "PUT",
            """{"preferredLanguage":"fr"}""");

        await controller.SavePreferences();

        Assert.Null(SetCookie(context));
    }

    [Fact]
    public async Task A_refused_language_leaves_the_cookie_alone_and_the_refusal_comes_back_verbatim()
    {
        const string refusal = """{"data":null,"statusCode":400,"isSuccessful":false,"errors":["PreferredLanguage must be one of: en, tr, fr, es, zh, ar, ru."],"errorCodes":["USER_PREFERRED_LANGUAGE_INVALID"]}""";
        var (controller, context) = ControllerWith(new CapturingGateway(HttpStatusCode.BadRequest, refusal), "PUT",
            """{"preferredLanguage":"de"}""");

        var result = Assert.IsType<ContentResult>(await controller.SavePreferences());

        Assert.Equal(400, result.StatusCode);
        Assert.Equal(refusal, result.Content);
        Assert.Null(SetCookie(context));
    }

    [Fact]
    public async Task Clearing_the_choice_sends_null_and_keeps_the_screen_language_as_it_is()
    {
        var gateway = new CapturingGateway(HttpStatusCode.OK, """{"data":{"preferredLanguage":null},"isSuccessful":true,"statusCode":200}""");
        var (controller, context) = ControllerWith(gateway, "PUT", """{"preferredLanguage":null}""");

        await controller.SavePreferences();

        Assert.Equal("""{"preferredLanguage":null}""", gateway.LastBody);
        Assert.Null(SetCookie(context));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{"preferredLanguage":7}""")]
    [InlineData("{}")]                                              // FIX2: no field is NOT "clear my choice"
    [InlineData("""{"language":"fr"}""")]                         // FIX2: a misspelled field
    [InlineData("""{"preferredLanguage":"\ud800"}""")]               // FIX2: a lone surrogate is refused, never a 500
    public async Task A_body_that_is_not_a_language_never_reaches_AuthService(string body)
    {
        var gateway = new CapturingGateway(HttpStatusCode.OK, "{}");
        var (controller, _) = ControllerWith(gateway, "PUT", body);

        Assert.IsType<BadRequestObjectResult>(await controller.SavePreferences());
        Assert.Null(gateway.LastUri);
    }

    [Fact]
    public async Task Without_a_session_token_nothing_is_sent_upstream()
    {
        var gateway = new CapturingGateway(HttpStatusCode.OK, "{}");
        var (controller, _) = ControllerWith(gateway, "GET", withToken: false);

        Assert.IsType<UnauthorizedObjectResult>(await controller.Me());
        Assert.Null(gateway.LastUri);
    }

    [Fact]
    public async Task A_body_that_is_not_json_is_refused_415_and_never_sent()
    {
        var gateway = new CapturingGateway(HttpStatusCode.OK, "{}");
        var (controller, _) = ControllerWith(gateway, "PUT", "preferredLanguage=fr", contentType: "application/x-www-form-urlencoded");

        var result = Assert.IsType<ObjectResult>(await controller.SavePreferences());

        Assert.Equal(415, result.StatusCode);
        Assert.Null(gateway.LastUri);
    }

    [Fact]
    public async Task The_profile_read_reaches_the_browser_as_four_fields_only()
    {
        const string whole = """{"data":{"id":"1","email":"jean@acme.test","firstName":"Jean","lastName":"Dupont","isActive":true,"roles":["Admin"],"tenantId":"77777777-7777-7777-7777-777777777777","failedLoginAttempts":3,"setupUrl":"https://dev.test/set?token=x","preferredLanguage":"fr"},"isSuccessful":true,"statusCode":200}""";
        var (controller, _) = ControllerWith(new CapturingGateway(HttpStatusCode.OK, whole), "GET");

        var result = Assert.IsType<ContentResult>(await controller.Me());

        Assert.Equal("application/json", result.ContentType);
        using var answer = System.Text.Json.JsonDocument.Parse(result.Content!);
        var data = answer.RootElement.GetProperty("data");
        Assert.Equal(["firstName", "lastName", "email", "preferredLanguage"], data.EnumerateObject().Select(p => p.Name));
        Assert.Equal("fr", data.GetProperty("preferredLanguage").GetString());
        Assert.DoesNotContain("roles", result.Content);
        Assert.DoesNotContain("setupUrl", result.Content);
        Assert.DoesNotContain("failedLoginAttempts", result.Content);
    }

    [Fact]
    public async Task A_tenant_header_the_browser_wrote_is_ignored_the_tenant_comes_from_the_token()
    {
        var gateway = new CapturingGateway(HttpStatusCode.OK, Saved("fr"));
        var (controller, _) = ControllerWith(gateway, "PUT", """{"preferredLanguage":"fr"}""", spoofedTenant: Guid.NewGuid().ToString());

        await controller.SavePreferences();

        Assert.Equal(TenantId.ToString("D"), gateway.LastTenantHeader);
    }

    [Fact]
    public async Task An_expired_token_sends_nothing_upstream()
    {
        var gateway = new CapturingGateway(HttpStatusCode.OK, "{}");
        var (controller, _) = ControllerWith(gateway, "GET", expired: true);

        Assert.IsType<UnauthorizedObjectResult>(await controller.Me());
        Assert.Null(gateway.LastUri);
    }

    [Theory]
    [InlineData("GET", "/Profile")]
    [InlineData("GET", "/Profile/api/me")]
    [InlineData("PUT", "/Profile/api/preferences")]
    public async Task A_platform_administrators_token_gets_403_and_none_of_the_page(string method, string url)
    {
        // UAS-001, measured end to end: the web tier's own validation of a SIGNED token, then the shell filter.
        using var client = _factory
            .WithWebHostBuilder(builder => builder.UseEnvironment("Development"))
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(new HttpMethod(method), url);
        request.Headers.Add("Cookie", "access_token=" + SignedDevelopmentToken("platform_admin", Guid.Parse("00000000-0000-0000-0000-000000000001")));
        if (method == "PUT")
        {
            request.Content = new StringContent("""{"preferredLanguage":"fr"}""", Encoding.UTF8, "application/json");
        }

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("profilePage", body);
        Assert.DoesNotContain("profileLanguage", body);
    }

    // Signed with the web tier's own Development settings (appsettings.Development.json, the local development value).
    private static string SignedDevelopmentToken(string actorType, Guid tenantId)
    {
        using var settings = System.Text.Json.JsonDocument.Parse(File.ReadAllText(WebSourcePath("appsettings.Development.json")));
        var jwt = settings.RootElement.GetProperty("JwtSettings");
        var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.GetProperty("Secret").GetString()!));
        var token = new JwtSecurityToken(
            jwt.GetProperty("Issuer").GetString(), jwt.GetProperty("Audience").GetString(),
            [new Claim("sub", Guid.NewGuid().ToString()), new Claim("actor_type", actorType), new Claim("tenant_id", tenantId.ToString())],
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new Microsoft.IdentityModel.Tokens.SigningCredentials(key, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string WebSourcePath(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(new[] { dir.FullName, "frontend", "Diten.Web" }.Concat(parts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(string.Join('/', parts));
    }

    [Theory]
    [InlineData("/Profile")]
    [InlineData("/Profile/api/me")]
    [InlineData("PUT /Profile/api/preferences")]
    public async Task The_routes_are_served_by_the_web_tier_and_ask_an_anonymous_caller_to_sign_in(string url)
    {
        using var client = _factory
            .WithWebHostBuilder(builder => builder.UseEnvironment("Development"))
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = url.StartsWith("PUT ", StringComparison.Ordinal)
            ? await client.PutAsync(url[4..], new StringContent("""{"preferredLanguage":"fr"}""", Encoding.UTF8, "application/json"))
            : await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains("login", response.Headers.Location?.OriginalString ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------- helpers

    private static string Saved(string language) =>
        $$"""{"data":{"preferredLanguage":"{{language}}"},"isSuccessful":true,"statusCode":200}""";

    private static string? SetCookie(HttpContext context) =>
        context.Response.Headers.SetCookie.FirstOrDefault(c => c is not null && c.StartsWith(CultureCookie + "=", StringComparison.Ordinal));

    private static (ProfileController Controller, HttpContext Context) ControllerWith(
        HttpMessageHandler gateway, string method, string? body = null, bool withToken = true,
        string contentType = "application/json", bool expired = false, string? spoofedTenant = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = Gateway })
            .Build();
        var controller = new ProfileController(new SingleClientFactory(gateway), configuration, NullLogger<ProfileController>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = method;
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body ?? string.Empty));
        httpContext.Request.ContentType = contentType;
        if (spoofedTenant is not null)
        {
            httpContext.Request.Headers["X-Tenant-Id"] = spoofedTenant;
        }

        if (withToken)
        {
            var jwt = new JwtSecurityToken(
                claims: [new Claim("sub", Guid.NewGuid().ToString()), new Claim("tenant_id", TenantId.ToString()), new Claim("actor_type", "tenant_user")],
                notBefore: expired ? DateTime.UtcNow.AddHours(-2) : null,
                expires: expired ? DateTime.UtcNow.AddHours(-1) : DateTime.UtcNow.AddMinutes(10));
            httpContext.Request.Headers.Cookie = $"access_token={new JwtSecurityTokenHandler().WriteToken(jwt)}";
        }

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return (controller, httpContext);
    }

    private sealed class CapturingGateway(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public string? LastUri { get; private set; }
        public HttpMethod? LastMethod { get; private set; }
        public string? LastTenantHeader { get; private set; }
        public string LastAuthorization { get; private set; } = string.Empty;
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastUri = request.RequestUri?.OriginalString;
            LastMethod = request.Method;
            LastTenantHeader = request.Headers.TryGetValues("X-Tenant-Id", out var values) ? values.First() : null;
            LastAuthorization = request.Headers.Authorization?.ToString() ?? string.Empty;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}

/// <summary>WP-EMAIL-LANG-01 delivery 2 — My profile's words exist in all seven languages, and the shell's "My profile"
/// item leads to it (the one changed line of the shell).</summary>
public sealed class ProfileViewContractTests
{
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    [Theory]
    [InlineData("Index.cshtml")]
    [InlineData("_IndexL10n.cshtml")]
    public void Every_localizer_key_of_the_view_exists_in_all_seven_languages(string view)
    {
        var source = File.ReadAllText(SourcePath("Views", "Profile", view));
        var keys = Regex.Matches(source, @"(?<!Shared)Localizer\[""([^""]+)""\]").Select(m => m.Groups[1].Value).Distinct().ToList();
        Assert.NotEmpty(keys);

        foreach (var language in Languages)
        {
            var resx = File.ReadAllText(SourcePath("Resources", "Views", "Profile", $"ProfileIndex.{language}.resx"));
            var missing = keys.Where(k => !resx.Contains($"<data name=\"{k}\"", StringComparison.Ordinal)).ToList();
            Assert.True(missing.Count == 0, $"{language} lacks: {string.Join(", ", missing)}");
        }
    }

    [Fact]
    public void All_seven_resx_files_carry_the_same_keys_and_none_is_a_copy_of_the_english()
    {
        Dictionary<string, string> Values(string language) => Regex
            .Matches(File.ReadAllText(SourcePath("Resources", "Views", "Profile", $"ProfileIndex.{language}.resx")),
                @"<data name=""([^""]+)""[^>]*>\s*<value>(.*?)</value>", RegexOptions.Singleline)
            .ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);

        var english = Values("en");
        foreach (var language in Languages.Where(l => l != "en"))
        {
            var values = Values(language);
            Assert.Equal(english.Keys.OrderBy(k => k), values.Keys.OrderBy(k => k));
            Assert.All(english.Keys, key => Assert.NotEqual(english[key], values[key]));
        }
    }

    [Fact]
    public void The_refusal_code_of_a_bad_language_has_its_own_sentence()
    {
        var bridge = File.ReadAllText(SourcePath("Views", "Profile", "_IndexL10n.cshtml"));
        Assert.Contains("[\"USER_PREFERRED_LANGUAGE_INVALID\"] = \"ErrorLanguageInvalid\"", bridge);
    }

    [Fact]
    public void The_shells_my_profile_item_leads_to_the_profile_page()
    {
        var layout = File.ReadAllText(SourcePath("Views", "Shared", "_LayoutTenantShell.cshtml"));
        var item = layout.IndexOf("@SharedLocalizer[\"MyProfile\"]", StringComparison.Ordinal);
        Assert.True(item > 0);
        var anchor = layout.LastIndexOf("<a ", item, StringComparison.Ordinal);
        Assert.Contains("href=\"/Profile\"", layout[anchor..item]);
    }

    [Fact]
    public void The_pages_language_options_are_exactly_the_seven_the_controller_writes()
    {
        var index = File.ReadAllText(SourcePath("Views", "Profile", "Index.cshtml"));
        var options = Regex.Matches(index, @"<option value=""([a-z]+)""").Select(m => m.Groups[1].Value);

        Assert.Equal(ProfileController.Languages.OrderBy(l => l), options.OrderBy(l => l));
    }

    [Fact]
    public void A_save_redraws_the_page_at_its_own_address_and_the_saved_flag_is_always_spent()
    {
        var script = File.ReadAllText(SourcePath("wwwroot", "assets", "js", "Profile", "index.js"));

        // A "?culture=xx" the top bar left in the address must not win over the cookie just written.
        Assert.Contains("root.location.replace(root.location.pathname)", script);
        Assert.DoesNotContain("location.reload(", script);
        // The flag is taken at start, whatever happens to the read after it.
        Assert.Matches(@"function start\(\) \{\s*savedBeforeReload = takeSavedFlag\(\);", script);
        // A 2xx without a readable body is not "saved".
        Assert.Contains("if (!result.body || !result.body.data)", script);
    }

    [Fact]
    public void The_loading_words_are_outside_the_hidden_placeholder()
    {
        var index = File.ReadAllText(SourcePath("Views", "Profile", "Index.cshtml"));
        var loading = index.IndexOf("@Localizer[\"Loading\"]", StringComparison.Ordinal);
        var hidden = index.IndexOf("aria-hidden=\"true\"", StringComparison.Ordinal);

        Assert.True(loading > 0 && hidden > loading, "the Loading text is inside (or after) the aria-hidden block");
    }

    [Fact]
    public void The_page_uses_the_tenant_shell_and_no_inline_style()
    {
        var index = File.ReadAllText(SourcePath("Views", "Profile", "Index.cshtml"));
        var script = File.ReadAllText(SourcePath("wwwroot", "assets", "js", "Profile", "index.js"));
        Assert.Contains("Layout = \"_LayoutTenantShell\";", index);
        Assert.DoesNotContain("style=", index);
        Assert.DoesNotContain(".style.", script);
    }

    private static string SourcePath(params string[] relativeParts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(new[] { dir.FullName, "frontend", "Diten.Web" }.Concat(relativeParts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(string.Join('/', relativeParts));
    }
}
