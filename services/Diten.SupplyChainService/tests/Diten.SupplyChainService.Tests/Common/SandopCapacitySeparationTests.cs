using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;
using Diten.SupplyChainService.Application.Common;

namespace Diten.SupplyChainService.Tests.Common;

/// <summary>
/// MOD-0190:202 and MOD-0192:202 both say the two modules share no persistence and no internal DTO, and that
/// each runs against frozen mocks. Nothing asserted it. The two modules sit in one assembly set and one
/// IMongoDatabase, so "no project reference" is vacuous here — the load-bearing facts are that no type of one
/// names a type of the other, and that their collection names are disjoint. Both are checked below.
/// </summary>
public sealed class SandopCapacitySeparationTests
{
    private const string Sandop = "SandopPlans";
    private const string Capacity = "CapacityPlans";

    private static IEnumerable<Assembly> ProductAssemblies() =>
        new[] { typeof(RequestContext).Assembly, typeof(Program).Assembly }
            .Concat(typeof(RequestContext).Assembly.GetReferencedAssemblies()
                .Where(a => a.Name?.StartsWith("Diten.SupplyChainService", StringComparison.Ordinal) == true)
                .Select(Assembly.Load))
            .Distinct();

    private static bool InFeature(Type? type, string feature) =>
        type?.Namespace?.Contains(".Features." + feature, StringComparison.Ordinal) == true;

    private static IEnumerable<Type> TypesOf(string feature) =>
        ProductAssemblies().SelectMany(a => a.GetTypes()).Where(t => InFeature(t, feature));

    /// <summary>Every type one module's types mention: bases, interfaces, field/property types, member signatures.</summary>
    private static IEnumerable<Type> Mentioned(Type type)
    {
        IEnumerable<Type> Expand(Type? t) =>
            t is null ? [] : t.IsGenericType ? new[] { t }.Concat(t.GetGenericArguments()) : [t];
        foreach (var t in Expand(type.BaseType)) yield return t;
        foreach (var i in type.GetInterfaces()) foreach (var t in Expand(i)) yield return t;
        const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        foreach (var f in type.GetFields(All)) foreach (var t in Expand(f.FieldType)) yield return t;
        foreach (var p in type.GetProperties(All)) foreach (var t in Expand(p.PropertyType)) yield return t;
        foreach (var m in type.GetMethods(All))
        {
            foreach (var t in Expand(m.ReturnType)) yield return t;
            foreach (var par in m.GetParameters()) foreach (var t in Expand(par.ParameterType)) yield return t;
        }
        foreach (var c in type.GetConstructors(All))
            foreach (var par in c.GetParameters()) foreach (var t in Expand(par.ParameterType)) yield return t;
    }

    [Theory]
    [InlineData(Sandop, Capacity)]
    [InlineData(Capacity, Sandop)]
    public void Neither_module_names_a_type_of_the_other(string mine, string theirs)
    {
        var offenders = TypesOf(mine)
            .SelectMany(t => Mentioned(t).Where(x => InFeature(x, theirs)).Select(x => $"{t.FullName} -> {x.FullName}"))
            .Distinct().ToArray();
        Assert.True(offenders.Length == 0,
            $"{mine} names {theirs} types:\n{string.Join("\n", offenders)}");
    }

    [Fact]
    public void The_two_modules_persist_into_disjoint_collections()
    {
        // Collection names are string literals passed to GetCollection/GetCollectionNames in each module's
        // persistence; read them from the compiled metadata's own source rather than guessing a prefix.
        var sandop = CollectionNames(Sandop);
        var capacity = CollectionNames(Capacity);
        Assert.NotEmpty(sandop);
        Assert.NotEmpty(capacity);
        var shared = sandop.Intersect(capacity, StringComparer.Ordinal).ToArray();
        Assert.True(shared.Length == 0, "shared collections: " + string.Join(", ", shared));
        // And neither borrows the other's naming, which would make a future rename collide silently.
        Assert.DoesNotContain(sandop, n => n.StartsWith("capacity", StringComparison.Ordinal));
        Assert.DoesNotContain(capacity, n => n.StartsWith("sandop", StringComparison.Ordinal));
    }

    private static HashSet<string> CollectionNames(string feature)
    {
        var root = RepositoryRoot();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(
                     Path.Combine(root, "services", "Diten.SupplyChainService", "src"), "*.cs", SearchOption.AllDirectories)
                 .Where(f => f.Contains(Path.Combine("Features", feature), StringComparison.Ordinal)))
            foreach (Match m in Regex.Matches(File.ReadAllText(file), @"GetCollection<[^>]+>\(\s*""([a-z_]+)""\s*\)"))
                names.Add(m.Groups[1].Value);
        return names;
    }

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
