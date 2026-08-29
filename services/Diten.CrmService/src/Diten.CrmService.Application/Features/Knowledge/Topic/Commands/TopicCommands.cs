using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Knowledge.Topic.Commands;

/// <summary>MOD-0162 FU02 topic write surface (subject-scoped, hierarchical). <c>TenantId</c> is server-resolved.
/// <c>SubjectId</c> is required and immutable on a topic. No delete command — closing a topic is
/// <see cref="ArchiveTopicCommand"/>.</summary>
public sealed record CreateTopicCommand(
    Guid SubjectId,
    string TopicCode,
    string TopicName,
    DateTimeOffset EffectiveFrom,
    Guid? ParentTopicId = null,
    string? Description = null,
    string? Status = null,
    int SortOrder = 0,
    DateTimeOffset? EffectiveTo = null,
    IReadOnlyList<string>? Alias = null,
    IReadOnlyList<KnowledgeExternalReferenceInput>? ExternalReferences = null) : IRequest<Response<Guid>>;

/// <summary>Full replace of the mutable fields. <c>TopicCode</c> and <c>SubjectId</c> are immutable. An archived topic
/// cannot be updated. A parent change is re-validated for cross-subject / self / cycle.</summary>
public sealed record UpdateTopicCommand(
    Guid TopicId,
    string TopicName,
    DateTimeOffset EffectiveFrom,
    Guid? ParentTopicId = null,
    string? Description = null,
    string? Status = null,
    int SortOrder = 0,
    DateTimeOffset? EffectiveTo = null,
    IReadOnlyList<string>? Alias = null,
    IReadOnlyList<KnowledgeExternalReferenceInput>? ExternalReferences = null) : IRequest<Response<bool>>;

public sealed record ArchiveTopicCommand(Guid TopicId) : IRequest<Response<bool>>;

/// <summary>Reverses <see cref="ArchiveTopicCommand"/>. The topic comes back as <c>inactive</c>. It fails with 409 when
/// restoring would break an invariant the create path enforces: the owning subject is archived, the parent topic is
/// archived, or another non-archived topic in the same subject has taken the code.</summary>
public sealed record UnarchiveTopicCommand(Guid TopicId) : IRequest<Response<bool>>;
