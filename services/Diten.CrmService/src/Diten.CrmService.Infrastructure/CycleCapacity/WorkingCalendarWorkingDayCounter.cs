using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.CycleCapacity.Read;
using Diten.CrmService.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Diten.CrmService.Infrastructure.CycleCapacity;

/// <summary>
/// MOD-0155 FU06 — asks CAND-CAP-0008 (the platform working calendar), through the Gateway, how many working days a
/// month contains.
///
/// <para><b>Why <c>/overrides/resolve</c> and not <c>/resolve</c>.</b> This is not a preference; it is the only door
/// that opens from a tenant context. The country-layer path <c>/api/platform/working-calendars/resolve</c> is
/// classified as an ADMIN path by the Gateway, which (a) rejects the <c>X-Tenant-Id</c> header outright with 400 and
/// (b) answers 403 to any <c>tenant_user</c> token — and its permission,
/// <c>platform.working-calendar.read</c>, opens the whole country layer and is not tenant-assignable. The
/// <c>/overrides</c> sub-path is on the Gateway's tenant-scoped allow-list, needs the narrower and tenant-assignable
/// <c>platform.working-calendar.override.read</c>, and dispatches to the SAME handler and the SAME provider — so the
/// answer is identical, it simply comes back through a door a tenant may use. It also resolves the country layer
/// TOGETHER with the tenant's own override, which is exactly what a capacity estimate needs.</para>
///
/// <para><b>Deliberately has no cache.</b> The same month asked twice makes two calls. A cached working-day count
/// would keep answering with yesterday's calendar after a holiday is published or an override is added — which is the
/// precise failure this whole seam exists to avoid.</para>
///
/// <para>The transport profile mirrors <c>MdmCyclePeriodLegalEntityValidator</c> and MOD-0167 FU02's product
/// validator verbatim: a 3-second total budget, ONE transient retry (502/503/504, 75 ms), Authorization /
/// X-Tenant-Id / X-Correlation-Id forwarded, and always through the Gateway — never a service port.</para>
///
/// <para><b>It never throws into the consumer and never invents a number.</b> Every failure comes back as an
/// unresolved result, because an exception would tempt a caller to fall back to "about 22 working days" — a
/// plausible-looking guess nobody could tell apart from a real answer. A 403 is reported as its OWN resolution and is
/// never flattened into "no calendar": the calendar may well exist and the caller simply lacks the platform
/// permission (F-RBAC-WC).</para>
/// </summary>
public sealed class WorkingCalendarWorkingDayCounter : IWorkingDayCounter
{
    private static readonly TimeSpan TotalTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(75);

    /// <summary>The platform operation. It counts working days over an inclusive range, having already excluded
    /// weekends, public holidays and company closures day by day.</summary>
    private const string Operation = "working-days-between";

    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ITenantContext _tenantContext;
    private readonly string _pathTemplate;

    public WorkingCalendarWorkingDayCounter(
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
        _pathTemplate = configuration["CycleCapacity:WorkingCalendarPathTemplate"]
            ?? "api/platform/working-calendars/overrides/resolve";
    }

