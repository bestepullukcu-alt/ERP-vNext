using System.Text;
using System.Text.Json;
using Diten.ManagementGovernanceService.Domain.Modules.Dws;

namespace Diten.ManagementGovernanceService.Application.Modules.Dws;

public sealed record DwsOperationPermission(string Operation, string Permission);
public static class DwsAuthorizationManifest
{
    public static IReadOnlyList<DwsOperationPermission> Entries { get; } = new DwsOperationPermission[]
    {
        new("CreateStructureCommand","management-governance.dws.create"),new("UpdateStructureMetadataCommand","management-governance.dws.update"),new("AddStructureNodeCommand","management-governance.dws.update"),new("MoveStructureNodeCommand","management-governance.dws.update"),new("ReorderStructureNodeCommand","management-governance.dws.update"),new("RemoveStructureNodeCommand","management-governance.dws.update"),new("AddStructuralDependencyCommand","management-governance.dws.update"),new("RemoveStructuralDependencyCommand","management-governance.dws.update"),new("CreateStructureBaselineCommand","management-governance.dws.baseline"),new("CreateNextStructureRevisionCommand","management-governance.dws.update"),new("GetStructureByIdQuery","management-governance.dws.read"),new("GetStructureTreeQuery","management-governance.dws.read"),new("ValidateStructureQuery","management-governance.dws.validate"),new("CompareStructureRevisionsQuery","management-governance.dws.compare"),new("CompareStructureBaselinesQuery","management-governance.dws.compare")
    };
    public static string RequireExact(string operation) { var m=Entries.Where(x=>x.Operation==operation).ToArray(); return m.Length==1?m[0].Permission:throw new DwsValidationException(DwsErrors.InvalidRequest); }
}

public enum DwsCommandFamily { CreateStructure, UpdateStructureMetadata, AddStructureNode, MoveStructureNode, ReorderStructureNode, RemoveStructureNode, AddStructuralDependency, RemoveStructuralDependency, CreateStructureBaseline, CreateNextStructureRevision }
public enum DwsOutcomeKind { Succeeded, NoOp }
public static class DwsClosedValues
{
    public static string Outcome(DwsOutcomeKind value)=>value switch{DwsOutcomeKind.Succeeded=>"succeeded",DwsOutcomeKind.NoOp=>"no-op",_=>throw new DwsValidationException(DwsErrors.InvalidStableOutcome)};
    public static DwsCommandFamily Command(string value)=>Enum.TryParse<DwsCommandFamily>(value,false,out var family)&&family.ToString()==value?family:throw new DwsValidationException(DwsErrors.InvalidRequest);
}

