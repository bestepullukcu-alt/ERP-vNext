using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.VisitEvidence;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Diten.CrmService.Infrastructure.VisitEvidence;

/// <summary>
/// WP-VW-W3L (W3-BE-d, K-L8) — the reps' primary seats from the Platform (PLT-ORG-SEAT), through the Gateway with the
/// caller's token: <c>GET api/platform/organization/seats/{userId}</c> and <c>POST api/platform/organization/seats/lookup</c>
/// (at most 100 ids per call). The transport profile is the working-calendar seam's
/// (<see cref="CycleCapacity.WorkingCalendarWorkingDayCounter"/>): a 3-second total budget, ONE transient retry (502 / 503
/// / 504, 75 ms), Authorization / X-Tenant-Id / X-Correlation-Id forwarded, never a service port, no cache.
/// <para><b>It never throws into the caller.</b> A timeout, an error, a refusal (403 — the caller lacks
/// <c>platform.organization-seats.read</c> for another user) or an unreadable answer is "not available": the legal entity
/// layer is skipped with <c>platform-unavailable</c> and the visit start goes on (K-L5). A user id that is not a Guid has no
/// Platform seat (no call).</para>
/// </summary>
public sealed class PlatformOrganizationSeatReader : IOrganizationSeatReader
{
    public const int MaxLookup = 100;

    private static readonly TimeSpan TotalTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(75);

    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ITenantContext _tenantContext;
    private readonly string _basePath;

    public PlatformOrganizationSeatReader(HttpClient httpClient, IConfiguration configuration, IHttpContextAccessor httpContextAccessor,
        ITenantContext tenantContext)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _tenantContext = tenantContext;
        var gatewayBaseUrl = configuration["Gateway:BaseUrl"] ?? configuration["GatewayUrl"] ?? "http://localhost:5000";
        _httpClient.BaseAddress = new Uri(gatewayBaseUrl.TrimEnd('/') + "/");
        _basePath = (configuration["VisitEvidence:OrganizationSeatsPath"] ?? "api/platform/organization/seats").TrimEnd('/');
    }

    public async Task<OrganizationSeatResult> GetAsync(string userId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(userId, out var id))
        {
            return new OrganizationSeatResult(true, null);
        }

        var dto = await SendAsync<SeatPayload>(() => new HttpRequestMessage(HttpMethod.Get, _basePath + "/" + id.ToString("D")), cancellationToken);
        return dto is null ? new OrganizationSeatResult(false, null) : new OrganizationSeatResult(true, ToSeat(dto.Primary));
    }

    public async Task<OrganizationSeatBulkResult> LookupAsync(IReadOnlyCollection<string> userIds, CancellationToken cancellationToken)
    {
        var seats = new Dictionary<string, OrganizationSeat?>(StringComparer.Ordinal);
        var unavailable = new HashSet<string>(StringComparer.Ordinal);
        var byGuid = new Dictionary<Guid, List<string>>();
        foreach (var userId in userIds.Where(u => !string.IsNullOrWhiteSpace(u)).Distinct(StringComparer.Ordinal))
        {
            if (Guid.TryParse(userId, out var id))
            {
                (byGuid.TryGetValue(id, out var list) ? list : byGuid[id] = new()).Add(userId);
            }
            else
            {
                seats[userId] = null; // not a Platform user id: no seat
            }
        }

        foreach (var batch in byGuid.Keys.Chunk(MaxLookup))
        {
            var dto = await SendAsync<SeatsPayload>(
                () => new HttpRequestMessage(HttpMethod.Post, _basePath + "/lookup") { Content = JsonContent.Create(new { userIds = batch }) },
                cancellationToken);
            var answered = (dto?.Items ?? []).Where(i => i is not null).GroupBy(i => i.UserId).ToDictionary(g => g.Key, g => g.First());
            foreach (var id in batch)
            {
                foreach (var userId in byGuid[id])
                {
                    if (dto is null)
                    {
                        unavailable.Add(userId);
                    }
                    else
                    {
                        seats[userId] = answered.TryGetValue(id, out var item) ? ToSeat(item.Primary) : null;
                    }
                }
            }
        }

        return new OrganizationSeatBulkResult(seats, unavailable);
    }

    /// <summary>The envelope's data, or null for every failure (never thrown).</summary>
    private async Task<T?> SendAsync<T>(Func<HttpRequestMessage> build, CancellationToken cancellationToken) where T : class
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TotalTimeout);
        try
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                using var request = build();
                ForwardContextHeaders(request);
                using var response = await _httpClient.SendAsync(request, timeout.Token);
                if (IsTransient(response.StatusCode) && attempt == 0)
                {
                    await Task.Delay(RetryDelay, timeout.Token);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var envelope = await response.Content.ReadFromJsonAsync<GatewayEnvelope<T>>(cancellationToken: timeout.Token);
                return envelope?.IsSuccessful == true ? envelope.Data : null;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or NotSupportedException)
        {
            return null;
        }

        return null;
    }

    private static OrganizationSeat? ToSeat(PrimaryPayload? p)
        => p is null ? null : new OrganizationSeat(p.PositionId, p.PositionCode, p.OrganizationUnitId, p.OrganizationUnitCode, p.LegalEntityId, p.EffectiveFrom);

    private static bool IsTransient(HttpStatusCode status)
        => status is HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;

    private void ForwardContextHeaders(HttpRequestMessage request)
    {
        var context = _httpContextAccessor.HttpContext;
        var authorization = context?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authorization) && AuthenticationHeaderValue.TryParse(authorization, out var parsed))
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

    private sealed record PrimaryPayload(
        Guid? PositionId, string? PositionCode, Guid? OrganizationUnitId, string? OrganizationUnitCode, Guid? LegalEntityId, DateTimeOffset? EffectiveFrom);

    private sealed record SeatPayload(Guid UserId, PrimaryPayload? Primary);

    private sealed record SeatsPayload(IReadOnlyList<SeatPayload>? Items);
}
