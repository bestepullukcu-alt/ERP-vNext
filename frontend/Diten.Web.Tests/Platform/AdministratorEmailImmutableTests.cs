using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.Web;
using Diten.Web.Controllers.Platform;
using Diten.Web.Models.PlatformAdministrators;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Diten.Web.Tests.Platform;

/// <summary>
/// BL-521 FIX4 item 1 — A PLATFORM ADMINISTRATOR'S E-MAIL CANNOT BE CHANGED (Platform answers 409
/// <c>PLATFORM_ADMINISTRATOR_EMAIL_IMMUTABLE</c>; a new address is invited and the old administrator removed). The edit form
/// says it in the reader's language (en, tr — Platform is two-language) from the CODE, and the e-mail field is read-only in
/// edit mode: the old "unlock the e-mail" button is gone. Measured on the REAL AdministratorsController with the REAL
/// SharedResource files.
/// </summary>
public sealed class AdministratorEmailImmutableTests
{
    private const string Refusal =
        """{"isSuccessful":false,"statusCode":409,"errors":["An administrator's e-mail cannot be changed. Invite the new address as a new administrator and remove the old one."],"reason_code":"PLATFORM_ADMINISTRATOR_EMAIL_IMMUTABLE"}""";

    [Theory]
    [InlineData("en")]
    [InlineData("tr")]
    public async Task The_edit_form_shows_the_refusal_in_the_readers_language_from_its_code(string culture)
    {
        using var _ = new CultureScope(culture);
        var localizer = Localizer();
        var expected = localizer["AdminEmailImmutable"];
        Assert.False(expected.ResourceNotFound, $"SharedResource.{culture} has no AdminEmailImmutable.");

        var result = await Controller(localizer).Edit(Guid.NewGuid(), new PlatformAdministratorEditViewModel
        {
            Email = "moved@bl521.test", UserName = "probe", DisplayName = "Probe", Roles = ["ReadOnly"], Version = 3
        });

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(Assert.IsType<JsonResult>(result).Value));
        Assert.False(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(expected.Value, Assert.Single(json.RootElement.GetProperty("errors").EnumerateArray()).GetString());
        if (culture == "tr")
        {
            Assert.DoesNotContain("cannot be changed", json.RootElement.GetRawText(), StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void The_edit_form_has_no_way_to_unlock_the_email_and_makes_it_read_only_in_edit_mode()
    {
        var form = File.ReadAllText(RepoPath("frontend", "Diten.Web", "Views", "Platform", "Administrators", "_CreateEditOffcanvas.cshtml"));
        var script = File.ReadAllText(RepoPath("frontend", "Diten.Web", "wwwroot", "assets", "js", "Platform", "Administrators", "index.js"));

        Assert.DoesNotContain("btnUnlockEmail", form, StringComparison.Ordinal);
        Assert.DoesNotContain("btnUnlockEmail", script, StringComparison.Ordinal);
        Assert.Contains("SharedLocalizer[\"AdminEmailImmutable\"]", form, StringComparison.Ordinal);
        Assert.Contains("emailInput.readOnly = true", script, StringComparison.Ordinal);
        Assert.Contains("emailInput.readOnly = false", script, StringComparison.Ordinal);
    }

    private static AdministratorsController Controller(IStringLocalizer<SharedResource> localizer)
    {
        var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            "DitenAuth", "DitenClients", [new Claim("actor_type", "platform_admin")],
            DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(30),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes("administrator-email-immutable-tests-signing-key-long-enough")), SecurityAlgorithms.HmacSha256)));
        var http = new DefaultHttpContext();
        http.Request.Headers.Cookie = $"access_token={token}";

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = "http://gateway.test" })
            .Build();
        return new AdministratorsController(new HttpClient(new FixedAnswer()), configuration, localizer, NullLogger<AdministratorsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };
    }

    private static IStringLocalizer<SharedResource> Localizer()
        => new StringLocalizer<SharedResource>(new ResourceManagerStringLocalizerFactory(
            Options.Create(new LocalizationOptions { ResourcesPath = "Resources" }), NullLoggerFactory.Instance));

    private static string RepoPath(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "frontend", "Diten.Web", "Resources")))
        {
            directory = directory.Parent;
        }

        return Path.Combine([directory?.FullName ?? throw new DirectoryNotFoundException("repository root"), .. parts]);
    }

    private sealed class FixedAnswer : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Conflict) { Content = new StringContent(Refusal, Encoding.UTF8, "application/json") });
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
        private readonly CultureInfo _ui = CultureInfo.CurrentUICulture;

        public CultureScope(string name) => CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo(name);

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _culture;
            CultureInfo.CurrentUICulture = _ui;
        }
    }
}
