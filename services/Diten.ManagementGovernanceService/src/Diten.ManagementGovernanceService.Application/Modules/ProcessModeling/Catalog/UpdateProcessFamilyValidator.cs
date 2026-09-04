namespace Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.Catalog;
public sealed class UpdateProcessFamilyValidator { public string? Validate(UpdateProcessFamilyCommand request) => CatalogValidation.Command(request); }
