using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.IndustrySuccessionPool.Queries;

public sealed record GetIndustrySuccessionPoolReadinessByIdQuery(Guid Id) : IRequest<Response<IndustrySuccessionPoolReadinessDto>>;
