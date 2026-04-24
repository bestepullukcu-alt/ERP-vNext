using Diten.Shared.Core;
using MediatR;

namespace Diten.DevEnablementService.Application.Features.GoldenReferenceItems.Commands;

public sealed record CreateGoldenReferenceItemCommand(string Code, string Name, string? Description, string? ReferenceType, int Priority) : IRequest<Response<Guid>>;
