using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.CandidateProfiles.Queries;

public sealed record GetCandidateProfileByIdQuery(Guid Id) : IRequest<Response<CandidateProfileDto>>;
