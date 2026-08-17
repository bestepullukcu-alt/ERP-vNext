using Diten.Platform.Application.Features.PayrollSources.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.PayrollSources.Validators;

public sealed class UpdatePayrollContractProfileValidator : PayrollContractProfileRequestValidator<UpdatePayrollContractProfileCommand>
{
    public UpdatePayrollContractProfileValidator()
        : base(command => command.Request)
    {
        RuleFor(command => command.SourceProfileId).NotEmpty();
    }
}
