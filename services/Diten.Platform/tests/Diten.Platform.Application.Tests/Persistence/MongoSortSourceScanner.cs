using System.Text;
using System.Text.RegularExpressions;

namespace Diten.Platform.Application.Tests.Persistence;

/// <summary>
/// Reads the repository sources and reports every server-side sort key together with the entity type it is
/// applied to and whether that key is a <see cref="DateTimeOffset"/> ON THAT TYPE.
///
/// ⚠ WHY TYPE RESOLUTION AND NOT JUST THE PROPERTY NAME. The first version of this guard harvested every
/// DateTimeOffset property name in the solution into one flat set and matched sort keys against it. MEASURED
/// 2026-08-28: that reports 28 ascending sites where only 26 are real. <c>OutboxEvent.CreatedAt</c> is a
/// plain <see cref="DateTime"/> — it lands on disk as a scalar BSON date and sorts perfectly — but some other
/// entity in the solution has a DateTimeOffset called <c>CreatedAt</c>, so the flat set condemned it. A guard
/// that cannot tell those apart forces exceptions to be written for code that was never broken, and an
/// exception list padded with non-bugs is how a guard stops being read.
/// </summary>
internal static class MongoSortSourceScanner
{
    internal sealed record SortSite(string RelativePath, int Line, string EntityType, string Key, string Form);

    // Builders<T>.Sort.Ascending(x => x.A).Ascending(x => x.B) — the type argument is written at the call
    // site, so T needs no inference at all. The tail captures the WHOLE chain, descending links included, so
    // that a multi-key chain is seen as one site and each link can be classified separately below.
    private static readonly Regex BuildersSort = new(
        @"Builders\s*<\s*(?<type>[\w]+)\s*>\s*\.Sort(?<tail>(?:\s*\.(?:Ascending|Descending)\s*\(\s*\w+\s*=>\s*\w+\.\w+\s*\))+)",
        RegexOptions.Compiled);

    // .SortBy(x => x.A).ThenBy(x => x.B) — the fluent form. T is NOT written here; it comes from the
    // enclosing repository class, which is why AmbientEntityTypes exists.
    private static readonly Regex FluentSort = new(
        @"\.SortBy(?<firstDir>Descending)?\s*\(\s*\w+\s*=>\s*\w+\.(?<first>\w+)\s*\)(?<tail>(?:\s*\.ThenBy(?:Descending)?\s*\(\s*\w+\s*=>\s*\w+\.\w+\s*\))*)",
        RegexOptions.Compiled);

    private static readonly Regex SortLink = new(
        @"\.(?<dir>Ascending|Descending|ThenByDescending|ThenBy)\s*\(\s*\w+\s*=>\s*\w+\.(?<key>\w+)\s*\)",
        RegexOptions.Compiled);

    private static readonly Regex DateTimeOffsetProperty = new(
        @"\b(?:public|internal|protected)\s+(?:virtual\s+|required\s+|override\s+|static\s+)*DateTimeOffset\??\s+(?<name>\w+)\s*(?:\{|=>)",
        RegexOptions.Compiled);

    // class Foo : Bar<Baz>, IQux  — captures the name and the raw base list, for inheritance walking.
    private static readonly Regex TypeDeclaration = new(
        @"\b(?:class|record)\s+(?<name>\w+)(?<generic><[^>{]*>)?(?<bases>\s*:\s*[^{]+)?",
        RegexOptions.Compiled);

    private static readonly Regex BlockComment = new(@"/\*.*?\*/", RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex GenericBinding = new(
        @"(?:TenantRepository|MongoRepository|IMongoCollection|BaseRepository)\s*<\s*(?<type>\w+)\s*>",
        RegexOptions.Compiled);

    /// <summary>Every DateTimeOffset property declared on a type, INCLUDING inherited ones.</summary>
    internal static Dictionary<string, HashSet<string>> DateTimeOffsetPropertiesByType(IEnumerable<string> files)
    {
        var declared = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var bases = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var path in files)
        {
            var source = StripFullLineComments(File.ReadAllText(path));
            foreach (var block in EnumerateTypeBlocks(source))
            {
                var set = declared.TryGetValue(block.Name, out var existing)
                    ? existing
                    : declared[block.Name] = new HashSet<string>(StringComparer.Ordinal);

                foreach (Match p in DateTimeOffsetProperty.Matches(block.Body))
                {
                    set.Add(p.Groups["name"].Value);
                }

                if (block.Bases.Count > 0)
                {
                    bases[block.Name] = block.Bases;
                }
            }
        }

        // Flatten inheritance: BaseEntity.CreatedAt is a DateTimeOffset and TaskItem never redeclares it, so
        // without this walk every `SortBy(x => x.CreatedAt)` on a derived entity would go unseen.
        var resolved = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var name in declared.Keys)
        {
            var acc = new HashSet<string>(StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var queue = new Queue<string>();
            queue.Enqueue(name);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!seen.Add(current))
                {
                    continue;
                }

                if (declared.TryGetValue(current, out var own))
                {
                    acc.UnionWith(own);
                }

                if (bases.TryGetValue(current, out var parents))
                {
                    foreach (var parent in parents)
                    {
                        queue.Enqueue(parent);
                    }
                }
            }

