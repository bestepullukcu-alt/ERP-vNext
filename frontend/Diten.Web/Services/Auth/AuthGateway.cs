using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace Diten.Web.Services.Auth;

public sealed class AuthGateway : IAuthGateway
{
    // Maps each backend password error code (AuthService PasswordErrorCodes) -> the SharedResource.*.resx key that
    // holds its localized template. Keep this 1:1 with the backend codes and the resx keys (all 7 languages) — a
    // guard test enforces it. An unknown code (not in this map) falls back to the English `detail`.
    private static readonly IReadOnlyDictionary<string, string> ErrorCodeResourceKeys = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["password.required"] = "Password.Error.Required",
        ["password.too_short"] = "Password.Error.TooShort",
        ["password.too_long"] = "Password.Error.TooLong",
        ["password.needs_uppercase"] = "Password.Error.NeedsUppercase",
        ["password.needs_special"] = "Password.Error.NeedsSpecial",
        ["password.current_required"] = "Password.Error.CurrentRequired",
        ["password.new_required"] = "Password.Error.NewRequired",
        ["password.reset_email_required"] = "Password.Error.ResetEmailRequired",
        ["password.reset_token_required"] = "Password.Error.ResetTokenRequired",
    };

    // User-facing message when the auth service / gateway is unreachable. Plain (non-localized) by design — the
    // raw transport exception / stack is NEVER surfaced; it is only logged.
    private const string AuthServiceUnavailableMessage =
        "The authentication service is temporarily unavailable. Please try again in a moment.";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuthGateway> _logger;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public AuthGateway(
        HttpClient httpClient,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AuthGateway> logger,
        IStringLocalizer<SharedResource> localizer)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _localizer = localizer;
    }

    public Task<AuthBridgeResult> LoginTenantAsync(string email, string password, Guid tenantId, bool rememberMe = false, CancellationToken ct = default)
    {
        return SendAuthRequestAsync(
            "/api/tenant-auth/login",
            new { email, password, rememberMe },
            tenantId,
            includeBearer: false,
            accessToken: null,
            ct: ct);
    }

    public Task<AuthBridgeResult> LoginPlatformAsync(string email, string password, bool rememberMe = false, CancellationToken ct = default)
    {
        return SendAuthRequestAsync(
            "/api/platform-auth/login",
            new { email, password, rememberMe },
            tenantId: null,
            includeBearer: false,
            accessToken: null,
            ct: ct);
    }

    public Task<AuthBridgeResult> ChangePlatformPasswordAsync(string currentPassword, string newPassword, bool rememberMe = false, CancellationToken ct = default)
    {
        var accessToken = _httpContextAccessor.HttpContext is { } context
            ? AuthTokenCookies.GetAccessToken(context.Request)
            : null;
        return SendAuthRequestAsync(
            "/api/platform-auth/change-password/forced",
            new { currentPassword, newPassword, rememberMe },
            tenantId: null,
            includeBearer: true,
            accessToken: accessToken,
            ct: ct);
    }

    public Task<AuthBridgeResult> ChangeTenantPasswordAsync(string currentPassword, string newPassword, bool rememberMe = false, CancellationToken ct = default)
    {
        // FIX-TENANT-MUSTCHANGEPW — mirror of ChangePlatformPasswordAsync for tenant_user. The AuthService reads the
        // user + tenant from the validated bearer JWT, so no X-Tenant-Id is sent (tenantId: null).
        var accessToken = _httpContextAccessor.HttpContext is { } context
            ? AuthTokenCookies.GetAccessToken(context.Request)
            : null;
        return SendAuthRequestAsync(
            "/api/tenant-auth/change-password/forced",
            new { currentPassword, newPassword, rememberMe },
            tenantId: null,
            includeBearer: true,
            accessToken: accessToken,
            ct: ct);
    }

    public async Task<bool> ForgotPlatformPasswordAsync(string email, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/platform-auth/forgot-password")
        {
            Content = JsonContent.Create(new { email })
        };
        AddClientMetadataHeaders(request);
        var response = await _httpClient.SendAsync(request, ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<AuthBridgeResult> ResetPlatformPasswordAsync(string email, string token, string newPassword, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/platform-auth/reset-password")
        {
            Content = JsonContent.Create(new { email, token, newPassword })
        };
        AddClientMetadataHeaders(request);
        var response = await _httpClient.SendAsync(request, ct);
        return response.IsSuccessStatusCode
            ? new AuthBridgeResult(true, null, null, null, null, null)
            : new AuthBridgeResult(false, null, null, null, null, await TryReadErrorAsync(response, ct));
    }

    public async Task<AuthBridgeResult> ResetTenantPasswordAsync(string email, string token, string newPassword, CancellationToken ct = default)
    {
        // Anonymous redemption: the invited user holds no JWT/tenant yet. The AuthService endpoint
        // resolves the user by token hash, so no X-Tenant-Id / bearer is sent.
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/users/set-password")
        {
            Content = JsonContent.Create(new { email, token, newPassword })
        };
        AddClientMetadataHeaders(request);
        var response = await _httpClient.SendAsync(request, ct);
        return response.IsSuccessStatusCode
            ? new AuthBridgeResult(true, null, null, null, null, null)
            : new AuthBridgeResult(false, null, null, null, null, await TryReadErrorAsync(response, ct));
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

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !ct.IsCancellationRequested))
        {
            // Auth service / gateway unreachable (connection refused / DNS / socket) or a client-side timeout.
            // Surface a clean, user-friendly 503 — never the raw exception text/stack — so the login page shows a
            // friendly message instead of a 500. A genuine caller cancellation (ct) is excluded by the filter and
            // propagates as a real cancellation.
            _logger.LogWarning(ex, "Auth bridge request to {Url} failed: authentication service is unreachable.", url);
            return new AuthBridgeResult(
                Success: false,
                AccessToken: null,
                RefreshToken: null,
                ExpiresAt: null,
                User: null,
                ErrorMessage: AuthServiceUnavailableMessage,
                ReauthRequired: false,
                StatusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (!response.IsSuccessStatusCode)
        {
            bool reauthRequired = false;
            if (response.Headers.TryGetValues("X-Refresh-Error-Type", out var values))
            {
                reauthRequired = values.Contains("terminal");
            }
            else
            {
                reauthRequired = response.StatusCode == System.Net.HttpStatusCode.Unauthorized 
                    || response.StatusCode == System.Net.HttpStatusCode.BadRequest
                    || response.StatusCode == System.Net.HttpStatusCode.Forbidden;
            }

            return new AuthBridgeResult(
                Success: false,
                AccessToken: null,
                RefreshToken: null,
                ExpiresAt: null,
                User: null,
                ErrorMessage: await TryReadErrorAsync(response, ct),
                ReauthRequired: reauthRequired,
                StatusCode: (int)response.StatusCode);
        }

        var authResponse = await ReadAuthBridgeResultAsync(response, ct);
        if (authResponse is null)
        {
            return new AuthBridgeResult(
                Success: false, 
                AccessToken: null, 
                RefreshToken: null, 
                ExpiresAt: null, 
                User: null, 
                ErrorMessage: "Authentication response could not be parsed.",
                ReauthRequired: false,
                StatusCode: (int)response.StatusCode);
        }

        return authResponse with { 
            Success = true, 
            ReauthRequired = false, 
            StatusCode = (int)response.StatusCode 
        };
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

    private async Task<string?> TryReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var content = await response.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(content))
        {
            return response.ReasonPhrase ?? $"HTTP {((int)response.StatusCode)}";
        }

        try
        {
            using var document = JsonDocument.Parse(content);

            // Prefer the machine-readable, localizable codes when present: resolve each to a string in the request
            // culture (already set by RequestLocalization) and substitute its param. Falls through to `detail`/
            // `errors`/`title` (English fallback) when no code maps or the array is absent.
            if (document.RootElement.TryGetProperty("errorCodes", out var errorCodes) && errorCodes.ValueKind == JsonValueKind.Array)
            {
                var localized = LocalizeErrorCodes(errorCodes);
                if (!string.IsNullOrWhiteSpace(localized))
                {
                    return localized;
                }
            }

            if (document.RootElement.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
            {
                return NormalizeValidationError(detail.GetString());
            }

            if (document.RootElement.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
            {
                return NormalizeValidationError(title.GetString());
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
                    return NormalizeValidationError(string.Join(" ", messages));
                }
            }
        }
        catch
        {
            return NormalizeValidationError(content);
        }

        return NormalizeValidationError(content);
    }

    private static string? NormalizeValidationError(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return message;
        }

        var clean = message.Replace("Validation failed:", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("\r", string.Empty)
            .Replace("\n", " ")
            .Replace("Severity: Error", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" --", " ")
            .Trim();

        while (clean.Contains("  ", StringComparison.Ordinal))
        {
            clean = clean.Replace("  ", " ", StringComparison.Ordinal);
        }

        return string.IsNullOrWhiteSpace(clean) ? message : clean;
    }

    // Resolve a backend `errorCodes` array (each item { "code": "password.x", "params": { "minLength": "10" } })
    // into a single localized, space-joined message using the current request culture. Returns null when no code
    // maps to a resx key (so the caller falls back to the English `detail`). A mapped-but-missing resx key is
    // skipped rather than rendered as the raw key name — that would be a defect.
    private string? LocalizeErrorCodes(JsonElement errorCodes)
    {
        var messages = new List<string>();
        foreach (var item in errorCodes.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object
                || !item.TryGetProperty("code", out var codeElement)
                || codeElement.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var code = codeElement.GetString();
            if (string.IsNullOrWhiteSpace(code) || !ErrorCodeResourceKeys.TryGetValue(code, out var resourceKey))
            {
                continue;
            }

            var localized = _localizer[resourceKey];
            if (localized.ResourceNotFound)
            {
                continue;
            }

            var paramValue = ExtractFirstParam(item);
            var message = paramValue is null ? localized.Value : SafeFormat(localized.Value, paramValue);
            if (!string.IsNullOrWhiteSpace(message))
            {
                messages.Add(message);
            }
        }

        return messages.Count > 0 ? string.Join(" ", messages) : null;
    }

    // Each password code carries at most one param (minLength / maxLength); take its value for the {0} placeholder.
    private static string? ExtractFirstParam(JsonElement item)
    {
        if (!item.TryGetProperty("params", out var parameters) || parameters.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var property in parameters.EnumerateObject())
        {
            return property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString(),
                JsonValueKind.Number => property.Value.GetRawText(),
                _ => property.Value.ToString()
            };
        }

        return null;
    }

    private static string SafeFormat(string template, string arg)
    {
        try
        {
            return string.Format(CultureInfo.CurrentCulture, template, arg);
        }
        catch (FormatException)
        {
            return template;
        }
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
