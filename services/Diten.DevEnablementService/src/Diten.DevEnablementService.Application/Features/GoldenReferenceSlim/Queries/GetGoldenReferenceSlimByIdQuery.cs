using Diten.Shared.Core;
using MediatR;

namespace Diten.DevEnablementService.Application.Features.GoldenReferenceSlim.Queries;

public sealed record GetGoldenReferenceSlimByIdQuery(Guid Id) : IRequest<Response<GoldenReferenceSlimDetailDto>>;
