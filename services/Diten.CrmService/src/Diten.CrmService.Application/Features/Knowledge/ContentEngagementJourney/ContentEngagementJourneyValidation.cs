using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.Knowledge.ContentEngagementJourney;

using JourneyEntity = Diten.CrmService.Domain.Entities.ContentEngagementJourney;

/// <summary>
/// MOD-0162 FU05 structural validation. Every rule returns a message (400/409 text) or null. All vocabulary
/// (journey-status / source / stage-type / advancement-rule / path-pin / stage-status) is validated IN-DOMAIN against
/// the <c>ContentEngagementJourney*</c> constants (D-VOCAB = A) — never through MOD-0048, so authoring never fails open
/// on an unpublished set. The in-array stage rules (unique StageOrder/StageCode, fallback sanity, branch-target sanity,
/// freeze, limits) live here as pure functions over the in-memory <see cref="JourneyEntity.Stages"/> list, because Mongo
/// cannot enforce in-array uniqueness — the handler is the ONLY defence (§4.5). Existence / archived / cross-tenant
/// reference checks need repository access and live in the handlers.
/// </summary>
public static class ContentEngagementJourneyValidation
{
    public const int MaxJourneyCode = 100;
    public const int MaxJourneyName = 200;
    public const int MaxObjective = 500;
    public const int MaxDescription = 2000;
    public const int MaxStageName = 200;
    public const int MaxStageObjective = 500;
    public const int MaxNotes = 2000;
    public const int MaxBranchDescription = 500;

    // ---------------- journey scalar rules ----------------

    public static string? ValidateJourneyCode(string? code)
        => string.IsNullOrWhiteSpace(code) ? "JourneyCode is required."
            : code.Trim().Length > MaxJourneyCode ? $"JourneyCode cannot exceed {MaxJourneyCode} characters." : null;

    public static string? ValidateJourneyName(string? name)
        => string.IsNullOrWhiteSpace(name) ? "JourneyName is required."
            : name.Trim().Length > MaxJourneyName ? $"JourneyName cannot exceed {MaxJourneyName} characters." : null;

    public static string? ValidateObjective(string? objective)
        => string.IsNullOrWhiteSpace(objective) ? "Objective is required."
            : objective.Trim().Length > MaxObjective ? $"Objective cannot exceed {MaxObjective} characters." : null;

    public static string? ValidateDescription(string? description)
        => description is { } d && d.Length > MaxDescription
            ? $"Description cannot exceed {MaxDescription} characters." : null;

    public static string? ValidateJourneyVersion(string? version)
        => string.IsNullOrWhiteSpace(version) ? "JourneyVersion is required." : null;

    public static string? ValidateJourneyStatus(string? status)
        => string.IsNullOrWhiteSpace(status) || ContentEngagementJourneyStatuses.IsValid(status)
            ? null
            : $"JourneyStatus must be one of: {string.Join(", ", ContentEngagementJourneyStatuses.All)}. " +
              "A journey is never hard-deleted; closing it is the archive endpoint.";

    public static string? ValidateSource(string? source)
        => string.IsNullOrWhiteSpace(source) || ContentEngagementJourneySources.IsValid(source)
            ? null
            : $"Source must be one of: {string.Join(", ", ContentEngagementJourneySources.All)}.";

    // ---------------- stage scalar rules ----------------

    public static string? ValidateStageCode(string? code)
        => string.IsNullOrWhiteSpace(code) ? "StageCode is required." : null;

    public static string? ValidateStageName(string? name)
        => string.IsNullOrWhiteSpace(name) ? "StageName is required."
            : name.Trim().Length > MaxStageName ? $"StageName cannot exceed {MaxStageName} characters." : null;

    public static string? ValidateStageObjective(string? objective)
        => string.IsNullOrWhiteSpace(objective) ? "StageObjective is required."
            : objective.Trim().Length > MaxStageObjective
                ? $"StageObjective cannot exceed {MaxStageObjective} characters." : null;

    /// <summary>StageType is OPTIONAL (FU01B §5.2) but fail-closed when supplied.</summary>
    public static string? ValidateStageType(string? type)
        => string.IsNullOrWhiteSpace(type) || ContentEngagementJourneyStageTypes.IsValid(type)
            ? null
            : $"StageType must be one of: {string.Join(", ", ContentEngagementJourneyStageTypes.All)}.";

    /// <summary>AdvancementRule is DECLARED metadata and never evaluated — but it is still fail-closed vocabulary.</summary>
    public static string? ValidateAdvancementRule(string? rule)
        => string.IsNullOrWhiteSpace(rule) || ContentEngagementJourneyAdvancementRules.IsValid(rule)
            ? null
            : $"AdvancementRule must be one of: {string.Join(", ", ContentEngagementJourneyAdvancementRules.All)}. " +
              "It is declared metadata only — this module never evaluates it.";

    public static string? ValidatePathPin(string? policy)
        => string.IsNullOrWhiteSpace(policy) || ContentEngagementJourneyPathPin.IsValid(policy)
            ? null
            : $"PathVersionPinPolicy must be one of: {string.Join(", ", ContentEngagementJourneyPathPin.All)}.";

    public static string? ValidateStageStatus(string? status)
        => string.IsNullOrWhiteSpace(status) || ContentEngagementJourneyStageStatuses.IsValid(status)
            ? null
            : $"StageStatus must be one of: {string.Join(", ", ContentEngagementJourneyStageStatuses.All)}.";

    public static string? ValidateNotes(string? notes)
        => notes is { } n && n.Length > MaxNotes ? $"Notes cannot exceed {MaxNotes} characters." : null;

