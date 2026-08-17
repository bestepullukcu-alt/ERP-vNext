using MediatR;
using Diten.Shared.Core;

namespace Diten.DevEnablementService.Application.Features.GoldenReferenceCompact.Commands;

public sealed record DeleteGoldenReferenceCompactCommand(Guid Id) : IRequest<Response<bool>>;
