using Diten.Platform.Application.Features.BusinessReferenceData.Commands;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using FluentValidation;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Validators;

public sealed class ReplaceBusinessReferenceDataVersionMappingsCommandValidator : AbstractValidator<ReplaceBusinessReferenceDataVersionMappingsCommand>
{
    public ReplaceBusinessReferenceDataVersionMappingsCommandValidator()
    {
        RuleFor(x => x.VersionId).NotEmpty();
        RuleFor(x => x.ActorId).NotEmpty().MaximumLength(128);
        RuleFor(x => x.CorrelationId).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Mappings).NotNull();
        RuleForEach(x => x.Mappings).ChildRules(mapping =>
        {
            mapping.RuleFor(x => x.MappingKey).NotEmpty().MaximumLength(128);
            mapping.RuleFor(x => x.SourceValueCode).NotEmpty().MaximumLength(128);
            mapping.RuleFor(x => x.TargetCode).NotEmpty().MaximumLength(128);
            mapping.RuleFor(x => x.TargetLabel).MaximumLength(256);
        });
    }
}
