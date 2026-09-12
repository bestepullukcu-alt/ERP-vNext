using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Application.Features.DocumentManagementMasterRegister.Services;

/// <summary>
/// WP-DM-1 — the ONE mapping from a parsed CSV row (<see cref="DocumentReferenceEntry"/>, produced by the reused
/// quoted-aware <c>DocumentReferenceListParser</c>) to a Document Master Register row. Shared by BOTH the ingest
/// command handler (runtime, via the tenant-scoped repo) and the startup auto-seed (Infrastructure, direct Mongo),
/// so the two paths produce byte-identical rows.
///
/// <para><b>DM-0 status → LifecycleStatus (SABİT, CT-approved 2026-09-10).</b> A CLOSED set — an unexpected status
/// throws (naive-split / register drift is surfaced, never silently mis-mapped). The register is a metadata
/// projection: <c>CollectionInstanceId=Guid.Empty</c> (folderless, §8), <c>IsSystemAllocated=false</c> (manual
/// provenance). Citability is derived downstream from LifecycleStatus (IsOperationallyEffective) — with 0 Effective
/// docs today, everything is Blocked/Unresolved, which is correct.</para>
/// </summary>
public static class DocumentRegisterIngestMapping
{
    /// <summary>
    /// The 1 "Executed (source on file)" row is CSV-linkable but DM-0 flags it "blocked/özel (QA)" — a completed
    /// record, not a controlled document, and the lifecycle enum has no "record" state. Conservative, non-citable,
    /// reversible mapping: LifecycleStatus=Draft (non-effective) + this note; NOT counted in the 36 CSV-blocked rows.
    /// Flagged for QA — re-ingest overwrites it once DM-0 firms the cell.
    /// </summary>
    public const string ExecutedRecordStatusReason =
        "Executed record (source on file) — not a controlled document; lifecycle mapping pending QA (DM-0 'blocked/özel').";

    /// <summary>
    /// DM-0 closed mapping. Throws <see cref="InvalidOperationException"/> for any status outside the six approved
    /// values — the caller surfaces it (fail-closed) rather than inventing a lifecycle.
    /// </summary>
    public static ControlledDocumentLifecycleStatus MapLifecycleStatus(string? status)
    {
        var t = status?.Trim() ?? string.Empty;

        // Void → terminal, non-citable.
        if (string.Equals(t, "Void", StringComparison.Ordinal))
        {
            return ControlledDocumentLifecycleStatus.Retired;
        }

        // Planned / NOT REGISTERED → not-yet / mandatory-but-unregistered; non-effective (Draft), CSV-blocked.
        if (string.Equals(t, "Planned", StringComparison.Ordinal)
            || string.Equals(t, "NOT REGISTERED", StringComparison.Ordinal))
        {
            return ControlledDocumentLifecycleStatus.Draft;
        }

        // Executed (source on file) → record, not a controlled document; conservative non-effective (Draft).
        if (t.StartsWith("Executed", StringComparison.OrdinalIgnoreCase))
        {
            return ControlledDocumentLifecycleStatus.Draft;
        }

        // Draft family. "Draft — final draft for approval" → InReview (dash-variant tolerant); plain "Draft" → Draft.
        if (t.StartsWith("Draft", StringComparison.OrdinalIgnoreCase))
        {
            if (t.Contains("final draft for approval", StringComparison.OrdinalIgnoreCase))
            {
                return ControlledDocumentLifecycleStatus.InReview;
            }

            if (string.Equals(t, "Draft", StringComparison.Ordinal))
            {
                return ControlledDocumentLifecycleStatus.Draft;
            }
        }

        throw new InvalidOperationException(
            $"Unexpected register status '{status}' — not in the DM-0 mapping (naive-split / register drift suspected).");
    }

    /// <summary>True for the "Executed (source on file)" record row (özel handling; not a CSV-blocked row).</summary>
    public static bool IsExecutedRecord(string? status) =>
        (status?.Trim() ?? string.Empty).StartsWith("Executed", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Applies the mapped fields onto <paramref name="target"/> (a new entity on create, or the existing row on
    /// update). Identity + lifecycle + blocked-reason only — criticality/type/class/folder are NOT mapped in DM-1
    /// (out of DM-0 scope; left at entity defaults). <paramref name="target"/>.<c>DocumentTitle</c> must already be
    /// set (it is <c>required</c>); it is re-set here to the source title for the update path.
    /// </summary>
    public static void Apply(DocumentMasterRegisterEntry target, DocumentReferenceEntry src)
    {
        target.PermanentUid = string.IsNullOrWhiteSpace(src.DocumentUid) ? null : src.DocumentUid.Trim();
        target.DocumentCode = string.IsNullOrWhiteSpace(src.DocumentCode) ? null : src.DocumentCode.Trim();
        target.DocumentTitle = src.Title;
        target.CurrentVersionLabel = src.DocumentVersion;
        target.LifecycleStatus = MapLifecycleStatus(src.Status);
        target.IsSystemAllocated = false;                 // manual provenance (not the FU07 engine)
        target.CollectionInstanceId = Guid.Empty;          // folderless metadata projection (§8)
        // DCP-005 Step 0, Part B — Quality's CSV call, copied 1:1 (both the seed and the runtime ingest command
        // go through this one method, so the two paths cannot disagree about it). See the field's own doc comment
        // on DocumentMasterRegisterEntry for how a citation consumes it.
        target.CitableByQualityDecision = src.LinkableInErp;

        if (!src.LinkableInErp)
        {
            // The 36 CSV-blocked rows (Void / Planned / NOT REGISTERED): shown, not dropped, with the register's own
            // reason. Citability is still derived from LifecycleStatus; this marks them visibly non-conforming.
            target.LinkScopeCompatibilityStatus = DocumentLinkScopeCompatibilityStatus.Invalid;
            target.StatusReason = src.LinkBlockedReason;   // parser guarantees non-empty for a blocked row
        }
        else if (IsExecutedRecord(src.Status))
        {
            target.LinkScopeCompatibilityStatus = DocumentLinkScopeCompatibilityStatus.Unvalidated;
            target.StatusReason = ExecutedRecordStatusReason;
        }
        else
        {
            target.LinkScopeCompatibilityStatus = DocumentLinkScopeCompatibilityStatus.Unvalidated;
            target.StatusReason = null;
        }
    }
}
