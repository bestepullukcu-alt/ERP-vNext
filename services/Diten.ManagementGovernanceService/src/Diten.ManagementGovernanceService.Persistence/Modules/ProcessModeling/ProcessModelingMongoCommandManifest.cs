using Diten.ManagementGovernanceService.Application.Modules.ProcessModeling;
using MongoDB.Bson;

namespace Diten.ManagementGovernanceService.Persistence.Modules.ProcessModeling;

internal enum ProcessModelingMongoOperation
{
    Create,
    Update,
    Archive,
    DraftReplace,
    Transition,
    CreateRevision,
    Retire,
    Publish,
    Unavailable
}

internal sealed record ProcessModelingMongoCommandSpec(
    string Command,
    ProcessModelingMongoOperation Operation,
    string Collection,
    IReadOnlySet<string> Fields,
    string? RequiredLifecycle = null,
    string? NextLifecycle = null,
    string? ParentCollection = null,
    string? ParentField = null);

internal sealed record ProcessModelingPreparedMutation(
    ProcessModelingMongoCommandSpec Spec,
    BsonDocument Values,
    IReadOnlyList<BsonDocument> Activities,
    IReadOnlyList<BsonDocument> ControlPoints,
    IReadOnlyList<BsonDocument> Relationships,
    Guid OutcomeEntityId);

/// <summary>
/// Closed persistence dispatcher for the exact MOD-0355 command inventory.  Callers supply business values,
/// never an operation or collection.  Tenant, identity, lifecycle, author and technical fields are rebound here.
/// </summary>
public sealed record ProcessModelingMongoCommandDescriptor(
    string Command,string Operation,string Collection,string? RequiredLifecycle,string? NextLifecycle,string? ParentCollection,string? ParentField,IReadOnlyList<string> Fields);

public static class ProcessModelingMongoCommandManifest
{
    private static readonly IReadOnlyDictionary<string, ProcessModelingMongoCommandSpec> Exact =
        new Dictionary<string, ProcessModelingMongoCommandSpec>(StringComparer.Ordinal)
        {
            ["CreateProcessArchitectureCommand"] = S("CreateProcessArchitectureCommand",ProcessModelingMongoOperation.Create,"mg_process_architectures",["ArchitectureCode","Name","Description","SortOrder"]),
            ["UpdateProcessArchitectureCommand"] = S("UpdateProcessArchitectureCommand",ProcessModelingMongoOperation.Update,"mg_process_architectures",["Name","Description","SortOrder"],"Active"),
            ["ArchiveProcessArchitectureCommand"] = S("ArchiveProcessArchitectureCommand",ProcessModelingMongoOperation.Archive,"mg_process_architectures",[],"Active","Archived"),
            ["CreateProcessDomainCommand"] = S("CreateProcessDomainCommand",ProcessModelingMongoOperation.Create,"mg_process_domains",["ProcessArchitectureId","DomainCode","Name","Description","SortOrder"],parentCollection:"mg_process_architectures",parentField:"ProcessArchitectureId"),
            ["UpdateProcessDomainCommand"] = S("UpdateProcessDomainCommand",ProcessModelingMongoOperation.Update,"mg_process_domains",["Name","Description","SortOrder"],"Active"),
            ["ArchiveProcessDomainCommand"] = S("ArchiveProcessDomainCommand",ProcessModelingMongoOperation.Archive,"mg_process_domains",[],"Active","Archived"),
            ["CreateProcessFamilyCommand"] = S("CreateProcessFamilyCommand",ProcessModelingMongoOperation.Create,"mg_process_families",["ProcessDomainId","FamilyCode","Name","Description","SortOrder"],parentCollection:"mg_process_domains",parentField:"ProcessDomainId"),
            ["UpdateProcessFamilyCommand"] = S("UpdateProcessFamilyCommand",ProcessModelingMongoOperation.Update,"mg_process_families",["Name","Description","SortOrder"],"Active"),
            ["ArchiveProcessFamilyCommand"] = S("ArchiveProcessFamilyCommand",ProcessModelingMongoOperation.Archive,"mg_process_families",[],"Active","Archived"),
            ["CreateProcessDefinitionCommand"] = S("CreateProcessDefinitionCommand",ProcessModelingMongoOperation.Create,"mg_process_definitions",["ProcessFamilyId","ProcessCode","Name","Purpose","Description"],parentCollection:"mg_process_families",parentField:"ProcessFamilyId"),
            ["UpdateProcessDefinitionCommand"] = S("UpdateProcessDefinitionCommand",ProcessModelingMongoOperation.Update,"mg_process_definitions",["Name","Purpose","Description"],"Active"),
            ["ArchiveProcessDefinitionCommand"] = S("ArchiveProcessDefinitionCommand",ProcessModelingMongoOperation.Archive,"mg_process_definitions",[],"Active","Archived"),
            ["CreateProcessModelCommand"] = S("CreateProcessModelCommand",ProcessModelingMongoOperation.Create,"mg_process_models",["ProcessDefinitionId","ModelCode","Name","Description"],parentCollection:"mg_process_definitions",parentField:"ProcessDefinitionId"),
            ["UpdateProcessModelCommand"] = S("UpdateProcessModelCommand",ProcessModelingMongoOperation.Update,"mg_process_models",["Name","Description"]),
            ["UpdateDraftProcessModelVersionCommand"] = S("UpdateDraftProcessModelVersionCommand",ProcessModelingMongoOperation.DraftReplace,"mg_process_model_versions",["Title","Description","ContentHash","Activities","ControlPoints","Relationships"],"Draft"),
            ["RequestProcessModelReviewCommand"] = S("RequestProcessModelReviewCommand",ProcessModelingMongoOperation.Transition,"mg_process_model_versions",[],"Draft","Review"),
            ["ReturnProcessModelToDraftCommand"] = S("ReturnProcessModelToDraftCommand",ProcessModelingMongoOperation.Transition,"mg_process_model_versions",[],"Review","Draft"),
            ["PublishProcessModelVersionCommand"] = S("PublishProcessModelVersionCommand",ProcessModelingMongoOperation.Publish,"mg_process_model_versions",[],"Review","Published"),
            ["RetireProcessModelVersionCommand"] = S("RetireProcessModelVersionCommand",ProcessModelingMongoOperation.Retire,"mg_process_model_versions",[],"Published","Retired"),
            ["CreateProcessModelRevisionCommand"] = S("CreateProcessModelRevisionCommand",ProcessModelingMongoOperation.CreateRevision,"mg_process_model_versions",["Title","Description","ContentHash"])
        };

