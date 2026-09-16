using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.IndustryTalentPool.Queries;

public sealed record GetIndustryTalentPoolReadinessListQuery : IRequest<Response<IReadOnlyList<IndustryTalentPoolReadinessListItemDto>>>;
