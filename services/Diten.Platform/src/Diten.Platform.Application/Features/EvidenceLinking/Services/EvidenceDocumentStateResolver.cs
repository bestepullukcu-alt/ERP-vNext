using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Entities.EvidenceLinking;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.EvidenceLinking.Services;

/// <summary>Computed (never stored) document state of an evidence link. <c>documentState</c> ∈
/// <see cref="EvidenceDocumentStates"/>.</summary>
public sealed record EvidenceDocumentState(
    Guid? CurrentVersionId,
    string? CurrentVersionLabel,
    bool IsSuperseded,
    string DocumentState,
    DateTimeOffset? ReviewDueAt,
    // WP-CL-FIX-1 — the document code shown to people (register DocumentCode ?? CanonicalId ?? DocumentKey).
    string? DocumentCode = null)
{
    public static readonly EvidenceDocumentState Unknown = new(null, null, false, EvidenceDocumentStates.Unknown, null);
}

public static class EvidenceDocumentStates
{
    public const string Effective = "effective";
    public const string Suspended = "suspended";
    public const string Retired = "retired";
    public const string Withdrawn = "withdrawn";
    public const string Unknown = "unknown";
}

/// <summary>
/// WP-CL-BE-5 — computes the current-document state of evidence links from EXISTING DocMgmt reads only (MOD-0029 is
/// read, never written). Controlled: the register row (SOP §6.2 lifecycle via the §20 reconciliation seam) + the
/// document's technical current version. External: the register entry's source status / superseded date / monitoring
/// due date. Anything that cannot be read reliably resolves to <c>unknown</c> + not superseded (never a guess).
/// </summary>
public interface IEvidenceDocumentStateResolver
{
    /// <summary>One read per distinct document; the result is keyed by link id.</summary>
    Task<IReadOnlyDictionary<Guid, EvidenceDocumentState>> ResolveAsync(
        IReadOnlyCollection<EvidenceLink> links, CancellationToken ct);
}

public sealed class EvidenceDocumentStateResolver : IEvidenceDocumentStateResolver
{
    private readonly IControlledDocumentRepository _documents;
    private readonly IControlledDocumentVersionRepository _versions;
    private readonly IDocumentMasterRegisterRepository _register;
    private readonly IExternalDocumentRegisterRepository _externalDocuments;
    private readonly TimeProvider _clock;

