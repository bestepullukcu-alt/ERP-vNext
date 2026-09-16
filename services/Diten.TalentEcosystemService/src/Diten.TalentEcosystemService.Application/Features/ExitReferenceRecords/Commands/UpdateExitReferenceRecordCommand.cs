using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.ExitReferenceRecords.Commands;

public sealed record UpdateExitReferenceRecordCommand(Guid Id, ExitReferenceRecordRequest Request) : IRequest<Response<NoContent>>;
