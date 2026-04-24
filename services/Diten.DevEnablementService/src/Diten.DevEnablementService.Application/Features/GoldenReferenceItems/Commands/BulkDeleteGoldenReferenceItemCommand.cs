using Diten.Shared.Core;
using MediatR;

namespace Diten.DevEnablementService.Application.Features.GoldenReferenceItems.Commands;

public sealed record BulkDeleteGoldenReferenceItemCommand(List<Guid> Ids) : IRequest<Response<int>>;
