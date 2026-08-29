using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.CyclePeriod.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Diten.CrmService.Infrastructure.CyclePeriod;

/// <summary>
/// MOD-0165 FU07 — proves an MDM legal entity may be referenced, through the Gateway, immediately before a
/// legal-entity-scoped period is persisted.
/// <para><b>Deliberately has no cache.</b> The same id asked twice makes two calls. A cache here would mean a period
/// could be scoped to a legal entity that no longer exists, which is precisely the thing this class is for.</para>
/// <para>The transport profile mirrors the working calendar's legal-entity validator and MOD-0167 FU02's product
/// validator verbatim: 3 second total budget, ONE transient retry (502/503/504, 75 ms), Authorization / X-Tenant-Id /
/// X-Correlation-Id forwarded, and always through the Gateway — never a service port. It is a third copy on purpose:
/// the three live in different services, and sharing a library would couple CrmService to Platform. What keeps them
/// honest is that all three must pass the same behaviours.</para>
/// <para><b>404 and unreachable are different answers.</b> 404, "not ACTIVE" and "not referenceable" mean the
/// dependency spoke and the write is invalid (400). A timeout, a 5xx, an auth rejection or a malformed body means we do
/// not know, which is a 503 with nothing persisted — the caller invokes this BEFORE any insert or replace, so a
/// dependency outage can never leave a half-authored period behind. A 403 is <b>not</b> flattened into "no such
/// entity": the entity may exist and we were simply not allowed to look (follow-up F-MDM-PERM).</para>
/// </summary>
public sealed class MdmCyclePeriodLegalEntityValidator : ICyclePeriodLegalEntityValidator
{
    private static readonly TimeSpan TotalTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(75);

    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ITenantContext _tenantContext;
    private readonly string _pathTemplate;

    public MdmCyclePeriodLegalEntityValidator(
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

        // Configurable so a route move is an ops change rather than a code change.
        _pathTemplate = configuration["CyclePeriod:LegalEntityValidationPathTemplate"]
            ?? "api/legal-entities/{id}/lookup-validation";
    }

    public async Task<CyclePeriodLegalEntityValidation> ValidateAsync(
        Guid legalEntityId, CancellationToken cancellationToken)
    {
        if (legalEntityId == Guid.Empty)
        {
            return CyclePeriodLegalEntityValidation.NotReferenceable;
        }

        var path = _pathTemplate.Replace("{id}", legalEntityId.ToString("D"));

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
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
                    return CyclePeriodLegalEntityValidation.NotReferenceable;
                }

                // Tenant mismatch, auth rejection and exhausted transient failures are dependency failures for this
                // write: the caller cannot PROVE the reference belongs to the current tenant, so persistence is
                // forbidden — but the author is told "we could not check", not "it does not exist".
                if (!response.IsSuccessStatusCode)
                {
                    return CyclePeriodLegalEntityValidation.Unavailable;
                }

                var envelope = await response.Content.ReadFromJsonAsync<GatewayEnvelope<LegalEntityLookup>>(
                    cancellationToken: timeout.Token);
                if (envelope?.IsSuccessful != true || envelope.Data is null)
                {
                    return CyclePeriodLegalEntityValidation.Unavailable;
                }

                return envelope.Data.LegalEntityId == legalEntityId
                       && string.Equals(envelope.Data.LifecycleState, "ACTIVE", StringComparison.OrdinalIgnoreCase)
                       && envelope.Data.Referenceable
                    ? CyclePeriodLegalEntityValidation.Valid
                    : CyclePeriodLegalEntityValidation.NotReferenceable;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException
                                       or NotSupportedException)
        {
            return CyclePeriodLegalEntityValidation.Unavailable;
        }

        return CyclePeriodLegalEntityValidation.Unavailable;
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

    private static bool IsTransient(HttpStatusCode status)
        => status is HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;

    private sealed record GatewayEnvelope<T>(T? Data, int StatusCode, bool IsSuccessful, IReadOnlyList<string> Errors);

    private sealed record LegalEntityLookup(
        Guid LegalEntityId,
        string Code,
        string LegalName,
        string? DisplayName,
        string LifecycleState,
        bool Referenceable);
}
