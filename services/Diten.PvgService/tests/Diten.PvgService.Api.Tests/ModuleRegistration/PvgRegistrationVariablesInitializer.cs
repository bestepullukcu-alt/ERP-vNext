using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;

namespace Diten.PvgService.Api.Tests.ModuleRegistration;

// WP-0230-DL11, AC-37 (c): the test assembly clears both registration variables before any test code runs, so a host
// started by any test in this assembly finds none even when the developer's shell sets them (and so no existing host
// test can reach a real Platform from a developer's shell). The initializer calls a method that takes the setter as a
// parameter; the test calls that method with a recording setter and never writes to the process environment itself.
internal static class PvgRegistrationVariablesInitializer
{
    internal static readonly string[] VariableNames =
    [
        "PlatformRegistration__InternalApiKey",
        "PlatformRegistration__BaseUrl"
    ];

    [ModuleInitializer]
    internal static void ClearAtLoad() => ClearRegistrationVariables(Environment.SetEnvironmentVariable);

    internal static void ClearRegistrationVariables(Action<string, string?> setter)
    {
        setter("PlatformRegistration__InternalApiKey", null);
        setter("PlatformRegistration__BaseUrl", null);
    }
}

public sealed class PvgRegistrationVariablesInitializerTests
{
    [Fact]
    public void Test_assembly_clears_the_registration_variables_before_any_host_starts()
    {
        // The method clears both names; measured with a recording setter, so the process environment is not written.
        var recorded = new List<(string Name, string? Value)>();
        PvgRegistrationVariablesInitializer.ClearRegistrationVariables((name, value) => recorded.Add((name, value)));

        Assert.Equal(2, recorded.Count);
        Assert.Equal(
            new[] { "PlatformRegistration__BaseUrl", "PlatformRegistration__InternalApiKey" },
            recorded.Select(entry => entry.Name).Order(StringComparer.Ordinal).ToArray());
        Assert.All(recorded, entry => Assert.Null(entry.Value));

        // The module initializer is what runs it when the assembly loads.
        var initializer = typeof(PvgRegistrationVariablesInitializer).GetMethod(
            nameof(PvgRegistrationVariablesInitializer.ClearAtLoad),
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotNull(initializer);
        Assert.NotNull(initializer!.GetCustomAttribute<ModuleInitializerAttribute>());

        // So both variables read as unset in this process, whatever the shell set.
        foreach (var name in PvgRegistrationVariablesInitializer.VariableNames)
        {
            Assert.True(string.IsNullOrEmpty(Environment.GetEnvironmentVariable(name)), name + " is set in the test process");
        }
    }
}
