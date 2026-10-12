using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Artifacts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Diten.CrmService.Infrastructure.Artifacts;

/// <summary>
/// WP-VW-W3 (BE-a, K-W3) — the visit evidence (start photo, doctor signature) in the MOD-0262-FU01 document repository,
/// scope <c>CrmVisitEvidence</c>, over the Gateway as the SIGNED-IN user (<see cref="DocumentRepositoryCallerContext"/>):
/// the Platform's scoped keys (<c>visit-evidence.write / read / read-all</c>) decide, so a rep stores and reads only their
/// own evidence. Owning item = the planned visit, owning version = the execution.
/// <para>⛔ Fail-closed store: any non-2xx or transport failure throws <see cref="ContentArtifactStoreException"/>. A read
/// that answers 404 (absent, another tenant's, or not this caller's) is <c>null</c>. Compensation never throws — a
/// failure is logged and the orphan stays for the repository's own sweep.</para>
/// </summary>
public sealed class HttpVisitEvidenceStore : IVisitEvidenceStore
{
    public const string Scope = "CrmVisitEvidence";

    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<HttpVisitEvidenceStore> _logger;

    public HttpVisitEvidenceStore(
        HttpClient httpClient, IConfiguration configuration, IHttpContextAccessor httpContextAccessor,
        ITenantContext tenantContext, ILogger<HttpVisitEvidenceStore> logger)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _tenantContext = tenantContext;
        _logger = logger;
        _httpClient.BaseAddress = DocumentRepositoryCallerContext.GatewayBase(configuration);
    }

    public async Task<ContentArtifactStoreResult> StoreAsync(VisitEvidenceStoreRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(request.Content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(request.MediaType);
        form.Add(fileContent, "file", request.FileName);
        form.Add(new StringContent(request.PlannedVisitId.ToString("D")), "owningItemId");
        form.Add(new StringContent(request.ExecutionId.ToString("D")), "owningVersionId");
        form.Add(new StringContent(DocumentRepositoryCallerContext.ResolveCompanyId(_httpContextAccessor).ToString("D")), "companyId");
        form.Add(new StringContent(Scope), "scope");

        HttpResponseMessage response;
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, DocumentRepositoryCallerContext.ObjectsPath) { Content = form };
            DocumentRepositoryCallerContext.Forward(message, _httpContextAccessor, _tenantContext);
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
                    (int)response.StatusCode, $"The document repository rejected the evidence upload ({(int)response.StatusCode}).");
            }

            GatewayEnvelope<RepositoryObjectPayload>? envelope;
            try
            {
                envelope = await response.Content.ReadFromJsonAsync<GatewayEnvelope<RepositoryObjectPayload>>(cancellationToken: cancellationToken);
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException)
            {
                throw new ContentArtifactStoreException(502, "The document repository returned an unreadable response.");
            }

            if (envelope?.IsSuccessful != true || envelope.Data is null || envelope.Data.ContentId == Guid.Empty)
            {
                throw new ContentArtifactStoreException(502, "The document repository returned no stored evidence.");
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
            using var message = new HttpRequestMessage(HttpMethod.Get, $"{DocumentRepositoryCallerContext.ObjectsPath}/{contentId:D}/content");
            DocumentRepositoryCallerContext.Forward(message, _httpContextAccessor, _tenantContext);
            response = await _httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new ContentArtifactStoreException(503, "The document repository is unavailable.");
        }

        using (response)
        {
            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
            {
                return null; // absent, another tenant's, or not this caller's evidence — non-leakage.
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new ContentArtifactStoreException(
                    (int)response.StatusCode, $"The document repository could not read the evidence ({(int)response.StatusCode}).");
            }

            // Buffer the (≤ 10 MB) image so the stream outlives the HttpResponseMessage the caller never sees.
            var buffer = new MemoryStream();
            await response.Content.CopyToAsync(buffer, cancellationToken);
            buffer.Position = 0;
            var mediaType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
            return new ContentArtifactReadResult(buffer, mediaType, "evidence", buffer.Length);
        }
    }

    public async Task CompensateAsync(Guid contentId, CancellationToken cancellationToken)
    {
        if (contentId == Guid.Empty)
        {
            return;
        }

        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, $"{DocumentRepositoryCallerContext.ObjectsPath}/{contentId:D}/compensate");
            DocumentRepositoryCallerContext.Forward(message, _httpContextAccessor, _tenantContext);
            using var response = await _httpClient.SendAsync(message, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Visit evidence compensation of {ContentId} answered {StatusCode}; the orphan stays for the repository sweep.",
                    contentId, (int)response.StatusCode);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Visit evidence compensation of {ContentId} failed; the orphan stays for the repository sweep.", contentId);
        }
    }

    private sealed record GatewayEnvelope<T>(T? Data, int StatusCode, bool IsSuccessful, IReadOnlyList<string>? Errors);

    private sealed record RepositoryObjectPayload(Guid ContentId, string? Checksum, long ByteSize, string? MediaType);
}
