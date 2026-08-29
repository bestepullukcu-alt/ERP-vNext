using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.Knowledge.Path;

/// <summary>
/// MOD-0162 FU04 structural validation. Every rule returns a message (400/409 text) or null. All vocabulary (path-status
/// / source / step-type / completion-rule / version-pin / step-status) is validated IN-DOMAIN against the
/// <c>KnowledgePath*</c> constants — never through MOD-0048, so authoring never fails open on an unpublished set. The
/// in-array step rules (unique StepOrder/StepCode, prerequisite direction/cycle, branch-target sanity, freeze) live here
/// as pure functions over the in-memory <see cref="KnowledgePath.Steps"/> list, because Mongo cannot enforce in-array
/// uniqueness — the handler is the ONLY defence (§4.5). Existence / archived / cross-tenant reference checks need
/// repository access and live in the handlers.
/// </summary>
public static class KnowledgePathValidation
{
    public const int MaxPathCode = 100;
    public const int MaxPathName = 200;
    public const int MaxObjective = 500;
    public const int MaxDescription = 2000;
    public const int MaxNotes = 2000;
    public const int MaxBranchDescription = 500;

    // ---------------- path scalar rules ----------------

    public static string? ValidatePathCode(string? code)
        => string.IsNullOrWhiteSpace(code) ? "PathCode is required."
            : code.Trim().Length > MaxPathCode ? $"PathCode cannot exceed {MaxPathCode} characters." : null;

    public static string? ValidatePathName(string? name)
        => string.IsNullOrWhiteSpace(name) ? "PathName is required."
            : name.Trim().Length > MaxPathName ? $"PathName cannot exceed {MaxPathName} characters." : null;

    public static string? ValidateObjective(string? objective)
        => string.IsNullOrWhiteSpace(objective) ? "Objective is required."
            : objective.Trim().Length > MaxObjective ? $"Objective cannot exceed {MaxObjective} characters." : null;

    public static string? ValidateDescription(string? description)
        => description is { } d && d.Length > MaxDescription
            ? $"Description cannot exceed {MaxDescription} characters." : null;

    public static string? ValidatePathVersion(string? version)
        => string.IsNullOrWhiteSpace(version) ? "PathVersion is required." : null;

    public static string? ValidatePathStatus(string? status)
        => string.IsNullOrWhiteSpace(status) || KnowledgePathStatuses.IsValid(status)
            ? null
            : $"PathStatus must be one of: {string.Join(", ", KnowledgePathStatuses.All)}. " +
              "A path is never hard-deleted; closing it is the archive endpoint.";

    public static string? ValidateSource(string? source)
        => string.IsNullOrWhiteSpace(source) || KnowledgePathSources.IsValid(source)
            ? null
            : $"Source must be one of: {string.Join(", ", KnowledgePathSources.All)}.";

    // ---------------- step scalar rules ----------------

    public static string? ValidateStepCode(string? code)
        => string.IsNullOrWhiteSpace(code) ? "StepCode is required." : null;

    public static string? ValidateStepTitle(string? title)
        => string.IsNullOrWhiteSpace(title) ? "StepTitle is required." : null;

    public static string? ValidateStepType(string? type)
        => KnowledgePathStepTypes.IsValid(type)
            ? null
            : $"StepType is required and must be one of: {string.Join(", ", KnowledgePathStepTypes.All)}.";

    public static string? ValidateCompletionRule(string? rule)
        => string.IsNullOrWhiteSpace(rule) || KnowledgePathCompletionRules.IsValid(rule)
            ? null
            : $"CompletionRule must be one of: {string.Join(", ", KnowledgePathCompletionRules.All)}.";

    public static string? ValidateVersionPin(string? policy)
        => string.IsNullOrWhiteSpace(policy) || KnowledgePathVersionPin.IsValid(policy)
            ? null
            : $"VersionPinPolicy must be one of: {string.Join(", ", KnowledgePathVersionPin.All)}.";

    public static string? ValidateStepStatus(string? status)
        => string.IsNullOrWhiteSpace(status) || KnowledgePathStepStatuses.IsValid(status)
            ? null
            : $"StepStatus must be one of: {string.Join(", ", KnowledgePathStepStatuses.All)}.";

    public static string? ValidateNotes(string? notes)
        => notes is { } n && n.Length > MaxNotes ? $"Notes cannot exceed {MaxNotes} characters." : null;

    /// <summary>duration-met requires a duration in [1, 600]; other rules leave it free (V-S11).</summary>
    public static string? ValidateDuration(string? completionRule, int? minutes)
    {
        var isDurationMet = string.Equals(
            KnowledgePathCompletionRules.Normalize(completionRule),
            KnowledgePathCompletionRules.DurationMet, StringComparison.Ordinal);

        if (isDurationMet && minutes is null)
        {
            return "EstimatedDurationMinutes is required when CompletionRule = duration-met.";
        }

        if (minutes is { } m &&
            (m < KnowledgePathLimits.MinEstimatedDurationMinutes || m > KnowledgePathLimits.MaxEstimatedDurationMinutes))
        {
            return $"EstimatedDurationMinutes must be between {KnowledgePathLimits.MinEstimatedDurationMinutes} and " +
                   $"{KnowledgePathLimits.MaxEstimatedDurationMinutes}.";
        }

        return null;
    }

