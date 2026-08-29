using ClosedXML.Excel;
using Diten.CrmService.Application.Common.ReferenceValidation;

namespace Diten.CrmService.Application.Features.ImportExport.Xlsx;

/// <summary>
/// MOD-0150 Import/Export Task 1 — writes the Contact workbook (Instructions / Contacts / AccountLinks / ReferenceData
/// / optional Accounts). The same writer produces the empty import template and the existing-data export, so an
/// exported file round-trips into the (Task 2) import reader column-for-column.
///
/// Every data cell is written as TEXT: an exported GUID, ISO-8601 date, "+90" dial code or leading-zero postal code
/// must survive Excel's locale-dependent auto-conversion unchanged. Dropdowns are built from the ReferenceData sheet
/// (live MOD-0048 published values) — a set that is not published simply gets no dropdown, never a hardcoded list.
/// </summary>
public static class ContactWorkbookBuilder
{
    private const string TextFormat = "@";
    private const int TemplateValidationRows = 500;
    private const int ExportValidationBuffer = 100;

    public static byte[] Build(ContactWorkbookRequest request)
    {
        using var workbook = new XLWorkbook();

        // ReferenceData first: the dropdowns on the data sheets point at its ranges.
        var referenceSheet = workbook.Worksheets.Add(ContactWorkbookSchema.ReferenceDataSheet);
        var setRanges = WriteReferenceData(referenceSheet, request.ReferenceSets);

        var instructions = workbook.Worksheets.Add(ContactWorkbookSchema.InstructionsSheet);
        WriteInstructions(instructions, request);

        var contacts = workbook.Worksheets.Add(ContactWorkbookSchema.ContactsSheet);
        WriteDataSheet(
            contacts,
            ContactWorkbookSchema.ContactColumns,
            request.ContactRows,
            ContactWorkbookSchema.ContactColumnSets,
            setRanges,
            referenceSheet,
            request.IsTemplate);

        if (request.Options.IncludeLinks || request.IsTemplate)
        {
            var links = workbook.Worksheets.Add(ContactWorkbookSchema.AccountLinksSheet);
            WriteDataSheet(
                links,
                ContactWorkbookSchema.AccountLinkColumns,
                request.AccountLinkRows,
                ContactWorkbookSchema.AccountLinkColumnSets,
                setRanges,
                referenceSheet,
                request.IsTemplate);
        }

        if (request.AccountRows is { } accountRows)
        {
            var accounts = workbook.Worksheets.Add(ContactWorkbookSchema.AccountsSheet);
            WriteDataSheet(
                accounts,
                ContactWorkbookSchema.AccountColumns,
                accountRows,
                new Dictionary<string, string>(),
                setRanges,
                referenceSheet,
                isTemplate: false);
        }

        // Instructions is the landing sheet — the PII/KVKK and historical-lifecycle notices must be read first.
        workbook.Worksheets.Worksheet(ContactWorkbookSchema.InstructionsSheet).Position = 1;
        instructions.SetTabActive();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    // ---- sheets --------------------------------------------------------------------------------------------------

    private static Dictionary<string, (int First, int Last)> WriteReferenceData(
        IXLWorksheet sheet, IReadOnlyList<ReferenceSetSnapshot> sets)
    {
        WriteHeader(sheet, ContactWorkbookSchema.ReferenceDataColumns);

        var ranges = new Dictionary<string, (int First, int Last)>(StringComparer.OrdinalIgnoreCase);
        var row = 2;

        foreach (var set in sets)
        {
            if (!set.IsPublished || set.Values.Count == 0)
            {
                // Controlled dependency, visible to the operator: the set exists in the contract but has no published
                // values yet. No CRM local seed / fallback value is ever written here.
                sheet.Cell(row, 1).Value = set.SetCode;
                sheet.Cell(row, 2).Value = ContactWorkbookSchema.NotPublishedMarker;
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
            foreach (var value in set.Values.OrderByDescending(v => v.IsActive).ThenBy(v => v.ValueCode, StringComparer.OrdinalIgnoreCase))
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
                    // Deprecated values are documented but must not be selectable for new/updated records.
                    sheet.Row(row).Style.Font.FontColor = XLColor.FromHtml("#8A8D93");
                    sheet.Row(row).Style.Font.Italic = true;
                }
                else
                {
                    lastSelectable = row;
                }

                row++;
            }

            // Active values are written first, so the selectable block is [first .. lastSelectable].
            if (lastSelectable >= first)
            {
                ranges[set.SetCode] = (first, lastSelectable);
            }
        }

        FinishSheet(sheet, ContactWorkbookSchema.ReferenceDataColumns.Count, row - 1);
        return ranges;
    }

