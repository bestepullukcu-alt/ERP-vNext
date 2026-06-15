using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.TenantOrganization.Queries;

public sealed record GetPositionsQuery() : IRequest<Response<IReadOnlyList<PositionDto>>>;