    /// <summary>V-S11: both visit numbers are boundary metadata only (no scheduling), but they must be sane:
    /// each &gt;= 1 and Max &gt;= Min.</summary>
    public static string? ValidateVisitRange(int? minVisitNumber, int? maxVisitNumber)
    {
        if (minVisitNumber is { } min && min < ContentEngagementJourneyLimits.MinVisitNumber)
        {
            return $"MinVisitNumber must be {ContentEngagementJourneyLimits.MinVisitNumber} or greater.";
        }

        if (maxVisitNumber is { } max && max < ContentEngagementJourneyLimits.MinVisitNumber)
        {
            return $"MaxVisitNumber must be {ContentEngagementJourneyLimits.MinVisitNumber} or greater.";
        }

        if (minVisitNumber is { } lower && maxVisitNumber is { } upper && upper < lower)
        {
            return "MaxVisitNumber cannot be smaller than MinVisitNumber.";
        }

        return null;
    }

    // ---------------- branch conditions (S5 — data only, never evaluated) ----------------

    /// <summary>Shape only: each ConditionCode is required, at most 20 per stage, description length bounded. The
    /// TargetStageId same-journey check needs the journey context and is done in
    /// <see cref="ValidateBranchTargets"/>.</summary>
    public static string? ValidateBranchShape(
        IReadOnlyList<ContentEngagementJourneyBranchConditionInput>? conditions)
    {
        if (conditions is null || conditions.Count == 0)
        {
            return null;
        }

        if (conditions.Count > ContentEngagementJourneyLimits.MaxBranchConditionsPerStage)
        {
            return "A stage cannot carry more than " +
                   $"{ContentEngagementJourneyLimits.MaxBranchConditionsPerStage} branch conditions.";
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

    /// <summary>V-S15: every non-null TargetStageId must reference a stage in the same journey (referential sanity,
    /// NEVER evaluated). <paramref name="stageIdsInJourney"/> is the set of stage ids the journey will hold after this
    /// write.</summary>
    public static string? ValidateBranchTargets(
        IReadOnlyList<ContentEngagementJourneyBranchConditionInput>? conditions, IReadOnlySet<Guid> stageIdsInJourney)
    {
        if (conditions is null)
        {
            return null;
        }

        foreach (var condition in conditions)
        {
            if (condition.TargetStageId is { } target && target != Guid.Empty && !stageIdsInJourney.Contains(target))
            {
                return "BranchConditions[].TargetStageId must reference a stage in the same journey.";
            }
        }

        return null;
    }

    // ---------------- in-array stage-set rules (handler is the only defence — no DB index) ----------------

    /// <summary>V-S03/S04: StageOrder and StageCode must be unique among ACTIVE stages, excluding the stage being
    /// edited (<paramref name="editingStageId"/>). Returns the 409 message or null.</summary>
    public static string? ValidateStageUniqueness(
        JourneyEntity journey, int stageOrder, string? stageCode, Guid? editingStageId)
    {
        foreach (var existing in journey.Stages.Where(s => !s.IsArchived()))
        {
            if (editingStageId is { } id && existing.StageId == id)
            {
                continue;
            }

            if (existing.StageOrder == stageOrder)
            {
                return $"StageOrder {stageOrder} is already used by an active stage in this journey.";
            }

            if (string.Equals(existing.StageCode, stageCode?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return $"StageCode '{stageCode?.Trim()}' is already used by an active stage in this journey.";
            }
        }

        return null;
    }

    /// <summary>V-S10: a fallback must be another ACTIVE stage of the SAME journey and can never be the stage itself.
    /// Pointing BACKWARDS is explicitly allowed (objection → reinforcement, FU01B §4) and no cycle check is made —
    /// nothing is ever evaluated, so a loop cannot run.</summary>
    public static string? ValidateFallback(JourneyEntity journey, Guid? fallbackStageId, Guid selfStageId)
    {
        if (fallbackStageId is not { } fallbackId || fallbackId == Guid.Empty)
        {
            return null;
        }

        if (fallbackId == selfStageId)
        {
            return "FallbackStageId cannot reference the stage itself.";
        }

        var target = journey.Stages.FirstOrDefault(s => s.StageId == fallbackId && !s.IsArchived());
        return target is null
            ? "FallbackStageId must reference an active stage in the same journey."
            : null;
    }

    /// <summary>V-S18: document growth guard — the journey cannot hold more than 100 stages (archived elements stay in
    /// the document, so they count).</summary>
    public static string? ValidateStageLimit(JourneyEntity journey)
        => journey.Stages.Count >= ContentEngagementJourneyLimits.MaxStagesPerJourney
            ? $"A journey cannot carry more than {ContentEngagementJourneyLimits.MaxStagesPerJourney} stages."
            : null;

    /// <summary>V-S17: an active stage may not be archived while another ACTIVE stage still points at it through
    /// FallbackStageId or a branch condition target (dangling reference guard).</summary>
    public static string? ValidateNoDanglingReference(JourneyEntity journey, Guid stageId)
    {
        foreach (var other in journey.Stages.Where(s => !s.IsArchived() && s.StageId != stageId))
        {
            if (other.FallbackStageId == stageId)
            {
                return $"Stage '{other.StageCode}' uses this stage as its fallback; update it before archiving.";
            }

            if (other.BranchConditions.Any(c => c.TargetStageId == stageId))
            {
                return $"Stage '{other.StageCode}' has a branch condition targeting this stage; " +
                       "update it before archiving.";
            }
        }

        return null;
    }

    public static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Branch condition write shape (S5). Carried as data, echoed back as data, never evaluated.</summary>
public sealed record ContentEngagementJourneyBranchConditionInput(
    string ConditionCode, string? Description, Guid? TargetStageId);
