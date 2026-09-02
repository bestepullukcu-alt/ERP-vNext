namespace Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.Catalog;
public sealed class CreateProcessArchitectureValidator { public string? Validate(CreateProcessArchitectureCommand request) => CatalogValidation.Command(request); }
