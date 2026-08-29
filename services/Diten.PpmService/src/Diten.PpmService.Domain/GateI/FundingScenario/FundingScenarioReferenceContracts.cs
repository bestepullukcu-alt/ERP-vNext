using System.Globalization;
using System.Text.Json;

namespace Diten.PpmService.Domain.GateI.FundingScenario;

public sealed record InvestmentCaseContextV1
{
    public const string ExpectedContractName="ppm.investment-case-context", ExpectedContractVersion="1.0";
    public InvestmentCaseContextV1(string contractName,string contractVersion,Guid investmentCaseId){ContractName=Exact(contractName,ExpectedContractName);ContractVersion=Exact(contractVersion,ExpectedContractVersion);InvestmentCaseId=Id(investmentCaseId);}
    public string ContractName{get;} public string ContractVersion{get;} public Guid InvestmentCaseId{get;}
    public string ToExactJson()=>"{\"ContractName\":\""+ContractName+"\",\"ContractVersion\":\""+ContractVersion+"\",\"InvestmentCaseId\":\""+InvestmentCaseId.ToString("D")+"\"}";
    public static InvestmentCaseContextV1 ParseExact(string json)=>FundingScenarioJson.Parse<InvestmentCaseContextV1>(json,["ContractName","ContractVersion","InvestmentCaseId"],e=>new(FundingScenarioJson.String(e,"ContractName"),FundingScenarioJson.String(e,"ContractVersion"),FundingScenarioJson.Guid(e,"InvestmentCaseId")));
    internal static string Exact(string value,string expected)=>string.Equals(value,expected,StringComparison.Ordinal)?value:throw new ArgumentException("unsupported_contract_identity");
    internal static Guid Id(Guid value)=>value==Guid.Empty?throw new ArgumentException("empty_identity"):value;
    internal static long Positive(long value)=>value>0?value:throw new ArgumentOutOfRangeException(nameof(value));
}

public sealed record BudgetVersionReferenceV1
{
    public const string ExpectedContractName="fpa.budget-version-reference", ExpectedContractVersion="1.0";
    public BudgetVersionReferenceV1(string contractName,string contractVersion,Guid budgetId,Guid budgetVersionId,long budgetVersionNumber){ContractName=InvestmentCaseContextV1.Exact(contractName,ExpectedContractName);ContractVersion=InvestmentCaseContextV1.Exact(contractVersion,ExpectedContractVersion);BudgetId=InvestmentCaseContextV1.Id(budgetId);BudgetVersionId=InvestmentCaseContextV1.Id(budgetVersionId);BudgetVersionNumber=InvestmentCaseContextV1.Positive(budgetVersionNumber);}
    public string ContractName{get;} public string ContractVersion{get;} public Guid BudgetId{get;} public Guid BudgetVersionId{get;} public long BudgetVersionNumber{get;}
    public string ToExactJson()=>"{\"ContractName\":\""+ContractName+"\",\"ContractVersion\":\""+ContractVersion+"\",\"BudgetId\":\""+BudgetId.ToString("D")+"\",\"BudgetVersionId\":\""+BudgetVersionId.ToString("D")+"\",\"BudgetVersionNumber\":"+BudgetVersionNumber.ToString(CultureInfo.InvariantCulture)+"}";
    public static BudgetVersionReferenceV1 ParseExact(string json)=>FundingScenarioJson.Parse<BudgetVersionReferenceV1>(json,["ContractName","ContractVersion","BudgetId","BudgetVersionId","BudgetVersionNumber"],e=>new(FundingScenarioJson.String(e,"ContractName"),FundingScenarioJson.String(e,"ContractVersion"),FundingScenarioJson.Guid(e,"BudgetId"),FundingScenarioJson.Guid(e,"BudgetVersionId"),FundingScenarioJson.Int64(e,"BudgetVersionNumber")));
}

