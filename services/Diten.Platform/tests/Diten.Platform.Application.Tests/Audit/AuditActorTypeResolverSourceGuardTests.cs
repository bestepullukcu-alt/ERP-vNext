using System.Text.RegularExpressions;
using Xunit;

namespace Diten.Platform.Application.Tests.Audit;

/// <summary>
/// BL-409 — ONE CLAIM-TO-ACTOR MAPPING, measured on the PRODUCTION SOURCE.
///
/// <para>The behaviour tests prove <c>AuditBehavior</c> and <c>DataExportAuditWriter</c> resolve correctly today. They
/// cannot prove that the next audit writer does not grow its own <c>"tenant_user" =&gt; AuditActorType.TenantUser</c>
/// switch with its own fallback — which is how BL-347's export writer, the entitlement sink and the explain service
/// came to hold three different answers (Unknown, System, TenantUser) for the same unrecognised token. This suite
/// finds every Platform production file where a token actor-type literal meets a person-valued
/// <c>AuditActorType</c>, and demands it is either the resolver or on the short, reasoned list below.</para>
/// </summary>
public sealed class AuditActorTypeResolverSourceGuardTests
{
    private const string ResolverFile = "Diten.Platform.Application/Contracts/Audit/AuditActorTypeResolver.cs";

    /// <summary>A token actor-type value as AuthService mints it, as a C# string literal.</summary>
    private static readonly Regex ClaimLiteral = new("\"(tenant_user|platform_admin|partner_admin)\"", RegexOptions.Compiled);

    /// <summary>A person-valued audit actor type — what a claim mapping produces.</summary>
    private static readonly Regex PersonActorType =
        new(@"\bAuditActorType\.(TenantUser|PlatformAdministrator|PartnerAdministrator)\b", RegexOptions.Compiled);

    /// <summary>
    /// Pre-existing mappings OUTSIDE BL-409's scope (production code outside the Application audit path). Each keeps a
    /// different fallback; converging them changes what those records say and is a decision of its own, not a fix.
    /// Adding to this list is a design decision.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> KnownOutsideBl409 = new Dictionary<string, string>
    {
        ["Diten.Platform.Infrastructure/Services/Audit/PlatformEntitlementAuditSink.cs"] =
            "MOD-0018 entitlement-deny audit: also maps system/service and falls back to System — Infrastructure layer",
        ["Diten.Platform.API/Authorization/Explain/SelfAccessExplainService.cs"] =
            "self-access explain audit: falls back to TenantUser — API layer"
    };

    [Fact]
    public void Only_the_resolver_maps_a_token_actor_type_to_an_audit_actor_type()
    {
        var mappers = MappingFiles();

        Assert.Contains(ResolverFile, mappers); // the guard is not vacuous: it sees the real mapping

        var unexpected = mappers
            .Where(file => file != ResolverFile && !KnownOutsideBl409.ContainsKey(file))
            .OrderBy(file => file, StringComparer.Ordinal)
            .ToList();
        Assert.True(unexpected.Count == 0,
            "These files map a token actor_type to an AuditActorType on their own: " + string.Join(", ", unexpected)
            + ". Use AuditActorTypeResolver (BL-409) instead of a second copy of the switch.");

        // A stale entry would make the list claim a copy exists that no longer does.
        var stale = KnownOutsideBl409.Keys.Except(mappers).OrderBy(file => file, StringComparer.Ordinal).ToList();
        Assert.True(stale.Count == 0,
            "These known mappings are gone — remove them from KnownOutsideBl409: " + string.Join(", ", stale));
    }

    [Theory]
    [InlineData("Diten.Platform.Application/Contracts/Behaviors/AuditBehavior.cs", "AuditActorTypeResolver.ForCommand(")]
    [InlineData("Diten.Platform.Application/Features/Audit/Services/DataExportAuditWriter.cs", "AuditActorTypeResolver.ForDataExport(")]
    public void Both_audit_writers_take_the_actor_type_from_the_resolver(string file, string call)
    {
        Assert.Contains(call, File.ReadAllText(Path.Combine(SourceRoot(), file)), StringComparison.Ordinal);
    }

    private static HashSet<string> MappingFiles()
    {
        var root = SourceRoot();
        return Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .Where(path =>
            {
                var source = File.ReadAllText(path);
                return ClaimLiteral.IsMatch(source) && PersonActorType.IsMatch(source);
            })
            .Select(path => Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/'))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static bool IsBuildOutput(string path)
    {
        var separator = Path.DirectorySeparatorChar;
        return path.Contains($"{separator}bin{separator}", StringComparison.Ordinal)
               || path.Contains($"{separator}obj{separator}", StringComparison.Ordinal);
    }

    private static string SourceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "services", "Diten.Platform", "src");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("services/Diten.Platform/src was not found above the test output directory.");
    }
}
