using Diten.Platform.Application.Features.HrisSources.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.HrisSources.Validators;

public sealed class ArchiveHrisSourceProfileValidator : AbstractValidator<ArchiveHrisSourceProfileCommand>
{
    public ArchiveHrisSourceProfileValidator() => RuleFor(x => x.Id).NotEmpty();
}
