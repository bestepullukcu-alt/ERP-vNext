using System.Reflection;
using Diten.Web.Controllers;
using Diten.Web.Models.TaskTypes;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// The closure outcome dictionary, as this form posts it.
///
/// <para><b>The defect these exist for is a DELETION, and it has already happened once on this screen.</b> The
/// API reads <c>closureOutcomes: null</c> as "leave the stored dictionary alone" and <c>[]</c> as "clear it" —
/// an asymmetry introduced precisely because this form did not draw the field. The moment it does, the
/// protection has to move into the form, and a mistake here silently wipes configuration with a 302 and a
/// success message. That is exactly how <c>GroupDocumentsText</c> lost a type its governing documents, found
/// live on 2026-08-26.</para>
///
/// <para>They assert against the REAL payload builder by reflection, not a copy of it: a test that restated the
/// mapping would agree with itself while the controller sent something else.</para>
/// </summary>
public sealed class TaskTypeClosureOutcomeEditorTests
{
    private static IDictionary<string, object?> Payload(TaskTypeEditViewModel model, string builder)
    {
        var method = typeof(TaskTypesController).GetMethod(
            builder, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var payload = method!.Invoke(null, [model]);
        Assert.NotNull(payload);

        return payload!.GetType()
            .GetProperties()
            .ToDictionary(p => p.Name, p => p.GetValue(payload), StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<IDictionary<string, object?>> Outcomes(IDictionary<string, object?> payload)
    {
        var raw = payload["closureOutcomes"];
        Assert.NotNull(raw);

        return ((System.Collections.IEnumerable)raw!)
            .Cast<object>()
            .Select(row => (IDictionary<string, object?>)row.GetType()
                .GetProperties()
                .ToDictionary(p => p.Name, p => p.GetValue(row), StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    private static TaskTypeEditViewModel Model(params TaskTypeClosureOutcomeViewModel[] outcomes) => new()
    {
        Code = "DEV",
        Name = "Deviation",
        ClosureOutcomes = [.. outcomes]
    };

    // ── (a) THE DELETION GUARD ───────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("ToCreatePayload")]
    [InlineData("ToUpdatePayload")]
    public void A_save_that_never_rendered_the_editor_sends_NULL_and_touches_nothing(string builder)
    {
        /*
         * ⚠ THE ONE THAT MUST NEVER GO RED.
         *
         * `ClosureOutcomesSubmitted` false means the browser did not render the dictionary section — an older
         * cached page, a partial post, a client that predates this slice. The list being empty proves nothing in
         * that case, and sending `[]` would tell the server to CLEAR a dictionary the user never saw.
         *
         * Null is the only safe answer, and it is the answer the API's own nullable field was designed for.
         */
        var payload = Payload(Model(), builder);

        Assert.True(payload.ContainsKey("closureOutcomes"), "the payload stopped carrying the dictionary at all");
        Assert.Null(payload["closureOutcomes"]);
    }

    [Fact]
    public void An_editor_that_DID_render_and_holds_no_rows_sends_an_empty_list_which_clears()
    {
        /*
         * The other half, and it is the half that makes the feature usable: an administrator who removes every
         * row means it. Collapsing this to null "to be safe" would make the last outcome undeletable from the
         * screen — safety that quietly removes a capability is not safety.
         */
        var model = Model();
        model.ClosureOutcomesSubmitted = true;

        Assert.Empty(Outcomes(Payload(model, "ToUpdatePayload")));
    }

    [Fact]
    public void A_type_saved_without_touching_its_dictionary_keeps_every_row()
    {
        /*
         * The scenario the brief names: open a type that HAS a dictionary, change its name, press Save. The
         * rows the form rendered come back in the post, so they round-trip unchanged — nothing is inferred and
         * nothing is dropped.
         */
        var model = Model(
            new TaskTypeClosureOutcomeViewModel
            {
                Code = "RESOLVED", LabelText = "Çözüldü", Disposition = "Completed", SortOrder = 10
            },
            new TaskTypeClosureOutcomeViewModel
            {
                Code = "SUPERSEDED", LabelText = "Yerini başka iş aldı", Disposition = "Cancelled", SortOrder = 20
            });
        model.ClosureOutcomesSubmitted = true;

        var rows = Outcomes(Payload(model, "ToUpdatePayload"));

        Assert.Equal(2, rows.Count);
        Assert.Equal("RESOLVED", rows[0]["code"]);
        Assert.Equal("Çözüldü", rows[0]["labelText"]);
        Assert.Equal("Completed", rows[0]["disposition"]);
        Assert.Equal("SUPERSEDED", rows[1]["code"]);
        Assert.Equal("Cancelled", rows[1]["disposition"]);
    }

    // ── (b) A SYSTEM OUTCOME KEEPS THE CATALOGUE'S LABEL ─────────────────────────────────────────────────

    [Fact]
    public void A_system_outcome_posts_its_resource_key_and_no_text_of_its_own()
    {
        /*
         * The server refuses an outcome carrying BOTH label halves (OutcomeLabelAmbiguousMessage), so a row that
         * somehow arrived with a key AND text has to be resolved before it is sent — and the KEY wins, because
         * it is bound to the seven translations this product ships while the text is one language.
         *
         * This is the payload half of "the label is not editable for a system outcome". The screen half is
         * asserted in the .cshtml/JS guard; both are needed, because a readonly input is presentation and the
         * payload is what actually reaches the server.
         */
        var model = Model(new TaskTypeClosureOutcomeViewModel
        {
            Code = "COMPLETED_PARTIALLY",
            LabelResourceKey = "WorkAggregation_ClosureOutcome_CompletedPartially",
            // A stale value left behind by switching a row from custom to system — must not travel.
            LabelText = "whatever the user had typed",
            Disposition = "Completed",
            RequiresReason = true
        });
        model.ClosureOutcomesSubmitted = true;

        var row = Assert.Single(Outcomes(Payload(model, "ToUpdatePayload")));

        Assert.Equal("WorkAggregation_ClosureOutcome_CompletedPartially", row["labelResourceKey"]);
        Assert.Null(row["labelText"]);
    }

    [Fact]
    public void A_tenant_outcome_posts_its_own_words_and_no_key()
    {
        var model = Model(new TaskTypeClosureOutcomeViewModel
        {
            Code = "ESCALATED", LabelText = "Üst birime devredildi", Disposition = "Completed"
        });
        model.ClosureOutcomesSubmitted = true;

        var row = Assert.Single(Outcomes(Payload(model, "ToUpdatePayload")));

        Assert.Null(row["labelResourceKey"]);
        Assert.Equal("Üst birime devredildi", row["labelText"]);
    }

    // ── (c) THE REASON FLAG IS PER ROW ───────────────────────────────────────────────────────────────────

    [Fact]
    public void RequiresReason_is_carried_row_by_row_and_not_as_one_setting()
    {
        /*
         * ⭐ THE STAR RULE, at this layer. Two rows in ONE dictionary disagree: "Rejected" asks why, "Approved"
         * does not. No global flag can produce that pair, so this fails the moment the value is read from
         * anywhere but the row.
         */
        var model = Model(
            new TaskTypeClosureOutcomeViewModel { Code = "APPROVED", LabelText = "Onaylandı", RequiresReason = false },
            new TaskTypeClosureOutcomeViewModel { Code = "REJECTED", LabelText = "Reddedildi", RequiresReason = true });
        model.ClosureOutcomesSubmitted = true;

        var rows = Outcomes(Payload(model, "ToUpdatePayload"));

        Assert.Equal(false, rows[0]["requiresReason"]);
        Assert.Equal(true, rows[1]["requiresReason"]);
    }

    // ── Housekeeping the repeater makes necessary ────────────────────────────────────────────────────────

    [Fact]
    public void A_row_the_user_added_and_never_filled_is_dropped_rather_than_refused()
    {
        // Pressing "Add outcome" and then Save is a half-typed thought, not a validation failure. The server
        // still refuses anything it cannot accept; this only stops an empty row from becoming one.
        var model = Model(
            new TaskTypeClosureOutcomeViewModel { Code = "RESOLVED", LabelText = "Çözüldü" },
            new TaskTypeClosureOutcomeViewModel { Code = "   " });
        model.ClosureOutcomesSubmitted = true;

        Assert.Equal("RESOLVED", Assert.Single(Outcomes(Payload(model, "ToUpdatePayload")))["code"]);
    }

    [Fact]
    public void An_unordered_list_keeps_the_order_it_was_arranged_in()
    {
        /*
         * With every row at SortOrder 0 the server's tie-breaker (code, alphabetical) becomes the real order, so
         * a list an administrator arranged deliberately would come back rearranged. Falling back to the row's
         * POSITION keeps what they built.
         */
        var model = Model(
            new TaskTypeClosureOutcomeViewModel { Code = "ZED", LabelText = "Z" },
            new TaskTypeClosureOutcomeViewModel { Code = "ALPHA", LabelText = "A" });
        model.ClosureOutcomesSubmitted = true;

        var rows = Outcomes(Payload(model, "ToUpdatePayload"));

        Assert.Equal("ZED", rows[0]["code"]);
        Assert.Equal(10, rows[0]["sortOrder"]);
        Assert.Equal("ALPHA", rows[1]["code"]);
        Assert.Equal(20, rows[1]["sortOrder"]);
    }

    [Fact]
    public void An_explicit_order_is_never_overwritten()
    {
        var model = Model(new TaskTypeClosureOutcomeViewModel { Code = "RESOLVED", LabelText = "Ç", SortOrder = 99 });
        model.ClosureOutcomesSubmitted = true;

        Assert.Equal(99, Assert.Single(Outcomes(Payload(model, "ToUpdatePayload")))["sortOrder"]);
    }
    /// <summary>
    /// The checkbox is written BEFORE its hidden partner, and the order is the whole rule.
    ///
    /// <para>MVC binds the FIRST value it meets for a repeated name. Written the other way round a ticked box puts
    /// <c>false,true</c> on the wire and the binder takes <c>false</c>: "requires a reason" can never be turned on,
    /// nothing throws, and the screen looks right.</para>
    ///
    /// <para><b>MEASURED LIVE, and only live.</b> This row shipped with the pair reversed and every test in this
    /// file passed — they build the model in C# and never post the form. `FormData(form)` on the running page
    /// returned <c>["false","true"]</c> with the box ticked. The suite was green on both sides of the fix, which
    /// is exactly why this assertion is about the MARKUP's order rather than the bound model.</para>
    ///
    /// <para>It cannot be an <c>asp-for</c> checkbox: that queues its hidden partner to
    /// <c>FormContext.EndOfFormContent</c>, emitted at <c>&lt;/form&gt;</c> — and the <c>&lt;template&gt;</c> this
    /// partial also renders is not inside a form at all. So the pair is hand-written, and this is its guard.</para>
    ///
    /// <para>Third time this defect class has appeared in one session: the user create form, the QMS Baselines
    /// designer (BL-332, still open in another module), and here.</para>
    /// </summary>
    [Fact]
    public void The_requires_reason_checkbox_precedes_its_hidden_partner()
    {
        var markup = File.ReadAllText(RowPartialPath());

        var checkbox = markup.IndexOf("type=\"checkbox\"", StringComparison.Ordinal);
        var hidden = markup.IndexOf("type=\"hidden\" name=\"ClosureOutcomes[@key].RequiresReason\"", StringComparison.Ordinal);

        Assert.True(checkbox >= 0, "the RequiresReason checkbox is gone from the row partial");
        Assert.True(hidden >= 0, "the RequiresReason hidden partner is gone — an unticked box would post nothing");
        Assert.True(
            checkbox < hidden,
            "the hidden partner precedes the checkbox: MVC takes the first value, so a ticked box binds FALSE");
    }

    private static string RowPartialPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return Path.Combine(
            dir!.FullName, "frontend", "Diten.Web", "Views", "Tasks", "TaskTypes", "_ClosureOutcomeRow.cshtml");
    }

}
