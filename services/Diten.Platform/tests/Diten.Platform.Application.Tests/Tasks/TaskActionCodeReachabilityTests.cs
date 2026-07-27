using System.Text.RegularExpressions;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// Every action code MOD-0024 can put on a button must be reachable through the browser.
///
/// <para><b>The defect.</b> A projected action code is not a label: the Task Center turns it straight into a URL
/// (<c>POST /Tasks/api/{id}/{code}</c>), which Diten.Web proxies to Platform. `inquire` was added to the provider
/// and to Platform's controller but not to the proxy's route constraint, so the button rendered and answered 404
/// before the request left the web tier. Platform's own suite was fully green throughout.</para>
///
/// <para><b>Why this test lives here and reads a file.</b> Diten.Web and Platform share no assembly, so there is no
/// constant both sides can compile against. This walks the repository and reads the proxy's canonical list
/// (<c>TaskTransitionRoutes.cs</c>) as text — the same technique DateTimeOffsetSortGuardTests uses. It fails on the
/// Platform side, where a new action is written, which is where the author is standing.</para>
/// </summary>
public sealed class TaskActionCodeReachabilityTests
{
    /// <summary>
    /// Codes the provider emits that are NOT transitions and so are never posted to the transition route.
    /// Empty today; an entry here is a claim that the client reaches the code some other way.
    /// </summary>
    private static readonly HashSet<string> NotTransitionRoutes = new(StringComparer.Ordinal);

    [Fact]
    public void Every_action_code_the_provider_projects_is_forwarded_by_the_web_proxy()
    {
        var projected = ProjectedActionCodes();
        var forwarded = ProxyForwardedCodes();

        // Non-vacuity: if either side stopped parsing, the comparison below would pass by being empty.
        Assert.NotEmpty(projected);
        Assert.NotEmpty(forwarded);
        Assert.Contains("inquire", projected);
        Assert.Contains("start", forwarded);

        var unreachable = projected
            .Where(code => !NotTransitionRoutes.Contains(code))
            .Where(code => !forwarded.Contains(code))
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            unreachable.Count == 0,
            "TaskWorkItemProvider projects action codes the Diten.Web proxy will not forward, so the button "
            + "renders and the request 404s in the web tier before it reaches Platform: "
            + string.Join(", ", unreachable)
            + ". Add each to TaskTransitionRoutes.All AND to the [HttpPost] constraint on "
            + "Diten.Web TasksController.ApiTransition (a route constraint must be a compile-time constant, so "
            + "the two are kept in step by tests).");
    }

    /// <summary>Every action code the provider can emit, read from its source rather than from one projection.</summary>
    private static IReadOnlyCollection<string> ProjectedActionCodes()
    {
        var source = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "services/Diten.Platform/src/Diten.Platform.Application/Features/Tasks/Providers/TaskWorkItemProvider.cs"));

        // Build("code", …) and Disabled("code", …) are the only two ways an action reaches the projection.
        return Regex.Matches(source, @"\b(?:Build|Disabled)\(\s*""(?<code>[a-zA-Z]+)""")
            .Select(match => match.Groups["code"].Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>The proxy's canonical list, read from Diten.Web — the two services share no assembly.</summary>
    private static IReadOnlyCollection<string> ProxyForwardedCodes()
    {
        var source = File.ReadAllText(Path.Combine(
            RepositoryRoot(), "frontend/Diten.Web/Controllers/TaskTransitionRoutes.cs"));

        var pattern = Regex.Match(source, @"Pattern\s*=\s*""\^\((?<codes>[^)]+)\)\$""");
        Assert.True(pattern.Success, "TaskTransitionRoutes.Pattern is no longer an anchored alternation literal.");

        return pattern.Groups["codes"].Value
            .Split('|', StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git"))
                && Directory.Exists(Path.Combine(directory.FullName, "frontend")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate the repository root walking up from {AppContext.BaseDirectory}.");
    }
}
