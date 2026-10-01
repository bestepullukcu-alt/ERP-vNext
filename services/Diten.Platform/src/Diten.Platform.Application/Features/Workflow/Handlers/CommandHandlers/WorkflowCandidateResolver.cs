using Diten.Platform.Application.Features.TenantOrganization;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.Workflow.Handlers.CommandHandlers;

/// <summary>
/// MOD-0023 — turns a step's candidate list (<c>user:{id}</c>, <c>position:{id}</c>, or a bare principal id) into the
/// ordered list of principals that may act. Start, next-step and escalation all resolve through here.
/// <para>
/// WP-CL-BE-3 — a <c>position:</c> candidate yields a holder only when BOTH sides are live:
/// <list type="bullet">
/// <item>the ASSIGNMENT is active now by MOD-0288's own rule (<see cref="TenantOrganizationMapper.IsActiveNow"/>:
/// cancelled ⇒ ended, half-open window) and not deleted — any <see cref="AssignmentType"/> (Primary, Secondary, Acting,
/// Delegated) counts;</item>
/// <item>the POSITION is <see cref="PositionStatus.Active"/>, not archived and not deleted — a Draft, Frozen or Closed
/// seat hands out no approvals.</item>
/// </list>
/// Assignments are read for the requested positions only (tenant-scoped repositories), never the whole table. When
/// the position repository is not available the position candidates are DROPPED (fail-closed): an approver whose seat
/// cannot be verified is not an approver.
/// </para>
/// </summary>
internal static class WorkflowCandidateResolver
{
    private const string UserPrefix = "user:";
    private const string PositionPrefix = "position:";

    public static async Task<IReadOnlyList<string>> ResolveAsync(
        IReadOnlyList<string>? candidates,
        IPositionAssignmentRepository? positionAssignments,
        IPositionRepository? positions,
        CancellationToken ct)
    {
        var normalized = Normalize(candidates);
        if (normalized.Count == 0)
        {
            return [];
        }

        var resolved = new List<string>();
        var positionIds = new HashSet<Guid>();
        foreach (var candidate in normalized)
        {
            if (candidate.StartsWith(UserPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var userId = candidate[UserPrefix.Length..].Trim();
                if (!string.IsNullOrWhiteSpace(userId))
                {
                    resolved.Add(userId);
                }

                continue;
            }

            if (candidate.StartsWith(PositionPrefix, StringComparison.OrdinalIgnoreCase) &&
                Guid.TryParse(candidate[PositionPrefix.Length..].Trim(), out var positionId))
            {
                positionIds.Add(positionId);
                continue;
            }

            resolved.Add(candidate);
        }

        if (positionIds.Count > 0 && positionAssignments is not null && positions is not null)
        {
            var livePositions = (await positions.GetByIdsAsync(positionIds, ct))
                .Where(IsLivePosition)
                .Select(x => x.Id)
                .ToHashSet();
            if (livePositions.Count > 0)
            {
                var now = DateTimeOffset.UtcNow;
                resolved.AddRange((await positionAssignments.GetByPositionIdsAsync(livePositions, ct))
                    .Where(x => livePositions.Contains(x.PositionId))
                    .Where(x => !x.IsDeleted && x.DeletedAt is null)
                    .Where(x => TenantOrganizationMapper.IsActiveNow(x, now))
                    .Select(x => x.UserId.ToString()));
            }
        }

        return resolved
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
    }

    // internal: REQ-WCN-01 names a step's candidate positions by this same rule (WorkflowApprovalWorkItemProvider).
    internal static bool IsLivePosition(Position position) =>
        position.Status == PositionStatus.Active
        && !position.IsArchived
        && !position.IsDeleted
        && position.DeletedAt is null;

    public static List<string> Normalize(IReadOnlyList<string>? candidates) =>
        (candidates ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
}
