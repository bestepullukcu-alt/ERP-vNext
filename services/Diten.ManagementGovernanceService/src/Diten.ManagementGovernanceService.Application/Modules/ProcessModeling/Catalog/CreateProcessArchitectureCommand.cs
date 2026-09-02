namespace Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.Catalog;
public sealed record CreateProcessArchitectureCommand(Guid Id, string ArchitectureCode, string Name, string? Description, int SortOrder, CatalogCommandContext Context) : ICatalogCommand
{ public CatalogMutation Mutation => new(CatalogMutationKind.CreateArchitecture, Id, null, ArchitectureCode, Name, null, Description, SortOrder, null); }
