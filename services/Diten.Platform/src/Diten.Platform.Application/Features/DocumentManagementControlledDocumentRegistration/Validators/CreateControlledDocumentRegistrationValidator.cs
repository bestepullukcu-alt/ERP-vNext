using Diten.Platform.Application.Features.DocumentManagementControlledDocumentRegistration.Commands;
using FluentValidation;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Application.Features.DocumentManagementControlledDocumentRegistration.Validators;

public sealed class CreateControlledDocumentRegistrationValidator : AbstractValidator<CreateControlledDocumentRegistrationCommand>
{
    public CreateControlledDocumentRegistrationValidator()
    {
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.IdempotencyKey).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Input.DocumentTitle).NotEmpty().MaximumLength(512);
        RuleFor(x => x.Input.DocumentClass).NotEmpty();
        RuleFor(x => x.Input.Criticality).NotEmpty();
        RuleFor(x => x.Input.DocumentType).NotEmpty();
        RuleFor(x => x.Input.AuthorUserId).NotEmpty();
        RuleFor(x => x.Input.Description).MaximumLength(4000);
        RuleFor(x => x.Input.DocumentScope).IsInEnum();
        RuleFor(x => x.Input.Kind).IsInEnum().WithMessage("Registration kind is not recognised.");
        RuleFor(x => x.Input.VariantType).IsInEnum().WithMessage("Variant type is not recognised.");
        RuleFor(x => x.Input.ParentRegisterEntryId)
            .Must(p => p is { } id && id != Guid.Empty)
            .When(x => x.Input.Kind == RegistrationKind.Variant)
            .WithMessage("A parent register entry is required for a variant.");
        RuleFor(x => x.Input.GoverningLanguage).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Input)
            .Must(x => !string.IsNullOrWhiteSpace(x.GoverningLanguageId ?? x.GoverningLanguage))
            .WithMessage("A governed language value is required.");
        RuleFor(x => x.Input)
            .Must(x => !string.IsNullOrWhiteSpace(x.RetentionClassId ?? x.RetentionClass))
            .WithMessage("A governed retention class is required.");
        RuleFor(x => x.Input.OwnerCompanyId).NotEmpty()
            .When(x => x.Input.DocumentScope == DocumentScope.Company);
        RuleFor(x => x.Input.CompanyId).NotEmpty()
            .When(x => x.Input.DocumentScope == DocumentScope.Company);
        RuleFor(x => x.Input.CorporateOwnerId).Equal(Guid.Empty)
            .When(x => x.Input.DocumentScope == DocumentScope.Company)
            .WithMessage("Company registration cannot specify CorporateOwnerId.");
        RuleFor(x => x.Input.CorporateOwnerId).NotEmpty()
            .When(x => x.Input.DocumentScope == DocumentScope.Corporate);
        RuleFor(x => x.Input.CompanyId).Equal(Guid.Empty)
            .When(x => x.Input.DocumentScope == DocumentScope.Corporate)
            .WithMessage("Corporate registration cannot specify CompanyId.");
        RuleFor(x => x.Input.OwnerCompanyId).Equal(Guid.Empty)
            .When(x => x.Input.DocumentScope == DocumentScope.Corporate)
            .WithMessage("Corporate registration cannot specify OwnerCompanyId.");
        RuleFor(x => x.Input.CollectionInstanceId).NotEmpty();
        RuleFor(x => x.Input.FolderId).NotEmpty()
            .When(x => x.Input.DocumentScope == DocumentScope.Corporate);
        RuleFor(x => x.Input.ReviewCycleMonths).InclusiveBetween(1, 120).When(x => x.Input.ReviewCycleMonths.HasValue);
        RuleFor(x => x.Input.InitialFile).NotNull();
        RuleFor(x => x.Input.InitialFile.FileName).NotEmpty().When(x => x.Input.InitialFile is not null);
        RuleFor(x => x.Input.InitialFile.ContentBase64).NotEmpty().When(x => x.Input.InitialFile is not null);
        RuleForEach(x => x.Input.Tags).MaximumLength(100);
    }
}
