using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.WorkforceAnalytics.Queries;

public sealed record GetWorkforceAnalyticsAuditMetadataQuery(Guid Id) : IRequest<Response<WorkforceAnalyticsAuditMetadataDto>>;
