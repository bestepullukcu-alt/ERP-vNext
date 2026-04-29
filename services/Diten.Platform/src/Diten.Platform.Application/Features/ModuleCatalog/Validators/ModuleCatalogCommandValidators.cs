using Diten.Platform.Application.Features.ModuleCatalog.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.ModuleCatalog.Validators;

public sealed class CreateDomainLandscapeCommandValidator : AbstractValidator<CreateDomainLandscapeCommand>
{
    public CreateDomainLandscapeCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
    }
}

public sealed class CreateSuitePlatformCommandValidator : AbstractValidator<CreateSuitePlatformCommand>
{
    public CreateSuitePlatformCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.DomainLandscapeId).NotEmpty();
    }
}

public sealed class CreateCapabilityGroupCommandValidator : AbstractValidator<CreateCapabilityGroupCommand>
{
    public CreateCapabilityGroupCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.DomainLandscapeId).NotEmpty();
        RuleFor(x => x.SuitePlatformId).NotEmpty();
    }
}

public sealed class CreateModuleDefinitionCommandValidator : AbstractValidator<CreateModuleDefinitionCommand>
{
    public CreateModuleDefinitionCommandValidator()
    {
        RuleFor(x => x.ModuleId).NotEmpty();
        RuleFor(x => x.ModuleName).NotEmpty();
        RuleFor(x => x.DomainLandscapeId).NotEmpty();
        RuleFor(x => x.SuitePlatformId).NotEmpty();
        RuleFor(x => x.CapabilityGroupId).NotEmpty();
    }
}

public sealed class ImportModuleCatalogCommandValidator : AbstractValidator<ImportModuleCatalogCommand>
{
    public ImportModuleCatalogCommandValidator()
    {
        RuleFor(x => x.Rows).NotEmpty();
    }
}

public sealed class CreateModulePageDefinitionCommandValidator : AbstractValidator<CreateModulePageDefinitionCommand>
{
    public CreateModulePageDefinitionCommandValidator()
    {
        RuleFor(x => x.ModuleId).NotEmpty();
        RuleFor(x => x.PageCode).NotEmpty();
        RuleFor(x => x.PageName).NotEmpty();
        RuleFor(x => x.PageType)
            .Must(BeValidPageType)
            .When(x => !string.IsNullOrWhiteSpace(x.PageType))
            .WithMessage("PageType is invalid.");
    }
    
    private static bool BeValidPageType(string? value) =>
        Enum.TryParse<Diten.Platform.Domain.Entities.ModulePageType>(value, true, out _);
}

public sealed class UpdateModulePageDefinitionCommandValidator : AbstractValidator<UpdateModulePageDefinitionCommand>
{
    public UpdateModulePageDefinitionCommandValidator()
    {
        RuleFor(x => x.ModuleId).NotEmpty();
        RuleFor(x => x.PageCode).NotEmpty();
        RuleFor(x => x.PageName).NotEmpty();
        RuleFor(x => x.PageType)
            .Must(BeValidPageType)
            .When(x => !string.IsNullOrWhiteSpace(x.PageType))
            .WithMessage("PageType is invalid.");
    }

    private static bool BeValidPageType(string? value) =>
        Enum.TryParse<Diten.Platform.Domain.Entities.ModulePageType>(value, true, out _);
}

public sealed class UpdateDomainLandscapeCommandValidator : AbstractValidator<UpdateDomainLandscapeCommand>
{
    public UpdateDomainLandscapeCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty();
    }
}

public sealed class UpdateSuitePlatformCommandValidator : AbstractValidator<UpdateSuitePlatformCommand>
{
    public UpdateSuitePlatformCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.DomainLandscapeId).NotEmpty();
    }
}

public sealed class UpdateCapabilityGroupCommandValidator : AbstractValidator<UpdateCapabilityGroupCommand>
{
    public UpdateCapabilityGroupCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.DomainLandscapeId).NotEmpty();
        RuleFor(x => x.SuitePlatformId).NotEmpty();
    }
}

public sealed class UpdateModuleDefinitionCommandValidator : AbstractValidator<UpdateModuleDefinitionCommand>
{
    public UpdateModuleDefinitionCommandValidator()
    {
        RuleFor(x => x.ModuleId).NotEmpty();
        RuleFor(x => x.ModuleName).NotEmpty();
        RuleFor(x => x.DomainLandscapeId).NotEmpty();
        RuleFor(x => x.SuitePlatformId).NotEmpty();
        RuleFor(x => x.CapabilityGroupId).NotEmpty();
    }
}
