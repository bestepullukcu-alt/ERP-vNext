using Diten.Shared.Core;
using MediatR;

namespace Diten.DevEnablementService.Application.Features.GoldenReferenceSlim.Commands;

public sealed record BulkDeleteGoldenReferenceSlimCommand(List<Guid> Ids) : IRequest<Response<int>>;
