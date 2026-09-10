using Diten.Platform.Application.Features.DocumentRepository.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.DocumentRepository.Validators;

/// <summary>
/// MOD-0262-FU01 — request-shape validation only. Content-level rules (extension / media type / size /
/// checksum) belong to the storage gateway, which enforces them while streaming; duplicating them here would
/// create a second, drifting source of truth.
/// </summary>
public sealed class StoreRepositoryObjectValidator : AbstractValidator<StoreRepositoryObjectCommand>
{
    public StoreRepositoryObjectValidator()
    {
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.FileName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Input.Content).NotNull();
        RuleFor(x => x.Input.OwningItemId).NotEmpty();
        RuleFor(x => x.Input.OwningVersionId).NotEmpty();
    }
}
