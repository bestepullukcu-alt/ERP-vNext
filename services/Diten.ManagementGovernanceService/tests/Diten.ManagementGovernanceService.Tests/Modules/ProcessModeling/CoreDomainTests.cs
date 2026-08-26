using Diten.ManagementGovernanceService.Domain.Modules.ProcessModeling;

namespace Diten.ManagementGovernanceService.Tests.Modules.ProcessModeling;

public sealed class CoreDomainTests
{
    private static readonly DateTime Now = new(2026, 8, 25, 12, 0, 0, DateTimeKind.Utc);

    [Fact] public void Catalog_archive_is_terminal_and_not_delete()
    {
        var x = new ProcessArchitecture(Guid.NewGuid(), Guid.NewGuid(), Now, "order architecture", "Orders", null, 0);
        x.Archive(0, Now.AddMinutes(1));
        Assert.Equal(CatalogLifecycleState.Archived, x.LifecycleState); Assert.False(x.IsDeleted); Assert.Null(x.DeletedAtUtc);
        Assert.Throws<InvalidOperationException>(() => x.Update("Other", null, 0, 1, Now.AddMinutes(2)));
    }

    [Fact] public void Definition_has_exact_mutable_fields_without_sort_order()
    {
        var definition=new ProcessDefinition(Guid.NewGuid(),Guid.NewGuid(),Now,Guid.NewGuid(),"DEF","Name","Purpose","Description");definition.UpdateDefinition("Changed","New purpose",new string('x',4000),0,Now.AddMinutes(1));
        Assert.Equal("New purpose",definition.Purpose);Assert.Null(typeof(ProcessDefinition).GetProperty("SortOrder"));Assert.Throws<ArgumentException>(()=>definition.UpdateDefinition("Changed",null,new string('x',4001),1,Now.AddMinutes(2)));
    }

    [Fact] public void Model_allocates_monotonic_single_open_revision()
    {
        var x = new ProcessModel(Guid.NewGuid(), Guid.NewGuid(), Now, Guid.NewGuid(), "order model", "Order", null);
        Assert.Equal(1, x.AllocateRevision(Guid.NewGuid(), 0, Now));
        Assert.Throws<InvalidOperationException>(() => x.AllocateRevision(Guid.NewGuid(), 1, Now));
    }

    [Fact] public void Version_lifecycle_and_immutability_are_exact()
    {
        var x = NewVersion(); x.RequestReview(0, Now); x.ReturnToDraft(1, Now); x.RequestReview(2, Now);
        x.PublishDomainTransitionSpec(3, Now); Assert.Equal(ProcessModelVersionState.Published, x.LifecycleState);
        Assert.Throws<InvalidOperationException>(() => x.ReplaceDraftContent("x", null, [], [], [], 4, Now));
        x.Retire(4, Now); Assert.Equal(ProcessModelVersionState.Retired, x.LifecycleState);
        Assert.Throws<InvalidOperationException>(() => x.RequestReview(5, Now));
    }

    [Fact] public void Invalid_graph_is_rejected()
    {
        var id = Guid.NewGuid(); var x = NewVersion(); var tenant=x.TenantId; var version=x.Id;
        Assert.Throws<ArgumentException>(() => x.ReplaceDraftContent("x", null,
            [new(Guid.NewGuid(),tenant,Now,version,id,"A","A",null,0)], [], [new(Guid.NewGuid(),tenant,Now,version,id,id,null,0)], 0, Now));
    }

    [Fact] public void Model_pointers_coordinate_open_publish_and_retire()
    {
        var model=new ProcessModel(Guid.NewGuid(),Guid.NewGuid(),Now,Guid.NewGuid(),"MODEL","Model",null);var revision=Guid.NewGuid();
        Assert.Equal(1,model.LatestRevisionNumber);Assert.Equal(1,model.AllocateRevision(revision,0,Now));
        model.PublishVersion(revision,1,Now.AddMinutes(1));Assert.Null(model.OpenVersionId);Assert.Equal(revision,model.PublishedVersionId);
        model.RetirePublishedVersion(revision,2,Now.AddMinutes(2));Assert.Null(model.PublishedVersionId);
        var next=Guid.NewGuid();Assert.Equal(2,model.AllocateRevision(next,3,Now.AddMinutes(3)));
    }

    [Fact] public void Draft_hash_is_server_computed_and_children_are_tenant_owned()
    {
        var version=NewVersion();var activity=new ProcessActivity(Guid.NewGuid(),version.TenantId,Now,version.Id,Guid.NewGuid(),"activity one"," Cafe\u0301 ",null,0);
        version.ReplaceDraftContent(" Title ",null,[activity],[],[],0,Now.AddMinutes(1));
        Assert.Equal(CanonicalContentHash.Compute(new("Title",null,[activity],[],[])),version.ContentHash);Assert.Equal("Café",activity.Name);Assert.Equal(version.TenantId,activity.TenantId);Assert.Equal(version.Id,activity.ProcessModelVersionId);
    }

    [Fact] public void Graph_rejects_cross_tenant_and_cross_version_children()
    {
        var version=NewVersion();ProcessActivity A(Guid tenant,Guid owner)=>new(Guid.NewGuid(),tenant,Now,owner,Guid.NewGuid(),"A","A",null,0);
        Assert.Throws<ArgumentException>(()=>version.ReplaceDraftContent("T",null,[A(Guid.NewGuid(),version.Id)],[],[],0,Now));
        Assert.Throws<ArgumentException>(()=>version.ReplaceDraftContent("T",null,[A(version.TenantId,Guid.NewGuid())],[],[],0,Now));
    }

    private static ProcessModelVersion NewVersion() => new(Guid.NewGuid(), Guid.NewGuid(), Now, Guid.NewGuid(), 1, "Order", null);
}
