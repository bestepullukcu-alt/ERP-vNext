using System.Reflection;
using Diten.MdmService.Api.Configuration;
using Xunit;

namespace Diten.MdmService.Application.Tests;

/// <summary>
/// WP-MDM-SETTLE-CAP-01 — every approval worker's <c>StartSettleSeconds</c> stays under Platform's sweep of
/// half-started workflows (<see cref="StartSettleWindow"/>: 1–600 s, FIX1). Found by reflection over the PRODUCTION
/// assemblies: a new setting is held to the rule without anyone listing it here. Each type is judged by its own
/// validation member — the one the host's start-up validation and its worker call: <c>IsValid()</c> (false) or
/// <c>EnsureValidWhenEnabled()</c> (throws, the worker enabled with a lease owner).
/// </summary>
public sealed class StartSettleWindowGuardTests
{
    // FIX1 M1: EVERY member of an *Options type in the Api and Application assemblies whose name says "Settle" (int or
    // TimeSpan, any visibility, instance) — so a settle window that is not StartSettleSeconds, or lives in another
    // assembly, cannot appear unseen. Today they are exactly the seven workers' StartSettleSeconds.
    private static readonly string[] KnownSettleMembers =
    [
        nameof(GlobalProductCorrectionWorkflowWorkerOptions) + ".StartSettleSeconds",
        nameof(GlobalProductRetirementRequestWorkflowWorkerOptions) + ".StartSettleSeconds",
        nameof(GskuCorrectionWorkflowWorkerOptions) + ".StartSettleSeconds",
        nameof(GskuRetirementRequestWorkflowWorkerOptions) + ".StartSettleSeconds",
        nameof(LskuIdentityWorkflowWorkerOptions) + ".StartSettleSeconds",
        nameof(LskuRetirementRequestWorkflowWorkerOptions) + ".StartSettleSeconds",
        nameof(ProductIdentityWorkflowWorkerOptions) + ".StartSettleSeconds"
    ];

    private static readonly System.Reflection.Assembly[] Scanned =
    [
        typeof(StartSettleWindow).Assembly,
        typeof(Diten.MdmService.Application.Features.ProductItemSkuMaster.Audit.CentralMappingRollout).Assembly
    ];

    public static IReadOnlyList<string> SettleMembers() => Scanned.SelectMany(assembly => assembly.GetTypes())
        .Where(type => type.Name.EndsWith("Options", StringComparison.Ordinal))
        .SelectMany(type => type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(member => member is PropertyInfo or FieldInfo && !member.Name.StartsWith('<')
                             && member.Name.Contains("Settle", StringComparison.Ordinal)
                             && (member is PropertyInfo p ? p.PropertyType : ((FieldInfo)member).FieldType) is var t
                             && (t == typeof(int) || t == typeof(TimeSpan)))
            .Select(member => type.Name + "." + member.Name))
        .Order(StringComparer.Ordinal)
        .ToList();

    public static IReadOnlyList<Type> Carriers() => Scanned.SelectMany(assembly => assembly.GetTypes())
        .Where(type => type is { IsClass: true, IsAbstract: false }
                       && type.Name.EndsWith("Options", StringComparison.Ordinal)
                       && type.GetProperty("StartSettleSeconds", BindingFlags.Public | BindingFlags.Instance)?.PropertyType == typeof(int))
        .OrderBy(type => type.Name, StringComparer.Ordinal)
        .ToList();

    public static IEnumerable<object[]> CarrierNames() => Carriers().Select(type => new object[] { type.Name });

    [Fact]
    public void The_scan_finds_every_settle_window_in_every_options_type()
    {
        Assert.Equal(KnownSettleMembers.Order(StringComparer.Ordinal), SettleMembers());
        Assert.Equal(KnownSettleMembers.Select(member => member[..member.IndexOf('.')]).Order(StringComparer.Ordinal),
            Carriers().Select(type => type.Name));
    }

    [Theory]
    [MemberData(nameof(CarrierNames))]
    public void A_settle_window_past_Platforms_sweep_is_refused_and_its_edge_accepted(string carrier)
    {
        var type = Carriers().Single(type => type.Name == carrier);

        // Literal seconds, not the constant: a constant moved past Platform's sweep must turn this red too (and the
        // floor: FIX1, CT sabotage D2 — a window of 0 trusts "nothing arrived" at once).
        Assert.False(Accepts(type, 0), $"{carrier} accepts 0 s");
        Assert.True(Accepts(type, 1), $"{carrier} refuses 1 s");
        Assert.False(Accepts(type, 601), $"{carrier} accepts 601 s");
        Assert.True(Accepts(type, 600), $"{carrier} refuses 600 s");
        Assert.True(Accepts(type, 120), $"{carrier} refuses its default 120 s");
    }

    private static bool Accepts(Type type, int seconds)
    {
        var options = Activator.CreateInstance(type)!;
        Set(options, "StartSettleSeconds", seconds);
        Set(options, "Enabled", true);
        Set(options, "LeaseOwner", "guard-worker");

        var isValid = type.GetMethod("IsValid", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
        if (isValid is not null && isValid.ReturnType == typeof(bool)) return (bool)isValid.Invoke(options, null)!;

        var ensure = type.GetMethod("EnsureValidWhenEnabled", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes)
                     ?? throw new InvalidOperationException($"{type.Name} has no validation member the host calls.");
        try
        {
            ensure.Invoke(options, null);
            return true;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is InvalidOperationException)
        {
            return false;
        }
    }

    // init-only setters are ordinary setters to reflection.
    private static void Set(object options, string property, object value) =>
        options.GetType().GetProperty(property, BindingFlags.Public | BindingFlags.Instance)?.SetValue(options, value);
}
