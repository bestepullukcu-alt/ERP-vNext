using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Services;

public sealed class BusinessReferenceDataImportService : IBusinessReferenceDataImportService
{
    private readonly IBusinessReferenceDataStewardshipRepository _repository;
    private readonly IReadOnlyList<IBusinessReferenceDataImportParser> _parsers;

    public BusinessReferenceDataImportService(
        IBusinessReferenceDataStewardshipRepository repository,
        IEnumerable<IBusinessReferenceDataImportParser> parsers)
    {
        _repository = repository;
        _parsers = parsers.ToList();
    }

    public async Task<BusinessReferenceDataImportPreviewModel> PreviewAsync(
        Guid targetDraftVersionId,
        string fileName,
        string format,
        string contentBase64,
        string actorId,
        string correlationId,
        CancellationToken ct = default)
    {
        var version = await _repository.GetVersionByIdAsync(targetDraftVersionId, ct)
            ?? throw new KeyNotFoundException("reference_data_version_not_found");

        if (version.Status != BusinessReferenceDataVersionStatus.Draft)
        {
            throw new InvalidOperationException("draft_required");
        }

        var parser = ResolveParser(format, fileName);
        var content = DecodePayload(contentBase64);
        IReadOnlyList<BusinessReferenceDataImportParsedRow> parsedRows;
        try
        {
            parsedRows = await parser.ParseAsync(content, ct);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new InvalidOperationException("invalid_import_payload");
        }

        var existingByCode = version.Values
            .Where(x => !string.IsNullOrWhiteSpace(x.ValueCode))
            .ToDictionary(x => x.ValueCode, x => x, StringComparer.OrdinalIgnoreCase);

        var duplicateMap = parsedRows
            .Where(x => !string.IsNullOrWhiteSpace(x.ValueCode))
            .GroupBy(x => x.ValueCode!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Count(), StringComparer.OrdinalIgnoreCase);

        var previewRows = new List<BusinessReferenceDataImportPreviewRow>(parsedRows.Count);
        foreach (var row in parsedRows)
        {
            var normalizedCode = NormalizeOptional(row.ValueCode);
            var normalizedName = NormalizeOptional(row.DisplayName);
            var issues = ValidateRow(row, duplicateMap);

            var op = InferOperation(row, normalizedCode, normalizedName, existingByCode);

            previewRows.Add(new BusinessReferenceDataImportPreviewRow
            {
                RowNumber = row.RowNumber,
                ValueCode = normalizedCode,
                DisplayName = normalizedName,
                Description = NormalizeOptional(row.Description),
                ParentValueCode = NormalizeOptional(row.ParentValueCode),
                ReplacementValueCode = NormalizeOptional(row.ReplacementValueCode),
                IsDeprecated = row.IsDeprecated,
                SortOrder = row.SortOrder,
                Attributes = row.Attributes,
                Operation = op,
                Issues = issues
            });
        }

        var invalid = previewRows.Count(x => !x.IsValid);
        var blocking = previewRows.Sum(x => x.BlockingIssueCount);
        var set = await _repository.GetSetByIdAsync(version.BusinessReferenceDataSetId, ct)
            ?? throw new KeyNotFoundException("reference_data_set_not_found");

        var preview = new BusinessReferenceDataImportPreview
        {
            TenantId = version.TenantId,
            PreviewId = Guid.NewGuid(),
            TargetDraftVersionId = version.BusinessReferenceDataVersionId,
            SetCode = set.SetCode,
            Format = format.Trim().ToLowerInvariant(),
            FileName = fileName.Trim(),
            ParserKey = parser.ParserKey,
            RowCount = previewRows.Count,
            ValidRowCount = previewRows.Count - invalid,
            InvalidRowCount = invalid,
            BlockingErrorCount = blocking,
            Rows = previewRows,
            LastCorrelationId = correlationId,
            CreatedBy = actorId,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(24)
        };

        await _repository.CreateImportPreviewAsync(preview, ct);
        return ToPreviewModel(preview);
    }

