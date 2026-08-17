using MediatR;
using Diten.Shared.Core;

namespace Diten.DevEnablementService.Application.Features.GoldenReferenceSlim.Commands;

public sealed record DeleteGoldenReferenceSlimCommand(Guid Id) : IRequest<Response<bool>>;
