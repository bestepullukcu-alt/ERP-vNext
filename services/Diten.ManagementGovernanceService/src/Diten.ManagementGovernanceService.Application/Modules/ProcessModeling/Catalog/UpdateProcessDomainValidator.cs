namespace Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.Catalog;
public sealed class UpdateProcessDomainValidator { public string? Validate(UpdateProcessDomainCommand request) => CatalogValidation.Command(request); }
