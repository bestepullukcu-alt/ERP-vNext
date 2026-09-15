using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Models;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.DocumentManagementMasterRegister.Services;

/// <summary>
/// WP-DM-DCP005-REGISTER-IMPORT-UI-01 — the ONE place a register import is forecast. Dry-run and commit both call
/// this; it never writes. Reuses the quoted-aware <see cref="DocumentReferenceListParser"/> and the shared
/// <see cref="DocumentRegisterIngestMapping"/> (DM-0) — the exact mapping <c>IngestDocumentMasterRegisterHandler</c>
/// applies for real, so a preview cannot promise a result the commit then fails to produce.
///
/// <para><b>Read-only by construction.</b> This class holds no repository write method and calls none — it reads
/// the tenant's current rows once (<see cref="IDocumentMasterRegisterRepository.GetAllForTenantAsync"/>) and
/// computes the forecast in memory. The actual write, when the caller decides to commit, is a SEPARATE step
/// (<c>IngestDocumentMasterRegisterCommand</c>, dispatched by <c>CommitDocumentRegisterImportHandler</c>) — this
/// service is never on that path, so "the preview changed nothing" needs no assertion about what it does not
/// contain, only about what it does not call.</para>
/// </summary>
public sealed class DocumentRegisterImportPreviewService
{
    private readonly IDocumentMasterRegisterRepository _register;
    private readonly IDocumentRegisterImportBatchRepository _batches;

    public DocumentRegisterImportPreviewService(
        IDocumentMasterRegisterRepository register, IDocumentRegisterImportBatchRepository batches)
    {
        _register = register;
        _batches = batches;
    }

    public async Task<DocumentRegisterImportPreview> BuildAsync(
        string fileName, string csvContent, Guid tenantId, CancellationToken ct)
    {
        var parsed = DocumentReferenceListParser.Parse(csvContent, tenantId);
        var hash = parsed.ContentHash;

        var alreadyImported = await _batches.FindByContentHashAsync(hash, ct);

        if (parsed.MissingColumns.Count > 0)
        {
            return new DocumentRegisterImportPreview(
                fileName, hash, TotalRows: 0, Created: 0, Updated: 0, Unchanged: 0, Blocked: 0,
                parsed.MissingColumns, parsed.Errors, LifecycleDistribution: new Dictionary<string, int>(),
                CitableByQualityDecisionYes: 0, CitableByQualityDecisionNo: 0,
                alreadyImported is not null, alreadyImported?.AppliedAt, alreadyImported?.Actor);
        }

        var errors = new List<string>(parsed.Errors);

        // Fail-closed on the DM-0 mapping, exactly like the ingest handler: a naive-split / register-drift status
        // value is reported for EVERY offending row (not just the first), never invented into a lifecycle.
        var mappingByUid = new Dictionary<string, ControlledDocumentLifecycleMapAttempt>(StringComparer.OrdinalIgnoreCase);
        foreach (var src in parsed.Entries)
        {
            try
            {
                mappingByUid[src.DocumentUid] = ControlledDocumentLifecycleMapAttempt.Ok(
                    DocumentRegisterIngestMapping.MapLifecycleStatus(src.Status));
            }
            catch (InvalidOperationException ex)
            {
                mappingByUid[src.DocumentUid] = ControlledDocumentLifecycleMapAttempt.Failed();
                errors.Add($"'{src.DocumentUid}': {ex.Message}");
            }
        }

        var existingRows = await _register.GetAllForTenantAsync(ct);
        var existingByUid = existingRows
            .Where(e => !string.IsNullOrWhiteSpace(e.PermanentUid))
            .ToDictionary(e => e.PermanentUid!.Trim(), StringComparer.OrdinalIgnoreCase);

        int created = 0, updated = 0, unchanged = 0, blocked = 0;
        var lifecycleDistribution = new Dictionary<string, int>(StringComparer.Ordinal);
        int citableYes = 0, citableNo = 0;

        foreach (var src in parsed.Entries)
        {
            if (!mappingByUid.TryGetValue(src.DocumentUid, out var attempt) || !attempt.Success)
            {
                continue; // already reported above; not counted in Created/Updated/Unchanged/Blocked
            }

            if (string.IsNullOrWhiteSpace(src.Title))
            {
                errors.Add($"'{src.DocumentUid}': title is empty — row would be skipped.");
                continue;
            }

            lifecycleDistribution[attempt.Status!.Value.ToString()] =
                lifecycleDistribution.GetValueOrDefault(attempt.Status!.Value.ToString()) + 1;

            if (src.LinkableInErp) { citableYes++; } else { citableNo++; }
            if (!src.LinkableInErp) { blocked++; }

            if (!existingByUid.TryGetValue(src.DocumentUid, out var existing))
            {
                created++;
                continue;
            }

            if (DocumentRegisterIngestMapping.WouldChange(existing, src, attempt.Status!.Value)) { updated++; } else { unchanged++; }
        }

        return new DocumentRegisterImportPreview(
            fileName, hash, parsed.Entries.Count, created, updated, unchanged, blocked,
            parsed.MissingColumns, errors, lifecycleDistribution, citableYes, citableNo,
            alreadyImported is not null, alreadyImported?.AppliedAt, alreadyImported?.Actor);
    }

    private readonly struct ControlledDocumentLifecycleMapAttempt
    {
        public bool Success { get; private init; }
        public ControlledDocumentLifecycleStatus? Status { get; private init; }

        public static ControlledDocumentLifecycleMapAttempt Ok(ControlledDocumentLifecycleStatus status) =>
            new() { Success = true, Status = status };

        public static ControlledDocumentLifecycleMapAttempt Failed() => new() { Success = false };
    }
}
