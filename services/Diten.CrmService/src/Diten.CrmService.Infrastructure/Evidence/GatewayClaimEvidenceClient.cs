using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.CrmService.Application.Features.ContentComposition.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Diten.CrmService.Infrastructure.Evidence;

/// <summary>
/// WP-CL-BE-5 — MOD-0031 over the Gateway (<c>/api/v1/evidence/*</c>, WP-CL-BE-2 contract + the BE-5 bulk read),
/// forwarding the CALLER's <c>Authorization</c> and <c>X-Tenant-Id</c> exactly like <c>GatewayClaimWorkflowClient</c>.
/// No service-token path: MOD-0031 re-checks that the caller can read the document.
/// <para>Outcome mapping: 2xx → ok; 404 → not found; 401/403 → forbidden; 5xx, timeout or network failure →
/// unavailable; any other 4xx → rejected with MOD-0031's reason code and message.</para>
/// </summary>
public sealed class GatewayClaimEvidenceClient : IClaimEvidenceClient
{
    private const string TenantHeaderName = "X-Tenant-Id";
    private const string AuthorizationHeaderName = "Authorization";
    private const int QueryBatchSize = 100;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<GatewayClaimEvidenceClient> _logger;

    public GatewayClaimEvidenceClient(HttpClient http, IConfiguration configuration,
        IHttpContextAccessor httpContextAccessor, ILogger<GatewayClaimEvidenceClient> logger)
    {
        _http = http;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _http.BaseAddress ??= new Uri(configuration["Gateway:BaseUrl"] ?? "http://localhost:5000");
    }

    public async Task<ClaimEvidenceCallResult<ClaimEvidenceLink>> LinkAsync(
        ClaimEvidenceObjectRef objectRef, ClaimEvidenceLinkInput input, CancellationToken ct)
    {
        var body = new
        {
            objectRef = new
            {
                module = objectRef.Module,
                objectType = objectRef.ObjectType,
                objectId = objectRef.ObjectId,
                objectVersion = objectRef.ObjectVersion
            },
            documentKind = input.DocumentKind,
            documentId = input.DocumentId,
            documentVersionId = input.DocumentVersionId,
            evidenceTypeCode = input.EvidenceTypeCode,
            locator = input.Locator is null
                ? null
                : new { quote = input.Locator.Quote, section = input.Locator.Section, page = input.Locator.Page, table = input.Locator.Table },
            supportedSpans = input.SupportedSpans?.Select(s => new { languageCode = s.LanguageCode, text = s.Text, start = s.Start, end = s.End })
        };
        return Single(await SendAsync(HttpMethod.Post, "/api/v1/evidence/links", body, ct));
    }

    public async Task<ClaimEvidenceCallResult<ClaimEvidenceLink>> GetAsync(Guid linkId, CancellationToken ct)
        => Single(await SendAsync(HttpMethod.Get, $"/api/v1/evidence/links/{linkId:D}", null, ct));

    public async Task<ClaimEvidenceCallResult<ClaimEvidenceLink>> RemoveAsync(Guid linkId, string? reason, CancellationToken ct)
        => Single(await SendAsync(HttpMethod.Post, $"/api/v1/evidence/links/{linkId:D}/remove", new { reason }, ct));

    public async Task<IReadOnlyList<ClaimEvidenceLink>?> QueryAsync(
        IReadOnlyList<ClaimEvidenceObjectRef> objects, bool includeRemoved, CancellationToken ct)
    {
        var all = new List<ClaimEvidenceLink>();
        foreach (var chunk in objects.Chunk(QueryBatchSize))
        {
            var body = new
            {
                objects = chunk.Select(o => new
                {
                    module = o.Module, objectType = o.ObjectType, objectId = o.ObjectId, objectVersion = o.ObjectVersion
                }),
                includeRemoved
            };
            var reply = await SendAsync(HttpMethod.Post, "/api/v1/evidence/links/query", body, ct);
            if (reply.Transport is not null || reply.Status is < 200 or >= 300
                || reply.Data is not { ValueKind: JsonValueKind.Array } rows)
            {
                return null;
            }

            foreach (var row in rows.EnumerateArray())
            {
                if (row.TryGetProperty("links", out var links) && links.ValueKind == JsonValueKind.Array)
                {
                    all.AddRange(links.EnumerateArray().Select(Parse).OfType<ClaimEvidenceLink>());
                }
            }
        }

        // A link can answer two overlapping object keys; keep one copy.
        return all.GroupBy(l => l.LinkId).Select(g => g.First()).ToList();
    }

