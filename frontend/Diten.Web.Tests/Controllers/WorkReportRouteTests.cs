using System.Reflection;
using Diten.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// The work report's two routes in this tier: the PAGE and the PROXY.
///
/// <para><b>The defect these exist for has happened three times in this controller.</b> A route Platform exposes
/// and this proxy does not answers 404 inside the web tier — which is how <c>inquire</c> shipped unreachable,
/// with a visibly enabled button behind it. A green Platform suite proves nothing about it, because the request
/// never leaves Diten.Web.</para>
///
/// <para>They assert against the REAL attributes by reflection, not a copy of the route strings: a test that
/// restated the template would agree with itself while the endpoint kept answering 404.</para>
/// </summary>
public sealed class WorkReportRouteTests
{
    private static MethodInfo Action(string name)
    {
        var method = typeof(TasksController).GetMethod(name, BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(method);
        return method!;
    }

    private static string Template(string actionName) =>
        Action(actionName).GetCustomAttribute<HttpGetAttribute>()?.Template
        ?? throw new InvalidOperationException($"{actionName} has no [HttpGet] template.");

    [Fact]
    public void The_screen_has_a_route_so_the_manifest_entry_is_not_a_promise_to_a_404()
    {
        /*
         * ⚠ THE MANIFEST NOW PUBLISHES THIS PAGE VISIBLE. It was `IsNavigationVisible: false` for exactly one
         * slice, because 5a shipped the query with no screen behind the route — and a visible manifest page with
         * no route grows a sidebar entry pointing at a 404 on the next reconciliation. This is the assertion
         * that keeps the two in step.
         */
        Assert.Equal("WorkReport", Template(nameof(TasksController.WorkReport)));
    }

    [Fact]
    public void The_screen_route_is_declared_BEFORE_the_guid_route_that_would_otherwise_swallow_it()
    {
        /*
         * "WorkReport" is not a Guid, so `{id:guid}` cannot match it — but ordering is what the sibling routes
         * (`field-definitions/option-sources`) are also careful about, and a route that only fails at match time
         * is a route nobody notices until the page 404s. Declaration order is the cheap, checkable guard.
         */
        var methods = typeof(TasksController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetCustomAttribute<HttpGetAttribute>() is not null)
            .OrderBy(m => m.MetadataToken)
            .Select(m => m.Name)
            .ToList();

        var screen = methods.IndexOf(nameof(TasksController.WorkReport));
        var details = methods.IndexOf(nameof(TasksController.Details));

        Assert.True(screen >= 0 && details >= 0, "one of the two routes disappeared");
        Assert.True(screen < details,
            "the WorkReport route moved below the {id:guid} route — declare it first, as the field-definition "
            + "routes are, so the two can never be confused at match time.");
    }

    [Fact]
    public void The_proxy_route_exists_so_the_screen_is_not_calling_a_404()
    {
        // The screen fetches `/Tasks/api/work-report`. MEASURED before this slice: the controller had ZERO
        // matches for "work-report", so that call would have 404'd inside the web tier.
        Assert.Equal("api/work-report", Template(nameof(TasksController.ApiWorkReport)));
    }

    [Fact]
    public void The_proxy_forwards_the_QUERY_STRING_whole_rather_than_re_listing_the_parameters()
    {
        /*
         * `from`, `to` and `groupBy` are PLATFORM's contract, not this tier's. Re-listing them here is how a
         * parameter gets dropped silently — the field-definition records proxy states the same rule, and this
         * asserts the screen's period actually survives the hop.
         */
        var source = File.ReadAllText(ControllerSourcePath());
        var start = source.IndexOf("ApiWorkReport", StringComparison.Ordinal);
        Assert.True(start > 0, "ApiWorkReport is gone from the controller");

        var body = source[start..(start + 400)];
        Assert.Contains("Request.QueryString.Value", body, StringComparison.Ordinal);
        Assert.Contains("/api/v1/tasks/work-report", body, StringComparison.Ordinal);
    }

    [Fact]
    public void The_proxy_decides_nothing_about_scope()
    {
        /*
         * ⚠ WHOSE ROWS ARE COUNTED IS PLATFORM'S ANSWER. The data-scope resolver runs there, and widening to the
         * whole tenant is a permission checked there. A proxy that "helped" — by adding a flag, or by filtering
         * the response — would be a second authority over the same question, and the two would disagree the
         * first time either changed.
         */
        var source = File.ReadAllText(ControllerSourcePath());
        var start = source.IndexOf("public Task<IActionResult> ApiWorkReport", StringComparison.Ordinal);
        Assert.True(start > 0);

        var body = source[start..(start + 300)];
        Assert.DoesNotContain("tenant-wide", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("scopeApplied", body, StringComparison.OrdinalIgnoreCase);
    }

    private static string ControllerSourcePath()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8 && dir is not null; i++)
        {
            var candidate = Path.Combine(dir, "Controllers", "TasksController.cs");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            var web = Path.Combine(dir, "frontend", "Diten.Web", "Controllers", "TasksController.cs");
            if (File.Exists(web))
            {
                return web;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new FileNotFoundException("TasksController.cs could not be located from the test output directory.");
    }
}
