using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Infrastructure.Eventing;

internal sealed record GateIAuditMapping(
    string Producer,
    string ModuleCode,
    string EventName,
    int EventVersion,
    string Scheme,
    AuditCategory Category,
    string EntityType,
    string Mutation,
    string AuditEntityType,
    AuditOperation Operation,
    AuditOutcome Outcome = AuditOutcome.Succeeded);

internal sealed class GateIAuditAdapterRegistry
{
    private readonly IReadOnlyDictionary<(string Producer, string EventName, int Version, string Entity, string Mutation), GateIAuditMapping> _mappings;

    public GateIAuditAdapterRegistry()
    {
        var mappings = new List<GateIAuditMapping>();
        AddDecisionRegistry(mappings);
        AddBudgeting(mappings);
        AddScenarioPlanning(mappings);
        AddOutcomeTracking(mappings);
        _mappings = mappings.ToDictionary(
            item => (item.Producer, item.EventName, item.EventVersion, item.EntityType, item.Mutation),
            item => item);
    }

    public GateIAuditMapping Resolve(
        string producer,
        string eventName,
        int version,
        string entityType,
        string mutation)
    {
        return _mappings.TryGetValue((producer, eventName, version, entityType, mutation), out var mapping)
            ? mapping
            : throw new Diten.BuildingBlocks.Eventing.EventUnsupportedException(
                "The Gate I producer schema or entity/mutation pair is unsupported.",
                "gate-i.mapping.unsupported");
    }

    public IReadOnlyList<GateIAuditMapping> ForProducer(string producer, string eventName, int version) =>
        _mappings.Values.Where(item =>
                string.Equals(item.Producer, producer, StringComparison.Ordinal)
                && string.Equals(item.EventName, eventName, StringComparison.Ordinal)
                && item.EventVersion == version)
            .ToArray();

    private static void AddDecisionRegistry(List<GateIAuditMapping> values)
    {
        const string producer = "Diten.ManagementGovernanceService";
        const string name = "management-governance.decision-registry-audit-intent.submitted.v1";
        const string scheme = "management-governance-event-hmac-sha256.v1";
        Add(values, producer, "MOD-0007", name, scheme, AuditCategory.Integration,
            "DecisionDraft", "Created", "DecisionRegistry.DecisionDraft", AuditOperation.Create);
        Add(values, producer, "MOD-0007", name, scheme, AuditCategory.Integration,
            "DecisionDraft", "Revised", "DecisionRegistry.DecisionDraft", AuditOperation.Update);
        Add(values, producer, "MOD-0007", name, scheme, AuditCategory.Integration,
            "DecisionDraft", "SoftDeleted", "DecisionRegistry.DecisionDraft", AuditOperation.Delete);
        Add(values, producer, "MOD-0007", name, scheme, AuditCategory.Integration,
            "DecisionRecord", "Published", "DecisionRegistry.DecisionRecord", AuditOperation.Activate);
        Add(values, producer, "MOD-0007", name, scheme, AuditCategory.Integration,
            "DecisionRecord", "Superseded", "DecisionRegistry.DecisionRecord", AuditOperation.Deactivate);
        Add(values, producer, "MOD-0007", name, scheme, AuditCategory.Integration,
            "DecisionRecord", "Withdrawn", "DecisionRegistry.DecisionRecord", AuditOperation.Deactivate);
    }

    private static void AddBudgeting(List<GateIAuditMapping> values)
    {
        const string producer = "Diten.FpaService";
        const string name = "fpa.budgeting-audit-intent.submitted.v1";
        const string scheme = "fpa-budgeting-event-hmac-sha256.v1";
        AddSet(values, producer, "MOD-0136", name, scheme, AuditCategory.System, "Budget",
            [("created", AuditOperation.Create), ("updated", AuditOperation.Update), ("archived", AuditOperation.Deactivate)]);
        AddSet(values, producer, "MOD-0136", name, scheme, AuditCategory.System, "BudgetFiscalCalendar",
            [("created", AuditOperation.Create), ("updated", AuditOperation.Update), ("archived", AuditOperation.Deactivate)]);
        AddSet(values, producer, "MOD-0136", name, scheme, AuditCategory.System, "BudgetVersionDraft",
            [("created", AuditOperation.Create), ("updated", AuditOperation.Update), ("abandoned", AuditOperation.Deactivate)]);
        AddSet(values, producer, "MOD-0136", name, scheme, AuditCategory.System, "BudgetVersion",
            [("certified", AuditOperation.Activate), ("retired", AuditOperation.Deactivate), ("superseded", AuditOperation.Deactivate)]);
        AddSet(values, producer, "MOD-0136", name, scheme, AuditCategory.System, "FundingBaselineSelection",
            [("replaced", AuditOperation.Update), ("closed", AuditOperation.Deactivate)]);
    }

