namespace Diten.Platform.Common.Authorization;

public sealed record TemporaryAccessGrant
{
    public TemporaryAccessGrant(
        string processInstanceId,
        string moduleCode,
        string? featureCode,
        DateTimeOffset expiresAtUtc,
        IReadOnlyList<EntitlementDataScope>? dataScopes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processInstanceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleCode);

        ProcessInstanceId = processInstanceId;
        ModuleCode = moduleCode;
        FeatureCode = featureCode;
        ExpiresAtUtc = expiresAtUtc;
        DataScopes = dataScopes is null ? Array.Empty<EntitlementDataScope>() : dataScopes.ToArray();
    }

    public string ProcessInstanceId { get; init; }

    public string ModuleCode { get; init; }

    public string? FeatureCode { get; init; }

    public DateTimeOffset ExpiresAtUtc { get; init; }

    public IReadOnlyList<EntitlementDataScope> DataScopes { get; init; }
}
