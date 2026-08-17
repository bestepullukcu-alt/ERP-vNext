using Diten.Shared.Core;
using MediatR;

namespace Diten.DevEnablementService.Application.Features.GoldenReferenceCompact.Commands;

public sealed record BulkDeleteGoldenReferenceCompactCommand(List<Guid> Ids) : IRequest<Response<int>>;
