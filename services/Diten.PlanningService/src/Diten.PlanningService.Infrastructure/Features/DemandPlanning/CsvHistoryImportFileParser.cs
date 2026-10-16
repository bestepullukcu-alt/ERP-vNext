using System.Text;
using Diten.PlanningService.Application.Features.DemandPlanning.HistoryImports;
using Diten.PlanningService.Domain.Features.DemandPlanning;

namespace Diten.PlanningService.Infrastructure.Features.DemandPlanning;

// A quoted field may contain a physical newline. RowNumber is its first physical line.
public sealed class CsvHistoryImportFileParser : IHistoryImportFileParser
{
    public static readonly string[] RequiredHeader =
    [
        "sourceRecordKey", "skuId", "skuLevel", "warehouseId", "occurredOn",
        "quantity", "uomId", "recordKind", "relatedSourceRecordKey"
    ];

    public ParsedHistoryFile Parse(ReadOnlyMemory<byte> fileBytes)
    {
        var issues = new List<ImportValidationIssue>();
        var rows = new List<ParsedHistoryRow>();
        string content;
        try { content = new UTF8Encoding(false, true).GetString(fileBytes.Span).TrimStart('\uFEFF'); }
        catch (DecoderFallbackException)
        {
            return new ParsedHistoryFile([], [],
            [new ImportValidationIssue { Code = "InvalidUtf8", Severity = ImportIssueSeverity.Blocking,
                Message = "File is not valid UTF-8." }]);
        }

        var records = ParseRecords(content);
        if (records.Count == 0)
            return new ParsedHistoryFile([], [],
            [new ImportValidationIssue { Code = "EmptyFile", Severity = ImportIssueSeverity.Blocking,
                Message = "File has no header or rows." }]);

        var header = records[0];
        if (header.ParseError is not null ||
            !header.RawFields.SequenceEqual(RequiredHeader, StringComparer.Ordinal))
            issues.Add(new ImportValidationIssue { Code = "InvalidHeader",
                Severity = ImportIssueSeverity.Blocking,
                Message = "CSV header does not match the draft import field contract." });

        rows.AddRange(records.Skip(1));
        if (rows.Count > 2_000) throw new HistoryImportFileLimitException();
        if (rows.Count == 0)
            issues.Add(new ImportValidationIssue { Code = "NoDataRows",
                Severity = ImportIssueSeverity.Blocking,
                Message = "File has no data rows." });
        return new ParsedHistoryFile(header.RawFields, rows, issues);
    }

    private static List<ParsedHistoryRow> ParseRecords(string content)
    {
        var records = new List<ParsedHistoryRow>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var physicalLine = 1;
        var recordStart = 1;
        var inQuotes = false;
        var afterQuote = false;
        var atFieldStart = true;
        var malformed = false;

        void EndField()
        {
            fields.Add(field.ToString());
            field.Clear();
            afterQuote = false;
            atFieldStart = true;
        }
        void EndRecord()
        {
            EndField();
            var error = malformed ? "MalformedCsv" :
                fields.Count == 1 && string.IsNullOrWhiteSpace(fields[0]) ? "BlankRow" : null;
            records.Add(new ParsedHistoryRow(recordStart, fields.ToArray(), error));
            fields.Clear();
            malformed = false;
            recordStart = physicalLine + 1;
        }

        for (var i = 0; i < content.Length; i++)
        {
            var c = content[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < content.Length && content[i + 1] == '"')
                    { field.Append('"'); i++; }
                    else { inQuotes = false; afterQuote = true; }
                }
                else if (c is '\r' or '\n')
                {
                    if (c == '\r' && i + 1 < content.Length && content[i + 1] == '\n')
                    { field.Append("\r\n"); i++; }
                    else field.Append(c);
                    physicalLine++;
                }
                else field.Append(c);
                continue;
            }
            if (c == ',') { EndField(); continue; }
            if (c is '\r' or '\n')
            {
                if (c == '\r' && i + 1 < content.Length && content[i + 1] == '\n') i++;
                EndRecord();
                physicalLine++;
                continue;
            }
            if (c == '"')
            {
                if (atFieldStart) { inQuotes = true; atFieldStart = false; }
                else { malformed = true; field.Append(c); }
                continue;
            }
            if (afterQuote) malformed = true;
            field.Append(c);
            atFieldStart = false;
        }
        if (inQuotes) malformed = true;
        if (field.Length != 0 || fields.Count != 0 || inQuotes || afterQuote || malformed)
            EndRecord();
        return records;
    }
}

