using System.Net.Http.Json;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Infrastructure.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Infrastructure.Services;

/// <summary>
/// WC-4 — resolves task recipients from AuthService. Mirrors <see cref="AuthUserDisplayNameClient"/>: same
/// internal-key header, same BaseUrl, same chunking, same never-throw contract.
///
/// <para><b>Deliberately NOT cached, unlike its display-name sibling.</b> A stale NAME is a cosmetic blemish for
/// ten minutes; a stale ADDRESS sends a notification to somewhere the person no longer reads, and the sender
/// believes it arrived. Notification volume is a handful of requests per task write, not a rendered page of
/// them, so there is nothing here worth trading correctness for.</para>
///
/// <para><b>Unresolvable recipients are omitted, never invented.</b> AuthService returns only users that have an
/// address, this drops anything else, and the caller sees a shorter list than it asked for. That difference IS
/// the signal — it is never papered over with the user id, which is what the code did before this class existed
/// and is why no task notification had ever been delivered.</para>
/// </summary>
public sealed class AuthTaskNotificationRecipientClient : ITaskNotificationRecipientResolver
{
    private const string InternalApiKeyHeader = "X-Internal-Api-Key";

    /// <summary>Ids per request, matching the display-name client and AuthService's own cap.</summary>
    private const int ChunkSize = 100;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AuthServiceOptions _authServiceOptions;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<AuthTaskNotificationRecipientClient> _logger;

    public AuthTaskNotificationRecipientClient(
        IHttpClientFactory httpClientFactory,
        IOptions<AuthServiceOptions> authServiceOptions,
        ITenantContext tenantContext,
        ILogger<AuthTaskNotificationRecipientClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _authServiceOptions = authServiceOptions.Value;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TaskNotificationRecipient>> ResolveAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken ct = default)
    {
        if (userIds is null || userIds.Count == 0)
        {
            return [];
        }

        var wanted = userIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (wanted.Count == 0)
        {
            return [];
        }

        if (string.IsNullOrWhiteSpace(_authServiceOptions.BaseUrl) ||
            string.IsNullOrWhiteSpace(_authServiceOptions.InternalApiKey))
        {
            // Loud, because the visible symptom is silence: nobody is notified and nothing else looks wrong.
            _logger.LogWarning(
                "Cannot resolve task notification recipients; AuthService BaseUrl/InternalApiKey not configured. "
                + "No task notification will be delivered until this is set.");
            return [];
        }

        // The tenant comes from the server-side context, never from a caller-supplied value, and AuthService
        // scopes the sweep by it again — a foreign id is simply not in the set it searches.
        var tenantId = _tenantContext.TenantId;
        var resolved = new List<TaskNotificationRecipient>(wanted.Count);

        for (var offset = 0; offset < wanted.Count; offset += ChunkSize)
        {
            var chunk = wanted.Skip(offset).Take(ChunkSize).ToList();
            resolved.AddRange(await FetchChunkAsync(tenantId, chunk, ct));
        }

        if (resolved.Count < wanted.Count)
        {
            // Named individually, because "who did not get told" is the question support will actually ask.
            var unresolved = wanted.Except(resolved.Select(r => r.UserId)).ToList();
            _logger.LogWarning(
                "task.notification.recipients_unresolved Count={Count} UserIds={UserIds} TenantId={TenantId}. "
                + "These people will NOT be notified.",
                unresolved.Count, string.Join(',', unresolved), tenantId);
        }

        return resolved;
    }

    private async Task<IReadOnlyList<TaskNotificationRecipient>> FetchChunkAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> chunk,
        CancellationToken ct)
    {
        try
        {
            var url = $"{_authServiceOptions.BaseUrl.TrimEnd('/')}/internal/users/contacts"
                      + $"?tenantId={tenantId}&ids={string.Join(',', chunk)}";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add(InternalApiKeyHeader, _authServiceOptions.InternalApiKey);

            var client = _httpClientFactory.CreateClient();
            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "AuthService contact request failed. StatusCode={StatusCode} Count={Count}",
                    (int)response.StatusCode, chunk.Count);
                return [];
            }

            var payload = await response.Content.ReadFromJsonAsync<List<AuthUserContact>>(cancellationToken: ct);

            return payload is null
                ? []
                : payload
                    .Where(entry => entry.Id != Guid.Empty && !string.IsNullOrWhiteSpace(entry.Email))
                    .GroupBy(entry => entry.Id)
                    .Select(group => new TaskNotificationRecipient(
                        group.Key,
                        group.First().Email,
                        string.IsNullOrWhiteSpace(group.First().DisplayName) ? null : group.First().DisplayName))
                    .ToList();
        }
        catch (Exception ex)
        {
            // Best effort by contract: a task write must survive AuthService being down. Nobody is notified,
            // and the log above says who.
            _logger.LogWarning(ex, "AuthService contact request threw; nobody in this batch will be notified.");
            return [];
        }
    }

    private sealed record AuthUserContact(Guid Id, string DisplayName, string Email);
}
