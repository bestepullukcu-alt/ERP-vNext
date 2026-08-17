using Diten.Platform.Application.Features.BusinessReferenceData.Commands;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using FluentValidation;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Validators;

public sealed class ReplaceBusinessReferenceDataVersionAttributeDefinitionsCommandValidator : AbstractValidator<ReplaceBusinessReferenceDataVersionAttributeDefinitionsCommand>
{
    public ReplaceBusinessReferenceDataVersionAttributeDefinitionsCommandValidator()
    {
        RuleFor(x => x.VersionId).NotEmpty();
        RuleFor(x => x.ActorId).NotEmpty().MaximumLength(128);
        RuleFor(x => x.CorrelationId).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Definitions).NotNull();
        RuleForEach(x => x.Definitions).ChildRules(definition =>
        {
            definition.RuleFor(d => d.AttributeCode).NotEmpty().MaximumLength(128);
            definition.RuleFor(d => d.DisplayName).NotEmpty().MaximumLength(256);
            definition.RuleFor(d => d.DataType).NotEmpty().MaximumLength(32);
        });
    }
}
