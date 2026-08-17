using Diten.Platform.Application.Features.PayrollSources.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.PayrollSources.Validators;

public sealed class ArchivePayrollExternalSystemProfileValidator : AbstractValidator<ArchivePayrollExternalSystemProfileCommand>
{
    public ArchivePayrollExternalSystemProfileValidator() =>
        RuleFor(command => command.Id).NotEmpty();
}
