using Diten.Platform.Application.Features.TimeAttendanceProviders.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.TimeAttendanceProviders.Validators;

public sealed class UpdateTimeAttendanceProviderProfileValidator : TimeAttendanceProviderProfileRequestValidator<UpdateTimeAttendanceProviderProfileCommand>
{
    public UpdateTimeAttendanceProviderProfileValidator()
        : base(command => command.Request)
    {
        RuleFor(command => command.Id).NotEmpty();
    }
}
