using Diten.CrmService.Application.Features.Territory.ResourceAssignments;
using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.Territory.PlanVsCurrent;

/// <summary>
/// MOD-0151 FU04B read-time diff engine (pack §22.4). Pure function over an immutable baseline plus the live
/// assignment chain — it writes nothing, caches nothing and is deterministic for a given (snapshot, assignments,
/// effectiveAt) triple.
///
/// <para><b>Slot key</b> = TerritoryNodeId + normalized PositionCode + business scopes; the live counterpart is found
/// by walking the replacement/transfer provenance forward from <c>SourceAssignmentId</c>. Legacy RoleCode is never a
/// key.</para>
/// </summary>
public static class TerritoryPlanVsCurrentEngine
{
    public sealed record Input(
        Guid ModelId,
        string ModelCode,
        TerritoryResourceAssignmentPlanSnapshot? Snapshot,
        IReadOnlyList<TerritoryResourceAssignment> Assignments,
        IReadOnlyDictionary<Guid, TerritoryNode> Nodes,
        DateTimeOffset EffectiveAt);

    public static IReadOnlyList<TerritoryPlanVsCurrentRowDto> Compute(Input input)
    {
        var byId = input.Assignments.ToDictionary(a => a.Id);
        var isCurrent = input.Assignments
            .Where(a => TerritoryCurrentResponsibilityPolicy.IsCurrent(a, input.EffectiveAt))
            .Select(a => a.Id)
            .ToHashSet();

        var rows = new List<TerritoryPlanVsCurrentRowDto>();
        var consumed = new HashSet<Guid>();

        foreach (var line in input.Snapshot?.Lines ?? [])
        {
            var source = byId.GetValueOrDefault(line.SourceAssignmentId);
            if (source is null)
            {
                // The baseline points at an assignment the live chain no longer exposes: an integrity signal, not an error.
                rows.Add(PlannedOnlyRow(input, line, TerritoryPlanVsCurrentDiffTypes.MissingCurrent, null));
                continue;
            }

            var chain = Walk(source, byId);
            var terminal = chain.Terminal;
            consumed.Add(source.Id);
            consumed.Add(terminal.Id);

            var terminalIsCurrent = isCurrent.Contains(terminal.Id);
            var movedNode = terminal.TerritoryId != line.TerritoryNodeId;

            if (chain.HasReplacement && terminalIsCurrent && !movedNode)
            {
                rows.Add(ComparisonRow(input, line, terminal, TerritoryPlanVsCurrentDiffTypes.Replaced));
                continue;
            }

            if (chain.HasTransfer && movedNode)
            {
                // The planned slot lost its holder …
                rows.Add(PlannedOnlyRow(input, line, TerritoryPlanVsCurrentDiffTypes.TransferredOut, terminal));
                // … and the target node gained one.
                if (terminalIsCurrent)
                {
                    rows.Add(TransferredInRow(input, line, terminal));
                }
                continue;
            }

            if (chain.HasReplacement && terminalIsCurrent)
            {
                rows.Add(ComparisonRow(input, line, terminal, TerritoryPlanVsCurrentDiffTypes.Replaced));
                continue;
            }

            if (!terminalIsCurrent)
            {
                var ended = string.Equals(terminal.Status, TerritoryResourceAssignmentValidation.EndedStatus,
                                StringComparison.OrdinalIgnoreCase)
                            || terminal.ValidTo is { } to && to < input.EffectiveAt;
                rows.Add(PlannedOnlyRow(
                    input, line,
                    ended
                        ? TerritoryPlanVsCurrentDiffTypes.EndedAfterActivation
                        : TerritoryPlanVsCurrentDiffTypes.MissingCurrent,
                    terminal));
                continue;
            }

            rows.Add(ComparisonRow(input, line, terminal, diffType: null));
        }

        // Anything current that no baseline line reaches was opened after activation.
        foreach (var assignment in input.Assignments)
        {
            if (consumed.Contains(assignment.Id) || !isCurrent.Contains(assignment.Id))
            {
                continue;
            }

            rows.Add(CurrentOnlyRow(input, assignment, TerritoryPlanVsCurrentDiffTypes.AddedAfterActivation));
        }

        return rows
            .OrderBy(r => TerritoryPlanVsCurrentDiffTypes.Rank(r.DiffType))
            .ThenBy(r => r.TerritoryNodeCode, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.PositionCode, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static TerritoryPlanVsCurrentSummaryDto Summarize(
        int plannedCount, int currentCount, IReadOnlyList<TerritoryPlanVsCurrentRowDto> rows)
        => new(
            plannedCount,
            currentCount,
            rows.Count,
            rows.Count(r => !string.Equals(r.DiffType, TerritoryPlanVsCurrentDiffTypes.Unchanged, StringComparison.OrdinalIgnoreCase)),
            rows.GroupBy(r => r.DiffType, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase));

    public static IReadOnlyList<TerritoryPlanVsCurrentRowDto> Filter(
        IReadOnlyList<TerritoryPlanVsCurrentRowDto> rows,
        Guid? territoryNodeId, string? businessUnit, string? positionCode, string? resourceId, string? diffType)
        => rows.Where(r =>
                (territoryNodeId is null || r.TerritoryNodeId == territoryNodeId || r.CurrentTerritoryNodeId == territoryNodeId)
                && (string.IsNullOrWhiteSpace(businessUnit)
                    || r.BusinessUnitScopes.Concat(r.CurrentBusinessUnitScopes).Any(s =>
                        string.Equals(s, businessUnit, StringComparison.OrdinalIgnoreCase)))
                && (string.IsNullOrWhiteSpace(positionCode)
                    || string.Equals(r.PositionCode, positionCode, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(r.CurrentPositionCode, positionCode, StringComparison.OrdinalIgnoreCase))
                && (string.IsNullOrWhiteSpace(resourceId)
                    || string.Equals(r.PlannedResourceId, resourceId, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(r.CurrentResourceId, resourceId, StringComparison.OrdinalIgnoreCase))
                && (string.IsNullOrWhiteSpace(diffType)
                    || string.Equals(r.DiffType, diffType, StringComparison.OrdinalIgnoreCase)))
            .ToList();

    // -----------------------------------------------------------------------------------------------------------
    // Chain walking
    // -----------------------------------------------------------------------------------------------------------

    private sealed record ChainResult(TerritoryResourceAssignment Terminal, bool HasReplacement, bool HasTransfer);

    /// <summary>Follows replacement/transfer provenance forward to the terminal assignment. Cycle-guarded.</summary>
    private static ChainResult Walk(
        TerritoryResourceAssignment source, IReadOnlyDictionary<Guid, TerritoryResourceAssignment> byId)
    {
        var current = source;
        var visited = new HashSet<Guid> { source.Id };
        var hasReplacement = false;
        var hasTransfer = false;

        while (true)
        {
            var isReplacement = current.ReplacementAssignmentId is not null;
            var nextId = current.ReplacementAssignmentId ?? current.TransferToAssignmentId;
            if (nextId is not { } id || !visited.Add(id) || !byId.TryGetValue(id, out var next))
            {
                return new ChainResult(current, hasReplacement, hasTransfer);
            }

            if (isReplacement)
            {
                hasReplacement = true;
            }
            else
            {
                hasTransfer = true;
            }

            current = next;
        }
    }

    // -----------------------------------------------------------------------------------------------------------
    // Row builders
    // -----------------------------------------------------------------------------------------------------------

    private static TerritoryPlanVsCurrentRowDto PlannedOnlyRow(
        Input input, TerritoryResourceAssignmentPlanSnapshotLine line, string diffType,
        TerritoryResourceAssignment? terminal)
        => new(
            diffType, input.ModelId, input.ModelCode,
            line.TerritoryNodeId, line.TerritoryNodeCode, line.TerritoryNodeName,
            line.BusinessScopes, line.PositionCode, line.PositionTitle, line.PositionType,
            line.ResourceId, line.ResourceDisplayName, line.PlannedEffectiveFrom, line.PlannedEffectiveTo,
            line.IsPrimary, line.SourceAssignmentId,
            CurrentResourceId: null, CurrentResourceDisplayName: null, CurrentPositionCode: null,
            CurrentPositionTitle: null, CurrentBusinessUnitScopes: [], CurrentEffectiveFrom: null,
            CurrentEffectiveTo: null, CurrentIsPrimary: null,
            CurrentAssignmentId: terminal?.Id, CurrentTerritoryNodeId: terminal?.TerritoryId,
            CurrentTerritoryNodeCode: NodeCode(input, terminal?.TerritoryId), CurrentStatus: terminal?.Status,
            ChangeReason: terminal?.ChangeReason, ReplacementReason: terminal?.ReplacementReason,
            TransferReason: terminal?.TransferReason,
            ReplacedAssignmentId: terminal?.ReplacedAssignmentId,
            ReplacementAssignmentId: terminal?.ReplacementAssignmentId,
            TransferFromAssignmentId: terminal?.TransferFromAssignmentId,
            TransferToAssignmentId: terminal?.TransferToAssignmentId,
            ChangedAt: terminal?.UpdatedAt, ChangedBy: null, CorrelationId: terminal?.CorrelationId,
            SecondaryDifferences: [], LegacyRoleCode: null);

    private static TerritoryPlanVsCurrentRowDto TransferredInRow(
        Input input, TerritoryResourceAssignmentPlanSnapshotLine line, TerritoryResourceAssignment terminal)
        => new(
            TerritoryPlanVsCurrentDiffTypes.TransferredIn, input.ModelId, input.ModelCode,
            terminal.TerritoryId, NodeCode(input, terminal.TerritoryId) ?? string.Empty,
            NodeName(input, terminal.TerritoryId) ?? string.Empty,
            terminal.BusinessScopes.Select(s => s.ScopeCode).ToList(),
            terminal.EffectivePositionCode, terminal.EffectivePositionTitle, terminal.Position.PositionType,
            PlannedResourceId: null, PlannedResourceDisplayName: null,
            PlannedEffectiveFrom: null, PlannedEffectiveTo: null, PlannedIsPrimary: null,
            PlannedAssignmentId: line.SourceAssignmentId,
            terminal.Resource.ResourceId, terminal.Resource.DisplayName,
            terminal.EffectivePositionCode, terminal.EffectivePositionTitle,
            terminal.BusinessScopes.Select(s => s.ScopeCode).ToList(),
            terminal.ValidFrom, terminal.ValidTo, terminal.IsPrimary, terminal.Id,
            terminal.TerritoryId, NodeCode(input, terminal.TerritoryId), terminal.Status,
            terminal.ChangeReason, terminal.ReplacementReason, terminal.TransferReason,
            terminal.ReplacedAssignmentId, terminal.ReplacementAssignmentId,
            terminal.TransferFromAssignmentId, terminal.TransferToAssignmentId,
            terminal.UpdatedAt, ChangedBy: null, terminal.CorrelationId,
            SecondaryDifferences: [$"transferred-from:{line.TerritoryNodeCode}"],
            LegacyRoleCode: LegacyRole(terminal));

    private static TerritoryPlanVsCurrentRowDto CurrentOnlyRow(
        Input input, TerritoryResourceAssignment assignment, string diffType)
        => new(
            diffType, input.ModelId, input.ModelCode,
            assignment.TerritoryId, NodeCode(input, assignment.TerritoryId) ?? string.Empty,
            NodeName(input, assignment.TerritoryId) ?? string.Empty,
            assignment.BusinessScopes.Select(s => s.ScopeCode).ToList(),
            assignment.EffectivePositionCode, assignment.EffectivePositionTitle, assignment.Position.PositionType,
            PlannedResourceId: null, PlannedResourceDisplayName: null,
            PlannedEffectiveFrom: null, PlannedEffectiveTo: null, PlannedIsPrimary: null, PlannedAssignmentId: null,
            assignment.Resource.ResourceId, assignment.Resource.DisplayName,
            assignment.EffectivePositionCode, assignment.EffectivePositionTitle,
            assignment.BusinessScopes.Select(s => s.ScopeCode).ToList(),
            assignment.ValidFrom, assignment.ValidTo, assignment.IsPrimary, assignment.Id,
            assignment.TerritoryId, NodeCode(input, assignment.TerritoryId), assignment.Status,
            assignment.ChangeReason, assignment.ReplacementReason, assignment.TransferReason,
            assignment.ReplacedAssignmentId, assignment.ReplacementAssignmentId,
            assignment.TransferFromAssignmentId, assignment.TransferToAssignmentId,
            assignment.UpdatedAt, ChangedBy: null, assignment.CorrelationId,
            SecondaryDifferences: [], LegacyRoleCode: LegacyRole(assignment));

    /// <summary>Planned and current both exist. When <paramref name="diffType"/> is null the field comparison decides,
    /// otherwise the caller's higher-precedence verdict wins and the field deltas become secondary differences.</summary>
    private static TerritoryPlanVsCurrentRowDto ComparisonRow(
        Input input, TerritoryResourceAssignmentPlanSnapshotLine line, TerritoryResourceAssignment current,
        string? diffType)
    {
        var differences = new List<string>();

        var positionChanged = !string.Equals(
            TerritoryCurrentResponsibilityPolicy.NormalizePosition(line.PositionCode),
            TerritoryCurrentResponsibilityPolicy.NormalizePosition(current.EffectivePositionCode),
            StringComparison.Ordinal);
        if (positionChanged)
        {
            differences.Add(TerritoryPlanVsCurrentDiffTypes.PositionChanged);
        }

        var plannedScopes = line.BusinessScopes
            .Select(TerritoryCurrentResponsibilityPolicy.NormalizeScope).Where(s => s.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
        var currentScopes = current.BusinessScopes
            .Select(s => TerritoryCurrentResponsibilityPolicy.NormalizeScope(s.ScopeCode)).Where(s => s.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
        var scopeChanged = !plannedScopes.SetEquals(currentScopes) || line.IsPrimary != current.IsPrimary;
        if (scopeChanged)
        {
            differences.Add(TerritoryPlanVsCurrentDiffTypes.ScopeChanged);
        }

        var dateChanged = line.PlannedEffectiveFrom.UtcDateTime.Date != current.ValidFrom.UtcDateTime.Date
                          || line.PlannedEffectiveTo?.UtcDateTime.Date != current.ValidTo?.UtcDateTime.Date;
        if (dateChanged)
        {
            differences.Add(TerritoryPlanVsCurrentDiffTypes.DateChanged);
        }

        var resolved = diffType ?? (dateChanged
            ? TerritoryPlanVsCurrentDiffTypes.DateChanged
            : scopeChanged
                ? TerritoryPlanVsCurrentDiffTypes.ScopeChanged
                : positionChanged
                    ? TerritoryPlanVsCurrentDiffTypes.PositionChanged
                    : TerritoryPlanVsCurrentDiffTypes.Unchanged);

        // The winning verdict is not repeated in the secondary list.
        differences.RemoveAll(d => string.Equals(d, resolved, StringComparison.OrdinalIgnoreCase));

        return new TerritoryPlanVsCurrentRowDto(
            resolved, input.ModelId, input.ModelCode,
            line.TerritoryNodeId, line.TerritoryNodeCode, line.TerritoryNodeName,
            line.BusinessScopes, line.PositionCode, line.PositionTitle, line.PositionType,
            line.ResourceId, line.ResourceDisplayName, line.PlannedEffectiveFrom, line.PlannedEffectiveTo,
            line.IsPrimary, line.SourceAssignmentId,
            current.Resource.ResourceId, current.Resource.DisplayName,
            current.EffectivePositionCode, current.EffectivePositionTitle,
            current.BusinessScopes.Select(s => s.ScopeCode).ToList(),
            current.ValidFrom, current.ValidTo, current.IsPrimary, current.Id,
            current.TerritoryId, NodeCode(input, current.TerritoryId), current.Status,
            current.ChangeReason, current.ReplacementReason, current.TransferReason,
            current.ReplacedAssignmentId, current.ReplacementAssignmentId,
            current.TransferFromAssignmentId, current.TransferToAssignmentId,
            current.UpdatedAt, ChangedBy: null, current.CorrelationId,
            differences, LegacyRole(current));
    }

    private static string? NodeCode(Input input, Guid? nodeId)
        => nodeId is { } id && input.Nodes.TryGetValue(id, out var node) ? node.TerritoryCode : null;

    private static string? NodeName(Input input, Guid? nodeId)
        => nodeId is { } id && input.Nodes.TryGetValue(id, out var node) ? node.Name : null;

    /// <summary>Display-only legacy value; deliberately read from the deprecated flat field and never used as a key.</summary>
    private static string? LegacyRole(TerritoryResourceAssignment assignment)
        => string.IsNullOrWhiteSpace(assignment.PositionCode)
           || string.Equals(assignment.PositionCode, assignment.Position.PositionCode, StringComparison.OrdinalIgnoreCase)
            ? null
            : assignment.PositionCode;
}
