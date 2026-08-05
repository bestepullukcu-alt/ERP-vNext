namespace Diten.ManagementGovernanceService.Persistence.DecisionRegistry.Migrations;

public sealed record QuarantineOnlyMigrationEvidence(int Imported, int Rewritten, int Deleted, bool ReadOnlyInventory);

public sealed class DecisionRegistryInitialMigration
{
    public QuarantineOnlyMigrationEvidence Inspect() => new(0, 0, 0, true);
}
