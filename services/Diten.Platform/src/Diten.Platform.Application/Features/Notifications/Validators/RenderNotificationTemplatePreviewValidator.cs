using Diten.Platform.Application.Features.Notifications.Queries;
using FluentValidation;

namespace Diten.Platform.Application.Features.Notifications.Validators;

public sealed class RenderNotificationTemplatePreviewValidator : AbstractValidator<RenderNotificationTemplatePreviewQuery>
{
    private const int MaxBodyLength = 100_000;

    public RenderNotificationTemplatePreviewValidator()
    {
        RuleFor(x => x.Request).NotNull();

        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request.SubjectTemplate)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .MaximumLength(300);

            RuleFor(x => x.Request)
                .Must(request => !string.IsNullOrWhiteSpace(request.BodyHtmlTemplate)
                                 || !string.IsNullOrWhiteSpace(request.BodyTextTemplate))
                .WithMessage("At least one of BodyHtmlTemplate or BodyTextTemplate is required.");

            RuleFor(x => x.Request.BodyHtmlTemplate).MaximumLength(MaxBodyLength);
            RuleFor(x => x.Request.BodyTextTemplate).MaximumLength(MaxBodyLength);

            RuleFor(x => x.Request.Variables).NotNull();
            RuleForEach(x => x.Request.Variables).ChildRules(variable =>
            {
                variable.RuleFor(v => v.Name)
                    .Cascade(CascadeMode.Stop)
                    .NotEmpty()
                    .Must(NotificationParsing.IsValidVariableName)
                    .WithMessage("Variable names must start with a letter and use letters, digits, dot or underscore.");
                variable.RuleFor(v => v.Type)
                    .Must(type => NotificationParsing.TryParseVariableType(type, out _))
                    .WithMessage("Unknown template variable type.");
            });

            RuleFor(x => x.Request.SampleVariables).NotNull();
        });
    }
}
