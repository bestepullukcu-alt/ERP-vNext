using Diten.PpmService.Domain.Entities;
using Xunit;

namespace Diten.PpmService.Tests.Initiatives;

public sealed class InitiativeCoreV2DomainTests
{
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _actor = Guid.NewGuid();

    [Fact]
    public void Proposed_allows_nullable_classifications_and_dates()
    {
        var initiative = New();
        Assert.Null(initiative.InitiativeTypeCode);
        Assert.Null(initiative.PriorityCode);
        Assert.Null(initiative.PlannedStartDate);
        Assert.Null(initiative.PlannedEndDate);
        Assert.False(initiative.IsActivationReady);
    }

    [Fact]
    public void One_planning_date_is_allowed_but_reverse_complete_range_is_rejected()
    {
        var onlyStart = New(start: new DateOnly(2026, 9, 2));
        Assert.Null(onlyStart.PlannedEndDate);
        Assert.Throws<ArgumentException>(() => New(start: new DateOnly(2026, 9, 3), end: new DateOnly(2026, 9, 2)));
    }

    [Fact]
    public void Exact_vocabularies_round_trip_and_reject_unknown_values()
    {
        AssertVocabulary(
            ["strategic-realignment", "funding-withdrawn", "business-case-rejected", "duplicate-initiative",
                "regulatory-block", "capacity-unavailable", "no-longer-viable", "superseded"],
            InitiativeVocabularies.CancellationReasons, InitiativeVocabularies.RequireCancellationReason);
        AssertVocabulary(
            ["funding-paused", "capacity-constraint", "dependency-blocked", "governance-review",
                "strategy-review", "external-constraint"],
            InitiativeVocabularies.HoldReasons, InitiativeVocabularies.RequireHoldReason);
        AssertVocabulary(
            ["delivered-as-planned", "delivered-with-variance", "partially-delivered", "transferred-to-operations"],
            InitiativeVocabularies.CompletionOutcomes, InitiativeVocabularies.RequireCompletionOutcome);
        AssertVocabulary(
            ["scope-completed", "planned-end-reached", "governance-directed-close", "early-completion"],
            InitiativeVocabularies.ClosureReasons, InitiativeVocabularies.RequireClosureReason);
        AssertVocabulary(
            ["tracking-required", "tracking-in-progress", "handed-off-to-outcome-owner", "no-benefit-commitment"],
            InitiativeVocabularies.BenefitDispositions, InitiativeVocabularies.RequireBenefitDisposition);
    }

    [Fact]
    public void Terminal_initiative_rejects_edit_and_lifecycle_mutation()
    {
        var initiative = New("type", "priority", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 2));
        initiative.Transition(_actor, InitiativeLifecycleState.Active);
        initiative.Transition(_actor, InitiativeLifecycleState.Completed);
        Assert.Throws<InvalidOperationException>(() => initiative.Update(_actor, "I-2", "Changed", null, null,
            "type", "priority", null, null));
        Assert.Throws<InvalidOperationException>(() => initiative.Transition(_actor, InitiativeLifecycleState.Active));
    }

    [Fact]
    public void Supersedes_identity_is_create_only()
    {
        var oldId = Guid.NewGuid();
        var initiative = new Initiative(_tenant, _actor, "I-1", "Initiative", null, null,
            null, null, null, null, oldId);
        initiative.Update(_actor, "I-1", "Changed", null, null, null, null, null, null);
        Assert.Equal(oldId, initiative.SupersedesInitiativeId);
    }

    [Fact]
    public void Closure_contains_exact_business_values_and_server_utc_completion()
    {
        var initiative = New();
        var completedAt = DateTime.UtcNow;
        var closure = new InitiativeClosure(_tenant, _actor, initiative.Id, "delivered-as-planned",
            "scope-completed", completedAt, "Completed safely.", [], [], "tracking-required",
            initiative.CreatedAtUtc);
        Assert.Equal(initiative.Id, closure.InitiativeId);
        Assert.Equal(completedAt, closure.CompletedAt);
        Assert.Empty(closure.EvidenceReferences);
        Assert.Empty(closure.FollowUpTaskReferences);
    }

    private Initiative New(string? type = null, string? priority = null, DateOnly? start = null, DateOnly? end = null) =>
        new(_tenant, _actor, "I-1", "Initiative", null, null, type, priority, start, end);

    private static void AssertVocabulary(
        IReadOnlyList<string> expected,
        IReadOnlyList<string> actual,
        Func<string, string> require)
    {
        Assert.Equal(expected, actual);
        Assert.Equal(expected, expected.Select(require));
        Assert.Throws<ArgumentException>(() => require("out-of-set"));
        Assert.Throws<ArgumentException>(() => require(expected[0].ToUpperInvariant()));
        Assert.Throws<ArgumentException>(() => require($" {expected[0]}"));
    }
}
