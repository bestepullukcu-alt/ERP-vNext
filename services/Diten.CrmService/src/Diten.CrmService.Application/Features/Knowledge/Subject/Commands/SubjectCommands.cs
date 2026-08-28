using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Knowledge.Subject.Commands;

/// <summary>MOD-0162 FU02 subject write surface. <c>TenantId</c> is server-resolved (never payload). No delete command —
/// closing a subject is <see cref="ArchiveSubjectCommand"/> (soft lifecycle).</summary>
public sealed record CreateSubjectCommand(
    string SubjectCode,
    string SubjectName,
    DateTimeOffset EffectiveFrom,
    Guid? ParentSubjectId = null,
    string? Description = null,
    string? Status = null,
    int SortOrder = 0,
    DateTimeOffset? EffectiveTo = null,
    IReadOnlyList<string>? Alias = null,
    IReadOnlyList<KnowledgeExternalReferenceInput>? ExternalReferences = null) : IRequest<Response<Guid>>;

/// <summary>Full replace of the mutable fields. <c>SubjectCode</c> is immutable (rename goes through SubjectName /
/// Alias). <c>ParentSubjectId</c> is re-assignable, under the same self/archived/cycle guards as create. An archived
/// subject cannot be updated.</summary>
public sealed record UpdateSubjectCommand(
    Guid SubjectId,
    string SubjectName,
    DateTimeOffset EffectiveFrom,
    Guid? ParentSubjectId = null,
    string? Description = null,
    string? Status = null,
    int SortOrder = 0,
    DateTimeOffset? EffectiveTo = null,
    IReadOnlyList<string>? Alias = null,
    IReadOnlyList<KnowledgeExternalReferenceInput>? ExternalReferences = null) : IRequest<Response<bool>>;

public sealed record ArchiveSubjectCommand(Guid SubjectId) : IRequest<Response<bool>>;

/// <summary>Reverses <see cref="ArchiveSubjectCommand"/>. The subject comes back as <c>inactive</c>, never straight to
/// <c>active</c>: re-activation stays a separate, deliberate step. An archived <c>SubjectCode</c> is reusable, so this
/// fails with 409 when another non-archived subject has taken the code in the meantime.</summary>
public sealed record UnarchiveSubjectCommand(Guid SubjectId) : IRequest<Response<bool>>;
