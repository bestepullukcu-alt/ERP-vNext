using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace Diten.Web.Services;

public sealed class PlatformProfileSnapshotProvider : IPlatformProfileSnapshotProvider
{
    private const string HttpContextItemKey = "Diten.PlatformProfileSnapshot";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly string _gatewayUrl;
    private readonly ILogger<PlatformProfileSnapshotProvider> _logger;

    public PlatformProfileSnapshotProvider(
        IHttpContextAccessor httpContextAccessor,
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        IConfiguration configuration,
        ILogger<PlatformProfileSnapshotProvider> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _gatewayUrl = configuration["GatewayUrl"] ?? "http://localhost:5000";
        _logger = logger;
    }

    public async Task<PlatformProfileSnapshot> GetCurrentAsync(CancellationToken ct = default)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return BuildFallback(null);
        }

        if (httpContext.Items.TryGetValue(HttpContextItemKey, out var cached) && cached is PlatformProfileSnapshot existing)
        {
            return existing;
        }

        var snapshot = await ResolveAsync(httpContext, ct);
        httpContext.Items[HttpContextItemKey] = snapshot;
        return snapshot;
    }

    public void InvalidateCurrent()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return;
        }

        httpContext.Items.Remove(HttpContextItemKey);

        var userId = ReadUserId(httpContext.User);
        if (!string.IsNullOrWhiteSpace(userId))
        {
            _cache.Remove(BuildCacheKey(userId));
        }
    }

    private async Task<PlatformProfileSnapshot> ResolveAsync(HttpContext httpContext, CancellationToken ct)
    {
        var principal = httpContext.User;
        if (principal.Identity?.IsAuthenticated != true)
        {
            return BuildFallback(principal);
        }

        var userId = ReadUserId(principal);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return BuildFallback(principal);
        }

        var cacheKey = BuildCacheKey(userId);
        if (_cache.TryGetValue(cacheKey, out PlatformProfileSnapshot? cached) && cached is not null)
        {
            return cached;
        }

        var snapshot = await FetchFromApiAsync(httpContext, principal, ct)
                       ?? BuildFallback(principal);

        _cache.Set(cacheKey, snapshot, CacheTtl);
        return snapshot;
    }

    private async Task<PlatformProfileSnapshot?> FetchFromApiAsync(HttpContext httpContext, ClaimsPrincipal principal, CancellationToken ct)
    {
        var accessToken = Auth.AuthTokenCookies.GetAccessToken(httpContext.Request);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return null;
        }

        try
        {
            using var client = _httpClientFactory.CreateClient();

            HttpRequestMessage BuildRequest()
            {
                var message = new HttpRequestMessage(HttpMethod.Get, $"{_gatewayUrl}/api/platform/account/me");
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                return message;
            }

            /*
             * BL-294/nav — MEASURED DECISION: this consumer gets the silent retry, and NOT the warning.
             *
             * It falls over at the same moment the sidebar and Ctrl+K do, but it does not fail the same way. Its
             * fallback is REAL data — display name, email, actor type and roles read straight off the JWT — so the
             * profile card stays correct and merely loses the server-side extras. There is nothing for the user to
             * act on, and a "could not load" banner on a card that is showing their own correct name would be noise.
             * The sidebar and the palette fall back to NOTHING, which is why only they warn.
             *
             * The retry still earns its place here: a failed fetch is cached for five minutes (CacheTtl), so one
             * momentary blip would otherwise degrade the card long after the endpoint recovered.
             */
            using var response = await Http.NavigationRetry.SendOnceMoreOnTransientAsync(client, BuildRequest, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug(
                    "Platform profile snapshot fetch returned {StatusCode}; falling back to JWT claims.",
                    (int)response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(ct);
            return ParseSnapshot(content, principal);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Platform profile snapshot fetch failed; falling back to JWT claims.");
            return null;
        }
    }

    private static PlatformProfileSnapshot? ParseSnapshot(string content, ClaimsPrincipal principal)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;
        var data = root.TryGetProperty("data", out var d)
            ? d
            : (root.TryGetProperty("Data", out var d2) ? d2 : root);

        if (data.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var displayName = ReadString(data, "displayName", "DisplayName");
        var email = ReadString(data, "email", "Email") ?? principal.FindFirst(ClaimTypes.Email)?.Value;
        var actorType = ReadString(data, "actorType", "ActorType") ?? principal.FindFirst("actor_type")?.Value;
        var firstName = ReadString(data, "firstName", "FirstName");
        var lastName = ReadString(data, "lastName", "LastName");
        var roles = ReadRoles(data) ?? principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = JoinNames(firstName, lastName) ?? EmailLocalPart(email) ?? "User";
        }

        return new PlatformProfileSnapshot(
            displayName,
            ComputeInitials(displayName, email),
            email,
            actorType,
            firstName,
            lastName,
            roles);
    }

    private static PlatformProfileSnapshot BuildFallback(ClaimsPrincipal? principal)
    {
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return new PlatformProfileSnapshot("User", "U", null, null, null, null, Array.Empty<string>());
        }

        var firstName = principal.FindFirst(ClaimTypes.GivenName)?.Value;
        var lastName = principal.FindFirst(ClaimTypes.Surname)?.Value;
        var email = principal.FindFirst(ClaimTypes.Email)?.Value ?? principal.FindFirst("email")?.Value;
        var actorType = principal.FindFirst("actor_type")?.Value;
        var nameClaim = principal.FindFirst(ClaimTypes.Name)?.Value ?? principal.FindFirst("name")?.Value;
        var roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

        var displayName = nameClaim
                          ?? JoinNames(firstName, lastName)
                          ?? EmailLocalPart(email)
                          ?? "User";

        return new PlatformProfileSnapshot(
            displayName,
            ComputeInitials(displayName, email),
            email,
            actorType,
            firstName,
            lastName,
            roles);
    }

    private static string? JoinNames(string? firstName, string? lastName)
    {
        var parts = new[] { firstName, lastName }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();
        return parts.Length == 0 ? null : string.Join(' ', parts);
    }

    private static string? EmailLocalPart(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var atIndex = email.IndexOf('@', StringComparison.Ordinal);
        return atIndex > 0 ? email[..atIndex] : email;
    }

    private static string ComputeInitials(string displayName, string? email)
    {
        var source = string.IsNullOrWhiteSpace(displayName)
            ? (EmailLocalPart(email) ?? "User")
            : displayName;

        var parts = source
            .Split(new[] { ' ', '.', '_', '-' }, StringSplitOptions.RemoveEmptyEntries)
            .Take(2)
            .Where(part => part.Length > 0)
            .Select(part => char.ToUpperInvariant(part[0]))
            .ToArray();

        return parts.Length == 0 ? "U" : new string(parts);
    }

    private static string? ReadString(JsonElement element, params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            if (element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String)
            {
                var value = property.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        return null;
    }

    private static List<string>? ReadRoles(JsonElement data)
    {
        foreach (var name in new[] { "roles", "Roles" })
        {
            if (data.TryGetProperty(name, out var rolesProperty) && rolesProperty.ValueKind == JsonValueKind.Array)
            {
                return rolesProperty.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString() ?? string.Empty)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .ToList();
            }
        }

        return null;
    }

    private static string? ReadUserId(ClaimsPrincipal principal)
    {
        return principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? principal.FindFirst("sub")?.Value;
    }

    private static string BuildCacheKey(string userId) => $"platform-profile-snapshot:{userId}";
}
