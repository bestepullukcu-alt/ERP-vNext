using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Diten.Web.Services.Auth;

public sealed class AuthGateway : IAuthGateway
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuthGateway(HttpClient httpClient, IHttpContextAccessor httpContextAccessor)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
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

    public Task<AuthBridgeResult> VerifyTenantMfaAsync(string challengeId, string code, CancellationToken ct = default)
    {
        return SendAuthRequestAsync(
            "/api/tenant-auth/mfa/verify",
            new { challengeId, code },
            tenantId: null,
            includeBearer: false,
            accessToken: null,
            ct: ct);
    }

    public Task<AuthBridgeResult> ResendTenantMfaAsync(string challengeId, CancellationToken ct = default)
    {
        return SendAuthRequestAsync(
            "/api/tenant-auth/mfa/resend",
            new { challengeId },
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
        AddClientMetadataHeaders(request);

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
        AddClientMetadataHeaders(request);

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

        var authResponse = await ReadAuthBridgeResultAsync(response, ct);
        if (authResponse is null)
        {
            return new AuthBridgeResult(false, null, null, null, null, "Authentication response could not be parsed.");
        }

        return authResponse with { Success = true };
    }

    private static async Task<AuthBridgeResult?> ReadAuthBridgeResultAsync(HttpResponseMessage response, CancellationToken ct)
    {
        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        var root = document.RootElement;
        if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
        {
            return data.Deserialize<AuthBridgeResult>(JsonOptions);
        }

        return root.Deserialize<AuthBridgeResult>(JsonOptions);
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

            if (document.RootElement.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
            {
                var messages = errors
                    .EnumerateArray()
                    .Where(e => e.ValueKind == JsonValueKind.String)
                    .Select(e => e.GetString())
                    .Where(e => !string.IsNullOrWhiteSpace(e))
                    .ToArray();

                if (messages.Length > 0)
                {
                    return string.Join(" ", messages);
                }
            }
        }
        catch
        {
            return content;
        }

        return content;
    }

    private void AddClientMetadataHeaders(HttpRequestMessage request)
    {
        var context = _httpContextAccessor.HttpContext;
        if (context is null)
        {
            return;
        }

        var remoteIp = context.Connection.RemoteIpAddress?.ToString();
        if (!string.IsNullOrWhiteSpace(remoteIp))
        {
            request.Headers.TryAddWithoutValidation("X-Forwarded-For", remoteIp);
        }

        var userAgent = context.Request.Headers.UserAgent.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(userAgent))
        {
            request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        }

        var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault();
        request.Headers.TryAddWithoutValidation(
            "X-Correlation-Id",
            string.IsNullOrWhiteSpace(correlationId) ? context.TraceIdentifier : correlationId);
    }
}
