using Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Services;
using Diten.Platform.Application.Features.DocumentManagementExternalDocuments;
using Diten.Platform.Domain.Entities.DocumentManagement;

namespace Diten.Platform.Application.Features.EvidenceLinking.Services;

/// <summary>
/// MOD-0031 slice 1 — "may the caller READ this document?". Evidence linking never decides document access itself: it
/// asks MOD-0029's existing authorities and never widens them.
/// </summary>
public interface IEvidenceDocumentAccessGate
{
    Task<bool> CanReadControlledAsync(ControlledDocument document, CancellationToken ct);

    Task<bool> CanReadExternalAsync(ExternalDocumentRegisterEntry entry, CancellationToken ct);
}

/// <summary>
/// Controlled documents: <see cref="DocumentAccessEvaluator.CanReadControlledDocumentAsync"/> — the same Layer 2 gate
/// (lifecycle + item matrix / owner company / folder view / share) the document explorer uses; unchanged and read-only.
/// External register entries carry no resource ACL in MOD-0029-FU14: the register is tenant-scoped and guarded by the
/// <see cref="ExternalDocumentPermissions.View"/> key, so the gate requires that key (or administrative document
/// access). Anything the gate cannot prove is a denial (fail-closed).
/// </summary>
public sealed class EvidenceDocumentAccessGate : IEvidenceDocumentAccessGate
{
    private readonly DocumentAccessEvaluator _access;

    public EvidenceDocumentAccessGate(DocumentAccessEvaluator access) => _access = access;

    public Task<bool> CanReadControlledAsync(ControlledDocument document, CancellationToken ct)
        => _access.CanReadControlledDocumentAsync(document, ct);

    public Task<bool> CanReadExternalAsync(ExternalDocumentRegisterEntry entry, CancellationToken ct)
    {
        var principal = _access.Principal;
        var allowed = principal.HasAdministrativeDocumentAccess
            || (principal.Permissions ?? []).Any(p =>
                string.Equals(p, ExternalDocumentPermissions.View, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(allowed);
    }
}
