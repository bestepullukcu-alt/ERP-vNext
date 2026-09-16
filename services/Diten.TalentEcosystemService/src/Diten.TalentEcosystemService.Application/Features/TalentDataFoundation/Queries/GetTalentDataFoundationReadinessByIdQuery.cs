using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.TalentDataFoundation.Queries;

public sealed record GetTalentDataFoundationReadinessByIdQuery(Guid Id) : IRequest<Response<TalentDataFoundationReadinessDto>>;
