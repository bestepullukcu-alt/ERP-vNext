using Diten.Platform.Application.Features.PayrollSources.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.PayrollSources.Validators;

public sealed class UpdatePayrollExternalSystemProfileValidator : PayrollExternalSystemProfileRequestValidator<UpdatePayrollExternalSystemProfileCommand>
{
    public UpdatePayrollExternalSystemProfileValidator()
        : base(command => command.Request)
    {
        RuleFor(command => command.Id).NotEmpty();
    }
}
