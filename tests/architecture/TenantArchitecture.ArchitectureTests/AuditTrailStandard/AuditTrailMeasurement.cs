using System.Text.RegularExpressions;

namespace TenantArchitecture.ArchitectureTests.AuditTrailStandard;

/*
 * THE MEASUREMENT — one definition, used by every test in AuditTrailStandardTests AND by the inventory table.
 * There is no second count anywhere: the inventory document quotes the table this class prints.
 *
 * DEFINITION OF A WRITE COMMAND (rule file §2):
 *   a non-abstract class / record / struct under `services/<svc>/src` that implements MediatR's `IRequest`
 *   (directly, or through an interface declared in the same service or in Diten.Building.Blocks that derives from
 *   it) and is NOT a query. A QUERY is: name ends with `Query`, or namespace contains `.Queries` — production's own
 *   definition (Platform `AuditBehavior.IsQueryRequest`) — WITH ONE DIFFERENCE: a type NAMED `*Command` is a
 *   command wherever it lives. The name beats the namespace here because the alternative hides a write: production
 *   would skip the audit of a `*Command` filed under `.Queries`, and this measure would not even list it. Such a
 *   type is reported separately (`CommandsFiledAsQueries`).
 *
 *   THE NAME RULE (CT decision 2026-10-02, rule file §2): in the services listed in `MeasuredByName` — and ONLY
 *   there — every non-abstract type whose name ends with `Command` is ALSO a write command, MediatR or not.
 *
 * EVIDENCE THAT A COMMAND IS AUDITED — all three are read from production source:
 *   marker    the command type carries the trail's marker interface(s), directly or through an interface that
 *             derives from it                                                       (ledger: tür = işaret)
 *   writer    the command's handler CALLS the trail's write member `Type.Method`    (ledger: tür = yazıcı)
 *   indirect  the handler names type X, and X (or an implementer of X) calls the write member — DECLARED in the
 *             ledger, then proven here; never inferred.
 *
 * ⚠ WHY A WRITER IS `Type.Method` AND NOT `Type`. The first version credited any handler that NAMED the writer's
 * type. An enum (`TaskTransitionKind`) or a repository with read methods (`IWorkflowTransitionLogRepository`) is
 * named by handlers that write nothing. A call is still text — the handler could call it on a dead branch — but a
 * handler that only reads no longer passes.
 */
internal sealed record CommandFinding(
    SourceType Command,
    IReadOnlyList<TrailDeclaration> Evidence,
    bool HasHandler,
    bool IsMediatR)
{
    public string Name => Command.Name;
    public bool IsAudited => Evidence.Any(trail => trail.IsAccepted);
    public bool HasCandidateOnly => !IsAudited && Evidence.Count > 0;
}

internal sealed class ServiceMeasurement
{
    public required string Service { get; init; }
    public required IReadOnlyList<SourceType> Types { get; init; }

    /// <summary>The service's own types plus Diten.Building.Blocks: a shared marker or behavior lives there (K4).</summary>
    public required IReadOnlyList<SourceType> TypesWithBuildingBlocks { get; init; }
    public required IReadOnlyList<CommandFinding> Commands { get; init; }
    public required IReadOnlyList<SourceType> Queries { get; init; }
    public required IReadOnlyList<string> DuplicateCommandNames { get; init; }
    public required IReadOnlyList<string> NonMediatRCommandNames { get; init; }
    public required IReadOnlyList<string> UnprovenIndirect { get; init; }
    public required IReadOnlyList<string> CommandsFiledAsQueries { get; init; }
    public required IReadOnlyList<string> UnresolvedHandlers { get; init; }
    public required IReadOnlyList<string> WritingQueryHandlers { get; init; }
    public AuditLedger? Ledger { get; init; }
}

internal static class AuditTrailMeasurement
{
    public const string LedgerDirectory = "tests/architecture/audit-ledger";
    public const string RuleFile = ".antigravity/rules/audit-trail-standard.md";
    public const string BuildingBlocks = "Diten.Building.Blocks";

    /// <summary>Services where a type named <c>*Command</c> is a write command whether or not it is a MediatR request.</summary>
    public static readonly IReadOnlySet<string> MeasuredByName = new HashSet<string>(StringComparer.Ordinal)
    {
        "Diten.ManagementGovernanceService",
        "Diten.PvgService"
    };

