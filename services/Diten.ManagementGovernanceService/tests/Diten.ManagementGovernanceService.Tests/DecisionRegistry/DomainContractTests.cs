using Diten.ManagementGovernanceService.Application.DecisionRegistry;
using Diten.ManagementGovernanceService.Domain.DecisionRegistry;
using Xunit;

namespace Diten.ManagementGovernanceService.Tests.DecisionRegistry;

public sealed class DomainContractTests
{
    [Fact] public void Permission_manifest_is_exactly_seven_permissions_and_eight_operations()
    { Assert.Equal(8, DecisionRegistryPermissions.OperationMap.Count); Assert.Equal(7, DecisionRegistryPermissions.OperationMap.Values.Distinct().Count()); Assert.DoesNotContain(DecisionRegistryPermissions.OperationMap.Keys, x => x.Contains('*')); }

    [Fact] public void References_have_exact_contract_shapes()
    { var reference = new DecisionRevisionReferenceV1(Guid.NewGuid(), Guid.NewGuid(), 1); Assert.Equal(5, reference.GetType().GetProperties().Length); var authority = new DecisionAuthorityReferenceV1(DecisionAuthorityKind.Person, Guid.NewGuid()); Assert.Equal(4, authority.GetType().GetProperties().Length); }

    [Fact] public void Published_revision_is_born_final_and_terminal_record_cannot_reopen()
    { var actor=Guid.NewGuid(); var record=DecisionRecord.Create(Guid.NewGuid(),Guid.NewGuid(),"InvestmentCase",Guid.NewGuid(),actor); record.Publish(Guid.NewGuid(),1,1); record.Withdraw(2); Assert.Throws<DecisionRegistryDomainException>(()=>record.Publish(Guid.NewGuid(),2,3)); }

    [Fact] public void Same_decision_revision_number_is_monotonic()
    { var record=DecisionRecord.Create(Guid.NewGuid(),Guid.NewGuid(),"InvestmentCase",Guid.NewGuid(),Guid.NewGuid()); record.Publish(Guid.NewGuid(),1,1); Assert.Throws<DecisionRegistryDomainException>(()=>record.Publish(Guid.NewGuid(),3,2)); }

    [Fact] public void Prepare_publish_separation_is_mandatory()
    { var actor=Guid.NewGuid(); var draft=DecisionDraft.Create(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),"text","result","rationale",new DecisionAuthorityReferenceV1(DecisionAuthorityKind.Position,Guid.NewGuid()),new Diten.ManagementGovernanceService.Domain.DecisionRegistry.TypedReference("source","1.0",Guid.NewGuid()),actor); Assert.Throws<DecisionRegistryDomainException>(()=>draft.RequirePublisherSeparation(actor)); }

    [Fact] public void Audit_contract_is_exact()
    { Assert.Equal(1,DecisionRegistryAuditIntentSubmittedV1.EventVersion); Assert.Equal("MOD-0007",DecisionRegistryAuditIntentSubmittedV1.ModuleCode); Assert.Equal(6,typeof(DecisionRegistryAuditIntentSubmittedV1).GetProperties().Length); }
}
