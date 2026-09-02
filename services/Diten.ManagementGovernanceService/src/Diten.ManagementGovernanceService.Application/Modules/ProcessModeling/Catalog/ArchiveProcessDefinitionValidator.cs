namespace Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.Catalog;
public sealed class ArchiveProcessDefinitionValidator { public string? Validate(ArchiveProcessDefinitionCommand request) => CatalogValidation.Command(request); }