    private static void AddScenarioPlanning(List<GateIAuditMapping> values)
    {
        const string producer = "Diten.FpaService";
        const string name = "fpa.scenario-planning-audit-intent.submitted.v1";
        const string scheme = "fpa-scenario-planning-event-hmac-sha256.v1";
        AddSet(values, producer, "MOD-0138", name, scheme, AuditCategory.Integration, "Scenario",
            [("Created", AuditOperation.Create), ("Updated", AuditOperation.Update), ("Archived", AuditOperation.Deactivate)]);
        AddSet(values, producer, "MOD-0138", name, scheme, AuditCategory.Integration, "ScenarioVersionDraft",
            [("Created", AuditOperation.Create), ("Updated", AuditOperation.Update), ("Abandoned", AuditOperation.Delete)]);
        AddSet(values, producer, "MOD-0138", name, scheme, AuditCategory.Integration, "ScenarioVersion",
            [("Published", AuditOperation.Create), ("Retired", AuditOperation.Deactivate)]);
        AddSet(values, producer, "MOD-0138", name, scheme, AuditCategory.Integration, "ComparatorRun",
            [("Requested", AuditOperation.Create), ("Started", AuditOperation.Execute), ("Completed", AuditOperation.Execute), ("Failed", AuditOperation.Execute)]);
        AddSet(values, producer, "MOD-0138", name, scheme, AuditCategory.Integration, "ScenarioSelection",
            [("Replaced", AuditOperation.Update), ("Closed", AuditOperation.Deactivate)]);
        var failed = values.FindIndex(item => item.EventName == name && item.EntityType == "ComparatorRun" && item.Mutation == "Failed");
        values[failed] = values[failed] with { Outcome = AuditOutcome.Failed };
    }

    private static void AddOutcomeTracking(List<GateIAuditMapping> values)
    {
        const string producer = "Diten.DecisionIntelligenceService";
        const string name = "decision-intelligence.outcome-tracking-audit-intent.submitted.v1";
        const string scheme = "decision-intelligence-event-hmac-sha256.v1";
        AddSet(values, producer, "MOD-0072", name, scheme, AuditCategory.Integration, "Outcome",
            [("Created", AuditOperation.Create), ("Retired", AuditOperation.Deactivate)]);
        Add(values, producer, "MOD-0072", name, scheme, AuditCategory.Integration,
            "OutcomeVersion", "Published", "OutcomeTracking.OutcomeVersion", AuditOperation.Activate);
        AddSet(values, producer, "MOD-0072", name, scheme, AuditCategory.Integration, "OutcomeMeasurement",
            [("Appended", AuditOperation.Create), ("Corrected", AuditOperation.Update)]);
    }

    private static void AddSet(
        List<GateIAuditMapping> values, string producer, string module, string name, string scheme,
        AuditCategory category, string entity, (string Mutation, AuditOperation Operation)[] pairs)
    {
        foreach (var pair in pairs)
        {
            var prefix = module switch
            {
                "MOD-0136" => "Budgeting",
                "MOD-0138" => "ScenarioPlanning",
                "MOD-0072" => "OutcomeTracking",
                _ => throw new InvalidOperationException("Unknown Gate I module.")
            };
            Add(values, producer, module, name, scheme, category, entity, pair.Mutation,
                $"{prefix}.{entity}", pair.Operation);
        }
    }

    private static void Add(
        List<GateIAuditMapping> values, string producer, string module, string name, string scheme,
        AuditCategory category, string entity, string mutation, string auditEntity,
        AuditOperation operation) => values.Add(new(
            producer, module, name, 1, scheme, category, entity, mutation, auditEntity, operation));
}
