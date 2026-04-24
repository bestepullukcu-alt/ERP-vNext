using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Diten.Web.Services.Auth;

public sealed class AuthGateway : IAuthGateway
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;

    public AuthGateway(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<AuthBridgeResult> LoginTenantAsync(string email, string password, Guid tenantId, CancellationToken ct = default)
    {
        return SendAuthRequestAsync(
            "/api/tenant-auth/login",
            new { email, password },
            tenantId,
            includeBearer: false,
            accessToken: null,
            ct: ct);
    }

    public Task<AuthBridgeResult> LoginPlatformAsync(string email, string password, CancellationToken ct = default)
    {
        return SendAuthRequestAsync(
            "/api/platform-auth/login",
            new { email, password },
            tenantId: null,
            includeBearer: false,
            accessToken: null,
            ct: ct);
    }

    public Task<AuthBridgeResult> RefreshAsync(string accessToken, string refreshToken, Guid? tenantId, CancellationToken ct = default)
    {
        return SendAuthRequestAsync(
            "/api/auth/refresh-token",
            new { accessToken, refreshToken },
            tenantId,
            includeBearer: false,
            accessToken: null,
            ct: ct);
    }

    public async Task LogoutAsync(string accessToken, string refreshToken, Guid? tenantId, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        request.Content = JsonContent.Create(new { accessToken, refreshToken });

        if (tenantId.HasValue)
        {
            request.Headers.Add("X-Tenant-Id", tenantId.Value.ToString());
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _httpClient.SendAsync(request, ct);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var payload = await TryReadErrorAsync(response, ct);
        throw new InvalidOperationException(payload ?? "Logout request failed.");
    }

    private async Task<AuthBridgeResult> SendAuthRequestAsync(
        string url,
        object payload,
        Guid? tenantId,
        bool includeBearer,
        string? accessToken,
        CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload)
        };

        if (tenantId.HasValue)
        {
            request.Headers.Add("X-Tenant-Id", tenantId.Value.ToString());
        }

        if (includeBearer && !string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            return new AuthBridgeResult(
                false,
                null,
                null,
                null,
                null,
                await TryReadErrorAsync(response, ct));
        }

        var authResponse = await response.Content.ReadFromJsonAsync<AuthBridgeResult>(JsonOptions, ct);
        if (authResponse is null)
        {
            return new AuthBridgeResult(false, null, null, null, null, "Authentication response could not be parsed.");
        }

        return authResponse with { Success = true };
    }

    private static async Task<string?> TryReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var content = await response.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(content))
        {
            return response.ReasonPhrase ?? $"HTTP {((int)response.StatusCode)}";
        }

        try
        {
            using var document = JsonDocument.Parse(content);
            if (document.RootElement.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
            {
                return detail.GetString();
            }

            if (document.RootElement.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
            {
                return title.GetString();
            }
        }
        catch
        {
            return content;
        }

        return content;
    }
}
