using System.Reflection;
using System.Text.RegularExpressions;
using Diten.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Constraints;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// The proxy must forward every transition code the Task Center can put on a button.
///
/// <para><b>The defect these exist for.</b> `inquire` was added to the Platform endpoint and to the projection,
/// but not to this proxy's route constraint. The action rendered, the user pressed it, and Diten.Web answered 404
/// before the request ever reached Platform — so a green Platform suite proved nothing. It was the third defect of
/// this shape in one session, which is why the check is now mechanical rather than remembered.</para>
///
/// <para>These assert against the REAL attribute on the controller and the REAL framework constraint type
/// (<see cref="RegexRouteConstraint"/>), not a copy of the pattern: a test that re-declared the regex would agree
/// with itself while the endpoint kept rejecting the request.</para>
/// </summary>
public sealed class TaskTransitionRouteTests
{
    private static readonly string RouteTemplate = TransitionRouteTemplate();

    // ── Test B: the proxy accepts every canonical code ───────────────────────────────────────────────────────

    [Fact]
    public void The_route_constraint_accepts_every_canonical_transition_code()
    {
        var constraint = new RegexRouteConstraint(ConstraintPatternFromRoute());

        foreach (var code in TaskTransitionRoutes.All)
        {
            Assert.True(
                Matches(constraint, code),
                $"The proxy route rejects the transition code \"{code}\". The Task Center projects it as an action "
                + "and the client turns an action code straight into this URL, so the user would get a 404 from a "
                + "button that is visibly enabled. Add it to the [HttpPost] constraint on "
                + "TasksController.ApiTransition and to TaskTransitionRoutes.");
        }
    }

    // ── Test C: anything else is refused, and refused whole ──────────────────────────────────────────────────

    [Theory]
    [InlineData("return")]        // designed but not built yet — must NOT be forwarded until it exists
    [InlineData("reassign")]      // same
    [InlineData("delete")]
    [InlineData("")]
    [InlineData("cancel-everything")]   // an unanchored pattern would forward this
    [InlineData("Cancel; DROP")]
    public void The_route_constraint_refuses_anything_not_on_the_canonical_list(string code)
    {
        var constraint = new RegexRouteConstraint(ConstraintPatternFromRoute());

        Assert.False(
            TaskTransitionRoutes.All.Contains(code) || Matches(constraint, code),
            $"The proxy route forwards \"{code}\", which is not a transition the engine implements. Forwarding an "
            + "unknown code turns a client typo into a request against Platform instead of a local refusal.");
    }

    // ── The literal and the list cannot drift ────────────────────────────────────────────────────────────────

    /*
     * A route constraint must be a compile-time constant, so the attribute cannot be built from the list. This is
     * the substitute for that missing compiler check: if someone edits one and not the other, this fails and says
     * which.
     */
    [Fact]
    public void The_attribute_carries_exactly_the_canonical_pattern()
    {
        Assert.Contains(
            TaskTransitionRoutes.Pattern,
            RouteTemplate);
    }

    [Fact]
    public void The_canonical_pattern_lists_exactly_the_canonical_codes()
    {
        // Read the alternation back out of the pattern, so the list and the regex are compared by CONTENT rather
        // than by both being edited in the same commit.
        var inner = Regex.Match(TaskTransitionRoutes.Pattern, @"^\^\((?<codes>[^)]+)\)\$$");
        Assert.True(inner.Success, $"Pattern is not the expected anchored alternation: {TaskTransitionRoutes.Pattern}");

        var fromPattern = inner.Groups["codes"].Value.Split('|', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(TaskTransitionRoutes.All, fromPattern);
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>The template ASP.NET actually builds the endpoint from — read off the method, never retyped.</summary>
    private static string TransitionRouteTemplate()
    {
        var method = typeof(TasksController).GetMethod(
            nameof(TasksController.ApiTransition),
            BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException("TasksController.ApiTransition no longer exists.");

        var attribute = method.GetCustomAttribute<HttpPostAttribute>()
            ?? throw new InvalidOperationException("ApiTransition has no [HttpPost] route.");

        return attribute.Template
            ?? throw new InvalidOperationException("ApiTransition's [HttpPost] carries no template.");
    }

    /// <summary>Extracts the regex handed to the framework's constraint, from the real route template.</summary>
    private static string ConstraintPatternFromRoute()
    {
        var match = Regex.Match(RouteTemplate, @"\{transition:regex\((?<pattern>.+)\)\}$");
        Assert.True(
            match.Success,
            $"The transition segment is no longer a regex constraint, so this guard cannot check it: {RouteTemplate}");

        return match.Groups["pattern"].Value;
    }

    private static bool Matches(IRouteConstraint constraint, string value)
    {
        var values = new RouteValueDictionary { ["transition"] = value };
        return constraint.Match(httpContext: null, route: null, routeKey: "transition", values, RouteDirection.IncomingRequest);
    }
}
