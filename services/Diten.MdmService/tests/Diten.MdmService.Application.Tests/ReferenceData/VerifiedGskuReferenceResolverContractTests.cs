using System.Reflection;
using Diten.MdmService.Application.Contracts.ReferenceData;
using Xunit;

namespace Diten.MdmService.Application.Tests.ReferenceData;

public sealed class VerifiedGskuReferenceResolverContractTests
{
    [Fact]
    public void CallerCanSupplyOnlyLockedValueCodesAndCancellation()
    {
        var method = typeof(IVerifiedGskuReferenceResolver).GetMethod(nameof(IVerifiedGskuReferenceResolver.ResolveLatestAsync));

        Assert.NotNull(method);
        Assert.Equal(
            [typeof(string), typeof(string), typeof(CancellationToken)],
            method.GetParameters().Select(x => x.ParameterType).ToArray());
        Assert.DoesNotContain(method.GetParameters(), parameter =>
            parameter.Name?.Contains("tenant", StringComparison.OrdinalIgnoreCase) == true
            || parameter.Name?.Contains("version", StringComparison.OrdinalIgnoreCase) == true
            || parameter.Name?.Contains("mode", StringComparison.OrdinalIgnoreCase) == true
            || parameter.Name?.Contains("time", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public void Uom_enumeration_accepts_only_cancellation_and_returns_bounded_projection()
    {
        var method = typeof(IVerifiedGskuReferenceResolver)
            .GetMethod(nameof(IVerifiedGskuReferenceResolver.EnumerateUomsAsync));

        Assert.NotNull(method);
        Assert.Equal([typeof(CancellationToken)], method.GetParameters().Select(x => x.ParameterType));
        Assert.Equal(
            ["Code", "DisplayText", "MaximumDecimalPrecision", "SortOrder"],
            typeof(VerifiedGskuUom).GetProperties().Select(x => x.Name).OrderBy(x => x, StringComparer.Ordinal));
    }
}
