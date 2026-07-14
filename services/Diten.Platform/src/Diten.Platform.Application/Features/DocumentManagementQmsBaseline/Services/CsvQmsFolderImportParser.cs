using System.Text;
using Diten.Platform.Application.Features.DocumentManagementQmsBaseline;

namespace Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Services;

/// <summary>
/// MOD-0028-FU06 — parses the GMG-QMS-LOG-0007 register CSV package (<c>00_all_folders_2175.csv</c> and the per-wave
/// CSVs) into raw import rows. UTF-8 BOM aware; slash-path hierarchy via the <c>full_path</c> column so the shared
/// <see cref="QmsFolderTreeValidator"/> performs parent-before-child, level, and sibling-uniqueness validation
/// unchanged. Governance columns (access_profile, retention_class, legacy_code, provisioning_wave, …) are carried onto
/// the row so the register's governance is never lost. Governance metadata only — never a physical folder, upload, or
/// content store.
///
/// <para>Provisioning/IQ evidence columns (<c>platform_folder_id</c>, <c>platform_parent_id</c>, <c>created_on</c>,
/// <c>created_by</c>, <c>permissions_applied</c>, <c>verified_on</c>, <c>verified_by</c>, <c>deviation_comment</c>) are
/// intentionally NOT mapped to the domain in this FU; they belong to the later provisioning-evidence FU. They are
/// recognised but ignored here.</para>
/// </summary>
public sealed class CsvQmsFolderImportParser : IQmsFolderImportParser
{
    /// <summary>Evidence columns that belong to a later provisioning FU; recognised and ignored in FU06.</summary>
    public static readonly IReadOnlyList<string> FutureEvidenceColumns =
    [
        "platform_folder_id", "platform_parent_id", "created_on", "created_by",
        "permissions_applied", "verified_on", "verified_by", "deviation_comment"
    ];

    public bool Supports(string format, string fileName)
    {
        var normalized = (format ?? string.Empty).Trim().ToLowerInvariant();
        return normalized is "csv" or "text/csv"
            || (fileName ?? string.Empty).EndsWith(".csv", StringComparison.OrdinalIgnoreCase);
    }

    public Task<IReadOnlyList<QmsFolderImportRow>> ParseAsync(byte[] content, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // Strip a UTF-8 BOM if present (Excel-saved CSVs include one).
        var text = (content.Length >= 3 && content[0] == 0xEF && content[1] == 0xBB && content[2] == 0xBF)
            ? Encoding.UTF8.GetString(content, 3, content.Length - 3)
            : Encoding.UTF8.GetString(content);

        var lines = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length == 0)
        {
            return Task.FromResult<IReadOnlyList<QmsFolderImportRow>>([]);
        }

        var header = ParseCsvLine(lines[0]);
        var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < header.Count; i++)
        {
            var name = header[i].Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(name) && !headerMap.ContainsKey(name))
            {
                headerMap[name] = i;
            }
        }

        var rows = new List<QmsFolderImportRow>(lines.Length - 1);
        for (var i = 1; i < lines.Length; i++)
        {
            var cols = ParseCsvLine(lines[i]);

            string? Get(string key) =>
                headerMap.TryGetValue(key, out var idx) && idx < cols.Count && !string.IsNullOrWhiteSpace(cols[idx])
                    ? cols[idx].Trim()
                    : null;

            var fullPath = Get("full_path");
            var folderName = Get("folder_name");

            // A row with neither a path nor a name is left for the validator to skip/report (row-number preserved).
            rows.Add(new QmsFolderImportRow(
                SourceRowNumber: i,
                Path: fullPath,
                ParentPath: null,
                Name: folderName,
                PurposeScope: Get("purpose"),
                RequiredByScope: null,
                AllowsManualChildren: null,
                TemplatesAllowed: null,
                AllowedDocClass: null,
                DefaultClassificationLevel: null,
                DefaultRetentionHint: Get("retention_class"),
                IsMandatory: null,
                IsAutoProvisioned: null,
                IsProtected: null,
                DisplayOrder: int.TryParse(Get("provisioning_order"), out var order) ? order : null,
                OutlineCode: null,
                FolderId: Get("folder_id"),
                ParentFolderId: Get("parent_folder_id"),
                RegisterFullPath: fullPath,
                DepartmentDomain: Get("department_domain"),
                FolderType: Get("folder_type"),
                ExampleDocuments: Get("example_documents"),
                OwningDepartments: Get("owning_departments"),
                ControlledByGqms: Get("controlled_by_gqms"),
                SourceOfTruth: Get("source_of_truth"),
                OwnerFunction: Get("owner_function"),
                AccessProfile: Get("access_profile"),
                RetentionClass: Get("retention_class"),
                ChangeControlRequired: Get("change_control_required"),
                GqmsScopeLink: Get("gqms_scope_link"),
                LegacyCode: Get("legacy_code"),
                ProvisioningWave: Get("provisioning_wave"),
                ProvisioningOrder: int.TryParse(Get("provisioning_order"), out var po) ? po : null));
        }

        return Task.FromResult<IReadOnlyList<QmsFolderImportRow>>(rows);
    }

    /// <summary>Minimal RFC-4180 line splitter: honours double-quoted fields and escaped ("") quotes.</summary>
    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                    continue;
                }

                inQuotes = !inQuotes;
                continue;
            }

            if (ch == ',' && !inQuotes)
            {
                result.Add(current.ToString().Trim());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        result.Add(current.ToString().Trim());
        return result;
    }
}
