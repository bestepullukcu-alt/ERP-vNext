using Diten.ManagementGovernanceService.Application.Modules.ProcessModeling;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.ManagementGovernanceService.Persistence.Modules.ProcessModeling;

public sealed class ProcessModelingMongoContext
{
    private readonly IMongoDatabase _database;
    public ProcessModelingMongoContext(IMongoClient client, string databaseName)
    { Client=client??throw new ArgumentNullException(nameof(client));if(!ProcessModelingMongoGuidPolicy.IsCanonical(client))throw new InvalidOperationException("process_modeling_uuid_policy_incompatible");if(string.IsNullOrWhiteSpace(databaseName)) throw new ArgumentException(nameof(databaseName)); _database=client.GetDatabase(databaseName); }
    public IMongoClient Client { get; }
    public IMongoCollection<BsonDocument> Collection(string name)
    { if(!ProcessModelingPersistenceManifest.Collections.Contains(name,StringComparer.Ordinal)) throw new InvalidOperationException("process_modeling_collection_forbidden"); return _database.GetCollection<BsonDocument>(name); }
}

public sealed class ProcessModelingMongoIndexInitializer(ProcessModelingMongoContext context)
{
    public async Task InitializeAsync(CancellationToken ct=default)
    {
        foreach(var index in ProcessModelingPersistenceManifest.Indexes)
        {
            var options=new CreateIndexOptions<BsonDocument> { Name=index.Name, Unique=index.Unique };
            if(index.PartialFilterJson is not null) options.PartialFilterExpression=new BsonDocumentFilterDefinition<BsonDocument>(BsonDocument.Parse(index.PartialFilterJson.Replace("'","\"",StringComparison.Ordinal)));
            await context.Collection(index.Collection).Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(new BsonDocument(index.Keys.Select(k=>new BsonElement(k,1))),options),cancellationToken:ct);
        }
    }
}

public sealed class ProcessModelingMongoRepository(ProcessModelingMongoContext context)
{
    public async Task<BsonDocument?> FindTenantAsync(string collection, Guid tenantId, Guid id, CancellationToken ct=default) =>
        await context.Collection(collection).Find(Builders<BsonDocument>.Filter.And(Builders<BsonDocument>.Filter.Eq("TenantId",G(tenantId)),Builders<BsonDocument>.Filter.Eq("_id",G(id)),Builders<BsonDocument>.Filter.Eq("IsDeleted",false))).FirstOrDefaultAsync(ct);

    public async Task CasAsync(IClientSessionHandle session,string collection,Guid tenantId,Guid id,int expectedVersion,BsonDocument values,CancellationToken ct)
    {
        var filter=Builders<BsonDocument>.Filter.And(Builders<BsonDocument>.Filter.Eq("TenantId",G(tenantId)),Builders<BsonDocument>.Filter.Eq("_id",G(id)),Builders<BsonDocument>.Filter.Eq("Version",expectedVersion),Builders<BsonDocument>.Filter.Eq("IsDeleted",false));
        var update=new BsonDocument("$set",values).Add("$inc",new BsonDocument("Version",1));
        if((await context.Collection(collection).UpdateOneAsync(session,filter,update,cancellationToken:ct)).ModifiedCount!=1) throw new ProcessModelingConflictException("process_model_stale_version");
    }
    private static BsonBinaryData G(Guid value)=>ProcessModelingMongoGuidPolicy.Write(value);
}

public sealed class ProcessModelingMongoReceiptReader(ProcessModelingMongoContext context)
{
    public const string SchemaIncompatible = "process_model_receipt_schema_incompatible";

