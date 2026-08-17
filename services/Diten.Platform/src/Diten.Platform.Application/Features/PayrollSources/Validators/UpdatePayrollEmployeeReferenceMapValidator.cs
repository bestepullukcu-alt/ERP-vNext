using Diten.Platform.Application.Features.PayrollSources.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.PayrollSources.Validators;

public sealed class UpdatePayrollEmployeeReferenceMapValidator : PayrollEmployeeReferenceMapRequestValidator<UpdatePayrollEmployeeReferenceMapCommand>
{
    public UpdatePayrollEmployeeReferenceMapValidator()
        : base(command => command.Request)
    {
        RuleFor(command => command.SourceProfileId).NotEmpty();
        RuleFor(command => command.MapId).NotEmpty();
    }
}
