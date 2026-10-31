using System.Net;
using System.Text.Json;
using Diten.InventoryService.Application.Features.Warehouse.Ports;
using Microsoft.Extensions.Logging;

namespace Diten.InventoryService.Infrastructure.Warehouse;

/// <summary>
/// Pack Q-13 (A) — the assignee check: AUTH <c>GET /api/users/{userId}/lookup-validation</c> (<c>UsersController.cs:133</c>, key
/// <c>auth.users.lookup-validation</c>) through the Gateway with the CALLER'S token and correlation id (<c>ForwardCallerHeadersHandler</c>).
/// 200 with <c>referenceable: true</c> → Valid; 404 (another tenant's user, an unknown or an inactive one — AUTH answers them alike) →
/// Invalid; anything else — 401 / 403, 400, 5xx, no answer, the 3 s bound — → Unavailable (the command answers 503 and writes nothing).
/// No cache: every assignment asks again.
/// </summary>
public sealed class AuthAssigneeDirectory : IWarehouseAssigneeDirectory
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    private readonly HttpClient _http;
    private readonly ILogger<AuthAssigneeDirectory> _logger;

    public AuthAssigneeDirectory(HttpClient http, ILogger<AuthAssigneeDirectory> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<AssigneeCheck> CheckAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);
        try
        {
            using var response = await _http.GetAsync($"api/users/{userId:D}/lookup-validation", timeout.Token);
            if (response.StatusCode == HttpStatusCode.NotFound) return AssigneeCheck.Invalid;
            if (response.StatusCode != HttpStatusCode.OK)
            {
                _logger.LogWarning("wms.assignee.check_unavailable {Status}", (int)response.StatusCode);
                return AssigneeCheck.Unavailable;
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object) root = data;
            return root.ValueKind == JsonValueKind.Object && root.TryGetProperty("referenceable", out var r) && r.ValueKind == JsonValueKind.True
                   && root.TryGetProperty("userId", out var id) && id.ValueKind == JsonValueKind.String && Guid.TryParse(id.GetString(), out var echoed) && echoed == userId
                ? AssigneeCheck.Valid
                : AssigneeCheck.Invalid;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException or JsonException
                                   && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("wms.assignee.check_unavailable {Reason}", ex.GetType().Name);
            return AssigneeCheck.Unavailable;
        }
    }
}
