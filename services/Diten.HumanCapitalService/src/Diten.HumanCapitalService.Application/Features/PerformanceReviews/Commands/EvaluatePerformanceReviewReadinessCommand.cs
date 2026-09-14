using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.PerformanceReviews.Commands;

public sealed record EvaluatePerformanceReviewReadinessCommand(Guid Id) : IRequest<Response<PerformanceReviewReadinessDto>>;
