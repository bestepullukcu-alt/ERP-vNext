using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.TepShell.Queries;

public sealed record GetTepShellMetadataListQuery : IRequest<Response<IReadOnlyList<TepShellMetadataListItemDto>>>;
