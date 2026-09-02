namespace Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.Catalog;
public sealed class GetCatalogTreeValidator { public string? Validate(GetCatalogTreeQuery request) => CatalogValidation.Query(request.Context); }
