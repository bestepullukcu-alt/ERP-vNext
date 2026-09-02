namespace Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.Catalog;
public sealed record CreateProcessDefinitionCommand(Guid Id, Guid ProcessFamilyId, string ProcessCode, string Name, string? Purpose, string? Description, CatalogCommandContext Context) : ICatalogCommand
{ public CatalogMutation Mutation => new(CatalogMutationKind.CreateDefinition, Id, ProcessFamilyId, ProcessCode, Name, Purpose, Description, null, null); }
