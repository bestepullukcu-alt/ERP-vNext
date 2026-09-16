using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.WorkforceAnalytics.Queries;

public sealed record GetWorkforceAnalyticsReadinessByIdQuery(Guid Id) : IRequest<Response<WorkforceAnalyticsReadinessDto>>;
