namespace Diten.Platform.Domain.Entities;

/// <summary>
/// Transaction-owned snapshot used by entitlement applicability readers. It is deliberately
/// separate from the operator-owned catalog business documents.
/// </summary>
public sealed class GlobalApplicabilityState
{
    public required string Id { get; init; }
    public required string Kind { get; init; }
    public Guid SourceId { get; init; }
    public required string Code { get; init; }
    public bool IsDeleted { get; init; }
    public bool IsActive { get; init; }
    public bool IsBaseline { get; init; }
    public bool IsCoreModule { get; init; }
    public bool IsTenantAssignable { get; init; }
    public IReadOnlyList<string> IncludedModuleKeys { get; init; } = [];
    public ulong GlobalVersion { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
