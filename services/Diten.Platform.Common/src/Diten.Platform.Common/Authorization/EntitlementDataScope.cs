namespace Diten.Platform.Common.Authorization;

public sealed record EntitlementDataScope(EntitlementDataScopeKind Kind, string? Value = null)
{
    public EntitlementDataScope(
        EntitlementDataScopeKind kind,
        Guid? scopeId,
        string? scopeCode,
        bool isInclude = true)
        : this(kind, null)
    {
        ScopeId = scopeId;
        ScopeCode = scopeCode;
        IsInclude = isInclude;
    }

    public Guid? ScopeId { get; init; }

    public string? ScopeCode { get; init; }

    public bool IsInclude { get; init; } = true;
}
