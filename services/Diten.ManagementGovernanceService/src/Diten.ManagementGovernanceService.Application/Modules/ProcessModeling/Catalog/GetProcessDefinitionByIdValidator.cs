namespace Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.Catalog;
public sealed class GetProcessDefinitionByIdValidator { public string? Validate(GetProcessDefinitionByIdQuery request) => CatalogValidation.Query(request.Context, request.Id); }
