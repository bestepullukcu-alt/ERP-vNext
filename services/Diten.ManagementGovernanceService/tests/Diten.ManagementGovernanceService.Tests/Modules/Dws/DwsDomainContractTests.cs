using Diten.ManagementGovernanceService.Application.Modules.Dws;
using Diten.ManagementGovernanceService.Domain.Modules.Dws;
using Xunit;

namespace Diten.ManagementGovernanceService.Tests.Modules.Dws;

public sealed class DwsDomainContractTests
{
    private static readonly Guid Tenant=Guid.Parse("10000000-0000-0000-0000-000000000001"),Revision=Guid.Parse("20000000-0000-0000-0000-000000000001"),A=Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),B=Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly DateTime Utc=new(2026,1,1,0,0,0,DateTimeKind.Utc);

    [Fact] public void Permission_and_command_query_contracts_are_exact()
    {
        Assert.Equal(15,DwsAuthorizationManifest.Entries.Count);Assert.Equal(6,DwsAuthorizationManifest.Entries.Select(x=>x.Permission).Distinct().Count());
        Assert.Equal(10,Enum.GetValues<DwsCommandFamily>().Length);Assert.Throws<DwsValidationException>(()=>DwsClosedValues.Command("createStructure"));
        Assert.Equal("no-op",DwsClosedValues.Outcome(DwsOutcomeKind.NoOp));Assert.Equal("management-governance.dws.create",DwsAuthorizationManifest.RequireExact("CreateStructureCommand"));
        var commands=new[]{typeof(CreateStructureCommand),typeof(UpdateStructureMetadataCommand),typeof(AddStructureNodeCommand),typeof(MoveStructureNodeCommand),typeof(ReorderStructureNodeCommand),typeof(RemoveStructureNodeCommand),typeof(AddStructuralDependencyCommand),typeof(RemoveStructuralDependencyCommand),typeof(CreateStructureBaselineCommand),typeof(CreateNextStructureRevisionCommand)};
        var queries=new[]{typeof(GetStructureByIdQuery),typeof(GetStructureTreeQuery),typeof(ValidateStructureQuery),typeof(CompareStructureRevisionsQuery),typeof(CompareStructureBaselinesQuery)};
        Assert.Equal(10,commands.Length);Assert.Equal(5,queries.Length);Assert.Throws<DwsValidationException>(()=>new CreateNextStructureRevisionCommand(Guid.NewGuid(),1,1,1).Validate());
    }

    [Fact] public void Tenant_entities_are_exact_and_fail_closed()
    {
        var inherited=new[]{"Id","TenantId","IsDeleted","DeletedAtUtc","CreatedAtUtc","UpdatedAtUtc","Version"};
        Assert.All(new[]{typeof(StructureDefinition),typeof(StructureRevision),typeof(StructureNode),typeof(StructuralDependency),typeof(StructureBaseline)},t=>Assert.All(inherited,p=>Assert.NotNull(t.GetProperty(p))));
        Assert.Throws<DwsValidationException>(()=>new StructureDefinition{TenantId=Guid.Empty,ExternalContextReference=Context()});
        Assert.Throws<DwsValidationException>(()=>StructuralDependency.Create(Tenant,Revision,A,B,DateTime.Now));
        Assert.Throws<DwsValidationException>(()=>new StructureBaseline{TenantId=Tenant,CreatedAtUtc=DateTime.Now,StructureDefinitionId=Guid.NewGuid(),SourceRevisionNumber=1,BaselineNumber=1,ContentHash=new('a',64),Snapshot="{}"});
    }

    [Fact] public void Hierarchy_validator_rejects_orphan_self_cycle_duplicate_order_and_code()
    {
        Assert.Throws<DwsValidationException>(()=>StructureNode.Create(Tenant,Revision,A,"A","A",null,0,A));
        Assert.Throws<DwsNotFoundException>(()=>DwsStructuralValidator.ValidateHierarchy(Tenant,Revision,[Node(A,Guid.NewGuid(),"A",0)]));
        Assert.Throws<DwsConflictException>(()=>DwsStructuralValidator.ValidateHierarchy(Tenant,Revision,[Node(A,B,"A",0),Node(B,A,"B",0)]));
        Assert.Equal(DwsErrors.DuplicateSiblingOrder,Assert.Throws<DwsConflictException>(()=>DwsStructuralValidator.ValidateHierarchy(Tenant,Revision,[Node(A,null,"A",0),Node(B,null,"B",0)])).Code);
        Assert.Equal(DwsErrors.DuplicateNodeCode,Assert.Throws<DwsConflictException>(()=>DwsStructuralValidator.ValidateHierarchy(Tenant,Revision,[Node(A,null,"A",0),Node(B,null,"A",1)])).Code);
        Assert.Throws<DwsNotFoundException>(()=>DwsStructuralValidator.ValidateHierarchy(Tenant,Revision,[StructureNode.Create(Guid.NewGuid(),Revision,null,"X","X",null,0,A)]));
        Assert.Throws<DwsNotFoundException>(()=>DwsStructuralValidator.ValidateHierarchy(Tenant,Revision,[StructureNode.Create(Tenant,Guid.NewGuid(),null,"X","X",null,0,A)]));
    }

    [Fact] public void Dependency_validator_rejects_missing_duplicate_and_multihop_cycle()
    {
        var nodes=new[]{Node(A,null,"A",0),Node(B,A,"B",0),Node(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),A,"C",1)};var c=nodes[2].LogicalNodeId;
        Assert.Throws<DwsNotFoundException>(()=>DwsStructuralValidator.ValidateDependencies(Tenant,Revision,nodes,[Dep(A,Guid.NewGuid())]));
        Assert.Equal(DwsErrors.DependencyCycle,Assert.Throws<DwsConflictException>(()=>DwsStructuralValidator.ValidateDependencies(Tenant,Revision,nodes,[Dep(A,B),Dep(A,B)])).Code);
        Assert.Equal(DwsErrors.DependencyCycle,Assert.Throws<DwsConflictException>(()=>DwsStructuralValidator.ValidateDependencies(Tenant,Revision,nodes,[Dep(A,B),Dep(B,c),Dep(c,A)])).Code);
        Assert.Throws<DwsNotFoundException>(()=>DwsStructuralValidator.ValidateDependencies(Tenant,Revision,nodes,[StructuralDependency.Create(Guid.NewGuid(),Revision,A,B,Utc)]));
        Assert.Throws<DwsNotFoundException>(()=>DwsStructuralValidator.ValidateDependencies(Tenant,Revision,nodes,[StructuralDependency.Create(Tenant,Guid.NewGuid(),A,B,Utc)]));
    }

    [Theory]
    [MemberData(nameof(CanonicalVectors))]
    public void Canonical_builder_produces_normative_bytes_and_hash(IReadOnlyDictionary<string,object?> projection,string bytes,string hash)
    {
        var actual=DwsCanonicalJson.Build(projection);Assert.Equal(bytes,actual.Text);Assert.Equal(hash,actual.Sha256);Assert.Throws<DwsValidationException>(()=>DwsCanonicalJson.Build(projection,"dws.request-canonical-json.v999"));
    }
    public static IEnumerable<object[]> CanonicalVectors()
    {
        yield return [new Dictionary<string,object?>{{"name","Cafe\u0301"}},"{\"name\":\"Café\"}","659906f125d844f7081786e4a1cba739414e49a9b9061d80ce09c691b5f56602"];
        yield return [new Dictionary<string,object?>{{"title","Root"},{"code","A"}},"{\"code\":\"A\",\"title\":\"Root\"}","de7a782be467c511dbef69310abe5208d9e2a480d76fddfad7f9b7fd267ad17b"];
        yield return [new Dictionary<string,object?>{{"name","Plan"},{"description",null}},"{\"description\":null,\"name\":\"Plan\"}","f4e250f367f7856aa340449797df4d6cb663581de459e4c960d9c4e519eb961a"];
        yield return [new Dictionary<string,object?>{{"name","Plan"}},"{\"name\":\"Plan\"}","fe153fe9078b057a070a5eb6a44e1542167d9545d6e136b623332b6289461f10"];
        yield return [new Dictionary<string,object?>{{"logicalNodeIds",new[]{A,B}}},"{\"logicalNodeIds\":[\"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\",\"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb\"]}","f7c9211f3bd38bd572071d4ecef84012def38159e7c297a5cbeb7280b110632b"];
        yield return [new Dictionary<string,object?>{{"logicalNodeIds",new[]{B,A}}},"{\"logicalNodeIds\":[\"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb\",\"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\"]}","e17fcc2f62e05a8a240c398f1c14b59b1752d1af2fc28f7db0ccd656a07099ef"];
        yield return [new Dictionary<string,object?>{{"description","Line 1\n\"quoted\"\\end"}},"{\"description\":\"Line 1\\n\\\"quoted\\\"\\\\end\"}","b78609a53164160720f2278fcc3d258a788baf525b308183aa2d5c5ebb598a0a"];
    }

    [Fact] public void Canonical_builder_rejects_normalized_property_collision_and_decimal_scale()
    {
        Assert.Throws<DwsValidationException>(()=>DwsCanonicalJson.Build(new Dictionary<string,object?>{{"Cafe\u0301",1},{"Café",2}}));
        Assert.Throws<DwsValidationException>(()=>DwsCanonicalJson.Build(new Dictionary<string,object?>{{"amount",1.00m}}));
        Assert.Throws<DwsValidationException>(()=>StructuralDependency.Create(Tenant,Revision,A,A,Utc));
        Assert.Equal(DwsErrors.InvalidUnicode,Assert.Throws<DwsValidationException>(()=>DwsCanonicalJson.Build(new Dictionary<string,object?>{{"name","bad\ud800"}})).Code);
        Assert.Equal(DwsErrors.InvalidUnicode,Assert.Throws<DwsValidationException>(()=>new StructuralMetadata("bad\udc00",null)).Code);
    }

    [Fact] public void Baseline_builder_produces_exact_751_byte_vector()
    {
        var nodes=new[]{Node(A,null,"A",0,"Root",null),Node(B,A,"B",0,"Node","Child")};var baseline=DwsBaselineBuilder.Build(Tenant,Guid.NewGuid(),1,1,new("ppm.external-context-reference","1.0",ExternalContextKind.Program,Guid.Parse("00112233-4455-6677-8899-aabbccddeeff")),new("Plan","v1"),nodes,[Dep(A,B)],Utc);
        Assert.Equal(751,System.Text.Encoding.UTF8.GetByteCount(baseline.Snapshot));Assert.Equal("1fcad71b78003f89414d23b7c203d544bfe70362b6fcf89c3500eade5a5217a7",baseline.ContentHash);
    }

    [Fact] public void Stable_outcome_is_closed_canonical_and_bounded()
    {
        var result=new Dictionary<string,object?>{{"StructureDefinitionId",Guid.NewGuid()},{"RevisionNumber",1},{"DefinitionVersion",1},{"RevisionVersion",1}};var code=DwsStableOutcome.DomainCode(DwsCommandFamily.CreateStructure,DwsOutcomeKind.Succeeded);var json=DwsStableOutcome.Build(DwsCommandFamily.CreateStructure,DwsOutcomeKind.Succeeded,code,result);
        Assert.StartsWith("{\"domainCode\":\"create_structure_succeeded\",\"outcomeKind\":\"succeeded\",\"result\":",json);DwsStableOutcome.Validate(DwsCommandFamily.CreateStructure,DwsOutcomeKind.Succeeded,code,json);Assert.Throws<DwsValidationException>(()=>DwsStableOutcome.Build(DwsCommandFamily.CreateStructure,DwsOutcomeKind.Succeeded,"invented",result));Assert.Throws<DwsValidationException>(()=>DwsStableOutcome.Validate(DwsCommandFamily.CreateStructure,DwsOutcomeKind.Succeeded,code,json.Replace("\"result\"","\"extra\"")));
    }

    [Fact] public async Task Replay_authorizes_and_checks_visibility_before_subject_or_payload()
    {
        var events=new List<string>();var guard=new Guard(events);var tenant=Tenant;var subject=Guid.NewGuid();var hash=new string('a',64);var result=new Dictionary<string,object?>{{"StructureDefinitionId",Guid.NewGuid()},{"RevisionNumber",1},{"DefinitionVersion",1},{"RevisionVersion",1}};var code=DwsStableOutcome.DomainCode(DwsCommandFamily.CreateStructure,DwsOutcomeKind.Succeeded);var outcome=DwsStableOutcome.Build(DwsCommandFamily.CreateStructure,DwsOutcomeKind.Succeeded,code,result);var receipt=new DwsIdempotencyReceipt(new(tenant,DwsCommandFamily.CreateStructure,"key"),subject,hash,DwsCanonicalJson.RequestVersion,DwsStableOutcome.Version,DwsOutcomeKind.Succeeded,code,outcome,Utc);
        var ex=await Assert.ThrowsAsync<DwsConflictException>(()=>DwsReceiptPolicy.ReplayAsync(receipt,tenant,Guid.NewGuid(),DwsCommandFamily.CreateStructure,"key",hash,guard,default));Assert.Equal(DwsErrors.IdempotencySubjectConflict,ex.Code);Assert.Equal(["permission","visibility"],events);
        ex=await Assert.ThrowsAsync<DwsConflictException>(()=>DwsReceiptPolicy.ReplayAsync(receipt,tenant,subject,DwsCommandFamily.CreateStructure,"key",new string('b',64),new Guard([]),default));Assert.Equal(DwsErrors.IdempotencyConflict,ex.Code);
    }

    [Fact] public void Receipt_hash_and_stable_outcome_field_types_are_exact()
    {
        var family=DwsCommandFamily.CreateStructure;var kind=DwsOutcomeKind.Succeeded;var code=DwsStableOutcome.DomainCode(family,kind);var valid=new Dictionary<string,object?>{{"StructureDefinitionId",A},{"RevisionNumber",1},{"DefinitionVersion",1},{"RevisionVersion",1}};var json=DwsStableOutcome.Build(family,kind,code,valid);
        var receipt=new DwsIdempotencyReceipt(new(Tenant,family,"key"),B,new string('a',64),DwsCanonicalJson.RequestVersion,DwsStableOutcome.Version,kind,code,json,Utc);Assert.Equal(json,receipt.StableOutcomeJson);DwsStableOutcome.Validate(family,kind,code,json);
        Assert.Throws<DwsValidationException>(()=>new DwsIdempotencyReceipt(new(Tenant,family,"key"),B,"",DwsCanonicalJson.RequestVersion,DwsStableOutcome.Version,kind,code,json,Utc));
        Assert.Throws<DwsValidationException>(()=>new DwsIdempotencyReceipt(new(Tenant,family,"key"),B,new string('A',64),DwsCanonicalJson.RequestVersion,DwsStableOutcome.Version,kind,code,json,Utc));
        AssertInvalid(new Dictionary<string,object?>(valid){["RevisionNumber"]="1"});AssertInvalid(new Dictionary<string,object?>(valid){["RevisionNumber"]=0});AssertInvalid(new Dictionary<string,object?>(valid){["StructureDefinitionId"]=null});
        Assert.Throws<DwsValidationException>(()=>DwsStableOutcome.Validate(family,kind,code,json.Replace("\"RevisionNumber\":1","\"RevisionNumber\":\"1\"",StringComparison.Ordinal)));Assert.Throws<DwsValidationException>(()=>DwsStableOutcome.Validate(family,kind,code,json.Replace($"\"StructureDefinitionId\":\"{A:D}\"","\"StructureDefinitionId\":null",StringComparison.Ordinal)));
        var removeFamily=DwsCommandFamily.RemoveStructureNode;var removeCode=DwsStableOutcome.DomainCode(removeFamily,kind);var validRemove=new Dictionary<string,object?>{{"StructureDefinitionId",A},{"RevisionNumber",1},{"LogicalNodeId",B},{"Removed",true},{"RevisionVersion",1}};var removeJson=DwsStableOutcome.Build(removeFamily,kind,removeCode,validRemove);DwsStableOutcome.Validate(removeFamily,kind,removeCode,removeJson);Assert.Throws<DwsValidationException>(()=>DwsStableOutcome.Validate(removeFamily,kind,removeCode,removeJson.Replace("\"Removed\":true","\"Removed\":\"true\"",StringComparison.Ordinal)));
        var remove=new Dictionary<string,object?>(validRemove){["Removed"]="true"};Assert.Throws<DwsValidationException>(()=>DwsStableOutcome.Build(removeFamily,kind,removeCode,remove));
        var baselineFamily=DwsCommandFamily.CreateStructureBaseline;var baselineCode=DwsStableOutcome.DomainCode(baselineFamily,kind);var baseline=new Dictionary<string,object?>{{"StructureDefinitionId",A},{"SourceRevisionNumber",1},{"BaselineNumber",1},{"ContentHash",new string('A',64)},{"CanonicalizationVersion",StructureBaseline.CanonicalVersion},{"DefinitionVersion",1}};Assert.Throws<DwsValidationException>(()=>DwsStableOutcome.Build(baselineFamily,kind,baselineCode,baseline));
        void AssertInvalid(Dictionary<string,object?> result)=>Assert.Throws<DwsValidationException>(()=>DwsStableOutcome.Build(family,kind,code,result));
    }

    [Fact] public void Comparison_is_deterministic_by_logical_identity()
    {
        var left=new[]{Node(A,null,"A",0),Node(B,A,"B",0)};var right=new[]{Node(A,null,"A",0),Node(B,null,"B",1)};var result=DwsComparison.Compare(left,right,[Dep(A,B)],[]);Assert.Equal("moved",Assert.Single(result.Nodes).Kind);Assert.Equal("removed",Assert.Single(result.Dependencies).Kind);
    }

    [Fact] public void Error_matrix_and_non_disclosing_tenant_boundary_are_exact(){Assert.Equal(new[]{400,401,403,404,409,503},DwsErrors.Matrix.Keys.Order().ToArray());var entity=new StructureDefinition{TenantId=Tenant,ExternalContextReference=Context()};Assert.Throws<DwsNotFoundException>(()=>DwsTenantBoundary.RequireVisible(Guid.NewGuid(),entity));}

    [Fact] public void Every_command_and_query_validator_fails_closed_on_invalid_identity_or_range()
    {
        IDwsRequestContract[] invalid=[new UpdateStructureMetadataCommand(Guid.Empty,"x",null,1),new AddStructureNodeCommand(Guid.Empty,null,"x","x",null,0,1),new MoveStructureNodeCommand(Guid.NewGuid(),Guid.Empty,null,0,1),new ReorderStructureNodeCommand(Guid.NewGuid(),Guid.NewGuid(),-1,1),new RemoveStructureNodeCommand(Guid.NewGuid(),Guid.Empty,1),new AddStructuralDependencyCommand(Guid.NewGuid(),A,A,1),new RemoveStructuralDependencyCommand(Guid.NewGuid(),Guid.Empty,B,1),new CreateStructureBaselineCommand(Guid.NewGuid(),0),new CreateNextStructureRevisionCommand(Guid.NewGuid(),null,null,1),new GetStructureByIdQuery(Guid.Empty),new GetStructureTreeQuery(Guid.NewGuid(),0),new ValidateStructureQuery(Guid.NewGuid(),-1),new CompareStructureRevisionsQuery(Guid.NewGuid(),1,1),new CompareStructureBaselinesQuery(Guid.NewGuid(),0,1)];
        Assert.All(invalid,x=>Assert.Throws<DwsValidationException>(x.Validate));
    }

    private static ExternalContextReference Context()=>new("ppm.external-context-reference","1.0",ExternalContextKind.Project,Guid.NewGuid());
    private static StructureNode Node(Guid id,Guid? parent,string code,int order,string? title=null,string? description=null)=>StructureNode.Create(Tenant,Revision,parent,code,title??code,description,order,id);
    private static StructuralDependency Dep(Guid from,Guid to)=>StructuralDependency.Create(Tenant,Revision,from,to,Utc);
    private sealed class Guard(List<string> events):IDwsReplayGuard{public Task RequirePermissionAsync(CancellationToken _){events.Add("permission");return Task.CompletedTask;}public Task RequireVisibleAsync(CancellationToken _){events.Add("visibility");return Task.CompletedTask;}}
}
