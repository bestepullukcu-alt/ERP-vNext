using System.Net;
using System.Net.Http.Json;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Tests.Testing;
using Diten.AuthService.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace Diten.AuthService.Application.Tests.Users;

/// <summary>
/// WP-USERS-ERROR-CODES-01 — what a PIPELINE validator refusal looks like on the wire, measured over real HTTP.
/// ValidationBehavior is the outermost behavior, so its exception never passes ExceptionHandlingBehavior: it reaches
/// the Api's GlobalExceptionHandler and leaves as { title, status, detail, traceId } — no Response envelope and no
/// envelope. The work package ADDS <c>errorCodes</c> to that body when a failure carries a code with an allowed
/// prefix, and changes nothing else: the two refusals without such a code keep exactly the four fields they had,
/// and the password refusal keeps the same four with the same values in front of the new one. The first two tests
/// use nothing the work package introduced and pass unchanged on the commit before it — that run is the BEFORE
/// evidence; the third passed there with the four-field list (CT decision B, 2026-10-02, moved it to five).
/// (traceId differs per request and the default FluentValidation sentence follows the host culture; both are held
/// out of the comparison, everything else is exact.)
/// </summary>
[Collection("AccountKindAcceptance")]
public sealed class ValidationEnvelopeUnchangedTests : IClassFixture<PlatformEdgeTestHost>
{
    private const string AccountKindDetail = "Validation failed: \n -- Kind: Kind must be one of: Unknown, Human, Service. Severity: Error";
    private const string PasswordDetail = "Validation failed: \n -- Email: Email is required. Severity: Error\n -- Email: A valid email address is required. Severity: Error\n -- Token: Reset token is required. Severity: Error\n -- NewPassword: New password is required. Severity: Error";

    private readonly PlatformEdgeTestHost _host;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly string _token;

    public ValidationEnvelopeUnchangedTests(PlatformEdgeTestHost host)
    {
        _host = host;
        _token = SeedTokenAsync().GetAwaiter().GetResult();
    }

    [Fact]
    public async Task A_text_only_validator_refusal_of_another_door_keeps_its_body()
    {
        using var client = _host.Client(_token, _tenantId);

        var response = await client.PostAsJsonAsync($"api/users/{Guid.Parse("11111111-2222-3333-4444-555555555555")}/account-kind", new { kind = "Robot" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(AccountKindDetail, await ProblemDetailAsync(response));
    }

    [Fact]
    public async Task A_FluentValidation_default_code_is_not_forwarded()
    {
        using var client = _host.Client(_token, _tenantId);

        var response = await client.PutAsJsonAsync($"api/users/{Guid.Empty}", new { firstName = "Still", lastName = "Valid", isActive = true });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var detail = await ProblemDetailAsync(response);
        Assert.StartsWith("Validation failed: \n -- Id: ", detail);
        Assert.EndsWith(" Severity: Error", detail);
    }

    [Fact]
    public async Task The_password_validator_refusal_keeps_its_four_fields_and_only_gains_errorCodes()
    {
        using var client = _host.Client();

        var response = await client.PostAsJsonAsync("api/users/set-password", new { email = "", token = "", newPassword = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(PasswordDetail, await ProblemDetailAsync(response, "errorCodes"));
    }

    /// <summary>Asserts the exact property list of the body and returns <c>detail</c>.</summary>
    private static async Task<string> ProblemDetailAsync(HttpResponseMessage response, params string[] added)
    {
        var body = await response.Content.ReadAsStringAsync();
        using var doc = System.Text.Json.JsonDocument.Parse(body);
        Assert.Equal(["title", "status", "detail", "traceId", .. added], doc.RootElement.EnumerateObject().Select(p => p.Name).ToArray());
        Assert.Equal("Validation failed", doc.RootElement.GetProperty("title").GetString());
        Assert.Equal(400, doc.RootElement.GetProperty("status").GetInt32());
        return doc.RootElement.GetProperty("detail").GetString()!;
    }

    private async Task<string> SeedTokenAsync()
    {
        using var scope = _host.Factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        sp.GetRequiredService<TenantContext>().SetTenant(_tenantId);
        var actor = new User($"actor.{Guid.NewGuid():N}@pinned.test", "hash:x", "Pinned", "Actor", _tenantId);
        actor.ConfirmEmail();
        actor = await sp.GetRequiredService<IUserRepository>().CreateAsync(actor, CancellationToken.None);
        return sp.GetRequiredService<ITokenService>().GenerateAccessToken(actor, [],
            ["auth.users.update", "auth.users.account-kind.manage"], expiresInMinutes: 60);
    }
}