    public async Task<WorkingDayCountResult> CountAsync(
        string countryCode,
        Guid? legalEntityId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
        {
            return Unresolved(
                CycleCapacityResolutions.CalendarUnresolved,
                CycleCapacityReasonCodes.CountryUnderivable,
                "No calendar country was supplied.");
        }

        if (to < from)
        {
            return Unresolved(
                CycleCapacityResolutions.CalendarUnresolved,
                CycleCapacityReasonCodes.MonthOutOfPeriod,
                $"The range {from:yyyy-MM-dd}..{to:yyyy-MM-dd} is inverted.");
        }

        var path = BuildPath(countryCode, legalEntityId, from, to);

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

                // Kept apart from every other failure on purpose: "you may not read the calendar" and "there is no
                // calendar" have completely different fixes.
                if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
                {
                    return Unresolved(
                        CycleCapacityResolutions.CalendarForbidden,
                        CycleCapacityReasonCodes.CalendarForbidden,
                        "The working calendar refused the request. The signed-in user needs "
                        + $"'{Application.Features.CycleCapacity.CycleCapacityPermissions.WorkingCalendarOverrideRead}'.");
                }

                if (!response.IsSuccessStatusCode)
                {
                    return Unresolved(
                        CycleCapacityResolutions.CalendarUnresolved,
                        CycleCapacityReasonCodes.CalendarUnresolved,
                        $"The working calendar answered {(int)response.StatusCode}.");
                }

                var envelope = await response.Content.ReadFromJsonAsync<GatewayEnvelope<WorkingDayResolvePayload>>(
                    cancellationToken: timeout.Token);

                if (envelope?.IsSuccessful != true || envelope.Data is null)
                {
                    return Unresolved(
                        CycleCapacityResolutions.CalendarUnresolved,
                        CycleCapacityReasonCodes.CalendarUnresolved,
                        "The working calendar's response could not be read.");
                }

                var payload = envelope.Data;

                // The platform answers 200 with resolution != "resolved" when no calendar covers the range. That is a
                // legitimate answer the consumer must branch on, NOT a transport error — and never a licence to guess.
                if (!string.Equals(payload.Resolution, "resolved", StringComparison.OrdinalIgnoreCase)
                    || payload.WorkingDayCount is not { } count)
                {
                    return new WorkingDayCountResult(
                        CycleCapacityResolutions.CalendarUnresolved,
                        null,
                        Reasons(payload.ReasonCodes, CycleCapacityReasonCodes.CalendarUnresolved),
                        string.IsNullOrWhiteSpace(payload.SelectionReason)
                            ? $"The working calendar reported '{payload.Resolution}'."
                            : payload.SelectionReason);
                }

                return new WorkingDayCountResult(
                    CycleCapacityResolutions.Resolved,
                    count,
                    Reasons(payload.ReasonCodes, CycleCapacityReasonCodes.CapacityOk),
                    string.IsNullOrWhiteSpace(payload.SelectionReason)
                        ? $"{count} working day(s) between {from:yyyy-MM-dd} and {to:yyyy-MM-dd}."
                        : payload.SelectionReason);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException
                                       or NotSupportedException)
        {
            return Unresolved(
                CycleCapacityResolutions.CalendarUnresolved,
                CycleCapacityReasonCodes.CalendarUnresolved,
                "The working calendar could not be reached.");
        }

        return Unresolved(
            CycleCapacityResolutions.CalendarUnresolved,
            CycleCapacityReasonCodes.CalendarUnresolved,
            "The working calendar could not be reached.");
    }

    private string BuildPath(string countryCode, Guid? legalEntityId, DateOnly from, DateOnly to)
    {
        var query =
            $"?op={Operation}"
            + $"&date={from:yyyy-MM-dd}"
            + $"&toDate={to:yyyy-MM-dd}"
            + $"&countryCode={Uri.EscapeDataString(countryCode)}";

        // The optional narrowing, and the ONLY one passed. A business unit is never sent as organizationUnitId: it is
        // a reference-data value code, not an organization-unit id (F-WC-ORG-UNIT).
        if (legalEntityId is { } id && id != Guid.Empty)
        {
            query += $"&legalEntityId={id:D}";
        }

        return _pathTemplate + query;
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

    private static IReadOnlyList<string> Reasons(IReadOnlyList<string>? platformCodes, string fallback)
        => platformCodes is { Count: > 0 } ? platformCodes : new[] { fallback };

    private static WorkingDayCountResult Unresolved(string resolution, string reasonCode, string reason)
        => new(resolution, null, new[] { reasonCode }, reason);

    private static bool IsTransient(HttpStatusCode status)
        => status is HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;

    private sealed record GatewayEnvelope<T>(T? Data, int StatusCode, bool IsSuccessful, IReadOnlyList<string>? Errors);

    /// <summary>The subset of the platform's resolve payload this seam needs. Deliberately narrow: reading fields we do
    /// not use would couple this consumer to parts of the calendar's contract it has no business knowing.</summary>
    private sealed record WorkingDayResolvePayload(
        string Resolution,
        int? WorkingDayCount,
        string? SelectionReason,
        IReadOnlyList<string>? ReasonCodes);
}
