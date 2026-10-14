using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.ManufacturingService.Application.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Diten.ManufacturingService.Infrastructure.LegalEntities;

/// <summary>
/// Proves an MDM legal entity may be referenced, through the Gateway, with the existing MDM endpoint
/// <c>GET /api/legal-entities/{id}/lookup-validation</c> — the MVP-1 pattern (CRM <c>MdmCyclePeriodLegalEntityValidator</c>,
/// Platform's working-calendar validator), copied on purpose rather than shared: the copies live in different services.
/// <para>Transport profile, as theirs: 3 s total budget, ONE transient retry (502/503/504, 75 ms), Authorization /
/// X-Tenant-Id / X-Correlation-Id forwarded, always through the Gateway. No cache.</para>
/// <para><b>404 and unreachable are different answers.</b> 404, "not ACTIVE", "not referenceable" or a different id echoed
/// back: MDM spoke and the legal entity may not be used (<see cref="LegalEntityValidation.NotReferenceable"/>). A
/// timeout, a 5xx, an auth rejection (including 403 — the caller may lack <c>mdm.legal-entities.read</c>) or a malformed
/// body: we do not know (<see cref="LegalEntityValidation.Unavailable"/>).</para>
/// </summary>
public sealed class MdmLegalEntityReferenceValidator : ILegalEntityReferenceValidator
{
    private static readonly TimeSpan TotalTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(75);

    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ITenantContext _tenantContext;
    private readonly ICorrelationContext _correlation;
    private readonly string _pathTemplate;

    public MdmLegalEntityReferenceValidator(
        HttpClient httpClient,
        IConfiguration configuration,
        IHttpContextAccessor httpContextAccessor,
        ITenantContext tenantContext,
        ICorrelationContext correlation)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _tenantContext = tenantContext;
        _correlation = correlation;
        var gatewayBaseUrl = configuration["Gateway:BaseUrl"] ?? "http://localhost:5000";
        _httpClient.BaseAddress ??= new Uri(gatewayBaseUrl.TrimEnd('/') + "/");
        _pathTemplate = configuration["LegalEntityValidation:PathTemplate"] ?? "api/legal-entities/{id}/lookup-validation";
    }

    public async Task<LegalEntityValidation> ValidateAsync(Guid legalEntityId, CancellationToken ct)
    {
        if (legalEntityId == Guid.Empty)
        {
            return LegalEntityValidation.NotReferenceable;
        }

        var path = _pathTemplate.Replace("{id}", legalEntityId.ToString("D"));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TotalTimeout);

        try
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, path);
                ForwardContextHeaders(request);

                using var response = await _httpClient.SendAsync(request, timeout.Token);
                if (IsTransient(response.StatusCode) && attempt == 0)
                {
                    await Task.Delay(RetryDelay, timeout.Token);
                    continue;
                }

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return LegalEntityValidation.NotReferenceable;
                }

                if (!response.IsSuccessStatusCode)
                {
                    return LegalEntityValidation.Unavailable;
                }

                var envelope = await response.Content.ReadFromJsonAsync<Envelope>(cancellationToken: timeout.Token);
                if (envelope?.IsSuccessful != true || envelope.Data is null)
                {
                    return LegalEntityValidation.Unavailable;
                }

                return envelope.Data.LegalEntityId == legalEntityId
                       && string.Equals(envelope.Data.LifecycleState, "ACTIVE", StringComparison.OrdinalIgnoreCase)
                       && envelope.Data.Referenceable
                    ? LegalEntityValidation.Valid
                    : LegalEntityValidation.NotReferenceable;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or NotSupportedException)
        {
            return LegalEntityValidation.Unavailable;
        }

        return LegalEntityValidation.Unavailable;
    }

    private void ForwardContextHeaders(HttpRequestMessage request)
    {
        var context = _httpContextAccessor.HttpContext;
        var authorization = context?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authorization) && AuthenticationHeaderValue.TryParse(authorization, out var parsed))
        {
            request.Headers.Authorization = parsed;
        }

        if (_tenantContext.IsResolved)
        {
            request.Headers.TryAddWithoutValidation("X-Tenant-Id", _tenantContext.TenantId.ToString());
        }

        request.Headers.TryAddWithoutValidation("X-Correlation-Id", _correlation.CorrelationId);
    }

    private static bool IsTransient(HttpStatusCode status)
        => status is HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;

    private sealed record Envelope(Lookup? Data, int StatusCode, bool IsSuccessful);

    private sealed record Lookup(Guid LegalEntityId, string? Code, string? LegalName, string? DisplayName, string? LifecycleState, bool Referenceable);
}
