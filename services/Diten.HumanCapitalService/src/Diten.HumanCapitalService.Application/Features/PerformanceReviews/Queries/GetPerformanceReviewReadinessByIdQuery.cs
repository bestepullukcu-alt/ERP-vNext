using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.PerformanceReviews.Queries;

public sealed record GetPerformanceReviewReadinessByIdQuery(Guid Id) : IRequest<Response<PerformanceReviewReadinessDto>>;
