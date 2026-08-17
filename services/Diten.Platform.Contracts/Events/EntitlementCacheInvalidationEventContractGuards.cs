namespace Diten.Platform.Contracts.Events;

internal static class EntitlementCacheInvalidationEventContractGuards
{
    public static Guid RequireId(Guid value, string parameterName)
    {
        return value == Guid.Empty
            ? throw new ArgumentException($"{parameterName} cannot be empty.", parameterName)
            : value;
    }

    public static DateTimeOffset RequireOccurredAtUtc(DateTimeOffset value, string parameterName)
    {
        if (value == default)
        {
            throw new ArgumentException($"{parameterName} cannot be default.", parameterName);
        }

        return value.Offset == TimeSpan.Zero
            ? value
            : throw new ArgumentException($"{parameterName} must be UTC.", parameterName);
    }

    public static string RequireModuleCode(string moduleCode)
    {
        if (string.IsNullOrWhiteSpace(moduleCode))
        {
            throw new ArgumentException("ModuleCode cannot be empty.", nameof(moduleCode));
        }

        return moduleCode.Trim().ToUpperInvariant();
    }

    public static string? OptionalStatus(string? status)
    {
        return string.IsNullOrWhiteSpace(status) ? null : status.Trim();
    }
}
