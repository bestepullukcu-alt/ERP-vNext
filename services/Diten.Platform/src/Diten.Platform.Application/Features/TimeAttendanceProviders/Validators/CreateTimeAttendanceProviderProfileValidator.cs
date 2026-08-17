using Diten.Platform.Application.Features.TimeAttendanceProviders.Commands;

namespace Diten.Platform.Application.Features.TimeAttendanceProviders.Validators;

public sealed class CreateTimeAttendanceProviderProfileValidator : TimeAttendanceProviderProfileRequestValidator<CreateTimeAttendanceProviderProfileCommand>
{
    public CreateTimeAttendanceProviderProfileValidator()
        : base(command => command.Request)
    {
    }
}
