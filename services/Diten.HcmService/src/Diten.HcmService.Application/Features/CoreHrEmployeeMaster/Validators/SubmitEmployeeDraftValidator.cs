using Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Commands;
using FluentValidation;

namespace Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Validators;

public sealed class SubmitEmployeeDraftValidator : AbstractValidator<SubmitEmployeeDraftCommand>
{
    public SubmitEmployeeDraftValidator()
    {
        RuleFor(x => x.DraftSessionId)
            .NotEmpty();

        RuleFor(x => x.IfMatch)
            .NotEmpty()
            .WithMessage("If-Match header is required.");

        RuleFor(x => x.Request.IdempotencyKey)
            .NotEmpty()
            .MaximumLength(200);
    }
}
