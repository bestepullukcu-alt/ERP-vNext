using Diten.Platform.Application.Features.PayrollSources.Commands;

namespace Diten.Platform.Application.Features.PayrollSources.Validators;

public sealed class CreatePayrollExternalSystemProfileValidator : PayrollExternalSystemProfileRequestValidator<CreatePayrollExternalSystemProfileCommand>
{
    public CreatePayrollExternalSystemProfileValidator()
        : base(command => command.Request)
    {
    }
}
