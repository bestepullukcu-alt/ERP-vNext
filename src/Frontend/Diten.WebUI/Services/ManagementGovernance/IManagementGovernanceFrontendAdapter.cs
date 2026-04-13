using Diten.WebUI.Models.ManagementGovernance;

namespace Diten.WebUI.Services.ManagementGovernance;

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
