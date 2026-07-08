using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.TenantOrganization.Queries;

public sealed record GetPersonReferenceByIdQuery(Guid PersonId) : IRequest<Response<PersonReferenceDto>>;
