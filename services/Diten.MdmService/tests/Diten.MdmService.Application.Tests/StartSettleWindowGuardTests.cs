using System.Reflection;
using Diten.MdmService.Api.Configuration;
using Xunit;

namespace Diten.MdmService.Application.Tests;

/// <summary>
/// WP-MDM-SETTLE-CAP-01 — every approval worker's <c>StartSettleSeconds</c> stays under Platform's sweep of
/// half-started workflows (<see cref="StartSettleWindow"/>, 25 min). Found by reflection over the PRODUCTION assembly:
/// a new <c>*WorkflowWorkerOptions</c> that carries the setting is held to the rule without anyone listing it here.
/// Each type is judged by its own validation member — the one the host's start-up validation and its worker call:
/// <c>IsValid()</c> (false) or <c>EnsureValidWhenEnabled()</c> (throws, the worker enabled with a lease owner).
/// </summary>
public sealed class StartSettleWindowGuardTests
{
    // The workers that carry the window today, by name — so a scan that misses one (or finds none) is seen.
    private static readonly string[] KnownCarriers =
    [
        nameof(ProductIdentityWorkflowWorkerOptions),
        nameof(GlobalProductCorrectionWorkflowWorkerOptions),
        nameof(GlobalProductRetirementRequestWorkflowWorkerOptions),
        nameof(GskuCorrectionWorkflowWorkerOptions),
        nameof(GskuRetirementRequestWorkflowWorkerOptions),
        nameof(LskuIdentityWorkflowWorkerOptions),
        nameof(LskuRetirementRequestWorkflowWorkerOptions)
    ];

    public static IReadOnlyList<Type> Carriers() => typeof(StartSettleWindow).Assembly.GetTypes()
        .Where(type => type is { IsClass: true, IsAbstract: false }
                       && type.Name.EndsWith("WorkflowWorkerOptions", StringComparison.Ordinal)
                       && type.GetProperty("StartSettleSeconds", BindingFlags.Public | BindingFlags.Instance)?.PropertyType == typeof(int))
        .OrderBy(type => type.Name, StringComparer.Ordinal)
        .ToList();

    public static IEnumerable<object[]> CarrierNames() => Carriers().Select(type => new object[] { type.Name });

    [Fact]
    public void The_scan_finds_every_worker_that_carries_the_window()
    {
        Assert.Equal(KnownCarriers.Order(StringComparer.Ordinal), Carriers().Select(type => type.Name));
    }

    [Theory]
    [MemberData(nameof(CarrierNames))]
    public void A_settle_window_past_Platforms_sweep_is_refused_and_its_edge_accepted(string carrier)
    {
        var type = Carriers().Single(type => type.Name == carrier);

        // Literal seconds, not the constant: a constant moved past Platform's sweep must turn this red too.
        Assert.False(Accepts(type, 1501), $"{carrier} accepts 1501 s");
        Assert.True(Accepts(type, 1500), $"{carrier} refuses 1500 s");
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
