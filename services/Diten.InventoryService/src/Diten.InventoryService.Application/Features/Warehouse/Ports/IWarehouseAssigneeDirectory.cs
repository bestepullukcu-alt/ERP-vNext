namespace Diten.InventoryService.Application.Features.Warehouse.Ports;

/// <summary>What AUTH says about a user a task is assigned to.</summary>
public enum AssigneeCheck
{
    /// <summary>An active user of the caller's tenant.</summary>
    Valid,

    /// <summary>Not a user of the tenant, or not active (AUTH 404) → 422 <c>WMS_ASSIGNEE_INVALID</c>.</summary>
    Invalid,

    /// <summary>AUTH could not answer (no answer, timeout, 5xx, 401 / 403) → 503, nothing written.</summary>
    Unavailable
}

/// <summary>
/// Pack Q-13 (decision A, Beste 2026-10-30): the assignee of a task is checked against AUTH
/// <c>GET /api/users/{userId}/lookup-validation</c> (<c>auth.users.lookup-validation</c>) through the Gateway with the CALLER'S token. A user
/// request only — the reconciler never assigns. AUTH checks the tenant and the user's activity, NOT the company (DL-2 open point).
/// </summary>
public interface IWarehouseAssigneeDirectory
{
    Task<AssigneeCheck> CheckAsync(Guid userId, CancellationToken cancellationToken = default);
}
