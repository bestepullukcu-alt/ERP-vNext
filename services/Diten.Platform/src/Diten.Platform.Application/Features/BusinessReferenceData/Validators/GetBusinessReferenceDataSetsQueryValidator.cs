using Diten.Platform.Application.Features.BusinessReferenceData.Commands;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using FluentValidation;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Validators;

public sealed class GetBusinessReferenceDataSetsQueryValidator : AbstractValidator<GetBusinessReferenceDataSetsQuery>
{
    public GetBusinessReferenceDataSetsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
        RuleFor(x => x.Sort).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Search).MaximumLength(200);
        RuleFor(x => x.Status).MaximumLength(32);
        RuleFor(x => x.ScopeType).MaximumLength(64);
    }
}
