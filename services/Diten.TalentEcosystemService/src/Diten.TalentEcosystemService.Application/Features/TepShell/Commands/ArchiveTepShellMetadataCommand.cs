using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.TepShell.Commands;

public sealed record ArchiveTepShellMetadataCommand(Guid Id) : IRequest<Response<NoContent>>;
