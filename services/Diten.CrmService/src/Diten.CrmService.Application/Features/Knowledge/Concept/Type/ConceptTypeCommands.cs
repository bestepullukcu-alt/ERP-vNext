using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Knowledge.Concept.Type;

/// <summary>MOD-0162 FU03 concept-type write surface. <c>TenantId</c> is server-resolved (never payload). No delete
/// command — closing a type is <see cref="ArchiveConceptTypeCommand"/> (soft lifecycle).</summary>
public sealed record CreateConceptTypeCommand(
    Guid SubjectId,
    string ConceptTypeCode,
    string ConceptTypeName,
    string? Description = null,
    int SortOrder = 0,
    string? Status = null) : IRequest<Response<Guid>>;

/// <summary>Full replace of the mutable fields. <c>ConceptTypeCode</c> and <c>SubjectId</c> are immutable (rename goes
/// through <c>ConceptTypeName</c>). An archived type cannot be updated.</summary>
public sealed record UpdateConceptTypeCommand(
    Guid ConceptTypeId,
    string ConceptTypeName,
    string? Description = null,
    int SortOrder = 0,
    string? Status = null) : IRequest<Response<bool>>;

public sealed record ArchiveConceptTypeCommand(Guid ConceptTypeId) : IRequest<Response<bool>>;
