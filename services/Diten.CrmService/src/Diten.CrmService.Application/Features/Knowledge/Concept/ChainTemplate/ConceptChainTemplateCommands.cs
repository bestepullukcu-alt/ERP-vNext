using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Knowledge.Concept.ChainTemplate;

/// <summary>MOD-0162 FU03 chain-template write surface. <c>TenantId</c> server-resolved. <c>OrderedConceptTypes</c> is a
/// sequence of at least two same-subject type ids with no repeat (v1). Two published versions of one <c>ChainCode</c>
/// may not overlap in effective window (409).</summary>
public sealed record CreateConceptChainTemplateCommand(
    Guid SubjectId,
    string ChainCode,
    string ChainName,
    IReadOnlyList<Guid> OrderedConceptTypes,
    DateTimeOffset EffectiveFrom,
    string? Description = null,
    string? Status = null,
    string? ChainVersion = null,
    DateTimeOffset? EffectiveTo = null) : IRequest<Response<Guid>>;

/// <summary>Full replace of the mutable fields. <c>ChainCode</c> and <c>SubjectId</c> are immutable. A published version
/// freezes <c>OrderedConceptTypes</c> — changing the sequence on a published template is rejected (make a new version).</summary>
public sealed record UpdateConceptChainTemplateCommand(
    Guid ConceptChainTemplateId,
    string ChainName,
    IReadOnlyList<Guid> OrderedConceptTypes,
    DateTimeOffset EffectiveFrom,
    string? Description = null,
    string? Status = null,
    string? ChainVersion = null,
    DateTimeOffset? EffectiveTo = null) : IRequest<Response<bool>>;

public sealed record ArchiveConceptChainTemplateCommand(Guid ConceptChainTemplateId) : IRequest<Response<bool>>;
