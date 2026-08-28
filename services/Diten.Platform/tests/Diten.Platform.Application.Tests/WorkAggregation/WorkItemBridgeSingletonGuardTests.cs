using System.Reflection;
using Diten.Platform.Application.Features.Tasks.Providers;
using Diten.Platform.Application.Features.WorkAggregation.Dispatch;
using Diten.Platform.Application.Features.WorkAggregation.Providers;
using Diten.Platform.Infrastructure.Services.WorkAggregation;
using Xunit;

namespace Diten.Platform.Application.Tests.WorkAggregation;

/// <summary>
/// WC-D1 — THE GUARD THAT REFUSES A SECOND BRIDGE CLASS, ACROSS BOTH PLATFORM ASSEMBLIES.
///
/// <para><b>Why this file exists even though four other tests look like they cover it.</b> They do not, and the
/// gap was measured on 2026-08-28:</para>
/// <list type="bullet">
/// <item><c>HttpWorkItemBridgeTests.Two_configuration_rows_bind_two_providers_and_two_dispatchers_from_one_class_each</c>
/// counts what an ISOLATED container produced from two rows. It proves configuration multiplies one class; it
/// says nothing about what else the repository contains.</item>
/// <item><c>HttpWorkItemBridgeTests.The_network_seam_has_exactly_one_implementation_and_it_names_no_module</c>
/// pins the set — but only in the INFRASTRUCTURE assembly. A hand-written bridge added to
/// <c>Diten.Platform.Application</c> never reaches it.</item>
/// <item><c>ProviderActionPermissionTests.Every_provider_in_the_assembly_declares_its_action_permissions</c> and
/// <c>WorkItemActionDispatchTests.Every_provider_in_the_assembly_has_a_dispatcher_for_its_code</c> scan the
/// Application assembly but assert <c>&gt;= 2</c> and a matching COUNT. A third provider shipped with a third
/// dispatcher satisfies both.</item>
/// </list>
///
/// <para>So until this file, the sentence <c>RemoteWorkItemProviderOptions</c> already wrote — "a guard test
/// refuses a second implementation of either seam" — was true of one assembly and untrue of the other. This test
/// makes it true of both: the expected set is written down, and ANY implementation outside it fails, wherever it
/// is added and however faithfully it copies the existing ones.</para>
///
/// <para>That is the whole point. A per-module bridge class is not caught by review — it is caught here, because
/// the failure it produces (N timeouts, N error dictionaries, one slow module slowing the board with nobody able
/// to say which) only appears in production, months later, to the wrong person. The "first team to need it sets
/// the pattern" advice was WITHDRAWN on 2026-08-26 for exactly that reason.</para>
///
/// <para><b>If this test fails because you added a class, that is the test working.</b> The fix is a
/// configuration row under <c>WorkAggregation:RemoteProviders</c>, not an entry in the list below. Widen the
/// expected set only when the owner has decided a genuinely new TRANSPORT exists (the current two are in-process
/// and HTTP) — and write down which one, here, with the date.</para>
/// </summary>
public sealed class WorkItemBridgeSingletonGuardTests
{
    /// <summary>
    /// Both assemblies a Platform bridge class could be written into. The Application assembly holds the
    /// in-process providers; the Infrastructure assembly holds the single network seam.
    /// </summary>
    private static readonly Assembly[] PlatformAssemblies =
    [
        typeof(IWorkItemProvider).Assembly,
        typeof(HttpWorkItemProvider).Assembly
    ];

    /// <summary>
    /// The complete, deliberate set. Two in-process providers for the two modules that live inside Platform, and
    /// ONE network provider that serves every module that does not — by configuration row, not by class.
    /// </summary>
    private static readonly Type[] ExpectedProviders =
    [
        typeof(HttpWorkItemProvider),
        typeof(TaskWorkItemProvider),
        typeof(WorkflowApprovalWorkItemProvider)
    ];

    /// <summary>The write half, one dispatcher per provider, on the same terms.</summary>
    private static readonly Type[] ExpectedDispatchers =
    [
        typeof(HttpWorkItemActionDispatcher),
        typeof(TaskWorkItemActionDispatcher),
        typeof(WorkflowApprovalWorkItemActionDispatcher)
    ];

    [Fact]
    public void Only_the_expected_work_item_seams_are_implemented_in_the_platform_assemblies()
    {
        var providers = Implementations<IWorkItemProvider>();
        var dispatchers = Implementations<IWorkItemActionDispatcher>();

        Assert.Equal(Sorted(ExpectedProviders), providers);
        Assert.Equal(Sorted(ExpectedDispatchers), dispatchers);
    }

    /// <summary>
    /// Non-vacuity. If both seams were renamed or moved out of these assemblies the test above would pass by
    /// finding nothing and comparing it to nothing; this refuses that reading before it can be believed.
    /// </summary>
    [Fact]
    public void The_guard_is_looking_at_assemblies_that_actually_contain_the_seams()
    {
        Assert.Equal(2, PlatformAssemblies.Distinct().Count());

        var providers = Implementations<IWorkItemProvider>();
        var dispatchers = Implementations<IWorkItemActionDispatcher>();

        Assert.NotEmpty(providers);
        Assert.NotEmpty(dispatchers);

        // One of each must come from EACH assembly, or the guard has quietly narrowed to a single one — which is
        // the exact blind spot this file was written to close.
        foreach (var assembly in PlatformAssemblies)
        {
            Assert.Contains(providers, t => t.Assembly == assembly);
            Assert.Contains(dispatchers, t => t.Assembly == assembly);
        }
    }

    private static List<Type> Implementations<T>()
        => PlatformAssemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(T).IsAssignableFrom(t))
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .ToList();

    private static List<Type> Sorted(IEnumerable<Type> types)
        => types.OrderBy(t => t.FullName, StringComparer.Ordinal).ToList();
}