public interface IDwsRequestContract { void Validate(); }
public sealed record CreateStructureCommand(ExternalContextReference ExternalContextReference,string Name,string? Description):IDwsRequestContract { public void Validate(){_ = ExternalContextReference??throw new DwsValidationException(DwsErrors.InvalidContextReference);_ = new StructuralMetadata(Name,Description);} }
public sealed record UpdateStructureMetadataCommand(Guid StructureDefinitionId,string Name,string? Description,int ExpectedRevisionVersion):IDwsRequestContract { public void Validate(){DwsRequestValidation.IdentityVersion(StructureDefinitionId,ExpectedRevisionVersion);_ = new StructuralMetadata(Name,Description);} }
public sealed record AddStructureNodeCommand(Guid StructureDefinitionId,Guid? ParentLogicalNodeId,string Code,string Title,string? Description,int SiblingOrder,int ExpectedRevisionVersion):IDwsRequestContract { public void Validate(){DwsRequestValidation.IdentityVersion(StructureDefinitionId,ExpectedRevisionVersion);DwsRequestValidation.Order(SiblingOrder);_ = DwsText.Required(Code,100);_ = DwsText.Required(Title,300);_ = DwsText.Optional(Description,4000);} }
public sealed record MoveStructureNodeCommand(Guid StructureDefinitionId,Guid LogicalNodeId,Guid? NewParentLogicalNodeId,int NewSiblingOrder,int ExpectedRevisionVersion):IDwsRequestContract { public void Validate(){DwsRequestValidation.IdentityVersion(StructureDefinitionId,ExpectedRevisionVersion);DwsRequestValidation.Identity(LogicalNodeId);DwsRequestValidation.Order(NewSiblingOrder);if(NewParentLogicalNodeId==LogicalNodeId)throw new DwsValidationException(DwsErrors.InvalidStructure);} }
public sealed record ReorderStructureNodeCommand(Guid StructureDefinitionId,Guid LogicalNodeId,int SiblingOrder,int ExpectedRevisionVersion):IDwsRequestContract { public void Validate(){DwsRequestValidation.IdentityVersion(StructureDefinitionId,ExpectedRevisionVersion);DwsRequestValidation.Identity(LogicalNodeId);DwsRequestValidation.Order(SiblingOrder);} }
public sealed record RemoveStructureNodeCommand(Guid StructureDefinitionId,Guid LogicalNodeId,int ExpectedRevisionVersion):IDwsRequestContract { public void Validate(){DwsRequestValidation.IdentityVersion(StructureDefinitionId,ExpectedRevisionVersion);DwsRequestValidation.Identity(LogicalNodeId);} }
public sealed record AddStructuralDependencyCommand(Guid StructureDefinitionId,Guid FromLogicalNodeId,Guid ToLogicalNodeId,int ExpectedRevisionVersion):IDwsRequestContract { public void Validate(){DwsRequestValidation.Dependency(StructureDefinitionId,FromLogicalNodeId,ToLogicalNodeId,ExpectedRevisionVersion);} }
public sealed record RemoveStructuralDependencyCommand(Guid StructureDefinitionId,Guid FromLogicalNodeId,Guid ToLogicalNodeId,int ExpectedRevisionVersion):IDwsRequestContract { public void Validate(){DwsRequestValidation.Dependency(StructureDefinitionId,FromLogicalNodeId,ToLogicalNodeId,ExpectedRevisionVersion);} }
public sealed record CreateStructureBaselineCommand(Guid StructureDefinitionId,int ExpectedRevisionVersion):IDwsRequestContract { public void Validate()=>DwsRequestValidation.IdentityVersion(StructureDefinitionId,ExpectedRevisionVersion); }
public sealed record CreateNextStructureRevisionCommand(Guid StructureDefinitionId,int? SourceRevisionNumber,int? SourceBaselineNumber,int ExpectedDefinitionVersion)
    : IDwsRequestContract
{
    public void Validate(){DwsRequestValidation.IdentityVersion(StructureDefinitionId,ExpectedDefinitionVersion);if((SourceRevisionNumber is null)==(SourceBaselineNumber is null)||(SourceRevisionNumber is int revision&&revision<=0)||(SourceBaselineNumber is int baseline&&baseline<=0))throw new DwsValidationException(DwsErrors.InvalidRequest);}
}
public sealed record GetStructureByIdQuery(Guid StructureDefinitionId):IDwsRequestContract { public void Validate()=>DwsRequestValidation.Identity(StructureDefinitionId); }
public sealed record GetStructureTreeQuery(Guid StructureDefinitionId,int? RevisionNumber):IDwsRequestContract { public void Validate(){DwsRequestValidation.Identity(StructureDefinitionId);DwsRequestValidation.OptionalPositive(RevisionNumber);} }
public sealed record ValidateStructureQuery(Guid StructureDefinitionId,int? RevisionNumber):IDwsRequestContract { public void Validate(){DwsRequestValidation.Identity(StructureDefinitionId);DwsRequestValidation.OptionalPositive(RevisionNumber);} }
public sealed record CompareStructureRevisionsQuery(Guid StructureDefinitionId,int LeftRevisionNumber,int RightRevisionNumber):IDwsRequestContract { public void Validate(){DwsRequestValidation.Identity(StructureDefinitionId);DwsRequestValidation.PositivePair(LeftRevisionNumber,RightRevisionNumber);} }
public sealed record CompareStructureBaselinesQuery(Guid StructureDefinitionId,int LeftBaselineNumber,int RightBaselineNumber):IDwsRequestContract { public void Validate(){DwsRequestValidation.Identity(StructureDefinitionId);DwsRequestValidation.PositivePair(LeftBaselineNumber,RightBaselineNumber);} }
public static class DwsRequestValidation
{
    public static void Identity(Guid id){if(id==Guid.Empty)throw new DwsValidationException(DwsErrors.InvalidRequest);}
    public static void IdentityVersion(Guid id,int version){Identity(id);if(version<=0)throw new DwsValidationException(DwsErrors.InvalidRequest);}
    public static void Order(int order){if(order<0)throw new DwsValidationException(DwsErrors.InvalidStructure);}
    public static void OptionalPositive(int? value){if(value<=0)throw new DwsValidationException(DwsErrors.InvalidRequest);}
    public static void PositivePair(int left,int right){if(left<=0||right<=0||left==right)throw new DwsValidationException(DwsErrors.InvalidRequest);}
    public static void Dependency(Guid definition,Guid from,Guid to,int version){IdentityVersion(definition,version);Identity(from);Identity(to);if(from==to)throw new DwsValidationException(DwsErrors.InvalidStructure);}
}

