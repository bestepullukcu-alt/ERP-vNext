using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.CandidateProfiles.Queries;

public sealed record GetCandidateProfileAuditMetadataQuery(Guid Id) : IRequest<Response<CandidateProfileAuditMetadataDto>>;
