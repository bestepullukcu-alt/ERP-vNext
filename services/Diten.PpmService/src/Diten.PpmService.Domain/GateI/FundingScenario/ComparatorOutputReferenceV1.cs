using System.Globalization;
using System.Text.Json;

namespace Diten.PpmService.Domain.GateI.FundingScenario;


public sealed record ComparatorOutputReferenceV1
{
    public const string ExpectedContractName="fpa.scenario-planning-reference", ExpectedContractVersion="1.0";
    public ComparatorOutputReferenceV1(string contractName,string contractVersion,Guid comparatorRunId,Guid comparatorOutputId,int outputVersion){ContractName=InvestmentCaseContextV1.Exact(contractName,ExpectedContractName);ContractVersion=InvestmentCaseContextV1.Exact(contractVersion,ExpectedContractVersion);ComparatorRunId=InvestmentCaseContextV1.Id(comparatorRunId);ComparatorOutputId=InvestmentCaseContextV1.Id(comparatorOutputId);OutputVersion=checked((int)InvestmentCaseContextV1.Positive(outputVersion));}
    public string ContractName{get;} public string ContractVersion{get;} public Guid ComparatorRunId{get;} public Guid ComparatorOutputId{get;} public int OutputVersion{get;}
    public string ToExactJson()=>"{\"ContractName\":\""+ContractName+"\",\"ContractVersion\":\""+ContractVersion+"\",\"ComparatorRunId\":\""+ComparatorRunId.ToString("D")+"\",\"ComparatorOutputId\":\""+ComparatorOutputId.ToString("D")+"\",\"OutputVersion\":"+OutputVersion.ToString(CultureInfo.InvariantCulture)+"}";
    public static ComparatorOutputReferenceV1 ParseExact(string json)=>FundingScenarioJson.Parse<ComparatorOutputReferenceV1>(json,["ContractName","ContractVersion","ComparatorRunId","ComparatorOutputId","OutputVersion"],e=>new(FundingScenarioJson.String(e,"ContractName"),FundingScenarioJson.String(e,"ContractVersion"),FundingScenarioJson.Guid(e,"ComparatorRunId"),FundingScenarioJson.Guid(e,"ComparatorOutputId"),FundingScenarioJson.Int32(e,"OutputVersion")));
}