    public async Task<ClaimEvidenceCallResult<IReadOnlyList<ClaimEvidenceDocumentOptionDto>>> GetDocumentOptionsAsync(
        string? search, string? kind, CancellationToken ct)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query.Add($"search={Uri.EscapeDataString(search.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(kind))
        {
            query.Add($"kind={Uri.EscapeDataString(kind.Trim())}");
        }

        var path = "/api/v1/evidence/document-options" + (query.Count == 0 ? string.Empty : "?" + string.Join('&', query));
        var reply = await SendAsync(HttpMethod.Get, path, null, ct);
        var outcome = Outcome(reply);
        if (outcome != ClaimEvidenceCallOutcome.Ok || reply.Data is not { ValueKind: JsonValueKind.Array } rows)
        {
            return new ClaimEvidenceCallResult<IReadOnlyList<ClaimEvidenceDocumentOptionDto>>(
                outcome == ClaimEvidenceCallOutcome.Ok ? ClaimEvidenceCallOutcome.Unavailable : outcome, null,
                reply.Status, reply.ReasonCode, reply.Message);
        }

        var options = rows.Deserialize<List<ClaimEvidenceDocumentOptionDto>>(Json) ?? [];
        return new ClaimEvidenceCallResult<IReadOnlyList<ClaimEvidenceDocumentOptionDto>>(
            ClaimEvidenceCallOutcome.Ok, options, reply.Status, null, null);
    }

    private static ClaimEvidenceCallResult<ClaimEvidenceLink> Single(Reply reply)
    {
        var outcome = Outcome(reply);
        if (outcome == ClaimEvidenceCallOutcome.Ok)
        {
            return reply.Data is { ValueKind: JsonValueKind.Object } data && Parse(data) is { } link
                ? new ClaimEvidenceCallResult<ClaimEvidenceLink>(ClaimEvidenceCallOutcome.Ok, link, reply.Status, null, null)
                // a 2xx without a link is not an answer
                : new ClaimEvidenceCallResult<ClaimEvidenceLink>(ClaimEvidenceCallOutcome.Unavailable, null, reply.Status,
                    null, null);
        }

        return new ClaimEvidenceCallResult<ClaimEvidenceLink>(outcome, null, reply.Status, reply.ReasonCode, reply.Message);
    }

    private static ClaimEvidenceCallOutcome Outcome(Reply reply) => reply.Transport is not null
        ? ClaimEvidenceCallOutcome.Unavailable
        : reply.Status switch
        {
            >= 200 and < 300 => ClaimEvidenceCallOutcome.Ok,
            404 => ClaimEvidenceCallOutcome.NotFound,
            401 or 403 => ClaimEvidenceCallOutcome.Forbidden,
            >= 500 => ClaimEvidenceCallOutcome.Unavailable,
            _ => ClaimEvidenceCallOutcome.Rejected
        };

    private sealed record WireObjectRef(string? Module, string? ObjectType, string? ObjectId, string? ObjectVersion);

    private sealed record WireLocator(string? Section, string? Page, string? Table, string? Quote);

    private sealed record WireSpan(string? LanguageCode, string? Text, int? Start, int? End);

