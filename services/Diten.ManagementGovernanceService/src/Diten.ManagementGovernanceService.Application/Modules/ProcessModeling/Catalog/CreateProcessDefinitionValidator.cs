namespace Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.Catalog;
public sealed class CreateProcessDefinitionValidator { public string? Validate(CreateProcessDefinitionCommand request) => CatalogValidation.Command(request); }