    public static IReadOnlyList<ProcessModelingMongoCommandDescriptor> Descriptors { get; } = Exact.Values
        .Select(x=>new ProcessModelingMongoCommandDescriptor(x.Command,x.Operation.ToString(),x.Collection,x.RequiredLifecycle,x.NextLifecycle,x.ParentCollection,x.ParentField,x.Fields.Order(StringComparer.Ordinal).ToArray()))
        .OrderBy(x=>x.Command,StringComparer.Ordinal).ToArray();

    internal static ProcessModelingPreparedMutation Prepare(MongoMutationEnvelope envelope)
    {
        if (!Exact.TryGetValue(envelope.Request.CommandFamily, out var spec) ||
            !ProcessModelingPermissions.ExactCommandMap.ContainsKey(envelope.Request.CommandFamily))
            throw new InvalidOperationException("process_modeling_command_forbidden");
        if (spec.Operation is ProcessModelingMongoOperation.Unavailable)
            throw new ProcessModelingUnavailableException("process_model_publish_second_slice_unavailable");

        var outcomeId = envelope.OutcomeEntityId ?? envelope.Request.AggregateId;
        if (outcomeId == Guid.Empty || envelope.Request.AggregateId == Guid.Empty)
            throw new ArgumentException("process_modeling_identity_required");

        ValidateTechnicalInput(envelope, spec, outcomeId);
        var values = CopyAllowed(envelope.Business, spec.Fields);
        IReadOnlyList<BsonDocument> activities = [];
        IReadOnlyList<BsonDocument> controls = [];
        IReadOnlyList<BsonDocument> relationships = [];

        if (spec.Operation is ProcessModelingMongoOperation.DraftReplace)
        {
            activities = BindChildren(envelope,"Activities",["LogicalActivityId","ActivityCode","Name","Description","SortOrder"]);
            controls = BindChildren(envelope,"ControlPoints",["LogicalControlPointId","ControlCode","Name","Description","LogicalActivityId","SortOrder"]);
            relationships = BindChildren(envelope,"Relationships",["FromActivityId","ToActivityId","ConditionLabel","SortOrder"]);
            values.Remove("Activities"); values.Remove("ControlPoints"); values.Remove("Relationships");
        }

        switch (spec.Operation)
        {
            case ProcessModelingMongoOperation.Create:
                BindNew(values,envelope.Request.AggregateId,envelope);
                if (spec.Collection is "mg_process_architectures" or "mg_process_domains" or "mg_process_families" or "mg_process_definitions")
                    values["LifecycleState"]="Active";
                if (spec.Collection=="mg_process_models")
                {
                    values["LatestRevisionNumber"]=1;
                    values["PublishedVersionId"]=BsonNull.Value;
                    values["OpenVersionId"]=BsonNull.Value;
                }
                break;
            case ProcessModelingMongoOperation.CreateRevision:
                BindNew(values,outcomeId,envelope);
                values["ProcessModelId"]=G(envelope.Request.AggregateId);
                values["ModelAuthorActorId"]=G(envelope.Request.SubjectId);
                values["LifecycleState"]="Draft";
                values["ValidFromUtc"]=BsonNull.Value;
                values["PublishedAtUtc"]=BsonNull.Value;
                values["RetiredAtUtc"]=BsonNull.Value;
                break;
            default:
                values["UpdatedAtUtc"]=envelope.CreatedAtUtc;
                break;
        }

        return new(spec,values,activities,controls,relationships,outcomeId);
    }

