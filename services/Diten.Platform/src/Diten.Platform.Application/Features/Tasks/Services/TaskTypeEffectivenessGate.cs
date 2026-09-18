using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Models;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Services;
using Diten.Platform.Domain.Entities.Tasks;

namespace Diten.Platform.Application.Features.Tasks.Services;

/// <summary>Which of the three disjoint outcomes <see cref="TaskTypeEffectivenessGate.CheckAsync(IControlledDocumentEffectivenessPort,TaskType,CancellationToken)"/>
/// found. <see cref="TaskTypeEffectivenessCheck.BlockingDetails"/> is populated only for <see cref="SomeBlocked"/>.</summary>
internal enum TaskTypeEffectivenessOutcome
{
    /// <summary>No bound document, or every bound document is <see cref="DocumentEffectivenessState.Effective"/>.</summary>
    AllEffective,

    /// <summary>At least one bound document resolved to Blocked or Unresolved — a DATA fact, not an infrastructure failure.</summary>
    SomeBlocked,

    /// <summary>The port itself threw — "could not check", never silently treated as clean or as Unresolved.</summary>
    RegisterUnavailable
}

internal sealed record TaskTypeEffectivenessCheck(TaskTypeEffectivenessOutcome Outcome, IReadOnlyList<string> BlockingDetails)
{
    public static readonly TaskTypeEffectivenessCheck Clean = new(TaskTypeEffectivenessOutcome.AllEffective, []);
    public static readonly TaskTypeEffectivenessCheck Unavailable = new(TaskTypeEffectivenessOutcome.RegisterUnavailable, []);
}

/// <summary>
/// WP-DM-DCP005-BL380-KURAL4-01 + WP-DM-DCP005-KURAL4-UI-01 (sahip 2026-09-15, Blueprint/SAP/Oracle kıyasıyla
/// doğrulandı — WP-CT-DECISION-BENCHMARK-01) — the ONE place that resolves a task type's bound governing
/// controlled documents (<see cref="TaskType.GroupDocuments"/> / <see cref="TaskType.LocalDocuments"/>) against
/// the Document Master Register and says what that means. <see cref="CheckAsync(IControlledDocumentEffectivenessPort,TaskType,CancellationToken)"/>
/// only RESOLVES (contract v2 §6: "port/uç karar vermez") — each caller decides what its own outcome means:
///
/// <list type="bullet">
/// <item><b>Create (<c>CreateTaskTypeHandler</c>)</b> — NEVER refused. A type born with a non-Effective or
/// unverifiable document is saved <c>IsActive=false</c> instead; the caller reads <see cref="TaskTypeEffectivenessCheck"/>
/// directly, never <see cref="ToBlockingResponse{T}"/>.</item>
/// <item><b>Editing bound documents on an ALREADY-ACTIVE type (<c>UpdateTaskTypeHandler</c>)</b> — hard refused
/// via <see cref="ToBlockingResponse{T}"/>, but ONLY when <see cref="HaveDocumentsChanged"/> says the incoming
/// set actually differs from what is stored; editing anything else, or re-posting the same set, never re-checks
/// (an active type is not retroactively re-examined just because it was saved again).</item>
/// <item><b>/active, pasif→aktif (<c>SetTaskTypeActiveHandler</c>)</b> — hard refused via
/// <see cref="ToBlockingResponse{T}"/>, unchanged from before this WP. Deactivating never checks; an
/// already-active type re-posted as active never re-checks.</item>
/// </list>
///
/// No handler carries its own copy of the resolve-and-classify logic — only the "what do I do about it" choice
/// differs per caller, and that choice is deliberately NOT here (contract v2 §6).
/// </summary>
internal static class TaskTypeEffectivenessGate
{
    public static Task<TaskTypeEffectivenessCheck> CheckAsync(
        IControlledDocumentEffectivenessPort port, TaskType type, CancellationToken ct)
        => CheckAsync(port, type.GroupDocuments, type.LocalDocuments, ct);

