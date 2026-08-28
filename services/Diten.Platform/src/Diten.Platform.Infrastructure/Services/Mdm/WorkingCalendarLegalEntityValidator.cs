using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.WorkingCalendar.Services;
using Diten.Platform.Common.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Diten.Platform.Infrastructure.Services.Mdm;

/// <summary>Gateway-backed, request-scoped validation for WorkingCalendar.LegalEntityId. Deliberately has no cache.</summary>
public sealed class WorkingCalendarLegalEntityValidator : IWorkingCalendarLegalEntityValidator
{
    private static readonly TimeSpan TotalTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(75);
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ITenantContext _tenantContext;

    public WorkingCalendarLegalEntityValidator(
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

    public async Task<WorkingCalendarLegalEntityValidationResult> ValidateAsync(
        Guid legalEntityId,
        CancellationToken ct = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TotalTimeout);

        try
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                using var request = new HttpRequestMessage(
                    HttpMethod.Get,
                    $"api/legal-entities/{legalEntityId:D}/lookup-validation");
                ForwardContextHeaders(request);

                using var response = await _httpClient.SendAsync(request, timeout.Token);
                if (IsTransient(response.StatusCode) && attempt == 0)
                {
                    await Task.Delay(RetryDelay, timeout.Token);
                    continue;
                }

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return WorkingCalendarLegalEntityValidationResult.NotReferenceable;
                }

                // Tenant mismatch, auth rejection and exhausted transient failures are dependency failures for this
                // write: the caller cannot prove the FK belongs to the current tenant, so persistence is forbidden.
                if (!response.IsSuccessStatusCode)
                {
                    return WorkingCalendarLegalEntityValidationResult.Unavailable;
                }

                var envelope = await response.Content.ReadFromJsonAsync<GatewayEnvelope<LegalEntityLookup>>(
                    cancellationToken: timeout.Token);
                if (envelope?.IsSuccessful != true || envelope.Data is null)
                {
                    return WorkingCalendarLegalEntityValidationResult.Unavailable;
                }

                return envelope.Data.LegalEntityId == legalEntityId
                       && string.Equals(envelope.Data.LifecycleState, "ACTIVE", StringComparison.OrdinalIgnoreCase)
                       && envelope.Data.Referenceable
                    ? WorkingCalendarLegalEntityValidationResult.Valid
                    : WorkingCalendarLegalEntityValidationResult.NotReferenceable;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or NotSupportedException)
        {
            return WorkingCalendarLegalEntityValidationResult.Unavailable;
        }

        return WorkingCalendarLegalEntityValidationResult.Unavailable;
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
        if (string.IsNullOrWhiteSpace(tenant) && _tenantContext.IsResolved && !_tenantContext.IsPlatformContext)
        {
            tenant = _tenantContext.TenantId.ToString();
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
