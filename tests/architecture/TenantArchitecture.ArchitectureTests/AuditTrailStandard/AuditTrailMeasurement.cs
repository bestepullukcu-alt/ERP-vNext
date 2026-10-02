using System.Text.RegularExpressions;

namespace TenantArchitecture.ArchitectureTests.AuditTrailStandard;

/*
 * THE MEASUREMENT — one definition, used by every test in AuditTrailStandardTests AND by the inventory table.
 * There is no second count anywhere: the inventory document quotes the table this class prints.
 *
 * DEFINITION OF A WRITE COMMAND (rule file §2):
 *   a non-abstract class / record / struct under `services/<svc>/src` that implements MediatR's `IRequest`
 *   (directly, or through an interface declared in the same service that derives from it) and is NOT a query.
 *   "Query" is production's own definition, copied from Platform `AuditBehavior.IsQueryRequest`:
 *   the type name ends with `Query`, or its namespace contains `.Queries`.
 *
 *   THE NAME RULE (CT decision 2026-10-02, rule file §2): in the services listed in `MeasuredByName` — and ONLY
 *   there — every non-abstract type whose name ends with `Command` is ALSO a write command, MediatR or not. Those
 *   services dispatch commands through application services, so the MediatR rule alone saw none of them. Such a
 *   command has no `IRequestHandler`, so it can be credited by a marker but never by a writer.
 *
 * EVIDENCE THAT A COMMAND IS AUDITED — all three are read from production source:
 *   marker    the command type carries the trail's marker interface(s)            (ledger: tür = işaret)
 *   writer    the command's handler names the trail's writer type                  (ledger: tür = yazıcı)
 *   indirect  the handler names type X, and X (or an implementer of X) names the writer — DECLARED in the
 *             ledger, then proven here; never inferred, because a handler that merely touches a broad service
 *             would otherwise be credited with an audit that another method of that service performs.
 */
internal sealed record CommandFinding(
    SourceType Command,
    IReadOnlyList<TrailDeclaration> Evidence,
    bool HasHandler)
{
    public string Name => Command.Name;
    public bool IsAudited => Evidence.Any(trail => trail.IsAccepted);
    public bool HasCandidateOnly => !IsAudited && Evidence.Count > 0;
}

internal sealed class ServiceMeasurement
{
    public required string Service { get; init; }
    public required IReadOnlyList<SourceType> Types { get; init; }
    public required IReadOnlyList<CommandFinding> Commands { get; init; }
    public required IReadOnlyList<string> DuplicateCommandNames { get; init; }
    public required IReadOnlyList<string> NonMediatRCommandNames { get; init; }
    public required IReadOnlyList<string> UnprovenIndirect { get; init; }
    public AuditLedger? Ledger { get; init; }
}

internal static class AuditTrailMeasurement
{
    public const string LedgerDirectory = "tests/architecture/audit-ledger";
    public const string RuleFile = ".antigravity/rules/audit-trail-standard.md";

    /// <summary>Services where a type named <c>*Command</c> is a write command whether or not it is a MediatR request.</summary>
    public static readonly IReadOnlySet<string> MeasuredByName = new HashSet<string>(StringComparer.Ordinal)
    {
        "Diten.ManagementGovernanceService",
        "Diten.PvgService"
    };

    private static readonly Lazy<IReadOnlyList<ServiceMeasurement>> Cached = new(() => MeasureAll(FindRepoRoot()));

    public static IReadOnlyList<ServiceMeasurement> All => Cached.Value;

    /// <summary>Exception classes are defined by the RULE file, not by this test: a table row starting `| İ&lt;n&gt; |`.</summary>
    public static IReadOnlyList<string> ExceptionClassesFromTheRule()
    {
        var path = Path.Combine(FindRepoRoot(), RuleFile);
        if (!File.Exists(path))
        {
            return [];
        }

        return Regex.Matches(File.ReadAllText(path), @"^\|\s*(İ\d+)\s*\|", RegexOptions.Multiline)
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    public static IReadOnlyList<string> LedgerFilesOnDisk()
    {
        var directory = Path.Combine(FindRepoRoot(), LedgerDirectory);
        return Directory.Exists(directory)
            ? Directory.GetFiles(directory, "Diten.*.md").Select(file => Path.GetFileNameWithoutExtension(file)).OrderBy(x => x, StringComparer.Ordinal).ToList()
            : [];
    }

    private static IReadOnlyList<ServiceMeasurement> MeasureAll(string repoRoot)
    {
        var servicesRoot = Path.Combine(repoRoot, "services");
        return Directory.GetDirectories(servicesRoot)
            .Where(directory => Directory.Exists(Path.Combine(directory, "src")))
            .OrderBy(directory => directory, StringComparer.Ordinal)
            .Select(directory => Measure(repoRoot, Path.GetFileName(directory)!))
            .ToList();
    }

    public static ServiceMeasurement Measure(string repoRoot, string service)
    {
        var types = CSharpSourceScanner.ScanDirectory(repoRoot, Path.Combine(repoRoot, "services", service, "src"));

        var ledgerRelative = $"{LedgerDirectory}/{service}.md";
        var ledgerPath = Path.Combine(repoRoot, ledgerRelative);
        var ledger = File.Exists(ledgerPath)
            ? AuditLedger.Parse(service, ledgerRelative, File.ReadAllText(ledgerPath))
            : null;

        // Interfaces of this service that are MediatR requests by inheritance (e.g. `ICatalogCommand<T> : IRequest<T>`).
        var requestInterfaces = new HashSet<string>(StringComparer.Ordinal) { "IRequest", "IBaseRequest" };
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var type in types.Where(t => t.Kind == "interface"))
            {
                if (!requestInterfaces.Contains(type.Name) && type.BaseNames.Any(requestInterfaces.Contains))
                {
                    requestInterfaces.Add(type.Name);
                    changed = true;
                }
            }
        }

