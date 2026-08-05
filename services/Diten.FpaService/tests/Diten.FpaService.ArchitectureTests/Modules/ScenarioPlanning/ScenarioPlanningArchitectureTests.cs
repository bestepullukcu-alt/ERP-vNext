using System.Reflection;
using Diten.FpaService.Domain.Modules.ScenarioPlanning;
using Diten.FpaService.Persistence.Modules.ScenarioPlanning;

namespace Diten.FpaService.ArchitectureTests.Modules.ScenarioPlanning;

public sealed class ScenarioPlanningArchitectureTests
{
    [Fact] public void Persistence_manifest_is_exact_12_collections_and_23_tenant_first_indexes()
    {Assert.Equal(12,ScenarioPlanningPersistenceOwnerManifest.Collections.Count);Assert.Equal(23,ScenarioPlanningPersistenceOwnerManifest.Indexes.Count);Assert.All(ScenarioPlanningPersistenceOwnerManifest.Indexes,x=>{Assert.Equal("TenantId",x.Keys[0]);Assert.False(x.HasTtl);});Assert.Empty(ScenarioPlanningPersistenceOwnerManifest.Validate());Assert.Equal(12,ScenarioPlanningPersistenceOwnerManifest.Collections.Select(x=>x.Name).Distinct().Count());Assert.Equal(23,ScenarioPlanningPersistenceOwnerManifest.Indexes.Select(x=>x.Name).Distinct().Count());}
    [Fact] public void Transaction_participants_are_owner_manifest_only_and_never_budgeting()
    {var owned=ScenarioPlanningPersistenceOwnerManifest.Collections.Select(x=>x.Name).ToHashSet();Assert.All(ScenarioPlanningPersistenceOwnerManifest.Transactions,t=>Assert.All(t.Participants,p=>Assert.Contains(p,owned)));Assert.DoesNotContain(ScenarioPlanningPersistenceOwnerManifest.Transactions.SelectMany(x=>x.Participants),x=>x.Contains("budgeting",StringComparison.OrdinalIgnoreCase));}
    [Fact] public void Published_and_history_types_have_no_public_mutators()
    {foreach(var type in new[]{typeof(ScenarioVersion),typeof(ComparatorOutput),typeof(ComparatorExecutionEvent),typeof(ScenarioSelectionOccurrence),typeof(ScenarioSelectionDisposition)})Assert.DoesNotContain(type.GetProperties(),x=>x.SetMethod?.IsPublic==true);}
    [Fact] public void Scenario_source_has_no_runtime_or_foreign_persistence_access()
    {var root=FindRoot();var roots=new[]{Path.Combine(root,"services/Diten.FpaService/src/Diten.FpaService.Domain/Modules/ScenarioPlanning"),Path.Combine(root,"services/Diten.FpaService/src/Diten.FpaService.Application/Modules/ScenarioPlanning"),Path.Combine(root,"services/Diten.FpaService/src/Diten.FpaService.Persistence/Modules/ScenarioPlanning")};var text=string.Join('\n',roots.SelectMany(x=>Directory.GetFiles(x,"*.cs",SearchOption.AllDirectories)).Select(File.ReadAllText));foreach(var forbidden in new[]{"Modules.Budgeting","BudgetingRepository","MongoClient","IMongoCollection","IClientSessionHandle","MassTransit","RabbitMQ","IEventBus","WebApplication","ControllerBase","Diten.PpmService","Diten.EnterpriseStrategyService"})Assert.DoesNotContain(forbidden,text,StringComparison.Ordinal);}
    [Fact] public void Mutation_guards_pin_index_order_selection_fence_and_one_open_draft()
    {var first=ScenarioPlanningPersistenceOwnerManifest.Indexes[0];Assert.Equal(new[]{"TenantId","Code"},first.Keys);Assert.NotNull(typeof(ScenarioSelectionFence).GetMethod("Replace"));Assert.NotNull(typeof(OpenDraftFence).GetMethod("Acquire"));}
    private static string FindRoot(){var d=new DirectoryInfo(AppContext.BaseDirectory);while(d is not null&&!File.Exists(Path.Combine(d.FullName,"AGENTS.md")))d=d.Parent;return d?.FullName??throw new InvalidOperationException();}
}
