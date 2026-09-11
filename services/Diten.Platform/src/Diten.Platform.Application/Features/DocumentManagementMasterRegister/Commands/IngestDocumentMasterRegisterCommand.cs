using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Models;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementMasterRegister.Commands;

// WP-DM-1a — ingest the counterparty's controlled-document reference CSV into the tenant's Document Master Register.
// Reuses the quoted-aware DocumentReferenceListParser; maps each row via DocumentRegisterIngestMapping (DM-0). This is
// a metadata catalog ingest — NOT the real file/binary (that is DM-4 / MOD-0262).

/// <summary>
/// WP-DM-1a — ingest <see cref="CsvContent"/> into the current tenant's Master Register. Idempotent upsert by
/// (TenantId, PermanentUid): an existing row is updated, a new one is created — re-import never duplicates.
/// </summary>
public sealed record IngestDocumentMasterRegisterCommand(
    string CsvContent,
    string? ActorName,
    string CorrelationId) : IRequest<Response<DocumentRegisterIngestResult>>;
