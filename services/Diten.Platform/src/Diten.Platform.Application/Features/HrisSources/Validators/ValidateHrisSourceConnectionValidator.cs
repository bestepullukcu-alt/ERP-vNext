using Diten.Platform.Application.Features.HrisSources.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.HrisSources.Validators;

public sealed class ValidateHrisSourceConnectionValidator : AbstractValidator<ValidateHrisSourceConnectionCommand>
{
    public ValidateHrisSourceConnectionValidator() => RuleFor(x => x.Id).NotEmpty();
}
