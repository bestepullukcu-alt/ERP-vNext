using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.DocumentManagementMasterRegister.Validators;

// MOD-0029-FU06 — input-shape validators (no Command suffix). Business/authorization rules stay in the service
// (DocumentMasterRegisterService), which returns typed reason codes.

public sealed class CreateMasterRegisterEntryValidator : AbstractValidator<CreateMasterRegisterEntryCommand>
{
    public CreateMasterRegisterEntryValidator()
    {
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.DocumentTitle).NotEmpty().MaximumLength(512).When(x => x.Input is not null);
        RuleFor(x => x.Input.DocumentClass).NotEmpty().When(x => x.Input is not null);
        RuleFor(x => x.Input.Criticality).NotEmpty().When(x => x.Input is not null);
        RuleFor(x => x.Input.AuthorUserId).NotEmpty().When(x => x.Input is not null);
    }
}

public sealed class UpdateMasterRegisterMetadataValidator : AbstractValidator<UpdateMasterRegisterMetadataCommand>
{
    public UpdateMasterRegisterMetadataValidator()
    {
        RuleFor(x => x.EntryId).NotEmpty();
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.DocumentTitle).NotEmpty().MaximumLength(512).When(x => x.Input is not null);
        RuleFor(x => x.Input.DocumentClass).NotEmpty().When(x => x.Input is not null);
        RuleFor(x => x.Input.Criticality).NotEmpty().When(x => x.Input is not null);
    }
}

public sealed class LinkControlledDocumentToRegisterEntryValidator : AbstractValidator<LinkControlledDocumentToRegisterEntryCommand>
{
    public LinkControlledDocumentToRegisterEntryValidator()
    {
        RuleFor(x => x.EntryId).NotEmpty();
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.ControlledDocumentId).NotEmpty().When(x => x.Input is not null);
        RuleFor(x => x.Input.ReconciliationReason).NotEmpty().MaximumLength(1000).When(x => x.Input is not null);
    }
}
