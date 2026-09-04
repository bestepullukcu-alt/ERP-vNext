namespace Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.Catalog;
public sealed class UpdateProcessDefinitionValidator { public string? Validate(UpdateProcessDefinitionCommand request) => CatalogValidation.Command(request); }
