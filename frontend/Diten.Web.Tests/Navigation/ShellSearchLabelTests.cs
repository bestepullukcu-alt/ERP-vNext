using System.Globalization;
using Diten.Web.Services.Navigation;
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

            Assert.NotNull(label);
            Assert.Equal(expected, label!.Value);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
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
