using System.Security.Cryptography;
using System.Text;
using Diten.PpmService.Application.GateI;
using Diten.PpmService.Domain.GateI.FundingScenario;

namespace Diten.PpmService.Application.Features.InvestmentCases.GateI.FundingScenario;


public static class FundingScenarioAtomicLane
{
    public static readonly FundingScenarioProducerProfile Budgeting=new("MOD-0136","budgeting.budget-version-references.validate","budgeting.budget-version-references.validate","diten-fpa-service","diten.fpa","diten.s2s.delegated.invoke","711962a3fdc1226d947672dc9b48d29296c960a0","1949b93ead3dc1ac3234673bbe00ed67e3615743","diten.fpa.budgeting.audit","budgeting-mod-0136-fixture-current.test-only","execution/domains/enterprise-strategy-business-performance/module-packs/fixtures/MOD-0136/audit/budgeting-audit-intent-submitted-v1.signing-vector.json");
    public static readonly FundingScenarioProducerProfile ScenarioPlanning=new("MOD-0138","fpa.scenario-planning.references.validate","fpa.scenario-planning.references.validate","diten-fpa-service","diten.fpa","diten.s2s.delegated.invoke","3df680d6e006bfce19e382253ddd1f2f873c2295","acae87090f35e5e0a7f37ad66dd8e98fc69c07bb","diten.fpa.mod-0138.scenario-planning","mod-0138-scenario-planning-fixture-current","execution/domains/enterprise-strategy-business-performance/module-packs/fixtures/MOD-0138/audit/scenario-planning-audit-intent-submitted-v1.signing-vector.json");
    public static IReadOnlyList<FundingScenarioProducerProfile> RequiredProfiles=>[Budgeting,ScenarioPlanning];
}
