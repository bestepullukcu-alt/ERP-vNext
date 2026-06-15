using Diten.Platform.Application.Features.BusinessReferenceData.Commands;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using FluentValidation;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Validators;

public sealed class RegisterBusinessReferenceDataUsageCommandValidator : AbstractValidator<RegisterBusinessReferenceDataUsageCommand>
{
    public RegisterBusinessReferenceDataUsageCommandValidator()
    {
        RuleFor(x => x.SetCode).NotEmpty().MaximumLength(64);
        RuleFor(x => x.ConsumerModule).NotEmpty().MaximumLength(128);
        RuleFor(x => x.ConsumerName).NotEmpty().MaximumLength(128);
        RuleFor(x => x.ConsumerEndpoint).MaximumLength(512);
        RuleFor(x => x.ScopeType).MaximumLength(32);
        RuleFor(x => x.ScopeKey).MaximumLength(128);
        RuleFor(x => x.VersionPin).GreaterThan(0).When(x => x.VersionPin.HasValue);
        RuleFor(x => x.ResolutionMode).MaximumLength(16);
        RuleFor(x => x.Criticality).MaximumLength(16);
        RuleFor(x => x.Notes).MaximumLength(1000);
        RuleFor(x => x.ActorId).NotEmpty().MaximumLength(128);
        RuleFor(x => x.CorrelationId).NotEmpty().MaximumLength(64);
    }
}
