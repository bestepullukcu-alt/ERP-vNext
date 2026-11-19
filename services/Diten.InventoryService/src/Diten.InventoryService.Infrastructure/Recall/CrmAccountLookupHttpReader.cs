using System.Globalization;
using System.Net;
using System.Text.Json;
using Diten.InventoryService.Application.Features.Recall.Ports;
using Microsoft.Extensions.Logging;

namespace Diten.InventoryService.Infrastructure.Recall;

/// <summary>
/// MOD-0177-FU01 (pack §8.6) — CRM-ACCOUNT-LOOKUP 1.0.0 U-1 <c>GET /api/crm/accounts/{id}/lookup-validation</c> through the Gateway with the
/// CALLER'S token and correlation id (<c>ForwardCallerHeadersHandler</c>); the tenant comes from the token only — no tenant or company header.
/// NO cache; each account is read once per call; at most 8 in flight, 2 s per read. Two time rules:
/// <list type="bullet">
/// <item><b>Page</b> (≤ 25 accounts): one linked token ends everything after <see cref="PageBudget"/> (3 s); what answered keeps its answer,
/// the rest is Unresolved (partial result).</item>
/// <item><b>Export</b> (every account, no total budget): ordinal "D" order, waves of 8; when EVERY read of a wave is a breaker-class failure
/// (timeout, 5xx, unreachable) the remaining accounts are not called and are Unresolved (<c>Breaker</c>).</item>
/// </list>
/// Classification: 200 valid → Resolved (status / isBillable are not used); 404 → NotFound; 401 / 403 → Forbidden; everything else →
/// Unresolved. Logged on failure: the account id and the reason — never a body, a name or a code.
/// </summary>
public sealed class CrmAccountLookupHttpReader : IRecallCustomerDirectory
{
    public const int MaxParallel = 8;
    public const int PageLimit = 25;
    public const string ContractVersionPrefix = "crm-account-lookup/1.";
    public static readonly TimeSpan PerReadTimeout = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan PageBudget = TimeSpan.FromSeconds(3);

    private readonly HttpClient _http;
    private readonly ILogger<CrmAccountLookupHttpReader> _logger;

    public CrmAccountLookupHttpReader(HttpClient http, ILogger<CrmAccountLookupHttpReader> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<IReadOnlyDictionary<Guid, RecallCustomerName>> ResolveAsync(IReadOnlyCollection<Guid> accountIds, RecallLookupMode mode,
        CancellationToken cancellationToken = default)
    {
        var ids = accountIds.Distinct().ToList();
        if (mode == RecallLookupMode.Page && ids.Count > PageLimit)
        {
            throw new ArgumentOutOfRangeException(nameof(accountIds), $"A page resolution takes at most {PageLimit} accounts.");
        }

        if (ids.Count == 0) return new Dictionary<Guid, RecallCustomerName>();
        return mode == RecallLookupMode.Page ? await PageAsync(ids, cancellationToken) : await ExportAsync(ids, cancellationToken);
    }

    private async Task<IReadOnlyDictionary<Guid, RecallCustomerName>> PageAsync(List<Guid> ids, CancellationToken caller)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(caller);
        budget.CancelAfter(PageBudget);
        using var gate = new SemaphoreSlim(MaxParallel, MaxParallel);
        var reads = ids.Select(async id =>
        {
            try
            {
                await gate.WaitAsync(budget.Token);
            }
            catch (OperationCanceledException) when (!caller.IsCancellationRequested)
            {
                Log(id, "Budget");
                return (id, Answer: new Read(RecallCustomerName.Unresolved, false));
            }

            try
            {
                return (id, Answer: await ReadOneAsync(id, budget.Token, caller));
            }
            finally
            {
                gate.Release();
            }
        });
        return (await Task.WhenAll(reads)).ToDictionary(r => r.id, r => r.Answer.Name);
    }

    private async Task<IReadOnlyDictionary<Guid, RecallCustomerName>> ExportAsync(List<Guid> ids, CancellationToken caller)
    {
        var answers = new Dictionary<Guid, RecallCustomerName>();
        var ordered = ids.OrderBy(id => id.ToString("D"), StringComparer.Ordinal).ToList();
        var open = false;
        for (var start = 0; start < ordered.Count; start += MaxParallel)
        {
            var wave = ordered.Skip(start).Take(MaxParallel).ToList();
            if (open)
            {
                foreach (var id in wave)
                {
                    Log(id, "Breaker");
                    answers[id] = RecallCustomerName.Unresolved;
                }

                continue;
            }

            var reads = await Task.WhenAll(wave.Select(async id => (id, Answer: await ReadOneAsync(id, caller, caller))));
            foreach (var (id, answer) in reads) answers[id] = answer.Name;
            open = reads.All(r => r.Answer.BreakerClass);
        }

        return answers;
    }

    /// <summary>One answer; <see cref="BreakerClass"/> = timeout, 5xx or unreachable (CRM is not answering), never a CRM verdict.</summary>
    private sealed record Read(RecallCustomerName Name, bool BreakerClass);

    private async Task<Read> ReadOneAsync(Guid accountId, CancellationToken budget, CancellationToken caller)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(budget);
        timeout.CancelAfter(PerReadTimeout);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/crm/accounts/{accountId:D}/lookup-validation");
            using var response = await _http.SendAsync(request, timeout.Token);
            var status = (int)response.StatusCode;
            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    var name = Parse(accountId, await response.Content.ReadAsStringAsync(timeout.Token));
                    if (name.Outcome != RecallCustomerOutcome.Resolved) Log(accountId, "InvalidBody");
                    return new Read(name, false);
                case HttpStatusCode.NotFound:
                    return new Read(new RecallCustomerName(RecallCustomerOutcome.NotFound), false);
                case HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden:
                    Log(accountId, status.ToString(CultureInfo.InvariantCulture));
                    return new Read(new RecallCustomerName(RecallCustomerOutcome.Forbidden), false);
                default:
                    Log(accountId, status.ToString(CultureInfo.InvariantCulture));
                    return new Read(RecallCustomerName.Unresolved, status >= 500);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException && !caller.IsCancellationRequested)
        {
            var spent = budget.IsCancellationRequested && !caller.IsCancellationRequested && budget != caller;
            Log(accountId, spent ? "Budget" : ex.GetType().Name);
            return new Read(RecallCustomerName.Unresolved, !spent);
        }
    }

    /// <summary>A 200 counts only when it is the asked account, carries a code and a name and speaks contract 1.x (pack §8.6).</summary>
    private static RecallCustomerName Parse(Guid accountId, string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            var data = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Object ? d : root;
            var id = Text(data, "accountId");
            var code = Text(data, "code");
            var name = Text(data, "name");
            var version = Text(data, "contractVersion");
            if (id is null || !Guid.TryParse(id, out var answered) || answered != accountId) return RecallCustomerName.Unresolved;
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name)) return RecallCustomerName.Unresolved;
            if (version is null || !version.StartsWith(ContractVersionPrefix, StringComparison.Ordinal)) return RecallCustomerName.Unresolved;
            return new RecallCustomerName(RecallCustomerOutcome.Resolved, code, name);
        }
        catch (JsonException)
        {
            return RecallCustomerName.Unresolved;
        }
    }

    private static string? Text(JsonElement e, string name)
        => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private void Log(Guid accountId, string reason) => _logger.LogInformation("recall.customer.lookup_unavailable {AccountId}: {Reason}", accountId, reason);
}
