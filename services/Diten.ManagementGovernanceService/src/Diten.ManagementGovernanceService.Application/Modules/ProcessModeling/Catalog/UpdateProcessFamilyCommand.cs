namespace Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.Catalog;
public sealed record UpdateProcessFamilyCommand(Guid Id, string Name, string? Description, int SortOrder, int ExpectedVersion, CatalogCommandContext Context) : ICatalogCommand
{ public CatalogMutation Mutation => new(CatalogMutationKind.UpdateFamily, Id, null, null, Name, null, Description, SortOrder, ExpectedVersion); }
