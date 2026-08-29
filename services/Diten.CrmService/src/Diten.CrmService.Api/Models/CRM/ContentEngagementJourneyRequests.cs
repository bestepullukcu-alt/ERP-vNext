using System.Text.Json.Serialization;

namespace Diten.CrmService.Api.Models.CRM;

// MOD-0162 FU05 request models. TenantId is NEVER part of any request body — it is server-resolved from the JWT claim.
// Route ids (journeyId / stageId) come from the path, never the body. Stages are managed only through the stage
// sub-routes; a `stages` array on the journey update is rejected (V-J16) — the property exists only so the handler can
// detect and 400 it. Campaign / Brand / Product / Segment members are deliberately ABSENT (§2.1/S6): an unknown member
// is rejected by the strict JSON binding, never silently swallowed.

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateContentEngagementJourneyRequest(
    string JourneyCode,
    string JourneyName,
    Guid SubjectId,
    string Objective,
    string JourneyVersion,
    DateTimeOffset EffectiveFrom,
    string? Description = null,
    Guid? TopicId = null,
    Guid? AudienceProfileId = null,
    string? LanguageCode = null,
    string? JourneyStatus = null,
    DateTimeOffset? EffectiveTo = null,
    string? Source = null,
    Guid? TenantId = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateContentEngagementJourneyRequest(
    string JourneyName,
    Guid SubjectId,
    string Objective,
    string JourneyVersion,
    DateTimeOffset EffectiveFrom,
    string? Description = null,
    Guid? TopicId = null,
    Guid? AudienceProfileId = null,
    string? LanguageCode = null,
    string? JourneyStatus = null,
    DateTimeOffset? EffectiveTo = null,
    string? Source = null,
    int? ExpectedVersion = null,
    object? Stages = null,
    Guid? TenantId = null);

public sealed record CreateContentEngagementJourneyVersionRequest(string? NewJourneyVersion = null);

public sealed record ContentEngagementJourneyBranchConditionRequest(
    string ConditionCode,
    string? Description = null,
    Guid? TargetStageId = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AddContentEngagementJourneyStageRequest(
    int StageOrder,
    string StageCode,
    string StageName,
    string StageObjective,
    Guid RecommendedKnowledgePathId,
    bool IsRequired,
    bool Repeatable = false,
    string? StageType = null,
    string? PathVersionPinPolicy = null,
    int? MinVisitNumber = null,
    int? MaxVisitNumber = null,
    string? AdvancementRule = null,
    Guid? FallbackStageId = null,
    string? Notes = null,
    IReadOnlyList<ContentEngagementJourneyBranchConditionRequest>? BranchConditions = null,
    int? ExpectedVersion = null,
    Guid? TenantId = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateContentEngagementJourneyStageRequest(
    int StageOrder,
    string StageCode,
    string StageName,
    string StageObjective,
    Guid RecommendedKnowledgePathId,
    bool IsRequired,
    bool Repeatable = false,
    string? StageType = null,
    string? PathVersionPinPolicy = null,
    int? MinVisitNumber = null,
    int? MaxVisitNumber = null,
    string? AdvancementRule = null,
    Guid? FallbackStageId = null,
    string? Notes = null,
    IReadOnlyList<ContentEngagementJourneyBranchConditionRequest>? BranchConditions = null,
    int? ExpectedVersion = null,
    Guid? TenantId = null);
