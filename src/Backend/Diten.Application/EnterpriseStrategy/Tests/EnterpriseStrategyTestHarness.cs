namespace Diten.Application.EnterpriseStrategy.Tests;

// Shared, framework-agnostic harness methods that module tests can call.
public static class EnterpriseStrategyTestHarness
{
    public static string[] AuthorizationPermissions() =>
    [
        "strategy.goal.view",
        "strategy.goal.create",
        "strategy.goal.edit",
        "strategy.goal.archive",
        "strategy.goal.activate",
        "strategy.objective.view",
        "strategy.objective.create",
        "strategy.objective.edit",
        "strategy.objective.archive",
        "strategy.objective.activate",
        "strategy.connection.view",
        "strategy.connection.create",
        "strategy.connection.edit",
        "strategy.connection.delete",
        "strategy.connection.validate",
        "strategy.initiative.view",
        "strategy.initiative.link",
        "strategy.initiative.unlink",
        "strategy.initiative.sync",
        "strategy.project.view",
        "strategy.project.link",
        "strategy.project.unlink",
        "strategy.project.sync"
    ];

    public static bool IsStaleWrite(int expectedVersion, int currentVersion)
        => expectedVersion > 0 && expectedVersion != currentVersion;

    public static bool IsDependencyUnavailable(bool healthy)
        => !healthy;
}