    public async Task<CoreMutationResult?> ReconcileAsync(CoreMutationRequest request,bool authorized,CancellationToken ct=default)
    {
        if(!authorized) return new(false,403,"process_model_permission_denied");
        var filter=Builders<BsonDocument>.Filter.And(Builders<BsonDocument>.Filter.Eq("TenantId",G(request.TenantId)),Builders<BsonDocument>.Filter.Eq("CommandFamily",request.CommandFamily),Builders<BsonDocument>.Filter.Eq("IdempotencyKey",request.IdempotencyKey));
        var receipt=await context.Collection("mg_process_modeling_idempotency_receipts").WithReadConcern(ReadConcern.Majority).Find(filter).FirstOrDefaultAsync(ct);if(receipt is null)return null;
        if(ProcessModelingMongoGuidPolicy.Read(receipt["SubjectId"])!=request.SubjectId||receipt["PayloadHash"].AsString!=request.CanonicalPayloadHash)return new(false,409,ProcessModelingErrors.IdempotencyConflict);
        if(!receipt.TryGetValue("HttpStatus",out var statusValue)||!statusValue.IsInt32||
           !receipt.TryGetValue("OutcomeEntityId",out var outcomeIdValue)||outcomeIdValue.BsonType!=BsonType.Binary)
            return new(false,503,SchemaIncompatible);
        var status=statusValue.AsInt32;
        var outcomeId=ProcessModelingMongoGuidPolicy.Read(outcomeIdValue);
        return new(true,status,receipt["Outcome"].AsString,outcomeId);
    }
    private static BsonBinaryData G(Guid value)=>ProcessModelingMongoGuidPolicy.Write(value);
}

public static class ProcessModelingMongoFailureMapper
{
    public static Exception Map(Exception error)=>error switch
    {
        MongoWriteException x when x.WriteError?.Category==ServerErrorCategory.DuplicateKey=>new ProcessModelingConflictException(ProcessModelingErrors.IdempotencyConflict),
        MongoException x when x.HasErrorLabel("TransientTransactionError")=>new ProcessModelingConflictException(ProcessModelingErrors.StaleVersion),
        _=>error
    };
}

