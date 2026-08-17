using Diten.Platform.Application.Features.SavedViews.Queries;
using FluentValidation;

namespace Diten.Platform.Application.Features.SavedViews.Validators;

public sealed class GetSavedViewsQueryValidator : AbstractValidator<GetSavedViewsQuery>
{
    public GetSavedViewsQueryValidator()
    {
        RuleFor(x => x.ModuleKey).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PageKey).NotEmpty().MaximumLength(100);
    }
}
