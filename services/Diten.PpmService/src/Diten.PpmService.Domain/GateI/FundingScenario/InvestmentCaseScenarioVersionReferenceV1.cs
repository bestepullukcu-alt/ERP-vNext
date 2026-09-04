using System.Globalization;
using System.Text.Json;

namespace Diten.PpmService.Domain.GateI.FundingScenario;


public sealed record InvestmentCaseScenarioVersionReferenceV1
{
    public const string ExpectedContractName="ppm.investment-case-scenario-version-reference", ExpectedContractVersion="1.0";
    public InvestmentCaseScenarioVersionReferenceV1(string contractName,string contractVersion,InvestmentCaseContextV1 context,ScenarioVersionReferenceV1 reference){ContractName=InvestmentCaseContextV1.Exact(contractName,ExpectedContractName);ContractVersion=InvestmentCaseContextV1.Exact(contractVersion,ExpectedContractVersion);InvestmentCaseContext=context??throw new ArgumentNullException(nameof(context));ScenarioVersionReference=reference??throw new ArgumentNullException(nameof(reference));}
    public string ContractName{get;} public string ContractVersion{get;} public InvestmentCaseContextV1 InvestmentCaseContext{get;} public ScenarioVersionReferenceV1 ScenarioVersionReference{get;}
    public string ToExactJson()=>FundingScenarioJson.Wrapper(ContractName,ContractVersion,"ScenarioVersionReference",InvestmentCaseContext.ToExactJson(),ScenarioVersionReference.ToExactJson());
    public static InvestmentCaseScenarioVersionReferenceV1 ParseExact(string json)=>FundingScenarioJson.Parse<InvestmentCaseScenarioVersionReferenceV1>(json,["ContractName","ContractVersion","InvestmentCaseContext","ScenarioVersionReference"],e=>new(FundingScenarioJson.String(e,"ContractName"),FundingScenarioJson.String(e,"ContractVersion"),InvestmentCaseContextV1.ParseExact(FundingScenarioJson.Object(e,"InvestmentCaseContext")),ScenarioVersionReferenceV1.ParseExact(FundingScenarioJson.Object(e,"ScenarioVersionReference"))));
}