public sealed record ScenarioVersionReferenceV1
{
    public const string ExpectedContractName="fpa.scenario-planning-reference", ExpectedContractVersion="1.0";
    public ScenarioVersionReferenceV1(string contractName,string contractVersion,Guid scenarioId,Guid scenarioVersionId,long scenarioVersionNumber){ContractName=InvestmentCaseContextV1.Exact(contractName,ExpectedContractName);ContractVersion=InvestmentCaseContextV1.Exact(contractVersion,ExpectedContractVersion);ScenarioId=InvestmentCaseContextV1.Id(scenarioId);ScenarioVersionId=InvestmentCaseContextV1.Id(scenarioVersionId);ScenarioVersionNumber=InvestmentCaseContextV1.Positive(scenarioVersionNumber);}
    public string ContractName{get;} public string ContractVersion{get;} public Guid ScenarioId{get;} public Guid ScenarioVersionId{get;} public long ScenarioVersionNumber{get;}
    public string ToExactJson()=>"{\"ContractName\":\""+ContractName+"\",\"ContractVersion\":\""+ContractVersion+"\",\"ScenarioId\":\""+ScenarioId.ToString("D")+"\",\"ScenarioVersionId\":\""+ScenarioVersionId.ToString("D")+"\",\"ScenarioVersionNumber\":"+ScenarioVersionNumber.ToString(CultureInfo.InvariantCulture)+"}";
    public static ScenarioVersionReferenceV1 ParseExact(string json)=>FundingScenarioJson.Parse<ScenarioVersionReferenceV1>(json,["ContractName","ContractVersion","ScenarioId","ScenarioVersionId","ScenarioVersionNumber"],e=>new(FundingScenarioJson.String(e,"ContractName"),FundingScenarioJson.String(e,"ContractVersion"),FundingScenarioJson.Guid(e,"ScenarioId"),FundingScenarioJson.Guid(e,"ScenarioVersionId"),FundingScenarioJson.Int64(e,"ScenarioVersionNumber")));
}

public sealed record ComparatorOutputReferenceV1
{
    public const string ExpectedContractName="fpa.scenario-planning-reference", ExpectedContractVersion="1.0";
    public ComparatorOutputReferenceV1(string contractName,string contractVersion,Guid comparatorRunId,Guid comparatorOutputId,int outputVersion){ContractName=InvestmentCaseContextV1.Exact(contractName,ExpectedContractName);ContractVersion=InvestmentCaseContextV1.Exact(contractVersion,ExpectedContractVersion);ComparatorRunId=InvestmentCaseContextV1.Id(comparatorRunId);ComparatorOutputId=InvestmentCaseContextV1.Id(comparatorOutputId);OutputVersion=checked((int)InvestmentCaseContextV1.Positive(outputVersion));}
    public string ContractName{get;} public string ContractVersion{get;} public Guid ComparatorRunId{get;} public Guid ComparatorOutputId{get;} public int OutputVersion{get;}
    public string ToExactJson()=>"{\"ContractName\":\""+ContractName+"\",\"ContractVersion\":\""+ContractVersion+"\",\"ComparatorRunId\":\""+ComparatorRunId.ToString("D")+"\",\"ComparatorOutputId\":\""+ComparatorOutputId.ToString("D")+"\",\"OutputVersion\":"+OutputVersion.ToString(CultureInfo.InvariantCulture)+"}";
    public static ComparatorOutputReferenceV1 ParseExact(string json)=>FundingScenarioJson.Parse<ComparatorOutputReferenceV1>(json,["ContractName","ContractVersion","ComparatorRunId","ComparatorOutputId","OutputVersion"],e=>new(FundingScenarioJson.String(e,"ContractName"),FundingScenarioJson.String(e,"ContractVersion"),FundingScenarioJson.Guid(e,"ComparatorRunId"),FundingScenarioJson.Guid(e,"ComparatorOutputId"),FundingScenarioJson.Int32(e,"OutputVersion")));
}

