using Diten.Platform.Application.Features.Audit.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.Audit.Validators;

public sealed class RedactAuditActorValidator : AbstractValidator<RedactAuditActorCommand>
{
    public RedactAuditActorValidator()
    {
        RuleFor(x => x.Request.ActorId)
            .NotEmpty().WithMessage("Actor id is required.");

        RuleFor(x => x.Request.Reason)
            .NotEmpty().WithMessage("Redaction reason is required.")
            .MaximumLength(500).WithMessage("Redaction reason cannot exceed 500 characters.");
    }
}
