using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.CandidateCareerPassport.Queries;

public sealed record GetCandidateCareerPassportReadinessByIdQuery(Guid Id) : IRequest<Response<CandidateCareerPassportReadinessDto>>;
