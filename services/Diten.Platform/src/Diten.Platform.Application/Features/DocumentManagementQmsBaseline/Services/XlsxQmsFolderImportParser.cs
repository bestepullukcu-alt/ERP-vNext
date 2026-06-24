using System.IO.Compression;
using System.Xml.Linq;

namespace Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Services;

/// <summary>
/// Reads an .xlsx QMS folder workbook (Office Open XML) without any third-party dependency by treating the file as a
/// zip archive (<see cref="ZipArchive"/>) and parsing worksheet / shared-strings XML — mirroring the established
/// repo convention (see XlsxBusinessReferenceDataImportParser). The first row element encountered is treated as the
/// header (so a header that starts at row 3, as in the real workbook, is handled because rows 1–2 carry no cells).
///
/// <para>Sheet selection: the parser selects the canonical sheet by NAME (<see cref="CanonicalSheetName"/> =
/// "last version") via the workbook relationships, not by a hardcoded index. If the canonical sheet is absent and no
/// fallback worksheet exists, it throws <see cref="QmsWorkbookFormatException"/>("canonical_sheet_not_found"), which
/// the import service maps to a controlled <c>VALIDATION_FAILED</c>.</para>
///
/// <para>Hierarchy — canonical mode: the <b>dotted outline code</b> column ("Folder (full path)" → <c>outline_code</c>,
/// e.g. <c>0</c>, <c>0.01</c>, <c>00.01.01</c>) plus the atomic <c>Folder name</c> column. The code is emitted as
/// <see cref="QmsFolderImportRow.OutlineCode"/> and resolved into a nested tree by
/// <c>DottedOutlineTreeBuilder</c> (numeric normalization; the code is NEVER used as a name/segment). Helper modes for
/// non-canonical sheets/fixtures: explicit slash <c>path</c>/<c>full_path</c>, <c>parent_path</c>+<c>name</c>, or
/// level columns <c>1st/2nd/3rd/4th</c> → <c>level1..level6</c>.</para>
///
/// <para>Header synonyms (case-insensitive): <c>folder (full path)</c>→outline_code, <c>folder name</c>→name,
/// <c>purpose / scope</c>→purpose_scope, <c>retention &amp; class</c>/<c>retention</c>→default_retention_hint,
/// <c>security class</c>→default_classification_level, plus the snake_case fixture keys.</para>
/// </summary>
public sealed class XlsxQmsFolderImportParser : IQmsFolderImportParser
{
    /// <summary>Canonical import sheet for the FU02 source-format contract (chosen by the user after validation).</summary>
    public const string CanonicalSheetName = "last version";

    private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace Pkg = "http://schemas.openxmlformats.org/package/2006/relationships";

    public bool Supports(string format, string fileName)
    {
        var normalized = (format ?? string.Empty).Trim().ToLowerInvariant();
        return normalized is "xlsx" or "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            || (fileName ?? string.Empty).EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase);
    }

