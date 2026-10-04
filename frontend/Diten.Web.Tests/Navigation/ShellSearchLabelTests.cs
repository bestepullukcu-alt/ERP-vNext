using System.Globalization;
using System.Text.Encodings.Web;
using Diten.Web.Services.Navigation;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Xunit;

namespace Diten.Web.Tests.Navigation;

/// <summary>
/// BL-520 / CT-SHELL-FIX1 item 8 — the label the three shell layouts hand to main.js as <c>data-search-placeholder</c>.
/// A missing resource comes back from the localizer as its own KEY; written through, the search box would read
/// "ShellSearchPlaceholder" in every language. The layouts take the label through <see cref="ShellSearchLabel"/>, which
/// gives no label then (the attribute is omitted and main.js shows the shortcut alone).
/// </summary>
public sealed class ShellSearchLabelTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ShellSearchLabelTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public void A_missing_resource_gives_no_label_never_the_key()
    {
        // What the real localizer returns for a key it does not have: the key as the value, flagged not-found.
        var missing = new Localizer(name => new LocalizedHtmlString(name, name, isResourceNotFound: true));

        Assert.Null(ShellSearchLabel.Template(missing));
    }

    [Fact]
    public void A_blank_resource_gives_no_label()
    {
        var blank = new Localizer(name => new LocalizedHtmlString(name, "  ", isResourceNotFound: false));

        Assert.Null(ShellSearchLabel.Template(blank));
    }

    [Theory]
    [InlineData("tr", "Ara ({0})")]
    [InlineData("en", "Search ({0})")]
    [InlineData("ar", "بحث ({0})")]
    public void The_real_dictionary_gives_the_label_in_the_request_language(string culture, string expected)
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            using var scope = _factory.Services.CreateScope();
            var localizer = scope.ServiceProvider.GetRequiredService<IHtmlLocalizer<SharedResource>>();

            var label = ShellSearchLabel.Template(localizer);

            Assert.Equal(expected, label?.ToString());
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    /// <summary>CT 2026-10-04: the label went out as a <see cref="LocalizedHtmlString"/>, which formats its value when Razor
    /// writes it; the <c>{0}</c> left for main.js then threw and no signed-in page rendered. Written into the page the way
    /// Razor writes any value, the label must come out as it stands, placeholder included.</summary>
    [Fact]
    public void The_label_writes_into_the_page_with_its_placeholder_left_for_main_js()
    {
        var dictionary = new Localizer(name => new LocalizedHtmlString(name, "Ara ({0})", isResourceNotFound: false));
        object? label = ShellSearchLabel.Template(dictionary);

        var page = new HtmlContentBuilder();
        if (label is IHtmlContent html)
        {
            page.AppendHtml(html);
        }
        else
        {
            page.Append((string?)label);
        }

        using var written = new StringWriter();
        page.WriteTo(written, HtmlEncoder.Default);
        Assert.Equal("Ara ({0})", written.ToString());
    }

    private sealed class Localizer(Func<string, LocalizedHtmlString> answer) : IHtmlLocalizer
    {
        public LocalizedHtmlString this[string name] => answer(name);
        public LocalizedHtmlString this[string name, params object[] arguments] => answer(name);
        public LocalizedString GetString(string name) => new(name, answer(name).Value, answer(name).IsResourceNotFound);
        public LocalizedString GetString(string name, params object[] arguments) => GetString(name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