public sealed record DwsIdempotencyIdentity(Guid TenantId,DwsCommandFamily CommandFamily,string IdempotencyKey);
public sealed record DwsIdempotencyReceipt
{
    public DwsIdempotencyIdentity Identity { get; }
    public Guid SecuritySubjectId { get; }
    public string RequestPayloadHash { get; }
    public string RequestCanonicalizationVersion { get; }
    public string OutcomeSchemaVersion { get; }
    public DwsOutcomeKind OutcomeKind { get; }
    public string DomainCode { get; }
    public string StableOutcomeJson { get; }
    public DateTime CreatedAtUtc { get; }
    public DwsIdempotencyReceipt(DwsIdempotencyIdentity identity,Guid securitySubjectId,string requestPayloadHash,string requestCanonicalizationVersion,string outcomeSchemaVersion,DwsOutcomeKind outcomeKind,string domainCode,string stableOutcomeJson,DateTime createdAtUtc)
    {
        Identity=identity;SecuritySubjectId=securitySubjectId;RequestPayloadHash=requestPayloadHash;RequestCanonicalizationVersion=requestCanonicalizationVersion;OutcomeSchemaVersion=outcomeSchemaVersion;OutcomeKind=outcomeKind;DomainCode=domainCode;StableOutcomeJson=stableOutcomeJson;CreatedAtUtc=createdAtUtc;Validate();
    }
    public void Validate()
    {
        if(Identity.TenantId==Guid.Empty||SecuritySubjectId==Guid.Empty||string.IsNullOrWhiteSpace(Identity.IdempotencyKey)||CreatedAtUtc.Kind!=DateTimeKind.Utc)throw new DwsValidationException(DwsErrors.InvalidRequest);
        if(!DwsStableOutcome.IsSha256(RequestPayloadHash))throw new DwsValidationException(DwsErrors.InvalidRequest);
        _=DwsClosedValues.Command(Identity.CommandFamily.ToString());_=DwsClosedValues.Outcome(OutcomeKind);
        if(RequestCanonicalizationVersion!=DwsCanonicalJson.RequestVersion||OutcomeSchemaVersion!=DwsStableOutcome.Version)throw new DwsValidationException(DwsErrors.UnknownCanonicalizationVersion);
        DwsStableOutcome.Validate(Identity.CommandFamily,OutcomeKind,DomainCode,StableOutcomeJson);
    }
}
public sealed record DwsAuditIntent(Guid TenantId,Guid AuditIntentId,Guid ActorId,string EntityType,string EntityId,string Mutation,DateTime OccurredAtUtc);
public sealed record DwsOutboxMessage(Guid TenantId,Guid EventId,Guid AuditIntentId,string DeliveryState,DateTime? NextAttemptAtUtc);