    // ---------------- branch conditions (D7 — data only, never evaluated) ----------------

    /// <summary>Shape only: each ConditionCode is required, at most 20 per step, description length bounded. The
    /// TargetStepId same-path check needs the path context and is done in <see cref="ValidateBranchTargets"/>.</summary>
    public static string? ValidateBranchShape(IReadOnlyList<KnowledgePathBranchConditionInput>? conditions)
    {
        if (conditions is null || conditions.Count == 0)
        {
            return null;
        }

        if (conditions.Count > KnowledgePathLimits.MaxBranchConditionsPerStep)
        {
            return $"A step cannot carry more than {KnowledgePathLimits.MaxBranchConditionsPerStep} branch conditions.";
        }

        foreach (var condition in conditions)
        {
            if (string.IsNullOrWhiteSpace(condition.ConditionCode))
            {
                return "BranchConditions[].ConditionCode is required.";
            }

            if (condition.Description is { } d && d.Length > MaxBranchDescription)
            {
                return $"BranchConditions[].Description cannot exceed {MaxBranchDescription} characters.";
            }
        }

        return null;
    }

    /// <summary>V-S14: every non-null TargetStepId must reference a step in the same path (referential sanity, NEVER
    /// evaluated). <paramref name="stepIdsInPath"/> is the set of step ids the path will hold after this write.</summary>
    public static string? ValidateBranchTargets(
        IReadOnlyList<KnowledgePathBranchConditionInput>? conditions, IReadOnlySet<Guid> stepIdsInPath)
    {
        if (conditions is null)
        {
            return null;
        }

        foreach (var condition in conditions)
        {
            if (condition.TargetStepId is { } target && target != Guid.Empty && !stepIdsInPath.Contains(target))
            {
                return "BranchConditions[].TargetStepId must reference a step in the same path.";
            }
        }

        return null;
    }

    // ---------------- in-array step-set rules (handler is the only defence — no DB index) ----------------

    /// <summary>V-S03/S04: StepOrder and StepCode must be unique among ACTIVE steps, excluding the step being edited
    /// (<paramref name="editingStepId"/>). Returns the 409 message or null.</summary>
    public static string? ValidateStepUniqueness(
        KnowledgePath path, int stepOrder, string stepCode, Guid? editingStepId)
    {
        foreach (var existing in path.Steps.Where(s => !s.IsArchived()))
        {
            if (editingStepId is { } id && existing.StepId == id)
            {
                continue;
            }

            if (existing.StepOrder == stepOrder)
            {
                return $"StepOrder {stepOrder} is already used by an active step in this path.";
            }

            if (string.Equals(existing.StepCode, stepCode?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return $"StepCode '{stepCode?.Trim()}' is already used by an active step in this path.";
            }
        }

        return null;
    }

    /// <summary>V-S09/S10: a prerequisite must be another ACTIVE step in the same path, with a strictly smaller
    /// StepOrder, no cycle, and — when the dependent step is required — the prerequisite must also be required.</summary>
    public static string? ValidatePrerequisite(
        KnowledgePath path, Guid? prerequisiteStepId, Guid selfStepId, int selfOrder, bool selfRequired)
    {
        if (prerequisiteStepId is not { } prereqId || prereqId == Guid.Empty)
        {
            return null;
        }

        if (prereqId == selfStepId)
        {
            return "PrerequisiteStepId cannot reference the step itself.";
        }

        var prereq = path.Steps.FirstOrDefault(s => s.StepId == prereqId && !s.IsArchived());
        if (prereq is null)
        {
            return "PrerequisiteStepId must reference an active step in the same path.";
        }

        if (prereq.StepOrder >= selfOrder)
        {
            return "A prerequisite step must have a smaller StepOrder (dependencies point backward only).";
        }

        if (selfRequired && !prereq.IsRequired)
        {
            return "A required step cannot depend on an optional prerequisite (the required chain would be skippable).";
        }

        if (HasPrerequisiteCycle(path, prereqId, selfStepId, selfOrder))
        {
            return "PrerequisiteStepId would create a prerequisite cycle.";
        }

        return null;
    }

    private static bool HasPrerequisiteCycle(KnowledgePath path, Guid startPrereqId, Guid selfStepId, int selfOrder)
    {
        var visited = new HashSet<Guid>();
        var current = path.Steps.FirstOrDefault(s => s.StepId == startPrereqId && !s.IsArchived());
        while (current is not null)
        {
            if (current.StepId == selfStepId)
            {
                return true;
            }

            if (!visited.Add(current.StepId))
            {
                return true; // pre-existing loop
            }

            if (current.PrerequisiteStepId is not { } next || next == Guid.Empty)
            {
                return false;
            }

            current = path.Steps.FirstOrDefault(s => s.StepId == next && !s.IsArchived());
        }

        return false;
    }

    public static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Branch condition write shape (D7). Carried as data, echoed back as data, never evaluated.</summary>
public sealed record KnowledgePathBranchConditionInput(string ConditionCode, string? Description, Guid? TargetStepId);
