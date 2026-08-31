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
