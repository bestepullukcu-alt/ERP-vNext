namespace Diten.Platform.Application.Features.AccessGovernance.LegalEntityScopeResolution;

public static class TrustedLegalEntityScopeResolutionLimits
{
    public const int MaxCandidates = 200;
    public const int MaxRequestBodyBytes = 1024;
    public const int MinModuleCodeLength = 3;
    public const int MaxModuleCodeLength = 100;
    public const int MaxPermissionKeyLength = 200;

    public static bool IsValidModuleCode(string? value)
    {
        if (value is null || value.Length is < MinModuleCodeLength or > MaxModuleCodeLength)
        {
            return false;
        }

        return IsKebabSegment(value);
    }

    public static bool IsValidPermissionKey(string? value)
    {
        if (value is null || value.Length is < 1 or > MaxPermissionKeyLength)
        {
            return false;
        }

        var segments = value.Split('.');
        return segments.Length >= 3 && segments.All(IsKebabSegment);
    }

    private static bool IsKebabSegment(string value)
    {
        if (value.Length == 0 || value[0] == '-' || value[^1] == '-')
        {
            return false;
        }

        var previousHyphen = false;
        foreach (var character in value)
        {
            var isLowerAlphaNumeric = character is >= 'a' and <= 'z' or >= '0' and <= '9';
            if (!isLowerAlphaNumeric && character != '-')
            {
                return false;
            }

            if (character == '-' && previousHyphen)
            {
                return false;
            }

            previousHyphen = character == '-';
        }

        return true;
    }
}
