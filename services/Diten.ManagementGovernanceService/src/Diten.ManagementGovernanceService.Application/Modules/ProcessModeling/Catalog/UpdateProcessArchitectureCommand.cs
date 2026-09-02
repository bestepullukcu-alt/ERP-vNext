namespace Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.Catalog;
public sealed record UpdateProcessArchitectureCommand(Guid Id, string Name, string? Description, int SortOrder, int ExpectedVersion, CatalogCommandContext Context) : ICatalogCommand
{ public CatalogMutation Mutation => new(CatalogMutationKind.UpdateArchitecture, Id, null, null, Name, null, Description, SortOrder, ExpectedVersion); }
