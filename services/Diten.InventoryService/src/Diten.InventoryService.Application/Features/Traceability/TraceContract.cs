namespace Diten.InventoryService.Application.Features.Traceability;

/// <summary>
/// TRACE-BUNDLE (MOD-0174) wire constants — <c>docs/analysis/contracts/trace.openapi.yaml</c> 1.1.0. Bodies stay
/// <c>contractVersion: "v1"</c> (v1.1 is additive to the v1 major, same rule as LOCATION 1.1.0).
/// </summary>
public static class TraceContract
{
    public const string BasePath = "/api/trace";

    public const string ModuleId = "MOD-0174";

    /// <summary>Self-registration module code (pack §14, CT-D5).</summary>
    public const string ModuleCode = "LOT-SERIAL-TRACKING";

    public const string DateTimeFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

    public const string DateFormat = "yyyy-MM-dd";

    public const int MaxSearchLength = 100;

    public const int MaxAttributeLength = 40;

    public const int MaxCoaLength = 60;

    public const int MaxTransformationReferenceLength = 60;

    public const int MaxSourceModuleLength = 40;

    public const int MaxGs1CodeLength = 200;
}
