using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.TalentDataFoundation.Queries;

public sealed record GetTalentDataFoundationReadinessListQuery : IRequest<Response<IReadOnlyList<TalentDataFoundationReadinessListItemDto>>>;
