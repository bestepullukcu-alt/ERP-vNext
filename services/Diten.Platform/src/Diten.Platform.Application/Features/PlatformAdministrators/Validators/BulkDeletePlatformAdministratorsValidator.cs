using Diten.Platform.Application.Features.PlatformAdministrators.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.PlatformAdministrators.Validators;

public sealed class BulkDeletePlatformAdministratorsValidator : AbstractValidator<BulkDeletePlatformAdministratorsCommand>
{
    public BulkDeletePlatformAdministratorsValidator()
    {
        RuleFor(x => x.Items)
            .NotEmpty();

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(x => x.Id).NotEqual(Guid.Empty);
            item.RuleFor(x => x.Version).GreaterThan(0);
        });
    }
}
