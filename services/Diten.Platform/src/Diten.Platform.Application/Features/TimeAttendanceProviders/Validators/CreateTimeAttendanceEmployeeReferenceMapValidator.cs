using Diten.Platform.Application.Features.TimeAttendanceProviders.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.TimeAttendanceProviders.Validators;

public sealed class CreateTimeAttendanceEmployeeReferenceMapValidator : TimeAttendanceEmployeeReferenceMapRequestValidator<CreateTimeAttendanceEmployeeReferenceMapCommand>
{
    public CreateTimeAttendanceEmployeeReferenceMapValidator()
        : base(command => command.Request)
    {
        RuleFor(command => command.ProviderProfileId).NotEmpty();
    }
}
