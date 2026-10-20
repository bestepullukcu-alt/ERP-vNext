using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.CrmService.Application.Features.VisitNotifications;
using Diten.CrmService.Infrastructure.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Diten.CrmService.Infrastructure.VisitNotifications;

/// <summary>
/// WP-VW-W5 (K-N1 / K-N8) — the Platform's internal event dispatch (<c>POST api/internal/notifications/events/dispatch</c>,
/// PLT-NOTIFY). It goes to the Platform DIRECTLY (<c>PlatformService:BaseUrl</c>, not the Gateway: an internal endpoint
/// is not on the Gateway's surface) with the shared internal API key (<c>AuthService:InternalApiKey</c>, the
/// <see cref="AuthUserDisplayNameClient"/> pattern) — so the scheduler, which has no request, can send. The caller's
/// token is NEVER forwarded.
/// <para>Transport (the W3L seat client's profile): a 3-second total budget, ONE transient retry (502 / 503 / 504,
/// 75 ms). It never throws into the caller: a missing key, a timeout, an error status or an unreadable answer is
/// "unavailable", and the row is retried by the scheduler.</para>
/// </summary>
public sealed class PlatformNotificationClient : IPlatformNotificationClient
{
    public const string DispatchPath = "api/internal/notifications/events/dispatch";
    private const string InternalApiKeyHeader = "X-Internal-Api-Key";
    private static readonly TimeSpan TotalTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(75);

    private readonly HttpClient _httpClient;
    private readonly string _internalApiKey;

    public PlatformNotificationClient(HttpClient httpClient, IConfiguration configuration, IOptions<AuthServiceOptions> auth)
    {
        _httpClient = httpClient;
        _internalApiKey = auth.Value.InternalApiKey ?? string.Empty;
        var baseUrl = configuration["PlatformService:BaseUrl"] ?? "http://localhost:5057";
        _httpClient.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    }

    public async Task<PlatformNotificationResult> DispatchAsync(PlatformNotificationRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_internalApiKey))
        {
            return PlatformNotificationResult.Unavailable("internal-api-key-missing");
        }

        var body = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["tenantId"] = request.TenantId,
            ["eventCode"] = request.EventCode,
            ["sourceModule"] = request.SourceModule,
            ["recipients"] = new { to = request.To.Select(Wire), cc = request.Cc.Select(Wire) },
            ["variables"] = request.Variables,
            ["correlationId"] = request.CorrelationId,
            ["causationId"] = request.CausationId
        };

        // W5-FIX2 (F-1) — the row id: a second call with it opens no second dispatch on the Platform.
        if (request.IdempotencyKey is { Length: > 0 } idempotencyKey)
        {
            body["idempotencyKey"] = idempotencyKey;
        }

        // W5-FIX (contract annex) — replyTo only with a user id; without one the field is not sent at all.
        if (request.ReplyToUserId is { Length: > 0 } replyTo)
        {
            body["replyTo"] = new { userId = replyTo };
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TotalTimeout);
        try
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                using var message = new HttpRequestMessage(HttpMethod.Post, DispatchPath) { Content = JsonContent.Create(body) };
                message.Headers.TryAddWithoutValidation(InternalApiKeyHeader, _internalApiKey);
                message.Headers.TryAddWithoutValidation("X-Tenant-Id", request.TenantId.ToString("D"));
                message.Headers.TryAddWithoutValidation("X-Correlation-Id", request.CorrelationId);
                using var response = await _httpClient.SendAsync(message, timeout.Token);
                if (IsTransient(response.StatusCode) && attempt == 0)
                {
                    await Task.Delay(RetryDelay, timeout.Token);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    return PlatformNotificationResult.Unavailable("platform-" + (int)response.StatusCode);
                }

                return Read(await response.Content.ReadAsStringAsync(timeout.Token));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // W5-FIX2 (F-1) — a time-out or a network error is UNCERTAIN: the Platform may have stored the mail. The row is
            // retried; the idempotency key makes the Platform answer the first result instead of opening a second dispatch.
            return PlatformNotificationResult.Unavailable(Diten.CrmService.Domain.Entities.VisitNotificationNotes.PlatformUncertain);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return PlatformNotificationResult.Unavailable("platform-unreadable");
        }

        return PlatformNotificationResult.Unavailable("platform-unreachable");
    }

    /// <summary>The answer (the Platform's envelope <c>{ data: … }</c>, or the bare object): the dispatch ids, the
    /// overall status and who was resolved how.</summary>
    public static PlatformNotificationResult Read(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind == JsonValueKind.Object && Prop(root, "isSuccessful") is { ValueKind: JsonValueKind.False })
        {
            return PlatformNotificationResult.Unavailable("platform-refused");
        }

        var data = Prop(root, "data") is { ValueKind: JsonValueKind.Object } d ? d : root;
        var status = Prop(data, "status") is { ValueKind: JsonValueKind.String } s ? s.GetString() : null;
        var ids = Prop(data, "dispatches") is { ValueKind: JsonValueKind.Array } arr
            ? arr.EnumerateArray().Select(x => Prop(x, "dispatchId")).Where(x => x is { ValueKind: JsonValueKind.String or JsonValueKind.Number })
                .Select(x => x!.Value.ToString()).ToList()
            : new List<string>();
        var resolved = Prop(data, "resolved");
        return new PlatformNotificationResult(true, status, ids, Resolved(resolved, "to"), Resolved(resolved, "cc"));
    }

    private static List<PlatformResolvedRecipient> Resolved(JsonElement? resolved, string name)
        => resolved is { ValueKind: JsonValueKind.Object } r && Prop(r, name) is { ValueKind: JsonValueKind.Array } list
            ? list.EnumerateArray().Select(x => new PlatformResolvedRecipient(
                Prop(x, "userId") is { ValueKind: JsonValueKind.String } u ? u.GetString() : null,
                Prop(x, "via") is { ValueKind: JsonValueKind.String } v ? v.GetString() ?? string.Empty : string.Empty)).ToList()
            : new List<PlatformResolvedRecipient>();

    private static JsonElement? Prop(JsonElement e, string name)
    {
        if (e.ValueKind != JsonValueKind.Object) return null;
        foreach (var p in e.EnumerateObject())
        {
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) return p.Value;
        }

        return null;
    }

    /// <summary>One recipient on the wire: only the field it carries.</summary>
    private static Dictionary<string, string> Wire(PlatformNotificationRecipient r)
    {
        var w = new Dictionary<string, string>(StringComparer.Ordinal);
        if (r.UserId is { } u) w["userId"] = u;
        if (r.LineManagerOf is { } m) w["lineManagerOf"] = m;
        if (r.Email is { } e) w["email"] = e;
        if (r.DisplayName is { } n) w["displayName"] = n;
        if (r.Locale is { } l) w["locale"] = l;
        return w;
    }

    private static bool IsTransient(HttpStatusCode status)
        => status is HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;
}

/// <summary>The Web links of the e-mails: <c>Crm:Notifications:WebBaseUrl</c> + the report page path.</summary>
public sealed class ConfigurationVisitNotificationLinks : IVisitNotificationLinks
{
    private readonly string _base;

    public ConfigurationVisitNotificationLinks(IConfiguration configuration)
        => _base = (configuration["Crm:Notifications:WebBaseUrl"] ?? "http://localhost:5001").TrimEnd('/');

    public string ReportUrl(Guid plannedVisitId) => _base + "/CRM/VisitReport?plannedVisitId=" + plannedVisitId.ToString("D");
}
