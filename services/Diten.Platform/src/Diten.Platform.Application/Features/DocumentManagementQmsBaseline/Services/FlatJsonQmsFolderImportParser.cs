using System.Text.Json;
using Diten.Platform.Application.Features.DocumentManagementQmsBaseline;

namespace Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Services;

/// <summary>
/// MOD-0028-FU06 — parses the register flat-array JSON package (<c>00_folder_list_flat.json</c>:
/// <c>{ "register": "...", "version": "...", "folders": [ … ] }</c>) into raw import rows, mapping the same
/// governance columns as <see cref="CsvQmsFolderImportParser"/>. Hierarchy is expressed via each folder's
/// <c>full_path</c>, so the shared <see cref="QmsFolderTreeValidator"/> validates parent-before-child, level, and
/// sibling-uniqueness unchanged. Governance metadata only — never a physical folder or content store.
/// </summary>
public sealed class FlatJsonQmsFolderImportParser : IQmsFolderImportParser
{
    public bool Supports(string format, string fileName)
    {
        var normalized = (format ?? string.Empty).Trim().ToLowerInvariant();
        // "flat-json" disambiguates from any other JSON import; a *.json filename is also accepted.
        return normalized is "json" or "flat-json" or "application/json"
            || (fileName ?? string.Empty).EndsWith(".json", StringComparison.OrdinalIgnoreCase);
    }

    public Task<IReadOnlyList<QmsFolderImportRow>> ParseAsync(byte[] content, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var (_, rows) = ParseWithMetadata(content);
        return Task.FromResult(rows);
    }

    /// <summary>
    /// Parses the package and also returns its register/version/status header metadata. Exposed so a caller can
    /// derive the DRAFT <c>BaselineRelease</c> source key/version from the package itself (server-side) rather than
    /// from a client payload. Throws <see cref="QmsWorkbookFormatException"/>("invalid_flat_json_package") on
    /// malformed JSON so the import service maps it to a controlled VALIDATION_FAILED.
    /// </summary>
    public (FlatJsonRegisterMetadata? Metadata, IReadOnlyList<QmsFolderImportRow> Rows) ParseWithMetadata(byte[] content)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(content);
        }
        catch (JsonException)
        {
            throw new QmsWorkbookFormatException("invalid_flat_json_package");
        }

        using (document)
        {
            var root = document.RootElement;

            // Accept either an object envelope { register, version, folders:[…] } or a bare array […].
            JsonElement folders;
            FlatJsonRegisterMetadata? metadata = null;
            if (root.ValueKind == JsonValueKind.Object)
            {
                metadata = new FlatJsonRegisterMetadata(
                    ReadString(root, "register"),
                    ReadString(root, "version"),
                    ReadString(root, "status"));

                if (!root.TryGetProperty("folders", out folders) || folders.ValueKind != JsonValueKind.Array)
                {
                    throw new QmsWorkbookFormatException("invalid_flat_json_package");
                }
            }
            else if (root.ValueKind == JsonValueKind.Array)
            {
                folders = root;
            }
            else
            {
                throw new QmsWorkbookFormatException("invalid_flat_json_package");
            }

            var rows = new List<QmsFolderImportRow>();
            var rowNumber = 0;
            foreach (var f in folders.EnumerateArray())
            {
                if (f.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                rowNumber++;
                var fullPath = ReadString(f, "full_path");
                rows.Add(new QmsFolderImportRow(
                    SourceRowNumber: rowNumber,
                    Path: fullPath,
                    ParentPath: null,
                    Name: ReadString(f, "folder_name"),
                    PurposeScope: ReadString(f, "purpose"),
                    RequiredByScope: null,
                    AllowsManualChildren: null,
                    TemplatesAllowed: null,
                    AllowedDocClass: null,
                    DefaultClassificationLevel: null,
                    DefaultRetentionHint: ReadString(f, "retention_class"),
                    IsMandatory: null,
                    IsAutoProvisioned: null,
                    IsProtected: null,
                    DisplayOrder: ReadInt(f, "provisioning_order"),
                    OutlineCode: null,
                    FolderId: ReadString(f, "folder_id"),
                    ParentFolderId: ReadString(f, "parent_folder_id"),
                    RegisterFullPath: fullPath,
                    DepartmentDomain: ReadString(f, "department_domain"),
                    FolderType: ReadString(f, "folder_type"),
                    ExampleDocuments: ReadString(f, "example_documents"),
                    OwningDepartments: ReadString(f, "owning_departments"),
                    ControlledByGqms: ReadString(f, "controlled_by_gqms"),
                    SourceOfTruth: ReadString(f, "source_of_truth"),
                    OwnerFunction: ReadString(f, "owner_function"),
                    AccessProfile: ReadString(f, "access_profile"),
                    RetentionClass: ReadString(f, "retention_class"),
                    ChangeControlRequired: ReadString(f, "change_control_required"),
                    GqmsScopeLink: ReadString(f, "gqms_scope_link"),
                    LegacyCode: ReadString(f, "legacy_code"),
                    ProvisioningWave: ReadString(f, "provisioning_wave"),
                    ProvisioningOrder: ReadInt(f, "provisioning_order")));
            }

            return (metadata, rows);
        }
    }

    private static string? ReadString(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var el))
        {
            return null;
        }

        var value = el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.ToString(),
            _ => null
        };

        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static int? ReadInt(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var el))
        {
            return null;
        }

        return el.ValueKind switch
        {
            JsonValueKind.Number when el.TryGetInt32(out var n) => n,
            JsonValueKind.String when int.TryParse(el.GetString(), out var s) => s,
            _ => null
        };
    }
}

/// <summary>Register header metadata read from a flat-JSON package envelope (source identity, not per-folder data).</summary>
public sealed record FlatJsonRegisterMetadata(string? Register, string? Version, string? Status);
