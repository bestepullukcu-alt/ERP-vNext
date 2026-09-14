using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.ExitReferenceRecords.Commands;

public sealed record ArchiveExitReferenceRecordCommand(Guid Id) : IRequest<Response<NoContent>>;