    private static void WriteInstructions(IXLWorksheet sheet, ContactWorkbookRequest request)
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
            ? "MOD-0150 Contact & Account Link — Import Template"
            : "MOD-0150 Contact & Account Link — Data Export");

        Section("1. What this file is");
        Line(request.IsTemplate
            ? "An empty import template. Fill in the Contacts sheet (and optionally the AccountLinks sheet) and keep every column header unchanged."
            : "An export of your existing CRM contacts. You may correct it and keep it as the input for the upcoming import.");
        Warn("This template prepares the supported import structure; upload/apply import will be delivered in the next task.");
        Line($"Template version: {ContactWorkbookSchema.TemplateVersion}   Generated (UTC): {request.GeneratedAtUtc}   Reference: {request.CorrelationId}");
        Line();

        Section("2. Operation column");
        Line("add    — create a new record.");
        Line("update — change an existing record; match it by ContactId (Contacts) or LinkId (AccountLinks).");
        Line("end    — close an Account link historically. Requires ValidTo. Applies to the AccountLinks sheet only.");
        Line("skip   — ignore this row.");
        Line("Rows exported from the system come with an EMPTY Operation cell: nothing happens to them until you choose an operation.");
        Line();

        Section("3. Required fields");
        Line("Contacts:    FirstName or LastName, ContactType, ContactStatus.");
        Line("AccountLinks: a contact (ContactId or ContactExternalSystem+ContactExternalId), an account (AccountId or AccountCode), and RoleCode.");
        Line("ContactType / ContactStatus / RoleCode must be published MOD-0048 values — see the ReferenceData sheet.");
        Line();

        Section("4. System-owned columns — do not edit by hand");
        Line("ContactId, LinkId, AccountId, ReportsToContactId are system identifiers written by the export.");
        Line("Changing them breaks the match and the row will be rejected. Leave them empty when adding a new record.");
        Line("AccountName is a read-only helper for readability and is ignored on import.");
        Line("DisplayName may be left empty — the system derives it from FirstName + LastName.");
        Line("Email and Phone are NOT unique identifiers and are never used to match an existing contact.");
        Line();

        Section("5. Related Account links — historical lifecycle");
        Line("Ending a link never deletes it: the record is kept with Status = ended and ValidTo = the end date.");
        Line("When a contact moves from one account to another, write TWO rows: Operation=end on the old link (with ValidTo),");
        Line("and Operation=add for the new account (with ValidFrom). The old row stays in history.");
        Line("An import never silently overwrites or removes an existing active link.");
        Line("Existing sales / visit / order / route context attached to a historical link is never reassigned.");
        Line();

        Section("6. Personal data (PII / KVKK)");
        Warn("This file can contain personal data (name, phone, e-mail, address, notes). Download it only if you are authorised, store it securely and delete it when you are done.");
        Warn("Do NOT write patient, health, diagnosis, treatment or prescription information into Notes or any other field. Clinical/patient data is out of scope for CRM Contacts and requires a dedicated healthcare privacy scope.");
        Line("Notes are limited to 2000 characters. Cross-country links require a business justification (CrossCountryReason).");
        Line();

        Section("7. Before you import (next task)");
        Line("The import will first run a dry-run and show you creates / updates / ends / skips / errors / warnings for review.");
        Line("Nothing is written until you confirm that preview.");
        Line();

        Section("8. This file was produced with");
        Line($"Sheets: {(request.Options.IncludeLinks || request.IsTemplate ? "Instructions, Contacts, AccountLinks, ReferenceData" : "Instructions, Contacts, ReferenceData")}"
             + (request.AccountRows is not null ? ", Accounts" : string.Empty));
        if (!request.IsTemplate)
        {
            Line($"Related account links: {(request.Options.IncludeLinks ? "included" : "not included")}");
            Line($"Historical (ended/inactive) links: {(request.Options.IncludeHistorical ? "included" : "active links only")}");
            Line($"Notes column: {(request.Options.IncludeNotes ? "included" : "left empty (opt-in)")}");
            var filters = request.Options.AppliedFilterFields();
            Line($"Filters applied: {(filters.Count == 0 ? "none" : string.Join(", ", filters))}");
            Line($"Contact rows: {request.ContactRows.Count}   Account link rows: {request.AccountLinkRows.Count}");
        }

        Line();
        Section("9. Reference data status");
        foreach (var set in request.ReferenceSets)
        {
            var required = ContactWorkbookSchema.RequiredSets.Contains(set.SetCode, StringComparer.OrdinalIgnoreCase);
            var published = set.IsPublished && set.Values.Any(v => !v.IsDeprecated);
            var text = $"{set.SetCode} — {(required ? "required" : "optional")} — "
                       + (published ? $"{set.Values.Count(v => !v.IsDeprecated)} selectable value(s)" : ContactWorkbookSchema.NotPublishedMarker);

            if (!published && required)
            {
                Warn(text + " — import will fail until the operator publishes this set in MOD-0048.");
            }
            else if (!published)
            {
                sheet.Cell(row, 1).Value = text;
                sheet.Cell(row, 1).Style.Font.FontColor = XLColor.FromHtml("#8A8D93");
                row++;
            }
            else
            {
                Line(text);
            }
        }

        sheet.Column(1).Width = 130;
        sheet.SheetView.FreezeRows(1);
    }

    private static void WriteDataSheet(
        IXLWorksheet sheet,
        IReadOnlyList<string> columns,
        IReadOnlyList<IReadOnlyList<string?>> rows,
        IReadOnlyDictionary<string, string> columnSets,
        IReadOnlyDictionary<string, (int First, int Last)> setRanges,
        IXLWorksheet referenceSheet,
        bool isTemplate)
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
        foreach (var column in ContactWorkbookSchema.SystemColumns.Concat(ContactWorkbookSchema.ReadOnlyHelperColumns))
        {
            var index = ContactWorkbookSchema.ColumnIndex(columns, column);
            if (index > 0)
            {
                sheet.Cell(1, index).Style.Fill.BackgroundColor = XLColor.FromHtml("#E7E7FF");
            }
        }

        var lastValidationRow = Math.Max(rows.Count + 1, 1) + (isTemplate ? TemplateValidationRows : ExportValidationBuffer);
        ApplyOperationValidation(sheet, columns, lastValidationRow);
        ApplyReferenceValidations(sheet, columns, columnSets, setRanges, referenceSheet, lastValidationRow);

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
        // Text format everywhere: GUIDs, ISO dates, "+90" dial codes and leading-zero postal codes must not be
        // reinterpreted by Excel when the file is opened or saved again.
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
        var index = ContactWorkbookSchema.ColumnIndex(columns, ContactWorkbookSchema.OperationColumn);
        if (index <= 0)
        {
            return;
        }

        // Operation is a fixed protocol keyword list (not reference data) — safe to inline. Excel requires an inline
        // list to be a QUOTED literal ("a,b,c"); without the quotes it parses formula1 as a reference and silently
        // drops the dropdown, so the quotes are part of the contract, not cosmetics.
        var validation = sheet.Range(2, index, lastRow, index).CreateDataValidation();
        validation.List($"\"{string.Join(",", ContactWorkbookSchema.OperationValues)}\"", true);
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
            var index = ContactWorkbookSchema.ColumnIndex(columns, column);
            if (index <= 0 || !setRanges.TryGetValue(setCode, out var range))
            {
                // Unpublished set → no dropdown. The ReferenceData sheet already says NOT_PUBLISHED; a hardcoded
                // fallback list is forbidden.
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
            : string.Join("; ", attributes.OrderBy(a => a.Key, StringComparer.OrdinalIgnoreCase).Select(a => $"{a.Key}={a.Value}"));
}
