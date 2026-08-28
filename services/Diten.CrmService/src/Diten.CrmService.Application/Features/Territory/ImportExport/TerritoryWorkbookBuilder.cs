using ClosedXML.Excel;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.ImportExport.Xlsx;

namespace Diten.CrmService.Application.Features.Territory.ImportExport;

/// <summary>
/// MOD-0151 FU08 workbook writer (pack §22.5). The same writer produces the empty import template and the data
/// export, so an exported file round-trips into the import reader column-for-column.
///
/// <para>Every data cell is written as TEXT: an exported GUID, ISO-8601 date or leading-zero code must survive
/// Excel's locale-dependent auto-conversion unchanged. Dropdowns are built from the live MOD-0048 published values on
/// the ReferenceValues sheet — an unpublished set simply gets no dropdown, never a hardcoded list.</para>
/// </summary>
public static class TerritoryWorkbookBuilder
{
    private const string TextFormat = "@";
    private const int TemplateValidationRows = 500;
    private const int ExportValidationBuffer = 100;

    public static byte[] Build(TerritoryWorkbookRequest request)
    {
        using var workbook = new XLWorkbook();

        // ReferenceValues first: the dropdowns on the data sheets point at its ranges.
        var referenceSheet = workbook.Worksheets.Add(TerritoryWorkbookSchema.ReferenceValuesSheet);
        var setRanges = WriteReferenceValues(referenceSheet, request.ReferenceSets);

        var notes = workbook.Worksheets.Add(TerritoryWorkbookSchema.ValidationNotesSheet);
        WriteValidationNotes(notes, request);

        foreach (var sheet in SheetOrder(request))
        {
            var columns = TerritoryWorkbookSchema.ColumnsFor(sheet);
            if (columns.Count == 0) continue;

            var rows = request.Sheets.TryGetValue(sheet, out var data) ? data : [];
            WriteDataSheet(
                workbook.Worksheets.Add(sheet), sheet, columns, rows,
                TerritoryWorkbookSchema.ColumnSetsFor(sheet), setRanges, referenceSheet,
                request.IsTemplate, IsImportable(sheet));
        }

        // ValidationNotes is the landing sheet — the boundary rules must be read before anything is typed.
        workbook.Worksheets.Worksheet(TerritoryWorkbookSchema.ValidationNotesSheet).Position = 1;
        notes.SetTabActive();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static bool IsImportable(string sheet)
        => TerritoryWorkbookSchema.ImportableSheets.Contains(sheet, StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<string> SheetOrder(TerritoryWorkbookRequest request)
        => request.IsTemplate
            ? TerritoryWorkbookSchema.TemplateSheets.Where(s =>
                s != TerritoryWorkbookSchema.ValidationNotesSheet && s != TerritoryWorkbookSchema.ReferenceValuesSheet)
            : new[]
            {
                TerritoryWorkbookSchema.ModelSheet,
                TerritoryWorkbookSchema.NodesSheet,
                TerritoryWorkbookSchema.AssignmentRulesSheet,
                TerritoryWorkbookSchema.AccountAssignmentsSheet,
                TerritoryWorkbookSchema.ResourceAssignmentsSheet,
                TerritoryWorkbookSchema.CoverageSummarySheet,
                TerritoryWorkbookSchema.PlanVsCurrentSheet
            };

    // ---- sheets --------------------------------------------------------------------------------------------------

    private static Dictionary<string, (int First, int Last)> WriteReferenceValues(
        IXLWorksheet sheet, IReadOnlyList<ReferenceSetSnapshot> sets)
    {
        WriteHeader(sheet, TerritoryWorkbookSchema.ReferenceValueColumns);

        var ranges = new Dictionary<string, (int First, int Last)>(StringComparer.OrdinalIgnoreCase);
        var row = 2;

        foreach (var set in sets)
        {
            if (!set.IsPublished || set.Values.Count == 0)
            {
                // Controlled dependency, visible to the operator: the set exists in the contract but is not published.
                // No CRM local seed / fallback value is ever written here.
                sheet.Cell(row, 1).Value = set.SetCode;
                sheet.Cell(row, 2).Value = TerritoryWorkbookSchema.NotPublishedMarker;
                sheet.Cell(row, 3).Value = set.IsPublished
                    ? "Set is published but has no values yet."
                    : "Not published in MOD-0048 for this tenant yet.";
                sheet.Cell(row, 5).Value = "FALSE";
                sheet.Cell(row, 6).Value = "FALSE";
                sheet.Row(row).Style.Font.FontColor = XLColor.FromHtml("#A0132B");
                row++;
                continue;
            }

            var first = row;
            var lastSelectable = 0;
            foreach (var value in set.Values
                         .OrderByDescending(v => v.IsActive)
                         .ThenBy(v => v.ValueCode, StringComparer.OrdinalIgnoreCase))
            {
                sheet.Cell(row, 1).Value = set.SetCode;
                sheet.Cell(row, 2).Value = value.ValueCode;
                sheet.Cell(row, 3).Value = value.DisplayName ?? string.Empty;
                sheet.Cell(row, 4).Value = value.Description ?? string.Empty;
                sheet.Cell(row, 5).Value = value.IsActive ? "TRUE" : "FALSE";
                sheet.Cell(row, 6).Value = value.IsDeprecated ? "TRUE" : "FALSE";
                sheet.Cell(row, 7).Value = FormatAttributes(value.Attributes);

                if (value.IsDeprecated)
                {
                    sheet.Row(row).Style.Font.FontColor = XLColor.FromHtml("#8A8D93");
                    sheet.Row(row).Style.Font.Italic = true;
                }
                else
                {
                    lastSelectable = row;
                }

                row++;
            }

            if (lastSelectable >= first)
            {
                ranges[set.SetCode] = (first, lastSelectable);
            }
        }

        FinishSheet(sheet, TerritoryWorkbookSchema.ReferenceValueColumns.Count, row - 1);
        return ranges;
    }

    private static void WriteValidationNotes(IXLWorksheet sheet, TerritoryWorkbookRequest request)
    {
        var row = 1;

        void Title(string text)
        {
            sheet.Cell(row, 1).Value = text;
            sheet.Cell(row, 1).Style.Font.Bold = true;
            sheet.Cell(row, 1).Style.Font.FontSize = 13;
            row += 2;
        }

        void Section(string text)
        {
            sheet.Cell(row, 1).Value = text;
            sheet.Cell(row, 1).Style.Font.Bold = true;
            sheet.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#F1F0F2");
            row++;
        }

        void Line(string text = "")
        {
            sheet.Cell(row, 1).Value = text;
            row++;
        }

        void Warn(string text)
        {
            sheet.Cell(row, 1).Value = text;
            sheet.Cell(row, 1).Style.Font.FontColor = XLColor.FromHtml("#A0132B");
            sheet.Cell(row, 1).Style.Font.Bold = true;
            row++;
        }

        Title(request.IsTemplate
            ? "MOD-0151 Territory Management — Import Template"
            : "MOD-0151 Territory Management — Data Export");

        Section("1. What this file is");
        Line(request.IsTemplate
            ? "An empty import template for one territory model. Fill in the sheets you need and keep every column header unchanged."
            : "An export of one territory model. You may correct it and upload it back as the import input.");
        Line($"Model: {request.ModelCode} — {request.ModelName} (status: {request.ModelStatus})");
        Line($"Template version: {TerritoryWorkbookSchema.TemplateVersion}   Generated (UTC): {request.GeneratedAtUtc}   Reference: {request.CorrelationId}");
        Line();

        Section("2. Import always runs as a DRY-RUN first");
        Line("The upload endpoint validates and shows you creates / updates / ends / skips / errors / conflicts / warnings.");
        Line("A dry-run writes NOTHING — no model, node, rule or assignment change, and no import run history row.");
        Line("Only after you review that preview does the separate Apply action write anything.");
        Line();

        Section("3. Operation column");
        Line("add    — create a new record (also accepted as 'create').");
        Line("update — change an existing record; matched by its system id, or by its code when the id cell is empty.");
        Line("end    — close an account assignment historically (AccountAssignments sheet only). Never deletes it.");
        Line("skip   — ignore this row.");
        Line("An EMPTY Operation cell means SKIP. Exported rows come with an empty Operation on purpose: nothing happens");
        Line("to them until you choose an operation. 'delete' is NOT supported — MOD-0151 records are ended, never destroyed.");
        Line();

        Section("4. Which sheet can do what");
        Line("Model              — update only, and only while the model is in 'draft'.");
        Line("Nodes              — add / update, only while the model is in 'draft'.");
        Line("AssignmentRules    — add / update, only while the model is in 'draft'.");
        Line("AccountAssignments — add / end, only while the model is 'active' (same rules as the on-screen apply).");
        Warn("ResourceAssignments — EXPORT, TEMPLATE and DRY-RUN only. Applying resource assignments from a file is not");
        Line("                      supported in this version (it would bypass the planned/active, replacement and transfer rules).");
        Warn("CoverageSummary and PlanVsCurrent are EXPORT-ONLY read models and cannot be imported at all.");
        Line();

        Section("5. Empty cells, clearing and identifiers");
        Line("An empty cell means 'leave this field unchanged'. To empty a field on purpose write " + TerritoryWorkbookSchema.ClearToken + ".");
        Line("Required fields can never be cleared.");
        Line("ModelId / NodeId / RuleId / AssignmentId are system identifiers written by the export. Changing one breaks");
        Line("the match and the row is rejected. Leave them empty when adding a new record.");
        Warn("Do NOT add a TenantId column. Tenancy comes from your login; a TenantId column in the file is ignored and reported as a warning.");
        Line();

        Section("6. Account assignments follow the on-screen rules exactly");
        Line("Only an active model accepts account assignments; the assignment window must fit inside the node and model windows.");
        Line("An overlapping active assignment is a conflict. To replace it set Override = TRUE and give an OverrideReason —");
        Line("the old record is closed with an end date, never deleted.");
        Line();

        Section("7. Re-uploading the same file is safe");
        Line("Applying the same file twice does not duplicate anything: an identical row is reported as no_change and skipped,");
        Line("a differing row is reported as a controlled conflict.");
        Line();

        Section("8. Reference values");
        Line("RuleType, ConflictPolicy, TerritoryLevel and BusinessUnitScopes must be published MOD-0048 values — see the");
        Line("ReferenceValues sheet. Multi-value cells (BusinessUnitScopes, CountryRefs, …) are separated by a semicolon.");
        Line("If a required set is not published the import fails closed; it is never substituted with a local list.");
        Line();

        Section("9. Reference data status");
        foreach (var set in request.ReferenceSets)
        {
            var published = set.IsPublished && set.Values.Any(v => !v.IsDeprecated);
            var text = $"{set.SetCode} — "
                       + (published
                           ? $"{set.Values.Count(v => !v.IsDeprecated)} selectable value(s)"
                           : TerritoryWorkbookSchema.NotPublishedMarker);

            if (published)
            {
                Line(text);
            }
            else
            {
                Warn(text + " — import will fail until the operator publishes this set in MOD-0048.");
            }
        }

        sheet.Column(1).Width = 130;
        sheet.SheetView.FreezeRows(1);
    }

    private static void WriteDataSheet(
        IXLWorksheet sheet,
        string sheetName,
        IReadOnlyList<string> columns,
        IReadOnlyList<IReadOnlyList<string?>> rows,
        IReadOnlyDictionary<string, string> columnSets,
        IReadOnlyDictionary<string, (int First, int Last)> setRanges,
        IXLWorksheet referenceSheet,
        bool isTemplate,
        bool importable)
    {
        WriteHeader(sheet, columns);

        for (var r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            for (var c = 0; c < columns.Count && c < row.Count; c++)
            {
                sheet.Cell(r + 2, c + 1).Value = row[c] ?? string.Empty;
            }
        }

        // Mark the columns the reader must not take input from, so the intent is visible in the file itself.
        foreach (var column in TerritoryWorkbookSchema.SystemColumns.Concat(TerritoryWorkbookSchema.ReadOnlyHelperColumns))
        {
            var index = TerritoryWorkbookSchema.ColumnIndex(columns, column);
            if (index > 0)
            {
                sheet.Cell(1, index).Style.Fill.BackgroundColor = XLColor.FromHtml("#E7E7FF");
            }
        }

        if (!importable)
        {
            // Export-only read model: say so in the file, not just in the docs.
            sheet.Range(1, 1, 1, columns.Count).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFF2D6");
        }

        var lastValidationRow = Math.Max(rows.Count + 1, 1) + (isTemplate ? TemplateValidationRows : ExportValidationBuffer);
        if (importable)
        {
            ApplyOperationValidation(sheet, columns, lastValidationRow);
            ApplyReferenceValidations(sheet, columns, columnSets, setRanges, referenceSheet, lastValidationRow);
        }

        FinishSheet(sheet, columns.Count, rows.Count + 1);
    }

    // ---- helpers -------------------------------------------------------------------------------------------------

    private static void WriteHeader(IXLWorksheet sheet, IReadOnlyList<string> columns)
    {
        for (var c = 0; c < columns.Count; c++)
        {
            sheet.Cell(1, c + 1).Value = columns[c];
        }

        var header = sheet.Range(1, 1, 1, columns.Count);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#F5F5F9");
        header.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
    }

    private static void FinishSheet(IXLWorksheet sheet, int columnCount, int lastRow)
    {
        // Text format everywhere: GUIDs, ISO dates and leading-zero codes must not be reinterpreted by Excel.
        for (var c = 1; c <= columnCount; c++)
        {
            sheet.Column(c).Style.NumberFormat.Format = TextFormat;
            sheet.Column(c).Width = 22;
        }

        sheet.SheetView.FreezeRows(1);
        if (lastRow >= 1)
        {
            sheet.Range(1, 1, Math.Max(lastRow, 1), columnCount).SetAutoFilter();
        }
    }

    private static void ApplyOperationValidation(IXLWorksheet sheet, IReadOnlyList<string> columns, int lastRow)
    {
        var index = TerritoryWorkbookSchema.ColumnIndex(columns, TerritoryWorkbookSchema.OperationColumn);
        if (index <= 0) return;

        // Excel requires an inline list to be a QUOTED literal ("a,b,c"); without the quotes formula1 is parsed as a
        // reference and the dropdown is silently dropped — the quotes are part of the contract, not cosmetics.
        var validation = sheet.Range(2, index, lastRow, index).CreateDataValidation();
        validation.List($"\"{string.Join(",", TerritoryImportOperations.Selectable)}\"", true);
        validation.IgnoreBlanks = true;
        validation.ErrorStyle = XLErrorStyle.Warning;
    }

    private static void ApplyReferenceValidations(
        IXLWorksheet sheet,
        IReadOnlyList<string> columns,
        IReadOnlyDictionary<string, string> columnSets,
        IReadOnlyDictionary<string, (int First, int Last)> setRanges,
        IXLWorksheet referenceSheet,
        int lastRow)
    {
        foreach (var (column, setCode) in columnSets)
        {
            var index = TerritoryWorkbookSchema.ColumnIndex(columns, column);
            if (index <= 0 || !setRanges.TryGetValue(setCode, out var range))
            {
                // Unpublished set → no dropdown. The ReferenceValues sheet already says NOT_PUBLISHED; a hardcoded
                // fallback list is forbidden.
                continue;
            }

            // Multi-value cells cannot carry a single-value dropdown without rejecting legitimate "a;b" input.
            if (column.EndsWith("Scopes", StringComparison.OrdinalIgnoreCase)
                || column.EndsWith("Refs", StringComparison.OrdinalIgnoreCase)
                || column.EndsWith("Types", StringComparison.OrdinalIgnoreCase)
                || column.EndsWith("Categories", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var source = referenceSheet.Range(range.First, 2, range.Last, 2);
            var validation = sheet.Range(2, index, lastRow, index).CreateDataValidation();
            validation.List(source, true);
            validation.IgnoreBlanks = true;
            // Warning (not Stop): a value that is valid today may be deprecated tomorrow, and the server validates
            // authoritatively on import. The dropdown is a convenience, never the security boundary.
            validation.ErrorStyle = XLErrorStyle.Warning;
        }
    }

    private static string FormatAttributes(IReadOnlyDictionary<string, string>? attributes)
        => attributes is null || attributes.Count == 0
            ? string.Empty
            : string.Join("; ", attributes
                .OrderBy(a => a.Key, StringComparer.OrdinalIgnoreCase)
                .Select(a => $"{a.Key}={a.Value}"));

    /// <summary>Shared file DTO with MOD-0150 so the API layer has one download path.</summary>
    public static ExportFileDto File(byte[] content, string fileName)
        => new(content, fileName, ExportFileDto.XlsxContentType);
}
