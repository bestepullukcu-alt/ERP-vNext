using Diten.Application.Common.Models;
using MediatR;

namespace Diten.Application.Commands.TaskCommands;

public class DeleteTaskCommand : IRequest<Response<bool>>
{
    public string Id { get; set; } = string.Empty;

    public DeleteTaskCommand(string id)
    {
        Id = id;
    }
}
