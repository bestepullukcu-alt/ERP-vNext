namespace Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Services;

/// <summary>
/// Decodes an import payload, resolves a parser for the declared format, parses the workbook, and builds a
/// deterministic validation plan. Shared by the dry-run and commit handlers so both phases validate identically.
/// Returns a plan whose summary carries controlled findings; it never throws for bad input.
/// </summary>
public sealed class QmsBaselineImportService
{
    private readonly IReadOnlyList<IQmsFolderImportParser> _parsers;
    private readonly QmsFolderTreeValidator _validator;
    private readonly DottedOutlineTreeBuilder _dottedBuilder;

    public QmsBaselineImportService(
        IEnumerable<IQmsFolderImportParser> parsers,
        QmsFolderTreeValidator validator,
        DottedOutlineTreeBuilder dottedBuilder)
    {
        _parsers = parsers.ToList();
        _validator = validator;
        _dottedBuilder = dottedBuilder;
    }

    public async Task<QmsBaselineImportPlan> BuildPlanAsync(
        string fileName,
        string format,
        string contentBase64,
        string sourceBaselineKey,
        Guid tenantId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(contentBase64))
        {
            return InvalidPlan("import_payload_required");
        }

        byte[] content;
        try
        {
            content = Convert.FromBase64String(contentBase64);
        }
        catch (FormatException)
        {
            return InvalidPlan("invalid_base64_payload");
        }

        var parser = _parsers.FirstOrDefault(p => p.Supports(format ?? string.Empty, fileName ?? string.Empty));
        if (parser is null)
        {
            return InvalidPlan("unsupported_import_format");
        }

        IReadOnlyList<QmsFolderImportRow> rows;
        try
        {
            rows = await parser.ParseAsync(content, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (QmsWorkbookFormatException ex)
        {
            // Controlled source-format failure (e.g. canonical sheet missing) -> VALIDATION_FAILED.
            return InvalidPlan(ex.Message);
        }
        catch (Exception)
        {
            return InvalidPlan("invalid_import_payload");
        }

        // Canonical "last version" sheet encodes hierarchy as a dotted outline code -> dedicated nested builder.
        // Helper/fixture sheets (slash path or level columns) use the slash-path validator.
        return rows.Any(r => !string.IsNullOrWhiteSpace(r.OutlineCode))
            ? _dottedBuilder.BuildPlan(rows, tenantId, sourceBaselineKey)
            : _validator.BuildPlan(rows, tenantId, sourceBaselineKey);
    }

    private static QmsBaselineImportPlan InvalidPlan(string error)
    {
        var summary = new QmsBaselineImportSummary(
            TotalRows: 0,
            ImportedDefinitionsCount: 0,
            SkippedRows: 0,
            Errors: [error],
            Warnings: [],
            DuplicatePathConflicts: [],
            InvalidHierarchyFindings: [],
            DryRun: true,
            Committed: false);
        return new QmsBaselineImportPlan(summary, []);
    }
}
