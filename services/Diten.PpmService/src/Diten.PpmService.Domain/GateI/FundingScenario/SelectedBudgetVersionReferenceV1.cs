using System.Globalization;
using System.Text.Json;

namespace Diten.PpmService.Domain.GateI.FundingScenario;


public sealed record SelectedBudgetVersionReferenceV1
{
    public const string ExpectedContractName="ppm.investment-case-selected-budget-version-reference", ExpectedContractVersion="1.0";
    public SelectedBudgetVersionReferenceV1(string contractName,string contractVersion,InvestmentCaseContextV1 context,BudgetVersionReferenceV1 reference){ContractName=InvestmentCaseContextV1.Exact(contractName,ExpectedContractName);ContractVersion=InvestmentCaseContextV1.Exact(contractVersion,ExpectedContractVersion);InvestmentCaseContext=context??throw new ArgumentNullException(nameof(context));BudgetVersionReference=reference??throw new ArgumentNullException(nameof(reference));}
    public string ContractName{get;} public string ContractVersion{get;} public InvestmentCaseContextV1 InvestmentCaseContext{get;} public BudgetVersionReferenceV1 BudgetVersionReference{get;}
    public string ToExactJson()=>FundingScenarioJson.Wrapper(ContractName,ContractVersion,"BudgetVersionReference",InvestmentCaseContext.ToExactJson(),BudgetVersionReference.ToExactJson());
    public static SelectedBudgetVersionReferenceV1 ParseExact(string json)=>FundingScenarioJson.Parse<SelectedBudgetVersionReferenceV1>(json,["ContractName","ContractVersion","InvestmentCaseContext","BudgetVersionReference"],e=>new(FundingScenarioJson.String(e,"ContractName"),FundingScenarioJson.String(e,"ContractVersion"),InvestmentCaseContextV1.ParseExact(FundingScenarioJson.Object(e,"InvestmentCaseContext")),BudgetVersionReferenceV1.ParseExact(FundingScenarioJson.Object(e,"BudgetVersionReference"))));
}
