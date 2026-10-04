using System.Collections.ObjectModel;
using System.Reflection;
using Diten.SupplyChainService.Api.ModuleRegistration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace Diten.SupplyChainService.Tests.ModuleRegistration;

// R-3 / Q398 (MODULE-RECIPE 6.4): a permission key enforced on an endpoint must be declared by its module's manifest
// provider. Platform syncs to Auth only the keys a manifest declares (Q353 §A), so an undeclared enforced key can never be
// granted and its endpoint answers 403 to everyone, forever. R-2 hit exactly that: supplychain.returns.transition was
// enforced on every Returns transition (ReturnsController.cs:20) and declared nowhere (F-R2-1).
//
// Enforcement is found by reading the attributes, not by name-guessing: each module has its own attribute
// (HasPermission, ReturnPermission, LoadPermission, CarrierPermission, ClaimPermission, SandopPermission,
// CapacityPermission). Every attribute whose type name ends in "PermissionAttribute" is read through its constructor
// arguments, and a second test fails if any routed endpoint carries none, so a new attribute that does not follow the
// naming cannot hide its keys from this guard.
public sealed class EnforcedPermissionDeclarationGuardTests
{
    private static readonly Assembly ApiAssembly = typeof(IModuleManifestProvider).Assembly;

    // Modules whose provider must NOT exist yet: their packs ship the provider, its AddSingleton and its navigation keys
    // WITH the module's UI, never ahead of it. Each entry is self-invalidating: the test fails the moment a provider with
    // that ModuleCode exists or any provider declares a key of that area, which forces the entry out and the keys in when
    // the module's UI is built (R-4).
    private static readonly IReadOnlyDictionary<string, (string ModuleCode, string Reason)> KnownWithoutProvider =
        new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            ["loads"] = ("routing-load-planning", "MOD-0185:602 ship rule — no UI yet"),
            ["claims"] = ("claims-management", "MOD-0187:819 ship rule — no UI yet"),
            ["sandop-plans"] = ("sop-workflow-signoffs", "MOD-0190:552 ship rule — no UI yet; also MediatR-excluded (Q273)"),
            ["capacity-plans"] = ("capacity-planning", "MOD-0192:574 ship rule — no UI yet; also uncomposed (Q273)")
        };

    [Fact]
    public void Every_enforced_key_is_declared_by_its_module_provider()
    {
        var declared = Declared();
        var failures = new List<string>();
        foreach (var group in Enforced().GroupBy(e => Area(e.Key)).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            if (KnownWithoutProvider.ContainsKey(group.Key)) continue;
            var keys = declared.Where(d => Area(d.Key) == group.Key).Select(d => d.Key).ToHashSet(StringComparer.Ordinal);
            failures.AddRange(group.Where(e => !keys.Contains(e.Key)).Select(e => $"{e.Key} — enforced at {e.Where}, declared by no provider"));
        }

        Assert.True(failures.Count == 0,
            "Enforced but undeclared, so no role can ever hold them (Platform syncs only declared keys):\n" + string.Join("\n", failures.Distinct()));
    }

    [Fact]
    public void Known_without_provider_entries_are_still_true()
    {
        var providers = Providers();
        var declaredAreas = Declared().Select(d => Area(d.Key)).ToHashSet(StringComparer.Ordinal);
        var enforcedAreas = Enforced().Select(e => Area(e.Key)).ToHashSet(StringComparer.Ordinal);
        var stale = new List<string>();
        foreach (var (area, (moduleCode, reason)) in KnownWithoutProvider)
        {
            var owner = providers.FirstOrDefault(p => p.Manifest.ModuleCode == moduleCode);
            if (owner.Type is not null)
                stale.Add($"'{area}': {owner.Type.Name} now provides {moduleCode} — remove the entry; its enforced keys must be declared ({reason})");
            else if (declaredAreas.Contains(area))
                stale.Add($"'{area}': a provider now declares {area} keys — remove the entry ({reason})");
            if (!enforcedAreas.Contains(area))
                stale.Add($"'{area}': no endpoint enforces {area} keys any more — remove the entry");
        }

        Assert.True(stale.Count == 0, "KnownWithoutProvider entries that are no longer true:\n" + string.Join("\n", stale));
    }

    [Fact]
    public void Every_routed_endpoint_carries_a_recognised_permission_attribute()
    {
        // Guards the enumeration itself: an endpoint whose permission attribute does not end in "PermissionAttribute" would
        // otherwise contribute no keys and make the guard above vacuously green.
        var unguarded = Endpoints()
            .Where(e => !PermissionAttributes(e.Method).Any() && !PermissionAttributes(e.Method.DeclaringType!).Any())
            .Select(e => $"{e.Method.DeclaringType!.Name}.{e.Method.Name}")
            .ToArray();
        Assert.True(unguarded.Length == 0, "Routed endpoints with no recognised permission attribute:\n" + string.Join("\n", unguarded));
    }

    [Fact]
    public void Enumeration_finds_every_module_attribute()
    {
        var used = Endpoints().SelectMany(e => PermissionAttributes(e.Method)).Select(a => a.AttributeType.Name).ToHashSet(StringComparer.Ordinal);
        Assert.Subset(used, new HashSet<string>(StringComparer.Ordinal)
        {
            "HasPermissionAttribute", "ReturnPermissionAttribute", "LoadPermissionAttribute", "CarrierPermissionAttribute",
            "ClaimPermissionAttribute", "SandopPermissionAttribute", "CapacityPermissionAttribute"
        });
    }

    private static IEnumerable<(string Key, string Where)> Enforced() =>
        Endpoints().SelectMany(e => PermissionAttributes(e.Method).Concat(PermissionAttributes(e.Method.DeclaringType!))
            .SelectMany(Keys).Select(key => (key, $"{e.Method.DeclaringType!.Name}.{e.Method.Name}")));

    private static IEnumerable<(Type Controller, MethodInfo Method)> Endpoints() =>
        ApiAssembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(ControllerBase).IsAssignableFrom(t))
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any())
                .Select(m => (t, m)));

    private static IEnumerable<CustomAttributeData> PermissionAttributes(MemberInfo member) =>
        member.CustomAttributes.Where(a => a.AttributeType.Name.EndsWith("PermissionAttribute", StringComparison.Ordinal));

    // Constructor arguments are either one string or a params string[] (HasPermission, ClaimPermission).
    private static IEnumerable<string> Keys(CustomAttributeData attribute) =>
        attribute.ConstructorArguments.SelectMany(argument => argument.Value switch
        {
            string key => new[] { key },
            ReadOnlyCollection<CustomAttributeTypedArgument> keys => keys.Select(k => (string)k.Value!),
            _ => Array.Empty<string>()
        });

    private static List<(Type Type, Diten.BuildingBlocks.ModuleRegistration.Abstractions.ModuleManifestDocument Manifest)> Providers() =>
        ApiAssembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IModuleManifestProvider).IsAssignableFrom(t))
            .Select(t => (t, ((IModuleManifestProvider)Activator.CreateInstance(t)!).GetManifest()))
            .ToList();

    // A provider counts whether or not Program.cs composes it: the ship rule withholds the AddSingleton (Carrier), not the
    // declaration, and the sync carries a key once the provider is composed.
    private static IEnumerable<(string Key, string Provider)> Declared() =>
        Providers().SelectMany(p => p.Manifest.Pages.SelectMany(page =>
            page.Actions.Select(a => a.PermissionKey).Append(page.RequiredPermission)).Select(key => (key, p.Type.Name)));

    private static string Area(string key) => key.Split('.') is { Length: >= 3 } parts ? parts[1] : key;
}