    public static async Task<TaskTypeEffectivenessCheck> CheckAsync(
        IControlledDocumentEffectivenessPort port,
        IReadOnlyList<string> groupDocuments,
        IReadOnlyDictionary<string, List<string>> localDocuments,
        CancellationToken ct)
    {
        var uids = groupDocuments
            .Concat(localDocuments.Values.SelectMany(v => v))
            .Where(uid => !string.IsNullOrWhiteSpace(uid))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (uids.Count == 0)
        {
            // No governing document is bound — nothing for Kural 4 to check (contract §1: the caller states By
            // explicitly; there is simply no identifier to resolve here).
            return TaskTypeEffectivenessCheck.Clean;
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
            return TaskTypeEffectivenessCheck.Unavailable;
        }

        var blocking = result.Items.Where(item => item.State != DocumentEffectivenessState.Effective).ToList();
        if (blocking.Count == 0)
        {
            return TaskTypeEffectivenessCheck.Clean;
        }

        var errors = blocking
            .Select(item => item.State == DocumentEffectivenessState.Unresolved
                ? $"{item.Identifier}: Unresolved"
                : $"{item.Identifier}: {item.State}" + (item.BlockedReason is { } reason ? $" ({reason})" : string.Empty))
            .ToList();

        return new TaskTypeEffectivenessCheck(TaskTypeEffectivenessOutcome.SomeBlocked, errors);
    }

    /// <summary>The HARD-refusal shape — /active and an active type's document-changing edit both want this
    /// unchanged. Returns null when the write may proceed.</summary>
    public static Response<T>? ToBlockingResponse<T>(TaskTypeEffectivenessCheck check, string correlationId) =>
        check.Outcome switch
        {
            TaskTypeEffectivenessOutcome.AllEffective => null,
            TaskTypeEffectivenessOutcome.SomeBlocked =>
                Response<T>.Fail(check.BlockingDetails, 409, TaskReasonCodes.TaskTypeEnableBlockedDocuments, correlationId),
            TaskTypeEffectivenessOutcome.RegisterUnavailable =>
                Response<T>.Fail(
                    "Could not verify the effectiveness of this type's governing documents; try again.",
                    503, TaskReasonCodes.TaskTypeEnableRegisterUnavailable, correlationId),
            _ => throw new ArgumentOutOfRangeException(nameof(check))
        };

    /// <summary>
    /// Whether the NORMALIZED incoming document set actually differs from what is stored — set comparison, not
    /// list-order comparison (re-arranging the same UIDs in the textarea is not a change). This is what tells
    /// <c>UpdateTaskTypeHandler</c> apart from a title/description-only edit of an active, already-governed type:
    /// without it, EVERY save of an active governed type would re-run the gate (the handler always full-replaces
    /// both document fields), and a type whose already-bound documents predate this Kural could never be edited
    /// again for anything unrelated to its documents.
    /// </summary>
    public static bool HaveDocumentsChanged(
        IReadOnlyList<string> previousGroupDocuments,
        IReadOnlyDictionary<string, List<string>> previousLocalDocuments,
        IReadOnlyList<string> newGroupDocuments,
        IReadOnlyDictionary<string, List<string>> newLocalDocuments)
    {
        if (!previousGroupDocuments.ToHashSet(StringComparer.Ordinal).SetEquals(newGroupDocuments))
        {
            return true;
        }

        // Rebuilt with a case-insensitive comparer regardless of what the stored dictionary's own comparer is
        // (BSON deserialization does not guarantee OrdinalIgnoreCase survives the round trip) — org keys must
        // compare the same way TaskTypeRules.NormalizeLocalDocuments already keys them.
        var previous = new Dictionary<string, List<string>>(previousLocalDocuments, StringComparer.OrdinalIgnoreCase);
        if (previous.Count != newLocalDocuments.Count)
        {
            return true;
        }

        foreach (var (org, uids) in newLocalDocuments)
        {
            if (!previous.TryGetValue(org, out var previousUids)
                || !previousUids.ToHashSet(StringComparer.Ordinal).SetEquals(uids))
            {
                return true;
            }
        }

        return false;
    }
}
