using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// The closure outcome dictionary — what a task type accepts as an ENDING.
///
/// <para>The defect behind this slice was not a missing field. <c>TaskItem.ClosureReasonCode</c> has existed on
/// the entity, in the mapper and in the detail DTO since the engine shipped; it appeared in zero files under
/// <c>frontend/</c> and was written null on every single close, because the browser's transition vocabulary
/// hard-coded <c>reasonCode: null</c>. So the column was unread AND empty, and the two halves hid each other:
/// nobody looked for a value nothing displayed.</para>
///
/// <para>These tests pin the three things that make the column real — a vocabulary to write, a refusal for what
/// is outside it, and the outcome-level reason flag — plus the one rule that must never break: a type with no
/// dictionary closes exactly as it always did.</para>
/// </summary>
public sealed class TaskClosureOutcomeTests
{
    private static TaskClosureOutcome Tenant(
        string code, TaskClosureDisposition disposition, bool requiresReason = false, int sort = 0) => new()
    {
        Code = code,
        LabelText = code + " (as typed)",
        Disposition = disposition,
        RequiresReason = requiresReason,
        SortOrder = sort
    };

    // ── The label split, inherited from TaskFieldDefinition rather than reinvented ────────────────────────

    [Fact]
    public void An_outcome_carries_exactly_one_label_source()
    {
        /*
         * The same rule TaskFieldDefinition makes for its own labels, and for the same reason its comment gives:
         * conflating a resource key with an author's words is how a raw key reaches the screen.
         */
        var neither = TaskTypeRules.NormalizeClosureOutcomes([
            new TaskClosureOutcome { Code = "X", Disposition = TaskClosureDisposition.Completed }
        ]);
        Assert.Null(neither.Value);
        Assert.Equal(TaskReasonCodes.TaskTypeClosureOutcomeInvalid, neither.Error!.Value.ReasonCode);

        var both = TaskTypeRules.NormalizeClosureOutcomes([
            new TaskClosureOutcome
            {
                Code = "X",
                LabelResourceKey = "WorkAggregation_ClosureOutcome_CompletedPartially",
                LabelText = "Half done",
                Disposition = TaskClosureDisposition.Completed
            }
        ]);
        Assert.Null(both.Value);
        Assert.Equal(TaskReasonCodes.TaskTypeClosureOutcomeInvalid, both.Error!.Value.ReasonCode);
    }

    [Fact]
    public void A_tenant_outcome_may_not_squat_on_a_system_code()
    {
        /*
         * A system code is bound to a resx entry translated in seven languages. A tenant row reusing the code
         * with its own words would inherit that translation and mean something else inside it — the reader would
         * see one sentence and the report would group by another.
         */
        var refused = TaskTypeRules.NormalizeClosureOutcomes([
            Tenant(TaskClosureOutcomeCatalog.CompletedPartially, TaskClosureDisposition.Completed)
        ]);

        Assert.Null(refused.Value);
        Assert.Equal(TaskTypeRules.OutcomeSystemCodeReservedMessage, refused.Error!.Value.Message);
    }

    [Fact]
    public void Codes_are_unique_across_the_WHOLE_dictionary_not_per_disposition()
    {
        /*
         * ⚠ THE SINGLE-LIST DECISION, ASSERTED.
         *
         * Two lists (completed / cancelled) would have made this legal, and it must not be: ClosureReasonCode is
         * ONE field, so a code appearing under both dispositions produces two closed tasks quoting the same code
         * with different meanings, and nothing downstream can tell them apart. Split the list in two and this
         * test is the one that fails.
         */
        var refused = TaskTypeRules.NormalizeClosureOutcomes([
            Tenant("DUPLICATE", TaskClosureDisposition.Completed),
            Tenant("duplicate", TaskClosureDisposition.Cancelled)
        ]);

        Assert.Null(refused.Value);
        Assert.Equal(TaskTypeRules.OutcomeCodeDuplicateMessage, refused.Error!.Value.Message);
    }

    [Fact]
    public void Codes_are_normalized_the_way_the_type_code_is()
    {
        // Written to ClosureReasonCode and compared on the way back in, so `partial` and `PARTIAL` must not
        // become two outcomes — the same argument the type code's own normalisation makes.
        var normalized = TaskTypeRules.NormalizeClosureOutcomes([Tenant("  partial ", TaskClosureDisposition.Completed)]);

        Assert.Null(normalized.Error);
        Assert.Equal("PARTIAL", Assert.Single(normalized.Value!).Code);
    }

    [Fact]
    public void The_picker_order_is_stable_rather_than_storage_order()
    {
        // SortOrder then code. A dropdown that reshuffles between two readings of the same task is a dropdown
        // people stop trusting to be the same list.
        var ordered = TaskTypeRules.NormalizeClosureOutcomes([
            Tenant("ZED", TaskClosureDisposition.Completed, sort: 20),
            Tenant("BETA", TaskClosureDisposition.Completed, sort: 10),
            Tenant("ALPHA", TaskClosureDisposition.Completed, sort: 10)
        ]);

        Assert.Equal(["ALPHA", "BETA", "ZED"], ordered.Value!.Select(outcome => outcome.Code));
    }

    // ── The disposition split ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Each_closure_is_offered_only_its_own_half_of_the_dictionary()
    {
        var type = new TaskType
        {
            TenantId = Guid.NewGuid(),
            Code = "DEV",
            Name = "Deviation",
            ClosureOutcomes =
            [
                Tenant("RESOLVED", TaskClosureDisposition.Completed),
                Tenant("SUPERSEDED", TaskClosureDisposition.Cancelled)
            ]
        };

        Assert.Equal("RESOLVED",
            Assert.Single(TaskTypeRules.OutcomesFor(type, TaskClosureDisposition.Completed)).Code);
        Assert.Equal("SUPERSEDED",
            Assert.Single(TaskTypeRules.OutcomesFor(type, TaskClosureDisposition.Cancelled)).Code);
    }

