namespace Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.Catalog;
public sealed class ArchiveProcessDomainValidator { public string? Validate(ArchiveProcessDomainCommand request) => CatalogValidation.Command(request); }