    private static void ValidateTechnicalInput(MongoMutationEnvelope envelope, ProcessModelingMongoCommandSpec spec, Guid outcomeId)
    {
        var permitted = new HashSet<string>(spec.Fields,StringComparer.Ordinal) { "_id","TenantId","Version","IsDeleted","CreatedAtUtc" };
        foreach (var element in envelope.Business)
            if (!permitted.Contains(element.Name)) throw new InvalidOperationException("process_modeling_field_forbidden:"+element.Name);

        var expectedId = spec.Operation==ProcessModelingMongoOperation.CreateRevision ? outcomeId : envelope.Request.AggregateId;
        if (envelope.Business.TryGetValue("_id",out var id) && ProcessModelingMongoGuidPolicy.Read(id)!=expectedId)
            throw new InvalidOperationException("process_modeling_identity_override_forbidden");
        if (envelope.Business.TryGetValue("TenantId",out var tenant) && ProcessModelingMongoGuidPolicy.Read(tenant)!=envelope.Request.TenantId)
            throw new InvalidOperationException("process_modeling_tenant_override_forbidden");
        if (envelope.Business.TryGetValue("Version",out var version) && version.ToInt32()!=0 && spec.Operation is ProcessModelingMongoOperation.Create or ProcessModelingMongoOperation.CreateRevision)
            throw new InvalidOperationException("process_modeling_version_override_forbidden");
        if (envelope.Business.TryGetValue("IsDeleted",out var deleted) && deleted.ToBoolean())
            throw new InvalidOperationException("process_modeling_delete_override_forbidden");
    }

    private static BsonDocument CopyAllowed(BsonDocument source,IReadOnlySet<string> fields)
    {
        var result=new BsonDocument();
        foreach(var field in fields)
            if(source.TryGetValue(field,out var value))result[field]=value.DeepClone();
        return result;
    }

    private static IReadOnlyList<BsonDocument> BindChildren(MongoMutationEnvelope envelope,string property,string[] fieldNames)
    {
        var fields=new HashSet<string>(fieldNames,StringComparer.Ordinal);
        if(!envelope.Business.TryGetValue(property,out var raw))return [];
        if(raw.BsonType!=BsonType.Array)throw new InvalidOperationException("process_modeling_graph_shape_invalid:"+property);
        var result=new List<BsonDocument>();
        foreach(var value in raw.AsBsonArray)
        {
            if(value.BsonType!=BsonType.Document)throw new InvalidOperationException("process_modeling_graph_shape_invalid:"+property);
            var input=value.AsBsonDocument;
            foreach(var element in input)
                if(!fields.Contains(element.Name))throw new InvalidOperationException("process_modeling_graph_field_forbidden:"+element.Name);
            var child=CopyAllowed(input,fields);
            child["_id"]=G(Guid.NewGuid());
            child["TenantId"]=G(envelope.Request.TenantId);
            child["ProcessModelVersionId"]=G(envelope.Request.AggregateId);
            child["CreatedAtUtc"]=envelope.CreatedAtUtc;
            child["IsDeleted"]=false;
            child["Version"]=0;
            result.Add(child);
        }
        return result;
    }

    private static void BindNew(BsonDocument values,Guid id,MongoMutationEnvelope envelope)
    {
        values["_id"]=G(id);
        values["TenantId"]=G(envelope.Request.TenantId);
        values["CreatedAtUtc"]=envelope.CreatedAtUtc;
        values["UpdatedAtUtc"]=BsonNull.Value;
        values["IsDeleted"]=false;
        values["DeletedAtUtc"]=BsonNull.Value;
        values["Version"]=0;
    }

    private static ProcessModelingMongoCommandSpec S(string command,ProcessModelingMongoOperation operation,string collection,string[] fields,string? required=null,string? next=null,string? parentCollection=null,string? parentField=null)
        =>new(command,operation,collection,new HashSet<string>(fields,StringComparer.Ordinal),required,next,parentCollection,parentField);
    private static BsonBinaryData G(Guid value)=>ProcessModelingMongoGuidPolicy.Write(value);
}
