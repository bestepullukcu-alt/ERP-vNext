using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using System.Reflection;
using MediatR;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class ProductLegalEntityScopeWriteAdmissionContractTests
{
    private static readonly string[] ExactMutationCommands =
    [
        "ReserveCanonicalCodeCommand", "CreateGlobalProductDraftCommand",
        "CreateFirstGskuDraftFacadeCommand", "CreateFirstGskuDraftCommand", "UpdateGskuDraftCommand",
        "CreateLskuDraftCommand", "CreateFinishedGoodDraftCommand",
        "RequestProductAbbreviationAllocationCommand", "ApproveProductAbbreviationAllocationCommand",
        "RejectProductAbbreviationAllocationCommand", "CancelProductAbbreviationAllocationCommand",
        "InitiateProductAbbreviationCorrectionCommand", "RequestProductAbbreviationRetirementCommand",
        "ApproveProductAbbreviationRetirementCommand", "RejectProductAbbreviationRetirementCommand",
        "CreateProductLegalEntityScopePolicyCommand", "ReplaceProductLegalEntityScopePolicyCommand",
        "EndProductLegalEntityScopePolicyCommand",
        // WP-MDM-SCOPE-INT-01 (CT decision): born fenced after the LSKU / FG rounds merged.
        "CancelFinishedGoodDraftCommand", "CancelLskuDraftCommand", "RepairLskuChildAdmissionsCommand",
        // WP-MDM-ITEM-MASTER S3 (CT decision R9): the stock-behaviour writers are born fenced.
        "SetGskuStockBehaviourCommand", "SetLskuStockBehaviourCommand", "SetFinishedGoodStockBehaviourCommand",
        "AssignFinishedGoodGtinCommand",
        // MOD-0290-FU05 slice 2 (CT decision A): the approved product's brand change is born fenced.
        "ChangeGlobalProductBrandCommand"
    ];

    [Fact]
    public void Mutation_inventory_is_exactly_the_frozen_26_commands_and_has_no_unclassified_twenty_seventh_writer()
    {
        var marker = typeof(IProductLegalEntityScopeInventoryMutation);
        var applicationTypes = marker.Assembly.GetTypes();
        // WP-MDM-SCOPE-ACCEPT-01 FIX1 S8: EVERY product command namespace — the Lifecycle and Workflow ones too (before,
        // three namespaces were scanned and seventeen writers were never looked at).
        var discoveredCommands = applicationTypes
            .Where(type => type.IsClass && !type.IsAbstract
                && type.Namespace is not null && IsProductCommandNamespace(type.Namespace)
                && type.Name.EndsWith("Command", StringComparison.Ordinal)
                && typeof(IBaseRequest).IsAssignableFrom(type))
            .ToArray();
        var unclassified = discoveredCommands
            .Where(type => !marker.IsAssignableFrom(type) && !KnownUnguardedLifecycleWriters.Contains(type.Name))
            .Select(type => type.FullName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        var stale = KnownUnguardedLifecycleWriters
            .Where(name => !discoveredCommands.Any(type => type.Name == name && !marker.IsAssignableFrom(type)))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.True(stale.Length == 0,
            "These writers are guarded now (or gone): remove them from KnownUnguardedLifecycleWriters: " + string.Join(", ", stale));
        var actual = discoveredCommands
            .Where(marker.IsAssignableFrom)
            .Where(type => type.IsClass && !type.IsAbstract && marker.IsAssignableFrom(type))
            .Select(type => type.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(unclassified.Length == 0,
            "Unclassified product inventory command(s): " + string.Join(", ", unclassified));
        Assert.Equal(26, actual.Length);
        Assert.Equal(ExactMutationCommands.OrderBy(name => name, StringComparer.Ordinal), actual);
        Assert.Equal(26, ExactMutationCommands.Distinct(StringComparer.Ordinal).Count());
    }

    private static bool IsProductCommandNamespace(string ns) =>
        ns.StartsWith("Diten.MdmService.Application.Features.ProductItemSkuMaster", StringComparison.Ordinal)
        || ns.StartsWith("Diten.MdmService.Application.Features.ProductAbbreviationRegister", StringComparison.Ordinal)
        || ns.StartsWith("Diten.MdmService.Application.Features.ProductLegalEntityScopes", StringComparison.Ordinal);

    /// <summary>
    /// WP-MDM-SCOPE-ACCEPT-01 FIX1 S8 — THE KNOWN GAP, NAMED: product writers that take neither the write fence nor a writer
    /// lease. FIX2 (5): the list lives in production code (<see cref="ProductLegalEntityScopeActivationDebt"/>) — the same list
    /// that keeps activation shut — so the rule is measured on what the product runs, never on a copy. A NEW unguarded
    /// command turns this test red; a guarded one must leave the list.
    /// </summary>
    private static IReadOnlySet<string> KnownUnguardedLifecycleWriters => ProductLegalEntityScopeActivationDebt.KnownUnguardedWriters;

    /// <summary>
    /// WP-MDM-SCOPE-ACCEPT-01 FIX2 (4) — every recovery runner of the MDM service either takes the shared worker scope rule
    /// (<c>ProductIdentityBackgroundScope</c> / <c>LskuBackgroundScope</c>) or reads the fence itself; the ones that do
    /// neither are NAMED in <see cref="ProductLegalEntityScopeActivationDebt.KnownUnfencedRecoveryRunners"/> (which keeps
    /// activation shut). A new runner blind to the fence turns this red; a runner that learns the rule must leave the list.
    /// </summary>
    [Fact]
    public void Every_recovery_runner_reads_the_fence_or_is_a_named_debt()
    {
        var sourceRoot = Path.Combine(FindRepoRoot(), "services", "Diten.MdmService", "src");
        var runners = Directory.EnumerateFiles(sourceRoot, "*RecoveryRunner.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToDictionary(path => Path.GetFileNameWithoutExtension(path), path => ReadsTheFence(File.ReadAllText(path)));
        Assert.True(runners.Count >= 9, "Recovery runners not found under " + sourceRoot);

        var blind = runners.Where(runner => !runner.Value
                && !ProductLegalEntityScopeActivationDebt.KnownUnfencedRecoveryRunners.Contains(runner.Key))
            .Select(runner => runner.Key).Order(StringComparer.Ordinal).ToArray();
        var stale = ProductLegalEntityScopeActivationDebt.KnownUnfencedRecoveryRunners
            .Where(name => !runners.TryGetValue(name, out var reads) || reads)
            .Order(StringComparer.Ordinal).ToArray();

        Assert.True(blind.Length == 0, "Recovery runner(s) that read neither the scope rule nor the fence: " + string.Join(", ", blind));
        Assert.True(stale.Length == 0,
            "These runners read the fence now (or are gone): remove them from KnownUnfencedRecoveryRunners: " + string.Join(", ", stale));
    }

    /// <summary>
    /// FIX3 (13) — a runner READS the fence only when it ASKS: it calls the shared worker rule
    /// (<c>InScopeAsync(…)</c> / <c>ResolveAsync(…)</c> on a <c>ProductIdentityBackgroundScope</c> / <c>LskuBackgroundScope</c>)
    /// or tests <c>.ActiveFence is null</c> itself. Both comment forms are stripped first; naming the type (an injected
    /// field never called) is not enough.
    /// </summary>
    internal static bool ReadsTheFence(string source)
    {
        var code = System.Text.RegularExpressions.Regex.Replace(source.ReplaceLineEndings("\n"), @"/\*.*?\*/", " ",
            System.Text.RegularExpressions.RegexOptions.Singleline);
        code = System.Text.RegularExpressions.Regex.Replace(code, @"//[^\n]*", " ");
        var holdsTheRule = code.Contains("ProductIdentityBackgroundScope", StringComparison.Ordinal)
                           || code.Contains("LskuBackgroundScope", StringComparison.Ordinal);
        return code.Contains(".ActiveFence is null", StringComparison.Ordinal)
               || holdsTheRule && System.Text.RegularExpressions.Regex.IsMatch(code, @"\.InScope\w*Async\s*\(");
    }

    [Theory]
    [InlineData("var mode = await backgroundScope.ResolveAsync(ct); if (!await backgroundScope.InScopeAsync(mode, id, ct)) continue; // ProductIdentityBackgroundScope", false)]
    [InlineData("private readonly ProductIdentityBackgroundScope _scope; // never asked", false)]
    [InlineData("/* ProductIdentityBackgroundScope … await scope.InScopeAsync(mode, id, ct) */ var x = 1;", false)]
    [InlineData("// rollout.ActiveFence is null\nvar x = 1;", false)]
    [InlineData("var s = sp.GetRequiredService<ProductIdentityBackgroundScope>(); if (!await s.InScopeAsync(m, id, ct)) continue;", true)]
    [InlineData("return rollout.Mode == ProductLegalEntityScopeRolloutMode.Preparation && rollout.ActiveFence is null;", true)]
    public void A_runner_reads_the_fence_only_when_it_asks_the_rule_not_when_it_merely_names_it(string source, bool reads)
    {
        // FIX3 (13): before, a type named in a comment or an injected field never called passed as "reads the fence".
        Assert.Equal(reads, ReadsTheFence(source.Replace("\\n", "\n")));
    }

    /// <summary>
    /// WP-MDM-SCOPE-ACCEPT-01 FIX3 (6) — WHAT THIS PROVES, AND NO MORE: no MDM HTTP surface (attribute-routed controller,
    /// minimal API in Program.cs) moves the scope rollout, and no MDM source checks the rollout permission keys. So a person
    /// who holds <c>mdm.product-legal-entity-scope-rollout.activate / .rollback</c> (Auth grants them to roles a person can
    /// hold) opens nothing with them; the only transition path is the Development-only CLI (its own test). It does NOT
    /// prove the key cannot be given to a person — that is CT's decision for WP-MDM-SCOPE-INT-01 (Auth
    /// <c>NonHumanPermissionKeys</c>), when an activation path is opened. (FIX2 named this test after that larger claim.)
    /// </summary>
    [Fact]
    public void No_mdm_http_surface_moves_the_scope_rollout_and_no_source_checks_its_permission_keys()
    {
        var root = FindRepoRoot();
        var sources = Directory.EnumerateFiles(Path.Combine(root, "services", "Diten.MdmService", "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToDictionary(path => Path.GetRelativePath(root, path).Replace('\\', '/'), path => WithoutComments(File.ReadAllText(path)));
        const string Manifest = "services/Diten.MdmService/src/Diten.MdmService.Api/ModuleRegistration/ProductItemSkuMasterManifestProvider.cs";
        const string Runner = "services/Diten.MdmService/src/Diten.MdmService.Api/Services/ProductLegalEntityScopes/ProductLegalEntityScopeOperationalRunner.cs";
        const string ProgramFile = "services/Diten.MdmService/src/Diten.MdmService.Api/Program.cs";

        // The permission keys, whole or assembled from pieces ("…scope-" + "rollout.activate"), appear only in the manifest.
        var keyUsers = sources.Where(item => item.Key != Manifest && System.Text.RegularExpressions.Regex.IsMatch(item.Value,
                @"scope-rollout|rollout\.activate|rollout\.rollback|""rollout""", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            .Select(item => item.Key).ToArray();
        Assert.True(keyUsers.Length == 0, "Rollout permission key used outside the manifest: " + string.Join(", ", keyUsers));

        // No HTTP surface reaches the runner or the transition actions: no controller (any file with [Route]/[Http*] or
        // ControllerBase) and no minimal API endpoint (Map{Get,Post,Put,Patch,Delete,Methods}) anywhere.
        var http = sources.Where(item => System.Text.RegularExpressions.Regex.IsMatch(item.Value,
                @"\[(Route|Http(Get|Post|Put|Patch|Delete))\b|:\s*(Controller|ControllerBase)\b|\.Map(Get|Post|Put|Patch|Delete|Methods)\s*\("))
            .ToArray();
        Assert.NotEmpty(http);
        var moving = http.Where(item => item.Key != ProgramFile && System.Text.RegularExpressions.Regex.IsMatch(item.Value,
                @"ProductLegalEntityScopeOperationalRunner|ActivateEnforced|SuspendFailClosed|CommitTransitionAsync|AcquireFenceAsync"))
            .Select(item => item.Key).ToArray();
        Assert.True(moving.Length == 0, "HTTP surface that moves the rollout: " + string.Join(", ", moving));
        Assert.DoesNotMatch(@"\.Map(Get|Post|Put|Patch|Delete|Methods)\s*\([^;]*(rollout|activate|suspend)",
            sources[ProgramFile].ToLowerInvariant());
        Assert.True(sources.ContainsKey(Runner));
    }

    /// <summary>
    /// FIX3 (5) — the gate guards the CLI; the rollout can also be written in-process. Only the operational runner (and
    /// the repository itself) may call the rollout repository's writes that move the mode or take a fence:
    /// <c>CreateAsync</c>, <c>UpdateAsync</c>, <c>CommitTransitionAsync</c>, <c>AcquireFenceAsync</c>. Measured on every MDM
    /// source: a variable or field typed as the rollout repository, and those calls on it. (Text, not syntax tree: a call
    /// through an untyped alias would evade it — stated, not hidden.)
    /// </summary>
    [Fact]
    public void Only_the_operational_runner_creates_moves_or_fences_the_scope_rollout()
    {
        var root = FindRepoRoot();
        var allowed = new[]
        {
            "services/Diten.MdmService/src/Diten.MdmService.Api/Services/ProductLegalEntityScopes/ProductLegalEntityScopeOperationalRunner.cs",
            "services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/ProductLegalEntityScopeRolloutStateRepository.cs"
        };
        var callers = new List<string>();
        foreach (var path in Directory.EnumerateFiles(Path.Combine(root, "services", "Diten.MdmService", "src"), "*.cs", SearchOption.AllDirectories)
                     .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
        {
            var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            var code = WithoutComments(File.ReadAllText(path));
            var names = System.Text.RegularExpressions.Regex.Matches(code,
                    @"I?ProductLegalEntityScopeRolloutStateRepository\??\s+(\w+)|(\w+)\s*=\s*[^;]*GetRequiredService<I?ProductLegalEntityScopeRolloutStateRepository>")
                .Select(match => match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value)
                .Distinct().ToArray();
            if (names.Any(name => System.Text.RegularExpressions.Regex.IsMatch(code,
                    $@"\b{name}\s*\.\s*(CreateAsync|UpdateAsync|CommitTransitionAsync|AcquireFenceAsync)\s*\(")))
                callers.Add(relative);
        }

        // The runner must be found (else the scan is blind); nobody else may appear.
        Assert.Equal([allowed[0]], callers.Where(caller => caller != allowed[1]).Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void The_application_registration_resolves_a_debt_that_blocks_activation_and_cannot_be_emptied()
    {
        // FIX3 (5): measured on the real registration, not on a hand-made instance.
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        Diten.MdmService.Application.DependencyInjection.AddApplication(services);
        using var provider = Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(services);
        var debt = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetRequiredService<IProductLegalEntityScopeActivationDebt>(provider);

        Assert.True(ProductLegalEntityScopeActivationDebt.BlocksActivation(debt));
        Assert.IsAssignableFrom<System.Collections.Frozen.FrozenSet<string>>(debt.UnguardedWriters);
        Assert.IsAssignableFrom<System.Collections.Frozen.FrozenSet<string>>(debt.UnfencedRecoveryRunners);
    }

    [Fact]
    public void The_scope_hosted_services_are_registered_on_the_api_host()
    {
        // FIX3 (7, 8): the gauge worker and the startup index build exist only if the host registers them.
        var program = WithoutComments(ReadRepoFile("services/Diten.MdmService/src/Diten.MdmService.Api/Program.cs"));
        Assert.Contains("AddHostedService<ProductLegalEntityScopeHoldGaugeWorker>()", program, StringComparison.Ordinal);
        Assert.Contains("AddHostedService<ProductLegalEntityScopeProofIndexInitializer>()", program, StringComparison.Ordinal);
    }

    /// <summary>
    /// FIX4 K4 — comments removed by a small lexer that knows string and char literals (regular, verbatim, interpolated,
    /// raw): a "/*" or "//" INSIDE a string is kept as text, and no code after it is lost.
    /// </summary>
    internal static string WithoutComments(string source)
    {
        var text = source.ReplaceLineEndings("\n");
        var output = new System.Text.StringBuilder(text.Length);
        for (var i = 0; i < text.Length;)
        {
            if (text[i] == '/' && i + 1 < text.Length && text[i + 1] == '/')
            {
                while (i < text.Length && text[i] != '\n') i++;
                continue;
            }
            if (text[i] == '/' && i + 1 < text.Length && text[i + 1] == '*')
            {
                var end = text.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = end < 0 ? text.Length : end + 2;
                output.Append(' ');
                continue;
            }
            if (text[i] == '"' || text[i] == '\'' || (text[i] is '@' or '$') && i + 1 < text.Length && (text[i + 1] is '"' or '@' or '$'))
            {
                var start = i;
                var verbatim = false;
                while (i < text.Length && text[i] is '@' or '$') { verbatim |= text[i] == '@'; i++; }
                if (i >= text.Length || text[i] is not ('"' or '\'')) { output.Append(text, start, i - start); continue; }
                var quote = text[i];
                var run = 0;
                while (i + run < text.Length && text[i + run] == '"' && quote == '"') run++;
                if (run >= 3)
                {
                    var close = text.IndexOf(new string('"', run), i + run, StringComparison.Ordinal);
                    i = close < 0 ? text.Length : close + run;
                }
                else
                {
                    i++;
                    while (i < text.Length)
                    {
                        if (!verbatim && text[i] == '\\') { i += 2; continue; }
                        if (text[i] == quote)
                        {
                            if (verbatim && i + 1 < text.Length && text[i + 1] == '"') { i += 2; continue; }
                            i++;
                            break;
                        }
                        if (!verbatim && text[i] == '\n') break;
                        i++;
                    }
                }
                output.Append(text, start, Math.Min(i, text.Length) - start);
                continue;
            }
            output.Append(text[i]);
            i++;
        }
        return output.ToString();
    }

    [Theory]
    [InlineData("var a = \"/* not a comment */\"; Call();", "Call();")]
    [InlineData("var b = @\"C:\\// path\"; Call();", "Call();")]
    [InlineData("var c = '/'; /* gone */ Call(); // gone too", "Call();")]
    public void Comments_are_removed_but_never_what_a_string_holds(string source, string survives)
    {
        // FIX4 K4: before, the regex cut from a "/*" inside a string to the next "*/" — code in between vanished.
        var code = WithoutComments(source);
        Assert.Contains(survives, code, StringComparison.Ordinal);
        Assert.DoesNotContain("gone", code, StringComparison.Ordinal);
    }

    /// <summary>
    /// FIX4 K4 — the rollout can also be written through its collection directly. Only the rollout repository and the
    /// guarded Replace session (whose transaction stamps the record's UpdatedAt, its write-conflict point) write the
    /// rollout collection — typed, or as BsonDocument by its name.
    /// </summary>
    [Fact]
    public void Only_the_rollout_repository_and_the_guarded_replace_write_the_rollout_collection()
    {
        var root = FindRepoRoot();
        var allowed = new[]
        {
            "services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/ProductLegalEntityScopeGuardedWriteSession.cs",
            "services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/ProductLegalEntityScopeRolloutStateRepository.cs"
        };
        const string Writes = @"(InsertOne|InsertMany|UpdateOne|UpdateMany|FindOneAndUpdate|FindOneAndReplace|FindOneAndDelete|ReplaceOne|DeleteOne|DeleteMany|BulkWrite)(Async)?\s*\(";
        var writers = new List<string>();
        foreach (var path in Directory.EnumerateFiles(Path.Combine(root, "services", "Diten.MdmService", "src"), "*.cs", SearchOption.AllDirectories)
                     .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
        {
            var code = WithoutComments(File.ReadAllText(path));
            var collection = @"(ProductLegalEntityScopeRolloutStateRepository\.CollectionName|RolloutStates|""mdm_product_legal_entity_scope_rollout_states"")";
            var names = System.Text.RegularExpressions.Regex.Matches(code,
                    @"IMongoCollection<ProductLegalEntityScopeRolloutState>\s+(\w+)|(\w+)\s*=\s*[^;]*GetCollection<\w+>\(\s*" + collection)
                .Select(match => match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value).Distinct();
            var bound = names.Any(name => System.Text.RegularExpressions.Regex.IsMatch(code, $@"\b{name}\s*\.\s*" + Writes));
            var inline = System.Text.RegularExpressions.Regex.IsMatch(code, @"GetCollection<\w+>\(\s*" + collection + @"\s*\)\s*\.\s*" + Writes);
            if (bound || inline) writers.Add(Path.GetRelativePath(root, path).Replace('\\', '/'));
        }

        Assert.Equal(allowed, writers.Order(StringComparer.Ordinal).ToArray());
    }


    [Fact]
    public void Write_fence_behavior_is_registered_after_validation_and_before_audit_forwarding()
    {
        var source = ReadRepoFile("services/Diten.MdmService/src/Diten.MdmService.Application/DependencyInjection.cs");
        var validation = source.IndexOf("ValidationBehavior", StringComparison.Ordinal);
        var fence = source.IndexOf("ProductLegalEntityScopeWriteFenceBehavior", StringComparison.Ordinal);
        var audit = source.IndexOf("AuditForwardingBehavior", StringComparison.Ordinal);

        Assert.True(validation >= 0 && fence > validation && audit > fence,
            $"Unexpected pipeline order: validation={validation}, fence={fence}, audit={audit}");
    }

    [Fact]
    public void Guarded_repository_contract_requires_exact_authority_lease_policy_and_expected_version()
    {
        var replace = Assert.Single(
            typeof(IProductLegalEntityScopeGuardedWriteSession).GetMethods(),
            method => method.Name == nameof(IProductLegalEntityScopeGuardedWriteSession.ReplaceAsync));

        Assert.Equal(
        [
            typeof(ProductLegalEntityScopeVerifiedWriterAuthority),
            typeof(ProductLegalEntityScopeWriterLease),
            typeof(ProductLegalEntityScopePolicy),
            typeof(int),
            typeof(CancellationToken)
        ], replace.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.Equal(
            typeof(Task<ProductLegalEntityScopePolicyWriteResult>),
            replace.ReturnType);
    }

    [Fact]
    public void Verified_writer_authority_cannot_be_publicly_constructed_or_issued_by_a_caller()
    {
        var type = typeof(ProductLegalEntityScopeVerifiedWriterAuthority);

        Assert.Empty(type.GetConstructors(BindingFlags.Instance | BindingFlags.Public));
        var issue = Assert.Single(
            type.GetMethods(BindingFlags.Static | BindingFlags.NonPublic),
            method => method.Name == "IssueForegroundReplace");
        Assert.False(issue.IsPublic);
        Assert.Equal(
            "Diten.MdmService.Infrastructure",
            Assert.Single(type.Assembly.GetCustomAttributes<System.Runtime.CompilerServices.InternalsVisibleToAttribute>(),
                attribute => attribute.AssemblyName == "Diten.MdmService.Infrastructure").AssemblyName);
    }

    [Fact]
    public void Foreground_replace_authority_has_exactly_one_production_issuer_source()
    {
        var root = FindRepoRoot();
        var sourceRoot = Path.Combine(
            root,
            "services",
            "Diten.MdmService",
            "src");
        var authorityDefinition = Path.GetFullPath(Path.Combine(
            sourceRoot,
            "Diten.MdmService.Domain",
            "ValueObjects",
            "ProductLegalEntityScopeVerifiedWriterAuthority.cs"));
        var issuers = Directory.EnumerateFiles(
                sourceRoot,
                "*.cs",
                SearchOption.AllDirectories)
            .Where(path => !path.Contains(
                $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains(
                $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase))
            .Where(path => !string.Equals(
                Path.GetFullPath(path),
                authorityDefinition,
                StringComparison.OrdinalIgnoreCase))
            .Where(path => File.ReadAllText(path).Contains(
                "IssueForegroundReplace(",
                StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
        [
            "services/Diten.MdmService/src/Diten.MdmService.Infrastructure/Security/" +
            "ProductLegalEntityScopeWriterAuthorityProvider.cs"
        ], issuers);
    }

    [Fact]
    public void Guarded_persistence_owns_one_transaction_rollout_write_policy_cas_and_never_uses_replace_one()
    {
        var source = ReadRepoFile(
            "services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/ProductLegalEntityScopeGuardedWriteSession.cs")
            .ReplaceLineEndings("\n");

        Assert.Contains("session.StartTransaction", source, StringComparison.Ordinal);
        Assert.Contains("_rollouts.UpdateOneAsync(\n                session", source, StringComparison.Ordinal);
        Assert.Contains("_policyDocuments.FindOneAndUpdateAsync(\n                session", source, StringComparison.Ordinal);
        Assert.Contains("ProductLegalEntityScopeRolloutMode.Preparation", source, StringComparison.Ordinal);
        Assert.Contains("ActiveWriterLease.PreWriteStateHash", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ReplaceOne", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Unguarded_policy_update_rejects_replace_transition_and_guarded_repository_delegates()
    {
        var source = ReadRepoFile(
            "services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/ProductLegalEntityScopePolicyRepository.cs")
            .ReplaceLineEndings("\n");

        Assert.Contains(
            "newAuditIntents.Single().Operation\n            == ProductAuditOperation.ProductLegalEntityScopePolicyReplaced",
            source,
            StringComparison.Ordinal);
        Assert.Contains("VerifiedZeroMutation: true", source, StringComparison.Ordinal);
        Assert.Contains("=> _guardedWriteSession.ReplaceAsync(", source, StringComparison.Ordinal);
    }

    private static string ReadRepoFile(string relativePath)
    {
        var root = FindRepoRoot();
        return File.ReadAllText(Path.Combine(
            root,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("REPO_ROOT_NOT_FOUND");
    }
}
