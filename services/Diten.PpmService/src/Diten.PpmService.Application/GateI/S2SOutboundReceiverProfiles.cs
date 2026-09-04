using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Diten.PpmService.Application.GateI;


public static class S2SOutboundReceiverProfiles
{
    public static readonly S2SOutboundReceiverProfile DecisionRegistry = new(
        "MOD-0007", "POST", "/internal/v1/decision-registry/decision-references/validate",
        "diten-management-governance-service", "diten.management-governance",
        "decision-registry.decision-references.validate.v1",
        "management-governance.decision-references.validate");

    public static readonly S2SOutboundReceiverProfile Budgeting = new(
        "MOD-0136", "POST", "/internal/v1/fpa/budgeting/budget-version-references/validate",
        "diten-fpa-service", "diten.fpa",
        "budgeting.budget-version-references.validate",
        "budgeting.budget-version-references.validate");

    public static readonly S2SOutboundReceiverProfile ScenarioPlanning = new(
        "MOD-0138", "POST", "/internal/v1/fpa/scenario-planning/references/validate",
        "diten-fpa-service", "diten.fpa",
        "fpa.scenario-planning.references.validate",
        "fpa.scenario-planning.references.validate");

    public static readonly S2SOutboundReceiverProfile OutcomeTracking = new(
        "MOD-0072", "POST", "/internal/v1/decision-intelligence/outcome-tracking/outcome-references/validate",
        "diten-decision-intelligence-service", "diten.decision-intelligence",
        "outcome-tracking.outcome-references.validate",
        "decision-intelligence.outcome-references.validate");

    public static IReadOnlyList<S2SOutboundReceiverProfile> All { get; } =
        [DecisionRegistry, Budgeting, ScenarioPlanning, OutcomeTracking];

    public static S2SOutboundReceiverProfile ForOwner(string ownerModule) =>
        All.Single(profile => string.Equals(profile.OwnerModule, ownerModule, StringComparison.Ordinal));
}
