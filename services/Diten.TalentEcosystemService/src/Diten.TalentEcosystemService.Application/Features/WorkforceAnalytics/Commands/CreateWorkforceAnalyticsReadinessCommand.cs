using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.WorkforceAnalytics.Commands;

public sealed record CreateWorkforceAnalyticsReadinessCommand(WorkforceAnalyticsReadinessCreateRequest Request) : IRequest<Response<Guid>>;
