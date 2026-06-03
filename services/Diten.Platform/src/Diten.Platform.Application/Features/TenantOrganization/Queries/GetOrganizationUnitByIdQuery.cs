using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.TenantOrganization.Queries;

public sealed record GetOrganizationUnitByIdQuery(Guid Id) : IRequest<Response<OrganizationUnitDto>>;
