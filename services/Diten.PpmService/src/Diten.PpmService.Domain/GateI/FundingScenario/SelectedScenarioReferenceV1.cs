using System.Globalization;
using System.Text.Json;

namespace Diten.PpmService.Domain.GateI.FundingScenario;


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