    public Task<IReadOnlyList<QmsFolderImportRow>> ParseAsync(byte[] content, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        using var stream = new MemoryStream(content);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var sharedStrings = ReadSharedStrings(archive);
        var sheetEntry = ResolveCanonicalSheet(archive);

        XDocument sheet;
        using (var sheetStream = sheetEntry.Open())
        {
            sheet = XDocument.Load(sheetStream);
        }

        var rows = sheet.Root?.Element(Main + "sheetData")?.Elements(Main + "row").ToList()
            ?? [];

        var result = new List<QmsFolderImportRow>();
        Dictionary<string, int>? headerMap = null;
        var dataRowNumber = 0;

        foreach (var row in rows)
        {
            var cells = new Dictionary<int, string>();
            foreach (var c in row.Elements(Main + "c"))
            {
                var reference = (string?)c.Attribute("r") ?? string.Empty;
                cells[ColumnIndex(reference)] = ReadCellValue(c, sharedStrings);
            }

            if (headerMap is null)
            {
                headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var kv in cells)
                {
                    var name = NormalizeHeader(kv.Value);
                    if (!string.IsNullOrEmpty(name) && !headerMap.ContainsKey(name))
                    {
                        headerMap[name] = kv.Key;
                    }
                }

                continue;
            }

            if (cells.Values.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            string? Get(params string[] keys)
            {
                foreach (var key in keys)
                {
                    if (headerMap!.TryGetValue(key, out var idx) && cells.TryGetValue(idx, out var val) && !string.IsNullOrWhiteSpace(val))
                    {
                        return val;
                    }
                }

                return null;
            }

            dataRowNumber++;
            result.Add(new QmsFolderImportRow(
                dataRowNumber,
                Get("path", "full_path") ?? DeriveLevelPath(headerMap, cells),
                Get("parent_path"),
                Get("name", "folder_name"),
                Get("purpose_scope"),
                Get("required_by_scope"),
                ParseBool(Get("allows_manual_children")),
                ParseBool(Get("templates_allowed")),
                Get("allowed_doc_class"),
                Get("default_classification_level"),
                Get("default_retention_hint"),
                ParseBool(Get("is_mandatory")),
                ParseBool(Get("is_auto_provisioned")),
                ParseBool(Get("is_protected")),
                int.TryParse(Get("display_order", "sort_order"), out var order) ? order : null,
                Get("outline_code")));
        }

        return Task.FromResult<IReadOnlyList<QmsFolderImportRow>>(result);
    }

    /// <summary>
    /// Maps a raw worksheet header to a canonical key. Accepts both the synthetic snake_case keys (used by fixtures)
    /// and the human-readable headers found in the real "Configuraiton of QMS folders v2" workbook. Level headers
    /// (<c>1st/2nd/3rd/4th</c>, incl. the workbook's <c>4rd</c> typo) map to <c>level1..level6</c> for the
    /// level-column hierarchy mode. The ambiguous "Folder (full path)" column (which holds a dotted outline code,
    /// not a slash path) is deliberately NOT mapped to <c>path</c>.
    /// </summary>
    private static string NormalizeHeader(string raw)
    {
        var name = raw.Trim().ToLowerInvariant();
        return name switch
        {
            "1st" or "level 1" => "level1",
            "2nd" or "level 2" => "level2",
            "3rd" or "level 3" => "level3",
            "4th" or "4rd" or "level 4" => "level4",
            "5th" or "level 5" => "level5",
            "6th" or "level 6" => "level6",
            "folder (full path)" => "outline_code",
            "folder name" => "name",
            "purpose / scope" or "purpose/scope" or "purpose (what it's for)" or "purpose (what it’s for)" => "purpose_scope",
            "retention & class" or "retention and class" or "retention" => "default_retention_hint",
            "security class" => "default_classification_level",
            _ => name
        };
    }

    /// <summary>
    /// Builds a slash path from the level columns (<c>level1..level6</c>) when present, joining the non-empty level
    /// values left-to-right. This is the clean, deterministic hierarchy encoding used by the workbook's "Arkusz1"
    /// sheet. Returns null when no level columns are populated.
    /// </summary>
    private static string? DeriveLevelPath(Dictionary<string, int> headerMap, Dictionary<int, string> cells)
    {
        var segments = new List<string>(6);
        for (var level = 1; level <= 6; level++)
        {
            if (headerMap.TryGetValue($"level{level}", out var idx)
                && cells.TryGetValue(idx, out var val)
                && !string.IsNullOrWhiteSpace(val))
            {
                segments.Add(val.Trim());
            }
        }

        return segments.Count == 0 ? null : string.Join('/', segments);
    }

    private static bool? ParseBool(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var v = raw.Trim().ToLowerInvariant();
        return v is "1" or "true" or "yes" or "y" ? true
            : v is "0" or "false" or "no" or "n" ? false
            : null;
    }