public sealed record SelectedBudgetVersionReferenceV1
{
    public const string ExpectedContractName="ppm.investment-case-selected-budget-version-reference", ExpectedContractVersion="1.0";
    public SelectedBudgetVersionReferenceV1(string contractName,string contractVersion,InvestmentCaseContextV1 context,BudgetVersionReferenceV1 reference){ContractName=InvestmentCaseContextV1.Exact(contractName,ExpectedContractName);ContractVersion=InvestmentCaseContextV1.Exact(contractVersion,ExpectedContractVersion);InvestmentCaseContext=context??throw new ArgumentNullException(nameof(context));BudgetVersionReference=reference??throw new ArgumentNullException(nameof(reference));}
    public string ContractName{get;} public string ContractVersion{get;} public InvestmentCaseContextV1 InvestmentCaseContext{get;} public BudgetVersionReferenceV1 BudgetVersionReference{get;}
    public string ToExactJson()=>FundingScenarioJson.Wrapper(ContractName,ContractVersion,"BudgetVersionReference",InvestmentCaseContext.ToExactJson(),BudgetVersionReference.ToExactJson());
    public static SelectedBudgetVersionReferenceV1 ParseExact(string json)=>FundingScenarioJson.Parse<SelectedBudgetVersionReferenceV1>(json,["ContractName","ContractVersion","InvestmentCaseContext","BudgetVersionReference"],e=>new(FundingScenarioJson.String(e,"ContractName"),FundingScenarioJson.String(e,"ContractVersion"),InvestmentCaseContextV1.ParseExact(FundingScenarioJson.Object(e,"InvestmentCaseContext")),BudgetVersionReferenceV1.ParseExact(FundingScenarioJson.Object(e,"BudgetVersionReference"))));
}

public sealed record InvestmentCaseScenarioVersionReferenceV1
{
    public const string ExpectedContractName="ppm.investment-case-scenario-version-reference", ExpectedContractVersion="1.0";
    public InvestmentCaseScenarioVersionReferenceV1(string contractName,string contractVersion,InvestmentCaseContextV1 context,ScenarioVersionReferenceV1 reference){ContractName=InvestmentCaseContextV1.Exact(contractName,ExpectedContractName);ContractVersion=InvestmentCaseContextV1.Exact(contractVersion,ExpectedContractVersion);InvestmentCaseContext=context??throw new ArgumentNullException(nameof(context));ScenarioVersionReference=reference??throw new ArgumentNullException(nameof(reference));}
    public string ContractName{get;} public string ContractVersion{get;} public InvestmentCaseContextV1 InvestmentCaseContext{get;} public ScenarioVersionReferenceV1 ScenarioVersionReference{get;}
    public string ToExactJson()=>FundingScenarioJson.Wrapper(ContractName,ContractVersion,"ScenarioVersionReference",InvestmentCaseContext.ToExactJson(),ScenarioVersionReference.ToExactJson());
    public static InvestmentCaseScenarioVersionReferenceV1 ParseExact(string json)=>FundingScenarioJson.Parse<InvestmentCaseScenarioVersionReferenceV1>(json,["ContractName","ContractVersion","InvestmentCaseContext","ScenarioVersionReference"],e=>new(FundingScenarioJson.String(e,"ContractName"),FundingScenarioJson.String(e,"ContractVersion"),InvestmentCaseContextV1.ParseExact(FundingScenarioJson.Object(e,"InvestmentCaseContext")),ScenarioVersionReferenceV1.ParseExact(FundingScenarioJson.Object(e,"ScenarioVersionReference"))));
}

public sealed record InvestmentCaseComparatorOutputReferenceV1
{
    public const string ExpectedContractName="ppm.investment-case-comparator-output-reference", ExpectedContractVersion="1.0";
    public InvestmentCaseComparatorOutputReferenceV1(string contractName,string contractVersion,InvestmentCaseContextV1 context,ComparatorOutputReferenceV1 reference){ContractName=InvestmentCaseContextV1.Exact(contractName,ExpectedContractName);ContractVersion=InvestmentCaseContextV1.Exact(contractVersion,ExpectedContractVersion);InvestmentCaseContext=context??throw new ArgumentNullException(nameof(context));ComparatorOutputReference=reference??throw new ArgumentNullException(nameof(reference));}
    public string ContractName{get;} public string ContractVersion{get;} public InvestmentCaseContextV1 InvestmentCaseContext{get;} public ComparatorOutputReferenceV1 ComparatorOutputReference{get;}
    public string ToExactJson()=>FundingScenarioJson.Wrapper(ContractName,ContractVersion,"ComparatorOutputReference",InvestmentCaseContext.ToExactJson(),ComparatorOutputReference.ToExactJson());
    public static InvestmentCaseComparatorOutputReferenceV1 ParseExact(string json)=>FundingScenarioJson.Parse<InvestmentCaseComparatorOutputReferenceV1>(json,["ContractName","ContractVersion","InvestmentCaseContext","ComparatorOutputReference"],e=>new(FundingScenarioJson.String(e,"ContractName"),FundingScenarioJson.String(e,"ContractVersion"),InvestmentCaseContextV1.ParseExact(FundingScenarioJson.Object(e,"InvestmentCaseContext")),ComparatorOutputReferenceV1.ParseExact(FundingScenarioJson.Object(e,"ComparatorOutputReference"))));
}

