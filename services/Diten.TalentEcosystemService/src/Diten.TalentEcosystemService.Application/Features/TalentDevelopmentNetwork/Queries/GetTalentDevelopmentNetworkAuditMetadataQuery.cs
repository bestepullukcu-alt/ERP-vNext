using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.TalentDevelopmentNetwork.Queries;

public sealed record GetTalentDevelopmentNetworkAuditMetadataQuery(Guid Id) : IRequest<Response<TalentDevelopmentNetworkAuditMetadataDto>>;
