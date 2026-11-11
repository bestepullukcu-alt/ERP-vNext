namespace Diten.InventoryService.Application.Features.CycleCounting.Ports;

/// <summary>What the user directory says about the counter a count is assigned to (pack §4.1 <c>AssignedTo</c>, §12).</summary>
public enum CycleCountAssigneeCheck
{
    /// <summary>An active user of the caller's tenant.</summary>
    Valid,

    /// <summary>Not a user of the tenant, or not active → 422 <c>COUNT_ASSIGNEE_INVALID</c>.</summary>
    Invalid,

    /// <summary>The directory could not answer → 503 <c>COUNT_ASSIGNEE_DIRECTORY_UNAVAILABLE</c>, nothing written.</summary>
    Unavailable
}

/// <summary>
/// MOD-0181's own assignee port (pack §4.1: "MOD-0178 <c>IWarehouseAssigneeDirectory</c> emsali; 0181 kendi portunu açar"). The production
/// adapter asks the same AUTH <c>lookup-validation</c> through the existing MOD-0178 HTTP client (called, not changed).
/// </summary>
public interface ICycleCountAssigneeDirectory
{
    Task<CycleCountAssigneeCheck> CheckAsync(Guid userId, CancellationToken cancellationToken = default);
}
