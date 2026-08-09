using System.ComponentModel.DataAnnotations;
using System.Net;
using Diten.Web.Models.TaskRecurrenceRules;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// BL-052 — the recurring-task rule screen.
///
/// <para><b>The measured gap.</b> The Phase 4 engine has been complete for a while: the entity, the hourly sweep
/// that generates exactly once per period, five CRUD endpoints and the Diten.Web proxy. What did not exist was a
/// screen — <c>rg -rl "ecurrence" Views wwwroot/assets/js</c> returned nothing, so a rule could only be created
/// by calling the API by hand.</para>
///
/// <para><b>Why these tests and not others.</b> Layout is the owner's call on the UX tour; what is pinned here is
/// what the screen must never get WRONG: the assignment target it is allowed to send, an end date that is
/// genuinely optional, and the page answering at all. The last one matters because a route the web tier does not
/// serve is answered 404 before authentication runs — which is exactly how a feature ships unreachable.</para>
/// </summary>
public sealed class TaskRecurrenceRuleScreenTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public TaskRecurrenceRuleScreenTests(WebApplicationFactory<Program> factory) => _factory = factory;

    // ── The rule the engine's own comment says must never be sent ────────────────────────────────────────

    [Fact]
    public void A_rule_assigned_to_MYSELF_is_refused()
    {
        /*
         * TaskSupportingEntities.cs states the reason: a background sweep has no "self". A rule that said
         * SelfAssigned generated tasks assigned to nobody — in nobody's list, invisible on every surface, while
         * still consuming the period, so the work could never be produced again. Invisible work that cannot be
         * regenerated is the worst outcome this module has.
         *
         * The server refuses it. The form must not merely hide the option: a hidden <option> is one devtools
         * edit away, and the value the browser sends is the only thing that is actually true.
         */
        var results = Validate(Rule(r => r.AssignmentTarget = "SelfAssigned"));

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(TaskRecurrenceRuleEditViewModel.AssignmentTarget)));
    }

    [Fact]
    public void A_rule_assigned_to_a_PERSON_needs_that_person()
    {
        // "Person" with no person is the same defect wearing a legal target: nobody receives the work.
        var results = Validate(Rule(r =>
        {
            r.AssignmentTarget = "Person";
            r.AssigneeUserId = null;
        }));

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(TaskRecurrenceRuleEditViewModel.AssigneeUserId)));
    }

    [Fact]
    public void A_rule_assigned_to_a_POOL_needs_that_pool()
    {
        var results = Validate(Rule(r =>
        {
            r.AssignmentTarget = "PositionPool";
            r.PoolPositionId = null;
            r.AssigneeUserId = null;
        }));

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(TaskRecurrenceRuleEditViewModel.PoolPositionId)));
    }

    [Fact]
    public void A_rule_assigned_to_a_real_person_is_accepted()
    {
        /*
         * NON-VACUITY for the three above. A validator that refused everything would satisfy them all and leave
         * the screen unable to save anything at all.
         */
        Assert.Empty(Validate(Rule()));
    }

    // ── Open-ended is an ANSWER, not a missing value ─────────────────────────────────────────────────────

    [Fact]
    public void A_rule_with_no_end_date_is_accepted()
    {
        // "Monthly close" ends when the company does. Demanding an end date would make the commonest real rule
        // the one the form cannot express.
        Assert.Empty(Validate(Rule(r => r.EndsAt = null)));
    }

    [Fact]
    public void A_rule_that_ends_before_it_starts_is_refused()
    {
        // The window the sweep would read as "no period ever". Silently accepting it produces a rule that looks
        // saved and generates nothing, with nothing on screen explaining why.
        var results = Validate(Rule(r =>
        {
            r.StartsAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
            r.EndsAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        }));

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(TaskRecurrenceRuleEditViewModel.EndsAt)));
    }

    [Fact]
    public void A_frequency_the_engine_does_not_have_is_refused()
    {
        // The enum is Daily/Weekly/Monthly/Quarterly/Yearly. Anything else deserializes to nothing useful on the
        // far side and the rule silently never fires.
        var results = Validate(Rule(r => r.Frequency = "Fortnightly"));

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(TaskRecurrenceRuleEditViewModel.Frequency)));
    }

    // ── The screen answers at all ────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("/Tasks/RecurrenceRules")]
    [InlineData("/Tasks/RecurrenceRules/Create")]
    public async Task The_screen_is_reachable(string url)
    {
        /*
         * Over REAL HTTP through the web pipeline. These requests carry no session, so the answer is the
         * cookie-auth challenge — 302 to login. That IS the signal: a URL the web tier does not serve is
         * answered 404 by routing BEFORE authentication runs (pinned below), so 302 means the page resolved.
         *
         * This is the delivery half of the item. A screen whose files exist but whose route does not is the
         * same class of defect as a payload nobody reads.
         */
        using var client = CreateClient();

        var response = await client.GetAsync(url);

        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
    }

    [Fact]
    public async Task A_page_the_web_tier_does_not_serve_answers_not_found()
    {
        // Non-vacuity: without this, the two above would pass for any URL at all.
        using var client = CreateClient();

        var response = await client.GetAsync("/Tasks/RecurrenceRules/NotARealPage");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── harness ──────────────────────────────────────────────────────────────────────────────────────────

    private HttpClient CreateClient() => _factory
        .WithWebHostBuilder(builder => builder.UseSetting("GatewayUrl", "http://localhost:59999"))
        .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    /// <summary>A rule that is valid in every respect, so each test changes exactly one thing.</summary>
    private static TaskRecurrenceRuleEditViewModel Rule(Action<TaskRecurrenceRuleEditViewModel>? mutate = null)
    {
        var model = new TaskRecurrenceRuleEditViewModel
        {
            Name = "Ay sonu kapanış",
            Frequency = "Monthly",
            Interval = 1,
            StartsAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            EndsAt = null,
            AssignmentTarget = "Person",
            AssigneeUserId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            IsActive = true
        };
        mutate?.Invoke(model);
        return model;
    }

    /// <summary>Runs the model's OWN validation, the same call MVC makes when the form posts.</summary>
    private static List<ValidationResult> Validate(TaskRecurrenceRuleEditViewModel model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }
}
