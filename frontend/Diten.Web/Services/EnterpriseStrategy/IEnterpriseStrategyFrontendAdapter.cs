using Diten.Web.Models.EnterpriseStrategy;

namespace Diten.Web.Services.EnterpriseStrategy;

public interface IEnterpriseStrategyFrontendAdapter
{
    IReadOnlyList<Goal> UseGoals();
    IReadOnlyList<Objective> UseObjectives();
    IReadOnlyList<StrategyConnection> UseConnections();
    IReadOnlyList<InitiativeStrategyLinkView> UseInitiativeLinks();
    IReadOnlyList<ProjectStrategyLinkView> UseProjectLinks();
    IReadOnlyList<StrategyMetricSummaryCard> UseMetricCards();
}
