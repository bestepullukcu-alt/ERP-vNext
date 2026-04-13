using Diten.Application.Common.Models;
using MediatR;

namespace Diten.Application.Commands.TaskCommands;

public class UpdateTaskStatusCommand : IRequest<Response<bool>>
{
    public string Id { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
