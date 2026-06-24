using Diten.Platform.Application.Features.Workflow.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.Workflow.Validators;

public sealed class PublishWorkflowDefinitionValidator : AbstractValidator<PublishWorkflowDefinitionCommand>
{
    public PublishWorkflowDefinitionValidator()
    {
        RuleFor(x => x.TemplateId)
            .NotEmpty();

        RuleFor(x => x.Request.DefinitionJson)
            .NotEmpty()
            .MaximumLength(250_000);

        RuleFor(x => x.Request.SchemaVersion)
            .NotEmpty()
            .MaximumLength(32);

        RuleFor(x => x.Request.ExpressionVersion)
            .NotEmpty()
            .MaximumLength(32);

        RuleFor(x => x.Request.ExpectedTemplateVersion)
            .GreaterThan(0)
            .When(x => x.Request.ExpectedTemplateVersion.HasValue);

        RuleFor(x => x.Request.ExpectedRowVersion)
            .MaximumLength(128)
            .When(x => x.Request.ExpectedRowVersion is not null);

        RuleFor(x => x.Request.PublishReason)
            .MaximumLength(500)
            .When(x => x.Request.PublishReason is not null);
    }
}
