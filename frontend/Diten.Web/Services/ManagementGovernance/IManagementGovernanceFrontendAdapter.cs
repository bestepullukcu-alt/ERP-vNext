using Diten.Web.Models.ManagementGovernance;

namespace Diten.Web.Services.ManagementGovernance;

public interface IManagementGovernanceFrontendAdapter
{
    DomainSummary UseManagementGovernanceSummary();
    SubdomainSummary? UseSubdomainSummary(string slug);
    IReadOnlyList<WorkQueueItem> UseGovernanceWorkQueue();
    IReadOnlyList<GovernanceEvent> UseGovernanceCadence();
    IReadOnlyList<ReviewCycleItem> UseGovernanceReviewCycles();
    IReadOnlyList<RecentActivityItem> UseRecentGovernanceActivity();
    IReadOnlyList<RiskSignal> UseRiskSignals();
}
