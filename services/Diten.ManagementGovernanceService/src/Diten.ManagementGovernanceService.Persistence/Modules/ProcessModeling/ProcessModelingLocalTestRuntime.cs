using System.Text.Json;
using Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest;
using Diten.ManagementGovernanceService.Application.Modules.ProcessModeling;
using Diten.ManagementGovernanceService.Domain.Modules.ProcessModeling;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.ManagementGovernanceService.Persistence.Modules.ProcessModeling;

public sealed class ProcessModelingLocalTestQueryStore(ProcessModelingMongoContext context):IProcessModelingLocalQueryStore
{
    public async Task<IReadOnlyList<ProcessModelListItem>> ListAsync(Guid tenantId,CancellationToken cancellationToken=default)
    {
        var filter=Visible(tenantId);
        var rows=await context.Collection("mg_process_models").Find(filter).SortBy(x=>x["ModelCode"]).ToListAsync(cancellationToken);
        return rows.Select(Model).ToArray();
    }

    public async Task<ProcessModelDetail?> GetAsync(Guid tenantId,Guid modelId,CancellationToken cancellationToken=default)
    {
        var model=await context.Collection("mg_process_models").Find(Visible(tenantId,modelId)).FirstOrDefaultAsync(cancellationToken);
        if(model is null)return null;
        var versions=await context.Collection("mg_process_model_versions").Find(Builders<BsonDocument>.Filter.And(
            Visible(tenantId),Builders<BsonDocument>.Filter.Eq("ProcessModelId",G(modelId))))
            .SortByDescending(x=>x["RevisionNumber"]).ToListAsync(cancellationToken);
        var views=new List<ProcessModelVersionView>(versions.Count);
        foreach(var version in versions)views.Add(await Version(version,tenantId,cancellationToken));
        return new(Model(model),views);
    }

    public async Task<ProcessModelVersionView?> GetVersionAsync(Guid tenantId,Guid versionId,CancellationToken cancellationToken=default)
    {
        var row=await context.Collection("mg_process_model_versions").Find(Visible(tenantId,versionId)).FirstOrDefaultAsync(cancellationToken);
        return row is null?null:await Version(row,tenantId,cancellationToken);
    }

    private async Task<ProcessModelVersionView> Version(BsonDocument row,Guid tenantId,CancellationToken ct)
    {
        var id=R(row["_id"]);
        var owner=Builders<BsonDocument>.Filter.And(Visible(tenantId),Builders<BsonDocument>.Filter.Eq("ProcessModelVersionId",G(id)));
        var activities=await context.Collection("mg_process_activities").Find(owner).SortBy(x=>x["SortOrder"]).ToListAsync(ct);
        var controls=await context.Collection("mg_process_control_points").Find(owner).SortBy(x=>x["SortOrder"]).ToListAsync(ct);
        var relationships=await context.Collection("mg_process_relationships").Find(owner).SortBy(x=>x["SortOrder"]).ToListAsync(ct);
        return new(id,R(row["ProcessModelId"]),row["RevisionNumber"].AsInt32,row["LifecycleState"].AsString,row["Title"].AsString,
            S(row,"Description"),S(row,"ContentHash")??string.Empty,row["Version"].AsInt32,D(row,"CreatedAtUtc")!.Value,D(row,"UpdatedAtUtc"),
            activities.Select(x=>new ProcessModelActivityInput(R(x["LogicalActivityId"]),x["ActivityCode"].AsString,x["Name"].AsString,S(x,"Description"),x["SortOrder"].AsInt32)).ToArray(),
            controls.Select(x=>new ProcessModelControlPointInput(R(x["LogicalControlPointId"]),x["ControlCode"].AsString,x["Name"].AsString,S(x,"Description"),N(x,"LogicalActivityId"),x["SortOrder"].AsInt32)).ToArray(),
            relationships.Select(x=>new ProcessModelRelationshipInput(R(x["FromActivityId"]),R(x["ToActivityId"]),S(x,"ConditionLabel"),x["SortOrder"].AsInt32)).ToArray());
    }

