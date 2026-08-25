namespace Diten.PvgService.Application.RegPvBase;

public static class PvgValidationReasonCodes
{
    public const string TenantContextRequired = "PVG_TENANT_CONTEXT_REQUIRED";
    public const string RequiredFieldMissing = "PVG_REQUIRED_FIELD_MISSING";
    public const string FieldValueInvalid = "PVG_FIELD_VALUE_INVALID";
    public const string OperationNotAllowedInSlice = "PVG_OPERATION_NOT_ALLOWED_IN_SLICE";
}
