using System.Reflection;
using Diten.BuildingBlocks.Email;
using Xunit;

namespace Diten.BuildingBlocks.Email.Tests;

public sealed class EmailShellTextsTests
{
    private static readonly string[] Seven = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    private static IEnumerable<PropertyInfo> Sentences() => typeof(EmailShellTexts)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(p => p.PropertyType == typeof(string) && p.Name != nameof(EmailShellTexts.Language));

    [Fact]
    public void The_shell_speaks_exactly_the_seven_tenant_languages()
    {
        Assert.Equal(Seven, EmailShellTexts.SupportedLanguages);
        foreach (var language in Seven)
        {
            Assert.Equal(language, EmailShellTexts.For(language).Language);
        }
    }

    [Fact]
    public void Every_sentence_exists_in_every_language()
    {
        foreach (var language in Seven)
        {
            var texts = EmailShellTexts.For(language);
            foreach (var sentence in Sentences())
            {
                Assert.False(
                    string.IsNullOrWhiteSpace((string?)sentence.GetValue(texts)),
                    $"{sentence.Name} is empty in '{language}'.");
            }
        }
    }

    [Fact]
    public void No_language_is_a_copy_of_English()
    {
        var english = EmailShellTexts.For("en");
        // "via" is a real French word as well: the sender pattern alone may coincide; the sentences may not.
        foreach (var language in Seven.Where(l => l != "en"))
        {
            var texts = EmailShellTexts.For(language);
            foreach (var sentence in Sentences().Where(p => p.Name != nameof(EmailShellTexts.SenderVia)))
            {
                Assert.NotEqual((string?)sentence.GetValue(english), (string?)sentence.GetValue(texts));
            }
        }

        foreach (var language in Seven.Where(l => l is not ("en" or "fr")))
        {
            Assert.NotEqual(english.SenderVia, EmailShellTexts.For(language).SenderVia);
        }
    }

    [Fact]
    public void Every_sentence_keeps_its_placeholders()
    {
        foreach (var language in Seven)
        {
            var texts = EmailShellTexts.For(language);
            Assert.Contains("{0}", texts.SenderVia);
            Assert.Contains("{1}", texts.SenderVia);
            Assert.Contains("{0}", texts.FooterOnBehalf);
            Assert.Contains("{1}", texts.FooterOnBehalf);
            Assert.Contains("{0}", texts.FooterPlatform);
            Assert.Contains("{0}", texts.FooterReply);
        }
    }

    [Fact]
    public void Only_Arabic_is_right_to_left()
    {
        Assert.Equal(["ar"], Seven.Where(l => EmailShellTexts.For(l).RightToLeft).ToArray());
    }

    [Theory]
    [InlineData(null, "en")]
    [InlineData("", "en")]
    [InlineData("TR", "tr")]
    [InlineData("tr-TR", "tr")]
    [InlineData("zh_CN", "zh")]
    [InlineData("de", "en")]
    public void A_language_outside_the_seven_falls_back_to_English_instead_of_failing(string? language, string expected)
    {
        Assert.Equal(expected, EmailShellTexts.NormalizeLanguage(language));
    }
}