    /// <summary>
    /// What "this handler writes" looks like in text: a call on something whose name says repository / store /
    /// collection, to a method whose name starts with a write verb. Used ONLY for query handlers (the query rule
    /// hides them from the command lists). Deliberately narrow: `_notifier.SendAsync` or `list.Add` are not it.
    /// </summary>
    private static readonly Regex RepositoryWriteCall = new(
        @"\b_?\w*(?:[Rr]epository|[Rr]epo|[Ss]tore|[Cc]ollection)\w*\s*\.\s*(?:Create|Update|Delete|Insert|Replace|Upsert|Save|Remove|Append|Add|Ensure|Set)\w*\s*\(",
        RegexOptions.Compiled);

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

    /// <summary>
    /// The name fragments Platform's <c>AuditBehavior</c> excludes from auditing, READ FROM PRODUCTION
    /// (<c>AuditBehaviorOptions.AutoExcludedRequestNameFragments</c>) — a marked command whose name contains one is
    /// skipped at runtime although it carries the marker.
    /// </summary>
    public static IReadOnlyList<string> PlatformAutoExcludedNameFragments()
    {
        var path = Path.Combine(FindRepoRoot(), "services", "Diten.Platform", "src", "Diten.Platform.Application",
            "Contracts", "Audit", "AuditBehaviorOptions.cs");
        if (!File.Exists(path))
        {
            return [];
        }

        var source = File.ReadAllText(path);
        var start = source.IndexOf("AutoExcludedRequestNameFragments { get; }", StringComparison.Ordinal);
        if (start < 0)
        {
            return [];
        }

        var open = source.IndexOf('[', start);
        var close = open < 0 ? -1 : source.IndexOf(']', open);
        return close < 0
            ? []
            : Regex.Matches(source[open..close], "\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToList();
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
        var buildingBlocks = CSharpSourceScanner.ScanDirectory(repoRoot, Path.Combine(servicesRoot, BuildingBlocks, "src"));
        return Directory.GetDirectories(servicesRoot)
            .Where(directory => Directory.Exists(Path.Combine(directory, "src")))
            .OrderBy(directory => directory, StringComparer.Ordinal)
            .Select(directory => Measure(repoRoot, Path.GetFileName(directory)!, buildingBlocks))
            .ToList();
    }

    public static ServiceMeasurement Measure(string repoRoot, string service, IReadOnlyList<SourceType> buildingBlocks)
    {
        var types = CSharpSourceScanner.ScanDirectory(repoRoot, Path.Combine(repoRoot, "services", service, "src"));
        var pool = service == BuildingBlocks ? types : types.Concat(buildingBlocks).ToList();

        var ledgerRelative = $"{LedgerDirectory}/{service}.md";
        var ledgerPath = Path.Combine(repoRoot, ledgerRelative);
        var ledger = File.Exists(ledgerPath)
            ? AuditLedger.Parse(service, ledgerRelative, File.ReadAllText(ledgerPath))
            : null;

        // interface name → the interfaces it derives from (service + building blocks)
        var interfaceBases = pool.Where(t => t.Kind == "interface")
            .GroupBy(t => t.Name, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.SelectMany(t => t.BaseNames).ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);

        HashSet<string> InterfaceClosure(SourceType type)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var queue = new Queue<string>(type.BaseNames);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (seen.Add(current) && interfaceBases.TryGetValue(current, out var parents))
                {
                    foreach (var parent in parents)
                    {
                        queue.Enqueue(parent);
                    }
                }
            }

            return seen;
        }

        bool IsConcrete(SourceType t) => t.Kind is "class" or "record" or "struct" && !t.IsAbstract;
        bool IsRequest(SourceType t) => IsConcrete(t) && InterfaceClosure(t).Overlaps(["IRequest", "IBaseRequest"]);
        bool NamedCommand(SourceType t) => t.Name.EndsWith("Command", StringComparison.Ordinal);
        bool ProductionSaysQuery(SourceType t) =>
            t.Name.EndsWith("Query", StringComparison.Ordinal) || t.Namespace.Contains(".Queries", StringComparison.Ordinal);
        bool IsQuery(SourceType t) => ProductionSaysQuery(t) && !NamedCommand(t);
        bool IsCommandByName(SourceType t) => IsConcrete(t) && NamedCommand(t) && !IsRequest(t);

        var requests = types.Where(IsRequest).ToList();
        var mediatRCommands = requests.Where(t => !IsQuery(t)).ToList();
        var queries = requests.Where(IsQuery).ToList();
        var commands = mediatRCommands.ToList();

