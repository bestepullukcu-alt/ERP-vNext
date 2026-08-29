using Diten.Platform.Application.Features.DocumentManagementIdentifiers.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.DocumentManagementIdentifiers.Validators;

// MOD-0029-FU07 — input-shape validators. Business/eligibility rules stay in DocumentIdentifierAllocationService.

public sealed class AllocateUidValidator : AbstractValidator<AllocateUidCommand>
{
    public AllocateUidValidator() => RuleFor(x => x.RegisterEntryId).NotEmpty();
}

public sealed class AllocateCodeValidator : AbstractValidator<AllocateCodeCommand>
{
    public AllocateCodeValidator() => RuleFor(x => x.RegisterEntryId).NotEmpty();
}

public sealed class AllocateIdentifiersValidator : AbstractValidator<AllocateIdentifiersCommand>
{
    public AllocateIdentifiersValidator() => RuleFor(x => x.RegisterEntryId).NotEmpty();
}

public sealed class ReserveIdentifierValidator : AbstractValidator<ReserveIdentifierCommand>
{
    public ReserveIdentifierValidator()
    {
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.IdentifierType).NotEmpty().When(x => x.Input is not null);
        RuleFor(x => x.Input.IdentifierValue).NotEmpty().MaximumLength(128).When(x => x.Input is not null);
    }
}

public sealed class CancelIdentifierValidator : AbstractValidator<CancelIdentifierCommand>
{
    public CancelIdentifierValidator() => RuleFor(x => x.AllocationId).NotEmpty();
}
