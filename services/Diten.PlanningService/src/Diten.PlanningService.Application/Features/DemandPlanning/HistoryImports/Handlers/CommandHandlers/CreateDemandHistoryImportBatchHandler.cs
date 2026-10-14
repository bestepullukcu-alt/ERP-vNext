using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Diten.PlanningService.Domain.Features.DemandPlanning;
using MediatR;

namespace Diten.PlanningService.Application.Features.DemandPlanning.HistoryImports;

public sealed class CreateDemandHistoryImportBatchHandler :
    IRequestHandler<CreateDemandHistoryImportBatchCommand, Response<HistoryImportBatchResult>>
{
    private readonly IHistoryImportScopeAuthority _authority;
    private readonly IHistoryRowReferenceChecker _references;
    private readonly IHistoryImportFileParser _parser;
    private readonly IHistoryImportBatchStore _store;

    public CreateDemandHistoryImportBatchHandler(IHistoryImportScopeAuthority authority,
        IHistoryRowReferenceChecker references, IHistoryImportFileParser parser,
        IHistoryImportBatchStore store)
    { _authority = authority; _references = references; _parser = parser; _store = store; }

    public async Task<Response<HistoryImportBatchResult>> Handle(
        CreateDemandHistoryImportBatchCommand command, CancellationToken cancellationToken)
    {
        if (command.TenantId == Guid.Empty || command.ActorId == Guid.Empty)
            return Response<HistoryImportBatchResult>.Fail("Tenant or actor scope is missing.", 403);
        if (command.SelectedLegalEntityHint == Guid.Empty || command.ScopeFrom == default ||
            command.ScopeThrough < command.ScopeFrom ||
            string.IsNullOrWhiteSpace(command.SourceSystem) ||
            string.IsNullOrWhiteSpace(command.SourceFileName) ||
            command.WarehouseScope is null || command.WarehouseScope.Count == 0 ||
            command.WarehouseScope.Any(string.IsNullOrWhiteSpace) ||
            string.IsNullOrWhiteSpace(command.IdempotencyKey) || command.IdempotencyKey.Length > 128)
            return Response<HistoryImportBatchResult>.Fail("Import scope or source metadata is invalid.");
        if (command.FileBytes is null || command.FileBytes.Length == 0)
            return Response<HistoryImportBatchResult>.Fail("Import file is empty.");
        if (command.FileBytes.Length > 1_048_576)
            return Response<HistoryImportBatchResult>.Fail("Import file exceeds 1 MiB.", 413);
        if (command.CoverageFileBytes is null || command.CoverageFileBytes.Length == 0)
            return Response<HistoryImportBatchResult>.Fail("Coverage declaration is required.");
        if (command.CoverageFileBytes.Length > 262_144)
            return Response<HistoryImportBatchResult>.Fail("Coverage declaration exceeds 256 KiB.", 413);

        Guid? legalEntityId;
        bool? authorized;
        try
        {
            legalEntityId = await _authority.ResolveSelectedAsync(command.TenantId,
                command.ActorId, command.SelectedLegalEntityHint, cancellationToken);
            if (legalEntityId is null || legalEntityId == Guid.Empty)
                return Response<HistoryImportBatchResult>.Fail("LegalEntity scope was not found.", 404);
            if (legalEntityId.Value != command.SelectedLegalEntityHint)
                return Response<HistoryImportBatchResult>.Fail("LegalEntity assignment is inconsistent.", 503);
            authorized = await _authority.IsAuthorizedAsync(command.TenantId, command.ActorId,
                legalEntityId.Value, command.ScopeFrom, command.ScopeThrough,
                command.WarehouseScope, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException ||
                                   !cancellationToken.IsCancellationRequested)
        {
            return Response<HistoryImportBatchResult>.Fail("Import scope authority is unavailable.", 503);
        }
        if (authorized is null)
            return Response<HistoryImportBatchResult>.Fail("Import scope authority is unavailable.", 503);
        if (authorized is false)
            return Response<HistoryImportBatchResult>.Fail("LegalEntity or warehouse scope was not found.", 404);

        ParsedHistoryFile parsed;
        try { parsed = _parser.Parse(command.FileBytes); }
        catch (HistoryImportFileLimitException)
        {
            return Response<HistoryImportBatchResult>.Fail("Import file exceeds 2,000 data rows.", 413);
        }

        var fileHash = Convert.ToHexString(SHA256.HashData(command.FileBytes));
        var coverageHash = Convert.ToHexString(SHA256.HashData(command.CoverageFileBytes));
        List<ImportWeekCoverage> coverage;
        try
        {
            coverage = JsonSerializer.Deserialize<List<ImportWeekCoverage>>(command.CoverageFileBytes,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
                    Converters = { new JsonStringEnumConverter() }
                }) ?? [];
        }
        catch (JsonException)
        {
            return Response<HistoryImportBatchResult>.Fail("Coverage declaration is invalid.");
        }
        if (coverage.Count is < 1 or > 2_000 || coverage.Any(week =>
                week.SkuId == Guid.Empty || week.SkuLevel is not ("Gsku" or "Lsku" or "FinishedGood") ||
                string.IsNullOrWhiteSpace(week.WarehouseId) ||
                !command.WarehouseScope.Contains(week.WarehouseId, StringComparer.Ordinal) ||
                string.IsNullOrWhiteSpace(week.UomId) ||
                week.WeekStart.DayOfWeek != DayOfWeek.Monday ||
                week.WeekStart < command.ScopeFrom || week.WeekStart.AddDays(6) > command.ScopeThrough ||
                (week.StockState.HasValue && !Enum.IsDefined(week.StockState.Value))) ||
            coverage.Select(week => (week.SkuId, week.WarehouseId, week.WeekStart))
                .Distinct().Count() != coverage.Count)
            return Response<HistoryImportBatchResult>.Fail("Coverage declaration scope is invalid.");
        var warehouses = command.WarehouseScope.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList();
        var fingerprintInput = string.Join('|', legalEntityId.Value.ToString("D"),
            command.SourceSystem.Trim(), command.SourceFileName.Trim(),
            command.ScopeFrom.ToString("yyyy-MM-dd"), command.ScopeThrough.ToString("yyyy-MM-dd"),
            string.Join(',', warehouses), fileHash, coverageHash);
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintInput)));
        var batch = new DemandHistoryImportBatch
        {
            TenantId = command.TenantId,
            LegalEntityId = legalEntityId.Value,
            UploadedByActorId = command.ActorId,
            SourceSystem = command.SourceSystem.Trim(),
            SourceFileName = command.SourceFileName.Trim(),
            SourceFileSha256 = fileHash,
            CoverageFileSha256 = coverageHash,
            CoverageWeeks = coverage,
            ScopeFrom = command.ScopeFrom,
            ScopeThrough = command.ScopeThrough,
            WarehouseScope = warehouses,
            FieldScope = parsed.HeaderFields.ToList(),
            IdempotencyKey = command.IdempotencyKey,
            RequestFingerprint = fingerprint,
            FileIssues = parsed.FileIssues.ToList(),
            ValidatedAt = DateTimeOffset.UtcNow
        };
        var headerValid = !batch.FileIssues.Any(x => x.Code == "InvalidHeader");
        foreach (var parsedRow in parsed.Rows)
        {
            var row = await ValidateRowAsync(command, legalEntityId.Value,
                parsedRow, headerValid, cancellationToken);
            batch.Rows.Add(row);
        }
        foreach (var week in coverage)
        {
            HistoryReferenceResult reference;
            try
            {
                reference = await _references.CheckAsync(command.TenantId, legalEntityId.Value,
                    week.SkuId, week.SkuLevel, week.WarehouseId, week.WeekStart,
                    week.UomId, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException ||
                                       !cancellationToken.IsCancellationRequested)
            {
                reference = new HistoryReferenceResult(HistoryReferenceState.Unavailable,
                    null, null, null, null);
            }
            if (reference.State != HistoryReferenceState.Verified)
                batch.FileIssues.Add(new ImportValidationIssue
                {
                    Code = "CoverageReferenceUnverified", Severity = ImportIssueSeverity.Blocking,
                    Message = "Declared SKU, warehouse or unit could not be verified."
                });
        }
        foreach (var row in batch.Rows.Where(row => row.SkuId.HasValue && row.OccurredOn.HasValue))
        {
            var weekStart = row.OccurredOn!.Value.AddDays(-((int)row.OccurredOn.Value.DayOfWeek + 6) % 7);
            var claim = coverage.SingleOrDefault(week => week.SkuId == row.SkuId &&
                week.WarehouseId == row.WarehouseId && week.WeekStart == weekStart);
            if (claim is not null &&
                (claim.SkuLevel != row.SkuLevel || claim.UomId != row.OriginalUomId))
                AddIssue(row, "CoverageRowConflict", ImportIssueSeverity.Blocking,
                    "Declared series scope or unit conflicts with its file row.");
        }
        foreach (var duplicateGroup in batch.Rows
            .Where(x => !string.IsNullOrWhiteSpace(x.SourceRecordKey))
            .GroupBy(x => x.SourceRecordKey, StringComparer.Ordinal)
            .Where(x => x.Count() > 1))
        {
            foreach (var row in duplicateGroup)
                AddIssue(row, "DuplicateKeyInBatch", ImportIssueSeverity.Blocking,
                    "Source record key appears more than once in this file.");
        }
        foreach (var series in batch.Rows
            .Where(x => x.SkuId is not null && !string.IsNullOrWhiteSpace(x.WarehouseId) &&
                        !string.IsNullOrWhiteSpace(x.OriginalUomId))
            .GroupBy(x => (x.SkuId, x.WarehouseId))
            .Where(x => x.Select(row => row.OriginalUomId)
                .Distinct(StringComparer.Ordinal).Skip(1).Any()))
        {
            foreach (var row in series)
                AddIssue(row, "MixedUnitsForSeries", ImportIssueSeverity.Blocking,
                    "One SKU and warehouse cannot use multiple units in the same batch.");
        }
        foreach (var row in batch.Rows.Where(x => x.IsQuarantined))
            batch.QuarantineCases.Add(new DemandHistoryQuarantineCase
            {
                RowNumber = row.RowNumber,
                ReasonCodes = row.Issues.Where(x => x.Severity == ImportIssueSeverity.Blocking)
                    .Select(x => x.Code).Distinct(StringComparer.Ordinal).ToList()
            });
        batch.ApprovalBlocked = batch.FileIssues.Any(x => x.Severity == ImportIssueSeverity.Blocking) ||
                                batch.QuarantineCases.Count != 0;
        var hasWarnings = batch.FileIssues.Any(x => x.Severity == ImportIssueSeverity.Warning) ||
                          batch.Rows.Any(x => x.Issues.Any(y => y.Severity == ImportIssueSeverity.Warning));
        batch.ValidationState = batch.ApprovalBlocked ? ImportBatchValidationState.Blocked :
            hasWarnings ? ImportBatchValidationState.ValidatedWithWarnings :
            ImportBatchValidationState.Validated;
        batch.AuditTrail.Add(new DemandHistoryImportAuditAction
        {
            Action = "FileBatchValidated", ActorId = command.ActorId,
            OccurredAt = batch.ValidatedAt,
            Detail = $"rows={batch.Rows.Count};quarantined={batch.QuarantineCases.Count};state={batch.ValidationState};coverage={coverageHash}"
        });

        HistoryImportInsertResult result;
        try { result = await _store.InsertOrGetAsync(batch, cancellationToken); }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return Response<HistoryImportBatchResult>.Fail("Import batch store is unavailable.", 503);
        }
        if (result.Outcome == HistoryImportInsertOutcome.Conflict || result.Batch is null)
            return Response<HistoryImportBatchResult>.Fail("Idempotency key was used for different import content.", 409);
        return Response<HistoryImportBatchResult>.Success(
            HistoryImportResultMapper.Map(result.Batch),
            result.Outcome == HistoryImportInsertOutcome.Created ? 201 : 200);
    }

    private async Task<DemandHistoryImportRow> ValidateRowAsync(
        CreateDemandHistoryImportBatchCommand command, Guid legalEntityId, ParsedHistoryRow parsed,
        bool headerValid, CancellationToken cancellationToken)
    {
        var row = new DemandHistoryImportRow
        {
            RowNumber = parsed.RowNumber, RawFields = parsed.RawFields.ToList()
        };
        if (parsed.ParseError is not null)
            AddIssue(row, parsed.ParseError, ImportIssueSeverity.Blocking,
                "CSV row cannot be parsed.");
        if (!headerValid)
            AddIssue(row, "InvalidHeader", ImportIssueSeverity.Blocking,
                "Row cannot be mapped because the file header is invalid.");
        if (parsed.RawFields.Count != 9)
            AddIssue(row, "ColumnCount", ImportIssueSeverity.Blocking,
                "CSV row must contain nine fields.");
        if (row.IsQuarantined) return row;

        var cells = parsed.RawFields.Select(x => x.Trim()).ToArray();
        row.SourceRecordKey = cells[0];
        if (row.SourceRecordKey.Length == 0)
            AddIssue(row, "MissingSourceKey", ImportIssueSeverity.Blocking,
                "Source record key is required.");
        if (Guid.TryParse(cells[1], out var skuId) && skuId != Guid.Empty)
            row.SkuId = skuId;
        else
            AddIssue(row, "InvalidSku", ImportIssueSeverity.Blocking,
                "SKU must be a nonempty GUID.");
        row.SkuLevel = cells[2];
        if (row.SkuLevel is not ("Gsku" or "Lsku" or "FinishedGood"))
            AddIssue(row, "InvalidSkuLevel", ImportIssueSeverity.Blocking,
                "SKU level is outside the frozen contract.");
        row.WarehouseId = cells[3];
        if (row.WarehouseId.Length == 0 ||
            !command.WarehouseScope.Contains(row.WarehouseId, StringComparer.Ordinal))
            AddIssue(row, "WarehouseOutsideScope", ImportIssueSeverity.Blocking,
                "Warehouse is missing or outside the declared batch scope.");
        if (DateOnly.TryParseExact(cells[4], "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var occurredOn))
        {
            row.OccurredOn = occurredOn;
            if (occurredOn < command.ScopeFrom || occurredOn > command.ScopeThrough)
                AddIssue(row, "DateOutsideScope", ImportIssueSeverity.Blocking,
                    "Occurrence date is outside the declared batch scope.");
        }
        else
            AddIssue(row, "InvalidDate", ImportIssueSeverity.Blocking,
                "Occurrence date must be yyyy-MM-dd.");
        if (decimal.TryParse(cells[5], NumberStyles.AllowLeadingSign |
                NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var quantity) && quantity > 0)
            row.OriginalQuantity = quantity;
        else
            AddIssue(row, "InvalidQuantity", ImportIssueSeverity.Blocking,
                "Quantity must be a positive invariant decimal.");
        row.OriginalUomId = cells[6];
        if (row.OriginalUomId.Length == 0)
            AddIssue(row, "MissingUom", ImportIssueSeverity.Blocking,
                "Original unit is required.");
        row.RecordKind = cells[7];
        row.RelatedSourceRecordKey = cells[8].Length == 0 ? null : cells[8];
        if (row.RecordKind is not ("Shipment" or "Correction" or "Cancellation" or "Return"))
            AddIssue(row, "InvalidRecordKind", ImportIssueSeverity.Blocking,
                "Record kind is unsupported.");
        if (row.RecordKind != "Shipment" && row.RelatedSourceRecordKey is null)
            AddIssue(row, "MissingRelatedRecord", ImportIssueSeverity.Blocking,
                "Adjustment or return must reference a source record.");
        if (row.RecordKind != "Shipment")
            AddIssue(row, "AdjustmentPendingReview", ImportIssueSeverity.Blocking,
                "Adjustment or return remains quarantined for later policy review.");
        if (row.IsQuarantined) return row;

        HistoryReferenceResult reference;
        try
        {
            reference = await _references.CheckAsync(command.TenantId, legalEntityId,
                row.SkuId!.Value, row.SkuLevel, row.WarehouseId,
                row.OccurredOn!.Value, row.OriginalUomId, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            reference = new HistoryReferenceResult(HistoryReferenceState.Unavailable,
                null, null, null, null);
        }
        if (reference.State != HistoryReferenceState.Verified ||
            (!reference.UnitVerificationPending &&
             (string.IsNullOrWhiteSpace(reference.BaseUomId) ||
              reference.ConversionFactor is not > 0 ||
              string.IsNullOrWhiteSpace(reference.ConversionContractId) ||
              string.IsNullOrWhiteSpace(reference.ConversionContractVersion))))
        {
            AddIssue(row, reference.State == HistoryReferenceState.Invalid ?
                "InvalidReference" : "ReferenceOrConversionUnverified",
                ImportIssueSeverity.Blocking,
                "SKU, Warehouse or dated unit conversion is not verified.");
            return row;
        }
        IReadOnlyList<string> acceptedUnits;
        try
        {
            acceptedUnits = await _store.GetAcceptedUnitsAsync(command.TenantId, legalEntityId,
                row.SkuId!.Value, row.WarehouseId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            AddIssue(row, "AcceptedHistoryUnavailable", ImportIssueSeverity.Blocking,
                "Accepted history unit could not be checked.");
            return row;
        }
        if (acceptedUnits.Any(unit => unit != row.OriginalUomId))
        {
            AddIssue(row, "UnitConflictsWithAcceptedHistory", ImportIssueSeverity.Blocking,
                "Unit differs from the already accepted SKU and warehouse history.");
            return row;
        }
        if (reference.UnitVerificationPending)
        {
            row.UnitVerificationPending = true;
            AddIssue(row, "UnitVerificationPending", ImportIssueSeverity.Warning,
                "Unit is retained without conversion until MOD-0290 verifies it.");
            AddIssue(row, "StockoutEvidenceUnknown", ImportIssueSeverity.Warning,
                "Shipment alone cannot prove that demand was fully served.");
            return row;
        }
        try
        {
            row.BaseUomId = reference.BaseUomId;
            row.ConversionFactor = reference.ConversionFactor;
            row.ConversionContractId = reference.ConversionContractId;
            row.ConversionContractVersion = reference.ConversionContractVersion;
            row.BaseQuantity = checked(row.OriginalQuantity!.Value * reference.ConversionFactor!.Value);
        }
        catch (OverflowException)
        {
            AddIssue(row, "ConversionOverflow", ImportIssueSeverity.Blocking,
                "Converted quantity cannot be represented.");
            return row;
        }
        AddIssue(row, "StockoutEvidenceUnknown", ImportIssueSeverity.Warning,
            "Shipment alone cannot prove that demand was fully served.");
        return row;
    }

    private static void AddIssue(DemandHistoryImportRow row, string code,
        ImportIssueSeverity severity, string message) => row.Issues.Add(new ImportValidationIssue
        { Code = code, Severity = severity, Message = message });
}
