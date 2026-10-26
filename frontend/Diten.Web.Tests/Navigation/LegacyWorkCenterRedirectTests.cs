using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Diten.Web.Tests.Navigation;

/// <summary>
/// BL-619 (WP-WEB-BL619-01) — the old mock Task Center <c>/WorkCenter</c> is removed; <c>/WorkCenterNext</c> is the
/// only Task Center. Every old URL (bookmarks, mails, browser history) must land on the real surface with a
/// PERMANENT redirect, measured over real HTTP through the web tier. The query string is dropped on purpose: the old
/// surface honoured a user override (<c>?as=</c>), and no part of an old URL is carried into the new one.
/// </summary>
public sealed class LegacyWorkCenterRedirectTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Target = "/WorkCenterNext";

    private readonly WebApplicationFactory<Program> _factory;

    public LegacyWorkCenterRedirectTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Theory]
    [InlineData("/WorkCenter")]
    [InlineData("/WorkCenter/")]
    [InlineData("/WorkCenter/Task/x")]
    [InlineData("/workcenter?tab=y")]
    [InlineData("/WORKCENTER/Index?as=someone")]
    public async Task An_old_task_center_url_is_permanently_redirected_to_the_new_one(string url)
    {
        using var client = CreateClient();

        var response = await client.GetAsync(url);

        Assert.True(
            response.StatusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.PermanentRedirect,
            $"GET {url} answered {(int)response.StatusCode}, not a permanent redirect — the removed mock page's URL "
            + "would fall through to routing (404 or the vanity-slug fallback) instead of the real Task Center.");
        Assert.Equal(Target, response.Headers.Location?.OriginalString);
    }

    [Theory]
    [InlineData("/WorkCenterNext")]
    [InlineData("/WorkCenterNext/Details/41400000-0000-0000-0000-0000000000d1")]
    [InlineData("/workcenternext?tab=inbox")]
    public async Task The_new_task_center_is_not_redirected_by_the_legacy_rule(string url)
    {
        using var client = CreateClient();

        var response = await client.GetAsync(url);

        Assert.False(
            response.StatusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.PermanentRedirect,
            $"GET {url} answered a permanent redirect — the legacy /WorkCenter rule swallowed /WorkCenterNext (a loop).");
        Assert.NotEqual(Target, response.Headers.Location?.OriginalString);
    }

    private HttpClient CreateClient() => _factory
        .WithWebHostBuilder(builder => builder.UseEnvironment("Development"))
        .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
}

/// <summary>
/// BL-619 guard — nothing in the web tier may point back at the removed mock Task Center: no view, script or controller
/// may load <c>mock-work-items</c> or link to <c>/WorkCenter</c> (the redirect itself lives in Program.cs, outside the
/// scanned folders). <c>/WorkCenterNext…</c> is the real surface and is not matched.
/// </summary>
public sealed class NoLegacyWorkCenterLinkGuardTests
{
    private static readonly Regex LegacyLink = new(
        @"[""'(`=]\s*/WorkCenter(?=[/""'?#)`]|$)",
        RegexOptions.IgnoreCase | RegexOptions.Multiline);

    private static readonly string[] Roots = { "Views", Path.Combine("wwwroot", "assets", "js"), "Controllers" };

    private static readonly string[] Extensions = { ".cshtml", ".js", ".cs" };

    [Fact]
    public void No_view_script_or_controller_references_the_removed_mock_task_center()
    {
        var web = WebRoot();
        var failures = new List<string>();
        var scanned = 0;

        foreach (var root in Roots)
        {
            foreach (var file in Directory.EnumerateFiles(Path.Combine(web, root), "*", SearchOption.AllDirectories))
            {
                if (!Extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase)
                    || file.EndsWith(".min.js", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                scanned++;
                var text = File.ReadAllText(file);
                var relative = Path.GetRelativePath(web, file);

                if (text.Contains("mock-work-items", StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add(relative + ": loads mock-work-items");
                }

                foreach (Match match in LegacyLink.Matches(text))
                {
                    var line = text.Take(match.Index).Count(c => c == '\n') + 1;
                    failures.Add($"{relative}:{line}: links to the removed /WorkCenter");
                }
            }
        }

        Assert.True(scanned > 100, $"only {scanned} files scanned — the guard is looking in the wrong place");
        Assert.True(failures.Count == 0,
            "The mock Task Center /WorkCenter was removed (BL-619); link /WorkCenterNext instead:\n"
            + string.Join("\n", failures));
    }

    [Theory]
    [InlineData("<a href=\"/WorkCenter\">x</a>", true)]
    [InlineData("<a href='/workcenter/Task/1'>x</a>", true)]
    [InlineData("location.href = \"/WorkCenter?tab=inbox\";", true)]
    [InlineData("<a href=\"/WorkCenterNext\">x</a>", false)]
    [InlineData("fetch('/WorkCenterNext/api/work-items')", false)]
    [InlineData("// the legacy /WorkCenter page was removed", false)]
    public void The_link_pattern_matches_old_links_only(string sample, bool expected)
    {
        Assert.Equal(expected, LegacyLink.IsMatch(sample));
    }

    private static string WebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Diten.Web.csproj"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("repo root not found"), "frontend", "Diten.Web");
    }
}