public static class DwsStableOutcome
{
    public const string Version="dws.idempotency-outcome.v1"; public const int MaximumBytes=4096;
    private static readonly IReadOnlyDictionary<DwsCommandFamily,IReadOnlySet<string>> Fields=new Dictionary<DwsCommandFamily,IReadOnlySet<string>>
    {
        [DwsCommandFamily.CreateStructure]=Set("StructureDefinitionId","RevisionNumber","DefinitionVersion","RevisionVersion"),[DwsCommandFamily.UpdateStructureMetadata]=Set("StructureDefinitionId","RevisionNumber","RevisionVersion"),[DwsCommandFamily.AddStructureNode]=Set("StructureDefinitionId","RevisionNumber","LogicalNodeId","RevisionVersion"),[DwsCommandFamily.MoveStructureNode]=Set("StructureDefinitionId","RevisionNumber","LogicalNodeId","ParentLogicalNodeId","SiblingOrder","RevisionVersion"),[DwsCommandFamily.ReorderStructureNode]=Set("StructureDefinitionId","RevisionNumber","LogicalNodeId","SiblingOrder","RevisionVersion"),[DwsCommandFamily.RemoveStructureNode]=Set("StructureDefinitionId","RevisionNumber","LogicalNodeId","Removed","RevisionVersion"),[DwsCommandFamily.AddStructuralDependency]=Set("StructureDefinitionId","RevisionNumber","FromLogicalNodeId","ToLogicalNodeId","RevisionVersion"),[DwsCommandFamily.RemoveStructuralDependency]=Set("StructureDefinitionId","RevisionNumber","FromLogicalNodeId","ToLogicalNodeId","Removed","RevisionVersion"),[DwsCommandFamily.CreateStructureBaseline]=Set("StructureDefinitionId","SourceRevisionNumber","BaselineNumber","ContentHash","CanonicalizationVersion","DefinitionVersion"),[DwsCommandFamily.CreateNextStructureRevision]=Set("StructureDefinitionId","NewRevisionNumber","DefinitionVersion","RevisionVersion")
    };
    private static HashSet<string> Set(params string[] values)=>new(values,StringComparer.Ordinal);
    private static readonly IReadOnlyDictionary<DwsCommandFamily,IReadOnlySet<string>> DomainCodes=Enum.GetValues<DwsCommandFamily>().ToDictionary(x=>x,x=>(IReadOnlySet<string>)Set(ToSnake(x)+"_succeeded",ToSnake(x)+"_no_op"));
    private static string ToSnake(DwsCommandFamily family)=>string.Concat(family.ToString().Select((c,i)=>(char.IsUpper(c)&&i>0?"_":"")+char.ToLowerInvariant(c)));
    public static string DomainCode(DwsCommandFamily family,DwsOutcomeKind kind){if(!Fields.ContainsKey(family))throw new DwsValidationException(DwsErrors.InvalidStableOutcome);_=DwsClosedValues.Outcome(kind);return ToSnake(family)+(kind==DwsOutcomeKind.Succeeded?"_succeeded":"_no_op");}
    public static string Build(DwsCommandFamily family,DwsOutcomeKind kind,string domainCode,IReadOnlyDictionary<string,object?> result)
    {
        if(!DomainCodes.TryGetValue(family,out var codes)||!Fields.TryGetValue(family,out var fields)||!codes.Contains(domainCode)||domainCode.Any(c=>c>127)||!fields.SetEquals(result.Keys))throw new DwsValidationException(DwsErrors.InvalidStableOutcome);
        ValidateTypedResult(family,result);
        var canonical=DwsCanonicalJson.Build(new Dictionary<string,object?>{{"domainCode",domainCode},{"outcomeKind",DwsClosedValues.Outcome(kind)},{"result",result}});
        if(canonical.Bytes.Length>MaximumBytes)throw new DwsValidationException(DwsErrors.InvalidStableOutcome); return canonical.Text;
    }
    public static void Validate(DwsCommandFamily family,DwsOutcomeKind kind,string domainCode,string json)
    {
        if(string.IsNullOrEmpty(json)||Encoding.UTF8.GetByteCount(json)>MaximumBytes)throw new DwsValidationException(DwsErrors.InvalidStableOutcome);
        try
        {
            using var document=JsonDocument.Parse(json,new JsonDocumentOptions{CommentHandling=JsonCommentHandling.Disallow,AllowTrailingCommas=false});var root=document.RootElement;
            if(root.ValueKind!=JsonValueKind.Object)throw new DwsValidationException(DwsErrors.InvalidStableOutcome);
            var properties=root.EnumerateObject().ToArray();if(properties.Length!=3||properties.Select(x=>x.Name).Distinct(StringComparer.Ordinal).Count()!=3||properties[0].Name!="domainCode"||properties[1].Name!="outcomeKind"||properties[2].Name!="result")throw new DwsValidationException(DwsErrors.InvalidStableOutcome);
            if(properties[0].Value.ValueKind!=JsonValueKind.String||properties[1].Value.ValueKind!=JsonValueKind.String||properties[0].Value.GetString()!=domainCode||properties[1].Value.GetString()!=DwsClosedValues.Outcome(kind)||properties[2].Value.ValueKind!=JsonValueKind.Object)throw new DwsValidationException(DwsErrors.InvalidStableOutcome);
            var result=new Dictionary<string,object?>(StringComparer.Ordinal);foreach(var property in properties[2].Value.EnumerateObject()){if(!result.TryAdd(property.Name,ParseValue(property.Name,property.Value)))throw new DwsValidationException(DwsErrors.InvalidStableOutcome);}
            if(Build(family,kind,domainCode,result)!=json)throw new DwsValidationException(DwsErrors.InvalidStableOutcome);
        }
        catch(JsonException){throw new DwsValidationException(DwsErrors.InvalidStableOutcome);}
    }
    private static object? ParseValue(string name,JsonElement value)=>value.ValueKind switch
    {
        JsonValueKind.Null=>null,
        JsonValueKind.True=>true,
        JsonValueKind.False=>false,
        JsonValueKind.Number when value.TryGetInt32(out var number)=>number,
        JsonValueKind.String when name.EndsWith("Id",StringComparison.Ordinal)&&Guid.TryParseExact(value.GetString(),"D",out var id)=>id,
        JsonValueKind.String=>value.GetString(),
        _=>throw new DwsValidationException(DwsErrors.InvalidStableOutcome)
    };
    public static bool IsSha256(string? value)=>value is { Length:64 }&&value.All(c=>c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static void ValidateTypedResult(DwsCommandFamily family,IReadOnlyDictionary<string,object?> result)
    {
        foreach(var (name,value) in result)
        {
            var valid=name switch
            {
                "Removed"=>value is true,
                "ContentHash"=>value is string hash&&IsSha256(hash),
                "CanonicalizationVersion"=>value is string version&&version==StructureBaseline.CanonicalVersion,
                "ParentLogicalNodeId"=>value is null||value is Guid parent&&parent!=Guid.Empty,
                "SiblingOrder"=>value is int order&&order>=0,
                "RevisionNumber" when family==DwsCommandFamily.CreateStructure=>value is int first&&first==1,
                "RevisionNumber" or "SourceRevisionNumber" or "BaselineNumber" or "NewRevisionNumber" or "DefinitionVersion" or "RevisionVersion"=>value is int number&&number>0,
                _ when name.EndsWith("Id",StringComparison.Ordinal)=>value is Guid id&&id!=Guid.Empty,
                _=>false
            };
            if(!valid)throw new DwsValidationException(DwsErrors.InvalidStableOutcome);
        }
    }
}

public interface IDwsReplayGuard{Task RequirePermissionAsync(CancellationToken cancellationToken);Task RequireVisibleAsync(CancellationToken cancellationToken);}
public static class DwsReceiptPolicy
{
    public static async Task<DwsIdempotencyReceipt> ReplayAsync(DwsIdempotencyReceipt receipt,Guid tenantId,Guid subjectId,DwsCommandFamily commandFamily,string key,string payloadHash,IDwsReplayGuard guard,CancellationToken cancellationToken)
    {
        await guard.RequirePermissionAsync(cancellationToken); await guard.RequireVisibleAsync(cancellationToken);
        receipt.Validate();
        if(receipt.Identity.TenantId!=tenantId||receipt.Identity.CommandFamily!=commandFamily||receipt.Identity.IdempotencyKey!=key)throw new DwsConflictException(DwsErrors.IdempotencyConflict);
        if(receipt.SecuritySubjectId!=subjectId)throw new DwsConflictException(DwsErrors.IdempotencySubjectConflict);
        if(receipt.RequestPayloadHash!=payloadHash)throw new DwsConflictException(DwsErrors.IdempotencyConflict);
        return receipt;
    }
}

public interface IDwsTransactionSession{bool IsActive{get;}Task CommitAsync(CancellationToken cancellationToken);Task AbortAsync(CancellationToken cancellationToken);} public interface IDwsTransactionSessionFactory{Task<IDwsTransactionSession> BeginAsync(CancellationToken cancellationToken);} public interface IDwsReceiptReconciler<T>{Task<DwsReconciliation<T>> ReconcileAsync(CancellationToken cancellationToken);} public sealed record DwsReconciliation<T>(bool IsDurableMatch,bool IsConflict,T? Value); public sealed class DwsUnknownCommitException:Exception;
public sealed class DwsTransactionCoordinator(IDwsTransactionSessionFactory sessions)
{
    public async Task<T> ExecuteAsync<T>(Func<IDwsTransactionSession,CancellationToken,Task<T>> body,IDwsReceiptReconciler<T> reconciler,CancellationToken token)
    {
        var session=await sessions.BeginAsync(token); if(!session.IsActive)throw new DwsValidationException(DwsErrors.TransactionUnavailable); T result; try{result=await body(session,token);}catch{await session.AbortAsync(token);throw;}
        for(var attempt=1;attempt<=3;attempt++)try{await session.CommitAsync(token);return result;}catch(DwsUnknownCommitException)when(attempt<3){}catch(DwsUnknownCommitException){break;}
        var r=await reconciler.ReconcileAsync(token); if(r.IsDurableMatch&&r.Value is not null)return r.Value;if(r.IsConflict)throw new DwsConflictException(DwsErrors.IdempotencyConflict);throw new DwsValidationException(DwsErrors.CommitIndeterminate);
    }
}
