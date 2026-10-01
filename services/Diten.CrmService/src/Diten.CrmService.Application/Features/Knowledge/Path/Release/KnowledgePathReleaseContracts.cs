using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.ContentComposition.ContentSetRevisions.Rendering;
using MediatR;

namespace Diten.CrmService.Application.Features.Knowledge.Path.Release;

// WP-KP-3 — render / release / withdrawal / usage of a knowledge path revision. Tenant and actor are server-resolved.

/// <summary>Renders the APPROVED revision's archive PDF and stores it (idempotent: an existing pdf is returned).</summary>
public sealed record RenderKnowledgePathRevisionCommand(Guid PathId, Guid RevisionId) : IRequest<Response<KnowledgePathArtifactDto>>;

/// <summary>Streams a rendered artifact; the content id is resolved from the revision, never taken from the client.</summary>
public sealed record GetKnowledgePathRevisionArtifactQuery(Guid PathId, Guid RevisionId, string? Kind)
    : IRequest<Response<ContentArtifactReadResult>>;

/// <summary>Releases the approved + rendered revision: the path is published (fail-closed gate, person SoD).</summary>
public sealed record ReleaseKnowledgePathRevisionCommand(Guid PathId, Guid RevisionId) : IRequest<Response<KnowledgePathReleaseDto>>;

/// <summary>Withdraws a released revision (reason required); refused while a published journey stage uses the path.</summary>
public sealed record WithdrawKnowledgePathReleaseCommand(Guid PathId, Guid RevisionId, string? Reason)
    : IRequest<Response<KnowledgePathReleaseDto>>;

public sealed record GetKnowledgePathUsageQuery(Guid PathId) : IRequest<Response<KnowledgePathUsageDto>>;

public sealed record KnowledgePathArtifactDto(
    string Kind, Guid ContentId, string? Checksum, long ByteSize, string? MediaType, string? FileName,
    DateTimeOffset RenderedAt, string? RenderedBy);

/// <summary>A journey stage that uses a path (by name — what the author acts on).</summary>
public sealed record KnowledgePathStageRefDto(Guid JourneyId, string JourneyCode, string JourneyName, string StageCode, string StageName);

public sealed record KnowledgePathReleaseDto(
    Guid RevisionId,
    Guid PathId,
    string State,
    DateTimeOffset At,
    string? By,
    string? Reason,
    string PathStatus,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<KnowledgePathStageRefDto> PreviousPathInUse);

public sealed record KnowledgePathJourneyUsageDto(
    Guid JourneyId, string Code, string Name, string Status, string StageCode, string StageName, string PinPolicy,
    string? PinnedVersion);

public sealed record KnowledgePathStrategyUsageDto(Guid TemplateId, string Code, string Name, string Status);

public sealed record KnowledgePathUsageDto(
    IReadOnlyList<KnowledgePathJourneyUsageDto> Journeys, IReadOnlyList<KnowledgePathStrategyUsageDto> StrategyTemplates);
