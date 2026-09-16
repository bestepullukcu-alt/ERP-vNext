using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.PerformanceReviews.Commands;

public sealed record CreatePerformanceReviewReadinessCommand(PerformanceReviewReadinessCreateRequest Request) : IRequest<Response<Guid>>;
