using Diten.Platform.Application.Features.BusinessReferenceData.Commands;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using FluentValidation;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Validators;

public sealed class PublishBusinessReferenceDataVersionCommandValidator : AbstractValidator<PublishBusinessReferenceDataVersionCommand>
{
    public PublishBusinessReferenceDataVersionCommandValidator()
    {
        RuleFor(x => x.VersionId).NotEmpty();
        RuleFor(x => x.ActorId).NotEmpty().MaximumLength(128);
        RuleFor(x => x.CorrelationId).NotEmpty().MaximumLength(64);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
        RuleFor(x => x.PublishMode).NotEmpty().MaximumLength(32);
        RuleFor(x => x.OverrideReason).NotEmpty().When(x => x.OverrideAction);
    }
}
