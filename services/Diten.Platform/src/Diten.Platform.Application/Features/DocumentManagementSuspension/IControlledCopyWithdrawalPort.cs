using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Application.Features.DocumentManagementSuspension;

/// <summary>
/// MOD-0029-FU13 → FU17 seam. A PORT the FU13 suspension/retirement engine invokes AFTER a document transitions to
/// Suspended/Retired, so the FU17 controlled-copy feature can raise a withdrawal plan and flag active copies for
/// withdrawal. FU13 owns the interface; FU17 provides the adapter. Kept optional so FU13 has no hard dependency on the
/// controlled-copy feature — a null port means "no automatic withdrawal plan" (backward compatible).
/// </summary>
public interface IControlledCopyWithdrawalPort
{
    /// <summary>Raises a withdrawal plan for the entry's active copies for the given trigger. Idempotent; never throws
    /// into the caller's transaction (failures are the caller's concern, but the adapter is best-effort).</summary>
    Task OnDocumentWithdrawnAsync(DocumentMasterRegisterEntry entry, ControlledDocumentLifecycleStatus newStatus, string correlationId, CancellationToken ct);
}
