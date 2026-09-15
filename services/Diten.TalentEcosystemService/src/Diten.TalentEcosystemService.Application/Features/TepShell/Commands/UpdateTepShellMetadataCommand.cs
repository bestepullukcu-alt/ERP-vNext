using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.TepShell.Commands;

public sealed record UpdateTepShellMetadataCommand(Guid Id, TepShellMetadataRequest Request) : IRequest<Response<TepShellMetadataDto>>;
