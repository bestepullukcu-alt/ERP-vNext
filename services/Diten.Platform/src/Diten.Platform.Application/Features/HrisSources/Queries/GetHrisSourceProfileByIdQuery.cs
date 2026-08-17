using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.HrisSources.Queries;

public sealed record GetHrisSourceProfileByIdQuery(Guid Id) : IRequest<Response<HrisSourceProfileDto>>;
