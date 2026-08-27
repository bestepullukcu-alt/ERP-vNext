using Diten.ManagementGovernanceService.Application.Modules.ProcessModeling;
using Diten.ManagementGovernanceService.Persistence.Modules.ProcessModeling;

namespace Diten.ManagementGovernanceService.IntegrationTests.Modules.ProcessModeling;

public sealed class ClosedCommandManifestTests
{
    [Fact]
    public void Exact_twenty_commands_are_closed_over_operation_collection_lifecycle_and_fields()
    {
        var descriptors=ProcessModelingMongoCommandManifest.Descriptors;
        Assert.Equal(20,descriptors.Count);
        Assert.Equal(ProcessModelingPermissions.ExactCommandMap.Keys.Order(StringComparer.Ordinal),descriptors.Select(x=>x.Command));
        Assert.Equal(20,descriptors.Select(x=>x.Command).Distinct(StringComparer.Ordinal).Count());
        Assert.All(descriptors,x=>Assert.Contains(x.Collection,ProcessModelingPersistenceManifest.Collections,StringComparer.Ordinal));

        var publish=Assert.Single(descriptors,x=>x.Command=="PublishProcessModelVersionCommand");
        Assert.Equal("Unavailable",publish.Operation);
        Assert.Equal("Review",publish.RequiredLifecycle);
        Assert.Equal("Published",publish.NextLifecycle);

        var revision=Assert.Single(descriptors,x=>x.Command=="CreateProcessModelRevisionCommand");
        Assert.Equal("CreateRevision",revision.Operation);
        Assert.Equal("mg_process_model_versions",revision.Collection);
        Assert.DoesNotContain("ProcessModelId",revision.Fields,StringComparer.Ordinal);
        Assert.DoesNotContain("ModelAuthorActorId",revision.Fields,StringComparer.Ordinal);

        Assert.Equal("mg_process_architectures",Assert.Single(descriptors,x=>x.Command=="CreateProcessDomainCommand").ParentCollection);
        Assert.Equal("mg_process_domains",Assert.Single(descriptors,x=>x.Command=="CreateProcessFamilyCommand").ParentCollection);
        Assert.Equal("mg_process_families",Assert.Single(descriptors,x=>x.Command=="CreateProcessDefinitionCommand").ParentCollection);
        Assert.Equal("mg_process_definitions",Assert.Single(descriptors,x=>x.Command=="CreateProcessModelCommand").ParentCollection);
    }

    [Fact]
    public void Caller_cannot_supply_a_business_plan_collection_or_operation()
    {
        Assert.Null(typeof(MongoMutationEnvelope).GetProperty("Plan"));
        Assert.DoesNotContain(typeof(MongoMutationEnvelope).Assembly.GetTypes(),x=>x.Name=="ProcessModelingMongoBusinessPlan");
    }

    [Fact]
    public void Immutable_and_server_owned_fields_are_absent_from_update_and_revision_inputs()
    {
        var descriptors=ProcessModelingMongoCommandManifest.Descriptors;
        var forbidden=new[]{"TenantId","Version","IsDeleted","DeletedAtUtc","LifecycleState","ModelAuthorActorId","PublishedVersionId","OpenVersionId","LatestRevisionNumber"};
        foreach(var descriptor in descriptors.Where(x=>x.Operation is "Update" or "Archive" or "Transition" or "DraftReplace" or "CreateRevision"))
            Assert.All(forbidden,field=>Assert.DoesNotContain(field,descriptor.Fields,StringComparer.Ordinal));

        var draft=Assert.Single(descriptors,x=>x.Command=="UpdateDraftProcessModelVersionCommand");
        Assert.Equal(new[]{"Activities","ContentHash","ControlPoints","Description","Relationships","Title"},draft.Fields);
    }
}
