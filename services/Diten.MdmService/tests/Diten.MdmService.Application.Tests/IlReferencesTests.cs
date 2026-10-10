using Diten.MdmService.Tests.Shared;
using Xunit;

namespace Diten.MdmService.Application.Tests;

/// <summary>
/// MOD-0290-FU04 S3b FIX2 (minor) — the IL reader the S3b guards stand on is measured itself, on synthetic code: a call
/// hidden behind a helper, then an async lambda, then an interface (and a virtual override) is still reached; a
/// <c>typeof</c> is reached as the type. Its limit (a member found by a string at run time) is written on the reader.
/// </summary>
public sealed class IlReferencesTests
{
    [Fact]
    public void S3b_FIX2_A_call_behind_a_helper_and_an_async_lambda_is_reached()
    {
        var reached = IlReferences.MembersReachedFrom(typeof(SyntheticCaller));
        Assert.Contains(typeof(SyntheticFill).GetMethod(nameof(SyntheticFill.Fill)), reached);
        // Not reached when only the caller's own methods are read (the helper is another type).
        Assert.DoesNotContain(typeof(SyntheticFill).GetMethod(nameof(SyntheticFill.Fill)),
            IlReferences.MembersReachedFrom(typeof(SyntheticCaller), transitive: false));
    }

    [Fact]
    public void S3b_FIX2_A_call_through_an_interface_or_a_virtual_method_reaches_the_implementation()
    {
        var reached = IlReferences.MembersReachedFrom(typeof(SyntheticInterfaceCaller));
        Assert.Contains(typeof(SyntheticFill).GetMethod(nameof(SyntheticFill.ThroughInterface)), reached);
        Assert.Contains(typeof(SyntheticFill).GetMethod(nameof(SyntheticFill.ThroughOverride)), reached);
    }

    [Fact]
    public void S3b_FIX2_A_typeof_is_reached_as_the_type()
    {
        Assert.Contains(typeof(SyntheticFill), IlReferences.MembersReachedFrom(typeof(SyntheticTypeOf)));
    }

    private static class SyntheticFill
    {
        public static Task Fill() => Task.CompletedTask;
        public static Task ThroughInterface() => Task.CompletedTask;
        public static Task ThroughOverride() => Task.CompletedTask;
    }

    private sealed class SyntheticCaller
    {
        public Task Run() => SyntheticHelper.Later();
    }

    private static class SyntheticHelper
    {
        public static Task Later()
        {
            Func<Task> lambda = async () => await SyntheticFill.Fill();
            return lambda();
        }
    }

    private interface ISyntheticStep
    {
        Task Step();
    }

    private sealed class SyntheticStep : ISyntheticStep
    {
        public Task Step() => SyntheticFill.ThroughInterface();
    }

    private abstract class SyntheticBase
    {
        public abstract Task Step();
    }

    private sealed class SyntheticDerived : SyntheticBase
    {
        public override Task Step() => SyntheticFill.ThroughOverride();
    }

    private sealed class SyntheticInterfaceCaller
    {
        public Task Run(ISyntheticStep step, SyntheticBase step2) => Task.WhenAll(step.Step(), step2.Step());
    }

    private sealed class SyntheticTypeOf
    {
        public string Name() => typeof(SyntheticFill).Name;
    }
}
