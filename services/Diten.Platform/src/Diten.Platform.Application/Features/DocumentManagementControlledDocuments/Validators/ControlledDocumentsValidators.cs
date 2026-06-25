using Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Validators;

// MOD-0029-FU01 — input-shape validators (no Command suffix). Business/authorization rules stay in the services.

public sealed class CreateControlledDocumentValidator : AbstractValidator<CreateControlledDocumentCommand>
{
    public CreateControlledDocumentValidator()
    {
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.CollectionInstanceId).NotEmpty();
        RuleFor(x => x.Input.CompanyId).NotEmpty();
        RuleFor(x => x.Input.Title).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Input.DocumentType).NotEmpty();
        RuleFor(x => x.Input.File).NotNull();
        RuleFor(x => x.Input.File.FileName).NotEmpty().When(x => x.Input?.File is not null);
        RuleFor(x => x.Input.File.ContentBase64).NotEmpty().When(x => x.Input?.File is not null);
    }
}

public sealed class CreateTemplateDocumentValidator : AbstractValidator<CreateTemplateDocumentCommand>
{
    public CreateTemplateDocumentValidator()
    {
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.CompanyId).NotEmpty();
        RuleFor(x => x.Input.Title).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Input.File).NotNull();
        RuleFor(x => x.Input.File.FileName).NotEmpty().When(x => x.Input?.File is not null);
        RuleFor(x => x.Input.File.ContentBase64).NotEmpty().When(x => x.Input?.File is not null);
    }
}

public sealed class CreateControlledDocumentVersionValidator : AbstractValidator<CreateControlledDocumentVersionCommand>
{
    public CreateControlledDocumentVersionValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty();
        RuleFor(x => x.File).NotNull();
        RuleFor(x => x.File.FileName).NotEmpty().When(x => x.File is not null);
        RuleFor(x => x.File.ContentBase64).NotEmpty().When(x => x.File is not null);
    }
}

public sealed class CreateTemplateVersionValidator : AbstractValidator<CreateTemplateVersionCommand>
{
    public CreateTemplateVersionValidator()
    {
        RuleFor(x => x.TemplateId).NotEmpty();
        RuleFor(x => x.File).NotNull();
        RuleFor(x => x.File.FileName).NotEmpty().When(x => x.File is not null);
        RuleFor(x => x.File.ContentBase64).NotEmpty().When(x => x.File is not null);
    }
}

public sealed class ShareControlledDocumentValidator : AbstractValidator<ShareControlledDocumentCommand>
{
    public ShareControlledDocumentValidator()
    {
        RuleFor(x => x.DocumentId).NotEmpty();
        RuleFor(x => x.TargetCompanyId).NotEmpty();
    }
}

public sealed class ShareTemplateValidator : AbstractValidator<ShareTemplateCommand>
{
    public ShareTemplateValidator()
    {
        RuleFor(x => x.TemplateId).NotEmpty();
        RuleFor(x => x.TargetCompanyId).NotEmpty();
    }
}

public sealed class DryRunFolderShareValidator : AbstractValidator<DryRunFolderShareCommand>
{
    public DryRunFolderShareValidator()
    {
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.SourceBranchCollectionInstanceId).NotEmpty();
        RuleFor(x => x.Input.TargetCompanyId).NotEmpty();
    }
}

public sealed class ExecuteFolderShareValidator : AbstractValidator<ExecuteFolderShareCommand>
{
    public ExecuteFolderShareValidator()
    {
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.SourceBranchCollectionInstanceId).NotEmpty();
        RuleFor(x => x.Input.TargetCompanyId).NotEmpty();
    }
}

public sealed class UpsertFolderDocumentAccessValidator : AbstractValidator<UpsertFolderDocumentAccessCommand>
{
    public UpsertFolderDocumentAccessValidator()
    {
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.CollectionInstanceId).NotEmpty();
        RuleFor(x => x.Input.CompanyId).NotEmpty();
        RuleFor(x => x.Input.TargetType).NotEmpty();
        RuleFor(x => x.Input.TargetId).NotEmpty();
        RuleFor(x => x.Input.Permissions).NotNull();
    }
}
