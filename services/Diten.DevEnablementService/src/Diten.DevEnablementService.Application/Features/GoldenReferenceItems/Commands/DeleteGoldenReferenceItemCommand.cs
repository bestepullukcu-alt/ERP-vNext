using MediatR;
using Diten.Shared.Core;

namespace Diten.DevEnablementService.Application.Features.GoldenReferenceItems.Commands;

public sealed record DeleteGoldenReferenceItemCommand(Guid Id) : IRequest<Response<bool>>;