    private sealed record WireLink(
        Guid LinkId,
        WireObjectRef? ObjectRef,
        string? DocumentKind,
        Guid DocumentId,
        Guid? DocumentVersionId,
        string? DocumentVersionLabel,
        string? DocumentTitle,
        string? EvidenceTypeCode,
        WireLocator? Locator,
        List<WireSpan>? SupportedSpans,
        string? Status,
        string? LinkedBy,
        DateTimeOffset LinkedAt,
        string? RemovedBy,
        DateTimeOffset? RemovedAt,
        string? RemovalReason,
        Guid? CurrentVersionId,
        string? CurrentVersionLabel,
        bool IsSuperseded,
        string? DocumentState,
        DateTimeOffset? ReviewDueAt,
        string? DocumentCode = null);

    private static ClaimEvidenceLink? Parse(JsonElement element)
    {
        WireLink? w;
        try
        {
            w = element.Deserialize<WireLink>(Json);
        }
        catch (JsonException)
        {
            return null;
        }

        if (w is null || w.LinkId == Guid.Empty || w.ObjectRef?.ObjectId is null)
        {
            return null;
        }

        return new ClaimEvidenceLink(
            w.LinkId,
            new ClaimEvidenceObjectRef(w.ObjectRef.Module ?? string.Empty, w.ObjectRef.ObjectType ?? string.Empty,
                w.ObjectRef.ObjectId, w.ObjectRef.ObjectVersion),
            w.DocumentKind ?? string.Empty,
            w.DocumentId,
            w.DocumentVersionId,
            w.DocumentVersionLabel,
            w.DocumentTitle ?? string.Empty,
            w.EvidenceTypeCode ?? string.Empty,
            new ClaimEvidenceLocator(w.Locator?.Section, w.Locator?.Page, w.Locator?.Table, w.Locator?.Quote),
            (w.SupportedSpans ?? []).Where(s => s.LanguageCode is not null && s.Text is not null)
                .Select(s => new ClaimEvidenceSpan(s.LanguageCode!, s.Text!, s.Start, s.End)).ToList(),
            w.Status ?? string.Empty,
            w.LinkedBy,
            w.LinkedAt,
            w.RemovedBy,
            w.RemovedAt,
            w.RemovalReason,
            w.CurrentVersionId,
            w.CurrentVersionLabel,
            w.IsSuperseded,
            w.DocumentState,
            w.ReviewDueAt,
            w.DocumentCode);
    }

    private sealed record Reply(int Status, JsonElement? Data, string? ReasonCode, string? Message, string? Transport);

    private async Task<Reply> SendAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(method, path);
            if (body is not null)
            {
                request.Content = JsonContent.Create(body, options: Json);
            }

            ForwardContextHeaders(request);
            using var response = await _http.SendAsync(request, ct);
            var status = (int)response.StatusCode;
            var text = await response.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(text))
            {
                return new Reply(status, null, null, null, null);
            }

            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            JsonElement? data = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var d)
                ? d.Clone()
                : null;
            var reason = root.ValueKind == JsonValueKind.Object ? Text(root, "reason_code") ?? Text(root, "reasonCode") : null;
            string? message = null;
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("errors", out var errors)
                && errors.ValueKind == JsonValueKind.Array)
            {
                message = string.Join("; ", errors.EnumerateArray()
                    .Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()));
            }

            return new Reply(status, data, reason, message, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException
                                       && !ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "claims.evidence.call_failed Method={Method} Path={Path}", method, path);
            return new Reply((int)HttpStatusCode.ServiceUnavailable, null, null, null, ex.GetType().Name);
        }
    }

    private void ForwardContextHeaders(HttpRequestMessage request)
    {
        var context = _httpContextAccessor.HttpContext;
        if (context is null)
        {
            return;
        }

        if (context.Request.Headers.TryGetValue(TenantHeaderName, out var tenant) && !string.IsNullOrWhiteSpace(tenant))
        {
            request.Headers.TryAddWithoutValidation(TenantHeaderName, tenant.ToString());
        }

        if (context.Request.Headers.TryGetValue(AuthorizationHeaderName, out var auth) && !string.IsNullOrWhiteSpace(auth))
        {
            request.Headers.TryAddWithoutValidation(AuthorizationHeaderName, auth.ToString());
        }
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
