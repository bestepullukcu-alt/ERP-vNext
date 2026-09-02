namespace Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.Catalog;
public sealed class UpdateProcessArchitectureValidator { public string? Validate(UpdateProcessArchitectureCommand request) => CatalogValidation.Command(request); }