        if (MeasuredByName.Contains(service))
        {
            // A contract record that shares its name with a MediatR command IS that command's payload (measured in
            // ManagementGovernance: `Modules/Dws/DwsContracts.cs` vs `Features/Dws/Commands/`), not a second command.
            var mediatRNames = commands.Select(c => c.Name).ToHashSet(StringComparer.Ordinal);
            commands.AddRange(types.Where(t => IsCommandByName(t) && !mediatRNames.Contains(t.Name)));
        }

        var filedAsQueries = mediatRCommands
            .Where(t => NamedCommand(t) && ProductionSaysQuery(t))
            .Select(t => $"{t.Name} (ad alanı {t.Namespace}; {t.RelativePath})")
            .ToList();

        var duplicates = commands.GroupBy(c => c.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key} ({string.Join(", ", group.Select(c => c.RelativePath))})")
            .ToList();

        var nonMediatR = types.Where(IsCommandByName).Select(t => t.Name).OrderBy(x => x, StringComparer.Ordinal).ToList();

        // request name → the text of every handler declared for it; and every handler whose request is unknown
        var requestNames = requests.Select(r => r.Name).ToHashSet(StringComparer.Ordinal);
        var handlerBodies = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var unresolved = new List<string>();
        foreach (var type in types.Where(t => t.Kind is "class" or "record"))
        {
            foreach (var baseType in type.Bases.Where(b => CSharpSourceScanner.SimpleName(b) == "IRequestHandler"))
            {
                var handled = CSharpSourceScanner.FirstGenericArgument(baseType);
                if (handled is null)
                {
                    unresolved.Add($"{type.Name}: IRequestHandler'ın ilk tip argümanı okunamadı ({type.RelativePath})");
                    continue;
                }

                if (!requestNames.Contains(handled))
                {
                    unresolved.Add($"{type.Name} → {handled} ({type.RelativePath})");
                }

                if (!handlerBodies.TryGetValue(handled, out var list))
                {
                    handlerBodies[handled] = list = [];
                }

                list.Add(type.Body);
            }
        }

        var writingQueryHandlers = queries
            .Where(q => handlerBodies.TryGetValue(q.Name, out var bodies) && bodies.Any(RepositoryWriteCall.IsMatch))
            .Select(q => q.Name)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        var trails = ledger?.Trails ?? [];
        var unproven = new List<string>();
        var findings = new List<CommandFinding>();

