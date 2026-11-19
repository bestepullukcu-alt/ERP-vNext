using Diten.Platform.Application.Features.DocumentRepository.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.DocumentRepository.Validators;

/// <summary>
/// MOD-0262-FU01 DL-2 — request-shape validation of a trusted consumer upload. Content rules (extension / media type /
/// size / checksum) stay with the storage gateway, as on the user path. <c>ValidationBehavior</c> runs before
/// <c>AuditBehavior</c>, so a refusal here is recorded by the executor's refusal writer (§8.1 "Ret başına tek yazar").
/// </summary>
public sealed class StoreTrustedRepositoryObjectValidator : AbstractValidator<StoreTrustedRepositoryObjectCommand>
{
    public StoreTrustedRepositoryObjectValidator()
    {
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.FileName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Input.Content).NotNull();
        RuleFor(x => x.Input.OwningItemId).NotEmpty();
        RuleFor(x => x.Input.OwningVersionId).NotEmpty();
        RuleFor(x => x.Input.CompanyId).NotEmpty();
        RuleFor(x => x.Consumer).NotNull();
        RuleFor(x => x.Consumer.ClientId).NotEmpty();
        RuleFor(x => x.Consumer.ConsumerModule).NotEmpty();
        RuleFor(x => x.CorrelationId).NotEmpty();
    }
}
