using System.Globalization;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Services;
using Xunit;

namespace Diten.MdmService.Application.Tests;

/// <summary>FIX1 S9 — three ASCII letters, checked before upper-casing (Pack:280).</summary>
public sealed class ProductAbbreviationNormalizerTests
{
    [Theory]
    [InlineData("abı")]   // dotless i: upper-cases to an ASCII I
    [InlineData("ſab")]   // long s: upper-cases to an ASCII S
    [InlineData("ABİ")]   // dotted capital I
    [InlineData("AB1")]
    [InlineData("ABCD")]
    [InlineData("")]
    [InlineData(null)]
    public void Anything_but_three_ASCII_letters_is_refused(string? value)
        => Assert.False(ProductAbbreviationNormalizer.TryNormalize(value, out _));

    [Fact]
    public void Lower_case_ASCII_is_upper_cased_the_same_way_in_every_culture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            Assert.True(ProductAbbreviationNormalizer.TryNormalize(" abi ", out var normalized));
            Assert.Equal("ABI", normalized);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
