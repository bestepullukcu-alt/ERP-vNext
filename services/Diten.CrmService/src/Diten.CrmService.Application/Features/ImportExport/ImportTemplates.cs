namespace Diten.CrmService.Application.Features.ImportExport;

/// <summary>Canonical CSV column headers — shared by import-template and export outputs so a round trip is possible.</summary>
public static class ImportTemplates
{
    public static readonly IReadOnlyList<string> ContactHeader = new[]
    {
        "ExternalSourceSystem", "ExternalId", "FirstName", "LastName", "DisplayName", "ContactType",
        "ProfessionalTitle", "Specialty", "Department", "Phone", "Email", "Status", "Notes"
    };

    public static readonly IReadOnlyList<string> AccountContactHeader = new[]
    {
        "AccountCode", "AccountId", "ContactExternalSourceSystem", "ContactExternalId", "ContactId",
        "RoleCode", "IsPrimary", "ValidFrom", "ValidTo", "Notes"
    };

    public static readonly IReadOnlyList<string> AccountRelationshipHeader = new[]
    {
        "SourceAccountCode", "SourceAccountId", "TargetAccountCode", "TargetAccountId",
        "RelationshipType", "Status", "ValidFrom", "ValidTo", "Notes"
    };

    public static string TemplateCsv(IReadOnlyList<string> header) => string.Join(",", header) + "\n";
}