public sealed record SelectedScenarioReferenceV1
{
    public const string ExpectedContractName="ppm.investment-case-selected-scenario-reference", ExpectedContractVersion="1.0";
    public SelectedScenarioReferenceV1(string contractName,string contractVersion,InvestmentCaseContextV1 context,ScenarioVersionReferenceV1 reference){ContractName=InvestmentCaseContextV1.Exact(contractName,ExpectedContractName);ContractVersion=InvestmentCaseContextV1.Exact(contractVersion,ExpectedContractVersion);InvestmentCaseContext=context??throw new ArgumentNullException(nameof(context));ScenarioVersionReference=reference??throw new ArgumentNullException(nameof(reference));}
    public string ContractName{get;} public string ContractVersion{get;} public InvestmentCaseContextV1 InvestmentCaseContext{get;} public ScenarioVersionReferenceV1 ScenarioVersionReference{get;}
    public string ToExactJson()=>FundingScenarioJson.Wrapper(ContractName,ContractVersion,"ScenarioVersionReference",InvestmentCaseContext.ToExactJson(),ScenarioVersionReference.ToExactJson());
    public static SelectedScenarioReferenceV1 ParseExact(string json)=>FundingScenarioJson.Parse<SelectedScenarioReferenceV1>(json,["ContractName","ContractVersion","InvestmentCaseContext","ScenarioVersionReference"],e=>new(FundingScenarioJson.String(e,"ContractName"),FundingScenarioJson.String(e,"ContractVersion"),InvestmentCaseContextV1.ParseExact(FundingScenarioJson.Object(e,"InvestmentCaseContext")),ScenarioVersionReferenceV1.ParseExact(FundingScenarioJson.Object(e,"ScenarioVersionReference"))));
}

internal static class FundingScenarioJson
{
    public static T Parse<T>(string json,string[] names,Func<JsonElement,T> factory){ArgumentNullException.ThrowIfNull(json);using var d=JsonDocument.Parse(json,new JsonDocumentOptions{AllowTrailingCommas=false,CommentHandling=JsonCommentHandling.Disallow});if(d.RootElement.ValueKind!=JsonValueKind.Object)throw new FormatException("object_required");var actual=d.RootElement.EnumerateObject().Select(x=>x.Name).ToArray();if(!actual.SequenceEqual(names,StringComparer.Ordinal))throw new FormatException("exact_property_order_required");return factory(d.RootElement);}
    public static string String(JsonElement e,string name){var p=e.GetProperty(name);return p.ValueKind==JsonValueKind.String?p.GetString()!:throw new FormatException("string_required");}
    public static Guid Guid(JsonElement e,string name){var s=String(e,name);return System.Guid.TryParseExact(s,"D",out var g)&&string.Equals(s,g.ToString("D"),StringComparison.Ordinal)?g:throw new FormatException("canonical_guid_required");}
    public static long Int64(JsonElement e,string name){var p=e.GetProperty(name);return p.ValueKind==JsonValueKind.Number&&p.TryGetInt64(out var v)?v:throw new FormatException("integer_required");}
    public static int Int32(JsonElement e,string name){var p=e.GetProperty(name);return p.ValueKind==JsonValueKind.Number&&p.TryGetInt32(out var v)?v:throw new FormatException("integer_required");}
    public static string Object(JsonElement e,string name){var p=e.GetProperty(name);return p.ValueKind==JsonValueKind.Object?p.GetRawText():throw new FormatException("object_required");}
    public static string Wrapper(string name,string version,string nestedName,string context,string nested)=>"{\"ContractName\":\""+name+"\",\"ContractVersion\":\""+version+"\",\"InvestmentCaseContext\":"+context+",\""+nestedName+"\":"+nested+"}";
}
