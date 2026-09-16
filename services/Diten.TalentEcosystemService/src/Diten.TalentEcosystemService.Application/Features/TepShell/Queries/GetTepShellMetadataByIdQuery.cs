using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.TepShell.Queries;

public sealed record GetTepShellMetadataByIdQuery(Guid Id) : IRequest<Response<TepShellMetadataDto>>;
