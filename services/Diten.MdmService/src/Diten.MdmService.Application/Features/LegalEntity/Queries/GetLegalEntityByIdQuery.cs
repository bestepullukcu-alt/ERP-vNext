using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.LegalEntity.Queries;

public sealed record GetLegalEntityByIdQuery(Guid LegalEntityId) : IRequest<Response<LegalEntityDetailDto>>;
