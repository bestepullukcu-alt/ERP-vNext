using Diten.Platform.Application.Features.PayrollSources.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.PayrollSources.Validators;

public sealed class RecordPayrollCycleReferenceValidator : PayrollCycleReferenceRequestValidator<RecordPayrollCycleReferenceCommand>
{
    public RecordPayrollCycleReferenceValidator()
        : base(command => command.Request)
    {
        RuleFor(command => command.SourceProfileId).NotEmpty();
    }
}