    [Fact]
    public void A_type_with_no_dictionary_asks_nothing_and_that_is_the_compatibility_rule()
    {
        /*
         * ⚠ THE LOAD-BEARING TEST OF THIS SLICE.
         *
         * Every task type written before the dictionary existed has an empty list, and a hundred-odd tasks are
         * already open against them. If this ever returns rows, those tasks meet a required field with nothing
         * choosable in it and cannot be closed at all — the feature would break the product it was added to.
         *
         * Both shapes are checked: a type nobody configured, and no type at all (an unclassified task).
         */
        var unconfigured = new TaskType { TenantId = Guid.NewGuid(), Code = "OLD", Name = "Old" };

        Assert.Empty(TaskTypeRules.OutcomesFor(unconfigured, TaskClosureDisposition.Completed));
        Assert.Empty(TaskTypeRules.OutcomesFor(unconfigured, TaskClosureDisposition.Cancelled));
        Assert.Empty(TaskTypeRules.OutcomesFor(null, TaskClosureDisposition.Completed));
    }

    [Fact]
    public void Absent_is_not_the_same_as_cleared()
    {
        // The update DTO leans on this: null means "not asking" (the editor does not draw the field yet), and a
        // caller that does not know about the dictionary must not delete it on every save.
        Assert.Empty(TaskTypeRules.NormalizeClosureOutcomes(null).Value!);
    }

    // ── The system catalogue ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_system_catalogue_stays_small_and_every_entry_is_resource_labelled()
    {
        /*
         * ⚠ A DELIBERATE CEILING, not an incidental count. Every entry here appears in every tenant's picker in
         * seven languages, forever. Raising it is a decision somebody has to make on purpose, and this is where
         * they are asked to.
         *
         * Business-specific outcomes — Approve/Reject, Resolved/Unresolved, Pass/Fail — belong to a TYPE's own
         * vocabulary and are written by the tenant. That is Oracle's rule (outcomes are defined on the human
         * task) and the reason this list is not a place to keep adding to.
         */
        var catalogue = TaskClosureOutcomeCatalog.All;

        Assert.Equal(5, catalogue.Count);
        Assert.All(catalogue, outcome =>
        {
            // System = translated. A catalogue entry carrying free text would be untranslatable by construction.
            Assert.False(string.IsNullOrWhiteSpace(outcome.LabelResourceKey));
            Assert.Null(outcome.LabelText);
            Assert.StartsWith(TaskClosureOutcomeCatalog.ResourceKeyPrefix, outcome.LabelResourceKey);
        });

        // It must survive its own validator: the catalogue is offered as a menu to pick from, so an entry the
        // rules would refuse could never be saved onto a type.
        Assert.Null(TaskTypeRules.NormalizeClosureOutcomes(catalogue).Error);
    }

    [Fact]
    public void The_reason_flag_belongs_to_the_outcome_and_not_to_a_global_setting()
    {
        /*
         * ⭐ THE DESIGN THIS SLICE REFUSES: one "notes are mandatory" switch above the list.
         *
         * With a switch, the outcomes that do not need a reason collect "ok" — and the field then carries no
         * signal on the outcomes that DID need one. The switch destroys the data it was turned on to gather.
         *
         * So the catalogue must contain BOTH kinds. A catalogue where every entry required a reason, or none
         * did, would be indistinguishable from the global switch and this test would be vacuous.
         */
        var catalogue = TaskClosureOutcomeCatalog.All;

        Assert.Contains(catalogue, outcome => outcome.RequiresReason);
        Assert.Contains(catalogue, outcome => !outcome.RequiresReason);

        // The ordinary close costs one click and no sentence; the one that says "not all of it" has to say which.
        Assert.False(Find(catalogue, TaskClosureOutcomeCatalog.CompletedAsRequested).RequiresReason);
        Assert.True(Find(catalogue, TaskClosureOutcomeCatalog.CompletedPartially).RequiresReason);
        // "It was not needed" is already the whole sentence; "superseded" is not — it has to name by what.
        Assert.False(Find(catalogue, TaskClosureOutcomeCatalog.CancelledNotRequired).RequiresReason);
        Assert.True(Find(catalogue, TaskClosureOutcomeCatalog.CancelledSuperseded).RequiresReason);
    }

    [Fact]
    public void Each_system_outcome_is_offered_for_exactly_the_closure_its_name_claims()
    {
        // A "CANCELLED_…" outcome offered when COMPLETING work would read as an accusation on a finished task.
        var catalogue = TaskClosureOutcomeCatalog.All;

        Assert.All(catalogue, outcome => Assert.Equal(
            outcome.Code.StartsWith("CANCELLED_", StringComparison.Ordinal)
                ? TaskClosureDisposition.Cancelled
                : TaskClosureDisposition.Completed,
            outcome.Disposition));
    }

    [Fact]
    public void The_catalogue_hands_out_copies_so_a_caller_cannot_edit_it()
    {
        // These are mutable entities. A shared instance would let one request's normalisation rewrite the
        // catalogue for every later one in the process.
        var first = TaskClosureOutcomeCatalog.All[0];
        first.RequiresReason = !first.RequiresReason;

        Assert.NotEqual(first.RequiresReason, TaskClosureOutcomeCatalog.All[0].RequiresReason);
    }

    private static TaskClosureOutcome Find(IReadOnlyList<TaskClosureOutcome> catalogue, string code) =>
        catalogue.Single(outcome => outcome.Code == code);
}
