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
        "EndProductLegalEntityScopePolicyCommand"
    ];

    [Fact]
    public void Mutation_inventory_is_exactly_the_frozen_18_commands_and_has_no_unclassified_nineteenth_writer()
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
        Assert.Equal(18, actual.Length);
        Assert.Equal(ExactMutationCommands.OrderBy(name => name, StringComparer.Ordinal), actual);
        Assert.Equal(18, ExactMutationCommands.Distinct(StringComparer.Ordinal).Count());
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

    private static bool ReadsTheFence(string source)
    {
        var code = string.Join('\n', source.ReplaceLineEndings("\n").Split('\n')
            .Select(line => line.TrimStart()).Where(line => !line.StartsWith("//", StringComparison.Ordinal)));
        return code.Contains("ProductIdentityBackgroundScope", StringComparison.Ordinal)
            || code.Contains("LskuBackgroundScope", StringComparison.Ordinal)
            || code.Contains(".ActiveFence is null", StringComparison.Ordinal);
    }

    /// <summary>
    /// WP-MDM-SCOPE-ACCEPT-01 FIX2 (5) — scope decision 5: activation and rollback only through an explicitly granted,
    /// audited key a person cannot hold. MEASURED: the permission keys <c>mdm.product-legal-entity-scope-rollout.activate /
    /// .rollback</c> exist (manifest; Auth grants them to a role a person can hold), so they must open NOTHING in MDM — no
    /// endpoint, handler or runner may check them. The only transition path is the Development-only CLI (its own test).
    /// When the non-human key is built (backlog), this test is where it is pinned.
    /// </summary>
    [Fact]
    public void The_rollout_activate_and_rollback_permissions_open_no_path_a_person_can_use()
    {
        var root = FindRepoRoot();
        var sourceRoot = Path.Combine(root, "services", "Diten.MdmService", "src");
        var users = Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => File.ReadAllText(path).Contains("product-legal-entity-scope-rollout.", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .ToArray();

        Assert.Equal(["services/Diten.MdmService/src/Diten.MdmService.Api/ModuleRegistration/ProductItemSkuMasterManifestProvider.cs"], users);
        var controllers = ReadRepoFile("services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/ProductLegalEntityScopesController.cs");
        Assert.DoesNotContain("Activate", controllers, StringComparison.Ordinal);
        Assert.DoesNotContain("Suspend", controllers, StringComparison.Ordinal);
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
