using Diten.Shared.Core;
using MediatR;

namespace Diten.DevEnablementService.Application.Features.GoldenReferenceItems.Queries;

public sealed record GetGoldenReferenceItemByIdQuery(Guid Id) : IRequest<Response<GoldenReferenceItemDetailDto>>;
