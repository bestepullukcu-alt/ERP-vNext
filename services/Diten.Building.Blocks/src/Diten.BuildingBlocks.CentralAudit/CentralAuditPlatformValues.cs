namespace Diten.BuildingBlocks.CentralAudit;

/// <summary>
/// Platform's audit enums by NAME → the number its JSON binder reads (<c>AuditAppendRequest</c> binds enums from numbers;
/// the Platform API registers no string-enum converter — the MDM and Auth forwarders send numbers for the same reason).
/// A mirror of <c>services/Diten.Platform/src/Diten.Platform.Domain/Enums/AuditEnums.cs</c>; the test
/// <c>CentralAuditPlatformValuesTests</c> reads that file and fails when the two drift.
/// </summary>
public static class CentralAuditPlatformValues
{
    public static readonly IReadOnlyDictionary<string, int> Categories = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["Security"] = 1,
        ["IdentityAccess"] = 2,
        ["TenantAdministration"] = 3,
        ["PlatformConfiguration"] = 4,
        ["ReferenceData"] = 5,
        ["ModuleCatalog"] = 6,
        ["SubscriptionBilling"] = 7,
        ["Quota"] = 8,
        ["DataExport"] = 9,
        ["DataPrivacy"] = 10,
        ["Integration"] = 11,
        ["System"] = 12,
        ["DocumentManagement"] = 13,
        ["MasterData"] = 14,
        ["PortfolioDelivery"] = 15,
        ["Tasks"] = 16,
        ["ESignature"] = 17
    };

    public static readonly IReadOnlyDictionary<string, int> Operations = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["Create"] = 1,
        ["Update"] = 2,
        ["Delete"] = 3,
        ["Activate"] = 4,
        ["Deactivate"] = 5,
        ["Suspend"] = 6,
        ["Reactivate"] = 7,
        ["Assign"] = 8,
        ["Revoke"] = 9,
        ["Export"] = 10,
        ["Redact"] = 11,
        ["Login"] = 12,
        ["Logout"] = 13,
        ["PermissionDenied"] = 14,
        ["Execute"] = 15,
        ["LifecycleTransition"] = 16
    };

    public static readonly IReadOnlyDictionary<string, int> ActorTypes = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        [CentralAuditActorTypes.PlatformAdministrator] = 1,
        [CentralAuditActorTypes.PartnerAdministrator] = 2,
        [CentralAuditActorTypes.TenantUser] = 3,
        [CentralAuditActorTypes.System] = 4,
        [CentralAuditActorTypes.Service] = 5
    };

    /// <summary>Platform <c>AuditOutcome.Succeeded</c> / <c>Failed</c> (Failed = the command failed after part of its data committed).</summary>
    public const int OutcomeSucceeded = 1;

    public const int OutcomeFailed = 2;

    public static int OutcomeOf(string outcome) => outcome == CentralAuditOutcomes.Failed ? OutcomeFailed : OutcomeSucceeded;

    public static bool IsKnown(CentralAuditMetadata metadata) =>
        Categories.ContainsKey(metadata.Category) && Operations.ContainsKey(metadata.Operation);
}
