namespace Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.Catalog;
public sealed record ArchiveProcessDefinitionCommand(Guid Id, int ExpectedVersion, CatalogCommandContext Context) : ICatalogCommand
{ public CatalogMutation Mutation => new(CatalogMutationKind.ArchiveDefinition, Id, null, null, null, null, null, null, ExpectedVersion); }
