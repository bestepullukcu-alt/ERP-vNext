namespace Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.Catalog;
public sealed class ArchiveProcessArchitectureValidator { public string? Validate(ArchiveProcessArchitectureCommand request) => CatalogValidation.Command(request); }
