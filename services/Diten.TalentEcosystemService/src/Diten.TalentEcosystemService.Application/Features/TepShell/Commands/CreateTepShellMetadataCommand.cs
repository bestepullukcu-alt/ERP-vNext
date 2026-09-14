using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.TepShell.Commands;

public sealed record CreateTepShellMetadataCommand(TepShellMetadataRequest Request) : IRequest<Response<Guid>>;
