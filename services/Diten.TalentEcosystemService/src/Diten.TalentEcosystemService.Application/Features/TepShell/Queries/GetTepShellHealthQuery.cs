using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.TepShell.Queries;

public sealed record GetTepShellHealthQuery : IRequest<Response<TepShellHealthDto>>;
