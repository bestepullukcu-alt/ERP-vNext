using System.Globalization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Web.Tests.Platform;

/// <summary>
/// BL-521 FIX5 item 7 — THE TWO NEW BULK-DELETE ANSWERS SPEAK THE READER'S LANGUAGE. Platform answers a repeated id (400) and a
/// refusal that came after some targets were already deleted (409 / 503) with fixed English sentences; the administrators list
/// shows them in the user's language by matching the sentence in <c>index.js</c> (localizeServerError). Platform is a
/// two-language module: en + tr. The measured parts: both languages have both keys, the Turkish text is Turkish (not the English
/// source), the page hands both keys to its script, and the script's matching patterns match the English source sentences.
/// </summary>
public sealed class AdministratorBulkDeleteMessagesTests
{
    private const string DuplicateIds = "The same administrator is listed more than once. Nothing was deleted.";
    private const string DeletePartial = "Some administrators were deleted, but the request stopped at one that could not be deleted. Reload the list to see what remains.";

    [Theory]
    [InlineData("en", "AdminBulkDuplicateIds", DuplicateIds)]
    [InlineData("en", "AdminBulkDeletePartial", DeletePartial)]
    public void The_english_resource_is_the_sentence_platform_answers_with(string culture, string key, string platformSentence)
    {
        var value = Text(culture, key);

        Assert.Equal(platformSentence, value);
    }

    [Theory]
    [InlineData("AdminBulkDuplicateIds", DuplicateIds)]
    [InlineData("AdminBulkDeletePartial", DeletePartial)]
    public void The_turkish_resource_exists_and_is_not_the_english_source(string key, string platformSentence)
    {
        var value = Text("tr", key);

        Assert.NotEqual(platformSentence, value);
        Assert.False(string.IsNullOrWhiteSpace(value));
    }

    [Fact]
    public void The_page_hands_both_keys_to_its_script_and_the_script_matches_the_platform_sentences()
    {
        var l10n = File.ReadAllText(RepoPath("frontend", "Diten.Web", "Views", "Platform", "Administrators", "_IndexL10n.cshtml"));
        var keys = File.ReadAllText(RepoPath("frontend", "Diten.Web", "wwwroot", "assets", "js", "Platform", "Administrators", "index.l10n.js"));
        var script = File.ReadAllText(RepoPath("frontend", "Diten.Web", "wwwroot", "assets", "js", "Platform", "Administrators", "index.js"));

        foreach (var key in new[] { "AdminBulkDuplicateIds", "AdminBulkDeletePartial" })
        {
            Assert.Contains($"SharedLocalizer[\"{key}\"]", l10n, StringComparison.Ordinal);
            Assert.Contains($"'{key}'", keys, StringComparison.Ordinal);
            Assert.Contains($"L.{key}", script, StringComparison.Ordinal);
        }

        // the patterns the script tests the server's sentence with are literal prefixes of the sentences Platform sends
        Assert.StartsWith("The same administrator is listed more than once", DuplicateIds, StringComparison.Ordinal);
        Assert.Contains("The same administrator is listed more than once", script, StringComparison.Ordinal);
        Assert.StartsWith("Some administrators were deleted, but the request stopped", DeletePartial, StringComparison.Ordinal);
        Assert.Contains("Some administrators were deleted, but the request stopped", script, StringComparison.Ordinal);
    }

    private static string Text(string culture, string key)
    {
        var previous = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo(culture);
            var localizer = new StringLocalizer<SharedResource>(new ResourceManagerStringLocalizerFactory(
                Options.Create(new LocalizationOptions { ResourcesPath = "Resources" }), NullLoggerFactory.Instance));
            var value = localizer[key];
            Assert.False(value.ResourceNotFound, $"SharedResource.{culture} has no {key}.");
            return value.Value;
        }
        finally
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = previous;
        }
    }

    private static string RepoPath(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "frontend", "Diten.Web", "Resources")))
        {
            directory = directory.Parent;
        }

        return Path.Combine([directory?.FullName ?? throw new DirectoryNotFoundException("repository root"), .. parts]);
    }
}
