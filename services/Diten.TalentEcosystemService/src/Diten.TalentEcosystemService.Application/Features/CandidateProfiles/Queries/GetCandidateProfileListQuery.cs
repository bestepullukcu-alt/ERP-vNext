using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.CandidateProfiles.Queries;

public sealed record GetCandidateProfileListQuery : IRequest<Response<IReadOnlyList<CandidateProfileListItemDto>>>;