        bool IsConcrete(SourceType t) => t.Kind is "class" or "record" or "struct" && !t.IsAbstract;
        bool IsRequest(SourceType t) => IsConcrete(t) && t.BaseNames.Any(requestInterfaces.Contains);
        bool IsQuery(SourceType t) => t.Name.EndsWith("Query", StringComparison.Ordinal) || t.Namespace.Contains(".Queries", StringComparison.Ordinal);

        bool IsCommandByName(SourceType t) => IsConcrete(t) && t.Name.EndsWith("Command", StringComparison.Ordinal) && !IsRequest(t);

        var commands = types.Where(t => IsRequest(t) && !IsQuery(t)).ToList();

        if (MeasuredByName.Contains(service))
        {
            // A contract record that shares its name with a MediatR command IS that command's payload (measured in
            // ManagementGovernance: `Modules/Dws/DwsContracts.cs` vs `Features/Dws/Commands/`), not a second command.
            var mediatRNames = commands.Select(c => c.Name).ToHashSet(StringComparer.Ordinal);
            commands.AddRange(types.Where(t => IsCommandByName(t) && !mediatRNames.Contains(t.Name)));
        }

        var duplicates = commands.GroupBy(c => c.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key} ({string.Join(", ", group.Select(c => c.RelativePath))})")
            .ToList();

        var nonMediatR = types
            .Where(IsCommandByName)
            .Select(t => t.Name)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        // command name → the text of every handler declared for it
        var handlerBodies = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var type in types.Where(t => t.Kind is "class" or "record"))
        {
            foreach (var baseType in type.Bases.Where(b => CSharpSourceScanner.SimpleName(b) == "IRequestHandler"))
            {
                var handled = CSharpSourceScanner.FirstGenericArgument(baseType);
                if (handled is null)
                {
                    continue;
                }

                if (!handlerBodies.TryGetValue(handled, out var list))
                {
                    handlerBodies[handled] = list = [];
                }

                list.Add(type.Body);
            }
        }

        var trails = ledger?.Trails ?? [];
        var unproven = new List<string>();
        var findings = new List<CommandFinding>();

        foreach (var command in commands)
        {
            var handlerText = handlerBodies.TryGetValue(command.Name, out var bodies) ? string.Join("\n", bodies) : string.Empty;
            var baseNames = command.BaseNames.ToHashSet(StringComparer.Ordinal);
            var evidence = new List<TrailDeclaration>();

            foreach (var trail in trails)
            {
                var found = trail.Kind switch
                {
                    TrailKind.Marker => trail.Required.All(baseNames.Contains) && !trail.Forbidden.Any(baseNames.Contains),
                    TrailKind.Writer => handlerText.Length > 0
                                        && trail.Required.All(token => CSharpSourceScanner.ContainsIdentifier(handlerText, token))
                                        && !trail.Forbidden.Any(token => CSharpSourceScanner.ContainsIdentifier(handlerText, token)),
                    _ => false
                };

                if (found)
                {
                    evidence.Add(trail);
                }
            }

            foreach (var declaration in (ledger?.Indirect ?? []).Where(d => d.Command == command.Name))
            {
                var trail = trails.FirstOrDefault(t => t.Name == declaration.Trail && t.Kind == TrailKind.Writer);
                var handlerNamesVia = handlerText.Length > 0 && CSharpSourceScanner.ContainsIdentifier(handlerText, declaration.Via);
                var viaText = string.Join(
                    "\n",
                    types.Where(t => t.Name == declaration.Via || t.BaseNames.Contains(declaration.Via)).Select(t => t.Body));
                var viaNamesWriter = trail is not null
                                     && viaText.Length > 0
                                     && trail.Required.All(token => CSharpSourceScanner.ContainsIdentifier(viaText, token));

                if (trail is not null && handlerNamesVia && viaNamesWriter)
                {
                    if (!evidence.Contains(trail))
                    {
                        evidence.Add(trail);
                    }
                }
                else
                {
                    unproven.Add(
                        $"{declaration.Command} → {declaration.Trail} üzerinden {declaration.Via}: "
                        + (trail is null ? "iz '## İzler' tablosunda 'yazıcı' olarak yok"
                            : !handlerNamesVia ? "handler bu türü anmıyor"
                            : "bu tür (ve uygulayanları) izin belirtecini anmıyor"));
                }
            }

            findings.Add(new CommandFinding(command, evidence, handlerText.Length > 0));
        }

        return new ServiceMeasurement
        {
            Service = service,
            Types = types,
            Commands = findings.OrderBy(f => f.Name, StringComparer.Ordinal).ToList(),
            DuplicateCommandNames = duplicates,
            NonMediatRCommandNames = nonMediatR,
            UnprovenIndirect = unproven,
            Ledger = ledger
        };
    }

    public static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Repo root not found (no AGENTS.md above the test binary).");
    }
}
