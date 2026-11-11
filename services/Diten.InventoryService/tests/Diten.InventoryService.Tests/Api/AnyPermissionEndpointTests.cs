using System.Net.Http.Json;
using Diten.InventoryService.Infrastructure.Authorization;
using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;

namespace Diten.InventoryService.Tests.Api;

/// <summary>
/// WP-MVP1-FIX-LIVE3 (core rule 3) — the three actions whose "any one of these keys" rule moved from the body to
/// <see cref="HasAnyPermissionAttribute"/> behave as before, on the real host: each accepted key opens the action (the request then meets its
/// ordinary validation — 400 / 404 / 428, never 401 / 403); a token without any of them is the contract 403 (same body as before); no token is
/// 401. The kind / status rule that follows (which key of the list applies) is unchanged and named in AuthorizationGuardTests.BodyKeyedActions.
/// </summary>
public sealed class AnyPermissionEndpointTests : IClassFixture<InventoryApiFactory>
{
    private readonly InventoryApiFactory _factory;

    public AnyPermissionEndpointTests(InventoryApiFactory factory) => _factory = factory;

    public static readonly TheoryData<string, string, string> Accepted = new()
    {
        { "POST", "/api/quarantine/decisions", QuarantinePermissions.DecisionsCreate },
        { "POST", "/api/quarantine/decisions", QuarantinePermissions.BlockedDispose },
        { "POST", "/api/quarantine/decisions", QuarantinePermissions.BlockedUnblock },
        { "POST", "/api/quarantine/decisions/{id}/submit", QuarantinePermissions.DecisionsCreate },
        { "POST", "/api/quarantine/decisions/{id}/submit", QuarantinePermissions.BlockedDispose },
        { "POST", "/api/quarantine/decisions/{id}/submit", QuarantinePermissions.BlockedUnblock },
        { "PATCH", "/api/trace/lots/{id}", TracePermissions.LotsUpdate },
        { "PATCH", "/api/trace/lots/{id}", TracePermissions.LotsUpdateReleased }
    };

    public static readonly TheoryData<string, string> Routes = new()
    {
        { "POST", "/api/quarantine/decisions" },
        { "POST", "/api/quarantine/decisions/{id}/submit" },
        { "PATCH", "/api/trace/lots/{id}" }
    };

    private async Task<(int Status, string Body)> SendAsync(InventoryScenario s, string method, string route, string token)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), route.Replace("{id}", Guid.NewGuid().ToString("D"), StringComparison.Ordinal))
        {
            Content = JsonContent.Create(new Dictionary<string, object?>())
        };
        if (token.Length > 0) request.Headers.Authorization = new("Bearer", token);
        using var response = await s.SendAsync(request);
        return ((int)response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [MemberData(nameof(Accepted))]
    public async Task Each_accepted_key_opens_the_action(string method, string route, string key)
    {
        var s = new InventoryScenario(_factory);
        var (status, body) = await SendAsync(s, method, route, TestTokens.Mint(s.Tenant, s.LegalEntity, s.User, [key]));
        Assert.True(status is not (401 or 403), $"{method} {route} with {key}: {status} {body}");
        Assert.True(status is 400 or 404 or 422 or 428, $"{method} {route} with {key}: the empty body must meet its ordinary validation, got {status} {body}");
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task Without_any_accepted_key_the_action_is_the_contract_403(string method, string route)
    {
        var s = new InventoryScenario(_factory);
        var (status, body) = await SendAsync(s, method, route, TestTokens.Mint(s.Tenant, s.LegalEntity, s.User, [QuarantinePermissions.HoldsRead, TracePermissions.LotsRead]));
        Assert.Equal(403, status);
        Assert.Contains("The caller lacks the permission required for this operation.", body, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task Without_a_token_the_action_is_401(string method, string route)
    {
        var s = new InventoryScenario(_factory);
        var (status, _) = await SendAsync(s, method, route, string.Empty);
        Assert.Equal(401, status);
    }
}
