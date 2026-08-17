using Diten.Platform.Application.Features.PayrollSources.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.PayrollSources.Validators;

public sealed class CreatePayrollEmployeeReferenceMapValidator : PayrollEmployeeReferenceMapRequestValidator<CreatePayrollEmployeeReferenceMapCommand>
{
    public CreatePayrollEmployeeReferenceMapValidator()
        : base(command => command.Request)
    {
        RuleFor(command => command.SourceProfileId).NotEmpty();
    }
}