    public async Task<BusinessReferenceDataImportCommitResultModel> CommitAsync(
        Guid previewId,
        string idempotencyKey,
        string actorId,
        string correlationId,
        CancellationToken ct = default)
    {
        var preview = await _repository.GetImportPreviewByIdAsync(previewId, ct)
            ?? throw new KeyNotFoundException("import_preview_not_found");

        if (preview.ExpiresAt.HasValue && preview.ExpiresAt.Value < DateTimeOffset.UtcNow)
        {
            throw new InvalidOperationException("import_preview_expired");
        }

        if (preview.IsCommitted)
        {
            if (string.Equals(preview.CommitIdempotencyKey, idempotencyKey, StringComparison.Ordinal))
            {
                return ToCommitResult(preview, idempotencyKey, true);
            }

            throw new InvalidOperationException("already_committed_different_idempotency");
        }

        if (preview.HasBlockingErrors)
        {
            throw new InvalidOperationException("import_blocking_errors");
        }

        var version = await _repository.GetVersionByIdAsync(preview.TargetDraftVersionId, ct)
            ?? throw new KeyNotFoundException("reference_data_version_not_found");

        if (version.Status != BusinessReferenceDataVersionStatus.Draft)
        {
            throw new InvalidOperationException("draft_required");
        }

        var oldToken = version.ConcurrencyToken;
        var valuesByCode = version.Values
            .Where(x => !string.IsNullOrWhiteSpace(x.ValueCode))
            .ToDictionary(x => x.ValueCode, x => x, StringComparer.OrdinalIgnoreCase);

        var inserted = 0;
        var updated = 0;
        var deprecated = 0;
        var noOp = 0;

        foreach (var row in preview.Rows.Where(x => x.IsValid).OrderBy(x => x.RowNumber))
        {
            var valueCode = row.ValueCode!;
            var op = row.Operation;

            if (op == BusinessReferenceDataImportOperation.NoOp)
            {
                noOp++;
                continue;
            }

            if (op == BusinessReferenceDataImportOperation.Insert)
            {
                var newValue = new BusinessReferenceDataValue
                {
                    ValueCode = valueCode,
                    DisplayName = row.DisplayName ?? valueCode,
                    Description = row.Description,
                    ParentValueCode = row.ParentValueCode,
                    ReplacementValueCode = row.ReplacementValueCode,
                    IsDeprecated = row.IsDeprecated,
                    SortOrder = row.SortOrder,
                    Attributes = row.Attributes
                };

                version.Values.Add(newValue);
                valuesByCode[valueCode] = newValue;
                inserted++;
                continue;
            }

            if (!valuesByCode.TryGetValue(valueCode, out var existing))
            {
                continue;
            }

            if (op == BusinessReferenceDataImportOperation.Deprecate)
            {
                existing.IsDeprecated = true;
                existing.ReplacementValueCode = row.ReplacementValueCode;
                deprecated++;
                continue;
            }

            existing.DisplayName = row.DisplayName ?? existing.DisplayName;
            existing.Description = row.Description;
            existing.ParentValueCode = row.ParentValueCode;
            existing.ReplacementValueCode = row.ReplacementValueCode;
            existing.IsDeprecated = row.IsDeprecated;
            existing.SortOrder = row.SortOrder;
            existing.Attributes = row.Attributes;
            updated++;
        }

        version.LastCorrelationId = correlationId;
        version.UpdatedBy = actorId;
        var saved = await _repository.UpdateVersionAsync(version, oldToken, ct);
        if (!saved)
        {
            throw new InvalidOperationException("concurrency_conflict");
        }

        preview.CommittedAt = DateTimeOffset.UtcNow;
        preview.CommitIdempotencyKey = idempotencyKey;
        preview.CommitInsertedCount = inserted;
        preview.CommitUpdatedCount = updated;
        preview.CommitDeprecatedCount = deprecated;
        preview.CommitNoOpCount = noOp;
        preview.UpdatedBy = actorId;
        preview.LastCorrelationId = correlationId;

        var previewSaved = await _repository.UpdateImportPreviewAsync(preview, ct);
        if (!previewSaved)
        {
            throw new InvalidOperationException("concurrency_conflict");
        }

        return ToCommitResult(preview, idempotencyKey, false);
    }

    private IBusinessReferenceDataImportParser ResolveParser(string format, string fileName)
    {
        var parser = _parsers.FirstOrDefault(x => x.Supports(format, fileName));
        return parser ?? throw new InvalidOperationException("unsupported_import_format");
    }

    private static byte[] DecodePayload(string contentBase64)
    {
        try
        {
            return Convert.FromBase64String(contentBase64.Trim());
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("invalid_base64_payload");
        }
    }

    private static List<BusinessReferenceDataImportPreviewIssue> ValidateRow(
        BusinessReferenceDataImportParsedRow row,
        IReadOnlyDictionary<string, int> duplicateMap)
    {
        var issues = new List<BusinessReferenceDataImportPreviewIssue>();
        var valueCode = NormalizeOptional(row.ValueCode);
        var displayName = NormalizeOptional(row.DisplayName);

        if (string.IsNullOrWhiteSpace(valueCode))
        {
            issues.Add(new BusinessReferenceDataImportPreviewIssue
            {
                RuleCode = "BusinessReferenceData-IMP-001",
                Message = "value_code is required.",
                IsBlocking = true
            });
        }

        if (!string.IsNullOrWhiteSpace(valueCode) && duplicateMap.TryGetValue(valueCode, out var count) && count > 1)
        {
            issues.Add(new BusinessReferenceDataImportPreviewIssue
            {
                RuleCode = "BusinessReferenceData-IMP-002",
                Message = "value_code must be unique within the import file.",
                IsBlocking = true
            });
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            issues.Add(new BusinessReferenceDataImportPreviewIssue
            {
                RuleCode = "BusinessReferenceData-IMP-003",
                Message = "display_name is required.",
                IsBlocking = true
            });
        }

        if (row.SortOrder < 0)
        {
            issues.Add(new BusinessReferenceDataImportPreviewIssue
            {
                RuleCode = "BusinessReferenceData-IMP-004",
                Message = "sort_order cannot be negative.",
                IsBlocking = true
            });
        }

        if (!string.IsNullOrWhiteSpace(valueCode)
            && string.Equals(valueCode, NormalizeOptional(row.ParentValueCode), StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new BusinessReferenceDataImportPreviewIssue
            {
                RuleCode = "BusinessReferenceData-IMP-005",
                Message = "parent_value_code cannot equal value_code.",
                IsBlocking = true
            });
        }

        if (!string.IsNullOrWhiteSpace(valueCode)
            && string.Equals(valueCode, NormalizeOptional(row.ReplacementValueCode), StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new BusinessReferenceDataImportPreviewIssue
            {
                RuleCode = "BusinessReferenceData-IMP-006",
                Message = "replacement_value_code cannot equal value_code.",
                IsBlocking = true
            });
        }

        return issues;
    }

