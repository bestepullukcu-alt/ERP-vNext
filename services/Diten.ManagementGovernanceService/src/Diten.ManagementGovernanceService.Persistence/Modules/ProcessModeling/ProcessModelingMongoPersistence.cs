using Diten.ManagementGovernanceService.Application.Modules.ProcessModeling;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.ManagementGovernanceService.Persistence.Modules.ProcessModeling;

public sealed class ProcessModelingMongoContext
{
    private readonly IMongoDatabase _database;
    public ProcessModelingMongoContext(IMongoClient client, string databaseName)
    { Client=client??throw new ArgumentNullException(nameof(client)); if(string.IsNullOrWhiteSpace(databaseName)) throw new ArgumentException(nameof(databaseName)); _database=client.GetDatabase(databaseName); }
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
    private static BsonBinaryData G(Guid value)=>new(value,GuidRepresentation.Standard);
}

public sealed class ProcessModelingMongoReceiptReader(ProcessModelingMongoContext context)
{
    public async Task<CoreMutationResult?> ReconcileAsync(CoreMutationRequest request,bool authorized,CancellationToken ct=default)
    {
        if(!authorized) return new(false,403,"process_model_permission_denied");
        var filter=Builders<BsonDocument>.Filter.And(Builders<BsonDocument>.Filter.Eq("TenantId",G(request.TenantId)),Builders<BsonDocument>.Filter.Eq("CommandFamily",request.CommandFamily),Builders<BsonDocument>.Filter.Eq("IdempotencyKey",request.IdempotencyKey));
        var receipt=await context.Collection("mg_process_modeling_idempotency_receipts").Find(filter).FirstOrDefaultAsync(ct);if(receipt is null)return null;
        if(receipt["SubjectId"].AsBsonBinaryData.ToGuid(GuidRepresentation.CSharpLegacy)!=request.SubjectId||receipt["PayloadHash"].AsString!=request.CanonicalPayloadHash)return new(false,409,ProcessModelingErrors.IdempotencyConflict);
        return new(true,200,receipt["Outcome"].AsString,receipt["AggregateId"].AsGuid);
    }
    private static BsonBinaryData G(Guid value)=>new(value,GuidRepresentation.Standard);
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
public sealed record MongoMutationEnvelope(CoreMutationRequest Request,BsonDocument Business,string Outcome,Guid AuditIntentId,Guid EventId,DateTime CreatedAtUtc,string ActorProvenance);
public sealed class ProcessModelingMongoMutationWriter(ProcessModelingMongoContext context,Action<ProcessModelingMutationParticipant>? testOnlyFault=null)
{
    public async Task WriteAsync(IClientSessionHandle session,MongoMutationEnvelope x,CancellationToken ct)
    {
        if(string.Equals(x.Request.CommandFamily,PublishProcessModelVersionContract.CommandName,StringComparison.Ordinal)) throw new ProcessModelingUnavailableException("process_model_publish_second_slice_unavailable");
        if(x.CreatedAtUtc.Kind!=DateTimeKind.Utc||string.IsNullOrWhiteSpace(x.ActorProvenance)) throw new ArgumentException("invalid_provenance");
        await context.Collection("mg_process_models").InsertOneAsync(session,x.Business,cancellationToken:ct); testOnlyFault?.Invoke(ProcessModelingMutationParticipant.Business);
        await context.Collection("mg_process_modeling_idempotency_receipts").InsertOneAsync(session,new BsonDocument{{"TenantId",G(x.Request.TenantId)},{"SubjectId",G(x.Request.SubjectId)},{"CommandFamily",x.Request.CommandFamily},{"IdempotencyKey",x.Request.IdempotencyKey},{"PayloadHash",x.Request.CanonicalPayloadHash},{"Outcome",x.Outcome},{"AggregateId",G(x.Request.AggregateId)},{"CreatedAtUtc",x.CreatedAtUtc},{"CompletedAtUtc",x.CreatedAtUtc}},cancellationToken:ct); testOnlyFault?.Invoke(ProcessModelingMutationParticipant.Receipt);
        await context.Collection("mg_process_modeling_audit_intents").InsertOneAsync(session,new BsonDocument{{"TenantId",G(x.Request.TenantId)},{"AuditIntentId",G(x.AuditIntentId)},{"AggregateId",G(x.Request.AggregateId)},{"CommandFamily",x.Request.CommandFamily},{"ActorProvenance",x.ActorProvenance},{"CreatedAtUtc",x.CreatedAtUtc}},cancellationToken:ct); testOnlyFault?.Invoke(ProcessModelingMutationParticipant.AuditIntent);
        await context.Collection("mg_process_modeling_outbox_messages").InsertOneAsync(session,new BsonDocument{{"TenantId",G(x.Request.TenantId)},{"EventId",G(x.EventId)},{"AggregateId",G(x.Request.AggregateId)},{"EventType",x.Request.CommandFamily+".accepted"},{"CreatedAtUtc",x.CreatedAtUtc}},cancellationToken:ct); testOnlyFault?.Invoke(ProcessModelingMutationParticipant.Outbox);
    }
    private static BsonBinaryData G(Guid value)=>new(value,GuidRepresentation.Standard);
}

public sealed class ProcessModelingMongoTransactionRunner(ProcessModelingMongoContext context,int maxCommitAttempts=3,Func<IClientSessionHandle,int,CancellationToken,Task>? testOnlyCommit=null)
{
    public async Task<T> ExecuteAsync<T>(Func<IClientSessionHandle,CancellationToken,Task<T>> body,Func<CancellationToken,Task<T?>> reconcile,CancellationToken ct=default) where T:class
    {
        using var session=await context.Client.StartSessionAsync(cancellationToken:ct); session.StartTransaction(new TransactionOptions(ReadConcern.Snapshot,ReadPreference.Primary,WriteConcern.WMajority));
        T result; try { result=await body(session,ct); }
        catch(MongoWriteException ex) when(ex.WriteError?.Category==ServerErrorCategory.DuplicateKey){if(session.IsInTransaction)await session.AbortTransactionAsync(ct);throw new ProcessModelingConflictException(ProcessModelingErrors.IdempotencyConflict);}
        catch(MongoException ex){try{if(session.IsInTransaction)await session.AbortTransactionAsync(ct);}catch(MongoException){}throw new ProcessModelingUnavailableException(ProcessModelingErrors.TransactionIndeterminate+":"+ex.GetType().Name);}
        catch { if(session.IsInTransaction) await session.AbortTransactionAsync(ct); throw; }
        for(var attempt=1;attempt<=maxCommitAttempts;attempt++) try { if(testOnlyCommit is null) await session.CommitTransactionAsync(ct); else await testOnlyCommit(session,attempt,ct); return result; }
        catch(ProcessModelingUnknownCommitException) when(attempt<maxCommitAttempts) { }
        catch(ProcessModelingUnknownCommitException) when(attempt==maxCommitAttempts) { break; }
        catch(MongoException ex) when(ex.HasErrorLabel("UnknownTransactionCommitResult")&&attempt<maxCommitAttempts) { }
        catch(MongoException ex) when(ex.HasErrorLabel("UnknownTransactionCommitResult")&&attempt==maxCommitAttempts) { break; }
        return await reconcile(ct) ?? throw new ProcessModelingUnavailableException("process_model_transaction_indeterminate");
    }
}
public sealed class ProcessModelingConflictException(string code):Exception(code);
public sealed class ProcessModelingUnavailableException(string code):Exception(code);
public sealed class ProcessModelingUnknownCommitException:Exception;