        foreach (var command in commands)
        {
            var handlerText = handlerBodies.TryGetValue(command.Name, out var bodies) ? string.Join("\n", bodies) : string.Empty;
            var closure = InterfaceClosure(command);
            var evidence = new List<TrailDeclaration>();

            foreach (var trail in trails)
            {
                var found = trail.Kind switch
                {
                    TrailKind.Marker => trail.RequiredGroups.All(group => group.Any(closure.Contains))
                                        && !trail.Forbidden.Any(closure.Contains),
                    TrailKind.Writer => handlerText.Length > 0
                                        && trail.RequiredGroups.All(group => group.Any(token => CallsWriteMember(handlerText, token, pool)))
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
                var viaCallsWriter = trail is not null
                                     && viaText.Length > 0
                                     && trail.RequiredGroups.All(group => group.Any(token => CallsWriteMember(viaText, token, pool)));

                if (trail is not null && handlerNamesVia && viaCallsWriter)
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
                            : "bu tür (ve uygulayanları) izin yazma üyesini çağırmıyor"));
                }
            }

            findings.Add(new CommandFinding(command, evidence, handlerText.Length > 0, IsRequest(command)));
        }

        return new ServiceMeasurement
        {
            Service = service,
            Types = types,
            TypesWithBuildingBlocks = pool,
            Commands = findings.OrderBy(f => f.Name, StringComparer.Ordinal).ToList(),
            Queries = queries,
            DuplicateCommandNames = duplicates,
            NonMediatRCommandNames = nonMediatR,
            UnprovenIndirect = unproven,
            CommandsFiledAsQueries = filedAsQueries,
            UnresolvedHandlers = unresolved,
            WritingQueryHandlers = writingQueryHandlers,
            Ledger = ledger
        };
    }

    /// <summary>
    /// Does <paramref name="text"/> CALL the write member <c>Type.Method</c>?
    /// <list type="bullet">
    /// <item><c>Type.Method(</c> written out — a static helper;</item>
    /// <item>a variable DECLARED with that type (<c>Type name</c>: field, parameter, primary-constructor parameter)
    /// and <c>name.Method(</c> — the injected-dependency case, and the ONLY form accepted for an interface;</item>
    /// <item>for a CLASS nobody declares a variable of (an entity reached through <c>var</c>): any <c>.Method(</c>,
    /// but ONLY when that class is the one type in the service that declares a method of that name. This is the weak
    /// form — it trusts the method name — so the name has to belong to the class alone: <c>.CancelAsync(</c> or
    /// <c>.TransitionAsync(</c>, which many types declare, proves nothing about which one was called (measured
    /// 2026-10-02: a handler calling <c>_approvals.CancelAsync(</c> was credited with the workflow log). It is
    /// refused for interfaces outright, where it would credit any handler calling <c>.CreateAsync(</c> on anything.</item>
    /// </list>
    /// </summary>
    public static bool CallsWriteMember(string text, string token, IReadOnlyList<SourceType> pool)
    {
        var dot = token.LastIndexOf('.');
        if (dot <= 0 || dot == token.Length - 1)
        {
            return false;
        }

        var typeName = token[..dot];
        var method = Regex.Escape(token[(dot + 1)..]);

        // A call with EXPLICIT type arguments is the same call: `_transaction.ExecuteAsync<Response<NoContent>>(…)`.
        // Measured 2026-10-02 (WP-PLATFORM-AUDIT-INTX-01): eight Platform handlers call the coordinator that way and
        // were read as "calls nothing". Only the two typed forms accept it; the weak (name-only) form below does not.
        const string TypeArguments = @"(?:<[^;(){}]*>)?";

        if (Regex.IsMatch(text, $@"(?<![\w.]){Regex.Escape(typeName)}\s*\.\s*{method}\s*{TypeArguments}\s*\("))
        {
            return true;
        }

        var variables = Regex.Matches(text, $@"(?<![\w.]){Regex.Escape(typeName)}\??\s+(_?[A-Za-z]\w*)\b")
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (variables.Count > 0)
        {
            return variables.Any(variable => Regex.IsMatch(text, $@"(?<![\w.]){Regex.Escape(variable)}\s*[?!]?\s*\.\s*{method}\s*{TypeArguments}\s*\("));
        }

        var isInterface = pool.Any(t => t.Name == typeName && t.Kind == "interface");
        if (isInterface)
        {
            return false;
        }

        // Asked once per (service pool, method): every handler of a service asks the same question about the same pool.
        var declarers = DeclarersByPool.GetOrCreateValue(pool).GetOrAdd(
            method,
            name => pool.Where(t => DeclaresMethod(t, name)).Select(t => t.Name).Distinct(StringComparer.Ordinal).ToList());
        return declarers.Count == 1
               && declarers[0] == typeName
               && Regex.IsMatch(text, $@"\.\s*{method}\s*\(");
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<IReadOnlyList<SourceType>, System.Collections.Concurrent.ConcurrentDictionary<string, List<string>>> DeclarersByPool = new();

    private static readonly HashSet<string> NotAReturnType = new(StringComparer.Ordinal)
    {
        "new", "return", "await", "throw", "else", "in", "is", "and", "or", "not", "out", "ref", "case", "when",
        "yield", "typeof", "nameof", "var", "this", "base"
    };

    /// <summary>
    /// Does the type DECLARE a method of that name (its return type in front of it, never a dot)? A call —
    /// <c>x.Method(</c>, <c>await Method(</c>, <c>return Method(</c> — is not a declaration.
    /// </summary>
    internal static bool DeclaresMethod(SourceType type, string escapedMethod) =>
        Regex.Matches(type.Body, $@"(?<![\w.])([A-Za-z_][\w.]*(?:<[^;{{}}()]*>)?[\?\]\[]*)\s+{escapedMethod}\s*(?:<[^<>()]*>)?\s*\(")
            .Any(match => !NotAReturnType.Contains(match.Groups[1].Value));

    /// <summary>The type exists and declares (or at least names) the method — a ledger token cannot point at a ghost.</summary>
    public static bool WriteMemberExists(string token, IReadOnlyList<SourceType> pool)
    {
        var dot = token.LastIndexOf('.');
        if (dot <= 0 || dot == token.Length - 1)
        {
            return false;
        }

        var method = Regex.Escape(token[(dot + 1)..]);
        return pool.Any(t => t.Name == token[..dot] && Regex.IsMatch(t.Body, $@"\b{method}\s*[<(]"));
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
