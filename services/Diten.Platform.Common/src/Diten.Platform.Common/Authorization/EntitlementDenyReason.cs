namespace Diten.Platform.Common.Authorization;

public enum EntitlementDenyReason
{
    MissingPermission = 0,
    ModuleNotEntitled = 1,
    FeatureNotEnabled = 2,
    EntitlementExpired = 3,
    EntitlementDisabled = 4,
    PartnerScopeViolation = 5,
    TenantIsolationViolation = 6
}
