namespace Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.Catalog;
public sealed record UpdateProcessDefinitionCommand(Guid Id, string Name, string? Purpose, string? Description, int ExpectedVersion, CatalogCommandContext Context) : ICatalogCommand
{ public CatalogMutation Mutation => new(CatalogMutationKind.UpdateDefinition, Id, null, null, Name, Purpose, Description, null, ExpectedVersion); }
