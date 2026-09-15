using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.ExitReferenceRecords.Queries;

public sealed record GetExitReferenceRecordListQuery : IRequest<Response<IReadOnlyList<ExitReferenceRecordListItemDto>>>;