    private static ProcessModelListItem Model(BsonDocument x)=>new(R(x["_id"]),R(x["ProcessDefinitionId"]),x["ModelCode"].AsString,x["Name"].AsString,S(x,"Description"),
        x["LatestRevisionNumber"].AsInt32,N(x,"PublishedVersionId"),N(x,"OpenVersionId"),x["Version"].AsInt32,D(x,"CreatedAtUtc")!.Value,D(x,"UpdatedAtUtc"));
    private static FilterDefinition<BsonDocument> Visible(Guid tenantId)=>Builders<BsonDocument>.Filter.And(Builders<BsonDocument>.Filter.Eq("TenantId",G(tenantId)),Builders<BsonDocument>.Filter.Eq("IsDeleted",false));
    private static FilterDefinition<BsonDocument> Visible(Guid tenantId,Guid id)=>Builders<BsonDocument>.Filter.And(Visible(tenantId),Builders<BsonDocument>.Filter.Eq("_id",G(id)));
    private static string? S(BsonDocument x,string n)=>!x.TryGetValue(n,out var v)||v.IsBsonNull?null:v.AsString;
    private static Guid? N(BsonDocument x,string n)=>!x.TryGetValue(n,out var v)||v.IsBsonNull?null:R(v);
    private static DateTime? D(BsonDocument x,string n)=>!x.TryGetValue(n,out var v)||v.IsBsonNull?null:DateTime.SpecifyKind(v.ToUniversalTime(),DateTimeKind.Utc);
    private static Guid R(BsonValue value)=>ProcessModelingMongoGuidPolicy.Read(value);
    private static BsonBinaryData G(Guid value)=>ProcessModelingMongoGuidPolicy.Write(value);
}

