using MediatR;
using Diten.Application.Common.Models;
using FluentValidation;

namespace Diten.Application.Commands.TaskCommands;

public class CreateTaskCommand : IRequest<Response<string>>
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
}

public class CreateTaskCommandValidator : AbstractValidator<CreateTaskCommand>
{
    public CreateTaskCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Title is required.");
    }
}
