using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.IndustryKnowledgeNetwork.Queries;

public sealed record GetIndustryKnowledgeNetworkReadinessByIdQuery(Guid Id) : IRequest<Response<IndustryKnowledgeNetworkReadinessDto>>;