    public EvidenceDocumentStateResolver(
        IControlledDocumentRepository documents,
        IControlledDocumentVersionRepository versions,
        IDocumentMasterRegisterRepository register,
        IExternalDocumentRegisterRepository externalDocuments,
        TimeProvider? clock = null)
    {
        _documents = documents;
        _versions = versions;
        _register = register;
        _externalDocuments = externalDocuments;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<IReadOnlyDictionary<Guid, EvidenceDocumentState>> ResolveAsync(
        IReadOnlyCollection<EvidenceLink> links, CancellationToken ct)
    {
        var result = new Dictionary<Guid, EvidenceDocumentState>();
        var controlled = new Dictionary<Guid, (ControlledDocument? Doc, DocumentMasterRegisterEntry? Row)>();
        var external = new Dictionary<Guid, ExternalDocumentRegisterEntry?>();
        var now = _clock.GetUtcNow();

        foreach (var link in links)
        {
            if (link.DocumentKind == EvidenceDocumentKinds.Controlled)
            {
                if (!controlled.TryGetValue(link.DocumentId, out var c))
                {
                    var doc = await _documents.GetByIdAsync(link.DocumentId, ct);
                    var row = doc is null ? null : await _register.GetByControlledDocumentIdAsync(doc.Id, ct);
                    c = (doc, row);
                    controlled[link.DocumentId] = c;
                }

                result[link.Id] = await ControlledAsync(link, c.Doc, c.Row, ct);
            }
            else if (link.DocumentKind == EvidenceDocumentKinds.External)
            {
                if (!external.TryGetValue(link.DocumentId, out var entry))
                {
                    entry = await _externalDocuments.GetByIdAsync(link.DocumentId, ct);
                    external[link.DocumentId] = entry;
                }

                result[link.Id] = External(entry, now);
            }
            else
            {
                result[link.Id] = EvidenceDocumentState.Unknown;
            }
        }

        return result;
    }

    private async Task<EvidenceDocumentState> ControlledAsync(
        EvidenceLink link, ControlledDocument? doc, DocumentMasterRegisterEntry? row, CancellationToken ct)
    {
        if (doc is null || doc.DeletedAt is not null || doc.IsDeleted)
        {
            return EvidenceDocumentState.Unknown;
        }

        var currentId = doc.CurrentVersionId;
        var currentLabel = currentId is null ? null : $"v{doc.CurrentVersionNumber}";

        // No register row ⇒ no reliable lifecycle read: unknown + not superseded (the WP's stop rule — never a guess).
        if (row is null || row.DeletedAt is not null || row.IsDeleted)
        {
            return new EvidenceDocumentState(currentId, currentLabel, false, EvidenceDocumentStates.Unknown, null,
                ControlledDocumentCode(doc, null));
        }

        var state = ControlledLifecycleState(row.LifecycleStatus);

        bool superseded;
        if (row.LifecycleStatus == ControlledDocumentLifecycleStatus.Superseded)
        {
            superseded = true; // the whole document was replaced by another register row
        }
        else if (row.LifecycleStatus == ControlledDocumentLifecycleStatus.Effective)
        {
            // A newer version is the effective one: the pinned version is no longer current, or DocMgmt stamped it.
            var pinnedStamped = link.DocumentVersionId is { } pinned
                && await _versions.GetByIdAsync(pinned, ct) is { VersionStatus: DocumentVersionStatus.Superseded };
            superseded = (currentId is { } current && link.DocumentVersionId is { } p && p != current) || pinnedStamped;
        }
        else
        {
            // UnderRevision: the replacement is not effective yet — the pinned version stands. Others: not decidable.
            superseded = false;
        }

        return new EvidenceDocumentState(currentId, currentLabel, superseded, state, row.NextReviewDueDate,
            ControlledDocumentCode(doc, row));
    }

    /// <summary>The documentState of a controlled document from its register lifecycle (SOP §6.2).</summary>
    public static string ControlledLifecycleState(ControlledDocumentLifecycleStatus lifecycle) => lifecycle switch
    {
        ControlledDocumentLifecycleStatus.Effective => EvidenceDocumentStates.Effective,
        // Operationally effective while revised (ControlledDocumentLifecyclePolicy.IsOperationallyEffective).
        ControlledDocumentLifecycleStatus.UnderRevision => EvidenceDocumentStates.Effective,
        ControlledDocumentLifecycleStatus.Suspended => EvidenceDocumentStates.Suspended,
        ControlledDocumentLifecycleStatus.Superseded => EvidenceDocumentStates.Retired,
        ControlledDocumentLifecycleStatus.Retired => EvidenceDocumentStates.Retired,
        ControlledDocumentLifecycleStatus.ObsoleteCopy => EvidenceDocumentStates.Retired,
        _ => EvidenceDocumentStates.Unknown // draft / in review / approved-pending: no effective version yet
    };

    /// <summary>WP-CL-FIX-1 — the code people see: the Master Register DocumentCode, else the CanonicalId (NOT unique
    /// across documents — it was shared by three live documents), else the DocumentKey.</summary>
    public static string ControlledDocumentCode(ControlledDocument doc, DocumentMasterRegisterEntry? row) =>
        !string.IsNullOrWhiteSpace(row?.DocumentCode) ? row.DocumentCode.Trim()
        : !string.IsNullOrWhiteSpace(doc.CanonicalId) ? doc.CanonicalId.Trim()
        : doc.DocumentKey;

    public static EvidenceDocumentState External(ExternalDocumentRegisterEntry? entry, DateTimeOffset now)
    {
        if (entry is null || entry.DeletedAt is not null || entry.IsDeleted)
        {
            return EvidenceDocumentState.Unknown;
        }

        var superseded = entry.SourceStatus == ExternalSourceStatus.Superseded
            || entry.ExternalDocumentStatus == ExternalDocumentStatus.Superseded
            || (entry.SourceSupersededDate is { } at && at <= now);
        var state = entry.SourceStatus switch
        {
            ExternalSourceStatus.Withdrawn => EvidenceDocumentStates.Withdrawn,
            _ when entry.ExternalDocumentStatus == ExternalDocumentStatus.Archived => EvidenceDocumentStates.Retired,
            ExternalSourceStatus.Superseded => EvidenceDocumentStates.Retired,
            ExternalSourceStatus.CurrentEffective => EvidenceDocumentStates.Effective,
            _ => EvidenceDocumentStates.Unknown
        };
        return new EvidenceDocumentState(null, entry.SourceVersion, superseded, state, entry.NextCheckDueDate,
            entry.ExternalDocumentCode);
    }
}
