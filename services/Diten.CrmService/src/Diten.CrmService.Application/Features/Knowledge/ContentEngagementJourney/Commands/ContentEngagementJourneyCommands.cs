using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Knowledge.ContentEngagementJourney.Commands;

// MOD-0162 FU05 ContentEngagementJourney write surface. TenantId is server-resolved (never in a payload). No
// delete/PATCH — closing is archive. Stages are managed only through the stage sub-commands (never through a `stages`
// array on Update — V-J16). S2: the stage commands mutate the SAME journey document and share the journey's optimistic
// Version token. Campaign / Brand / Product / Segment are deliberately absent (§2.1/S6) and no runtime-state field
// (current stage, progress, target) exists anywhere in this surface.

public sealed record CreateContentEngagementJourneyCommand(
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
    string? Source = null) : IRequest<Response<Guid>>;

/// <summary>Full replace of the journey's mutable fields. <c>JourneyCode</c> is immutable. A <c>stages</c> array is
/// rejected (V-J16). A published version is frozen: only EffectiveTo and JourneyStatus (inactive/archived) may change
/// (V-J13); switching to <c>published</c> via Update is rejected — publish is a separate endpoint (V-J12, SoD).</summary>
public sealed record UpdateContentEngagementJourneyCommand(
    Guid JourneyId,
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
    bool StagesProvided = false,
    int? ExpectedVersion = null) : IRequest<Response<bool>>;

/// <summary>Publish is its own endpoint and its own permission (SoD: author ≠ publisher). Freezes the stage set.</summary>
public sealed record PublishContentEngagementJourneyCommand(
    Guid JourneyId, int? ExpectedVersion = null) : IRequest<Response<bool>>;

/// <summary>Clones a published version into a new draft: JourneyVersion bumped, stages copied with NEW StageIds and the
/// internal references (FallbackStageId, BranchConditions[].TargetStageId) REMAPPED onto the clone's own ids,
/// SupersedesJourneyId set, no auto-publish, source version unchanged.</summary>
public sealed record CreateContentEngagementJourneyVersionCommand(
    Guid JourneyId, string? NewJourneyVersion = null) : IRequest<Response<Guid>>;

public sealed record ArchiveContentEngagementJourneyCommand(
    Guid JourneyId, int? ExpectedVersion = null) : IRequest<Response<bool>>;

// --------------- embedded stage sub-commands (mutate the journey document, share the journey Version token) ---------------

public sealed record AddContentEngagementJourneyStageCommand(
    Guid JourneyId,
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
    IReadOnlyList<ContentEngagementJourneyBranchConditionInput>? BranchConditions = null,
    int? ExpectedVersion = null) : IRequest<Response<Guid>>;

public sealed record UpdateContentEngagementJourneyStageCommand(
    Guid JourneyId,
    Guid StageId,
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
    IReadOnlyList<ContentEngagementJourneyBranchConditionInput>? BranchConditions = null,
    int? ExpectedVersion = null) : IRequest<Response<bool>>;

public sealed record ArchiveContentEngagementJourneyStageCommand(
    Guid JourneyId, Guid StageId, int? ExpectedVersion = null) : IRequest<Response<bool>>;
