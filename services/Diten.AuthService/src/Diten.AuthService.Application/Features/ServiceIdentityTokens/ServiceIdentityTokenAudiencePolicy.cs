namespace Diten.AuthService.Application.Features.ServiceIdentityTokens;

public static class ServiceIdentityTokenAudiencePolicy
{
    public const string MdmServiceName = "Diten.MDM";
    public const string TrustedAuditSourceIngest = "TRUSTED_AUDIT_SOURCE_INGEST";
    public const string TrustedWorkflowConsumer = "TRUSTED_WORKFLOW_CONSUMER";

    public static bool IsAllowedAudience(string audience) =>
        string.Equals(audience, TrustedAuditSourceIngest, StringComparison.Ordinal)
        || string.Equals(audience, TrustedWorkflowConsumer, StringComparison.Ordinal);

    public static bool IsAllowedPair(string serviceName, string audience) =>
        string.Equals(serviceName, MdmServiceName, StringComparison.Ordinal)
        && IsAllowedAudience(audience);

    public static bool TryResolveIdentityAudience(string? persistedAudience, out string audience)
    {
        if (string.IsNullOrEmpty(persistedAudience))
        {
            audience = TrustedAuditSourceIngest;
            return true;
        }

        if (IsAllowedAudience(persistedAudience))
        {
            audience = persistedAudience;
            return true;
        }

        audience = string.Empty;
        return false;
    }
}
