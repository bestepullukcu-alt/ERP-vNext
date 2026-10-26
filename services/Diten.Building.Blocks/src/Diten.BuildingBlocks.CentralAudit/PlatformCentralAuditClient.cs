using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace Diten.BuildingBlocks.CentralAudit;

public enum CentralAuditDeliveryOutcome
{
    /// <summary>Platform has it (202 Queued, or 202 Duplicate — it already had this event id).</summary>
    Accepted,

    /// <summary>Platform refused it (400): the same row will be refused again.</summary>
    Rejected,

    /// <summary>Not delivered for a reason that can pass: unreachable, 5xx, 401/403 (key), timeout.</summary>
    Retry
}

public sealed record CentralAuditDeliveryResult(CentralAuditDeliveryOutcome Outcome, string? Error = null);

public interface ICentralAuditPlatformClient
{
    Task<CentralAuditDeliveryResult> SendAsync(CentralAuditOutboxRecord record, CancellationToken ct);
}

/// <summary>
/// The ONE client of <c>POST /api/internal/audit/append</c> outside Platform (MDM's and Auth's older copies are the named
/// exceptions, FU-BL501-MIGRATE). Sends Platform's <c>AuditAppendRequest</c> shape with enums as numbers and
/// <c>EventId</c> for Platform's per-event idempotency.
/// </summary>
public sealed class PlatformCentralAuditClient : ICentralAuditPlatformClient
{
    public const string HttpClientName = "Diten.CentralAudit";
    public const string AppendPath = "/api/internal/audit/append";
    private const string InternalApiKeyHeader = "X-Internal-Api-Key";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<CentralAuditOptions> _options;

    public PlatformCentralAuditClient(IHttpClientFactory httpClientFactory, IOptionsMonitor<CentralAuditOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
    }

    public async Task<CentralAuditDeliveryResult> SendAsync(CentralAuditOutboxRecord record, CancellationToken ct)
    {
        var options = _options.CurrentValue;
        if (!options.IsConfigured)
        {
            return new CentralAuditDeliveryResult(CentralAuditDeliveryOutcome.Retry, "Platform address / internal key not configured.");
        }

        if (!CentralAuditPlatformValues.Categories.TryGetValue(record.Category, out var category)
            || !CentralAuditPlatformValues.Operations.TryGetValue(record.Operation, out var operation)
            || !CentralAuditPlatformValues.ActorTypes.TryGetValue(record.ActorType, out var actorType))
        {
            return new CentralAuditDeliveryResult(
                CentralAuditDeliveryOutcome.Rejected,
                $"Category '{record.Category}' / operation '{record.Operation}' / actor type '{record.ActorType}' is not a Platform audit enum name.");
        }

        var payload = new
        {
            record.CorrelationId,
            record.RequestType,
            ActorType = actorType,
            record.ActorId,
            record.ActorDisplayName,
            TargetTenantId = record.TenantId,
            Category = category,
            record.EntityType,
            record.EntityId,
            Operation = operation,
            Outcome = CentralAuditPlatformValues.OutcomeOf(record),
            // Identifiers only — never a request body (AUD-001 §3: names, not values).
            Metadata = new Dictionary<string, object?>
            {
                ["eventId"] = record.Id,
                ["companyId"] = record.CompanyId,
                ["writtenInCommandTransaction"] = record.WrittenInCommandTransaction,
                ["confirmedByCommand"] = record.ConfirmedByCommand
            },
            OccurredAtUtc = new DateTimeOffset(DateTime.SpecifyKind(record.OccurredAtUtc, DateTimeKind.Utc)),
            record.SourceService,
            record.SourceModule,
            IsPlatformGlobal = false,
            EventId = record.Id
        };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{options.PlatformBaseUrl!.TrimEnd('/')}{AppendPath}")
            {
                Content = JsonContent.Create(payload)
            };
            request.Headers.Add(InternalApiKeyHeader, options.InternalApiKey);

            using var response = await _httpClientFactory.CreateClient(HttpClientName).SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
            {
                return new CentralAuditDeliveryResult(CentralAuditDeliveryOutcome.Accepted);
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            var error = $"Platform answered {(int)response.StatusCode}: {Truncate(body)}";
            return response.StatusCode == HttpStatusCode.BadRequest
                ? new CentralAuditDeliveryResult(CentralAuditDeliveryOutcome.Rejected, error)
                : new CentralAuditDeliveryResult(CentralAuditDeliveryOutcome.Retry, error);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new CentralAuditDeliveryResult(CentralAuditDeliveryOutcome.Retry, $"{ex.GetType().Name}: {Truncate(ex.Message)}");
        }
    }

    private static string Truncate(string value) => value.Length <= 300 ? value : value[..300];
}
