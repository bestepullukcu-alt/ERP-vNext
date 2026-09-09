using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.LegalEntity.Queries;

/// <summary>Returns { self + all descendant } legal-entity ids for roll-up scoping.</summary>
public sealed record GetLegalEntityDescendantsQuery(Guid LegalEntityId) : IRequest<Response<LegalEntityDescendantsDto>>;
