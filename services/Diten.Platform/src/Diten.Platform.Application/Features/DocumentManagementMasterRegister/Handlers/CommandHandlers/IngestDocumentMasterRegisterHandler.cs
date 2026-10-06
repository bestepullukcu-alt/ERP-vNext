using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Commands;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Models;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Services;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementMasterRegister.Handlers.CommandHandlers;

/// <summary>
/// WP-DM-1a — ingest the controlled-document reference CSV into the current tenant's Document Master Register.
/// Reuses the quoted-aware <see cref="DocumentReferenceListParser"/> (naive splitting is banned — it corrupts the
/// blocked-reason column) and the shared <see cref="DocumentRegisterIngestMapping"/> (DM-0 status mapping). Idempotent
/// upsert by (TenantId, PermanentUid): <see cref="IDocumentMasterRegisterRepository.GetByPermanentUidAsync"/> → update
/// if present, else create — re-import never duplicates.
///
/// <para>An existing row is written and counted <c>Updated</c> only when
/// <see cref="DocumentRegisterIngestMapping.WouldChange"/> says its mapped fields actually differ; otherwise it is
/// left untouched and counted <c>Unchanged</c> — the identical comparison the preview
/// (<c>DocumentRegisterImportPreviewService</c>) already forecasts with, so a re-import of an unchanged file writes
/// nothing and reports 0 updates, matching what the preview promised.</para>
/// </summary>
public sealed class IngestDocumentMasterRegisterHandler(
    IDocumentMasterRegisterRepository register,
    ITenantContext tenantContext)
    : IRequestHandler<IngestDocumentMasterRegisterCommand, Response<DocumentRegisterIngestResult>>
{
    public async Task<Response<DocumentRegisterIngestResult>> Handle(IngestDocumentMasterRegisterCommand request, CancellationToken ct)
    {
        var tenantId = TenantGuard.RequireTenant(tenantContext);
        var actor = string.IsNullOrWhiteSpace(request.ActorName) ? "document-register-ingest" : request.ActorName!;

        var parsed = DocumentReferenceListParser.Parse(request.CsvContent, tenantId);
        if (parsed.MissingColumns.Count > 0)
        {
            return Response<DocumentRegisterIngestResult>.Fail(
                $"The reference file is missing required columns: {string.Join(", ", parsed.MissingColumns)}.",
                400, "invalid_document_reference_file", request.CorrelationId);
        }

        // Fail-closed, all-or-nothing on an unexpected status: validate the whole batch's DM-0 mapping BEFORE any
        // write, so a naive-split / register-drift value never half-writes the register.
        try
        {
            foreach (var src in parsed.Entries)
            {
                _ = DocumentRegisterIngestMapping.MapLifecycleStatus(src.Status);
            }
        }
        catch (InvalidOperationException ex)
        {
            return Response<DocumentRegisterIngestResult>.Fail(ex.Message, 422, "unexpected_register_status", request.CorrelationId);
        }

        var errors = new List<string>(parsed.Errors);
        int created = 0, updated = 0, unchanged = 0, blocked = 0;

        foreach (var src in parsed.Entries)
        {
            if (string.IsNullOrWhiteSpace(src.Title))
            {
                // DocumentTitle is required on the register entity; an empty title is reported, never invented.
                errors.Add($"'{src.DocumentUid}': title is empty — row skipped.");
                continue;
            }

            var existing = await register.GetByPermanentUidAsync(src.DocumentUid, ct);
            if (existing is null)
            {
                var entry = new DocumentMasterRegisterEntry
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    DocumentTitle = src.Title,
                    CreatedBy = actor
                };
                DocumentRegisterIngestMapping.Apply(entry, src);
                await register.CreateAsync(entry, ct);
                created++;
            }
            else
            {
                /*
                 * WP-DM-DCP005-RETIRE-CSV-01, AC1 — the SAME comparison the preview already promised its counts
                 * with (DocumentRegisterIngestMapping.WouldChange). The mapping was already validated fail-closed
                 * above, so re-resolving the status here cannot throw. A row whose mapped fields are byte-identical
                 * to what Apply would write is neither re-written nor counted as Updated: it used to be both,
                 * which is why a no-op re-import of the same CSV reported "N updated" while the preview, moments
                 * earlier, had said "0 to update" — the commit's own history then disagreed with the preview that
                 * produced it.
                 */
                var mappedStatus = DocumentRegisterIngestMapping.MapLifecycleStatus(src.Status);
                if (DocumentRegisterIngestMapping.WouldChange(existing, src, mappedStatus))
                {
                    DocumentRegisterIngestMapping.Apply(existing, src);
                    existing.UpdatedAt = DateTimeOffset.UtcNow;
                    existing.UpdatedBy = actor;
                    await register.UpdateAsync(existing, ct);
                    updated++;
                }
                else
                {
                    unchanged++;
                }
            }

            if (!src.LinkableInErp)
            {
                blocked++;
            }
        }

        return Response<DocumentRegisterIngestResult>.Success(
            new DocumentRegisterIngestResult(parsed.Entries.Count, created, updated, unchanged, blocked, errors),
            correlationId: request.CorrelationId);
    }
}
