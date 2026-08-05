using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.FpaService.Domain.Modules.ScenarioPlanning.Common;

namespace Diten.FpaService.Domain.Modules.ScenarioPlanning;

public enum ScenarioLifecycle { Active, Archived }
public enum ScenarioAssumptionValueType { Decimal, Integer, Boolean, String, Date }
public enum ScenarioAssumptionUnitKind { None, Units, Thousands, Millions, Currency }

public sealed class Scenario : ScenarioPlanningEntityBase
{
    public Scenario(Guid id, Guid tenantId, string code, string name, string? description, DateTimeOffset now)
    { Id=ScenarioGuard.Required(id,"scenario.id-required");TenantId=ScenarioGuard.Required(tenantId,"scenario.tenant-required");Code=ScenarioGuard.Text(code,128,"scenario.code-invalid");Name=ScenarioGuard.Text(name,128,"scenario.name-invalid");Description=description?.Trim().Normalize(NormalizationForm.FormC);Lifecycle=ScenarioLifecycle.Active;CreatedAt=now; }
    public string Code { get; private set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public ScenarioLifecycle Lifecycle { get; private set; }
    public void Archive(DateTimeOffset now) { if(Lifecycle!=ScenarioLifecycle.Active)throw new ScenarioPlanningDomainException("scenario.archive-invalid");Lifecycle=ScenarioLifecycle.Archived;UpdatedAt=now;Version++; }
}

public sealed record CurrencyReferenceV1(string ContractName,string ContractVersion,string CurrencyCode);
public sealed record BudgetVersionReferenceV1(string ContractName,string ContractVersion,Guid BudgetId,Guid BudgetVersionId,long BudgetVersionNumber)
{
    public void Validate(){if(ContractName!="fpa.budget-version-reference"||ContractVersion!="1.0"||BudgetId==Guid.Empty||BudgetVersionId==Guid.Empty||BudgetVersionNumber<=0)throw new ScenarioPlanningDomainException("scenario.budget-reference-invalid");}
}

public sealed record ScenarioAssumptionV1(string AssumptionKey,ScenarioAssumptionValueType ValueType,string Value,ScenarioAssumptionUnitKind UnitKind,CurrencyReferenceV1? CurrencyReference)
{
    public ScenarioAssumptionV1 Validate()
    {
        var key=ScenarioGuard.Text(AssumptionKey,128,"scenario.assumption-key-invalid");
        if(UnitKind==ScenarioAssumptionUnitKind.Currency!=(CurrencyReference is not null))throw new ScenarioPlanningDomainException("scenario.assumption-currency-invalid");
        var value=ValueType switch
        {
            ScenarioAssumptionValueType.Decimal=>CanonicalDecimal(Value),
            ScenarioAssumptionValueType.Integer=>long.Parse(Value,NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
            ScenarioAssumptionValueType.Boolean=>Value is "true" or "false"?Value:throw new ScenarioPlanningDomainException("scenario.assumption-boolean-invalid"),
            ScenarioAssumptionValueType.Date=>DateOnly.ParseExact(Value,"yyyy-MM-dd",CultureInfo.InvariantCulture).ToString("yyyy-MM-dd",CultureInfo.InvariantCulture),
            _=>ScenarioGuard.Text(Value,2000,"scenario.assumption-string-invalid")
        };
        return this with{AssumptionKey=key,Value=value};
    }
    internal static string CanonicalDecimal(string value)
    {
        if(value.Contains('e',StringComparison.OrdinalIgnoreCase)||!decimal.TryParse(value,NumberStyles.AllowLeadingSign|NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var parsed))throw new ScenarioPlanningDomainException("scenario.decimal-invalid");
        var parts=value.TrimStart('-').Split('.');if(parts[0].Length+(parts.Length==2?parts[1].Length:0)>28||(parts.Length==2&&parts[1].Length>4))throw new ScenarioPlanningDomainException("scenario.decimal-invalid");
        return parsed==0?"0":parsed.ToString("0.####",CultureInfo.InvariantCulture);
    }
}

public sealed class ScenarioVersionDraft : ScenarioPlanningEntityBase
{
    private readonly List<ScenarioAssumptionV1> _assumptions=[];private readonly List<BudgetVersionReferenceV1> _foreignReferences=[];
    public ScenarioVersionDraft(Guid id,Guid tenantId,Guid scenarioId,string label,DateOnly from,DateOnly to,string? notes,Guid? basedOn,DateTimeOffset now)
    {if(to<from)throw new ScenarioPlanningDomainException("scenario.draft-date-invalid");Id=ScenarioGuard.Required(id,"scenario.draft-id-required");TenantId=ScenarioGuard.Required(tenantId,"scenario.tenant-required");ScenarioId=ScenarioGuard.Required(scenarioId,"scenario.id-required");VersionLabel=ScenarioGuard.Text(label,128,"scenario.label-invalid");EffectiveFrom=from;EffectiveTo=to;Notes=notes?.Trim().Normalize(NormalizationForm.FormC);BasedOnScenarioVersionId=basedOn;CreatedAt=now;}
    public Guid ScenarioId{get;}public string VersionLabel{get;private set;}public DateOnly EffectiveFrom{get;private set;}public DateOnly EffectiveTo{get;private set;}public string? Notes{get;private set;}public Guid? BasedOnScenarioVersionId{get;}public bool IsOpen=>!IsDeleted;
    public IReadOnlyList<ScenarioAssumptionV1> Assumptions=>_assumptions.AsReadOnly();public IReadOnlyList<BudgetVersionReferenceV1> ForeignReferences=>_foreignReferences.AsReadOnly();
    public void ReplaceContent(long expectedVersion,IEnumerable<ScenarioAssumptionV1> assumptions,IEnumerable<BudgetVersionReferenceV1> references,DateTimeOffset now)
    {if(expectedVersion!=Version||!IsOpen)throw new ScenarioPlanningDomainException("scenario.draft-stale");var a=assumptions.Select(x=>x.Validate()).ToArray();if(a.Length>200||a.Select(x=>x.AssumptionKey).Distinct(StringComparer.Ordinal).Count()!=a.Length)throw new ScenarioPlanningDomainException("scenario.assumptions-invalid");var r=references.ToArray();if(r.Length>20)throw new ScenarioPlanningDomainException("scenario.references-invalid");foreach(var x in r)x.Validate();_assumptions.Clear();_assumptions.AddRange(a);_foreignReferences.Clear();_foreignReferences.AddRange(r);Version++;UpdatedAt=now;}
    public void Abandon(DateTimeOffset now){if(!IsOpen)throw new ScenarioPlanningDomainException("scenario.draft-closed");IsDeleted=true;DeletedAt=now;UpdatedAt=now;Version++;}
}

public sealed class OpenDraftFence
{
    private readonly HashSet<(Guid Tenant,Guid Scenario)> _open=[];
    public void Acquire(Guid tenantId,Guid scenarioId){if(!_open.Add((tenantId,scenarioId)))throw new ScenarioPlanningDomainException("scenario.one-open-draft");}
    public void Release(Guid tenantId,Guid scenarioId)=>_open.Remove((tenantId,scenarioId));
}

public sealed class ScenarioVersion : ScenarioPlanningEntityBase
{
    public ScenarioVersion(Guid id,Guid tenantId,Guid scenarioId,long number,ScenarioVersionDraft draft,string contentHash,DateTimeOffset publishedAt,Guid actor)
    {if(number<=0||!draft.IsOpen)throw new ScenarioPlanningDomainException("scenario.publish-invalid");Id=id;TenantId=tenantId;ScenarioId=scenarioId;ScenarioVersionNumber=number;VersionLabel=draft.VersionLabel;EffectiveFrom=draft.EffectiveFrom;EffectiveTo=draft.EffectiveTo;Notes=draft.Notes;Assumptions=Array.AsReadOnly(draft.Assumptions.ToArray());ForeignReferences=Array.AsReadOnly(draft.ForeignReferences.ToArray());ContentHash=contentHash;PublishedAtUtc=publishedAt;PublishedByActorId=actor;CreatedAt=publishedAt;}
    public Guid ScenarioId{get;}public long ScenarioVersionNumber{get;}public string VersionLabel{get;}public DateOnly EffectiveFrom{get;}public DateOnly EffectiveTo{get;}public string? Notes{get;}public IReadOnlyList<ScenarioAssumptionV1> Assumptions{get;}public IReadOnlyList<BudgetVersionReferenceV1> ForeignReferences{get;}public string ContentHash{get;}public string CanonicalizationVersion=>ScenarioContentCanonicalizer.Version;public DateTimeOffset PublishedAtUtc{get;}public Guid PublishedByActorId{get;}
}

public static class ScenarioContentCanonicalizer
{
    public const string Version="fpa.scenario-content-canonical-json.v1";
    public static byte[] Canonicalize(string label,DateOnly from,DateOnly to,string? notes,IEnumerable<ScenarioAssumptionV1> assumptions,IEnumerable<BudgetVersionReferenceV1> references)
    {
        using var stream=new MemoryStream();using(var w=new Utf8JsonWriter(stream,new JsonWriterOptions{Indented=false}))
        {w.WriteStartObject();w.WriteString("CanonicalizationVersion",Version);w.WriteString("VersionLabel",label.Normalize(NormalizationForm.FormC));w.WriteString("EffectiveFrom",from.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture));w.WriteString("EffectiveTo",to.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture));if(notes is null)w.WriteNull("Notes");else w.WriteString("Notes",notes.Normalize(NormalizationForm.FormC));w.WritePropertyName("Assumptions");w.WriteStartArray();foreach(var a in assumptions.Select(x=>x.Validate()).OrderBy(x=>x.AssumptionKey,StringComparer.Ordinal)){w.WriteStartObject();w.WriteString("AssumptionKey",a.AssumptionKey);w.WriteString("ValueType",a.ValueType.ToString());w.WriteString("Value",a.Value);w.WriteString("UnitKind",a.UnitKind.ToString());if(a.CurrencyReference is null)w.WriteNull("CurrencyReference");else{w.WritePropertyName("CurrencyReference");JsonSerializer.Serialize(w,a.CurrencyReference);}w.WriteEndObject();}w.WriteEndArray();w.WritePropertyName("ForeignReferences");w.WriteStartArray();foreach(var r in references.OrderBy(x=>x.ContractName,StringComparer.Ordinal).ThenBy(x=>x.ContractVersion,StringComparer.Ordinal).ThenBy(x=>x.BudgetId).ThenBy(x=>x.BudgetVersionId).ThenBy(x=>x.BudgetVersionNumber)){w.WriteStartObject();w.WriteString("ContractName",r.ContractName);w.WriteString("ContractVersion",r.ContractVersion);w.WriteString("BudgetId",r.BudgetId);w.WriteString("BudgetVersionId",r.BudgetVersionId);w.WriteNumber("BudgetVersionNumber",r.BudgetVersionNumber);w.WriteEndObject();}w.WriteEndArray();w.WriteEndObject();}
        return stream.ToArray();
    }
    public static string Sha256(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}

public sealed record ScenarioVersionReferenceV1(string ContractName,string ContractVersion,Guid ScenarioId,Guid ScenarioVersionId,long ScenarioVersionNumber);
public sealed record ComparatorOutputReferenceV1(string ContractName,string ContractVersion,Guid ComparatorRunId,Guid ComparatorOutputId,int OutputVersion);
public sealed record ComparatorInput(ScenarioVersionReferenceV1 Reference,IReadOnlyDictionary<string,decimal> Values);
public sealed record ComparatorCandidateResult(ScenarioVersionReferenceV1 Reference,decimal Score,int Rank,IReadOnlyDictionary<string,decimal> Deltas);
public sealed class ComparatorRunDefinition
{
    public ComparatorRunDefinition(Guid id,IEnumerable<ComparatorInput> inputs,IReadOnlyDictionary<string,string> parameters){var values=inputs.ToArray();if(values.Length is <2 or >10||parameters.Count!=0)throw new ScenarioPlanningDomainException("scenario.comparator-input-invalid");ComparatorRunId=id;Inputs=Array.AsReadOnly(values);Parameters=new ReadOnlyDictionary<string,string>(new Dictionary<string,string>());}
    public Guid ComparatorRunId{get;}public string ComparatorKey=>"BaselineDelta";public string AlgorithmVersion=>"1.0";public string NormalizationVersion=>"fpa.scenario-normalization.v1";public string ParameterSchemaVersion=>"1.0";public string OutputSchemaVersion=>"1.0";public IReadOnlyList<ComparatorInput> Inputs{get;}public IReadOnlyDictionary<string,string> Parameters{get;}
}
public static class BaselineDeltaV1
{
    public static IReadOnlyList<ComparatorCandidateResult> Execute(ComparatorRunDefinition definition)
    {var baseline=definition.Inputs[0];var results=definition.Inputs.Skip(1).Select(x=>{if(!baseline.Values.Keys.Order().SequenceEqual(x.Values.Keys.Order()))throw new ScenarioPlanningDomainException("scenario.comparator-incompatible");var deltas=x.Values.ToDictionary(k=>k.Key,k=>k.Value-baseline.Values[k.Key],StringComparer.Ordinal);return (x.Reference,Score:deltas.Values.Sum(),Deltas:(IReadOnlyDictionary<string,decimal>)new ReadOnlyDictionary<string,decimal>(deltas));}).OrderByDescending(x=>x.Score).ThenBy(x=>x.Reference.ScenarioVersionId).Select((x,i)=>new ComparatorCandidateResult(x.Reference,x.Score,i+1,x.Deltas)).ToArray();return Array.AsReadOnly(results);}
}
public enum ComparatorExecutionState{Requested,Started,Completed,Failed}
public sealed class ComparatorExecutionEvent{public ComparatorExecutionEvent(long sequence,ComparatorExecutionState state,DateTimeOffset occurredAtUtc){Sequence=sequence;State=state;OccurredAtUtc=occurredAtUtc;}public long Sequence{get;}public ComparatorExecutionState State{get;}public DateTimeOffset OccurredAtUtc{get;}}
public sealed class ComparatorOutput{public ComparatorOutput(Guid id,Guid runId,IEnumerable<ComparatorCandidateResult> results,string digest){ComparatorOutputId=id;ComparatorRunId=runId;CandidateResults=Array.AsReadOnly(results.ToArray());ResultDigest=digest;}public Guid ComparatorOutputId{get;}public Guid ComparatorRunId{get;}public int OutputVersion=>1;public IReadOnlyList<ComparatorCandidateResult> CandidateResults{get;}public string ResultDigest{get;}}

public sealed record InvestmentCaseContextV1(string ContractName,string ContractVersion,Guid InvestmentCaseId){public void Validate(){if(ContractName!="ppm.investment-case-context"||ContractVersion!="1.0"||InvestmentCaseId==Guid.Empty)throw new ScenarioPlanningDomainException("scenario.selection-context-invalid");}}
public sealed class ScenarioSelectionOccurrence{public ScenarioSelectionOccurrence(Guid id,long revision,Guid tenantId,InvestmentCaseContextV1 context,ScenarioVersionReferenceV1 reference,DateTimeOffset effectiveFromUtc){SelectionOccurrenceId=id;SelectionRevision=revision;TenantId=tenantId;Context=context;ScenarioVersionReference=reference;EffectiveFromUtc=effectiveFromUtc;}public Guid SelectionOccurrenceId{get;}public long SelectionRevision{get;}public Guid TenantId{get;}public InvestmentCaseContextV1 Context{get;}public ScenarioVersionReferenceV1 ScenarioVersionReference{get;}public DateTimeOffset EffectiveFromUtc{get;}}
public sealed class ScenarioSelectionDisposition{public ScenarioSelectionDisposition(Guid id,long sequence,string disposition,DateTimeOffset occurredAtUtc){SelectionOccurrenceId=id;Sequence=sequence;Disposition=disposition;OccurredAtUtc=occurredAtUtc;}public Guid SelectionOccurrenceId{get;}public long Sequence{get;}public string Disposition{get;}public DateTimeOffset OccurredAtUtc{get;}}
public sealed class ScenarioSelectionFence
{
    private readonly List<ScenarioSelectionOccurrence> _occurrences=[];private readonly List<ScenarioSelectionDisposition> _dispositions=[];public long Revision{get;private set;}public ScenarioSelectionOccurrence? Current=>_occurrences.LastOrDefault(x=>!_dispositions.Any(d=>d.SelectionOccurrenceId==x.SelectionOccurrenceId));public IReadOnlyList<ScenarioSelectionOccurrence> Occurrences=>_occurrences.AsReadOnly();public IReadOnlyList<ScenarioSelectionDisposition> Dispositions=>_dispositions.AsReadOnly();
    public ScenarioSelectionOccurrence Replace(long expected,Guid id,Guid tenant,InvestmentCaseContextV1 context,ScenarioVersionReferenceV1 reference,DateTimeOffset now){if(expected!=Revision)throw new ScenarioPlanningDomainException("scenario.selection-stale");context.Validate();if(Current is not null)_dispositions.Add(new(Current.SelectionOccurrenceId,1,"Replaced",now));var next=new ScenarioSelectionOccurrence(id,++Revision,tenant,context,reference,now);_occurrences.Add(next);return next;}
    public void Close(long expected,DateTimeOffset now){if(expected!=Revision||Current is null)throw new ScenarioPlanningDomainException("scenario.selection-stale");_dispositions.Add(new(Current.SelectionOccurrenceId,1,"Closed",now));Revision++;}
}
