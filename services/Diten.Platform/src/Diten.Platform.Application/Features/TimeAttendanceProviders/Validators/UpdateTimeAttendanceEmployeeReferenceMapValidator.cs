using Diten.Platform.Application.Features.TimeAttendanceProviders.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.TimeAttendanceProviders.Validators;

public sealed class UpdateTimeAttendanceEmployeeReferenceMapValidator : TimeAttendanceEmployeeReferenceMapRequestValidator<UpdateTimeAttendanceEmployeeReferenceMapCommand>
{
    public UpdateTimeAttendanceEmployeeReferenceMapValidator()
        : base(command => command.Request)
    {
        RuleFor(command => command.ProviderProfileId).NotEmpty();
        RuleFor(command => command.MapId).NotEmpty();
    }
}
