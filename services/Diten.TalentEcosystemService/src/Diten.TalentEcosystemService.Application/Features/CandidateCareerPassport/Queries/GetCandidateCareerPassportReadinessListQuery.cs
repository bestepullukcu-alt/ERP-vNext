using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.CandidateCareerPassport.Queries;

public sealed record GetCandidateCareerPassportReadinessListQuery : IRequest<Response<IReadOnlyList<CandidateCareerPassportReadinessListItemDto>>>;
