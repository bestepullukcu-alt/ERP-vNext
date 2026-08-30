using System.Globalization;
using System.Text.Json;

namespace Diten.PpmService.Domain.GateI.FundingScenario;


public sealed record ScenarioVersionReferenceV1
{
    public const string ExpectedContractName="fpa.scenario-planning-reference", ExpectedContractVersion="1.0";
    public ScenarioVersionReferenceV1(string contractName,string contractVersion,Guid scenarioId,Guid scenarioVersionId,long scenarioVersionNumber){ContractName=InvestmentCaseContextV1.Exact(contractName,ExpectedContractName);ContractVersion=InvestmentCaseContextV1.Exact(contractVersion,ExpectedContractVersion);ScenarioId=InvestmentCaseContextV1.Id(scenarioId);ScenarioVersionId=InvestmentCaseContextV1.Id(scenarioVersionId);ScenarioVersionNumber=InvestmentCaseContextV1.Positive(scenarioVersionNumber);}
    public string ContractName{get;} public string ContractVersion{get;} public Guid ScenarioId{get;} public Guid ScenarioVersionId{get;} public long ScenarioVersionNumber{get;}
    public string ToExactJson()=>"{\"ContractName\":\""+ContractName+"\",\"ContractVersion\":\""+ContractVersion+"\",\"ScenarioId\":\""+ScenarioId.ToString("D")+"\",\"ScenarioVersionId\":\""+ScenarioVersionId.ToString("D")+"\",\"ScenarioVersionNumber\":"+ScenarioVersionNumber.ToString(CultureInfo.InvariantCulture)+"}";
    public static ScenarioVersionReferenceV1 ParseExact(string json)=>FundingScenarioJson.Parse<ScenarioVersionReferenceV1>(json,["ContractName","ContractVersion","ScenarioId","ScenarioVersionId","ScenarioVersionNumber"],e=>new(FundingScenarioJson.String(e,"ContractName"),FundingScenarioJson.String(e,"ContractVersion"),FundingScenarioJson.Guid(e,"ScenarioId"),FundingScenarioJson.Guid(e,"ScenarioVersionId"),FundingScenarioJson.Int64(e,"ScenarioVersionNumber")));
}
