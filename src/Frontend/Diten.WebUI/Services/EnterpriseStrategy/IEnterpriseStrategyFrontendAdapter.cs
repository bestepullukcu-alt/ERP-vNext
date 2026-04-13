using Diten.WebUI.Models.EnterpriseStrategy;

namespace Diten.WebUI.Services.EnterpriseStrategy;

public interface IEnterpriseStrategyFrontendAdapter
{
    IReadOnlyList<Goal> UseGoals();
    IReadOnlyList<Objective> UseObjectives();
    IReadOnlyList<StrategyConnection> UseConnections();
    IReadOnlyList<InitiativeStrategyLinkView> UseInitiativeLinks();
    IReadOnlyList<ProjectStrategyLinkView> UseProjectLinks();
    IReadOnlyList<StrategyMetricSummaryCard> UseMetricCards();
}