public enum ProcessModelingMutationParticipant { Business=1,Receipt=2,AuditIntent=3,Outbox=4 }
public sealed record MongoMutationEnvelope(CoreMutationRequest Request,BsonDocument Business,string Outcome,Guid AuditIntentId,Guid EventId,DateTime CreatedAtUtc,string ActorProvenance,int HttpStatus=200,Guid? OutcomeEntityId=null);
public sealed class ProcessModelingMongoMutationWriter(ProcessModelingMongoContext context,Action<ProcessModelingMutationParticipant>? testOnlyFault=null)
{
    public const string AuditEventType = "management-governance.process-modeling-audit-intent.submitted.v1";
    public async Task WriteAsync(IClientSessionHandle session,MongoMutationEnvelope x,CancellationToken ct)
    {
        if(session is null||!session.IsInTransaction||!ReferenceEquals(session.Client,context.Client))throw new InvalidOperationException("process_modeling_session_required");
        if(x.CreatedAtUtc.Kind!=DateTimeKind.Utc||string.IsNullOrWhiteSpace(x.ActorProvenance)) throw new ArgumentException("invalid_provenance");
        await ApplyBusinessAsync(session,x,ct);
        testOnlyFault?.Invoke(ProcessModelingMutationParticipant.Business);
        var outcomeId=x.OutcomeEntityId??x.Request.AggregateId;
        await context.Collection("mg_process_modeling_idempotency_receipts").InsertOneAsync(session,new BsonDocument{{"TenantId",G(x.Request.TenantId)},{"SubjectId",G(x.Request.SubjectId)},{"CommandFamily",x.Request.CommandFamily},{"IdempotencyKey",x.Request.IdempotencyKey},{"PayloadHash",x.Request.CanonicalPayloadHash},{"Outcome",x.Outcome},{"HttpStatus",x.HttpStatus},{"AggregateId",G(x.Request.AggregateId)},{"OutcomeEntityId",G(outcomeId)},{"CreatedAtUtc",x.CreatedAtUtc},{"CompletedAtUtc",x.CreatedAtUtc}},cancellationToken:ct); testOnlyFault?.Invoke(ProcessModelingMutationParticipant.Receipt);
        var audit=ProcessModelingCommandContract.Audit(x.Request.CommandFamily);
        var auditEntityId=string.Equals(x.Request.CommandFamily,"CreateProcessModelRevisionCommand",StringComparison.Ordinal)?outcomeId:x.Request.AggregateId;
        var payload=new BsonDocument{{"auditIntentId",G(x.AuditIntentId)},{"actorId",G(x.Request.SubjectId)},{"entityType",audit.Entity},{"entityId",G(auditEntityId)},{"mutation",audit.Mutation},{"occurredAtUtc",x.CreatedAtUtc}};
        var metadata=new BsonDocument{{"tenantId",G(x.Request.TenantId)},{"permission",ProcessModelingPermissions.ExactCommandMap[x.Request.CommandFamily]},{"actorProvenance",x.ActorProvenance}};
        await context.Collection("mg_process_modeling_audit_intents").InsertOneAsync(session,new BsonDocument{{"TenantId",G(x.Request.TenantId)},{"AuditIntentId",G(x.AuditIntentId)},{"Payload",payload},{"TrustedMetadata",metadata},{"CreatedAtUtc",x.CreatedAtUtc}},cancellationToken:ct); testOnlyFault?.Invoke(ProcessModelingMutationParticipant.AuditIntent);
        await context.Collection("mg_process_modeling_outbox_messages").InsertOneAsync(session,new BsonDocument{{"TenantId",G(x.Request.TenantId)},{"EventId",G(x.EventId)},{"AggregateId",G(auditEntityId)},{"EventType",AuditEventType},{"EventVersion",1},{"Payload",payload},{"TrustedMetadata",metadata},{"CreatedAtUtc",x.CreatedAtUtc}},cancellationToken:ct); testOnlyFault?.Invoke(ProcessModelingMutationParticipant.Outbox);
    }
    private async Task ApplyBusinessAsync(IClientSessionHandle session,MongoMutationEnvelope x,CancellationToken ct)
    {
        var prepared=ProcessModelingMongoCommandManifest.Prepare(x);
        var spec=prepared.Spec;
        switch(spec.Operation)
        {
            case ProcessModelingMongoOperation.Create:
                await RequireActiveParentAsync(session,x.Request.TenantId,spec,prepared.Values,ct);
                await context.Collection(spec.Collection).InsertOneAsync(session,prepared.Values,cancellationToken:ct); break;
            case ProcessModelingMongoOperation.Update:
            case ProcessModelingMongoOperation.Archive:
            case ProcessModelingMongoOperation.Transition:
                await CasAsync(session,spec.Collection,x.Request,prepared.Values,spec.RequiredLifecycle,spec.NextLifecycle,ct); break;
            case ProcessModelingMongoOperation.DraftReplace:
                await CasAsync(session,spec.Collection,x.Request,prepared.Values,"Draft",null,ct);
                await ReplaceGraphAsync(session,x.Request.TenantId,x.Request.AggregateId,"mg_process_activities",prepared.Activities,ct);
                await ReplaceGraphAsync(session,x.Request.TenantId,x.Request.AggregateId,"mg_process_control_points",prepared.ControlPoints,ct);
                await ReplaceGraphAsync(session,x.Request.TenantId,x.Request.AggregateId,"mg_process_relationships",prepared.Relationships,ct);
                break;
            case ProcessModelingMongoOperation.CreateRevision:
                await CreateRevisionAsync(session,x,prepared,ct); break;
            case ProcessModelingMongoOperation.Retire:
                var version=await context.Collection(spec.Collection).Find(session,Builders<BsonDocument>.Filter.And(Builders<BsonDocument>.Filter.Eq("TenantId",G(x.Request.TenantId)),Builders<BsonDocument>.Filter.Eq("_id",G(x.Request.AggregateId)),Builders<BsonDocument>.Filter.Eq("IsDeleted",false))).FirstOrDefaultAsync(ct);
                if(version is null)throw new ProcessModelingNotFoundException(ProcessModelingErrors.MissingOrInvisible);
                await CasAsync(session,spec.Collection,x.Request,prepared.Values,"Published","Retired",ct);
                await ClearPublishedPointerAsync(session,x.Request.TenantId,ProcessModelingMongoGuidPolicy.Read(version["ProcessModelId"]),x.Request.AggregateId,ct);
                break;
            default: throw new InvalidOperationException("process_modeling_operation_forbidden");
        }
    }
    private async Task RequireActiveParentAsync(IClientSessionHandle session,Guid tenantId,ProcessModelingMongoCommandSpec spec,BsonDocument values,CancellationToken ct)
    {
        if(spec.ParentCollection is null||spec.ParentField is null)return;
        if(!values.TryGetValue(spec.ParentField,out var parentValue)||parentValue.BsonType!=BsonType.Binary)
            throw new ProcessModelingNotFoundException(ProcessModelingErrors.MissingOrInvisible);
        var filter=Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("TenantId",G(tenantId)),
            Builders<BsonDocument>.Filter.Eq("_id",parentValue),
            Builders<BsonDocument>.Filter.Eq("LifecycleState","Active"),
            Builders<BsonDocument>.Filter.Eq("IsDeleted",false));
        if(!await context.Collection(spec.ParentCollection).Find(session,filter).AnyAsync(ct))
            throw new ProcessModelingNotFoundException(ProcessModelingErrors.MissingOrInvisible);
    }
    private async Task CasAsync(IClientSessionHandle session,string collection,CoreMutationRequest request,BsonDocument values,string? requiredLifecycle,string? nextLifecycle,CancellationToken ct)
    {
        var filters=new List<FilterDefinition<BsonDocument>>{Builders<BsonDocument>.Filter.Eq("TenantId",G(request.TenantId)),Builders<BsonDocument>.Filter.Eq("_id",G(request.AggregateId)),Builders<BsonDocument>.Filter.Eq("Version",request.ExpectedVersion),Builders<BsonDocument>.Filter.Eq("IsDeleted",false)};
        if(requiredLifecycle is not null)filters.Add(Builders<BsonDocument>.Filter.Eq("LifecycleState",requiredLifecycle));
        var set=new BsonDocument(values.Where(e=>e.Name is not "_id" and not "TenantId" and not "Version" and not "IsDeleted")); if(nextLifecycle is not null)set["LifecycleState"]=nextLifecycle;
        var update=new BsonDocument("$set",set).Add("$inc",new BsonDocument("Version",1));
        if((await context.Collection(collection).UpdateOneAsync(session,Builders<BsonDocument>.Filter.And(filters),update,cancellationToken:ct)).ModifiedCount!=1)throw new ProcessModelingConflictException(ProcessModelingErrors.StaleVersion);
    }
    private async Task ReplaceGraphAsync(IClientSessionHandle session,Guid tenantId,Guid versionId,string collection,IReadOnlyList<BsonDocument> replacements,CancellationToken ct)
    {
        var filter=Builders<BsonDocument>.Filter.And(Builders<BsonDocument>.Filter.Eq("TenantId",G(tenantId)),Builders<BsonDocument>.Filter.Eq("ProcessModelVersionId",G(versionId)));
        await context.Collection(collection).DeleteManyAsync(session,filter,cancellationToken:ct);
        if(replacements.Count>0)await context.Collection(collection).InsertManyAsync(session,replacements,cancellationToken:ct);
    }
    private async Task CreateRevisionAsync(IClientSessionHandle session,MongoMutationEnvelope x,ProcessModelingPreparedMutation prepared,CancellationToken ct)
    {
        var modelId=x.Request.AggregateId;
        var filter=Builders<BsonDocument>.Filter.And(Builders<BsonDocument>.Filter.Eq("TenantId",G(x.Request.TenantId)),Builders<BsonDocument>.Filter.Eq("_id",G(modelId)),Builders<BsonDocument>.Filter.Eq("Version",x.Request.ExpectedVersion),Builders<BsonDocument>.Filter.Eq("IsDeleted",false),Builders<BsonDocument>.Filter.Or(Builders<BsonDocument>.Filter.Eq("OpenVersionId",BsonNull.Value),Builders<BsonDocument>.Filter.Exists("OpenVersionId",false)));
        var update=new BsonDocument("$set",new BsonDocument("OpenVersionId",G(prepared.OutcomeEntityId))).Add("$inc",new BsonDocument{{"Version",1},{"LatestRevisionNumber",1}});
        var model=await context.Collection("mg_process_models").FindOneAndUpdateAsync(session,filter,update,new FindOneAndUpdateOptions<BsonDocument>{ReturnDocument=ReturnDocument.After},ct)??throw new ProcessModelingConflictException(ProcessModelingErrors.StaleVersion);
        var version=prepared.Values.DeepClone().AsBsonDocument;version["RevisionNumber"]=model.GetValue("LatestRevisionNumber",1).ToInt32();
        await context.Collection(prepared.Spec.Collection).InsertOneAsync(session,version,cancellationToken:ct);
    }
    private async Task ClearPublishedPointerAsync(IClientSessionHandle session,Guid tenantId,Guid modelId,Guid versionId,CancellationToken ct)
    {
        var filter=Builders<BsonDocument>.Filter.And(Builders<BsonDocument>.Filter.Eq("TenantId",G(tenantId)),Builders<BsonDocument>.Filter.Eq("_id",G(modelId)),Builders<BsonDocument>.Filter.Eq("PublishedVersionId",G(versionId)),Builders<BsonDocument>.Filter.Eq("IsDeleted",false));
        var update=new BsonDocument("$set",new BsonDocument("PublishedVersionId",BsonNull.Value)).Add("$inc",new BsonDocument("Version",1));
        if((await context.Collection("mg_process_models").UpdateOneAsync(session,filter,update,cancellationToken:ct)).ModifiedCount!=1)throw new ProcessModelingConflictException(ProcessModelingErrors.StaleVersion);
    }
    private static BsonBinaryData G(Guid value)=>ProcessModelingMongoGuidPolicy.Write(value);
}

