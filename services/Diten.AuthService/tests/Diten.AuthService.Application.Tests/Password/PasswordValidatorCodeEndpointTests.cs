using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Tests.Testing;
using Diten.AuthService.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace Diten.AuthService.Application.Tests.Password;

/// <summary>
/// WP-USERS-ERROR-CODES-01 (CT decision B) — the password validators always named their codes
/// (<see cref="PasswordErrorCodes"/>), but a pipeline validator's refusal left through the Api's GlobalExceptionHandler,
/// which dropped them: on the wire there was only English text. Now the same prefix list and helper serve both doors,
/// so <c>set-password</c> and <c>change-password</c> carry their codes. Real HTTP, test-owned mongod.
/// </summary>
[Collection("AccountKindAcceptance")]
public sealed class PasswordValidatorCodeEndpointTests : IClassFixture<PlatformEdgeTestHost>
{
    private readonly PlatformEdgeTestHost _host;
    private readonly Guid _tenantId = Guid.NewGuid();

    public PasswordValidatorCodeEndpointTests(PlatformEdgeTestHost host) => _host = host;

    [Fact]
    public async Task Set_password_with_nothing_filled_in_carries_its_three_codes()
    {
        using var client = _host.Client();

        var response = await client.PostAsJsonAsync("api/users/set-password", new { email = "", token = "", newPassword = "" });

        Assert.Equal(
            [PasswordErrorCodes.ResetEmailRequired, PasswordErrorCodes.ResetTokenRequired, PasswordErrorCodes.NewRequired],
            (await CodesAsync(response)).Select(c => c.Code));
    }

    [Fact]
    public async Task Set_password_too_long_carries_the_code_and_its_param()
    {
        using var client = _host.Client();

        var response = await client.PostAsJsonAsync("api/users/set-password", new { email = "a@b.test", token = "t", newPassword = new string('x', 129) });

        var (code, maxLength) = Assert.Single(await CodesAsync(response));
        Assert.Equal(PasswordErrorCodes.TooLong, code);
        Assert.Equal("128", maxLength);
    }

    [Fact]
    public async Task Change_password_with_nothing_filled_in_carries_its_two_codes()
    {
        using var client = _host.Client(await SeedTokenAsync(), _tenantId);

        var response = await client.PostAsJsonAsync("api/auth/change-password", new { currentPassword = "", newPassword = "" });

        Assert.Equal(
            [PasswordErrorCodes.CurrentRequired, PasswordErrorCodes.NewRequired],
            (await CodesAsync(response)).Select(c => c.Code));
    }

    private static async Task<List<(string Code, string? MaxLength)>> CodesAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"expected 400, got {(int)response.StatusCode}: {body}");
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("Validation failed", doc.RootElement.GetProperty("title").GetString());
        Assert.True(doc.RootElement.TryGetProperty("errorCodes", out var codes), $"no errorCodes on the wire: {body}");
        return codes.EnumerateArray()
            .Select(c => (c.GetProperty("code").GetString()!,
                c.TryGetProperty("params", out var p) && p.ValueKind == JsonValueKind.Object && p.TryGetProperty("maxLength", out var m) ? m.GetString() : null))
            .ToList();
    }

    private async Task<string> SeedTokenAsync()
    {
        using var scope = _host.Factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        sp.GetRequiredService<TenantContext>().SetTenant(_tenantId);
        var actor = new User($"actor.{Guid.NewGuid():N}@pwcodes.test", "hash:x", "Password", "Actor", _tenantId);
        actor.ConfirmEmail();
        actor = await sp.GetRequiredService<IUserRepository>().CreateAsync(actor, CancellationToken.None);
        return sp.GetRequiredService<ITokenService>().GenerateAccessToken(actor, [], [], expiresInMinutes: 60);
    }
}
