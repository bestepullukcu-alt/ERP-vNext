namespace Diten.InventoryService.Domain.Recall.Enums;

/// <summary>WP-0177-R5 (pack §4.11) — how often a drill plan is due (RECALL <c>DrillPlanInput.frequency</c>).</summary>
public enum RecallDrillFrequency
{
    Yearly1,
    Yearly2,
    Quarterly,
    CustomDays
}

/// <summary>Which lot a drill takes (pack §4.11): a random finished-good lot, a random raw-material lot, or one the user selects.</summary>
public enum RecallDrillScopeKind
{
    RandomFinishedGoodLot,
    RawMaterialLot,
    SelectedLot
}

public enum RecallDrillRunStatus
{
    Running,
    Completed,
    Cancelled
}

public enum RecallDrillSelectionMode
{
    Auto,
    Manual
}

/// <summary>The three steps, in this order (pack §8.10, 409 <c>RECALL_DRILL_STEP_ORDER</c>).</summary>
public enum RecallDrillStep
{
    Trace,
    Scope,
    CustomerList
}

public enum RecallDrillEventKind
{
    PlanCreated,
    PlanUpdated,
    Started,
    StepCompleted,
    ResultPrepared,
    Completed,
    Cancelled
}
