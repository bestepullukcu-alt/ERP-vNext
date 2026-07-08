using Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Commands;
using FluentValidation;

namespace Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Validators;

public sealed class CreateEmployeeDraftValidator : AbstractValidator<CreateEmployeeDraftCommand>
{
    public CreateEmployeeDraftValidator()
    {
        RuleFor(x => x.Request.IdempotencyKey)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Request.SourceContext)
            .MaximumLength(120);

        RuleFor(x => x.Request.ClientReference)
            .MaximumLength(120);
    }
}
