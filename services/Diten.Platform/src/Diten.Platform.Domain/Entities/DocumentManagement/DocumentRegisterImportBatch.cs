using Diten.Platform.Common.Persistence;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// WP-DM-DCP005-REGISTER-IMPORT-UI-01 — one COMMITTED upload of the Document Master Register CSV. The register's
/// own upload history: "who loaded this file, when, and what happened" — the same role the CSV list's
/// <c>DocumentReferenceListVersion</c> plays for the Tasks side, but content-hash keyed rather than
/// version-labelled, because a register import is idempotent-by-content, not manually versioned.
///
/// <para><b>Written ONCE, by <see cref="Diten.Platform.Application.Features.DocumentManagementMasterRegister.Handlers.CommandHandlers.CommitDocumentRegisterImportHandler"/>
/// only</b> — a dry-run never reaches this collection. <see cref="ContentHash"/> is the idempotency key
/// (<c>(TenantId, ContentHash)</c> unique): a second commit of the exact same bytes is answered
/// <c>IMPORT_ALREADY_APPLIED</c> rather than writing a second row.</para>
/// </summary>
public sealed class DocumentRegisterImportBatch : TenantScopedEntity
{
    /// <summary>SHA-256 of the raw uploaded bytes — the same algorithm <c>DocumentReferenceListParser.HashContent</c>
    /// uses, so a byte-identical file always hashes identically regardless of which import path read it.</summary>
    public required string ContentHash { get; set; }

    public required string FileName { get; set; }

    /// <summary>Who committed this import — the actor name, same convention as <c>DocumentRegisterIngestMapping</c>'s
    /// callers. Distinct from <c>CreatedBy</c> (a system/audit convention) so the screen has one obvious field.</summary>
    public required string Actor { get; set; }

    public required DateTimeOffset AppliedAt { get; set; }

    public required int TotalRows { get; set; }
    public required int Created { get; set; }
    public required int Updated { get; set; }

    /// <summary>WP-DM-DCP005-RETIRE-CSV-01, AC1 — an existing row the commit left untouched because
    /// <c>DocumentRegisterIngestMapping.WouldChange</c> said nothing had changed. Additive: a batch committed
    /// before this field existed reads back as 0 here, which is the honest answer for a row this WP never
    /// measured — not a claim that nothing was unchanged.</summary>
    public required int Unchanged { get; set; }

    public required int Blocked { get; set; }

    public string? CorrelationId { get; set; }
}
