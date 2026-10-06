using System.Net;
using System.Text;
using System.Text.Json;
using Diten.Web;
using Diten.Web.Controllers.Platform;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Platform;

/// <summary>
/// BL-529 — the Platform Administrators proxy's "Reset password" is an ADMINISTRATOR's reset: it calls AuthService's
/// <c>POST /api/platform-auth/platform-admins/reset-password</c> (the old password and every session of that administrator
/// end, audited), never the anonymous <c>forgot-password</c>, which only e-mails a link and leaves the old password alive.
/// Driven on the REAL <see cref="AdministratorsController"/> with a gateway handler that records what it is sent.
/// </summary>
public sealed class AdministratorsResetPasswordProxyTests
{
    private const string Gateway = "http://gateway.test";

    [Fact]
    public async Task Reset_password_calls_the_administrators_reset_and_not_forgot_password()
    {
        var gateway = new RecordingGateway(HttpStatusCode.OK, """{"message":"processed"}""");
        var controller = ControllerWith(gateway);

        var result = await controller.ResetPasswordProxy(Guid.NewGuid(), "target@platform.test");

        var (method, path, body) = Assert.Single(gateway.Requests);
        Assert.Equal(HttpMethod.Post, method);
        Assert.Equal("/api/platform-auth/platform-admins/reset-password", path);
        Assert.Equal("target@platform.test", body.RootElement.GetProperty("email").GetString());
        Assert.True(Json(result).GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task Reset_password_carries_the_signed_in_administrators_token()
    {
        // BL-529 FIX2 — AuthService authorizes the reset by the caller's platform token; without it the call is anonymous.
        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            claims: [new System.Security.Claims.Claim("actor_type", "platform_admin")],
            expires: DateTime.UtcNow.AddMinutes(10)));
        var gateway = new RecordingGateway(HttpStatusCode.OK, """{"message":"processed"}""");
        var controller = ControllerWith(gateway);
        controller.ControllerContext.HttpContext.Request.Headers.Cookie = $"access_token={token}";

        await controller.ResetPasswordProxy(Guid.NewGuid(), "target@platform.test");

        Assert.Equal($"Bearer {token}", Assert.Single(gateway.Authorizations));
    }

    [Fact]
    public async Task A_refused_reset_is_not_reported_as_sent()
    {
        var gateway = new RecordingGateway(HttpStatusCode.Conflict, """{"errorCodes":[{"code":"USER_RESET_SELF"}]}""");

        var result = await ControllerWith(gateway).ResetPasswordProxy(Guid.NewGuid(), "me@platform.test");

        Assert.False(Json(result).GetProperty("success").GetBoolean());
    }

    [Theory]
    [InlineData("en", "current password stops working immediately")]
    [InlineData("tr", "mevcut parolası hemen geçersiz olur")]
    public void The_resend_help_says_an_existing_administrators_password_ends(string lang, string phrase)
    {
        var resx = File.ReadAllText(RepoPath("frontend", "Diten.Web", "Resources", "Views", "Platform", "Administrators", $"AdministratorsIndex.{lang}.resx"));
        var match = System.Text.RegularExpressions.Regex.Match(resx, @"<data name=""ResendInvitationHelp""[^>]*>\s*<value>(.*?)</value>");
        Assert.True(match.Success, "ResendInvitationHelp is missing");
        Assert.Contains(phrase, match.Groups[1].Value, StringComparison.Ordinal);
    }

    private static string RepoPath(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Resources")))
        {
            dir = dir.Parent;
        }

        return Path.Combine([dir?.FullName ?? throw new DirectoryNotFoundException("repo root"), .. parts]);
    }

    private static AdministratorsController ControllerWith(HttpMessageHandler gateway)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = Gateway }).Build();
        return new AdministratorsController(new HttpClient(gateway), configuration, new KeyLocalizer(), NullLogger<AdministratorsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    private static JsonElement Json(IActionResult result)
    {
        var json = Assert.IsType<JsonResult>(result);
        return JsonDocument.Parse(JsonSerializer.Serialize(json.Value)).RootElement;
    }

    private sealed class RecordingGateway(HttpStatusCode status, string answer) : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Path, JsonDocument Body)> Requests { get; } = [];
        public List<string?> Authorizations { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorizations.Add(request.Headers.Authorization?.ToString());
            var raw = request.Content is null ? "{}" : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request.Method, request.RequestUri!.AbsolutePath, JsonDocument.Parse(raw)));
            return new HttpResponseMessage(status) { Content = new StringContent(answer, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