public sealed class ProcessModelingLocalTestCommandExecutor(
    ProcessModelingMongoReceiptReader receipts,ProcessModelingMongoMutationWriter writer,
    ProcessModelingMongoTransactionRunner transactions,TimeProvider clock):IProcessModelingLocalCommandExecutor
{
    public async Task<ProcessModelingLocalExecutionResult> ExecuteAsync(ProcessModelingLocalExecutionRequest command,CancellationToken cancellationToken)
    {
        if(string.Equals(command.CommandName,"PublishProcessModelVersionCommand",StringComparison.Ordinal))
            return ProcessModelingLocalExecutionResult.Fail("process_model_publish_runtime_unavailable",503);
        if(!ProcessModelingPermissions.ExactCommandMap.TryGetValue(command.CommandName,out var permission)||
           !string.Equals(permission,command.Context.Permission,StringComparison.Ordinal))
            return ProcessModelingLocalExecutionResult.Fail("process_model_permission_denied",403);
        try
        {
            var create=command.CommandName is "CreateProcessModelCommand" or "CreateProcessArchitectureCommand" or "CreateProcessDomainCommand" or "CreateProcessFamilyCommand" or "CreateProcessDefinitionCommand";
            var aggregateId=create?Guid.NewGuid():command.RouteId??Guid.Empty;
            if(aggregateId==Guid.Empty)return ProcessModelingLocalExecutionResult.Fail("process_modeling_bad_request",400);
            var expected=ExpectedVersion(command.RawBody,create);
            var request=new CoreMutationRequest(command.Context.TenantId,command.Context.ActorId,command.CommandName,command.Context.IdempotencyKey,
                "sha256:"+command.Context.RequestHash,expected,aggregateId);
            var durable=await receipts.ReconcileAsync(request,true,cancellationToken);
            if(durable is not null)return Map(durable);
            var business=Business(command.CommandName,command.RawBody);
            var outcomeId=command.CommandName=="CreateProcessModelRevisionCommand"?Guid.NewGuid():aggregateId;
            var now=clock.GetUtcNow().UtcDateTime;
            var envelope=new MongoMutationEnvelope(request,business,create?"created":"accepted",Guid.NewGuid(),Guid.NewGuid(),now,
                "local-test:"+command.Context.ActorId.ToString("D"),create?201:command.CommandName=="CreateProcessModelRevisionCommand"?201:204,outcomeId);
            var result=await transactions.ExecuteAsync(async(session,ct)=>
            {
                await writer.WriteAsync(session,envelope,ct);
                return new CoreMutationResult(true,envelope.HttpStatus,envelope.Outcome,outcomeId);
            },ct=>receipts.ReconcileAsync(request,true,ct),cancellationToken);
            return Map(result);
        }
        catch(Exception ex) when(ex is ArgumentException or JsonException or InvalidOperationException)
        {return ProcessModelingLocalExecutionResult.Fail("process_modeling_bad_request",400);}
        catch(ProcessModelingNotFoundException){return ProcessModelingLocalExecutionResult.Fail("process_model_not_found",404);}
        catch(ProcessModelingConflictException ex){return ProcessModelingLocalExecutionResult.Fail(ex.Message,409);}
        catch(ProcessModelingUnavailableException){return ProcessModelingLocalExecutionResult.Fail("process_model_provider_unavailable",503);}
        catch(MongoException){return ProcessModelingLocalExecutionResult.Fail("process_model_provider_unavailable",503);}
    }

    private static int ExpectedVersion(byte[] raw,bool create)
    {
        using var json=JsonDocument.Parse(raw);
        if(create)return 0;
        return json.RootElement.TryGetProperty("expectedVersion",out var value)&&value.TryGetInt32(out var version)&&version>=0?version:throw new ArgumentException("expected_version_required");
    }

    private static BsonDocument Business(string command,byte[] raw)
    {
        using var json=JsonDocument.Parse(raw);
        var root=json.RootElement;
        var result=new BsonDocument();
        foreach(var property in root.EnumerateObject())
        {
            if(property.NameEquals("expectedVersion"))continue;
            result[Pascal(property.Name)]=Bson(property.Value);
        }
        Normalize(command,result);
        return result;
    }

    private static void Normalize(string command,BsonDocument x)
    {
        foreach(var name in new[]{"ArchitectureCode","DomainCode","FamilyCode","ProcessCode","ModelCode","ActivityCode","ControlCode"})
            if(x.TryGetValue(name,out var value)&&value.IsString)x[name]=ProcessModelingText.Code(value.AsString);
        foreach(var name in new[]{"Name","Title"})if(x.TryGetValue(name,out var value)&&value.IsString)x[name]=ProcessModelingText.Required(value.AsString,200);
        foreach(var (name,max) in new[]{("Description",4000),("Purpose",2000),("ConditionLabel",500)})
            if(x.TryGetValue(name,out var value)&&value.IsString)x[name]=ProcessModelingText.Optional(value.AsString,max) is { } text?text:BsonNull.Value;
        if(command=="UpdateDraftProcessModelVersionCommand")
        {
            foreach(var collection in new[]{"Activities","ControlPoints"})if(x.TryGetValue(collection,out var array))
                foreach(var item in array.AsBsonArray)if(item.AsBsonDocument.TryGetValue("Name",out var name))item.AsBsonDocument["Name"]=ProcessModelingText.Required(name.AsString,200);
            x["ContentHash"]="sha256:"+new string('0',64);
        }
        if(command=="CreateProcessModelRevisionCommand")x["ContentHash"]="sha256:"+new string('0',64);
    }

    private static BsonValue Bson(JsonElement value)=>value.ValueKind switch
    {
        JsonValueKind.String when value.TryGetGuid(out var guid)=>G(guid),
        JsonValueKind.String=>value.GetString()!,JsonValueKind.Number=>value.GetInt32(),JsonValueKind.True=>true,JsonValueKind.False=>false,
        JsonValueKind.Null=>BsonNull.Value,JsonValueKind.Array=>new BsonArray(value.EnumerateArray().Select(Bson)),
        JsonValueKind.Object=>new BsonDocument(value.EnumerateObject().Select(x=>new BsonElement(Pascal(x.Name),Bson(x.Value)))),
        _=>throw new JsonException()
    };
    private static string Pascal(string value)=>char.ToUpperInvariant(value[0])+value[1..];
    private static ProcessModelingLocalExecutionResult Map(CoreMutationResult x)=>x.Accepted
        ? x.HttpStatus==201&&x.AggregateId is { } id?ProcessModelingLocalExecutionResult.Created(id):ProcessModelingLocalExecutionResult.NoContent()
        : ProcessModelingLocalExecutionResult.Fail(x.StableCode,x.HttpStatus);
    private static BsonBinaryData G(Guid value)=>ProcessModelingMongoGuidPolicy.Write(value);
}
