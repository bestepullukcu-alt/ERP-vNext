namespace Diten.PvgService.Application.RegPvBase;

public static class PvgValidationReasonCodes
{
    public const string TenantContextRequired = "PVG_TENANT_CONTEXT_REQUIRED";
    public const string RequiredFieldMissing = "PVG_REQUIRED_FIELD_MISSING";
    public const string FieldValueInvalid = "PVG_FIELD_VALUE_INVALID";
    public const string OperationNotAllowedInSlice = "PVG_OPERATION_NOT_ALLOWED_IN_SLICE";
    public const string AwarenessAfterReceived = "PVG_AWARENESS_AFTER_RECEIVED";
    public const string OwnerReadContractUnavailable = "PVG_OWNER_READ_CONTRACT_UNAVAILABLE";
    public const string ProductGskuRequiresGlobalProduct = "PVG_PRODUCT_GSKU_REQUIRES_GLOBAL_PRODUCT";
    public const string CountrySourceUnsettled = "PVG_COUNTRY_SOURCE_UNSETTLED";
    public const string ReporterSelectionIncomplete = "PVG_REPORTER_SELECTION_INCOMPLETE";
    public const string ReporterLinkRemovalRequired = "PVG_REPORTER_LINK_REMOVAL_REQUIRED";
    public const string ReporterLinkRemovalWithSelection = "PVG_REPORTER_LINK_REMOVAL_WITH_SELECTION";
    public const string ReporterLinkAbsent = "PVG_REPORTER_LINK_ABSENT";
}
