using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.HrKpiAnalytics.Queries;

public sealed record GetHrKpiAnalyticsAuditMetadataQuery(Guid Id) : IRequest<Response<HrKpiAnalyticsAuditMetadataDto>>;
