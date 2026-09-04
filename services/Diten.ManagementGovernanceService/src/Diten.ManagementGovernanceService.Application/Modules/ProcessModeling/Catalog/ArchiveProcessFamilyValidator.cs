namespace Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.Catalog;
public sealed class ArchiveProcessFamilyValidator { public string? Validate(ArchiveProcessFamilyCommand request) => CatalogValidation.Command(request); }
