using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web.Controllers.CRM;
using Diten.Web.Views.CRM.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-KP-4 (DESIGN-KP-STUDIO §7, bridge-decision §7 / §8) — the Content Sets console is retired on the Web layer (the
/// Content Scopes console already went with WP-SB-1R): no controller / view / script / resx / proxy is left, every old
/// <c>/CRM/ContentSets*</c> bookmark is a PERMANENT redirect to the Knowledge Path Studio list, the claims quick view
/// lists where a claim is used with the new knowledge-path type ("Knowledge path", 7 languages, linked to the studio
/// workspace) and never a content set, and the new knowledge-content form no longer offers the SB-2 release vocabulary.
/// </summary>
public sealed class ContentSetRetirementWebTests
{
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    [Fact]
    public void Every_old_content_sets_url_is_a_permanent_redirect_to_the_knowledge_path_studio()
    {
        var type = typeof(ContentSetsRedirectController);
        Assert.Equal("CRM/ContentSets", type.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.NotNull(type.GetCustomAttribute<AuthorizeAttribute>());
        var action = type.GetMethod(nameof(ContentSetsRedirectController.RedirectToKnowledgePaths))!;
        Assert.Equal(["", "{**rest}"], action.GetCustomAttributes<HttpGetAttribute>().Select(a => a.Template!).OrderBy(t => t).ToArray());

        var redirect = Assert.IsType<RedirectResult>(new ContentSetsRedirectController().RedirectToKnowledgePaths());
        Assert.True(redirect.Permanent);
        Assert.Equal("/CRM/KnowledgePaths", redirect.Url);

        // The redirect is the ONLY thing left under the old route: no other action, no proxy.
        var actions = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.Single(actions);
    }

    [Fact]
    public void The_content_set_and_content_scope_consoles_and_their_assets_are_gone()
    {
        var web = typeof(ContentSetsRedirectController).Assembly;
        Assert.Null(web.GetType("Diten.Web.Controllers.CRM.ContentSetsController"));
        Assert.Null(web.GetType("Diten.Web.Controllers.CRM.ContentScopesController"));
        Assert.Null(web.GetType("Diten.Web.Models.CRM.ContentSetCreateViewModel"));
        Assert.Null(web.GetType("Diten.Web.Views.CRM.ContentSets.ContentSetsIndex"));

        var root = Path.Combine(RepoRoot(), "frontend", "Diten.Web");
        foreach (var dir in new[] { "ContentSets", "ContentScopes" })
        {
            Assert.False(Directory.Exists(Path.Combine(root, "Views", "CRM", dir)), $"Views/CRM/{dir}");
            Assert.False(Directory.Exists(Path.Combine(root, "wwwroot", "assets", "js", "CRM", dir)), $"js/CRM/{dir}");
            Assert.False(Directory.Exists(Path.Combine(root, "Resources", "Views", "CRM", dir)), $"Resources/{dir}");
        }

        // No controller of the Web layer proxies a content-set / content-scope endpoint any more.
        var proxies = Directory.EnumerateFiles(Path.Combine(root, "Controllers"), "*.cs", SearchOption.AllDirectories)
            .Where(f => Regex.IsMatch(File.ReadAllText(f), "content-composition/content-(sets|set-revisions|scopes)"))
            .Select(Path.GetFileName)
            .ToList();
        Assert.Empty(proxies);
    }

    [Fact]
    public void The_claims_quick_view_lists_knowledge_paths_linked_to_the_studio_and_never_a_content_set()
    {
        var script = File.ReadAllText(Path.Combine(RepoRoot(), "frontend", "Diten.Web", "wwwroot", "assets", "js", "CRM", "Claims", "index.js"));
        Assert.Contains("'knowledge-path': id => `/CRM/KnowledgePaths/${encodeURIComponent(id)}`", script);
        Assert.Contains("getJson(`${api}/claims/usage?claimCode=${encodeURIComponent(claimCode)}`)", script);
        Assert.Contains("const usageType = type => L['UsageType_' + type] || L.Unknown;", script);
        Assert.DoesNotContain("content-set", script);
        Assert.DoesNotContain("/CRM/ContentSets", script);
        var view = File.ReadAllText(Path.Combine(RepoRoot(), "frontend", "Diten.Web", "Views", "CRM", "Claims", "Index.cshtml"));
        Assert.Contains("id=\"pvUsageList\"", view);
        Assert.Contains("@Localizer[\"QvUsage\"]", view);

        // Every L.* key the usage list reads is in the bridge.
        foreach (var key in Regex.Matches(script.Substring(script.IndexOf("const USAGE_LINKS", StringComparison.Ordinal)),
                         @"\bL\.([A-Za-z]+)\b").Select(m => m.Groups[1].Value).Distinct())
        {
            Assert.Contains(key, ClaimsIndexL10nKeys.Bridge);
        }
    }

    [Fact]
    public void The_usage_keys_are_translated_in_seven_languages_without_echo()
    {
        Assert.Contains("UsageType_knowledge-path", ClaimsIndexL10nKeys.UsageKeys);
        Assert.DoesNotContain(ClaimsIndexL10nKeys.Bridge, k => k.Contains("content-set", StringComparison.Ordinal));
        foreach (var language in Languages)
        {
            var values = Values(language);
            foreach (var key in ClaimsIndexL10nKeys.UsageKeys)
            {
                Assert.True(values.TryGetValue(key, out var text) && !string.IsNullOrWhiteSpace(text), $"{language}: {key}");
                Assert.NotEqual(key, text);
            }
        }

        Assert.Equal("Bilgi yolu", Values("tr")["UsageType_knowledge-path"]);
        Assert.Equal("Knowledge path", Values("en")["UsageType_knowledge-path"]);
    }

    [Theory]
    [InlineData("", false)]                         // a new content: the SB-2 vocabulary is not offered
    [InlineData("presentation", false)]
    [InlineData("assembled-presentation", true)]    // an old record keeps showing its own value
    public void The_content_form_no_longer_offers_the_retired_release_vocabulary(string current, bool offered)
    {
        string[] types = ["presentation", "assembled-presentation", "brochure"];
        var options = KnowledgeController.WithoutRetired(types, KnowledgeController.RetiredContentType, current);
        Assert.Equal(offered, options.Contains("assembled-presentation"));
        Assert.Contains("presentation", options);

        string[] sources = ["manual", "content-studio"];
        Assert.Equal(["manual"], KnowledgeController.WithoutRetired(sources, KnowledgeController.RetiredContentSource, "manual"));
    }

    private static Dictionary<string, string> Values(string language)
    {
        var path = Path.Combine(RepoRoot(), "frontend", "Diten.Web", "Resources", "Views", "CRM", "Claims", $"ClaimsIndex.{language}.resx");
        return XDocument.Load(path).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty, StringComparer.Ordinal);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Resources")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
