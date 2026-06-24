using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.Workflow.Handlers.CommandHandlers;

internal static class WorkflowCandidateResolver
{
    private const string UserPrefix = "user:";
    private const string PositionPrefix = "position:";

    public static async Task<IReadOnlyList<string>> ResolveAsync(
        IReadOnlyList<string>? candidates,
        IPositionAssignmentRepository? positionAssignments,
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

        if (positionIds.Count > 0 && positionAssignments is not null)
        {
            var now = DateTimeOffset.UtcNow;
            var assignments = await positionAssignments.GetAllAsync(ct);
            resolved.AddRange(assignments
                .Where(x => positionIds.Contains(x.PositionId))
                .Where(x => x.EffectiveFrom <= now && (!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= now))
                .Select(x => x.UserId.ToString()));
        }

        return resolved
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
    }

    public static List<string> Normalize(IReadOnlyList<string>? candidates) =>
        (candidates ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
}
