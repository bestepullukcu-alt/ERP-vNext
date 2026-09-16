using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.IndustryKnowledgeNetwork.Commands;

public sealed record EvaluateIndustryKnowledgeNetworkReadinessCommand(Guid Id) : IRequest<Response<IndustryKnowledgeNetworkReadinessDto>>;
