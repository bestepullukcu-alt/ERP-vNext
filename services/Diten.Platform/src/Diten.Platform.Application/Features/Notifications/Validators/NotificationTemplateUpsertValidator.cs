using Diten.Platform.Application.Features.Notifications.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.Notifications.Validators;

public sealed class CreateNotificationTemplateValidator : AbstractValidator<CreateNotificationTemplateCommand>
{
    public CreateNotificationTemplateValidator()
    {
        Include(new NotificationTemplateUpsertRequestValidator<CreateNotificationTemplateCommand>(x => x.Request, x => x.TenantId));
    }
}

public sealed class UpdateNotificationTemplateValidator : AbstractValidator<UpdateNotificationTemplateCommand>
{
    public UpdateNotificationTemplateValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        Include(new NotificationTemplateUpsertRequestValidator<UpdateNotificationTemplateCommand>(x => x.Request, x => x.TenantId));
    }
}

public sealed class NotificationTemplateUpsertRequestValidator<T> : AbstractValidator<T>
{
    public NotificationTemplateUpsertRequestValidator(
        Func<T, NotificationTemplateUpsertRequest> request,
        Func<T, Guid?> tenantId)
    {
        RuleFor(x => request(x).TemplateKey)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(160)
            .Must(NotificationParsing.IsValidTemplateKey)
            .WithMessage("TemplateKey must use lowercase dotted format.");

        RuleFor(x => request(x).Locale)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(32);

        RuleFor(x => request(x).Channel)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(value => NotificationParsing.TryParseChannel(value, out _))
            .WithMessage("Channel must be Email.");

        RuleFor(x => request(x).Status)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(value => NotificationParsing.TryParseTemplateStatus(value, out _))
            .WithMessage("Status must be Draft, Active, or Archived.");

        RuleFor(x => request(x).SubjectTemplate)
            .NotEmpty()
            .MaximumLength(300);

        RuleFor(x => request(x).BodyHtmlTemplate)
            .NotEmpty()
            .When(x => string.IsNullOrWhiteSpace(request(x).BodyTextTemplate))
            .WithMessage("BodyHtmlTemplate is required when BodyTextTemplate is empty.");

        RuleFor(x => request(x).BodyHtmlTemplate)
            .MaximumLength(100000);

        RuleFor(x => request(x).BodyTextTemplate)
            .MaximumLength(100000);

        RuleFor(x => request(x).Variables)
            .NotNull();

        RuleForEach(x => request(x).Variables)
            .ChildRules(variable =>
            {
                variable.RuleFor(x => x.Name)
                    .Cascade(CascadeMode.Stop)
                    .NotEmpty()
                    .Must(NotificationParsing.IsValidVariableName)
                    .WithMessage("Variable name must be alphanumeric and may include dot or underscore.");

                variable.RuleFor(x => x.Type)
                    .Cascade(CascadeMode.Stop)
                    .NotEmpty()
                    .Must(value => NotificationParsing.TryParseVariableType(value, out _))
                    .WithMessage("Variable type must be String, Number, Boolean, Date, or Url.");
            });

        RuleFor(x => request(x).Variables)
            .Must(values => values.Select(v => v.Name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() == values.Count)
            .When(x => request(x).Variables is not null)
            .WithMessage("Template variable names must be unique.");

        RuleFor(x => tenantId(x))
            .NotNull()
            .When(x => !request(x).IsPlatformDefault)
            .WithMessage("Tenant route id is required for tenant-specific templates.");

        RuleFor(x => request(x).IsPlatformDefault)
            .Equal(true)
            .When(x => tenantId(x) is null)
            .WithMessage("Templates without a tenant route must be platform/global defaults.");
    }
}