    private static BusinessReferenceDataImportOperation InferOperation(
        BusinessReferenceDataImportParsedRow row,
        string? normalizedCode,
        string? normalizedName,
        IReadOnlyDictionary<string, BusinessReferenceDataValue> existingByCode)
    {
        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            return BusinessReferenceDataImportOperation.NoOp;
        }

        if (!existingByCode.TryGetValue(normalizedCode, out var existing))
        {
            return BusinessReferenceDataImportOperation.Insert;
        }

        var normalizedDescription = NormalizeOptional(row.Description);
        var normalizedParent = NormalizeOptional(row.ParentValueCode);
        var normalizedReplacement = NormalizeOptional(row.ReplacementValueCode);
        var normalizedAttributes = row.Attributes ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var existingAttributes = existing.Attributes ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (row.IsDeprecated && !existing.IsDeprecated)
        {
            return BusinessReferenceDataImportOperation.Deprecate;
        }

        var isSame = string.Equals(existing.DisplayName, normalizedName ?? existing.DisplayName, StringComparison.Ordinal)
                     && string.Equals(existing.Description, normalizedDescription, StringComparison.Ordinal)
                     && string.Equals(existing.ParentValueCode, normalizedParent, StringComparison.Ordinal)
                     && string.Equals(existing.ReplacementValueCode, normalizedReplacement, StringComparison.Ordinal)
                     && existing.IsDeprecated == row.IsDeprecated
                     && existing.SortOrder == row.SortOrder
                     && DictionariesEqual(existingAttributes, normalizedAttributes);

        return isSame ? BusinessReferenceDataImportOperation.NoOp : BusinessReferenceDataImportOperation.Update;
    }

    private static bool DictionariesEqual(
        IReadOnlyDictionary<string, string> left,
        IReadOnlyDictionary<string, string> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        foreach (var pair in left)
        {
            if (!right.TryGetValue(pair.Key, out var value) || !string.Equals(pair.Value, value, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static BusinessReferenceDataImportPreviewModel ToPreviewModel(BusinessReferenceDataImportPreview preview)
    {
        var rowModels = preview.Rows
            .OrderBy(x => x.RowNumber)
            .Select(x => new BusinessReferenceDataImportPreviewRowModel(
                x.RowNumber,
                x.ValueCode,
                x.DisplayName,
                x.Operation.ToString(),
                x.IsValid,
                x.BlockingIssueCount,
                x.Issues
                    .Select(i => new BusinessReferenceDataImportPreviewIssueModel(i.RuleCode, i.Message, i.IsBlocking))
                    .ToList()))
            .ToList();

        var errorRows = preview.Rows
            .SelectMany(row => row.Issues.Select(issue => new BusinessReferenceDataImportErrorReportRowModel(
                row.RowNumber,
                row.ValueCode,
                row.Operation.ToString(),
                issue.RuleCode,
                issue.IsBlocking,
                issue.Message)))
            .ToList();

        return new BusinessReferenceDataImportPreviewModel(
            preview.PreviewId,
            preview.TargetDraftVersionId,
            preview.SetCode,
            preview.Format,
            preview.ParserKey,
            preview.RowCount,
            preview.ValidRowCount,
            preview.InvalidRowCount,
            preview.BlockingErrorCount,
            rowModels,
            new BusinessReferenceDataImportErrorReportModel(
                "application/json",
                $"import-errors-{preview.PreviewId:N}.json",
                errorRows));
    }

    private static BusinessReferenceDataImportCommitResultModel ToCommitResult(
        BusinessReferenceDataImportPreview preview,
        string idempotencyKey,
        bool replay)
    {
        return new BusinessReferenceDataImportCommitResultModel(
            preview.PreviewId,
            preview.TargetDraftVersionId,
            idempotencyKey,
            preview.CommitInsertedCount,
            preview.CommitUpdatedCount,
            preview.CommitDeprecatedCount,
            preview.CommitNoOpCount,
            preview.CommittedAt ?? DateTimeOffset.UtcNow,
            replay);
    }

    private static string? NormalizeOptional(string? raw)
    {
        return string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
    }
}
