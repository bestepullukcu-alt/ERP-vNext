using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.IndustryKnowledgeNetwork.Queries;

public sealed record GetIndustryKnowledgeNetworkReadinessListQuery : IRequest<Response<IReadOnlyList<IndustryKnowledgeNetworkReadinessListItemDto>>>;
