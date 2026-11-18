using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Diten.Platform.Application.Contracts.RecordCodes;
using Diten.Platform.Domain.Features.RecordCodes;
using Diten.Platform.Infrastructure.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Infrastructure.Services.Mdm;

/// <summary>
/// MOD-0040-FU02 §8.3 — <c>GET {MdmService:BaseUrl}/api/legal-entities/{id}/lookup-validation</c> with
/// <c>X-Tenant-Id</c> = the token's tenant and the forwarded user token. One 3-second budget (the
/// <c>WorkingCalendarLegalEntityValidator</c> precedent), no retry, no cache. The answer is classified by the pack's table;
/// the code is taken EXACTLY as MDM wrote it — never trimmed or upper-cased. <c>MdmLegalEntityReferenceValidator</c>
/// (MOD-0288's) is not touched.
/// </summary>
public sealed class MdmLegalEntityShortCodeReader : ILegalEntityShortCodeReader
{
    public static readonly TimeSpan TotalTimeout = TimeSpan.FromSeconds(3);

    private static readonly HashSet<string> NotEligibleStatuses = new(StringComparer.Ordinal) { "DRAFT", "SUBMITTED", "REJECTED" };

    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public MdmLegalEntityShortCodeReader(
        HttpClient httpClient,
        IOptions<MdmServiceOptions> options,
        IHttpContextAccessor httpContextAccessor)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        if (string.IsNullOrWhiteSpace(options?.Value.BaseUrl))
        {
            throw new InvalidOperationException("Configuration error: 'MdmService:BaseUrl' is missing.");
        }

        _httpClient.BaseAddress = new Uri(options.Value.BaseUrl.TrimEnd('/') + "/");
    }

    public async Task<LegalEntityShortCodeResult> ReadAsync(
        Guid tenantId,
        Guid legalEntityId,
        CancellationToken cancellationToken = default)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TotalTimeout);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/legal-entities/{legalEntityId:D}/lookup-validation");
            request.Headers.TryAddWithoutValidation("X-Tenant-Id", tenantId.ToString("D"));
            var authorization = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
            if (!string.IsNullOrWhiteSpace(authorization) && AuthenticationHeaderValue.TryParse(authorization, out var parsed))
            {
                request.Headers.Authorization = parsed;
            }

            using var response = await _httpClient.SendAsync(request, budget.Token);
            switch (response.StatusCode)
            {
                case HttpStatusCode.NotFound:
                    return LegalEntityShortCodeResult.Of(LegalEntityShortCodeOutcome.NotFound, "MDM 404");
                case HttpStatusCode.Forbidden:
                    return LegalEntityShortCodeResult.Of(LegalEntityShortCodeOutcome.ReadPermissionMissing, "MDM 403");
                case HttpStatusCode.OK:
                    break;
                default:
                    return LegalEntityShortCodeResult.Of(LegalEntityShortCodeOutcome.Unavailable, $"MDM {(int)response.StatusCode}");
            }

            var body = await response.Content.ReadAsStringAsync(budget.Token);
            return Classify(body, legalEntityId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or IOException)
        {
            return LegalEntityShortCodeResult.Of(LegalEntityShortCodeOutcome.Unavailable, "MDM unreachable or past its 3 s budget");
        }
    }

    private static LegalEntityShortCodeResult Classify(string body, Guid legalEntityId)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            return Unavailable("unreadable body");
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("data", out var data)
                || data.ValueKind != JsonValueKind.Object)
            {
                return Unavailable("no data");
            }

            if (!data.TryGetProperty("legalEntityId", out var id)
                || id.ValueKind != JsonValueKind.String
                || !Guid.TryParse(id.GetString(), out var answeredId)
                || answeredId != legalEntityId)
            {
                return Unavailable("another legal entity answered");
            }

            if (!data.TryGetProperty("referenceable", out var referenceable)
                || referenceable.ValueKind != JsonValueKind.True
                || !data.TryGetProperty("lifecycleState", out var lifecycle)
                || lifecycle.ValueKind != JsonValueKind.String
                || !string.Equals(lifecycle.GetString(), "ACTIVE", StringComparison.Ordinal))
            {
                return Unavailable("a 200 that is not an active, referenceable legal entity");
            }

            // lookup-validation 1.0.0 has no approvalStatus: absent → the consumer's own pre-check (TC-3). Present → judged.
            if (data.TryGetProperty("approvalStatus", out var approval))
            {
                var status = approval.ValueKind == JsonValueKind.String ? approval.GetString() : null;
                if (NotEligibleStatuses.Contains(status ?? string.Empty))
                {
                    return LegalEntityShortCodeResult.Of(LegalEntityShortCodeOutcome.NotEligible, $"approvalStatus {status}");
                }

                if (!string.Equals(status, "APPROVED", StringComparison.Ordinal))
                {
                    return Unavailable("approvalStatus empty or outside the four values");
                }
            }

            var code = data.TryGetProperty("code", out var codeElement) && codeElement.ValueKind == JsonValueKind.String
                ? codeElement.GetString()
                : null;
            return RecordCodeFormat.IsValidPrefix(code)
                ? LegalEntityShortCodeResult.Found(code!)
                : LegalEntityShortCodeResult.Of(LegalEntityShortCodeOutcome.ShortCodeInvalid, "short code does not match ^[A-Z]{2,5}$");
        }
    }

    private static LegalEntityShortCodeResult Unavailable(string reason) =>
        LegalEntityShortCodeResult.Of(LegalEntityShortCodeOutcome.Unavailable, reason);
}
