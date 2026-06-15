using Diten.Platform.Application.Features.BusinessReferenceData.Commands;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using FluentValidation;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Validators;

public sealed class ReplaceBusinessReferenceDataVersionValuesCommandValidator : AbstractValidator<ReplaceBusinessReferenceDataVersionValuesCommand>
{
    public ReplaceBusinessReferenceDataVersionValuesCommandValidator()
    {
        RuleFor(x => x.VersionId).NotEmpty();
        RuleFor(x => x.ActorId).NotEmpty().MaximumLength(128);
        RuleFor(x => x.CorrelationId).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Values).NotEmpty().WithMessage("values_required");
        RuleForEach(x => x.Values).ChildRules(value =>
        {
            value.RuleFor(v => v.Code).NotEmpty().WithMessage("value_code_required").MaximumLength(128);
            value.RuleFor(v => v.Label).NotEmpty().WithMessage("value_label_required").MaximumLength(256);
            value.RuleFor(v => v.Description).MaximumLength(2000);
            value.RuleFor(v => v.SortOrder).GreaterThanOrEqualTo(0);
            value.RuleFor(v => v.ParentValueCode).MaximumLength(128);
        });
    }
}
