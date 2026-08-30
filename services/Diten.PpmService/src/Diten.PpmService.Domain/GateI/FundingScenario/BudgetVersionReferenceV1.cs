using System.Globalization;
using System.Text.Json;

namespace Diten.PpmService.Domain.GateI.FundingScenario;


public sealed record BudgetVersionReferenceV1
{
    public const string ExpectedContractName="fpa.budget-version-reference", ExpectedContractVersion="1.0";
    public BudgetVersionReferenceV1(string contractName,string contractVersion,Guid budgetId,Guid budgetVersionId,long budgetVersionNumber){ContractName=InvestmentCaseContextV1.Exact(contractName,ExpectedContractName);ContractVersion=InvestmentCaseContextV1.Exact(contractVersion,ExpectedContractVersion);BudgetId=InvestmentCaseContextV1.Id(budgetId);BudgetVersionId=InvestmentCaseContextV1.Id(budgetVersionId);BudgetVersionNumber=InvestmentCaseContextV1.Positive(budgetVersionNumber);}
    public string ContractName{get;} public string ContractVersion{get;} public Guid BudgetId{get;} public Guid BudgetVersionId{get;} public long BudgetVersionNumber{get;}
    public string ToExactJson()=>"{\"ContractName\":\""+ContractName+"\",\"ContractVersion\":\""+ContractVersion+"\",\"BudgetId\":\""+BudgetId.ToString("D")+"\",\"BudgetVersionId\":\""+BudgetVersionId.ToString("D")+"\",\"BudgetVersionNumber\":"+BudgetVersionNumber.ToString(CultureInfo.InvariantCulture)+"}";
    public static BudgetVersionReferenceV1 ParseExact(string json)=>FundingScenarioJson.Parse<BudgetVersionReferenceV1>(json,["ContractName","ContractVersion","BudgetId","BudgetVersionId","BudgetVersionNumber"],e=>new(FundingScenarioJson.String(e,"ContractName"),FundingScenarioJson.String(e,"ContractVersion"),FundingScenarioJson.Guid(e,"BudgetId"),FundingScenarioJson.Guid(e,"BudgetVersionId"),FundingScenarioJson.Int64(e,"BudgetVersionNumber")));
}