    /// <summary>
    /// Resolves the canonical sheet (<see cref="CanonicalSheetName"/>) by name through the workbook relationships.
    /// Throws <see cref="QmsWorkbookFormatException"/>("canonical_sheet_not_found") when it is absent.
    /// </summary>
    private static ZipArchiveEntry ResolveCanonicalSheet(ZipArchive archive)
    {
        var workbookEntry = archive.GetEntry("xl/workbook.xml")
            ?? throw new QmsWorkbookFormatException("canonical_sheet_not_found");
        XDocument workbook;
        using (var s = workbookEntry.Open())
        {
            workbook = XDocument.Load(s);
        }

        var sheetEl = workbook.Root?.Element(Main + "sheets")?.Elements(Main + "sheet")
            .FirstOrDefault(e => string.Equals((string?)e.Attribute("name"), CanonicalSheetName, StringComparison.OrdinalIgnoreCase));
        var rid = (string?)sheetEl?.Attribute(Rel + "id");
        if (string.IsNullOrEmpty(rid))
        {
            throw new QmsWorkbookFormatException("canonical_sheet_not_found");
        }

        var relsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels")
            ?? throw new QmsWorkbookFormatException("canonical_sheet_not_found");
        XDocument rels;
        using (var s = relsEntry.Open())
        {
            rels = XDocument.Load(s);
        }

        var target = rels.Root?.Elements(Pkg + "Relationship")
            .FirstOrDefault(e => string.Equals((string?)e.Attribute("Id"), rid, StringComparison.Ordinal))
            ?.Attribute("Target")?.Value;
        if (string.IsNullOrWhiteSpace(target))
        {
            throw new QmsWorkbookFormatException("canonical_sheet_not_found");
        }

        var normalized = target.Replace('\\', '/').TrimStart('/');
        var fullName = normalized.StartsWith("xl/", StringComparison.OrdinalIgnoreCase) ? normalized : "xl/" + normalized;
        return archive.GetEntry(fullName)
            ?? archive.Entries.FirstOrDefault(e => e.FullName.EndsWith(normalized, StringComparison.OrdinalIgnoreCase))
            ?? throw new QmsWorkbookFormatException("canonical_sheet_not_found");
    }

    private static IReadOnlyList<string> ReadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null)
        {
            return [];
        }

        XDocument doc;
        using (var s = entry.Open())
        {
            doc = XDocument.Load(s);
        }

        var list = new List<string>();
        foreach (var si in doc.Root?.Elements(Main + "si") ?? Enumerable.Empty<XElement>())
        {
            list.Add(string.Concat(si.Descendants(Main + "t").Select(t => t.Value)));
        }

        return list;
    }

    private static string ReadCellValue(XElement cell, IReadOnlyList<string> sharedStrings)
    {
        var type = (string?)cell.Attribute("t");
        if (type == "inlineStr")
        {
            return string.Concat(cell.Element(Main + "is")?.Descendants(Main + "t").Select(t => t.Value)
                ?? Enumerable.Empty<string>());
        }

        var v = cell.Element(Main + "v")?.Value;
        if (string.IsNullOrEmpty(v))
        {
            return string.Empty;
        }

        if (type == "s" && int.TryParse(v, out var idx) && idx >= 0 && idx < sharedStrings.Count)
        {
            return sharedStrings[idx];
        }

        if (type == "b")
        {
            return v == "1" ? "true" : "false";
        }

        return v;
    }

    private static int ColumnIndex(string cellReference)
    {
        var index = 0;
        foreach (var ch in cellReference)
        {
            if (ch is >= 'A' and <= 'Z')
            {
                index = (index * 26) + (ch - 'A' + 1);
            }
            else if (ch is >= 'a' and <= 'z')
            {
                index = (index * 26) + (ch - 'a' + 1);
            }
            else
            {
                break;
            }
        }

        return index - 1;
    }
}
