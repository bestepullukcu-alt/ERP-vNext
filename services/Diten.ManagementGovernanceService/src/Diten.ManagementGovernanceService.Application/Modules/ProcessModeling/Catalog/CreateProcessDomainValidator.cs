namespace Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.Catalog;
public sealed class CreateProcessDomainValidator { public string? Validate(CreateProcessDomainCommand request) => CatalogValidation.Command(request); }
