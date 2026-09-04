namespace Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.Catalog;
public sealed class CreateProcessFamilyValidator { public string? Validate(CreateProcessFamilyCommand request) => CatalogValidation.Command(request); }
