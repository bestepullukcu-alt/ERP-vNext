using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.LegalEntity.Queries;

public sealed record ValidateLegalEntityReferenceQuery(Guid LegalEntityId) : IRequest<Response<LegalEntityLookupDto>>;
