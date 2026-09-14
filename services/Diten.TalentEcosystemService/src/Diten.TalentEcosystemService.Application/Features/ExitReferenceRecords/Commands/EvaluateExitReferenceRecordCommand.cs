using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.ExitReferenceRecords.Commands;

public sealed record EvaluateExitReferenceRecordCommand(Guid Id, EvaluateExitReferenceRecordRequest Request)
    : IRequest<Response<ExitReferenceEvaluationDto>>;
