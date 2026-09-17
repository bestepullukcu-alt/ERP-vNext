using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.PerformanceReviews.Commands;

public sealed record DeletePerformanceReviewReadinessCommand(Guid Id) : IRequest<Response<bool>>;
