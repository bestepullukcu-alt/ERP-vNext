using Diten.Platform.Application.Features.BusinessReferenceData.Commands;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using FluentValidation;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Validators;

public sealed class DeactivateBusinessReferenceDataUsageRegistrationCommandValidator : AbstractValidator<DeactivateBusinessReferenceDataUsageRegistrationCommand>
{
    public DeactivateBusinessReferenceDataUsageRegistrationCommandValidator()
    {
        RuleFor(x => x.UsageRegistrationId).NotEmpty();
        RuleFor(x => x.ActorId).NotEmpty().MaximumLength(128);
        RuleFor(x => x.CorrelationId).NotEmpty().MaximumLength(64);
    }
}
