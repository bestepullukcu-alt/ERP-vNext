namespace Diten.Platform.Application.Features.DocumentManagementContract;

public static class DocumentManagementRoutes
{
    public const string ApiFamily = "api/v1/document-management";
    public const string Contract = ApiFamily + "/contract";
}

public static class DocumentManagementPermissions
{
    public const string ContractView = "platform.document-management.contract.view";
    public const string CollectionDefinitionsList = "platform.document-management.collection-definitions.list";
    public const string CollectionDefinitionsView = "platform.document-management.collection-definitions.view";
    public const string BaselineReleasesList = "platform.document-management.baseline-releases.list";
    public const string CorporateRootInitialize = "platform.document-management.corporate-root.initialize";
    public const string CollectionInstancesView = "platform.document-management.collection-instances.view";

    public static readonly IReadOnlyList<string> RequiredForFu01 =
    [
        ContractView,
        CollectionDefinitionsList,
        CollectionDefinitionsView,
        BaselineReleasesList,
        CorporateRootInitialize,
        CollectionInstancesView
    ];
}

public static class DocumentManagementReasonCodes
{
    public const string PermissionDenied = "PERM_DENIED";
}

public static class DocumentManagementFeatureFlags
{
    public const string CorporateRoot = "mod0028.corporate_root.enabled";
    public const string CompanyProvisioning = "mod0028.company_provisioning.enabled";
    public const string Mod0220Fallback = "mod0028.mod0220_fallback.enabled";
    public const string InstantiationRetry = "mod0028.instantiation_retry.enabled";
    public const string ManualLocalNodes = "mod0028.manual_local_nodes.enabled";
    public const string Exceptions = "mod0028.exceptions.enabled";
    public const string PositionScope = "mod0028.position_scope.enabled";
    public const string PersonScope = "mod0028.person_scope.enabled";
    public const string WorkflowIntegration = "mod0028.workflow_integration.enabled";
}

public sealed class DocumentManagementFeatureFlagOptions
{
    public const string SectionName = "DocumentManagement:FeatureFlags";

    public bool CorporateRootEnabled { get; set; } = true;
    public bool CompanyProvisioningEnabled { get; set; } = true;
    public bool Mod0220FallbackEnabled { get; set; }
    public bool InstantiationRetryEnabled { get; set; }
    public bool ManualLocalNodesEnabled { get; set; } = true;
    public bool ExceptionsEnabled { get; set; }
    public bool PositionScopeEnabled { get; set; }
    public bool PersonScopeEnabled { get; set; }
    public bool WorkflowIntegrationEnabled { get; set; }

    public IReadOnlyDictionary<string, bool> ToSummary() => new Dictionary<string, bool>(StringComparer.Ordinal)
    {
        [DocumentManagementFeatureFlags.CorporateRoot] = CorporateRootEnabled,
        [DocumentManagementFeatureFlags.CompanyProvisioning] = CompanyProvisioningEnabled,
        [DocumentManagementFeatureFlags.Mod0220Fallback] = Mod0220FallbackEnabled,
        [DocumentManagementFeatureFlags.InstantiationRetry] = InstantiationRetryEnabled,
        [DocumentManagementFeatureFlags.ManualLocalNodes] = ManualLocalNodesEnabled,
        [DocumentManagementFeatureFlags.Exceptions] = ExceptionsEnabled,
        [DocumentManagementFeatureFlags.PositionScope] = PositionScopeEnabled,
        [DocumentManagementFeatureFlags.PersonScope] = PersonScopeEnabled,
        [DocumentManagementFeatureFlags.WorkflowIntegration] = WorkflowIntegrationEnabled
    };
}

public sealed record DocumentManagementContractResponse(
    string ModuleId,
    string ModuleName,
    string ParentModuleId,
    string ApiFamily,
    string ContractVersion,
    IReadOnlyList<string> ActiveScopes,
    IReadOnlyList<string> DeferredScopes,
    IReadOnlyDictionary<string, bool> FeatureFlags,
    IReadOnlyList<string> PermissionsRequiredForFu01,
    bool Mod0220ValidatorRegistered,
    bool AuditCorrelationReady,
    string BusinessCapabilityStatus,
    string Warning);
