using System.Net;
using System.Text.Json;
using Diten.PpmService.Application.Features.Portfolios;
using Microsoft.Extensions.Options;

namespace Diten.PpmService.Infrastructure.Portfolios;

// Credentials are bound to each request after explicit Bearer authentication and scope checks.
public sealed class PortfolioAuthorityClient(HttpClient httpClient, IOptions<PortfolioAuthorityOptions> options,
    PortfolioAuthRequestContext requestContext, PortfolioAuthTrustedTarget trustedTarget)
    : IPortfolioOwnerActionAuthority
{
    private const string PolicyVersion = "portfolio-owner-auth-adapter-v1";
    private static readonly JsonDocumentOptions JsonOptions = new() { CommentHandling = JsonCommentHandling.Disallow };

    public async Task<PortfolioAuthorityEvidence> CanManageAsync(PortfolioAuthorityScope scope, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Evidence(scope, CanUseAuth() && await requestContext.IsValidAsync(scope, ct)
            ? LocalActorOutcome(scope) : PortfolioAuthorityOutcome.Unavailable);
    }

    public async Task<PortfolioOwnerEvidence> EvaluateAsync(PortfolioAuthorityScope scope, CancellationToken ct)
    {
        var actor = await CanManageAsync(scope, ct);
        if (actor.Outcome != PortfolioAuthorityOutcome.Allowed || scope.TargetUserId is not { } targetId || targetId == Guid.Empty)
            return new(actor, Evidence(scope, actor.Outcome == PortfolioAuthorityOutcome.Allowed
                ? PortfolioAuthorityOutcome.Unavailable : actor.Outcome), false, false, false, false, null, PortfolioOwnerLabelState.Available);

        var assertion = await GetDataAsync(scope, $"api/users/{targetId:D}/account-assertion", ct);
        if (assertion.Status == HttpStatusCode.NotFound)
            return new(actor, Evidence(scope, PortfolioAuthorityOutcome.NotFound), false, false, false, false, null, PortfolioOwnerLabelState.Available);
        if (assertion.Status != HttpStatusCode.OK || !TryAssertion(assertion.Data, targetId, out var active, out var accountKind))
            return Unavailable(actor, scope);

        var label = await GetDataAsync(scope, $"api/users/{targetId:D}/display-label", ct);
        if (label.Status == HttpStatusCode.NotFound)
            return new(actor, Evidence(scope, PortfolioAuthorityOutcome.NotFound), false, false, false, false, null, PortfolioOwnerLabelState.Available);
        if (label.Status != HttpStatusCode.OK || !TryLabel(label.Data, targetId, out var displayLabel, out var labelState))
            return Unavailable(actor, scope);

        return new(actor, Evidence(scope, PortfolioAuthorityOutcome.Allowed), true, active,
            string.Equals(accountKind, "Human", StringComparison.Ordinal), true, displayLabel, labelState);
    }

    public async Task<PortfolioOwnerCandidatesEvidence> CandidatesAsync(PortfolioAuthorityScope scope, CancellationToken ct)
    {
        var authority = await CanManageAsync(scope, ct);
        if (authority.Outcome != PortfolioAuthorityOutcome.Allowed)
            return new(authority, []);
        if (scope.Search is null || scope.Limit is not { } limit || limit is < 1 or > 20)
            return new(Evidence(scope, PortfolioAuthorityOutcome.Unavailable), []);

        var lookup = await GetDataAsync(scope, $"api/users/lookup?search={Uri.EscapeDataString(scope.Search)}&limit={limit}", ct);
        if (lookup.Status != HttpStatusCode.OK || !TryCandidateLookup(lookup.Data, limit, out var lookupCandidates))
            return new(Evidence(scope, PortfolioAuthorityOutcome.Unavailable), []);

        var candidates = new List<PortfolioOwnerCandidate>();
        foreach (var lookupCandidate in lookupCandidates)
        {
            // Lookup only proves a bounded, tenant-scoped discovery result. It does not prove account kind.
            var assertion = await GetDataAsync(scope, $"api/users/{lookupCandidate.UserId:D}/account-assertion", ct);
            if (assertion.Status != HttpStatusCode.OK ||
                !TryAssertion(assertion.Data, lookupCandidate.UserId, out var active, out var accountKind))
                return new(Evidence(scope, PortfolioAuthorityOutcome.Unavailable), []);

            // These are definitive target facts, so they are omitted rather than converted into a provider outage.
            if (!active || !string.Equals(accountKind, "Human", StringComparison.Ordinal)) continue;

            var label = await GetDataAsync(scope, $"api/users/{lookupCandidate.UserId:D}/display-label", ct);
            if (label.Status != HttpStatusCode.OK ||
                !TryLabel(label.Data, lookupCandidate.UserId, out var displayLabel, out var labelState))
                return new(Evidence(scope, PortfolioAuthorityOutcome.Unavailable), []);

            // Unnamed and too-long labels are valid Auth facts but cannot appear in the selectable owner list.
            if (labelState == PortfolioOwnerLabelState.Available)
                candidates.Add(new PortfolioOwnerCandidate(lookupCandidate.UserId, displayLabel!));
        }
        return new(authority, candidates);
    }

    private async Task<(HttpStatusCode Status, JsonElement Data)> GetDataAsync(PortfolioAuthorityScope scope, string path, CancellationToken ct)
    {
        if (!CanUseAuth() || !trustedTarget.TryCreateRequestUri(path, out var uri)) return (0, default);
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        if (!await requestContext.TryBindAsync(request, scope, ct)) return (0, default);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.TimeoutSeconds));
        try
        {
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (response.StatusCode == HttpStatusCode.NotFound) return (response.StatusCode, default);
            if (response.StatusCode != HttpStatusCode.OK) return (response.StatusCode, default);
            var body = await response.Content.ReadAsStringAsync(timeout.Token);
            using var document = JsonDocument.Parse(body, JsonOptions);
            return TryEnvelope(document.RootElement, out var data) ? (response.StatusCode, data.Clone()) : (0, default);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or NotSupportedException or FormatException or OverflowException or InvalidOperationException)
        {
            return (0, default);
        }
    }

    private bool CanUseAuth() => trustedTarget.IsEnabled &&
        httpClient.DefaultRequestHeaders.Authorization is null &&
        !httpClient.DefaultRequestHeaders.Contains("X-Tenant-Id") &&
        !httpClient.DefaultRequestHeaders.Contains("Cookie");

    private static PortfolioAuthorityOutcome LocalActorOutcome(PortfolioAuthorityScope scope)
    {
        if (scope.TenantId == Guid.Empty || scope.ActorId == Guid.Empty || scope.PortfolioId is not { } id || id == Guid.Empty ||
            scope.RecordTenantId != scope.TenantId || scope.CreatorId is not { } creator || creator == Guid.Empty ||
            scope.Version is not > 0 || scope.LifecycleState != Domain.Entities.PortfolioLifecycleState.Draft ||
            scope.TemporaryNonProductionAccessBinding is null || !scope.TemporaryNonProductionAccessBinding.HasValidShape())
            return PortfolioAuthorityOutcome.Unavailable;
        if (scope.TemporaryNonProductionAccessBinding.PortfolioId != id) return PortfolioAuthorityOutcome.Unavailable;
        return scope.ActorId == creator || scope.ActorId == scope.CurrentOwnerUserId
            ? PortfolioAuthorityOutcome.Allowed : PortfolioAuthorityOutcome.Denied;
    }

    private static bool TryEnvelope(JsonElement root, out JsonElement data)
    {
        data = default;
        return root.ValueKind == JsonValueKind.Object && root.TryGetProperty("isSuccessful", out var successful) && successful.ValueKind == JsonValueKind.True &&
            root.TryGetProperty("statusCode", out var status) && TryInt32(status, out var statusCode) && statusCode == 200 &&
            root.TryGetProperty("data", out data) && data.ValueKind is JsonValueKind.Object or JsonValueKind.Array;
    }

    private static bool TryAssertion(JsonElement data, Guid targetId, out bool active, out string? accountKind)
    {
        active = false; accountKind = null;
        if (data.ValueKind != JsonValueKind.Object || !data.TryGetProperty("userId", out var id) || !TryGuid(id, out var receivedId) || receivedId != targetId ||
            !data.TryGetProperty("active", out var activeValue) || activeValue.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
            !data.TryGetProperty("accountKind", out var kind) || kind.ValueKind != JsonValueKind.String ||
            !data.TryGetProperty("assertedAt", out var asserted) || !TryRequiredTimestamp(asserted) ||
            !data.TryGetProperty("userUpdatedAt", out var updated) || !TryNullableTimestamp(updated)) return false;
        accountKind = kind.GetString();
        if (accountKind is not ("Human" or "Unknown" or "Service")) return false;
        active = activeValue.GetBoolean();
        return true;
    }

    private static bool TryLabel(JsonElement data, Guid targetId, out string? label, out PortfolioOwnerLabelState state)
    {
        label = null; state = PortfolioOwnerLabelState.Available;
        if (data.ValueKind != JsonValueKind.Object || !data.TryGetProperty("userId", out var id) || !TryGuid(id, out var receivedId) || receivedId != targetId ||
            !data.TryGetProperty("labelState", out var rawState) || rawState.ValueKind != JsonValueKind.String ||
            !data.TryGetProperty("displayLabel", out var rawLabel)) return false;
        if (rawState.GetString() == "Unnamed" && rawLabel.ValueKind == JsonValueKind.Null)
        { state = PortfolioOwnerLabelState.Missing; return true; }
        if (rawState.GetString() != "Named" || rawLabel.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(label = rawLabel.GetString())) return false;
        state = label.Length > 200 ? PortfolioOwnerLabelState.TooLong : PortfolioOwnerLabelState.Available;
        return true;
    }

    private static bool TryCandidateLookup(JsonElement data, int limit, out IReadOnlyList<LookupCandidate> candidates)
    {
        candidates = [];
        if (data.ValueKind != JsonValueKind.Array || data.GetArrayLength() > limit) return false;
        var values = new List<LookupCandidate>();
        foreach (var item in data.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("userId", out var id) || !TryGuid(id, out var userId) || userId == Guid.Empty ||
                !item.TryGetProperty("displayLabel", out var label) || label.ValueKind != JsonValueKind.String)
                return false;
            values.Add(new(userId));
        }
        if (values.Select(x => x.UserId).Distinct().Count() != values.Count) return false;
        candidates = values;
        return true;
    }

    private static bool TryGuid(JsonElement value, out Guid result)
    {
        result = Guid.Empty;
        return value.ValueKind == JsonValueKind.String && Guid.TryParse(value.GetString(), out result) && result != Guid.Empty;
    }
    private static bool TryInt32(JsonElement value, out int result)
    {
        result = default;
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out result);
    }
    private static bool TryRequiredTimestamp(JsonElement value) =>
        value.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(value.GetString(), out _);
    // Auth intentionally permits null when the account record has never been updated; omission is malformed.
    private static bool TryNullableTimestamp(JsonElement value) =>
        value.ValueKind == JsonValueKind.Null || TryRequiredTimestamp(value);

    private sealed record LookupCandidate(Guid UserId);

    private static PortfolioOwnerEvidence Unavailable(PortfolioAuthorityEvidence actor, PortfolioAuthorityScope scope) =>
        new(actor, Evidence(scope, PortfolioAuthorityOutcome.Unavailable), false, false, false, false, null, PortfolioOwnerLabelState.Available);
    private static PortfolioAuthorityEvidence Evidence(PortfolioAuthorityScope scope, PortfolioAuthorityOutcome outcome)
    {
        var now = DateTime.UtcNow;
        return new(scope, outcome, PolicyVersion, now, now.AddMinutes(1));
    }
}
