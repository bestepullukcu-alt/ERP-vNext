using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.CyclePeriod.Read;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Diten.CrmService.Infrastructure.CyclePeriod;

/// <summary>
/// MOD-0165 FU07 — the tenant's referenceable MDM legal entities, for the scope selector.
/// <para><b>An authoring lookup, never a validation.</b> Choosing from this list does not make a legal entity
/// referenceable: <see cref="MdmCyclePeriodLegalEntityValidator"/> proves that per id, immediately before persistence.
/// The list can be seconds out of date, and a period must not be scoped to an entity deactivated while the form was
/// open.</para>
/// <para>Unreachable is reported as unreachable rather than as an empty tenant, so the UI can say "we could not load
/// these" instead of "there are none". No hardcoded fallback list exists (PSS-LOOKUPS-001).</para>
/// </summary>
public sealed class MdmCyclePeriodLegalEntityCatalog : ICyclePeriodLegalEntityCatalog
{
    private static readonly TimeSpan TotalTimeout = TimeSpan.FromSeconds(3);

    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<MdmCyclePeriodLegalEntityCatalog> _logger;
    private readonly string _path;

    public MdmCyclePeriodLegalEntityCatalog(
        HttpClient httpClient,
        IConfiguration configuration,
        IHttpContextAccessor httpContextAccessor,
        ITenantContext tenantContext,
        ILogger<MdmCyclePeriodLegalEntityCatalog> logger)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _tenantContext = tenantContext;
        _logger = logger;

        var gatewayBaseUrl = configuration["Gateway:BaseUrl"]
            ?? configuration["GatewayUrl"]
            ?? "http://localhost:5000";
        _httpClient.BaseAddress = new Uri(gatewayBaseUrl.TrimEnd('/') + "/");
        _path = configuration["CyclePeriod:LegalEntityLookupPath"] ?? "api/legal-entities/lookup";
    }

    public async Task<LegalEntityLookupResult> GetReferenceableAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TotalTimeout);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, _path);
            ForwardContextHeaders(request);

            using var response = await _httpClient.SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                // Includes 403: the caller may simply lack mdm.legal-entities.read (F-MDM-PERM). That is "we could not
                // look", not "the tenant has no legal entities".
                _logger.LogInformation(
                    "Legal-entity lookup returned {Status}; the scope selector reports the list as unavailable.",
                    (int)response.StatusCode);
                return LegalEntityLookupResult.Unavailable;
            }

            var envelope = await response.Content
                .ReadFromJsonAsync<GatewayEnvelope<List<LegalEntityLookupItem>>>(cancellationToken: timeout.Token);
            if (envelope?.IsSuccessful != true || envelope.Data is null)
            {
                return LegalEntityLookupResult.Unavailable;
            }

            var options = envelope.Data
                .Where(e => e.Referenceable
                            && string.Equals(e.LifecycleState, "ACTIVE", StringComparison.OrdinalIgnoreCase)
                            && e.LegalEntityId != Guid.Empty)
                .Select(e => new LegalEntityLookupOption(
                    e.LegalEntityId,
                    e.Code ?? string.Empty,
                    string.IsNullOrWhiteSpace(e.DisplayName) ? e.LegalName ?? e.Code ?? string.Empty : e.DisplayName))
                .ToList();

            return new LegalEntityLookupResult(true, options);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException
                                       or NotSupportedException)
        {
            _logger.LogWarning(ex, "Legal-entity lookup failed; the scope selector reports the list as unavailable.");
            return LegalEntityLookupResult.Unavailable;
        }
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

    private sealed record GatewayEnvelope<T>(T? Data, int StatusCode, bool IsSuccessful, IReadOnlyList<string> Errors);

    private sealed record LegalEntityLookupItem(
        Guid LegalEntityId,
        string? Code,
        string? LegalName,
        string? DisplayName,
        string? LifecycleState,
        bool Referenceable);
}
