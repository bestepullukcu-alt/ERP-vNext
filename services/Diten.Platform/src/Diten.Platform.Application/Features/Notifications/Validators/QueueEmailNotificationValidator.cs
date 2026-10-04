using Diten.Platform.Application.Features.Notifications.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.Notifications.Validators;

public sealed class QueueEmailNotificationValidator : AbstractValidator<QueueEmailNotificationCommand>
{
    public QueueEmailNotificationValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty().WithMessage("Target tenant id is required.");
        RuleFor(x => x.Request.TemplateKey)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(NotificationParsing.IsValidTemplateKey)
            .WithMessage("TemplateKey must use lowercase dotted format.");
        RuleFor(x => x.Request.Locale).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Request.Variables).NotNull();
        RuleFor(x => x.Request.To)
            .NotNull()
            .Must(x => x.Count > 0)
            .WithMessage("At least one To recipient is required.");
        RuleForEach(x => x.Request.To).SetValidator(new EmailRecipientDtoValidator());
        RuleForEach(x => x.Request.Cc!)
            .SetValidator(new EmailRecipientDtoValidator())
            .When(x => x.Request.Cc is not null);
        RuleForEach(x => x.Request.Bcc!)
            .SetValidator(new EmailRecipientDtoValidator())
            .When(x => x.Request.Bcc is not null);
    }

    /// <summary>
    /// BL-454 — is this the refusal of a recipient that is not ONE plain address? A caller INSIDE the process (an
    /// event consumer, the event-code adapter) gets the pipeline's <see cref="ValidationException"/>, not a response
    /// with a reason code; this is how it recognises the one refusal no retry can ever fix.
    /// </summary>
    public static bool IsRecipientRefusal(Exception exception) =>
        exception is ValidationException validation
        && validation.Errors.Any(error => error.ErrorCode
            == Diten.Platform.Application.Features.Notifications.Handlers.CommandHandlers.QueueEmailNotificationHandler.ReasonRecipientInvalid);
}

public sealed class EmailRecipientDtoValidator : AbstractValidator<EmailRecipientDto>
{
    private const string RecipientInvalid =
        Diten.Platform.Application.Features.Notifications.Handlers.CommandHandlers.QueueEmailNotificationHandler.ReasonRecipientInvalid;

    public EmailRecipientDtoValidator()
    {
        // BL-454 — ONE rule for an address (EmailAddressText.IsSingleAddress), refused with the curated code
        // RECIPIENT_INVALID: GlobalExceptionHandler carries a curated ErrorCode through verbatim as reason_code. The
        // FluentValidation EmailAddress() this replaces accepted a value with a line break, and refused others with a
        // code-less failure. This is the ONE place the rule is applied: every caller reaches the handler through the
        // MediatR pipeline, and in-process callers recognise the refusal by IsRecipientRefusal.
        // Every rule on the address carries the SAME code: an empty or over-long address is as permanently invalid as a
        // second mailbox, and an in-process caller must recognise all three (IsRecipientRefusal) or it redelivers forever.
        RuleFor(x => x.Email)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithErrorCode(RecipientInvalid)
            .WithMessage("A recipient address is not a single valid address.")
            .MaximumLength(256)
            .WithErrorCode(RecipientInvalid)
            .WithMessage("A recipient address is not a single valid address.")
            .Must(email => Diten.BuildingBlocks.Email.EmailAddressText.IsSingleAddress(email?.Trim()))
            .WithErrorCode(RecipientInvalid)
            .WithMessage("A recipient address is not a single valid address.");
        RuleFor(x => x.DisplayName).MaximumLength(160);
    }
}

public sealed class MarkNotificationDispatchSentValidator : AbstractValidator<MarkNotificationDispatchSentCommand>
{
    public MarkNotificationDispatchSentValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.DispatchId).NotEmpty();
        RuleFor(x => x.ProviderMessageId).MaximumLength(256);
    }
}

public sealed class MarkNotificationDispatchFailedValidator : AbstractValidator<MarkNotificationDispatchFailedCommand>
{
    public MarkNotificationDispatchFailedValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.DispatchId).NotEmpty();
        RuleFor(x => x.ErrorCode).NotEmpty().MaximumLength(128);
        RuleFor(x => x.ErrorMessage).NotEmpty().MaximumLength(2000)
            .Must(value => !NotificationParsing.LooksLikeRawSecret(value))
            .WithMessage("ErrorMessage must be redacted.");
    }
}

public sealed class CancelNotificationDispatchValidator : AbstractValidator<CancelNotificationDispatchCommand>
{
    public CancelNotificationDispatchValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.DispatchId).NotEmpty();
    }
}
