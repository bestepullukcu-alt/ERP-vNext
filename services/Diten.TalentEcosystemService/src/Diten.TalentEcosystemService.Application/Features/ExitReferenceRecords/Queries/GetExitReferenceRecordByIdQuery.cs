using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.ExitReferenceRecords.Queries;

public sealed record GetExitReferenceRecordByIdQuery(Guid Id) : IRequest<Response<ExitReferenceRecordDto>>;
