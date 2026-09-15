using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Models;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Services;
using Diten.Platform.Domain.Entities.Tasks;

namespace Diten.Platform.Application.Features.Tasks.Services;

/// <summary>
/// WP-DM-DCP005-BL380-KURAL4-01 (Kural 4, DCP-005 Adım 3, G3 — sahip 2026-09-15, Kalite teyidi bekliyor) — the ONE
/// place that decides whether a task type's bound governing controlled documents (<see cref="TaskType.GroupDocuments"/>
/// / <see cref="TaskType.LocalDocuments"/>) let it go ACTIVE. Every write path that can make a type active —
/// <c>SetTaskTypeActiveHandler</c> (pasiften aktife) and <c>CreateTaskTypeHandler</c> (born <c>IsActive=true</c>,
/// measured: documents can already be bound at creation) — calls this SAME method before writing. No handler
/// carries its own copy of the rule, so a differing Kalite answer changes exactly one place.
///
/// <para><b>Fail-closed, deliberately.</b> Any document that is not <see cref="DocumentEffectivenessState.Effective"/>
/// — Blocked or Unresolved — refuses activation, and so does a thrown exception from the port itself (the register
/// could not be reached): silently letting activation through on a read failure is exactly the gap this Kural closes.</para>
///
/// <para><b>Retiring is never gated.</b> This method is called ONLY on the pasif→aktif transition; nothing here
/// runs when a type is taken inactive, and an already-active type never re-checks itself.</para>
/// </summary>
internal static class TaskTypeEffectivenessGate
{
    /// <summary>Returns null when activation may proceed; a failed <see cref="Response{T}"/> otherwise.</summary>
    public static async Task<Response<T>?> BlockIfGoverningDocumentsNotEffectiveAsync<T>(
        IControlledDocumentEffectivenessPort port,
        TaskType type,
        string correlationId,
        CancellationToken ct)
    {
        var uids = type.GroupDocuments
            .Concat(type.LocalDocuments.Values.SelectMany(v => v))
            .Where(uid => !string.IsNullOrWhiteSpace(uid))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (uids.Count == 0)
        {
            // No governing document is bound — nothing for Kural 4 to check (contract §1: the caller states By
            // explicitly; there is simply no identifier to resolve here).
            return null;
        }

        DocumentEffectivenessResult result;
        try
        {
            result = await port.ResolveAsync(new DocumentEffectivenessQuery(uids, DocumentIdentifierKind.Uid), ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Fail-closed (contract §2/§3/§5): a register read that throws is "could not check", never treated as
            // Unresolved or silently allowed through.
            return Response<T>.Fail(
                "Could not verify the effectiveness of this type's governing documents; try again.",
                503,
                TaskReasonCodes.TaskTypeEnableRegisterUnavailable,
                correlationId);
        }

        var blocking = result.Items.Where(item => item.State != DocumentEffectivenessState.Effective).ToList();
        if (blocking.Count == 0)
        {
            return null;
        }

        var errors = blocking
            .Select(item => item.State == DocumentEffectivenessState.Unresolved
                ? $"{item.Identifier}: Unresolved"
                : $"{item.Identifier}: {item.State}" + (item.BlockedReason is { } reason ? $" ({reason})" : string.Empty))
            .ToList();

        return Response<T>.Fail(errors, 409, TaskReasonCodes.TaskTypeEnableBlockedDocuments, correlationId);
    }
}