public sealed class ProcessModelingMongoTransactionRunner(ProcessModelingMongoContext context,int maxCommitAttempts=3,Func<IClientSessionHandle,int,CancellationToken,Task>? testOnlyCommit=null)
{
    public async Task<T> ExecuteAsync<T>(Func<IClientSessionHandle,CancellationToken,Task<T>> body,Func<CancellationToken,Task<T?>> reconcile,CancellationToken ct=default) where T:class
    {
        using var session=await context.Client.StartSessionAsync(cancellationToken:ct); session.StartTransaction(new TransactionOptions(ReadConcern.Snapshot,ReadPreference.Primary,WriteConcern.WMajority));
        T result; try { result=await body(session,ct); }
        catch(MongoWriteException ex) when(ex.WriteError?.Category==ServerErrorCategory.DuplicateKey)
        {
            await AbortSafelyAsync(session,ct);
            try { return await reconcile(ct)??throw new ProcessModelingConflictException(ProcessModelingErrors.IdempotencyConflict); }
            catch(OperationCanceledException){throw;}
            catch(ProcessModelingConflictException){throw;}
            catch(MongoException receiptError){throw new ProcessModelingUnavailableException(ProcessModelingErrors.TransactionIndeterminate+":"+receiptError.GetType().Name);}
        }
        catch(MongoException ex){try{if(session.IsInTransaction)await session.AbortTransactionAsync(ct);}catch(MongoException){}throw new ProcessModelingUnavailableException(ProcessModelingErrors.TransactionIndeterminate+":"+ex.GetType().Name);}
        catch { if(session.IsInTransaction) await session.AbortTransactionAsync(ct); throw; }
        for(var attempt=1;attempt<=maxCommitAttempts;attempt++) try { if(testOnlyCommit is null) await session.CommitTransactionAsync(ct); else await testOnlyCommit(session,attempt,ct); return result; }
        catch(ProcessModelingUnknownCommitException) when(attempt<maxCommitAttempts) { }
        catch(ProcessModelingUnknownCommitException) when(attempt==maxCommitAttempts) { break; }
        catch(MongoException ex) when(ex.HasErrorLabel("UnknownTransactionCommitResult")&&attempt<maxCommitAttempts) { }
        catch(MongoException ex) when(ex.HasErrorLabel("UnknownTransactionCommitResult")&&attempt==maxCommitAttempts) { break; }
        return await reconcile(ct) ?? throw new ProcessModelingUnavailableException("process_model_transaction_indeterminate");
    }

    private static async Task AbortSafelyAsync(IClientSessionHandle session,CancellationToken ct)
    {
        if(!session.IsInTransaction)return;
        try { await session.AbortTransactionAsync(ct); }
        catch(OperationCanceledException){throw;}
        catch(MongoException) { }
    }
}
public sealed class ProcessModelingConflictException(string code):Exception(code);
public sealed class ProcessModelingNotFoundException(string code):Exception(code);
public sealed class ProcessModelingUnavailableException(string code):Exception(code);
public sealed class ProcessModelingUnknownCommitException:Exception;
