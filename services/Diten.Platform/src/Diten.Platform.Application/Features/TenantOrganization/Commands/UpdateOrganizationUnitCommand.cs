using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.TenantOrganization.Commands;

public sealed record UpdateOrganizationUnitCommand(Guid Id, OrganizationUnitRequest Request) : IRequest<Response<NoContent>>;
