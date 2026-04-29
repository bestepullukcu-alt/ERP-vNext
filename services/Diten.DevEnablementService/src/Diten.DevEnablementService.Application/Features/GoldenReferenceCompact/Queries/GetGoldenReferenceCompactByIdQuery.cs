using Diten.Shared.Core;
using MediatR;

namespace Diten.DevEnablementService.Application.Features.GoldenReferenceCompact.Queries;

public sealed record GetGoldenReferenceCompactByIdQuery(Guid Id) : IRequest<Response<GoldenReferenceCompactDetailDto>>;
