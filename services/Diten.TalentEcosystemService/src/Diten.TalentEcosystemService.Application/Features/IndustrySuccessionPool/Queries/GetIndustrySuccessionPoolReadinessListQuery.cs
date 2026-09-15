using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.IndustrySuccessionPool.Queries;

public sealed record GetIndustrySuccessionPoolReadinessListQuery : IRequest<Response<IReadOnlyList<IndustrySuccessionPoolReadinessListItemDto>>>;