            resolved[name] = acc;
        }

        return resolved;
    }

    /// <summary>Every ASCENDING sort key found in <paramref name="files"/>, with its entity type resolved.</summary>
    internal static List<SortSite> AscendingSortSites(IEnumerable<string> files, string root)
    {
        var sites = new List<SortSite>();

        foreach (var path in files)
        {
            var source = StripFullLineComments(File.ReadAllText(path));
            var relative = Path.GetRelativePath(root, path);
            var ambient = AmbientEntityTypesByPosition(source);

            foreach (Match m in BuildersSort.Matches(source))
            {
                var type = m.Groups["type"].Value;
                foreach (Match link in SortLink.Matches(m.Groups["tail"].Value))
                {
                    if (link.Groups["dir"].Value is "Ascending" or "ThenBy")
                    {
                        sites.Add(new SortSite(relative, LineOf(source, m.Index), type, link.Groups["key"].Value, "Builders<T>.Sort.Ascending"));
                    }
                }
            }

            foreach (Match m in FluentSort.Matches(source))
            {
                var line = LineOf(source, m.Index);
                var keys = new List<string>();
                if (!m.Groups["firstDir"].Success)
                {
                    keys.Add(m.Groups["first"].Value);
                }

                foreach (Match link in SortLink.Matches(m.Groups["tail"].Value))
                {
                    if (link.Groups["dir"].Value == "ThenBy")
                    {
                        keys.Add(link.Groups["key"].Value);
                    }
                }

                // The fluent form names no type, so every entity the file binds a collection to is a
                // candidate. A file with one repository class has exactly one; the reporting below names
                // whichever candidate actually declares the key as a DateTimeOffset.
                var candidates = EnclosingBindings(ambient, m.Index);
                foreach (var key in keys)
                {
                    foreach (var type in candidates)
                    {
                        sites.Add(new SortSite(relative, line, type, key, ".SortBy"));
                    }
                }
            }
        }

        return sites;
    }


    internal sealed record SortChain(string RelativePath, int Line, string EntityType, IReadOnlyList<string> Keys);

    /// <summary>
    /// Every server-side sort chain, with ALL of its keys REGARDLESS OF DIRECTION.
    ///
    /// <para>Direction is irrelevant to the parallel-arrays failure: the server rejects two array-valued sort
    /// keys whether they ascend or descend. This is also where the THIRD blind spot of the original regex is
    /// closed — it understood <c>.SortBy(...).ThenBy(...)</c> but not the multi-key builder chain
    /// <c>Sort.Ascending(a).Ascending(b)</c>. There are 11 of those in the tree; none carries two date keys
    /// today, which is precisely the kind of "true for now" that a guard exists to keep true.</para>
    /// </summary>
    internal static List<SortChain> AllSortChains(IEnumerable<string> files, string root)
    {
        var chains = new List<SortChain>();

        foreach (var path in files)
        {
            var source = StripFullLineComments(File.ReadAllText(path));
            var relative = Path.GetRelativePath(root, path);
            var bindings = AmbientEntityTypesByPosition(source);

            foreach (Match m in BuildersSort.Matches(source))
            {
                var keys = SortLink.Matches(m.Groups["tail"].Value).Select(l => l.Groups["key"].Value).ToList();
                chains.Add(new SortChain(relative, LineOf(source, m.Index), m.Groups["type"].Value, keys));
            }

            foreach (Match m in FluentSort.Matches(source))
            {
                var keys = new List<string> { m.Groups["first"].Value };
                keys.AddRange(SortLink.Matches(m.Groups["tail"].Value).Select(l => l.Groups["key"].Value));
                chains.Add(new SortChain(
                    relative,
                    LineOf(source, m.Index),
                    EnclosingBindings(bindings, m.Index)[0],
                    keys));
            }
        }

        return chains;
    }

    // The fluent .SortBy form names no entity type, so it has to be inferred from the repository the call
    // sits in: `TenantRepository<TaskItem>` on the class, or an `IMongoCollection<T>` field. Bindings are
    // recorded WITH their offset so a file holding a dozen repository classes (TaskRepositories.cs holds
    // eleven) attributes each sort to the class it is actually written in, not to whichever binding happened
    // to appear first in the file.
    private static List<(int Index, string Type)> AmbientEntityTypesByPosition(string source)
        => GenericBinding.Matches(source)
            .Select(m => (m.Index, m.Groups["type"].Value))
            .OrderBy(x => x.Index)
            .ToList();

    private static IReadOnlyList<string> EnclosingBindings(List<(int Index, string Type)> bindings, int at)
    {
        // The binding that opens the enclosing class is the last one declared before the call site.
        var nearest = bindings.Where(b => b.Index < at).Select(b => b.Type).LastOrDefault();
        return nearest is null ? ["<unresolved>"] : [nearest];
    }

    private static int LineOf(string source, int index)
        => source.Take(index).Count(c => c == '\n') + 1;

    private sealed record TypeBlock(string Name, List<string> Bases, string Body);

    // Brace-matched extraction, so a property declared on TaskItem is never credited to the class above it.
    private static IEnumerable<TypeBlock> EnumerateTypeBlocks(string source)
    {
        foreach (Match decl in TypeDeclaration.Matches(source))
        {
            var open = source.IndexOf('{', decl.Index + decl.Length - 1);
            if (open < 0)
            {
                continue;
            }

            var depth = 0;
            var end = -1;
            for (var i = open; i < source.Length; i++)
            {
                if (source[i] == '{')
                {
                    depth++;
                }
                else if (source[i] == '}' && --depth == 0)
                {
                    end = i;
                    break;
                }
            }

            if (end < 0)
            {
                continue;
            }

            var bases = new List<string>();
            if (decl.Groups["bases"].Success)
            {
                foreach (Match b in Regex.Matches(decl.Groups["bases"].Value, @"\b(?<name>\w+)"))
                {
                    bases.Add(b.Groups["name"].Value);
                }
            }

            yield return new TypeBlock(decl.Groups["name"].Value, bases, source[open..end]);
        }
    }

    /// <summary>
    /// Removes whole-line <c>//</c> comments AND <c>/* … */</c> blocks before scanning.
    ///
    /// <para>⚠ THE BLOCK-COMMENT HALF IS NOT TIDINESS. The comments that document this very rule quote the
    /// forbidden chains verbatim — <c>TaskItemRepository.ByDueDate</c> exists precisely to say "do not put
    /// <c>SortBy(x =&gt; x.DueAt)</c> back", and the first run of the extended guard duly reported that
    /// sentence as a violation of itself. A guard that cannot read prose about itself teaches people to stop
    /// writing the prose.</para>
    ///
    /// <para>Line COUNT is preserved: every removed construct leaves its newlines behind, so the line numbers
    /// this scanner reports still point at the real source.</para>
    ///
    /// <para>A <c>//</c> inside a string literal (connection strings) is left alone, because only lines that
    /// START with the marker are dropped.</para>
    /// </summary>
    internal static string StripFullLineComments(string source)
    {
        var withoutBlocks = BlockComment.Replace(
            source,
            m => new string('\n', m.Value.Count(c => c == '\n')));

        var builder = new StringBuilder(withoutBlocks.Length);
        foreach (var line in withoutBlocks.Split('\n'))
        {
            builder.Append(line.TrimStart().StartsWith("//", StringComparison.Ordinal) ? string.Empty : line).Append('\n');
        }

        return builder.ToString();
    }

    internal static List<string> ProductionSources(string servicesRoot)
        => Directory
            .EnumerateFiles(servicesRoot, "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}tests{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToList();

    internal static List<string> AllSources(string servicesRoot)
        => Directory
            .EnumerateFiles(servicesRoot, "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToList();
}
