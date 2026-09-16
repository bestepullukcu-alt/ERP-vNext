using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.PerformanceReviews.Queries;

public sealed record GetPerformanceReviewAuditMetadataQuery(Guid Id) : IRequest<Response<PerformanceReviewAuditMetadataDto>>;
