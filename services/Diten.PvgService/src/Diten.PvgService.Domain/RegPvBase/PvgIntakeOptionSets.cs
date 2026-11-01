namespace Diten.PvgService.Domain.RegPvBase;

// Single place of the PVG-controlled intake option sets until the reference-data publication path is selected
// (owner decision PVG-MOD0230-DALGA2-01 K1). Report type values wait for QPPV confirmation.
public static class PvgIntakeOptionSets
{
    public const string UnknownCountry = "unknown";

    public static IReadOnlySet<string> ReportTypes { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "Spontaneous",
        "Study",
        "Literature",
        "Solicited",
        "Other"
    };

    public static IReadOnlySet<string> ProductRoles { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "Suspect",
        "Concomitant"
    };

    public static IReadOnlySet<string> ReporterSelectionDirectories { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "Crm",
        "PersonDirectory"
    };
}
