using Diten.Platform.Application.Features.Workflow.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.Workflow.Validators;

public sealed class CreateWorkflowDefinitionValidator : AbstractValidator<CreateWorkflowDefinitionCommand>
{
    public CreateWorkflowDefinitionValidator()
    {
        RuleFor(x => x.Request.TemplateCode)
            .NotEmpty()
            .MaximumLength(64)
            .Matches("^[A-Za-z0-9._-]+$")
            .WithMessage("TemplateCode may contain only letters, numbers, dots, underscores, and hyphens.");

        RuleFor(x => x.Request.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Request.Description)
            .MaximumLength(1000)
            .When(x => x.Request.Description is not null);
    }
}
