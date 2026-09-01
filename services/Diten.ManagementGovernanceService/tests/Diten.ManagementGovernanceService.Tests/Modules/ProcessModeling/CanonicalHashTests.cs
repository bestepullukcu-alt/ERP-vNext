using System.Text;
using System.Text.Json;
using Diten.ManagementGovernanceService.Domain.Modules.ProcessModeling;

namespace Diten.ManagementGovernanceService.Tests.Modules.ProcessModeling;

public sealed class CanonicalHashTests
{
    [Theory]
    [InlineData("Order Fulfilment", null, 194, "ab545fe0678b7ccc124136814af229d01f5d42b5180f8a903dc1276bb707eadd")]
    [InlineData("İade Süreci", "Café — müşteri", 208, "931d38322970398e266ba8f9aab2f5fb1a90effbf56552c0858452014614215f")]
    public void Empty_vectors_match_exact_bytes(string title, string? description, int length, string hash)
    {
        var content = new CanonicalProcessContent(title, description, [], [], []);
        var bytes = CanonicalContentHash.Write(content);
        Assert.Equal(length, bytes.Length); Assert.Equal("sha256:" + hash, CanonicalContentHash.Compute(content));
        Assert.False(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble)); Assert.NotEqual((byte)'\n', bytes[^1]);
        Assert.Equal(title=="Order Fulfilment"?CanonicalHexFixtures.Vector1:CanonicalHexFixtures.Vector2,Convert.ToHexString(bytes).ToLowerInvariant());
        Assert.NotEqual(JsonSerializer.SerializeToUtf8Bytes(content), bytes);
    }

    [Fact] public void Caller_order_is_irrelevant_and_normalized_values_are_persisted()
    {
        var tenant=Guid.NewGuid();var version=Guid.NewGuid();var now=new DateTime(2026,8,26,0,0,0,DateTimeKind.Utc);var one=Guid.NewGuid();var two=Guid.NewGuid();
        var a=new ProcessActivity(Guid.NewGuid(),tenant,now,version,one,"A","Cafe\u0301",null,20); var b=new ProcessActivity(Guid.NewGuid(),tenant,now,version,two,"B","Beta",null,10);
        Assert.Equal("Café",a.Name);
        var first=new CanonicalProcessContent("Title",null,[a,b],[],[]);var second=first with{Activities=[b,a]};
        Assert.Equal(CanonicalContentHash.Write(first),CanonicalContentHash.Write(second));
        Assert.ThrowsAny<ArgumentException>(()=>CanonicalContentHash.Write(new("\uD800",null,[],[],[])));
    }

    [Fact] public void Null_label_and_literal_old_sentinel_are_materially_distinct()
    {
        var tenant=Guid.NewGuid();var version=Guid.NewGuid();var now=new DateTime(2026,8,26,0,0,0,DateTimeKind.Utc);var a=Guid.NewGuid();var b=Guid.NewGuid();
        ProcessRelationship R(string? label)=>new(Guid.NewGuid(),tenant,now,version,a,b,label,0);
        Assert.NotEqual(CanonicalContentHash.Compute(new("T",null,[],[],[R(null)])),CanonicalContentHash.Compute(new("T",null,[],[],[R("<null>")])));
    }

    [Fact] public void Non_empty_unsorted_vector_matches_normative_output()
    {
        var a1 = Guid.Parse("11111111-1111-4111-8111-111111111111"); var a2 = Guid.Parse("22222222-2222-4222-8222-222222222222"); var a3 = Guid.Parse("33333333-3333-4333-8333-333333333333");
        var tenant=Guid.Parse("99999999-9999-4999-8999-999999999999"); var version=Guid.Parse("88888888-8888-4888-8888-888888888888"); var now=new DateTime(2026,8,25,0,0,0,DateTimeKind.Utc);
        var content = new CanonicalProcessContent("Order Fulfilment", null,
            [A(a3,"RELEASE","Release Order",null,30), A(a1,"CAPTURE","Capture Order",null,10), A(a2,"REVIEW","Review Order","Manual business review",20)],
            [C(Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"),"CP-RELEASE","Release Check","Definition-time checkpoint",a3,20), C(Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),"CP-REVIEW","Review Check",null,a2,10)],
            [R(a2,a3,"Standard continuation",20), R(a1,a2,null,10)]);
        ProcessActivity A(Guid logical,string code,string name,string? description,int order)=>new(Guid.NewGuid(),tenant,now,version,logical,code,name,description,order);
        ProcessControlPoint C(Guid logical,string code,string name,string? description,Guid? activity,int order)=>new(Guid.NewGuid(),tenant,now,version,logical,code,name,description,activity,order);
        ProcessRelationship R(Guid from,Guid to,string? label,int order)=>new(Guid.NewGuid(),tenant,now,version,from,to,label,order);
        var bytes = CanonicalContentHash.Write(content);
        Assert.Equal(1421, bytes.Length);
        Assert.Equal(CanonicalHexFixtures.Vector3,Convert.ToHexString(bytes).ToLowerInvariant());
        Assert.Equal("sha256:221c8a60cb22d36b3c73ae740c9911bad5241e3f39e2fa4a1e4048c80717fd95", CanonicalContentHash.Compute(content));
        Assert.StartsWith("{\"contractName\":\"management-governance.process-modeling.content-hash\"", Encoding.UTF8.GetString(bytes));
    }

    [Fact] public void Material_change_changes_hash()
    {
        var first = new CanonicalProcessContent("A", null, [], [], []); var second = first with { Title = "B" };
        Assert.NotEqual(CanonicalContentHash.Compute(first), CanonicalContentHash.Compute(second));
    }

    [Fact] public void Duplicate_complete_sort_key_is_rejected()
    {
        var id = Guid.NewGuid(); var item = new ProcessActivity(Guid.NewGuid(),Guid.NewGuid(),new DateTime(2026,8,25,0,0,0,DateTimeKind.Utc),Guid.NewGuid(),id,"A","A",null,0);
        Assert.Throws<ArgumentException>(() => CanonicalContentHash.Write(new("A",null,[item,item],[],[])));
    }
}
