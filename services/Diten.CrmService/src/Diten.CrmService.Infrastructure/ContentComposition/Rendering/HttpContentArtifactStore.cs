using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.ContentComposition.ContentSetRevisions.Rendering;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Diten.CrmService.Infrastructure.ContentComposition.Rendering;

/// <summary>
/// SCMM-16B (CAND-CAP-0011, SCMM-16) — stores/reads a rendered artifact through the MOD-0262-FU01 document repository
/// (<c>/api/v1/document-repository</c>) over the Gateway. Mirrors the CRM cross-service transport verbatim
/// (<see cref="Diten.CrmService.Infrastructure.CyclePeriod.MdmCyclePeriodLegalEntityValidator"/> /
/// <c>HttpCrmAuditPublisher</c>): <c>Gateway:BaseUrl</c>, the caller's <c>Authorization</c> / <c>X-Tenant-Id</c> /
/// <c>X-Correlation-Id</c> forwarded so FU01 authorises and tenant-scopes as the same user. TenantId/CompanyId come from
/// that forwarded context, never from a payload.
/// <para>
/// ⛔ <b>Fail-closed.</b> Unlike the fail-soft audit client, a non-2xx upload throws
/// <see cref="ContentArtifactStoreException"/>; the render command then fails and binds no artifact. Download is
/// non-leaking: a 404 (absent, or another tenant's content id) returns <c>null</c>.
/// </para>
/// </summary>
public sealed class HttpContentArtifactStore : IContentArtifactStore
{
    private const string ObjectsPath = "api/v1/document-repository/objects";
    private const string Scope = "ContentMessagingArtifacts";

    private static readonly string[] CompanyClaimTypes =
        ["companyId", "company_id", "legalEntityId", "legal_entity_id"];

    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ITenantContext _tenantContext;

    public HttpContentArtifactStore(
        HttpClient httpClient,
        IConfiguration configuration,
        IHttpContextAccessor httpContextAccessor,
        ITenantContext tenantContext)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _tenantContext = tenantContext;

        var gatewayBaseUrl = configuration["Gateway:BaseUrl"]
            ?? configuration["GatewayUrl"]
            ?? "http://localhost:5000";
        _httpClient.BaseAddress = new Uri(gatewayBaseUrl.TrimEnd('/') + "/");
    }

    public async Task<ContentArtifactStoreResult> StoreAsync(
        ContentArtifactStoreRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(request.Content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(request.MediaType);
        form.Add(fileContent, "file", request.FileName);
        form.Add(new StringContent(request.OwningItemId.ToString("D")), "owningItemId");
        form.Add(new StringContent(request.OwningVersionId.ToString("D")), "owningVersionId");
        form.Add(new StringContent(ResolveCompanyId().ToString("D")), "companyId");
        form.Add(new StringContent(Scope), "scope");

        HttpResponseMessage response;
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, ObjectsPath) { Content = form };
            ForwardContextHeaders(message);
            response = await _httpClient.SendAsync(message, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new ContentArtifactStoreException(503, "The document repository is unavailable.");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new ContentArtifactStoreException(
                    (int)response.StatusCode,
                    $"The document repository rejected the artifact upload ({(int)response.StatusCode}).");
            }

            GatewayEnvelope<RepositoryObjectPayload>? envelope;
            try
            {
                envelope = await response.Content.ReadFromJsonAsync<GatewayEnvelope<RepositoryObjectPayload>>(
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException)
            {
                throw new ContentArtifactStoreException(502, "The document repository returned an unreadable response.");
            }

            if (envelope?.IsSuccessful != true || envelope.Data is null || envelope.Data.ContentId == Guid.Empty)
            {
                throw new ContentArtifactStoreException(502, "The document repository returned no stored artifact.");
            }

            var data = envelope.Data;
            return new ContentArtifactStoreResult(data.ContentId, data.Checksum ?? string.Empty, data.ByteSize,
                string.IsNullOrWhiteSpace(data.MediaType) ? request.MediaType : data.MediaType);
        }
    }

    public async Task<ContentArtifactReadResult?> OpenReadAsync(Guid contentId, CancellationToken cancellationToken)
    {
        if (contentId == Guid.Empty)
        {
            return null;
        }

        HttpResponseMessage response;
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Get, $"{ObjectsPath}/{contentId:D}/content");
            ForwardContextHeaders(message);
            response = await _httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new ContentArtifactStoreException(503, "The document repository is unavailable.");
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null; // absent, or another tenant's content id — non-leakage.
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new ContentArtifactStoreException(
                    (int)response.StatusCode,
                    $"The document repository could not read the artifact ({(int)response.StatusCode}).");
            }

            // Buffer the (small) PDF so the stream outlives the HttpResponseMessage the caller never sees.
            var buffer = new MemoryStream();
            await response.Content.CopyToAsync(buffer, cancellationToken);
            buffer.Position = 0;

            var mediaType = response.Content.Headers.ContentType?.MediaType ?? "application/pdf";
            var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                ?? $"{contentId:D}.pdf";

            return new ContentArtifactReadResult(buffer, mediaType, fileName, buffer.Length);
        }
    }

    // CompanyId is server-derived: the first company claim on the caller's token, or Guid.Empty for a tenant-scoped
    // artifact with no company context. Never taken from a request payload.
    private Guid ResolveCompanyId()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user is null)
        {
            return Guid.Empty;
        }

        foreach (var type in CompanyClaimTypes)
        {
            var value = user.FindFirstValue(type);
            if (Guid.TryParse(value, out var companyId) && companyId != Guid.Empty)
            {
                return companyId;
            }
        }

        return Guid.Empty;
    }

    private void ForwardContextHeaders(HttpRequestMessage request)
    {
        var context = _httpContextAccessor.HttpContext;

        var authorization = context?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authorization)
            && AuthenticationHeaderValue.TryParse(authorization, out var parsed))
        {
            request.Headers.Authorization = parsed;
        }

        var tenant = context?.Request.Headers["X-Tenant-Id"].ToString();
        if (string.IsNullOrWhiteSpace(tenant) && _tenantContext.TenantId is { } tenantId)
        {
            tenant = tenantId.ToString();
        }

        if (!string.IsNullOrWhiteSpace(tenant))
        {
            request.Headers.TryAddWithoutValidation("X-Tenant-Id", tenant);
        }

        var correlation = context?.Request.Headers["X-Correlation-Id"].ToString();
        if (string.IsNullOrWhiteSpace(correlation))
        {
            correlation = context?.TraceIdentifier;
        }

        if (!string.IsNullOrWhiteSpace(correlation))
        {
            request.Headers.TryAddWithoutValidation("X-Correlation-Id", correlation);
        }
    }

    private sealed record GatewayEnvelope<T>(T? Data, int StatusCode, bool IsSuccessful, IReadOnlyList<string>? Errors);

    private sealed record RepositoryObjectPayload(Guid ContentId, string? Checksum, long ByteSize, string? MediaType);
}
