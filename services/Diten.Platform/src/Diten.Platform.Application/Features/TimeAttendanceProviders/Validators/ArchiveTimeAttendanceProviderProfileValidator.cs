using Diten.Platform.Application.Features.TimeAttendanceProviders.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.TimeAttendanceProviders.Validators;

public sealed class ArchiveTimeAttendanceProviderProfileValidator : AbstractValidator<ArchiveTimeAttendanceProviderProfileCommand>
{
    public ArchiveTimeAttendanceProviderProfileValidator() =>
        RuleFor(command => command.Id).NotEmpty();
}
