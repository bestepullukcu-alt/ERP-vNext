using System.Globalization;
using System.Text.Json;

namespace Diten.PpmService.Domain.GateI.FundingScenario;


public sealed record InvestmentCaseComparatorOutputReferenceV1
{
    public const string ExpectedContractName="ppm.investment-case-comparator-output-reference", ExpectedContractVersion="1.0";
    public InvestmentCaseComparatorOutputReferenceV1(string contractName,string contractVersion,InvestmentCaseContextV1 context,ComparatorOutputReferenceV1 reference){ContractName=InvestmentCaseContextV1.Exact(contractName,ExpectedContractName);ContractVersion=InvestmentCaseContextV1.Exact(contractVersion,ExpectedContractVersion);InvestmentCaseContext=context??throw new ArgumentNullException(nameof(context));ComparatorOutputReference=reference??throw new ArgumentNullException(nameof(reference));}
    public string ContractName{get;} public string ContractVersion{get;} public InvestmentCaseContextV1 InvestmentCaseContext{get;} public ComparatorOutputReferenceV1 ComparatorOutputReference{get;}
    public string ToExactJson()=>FundingScenarioJson.Wrapper(ContractName,ContractVersion,"ComparatorOutputReference",InvestmentCaseContext.ToExactJson(),ComparatorOutputReference.ToExactJson());
    public static InvestmentCaseComparatorOutputReferenceV1 ParseExact(string json)=>FundingScenarioJson.Parse<InvestmentCaseComparatorOutputReferenceV1>(json,["ContractName","ContractVersion","InvestmentCaseContext","ComparatorOutputReference"],e=>new(FundingScenarioJson.String(e,"ContractName"),FundingScenarioJson.String(e,"ContractVersion"),InvestmentCaseContextV1.ParseExact(FundingScenarioJson.Object(e,"InvestmentCaseContext")),ComparatorOutputReferenceV1.ParseExact(FundingScenarioJson.Object(e,"ComparatorOutputReference"))));
}
