using Diten.Platform.Application.Features.BusinessReferenceData.Commands;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using FluentValidation;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Validators;

public sealed class PreviewBusinessReferenceDataImportCommandValidator : AbstractValidator<PreviewBusinessReferenceDataImportCommand>
{
    public PreviewBusinessReferenceDataImportCommandValidator()
    {
        RuleFor(x => x.TargetDraftVersionId).NotEmpty();
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Format).NotEmpty().MaximumLength(32);
        RuleFor(x => x.ContentBase64).NotEmpty();
        RuleFor(x => x.ActorId).NotEmpty().MaximumLength(128);
        RuleFor(x => x.CorrelationId).NotEmpty().MaximumLength(64);
    }
}
