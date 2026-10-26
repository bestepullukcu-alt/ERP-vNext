using System.Text.RegularExpressions;
using Diten.BuildingBlocks.CentralAudit;
using Diten.SupplyChainService.Api.ModuleRegistration;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Domain.Features.SandopPlans;
using MediatR;
using Xunit;
namespace Diten.SupplyChainService.Tests.CentralAudit;

/// <summary>
/// BL-501 (WP-A1, E-2) — the guards behind <c>AddCentralAudit(…, RequireEnlistment = true)</c>, measured on production
/// code: every write command is marked and names a real module, Platform category and operation; every transaction a
/// command can run in enlists the event; and a failed envelope is not a success.
/// </summary>
public sealed class CentralAuditDeclarationTests
{
    private static readonly Type[] Commands = typeof(RequestContext).Assembly.GetTypes()
        .Where(t => t is { IsClass: true, IsAbstract: false } && t.Name.EndsWith("Command", StringComparison.Ordinal)
            && t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>)))
        .OrderBy(t => t.Name, StringComparer.Ordinal).ToArray();

    [Fact]
    public void Every_write_command_is_marked_and_names_a_manifest_module_and_Platform_enum_names()
    {
        Assert.Equal(17, Commands.Length);
        IModuleManifestProvider[] providers =
        [
            new ShipmentTrackingPodManifestProvider(), new CarrierManagementManifestProvider(), new RoutingLoadPlanningManifestProvider(),
            new ReverseLogisticsManifestProvider(), new ClaimsManagementManifestProvider(), new CapacityPlanningManifestProvider(),
            new SopWorkflowSignoffsManifestProvider()
        ];
        var modules = providers.Select(p => p.GetManifest().ModuleCode).ToHashSet(StringComparer.Ordinal);
        var enums = File.ReadAllText(Path.Combine(RepositoryRoot(), "services", "Diten.Platform", "src", "Diten.Platform.Domain", "Enums", "AuditEnums.cs"));
        var categories = EnumNames(enums, "AuditCategory");
        var operations = EnumNames(enums, "AuditOperation");

        var failures = new List<string>();
        foreach (var type in Commands)
        {
            if (!typeof(ICentralAuditDeclaration).IsAssignableFrom(type)) { failures.Add($"{type.Name}: not marked"); continue; }
            var metadata = ((ICentralAuditDeclaration)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(type)).CentralAudit;
            if (!modules.Contains(metadata.SourceModule)) failures.Add($"{type.Name}: module {metadata.SourceModule} is no manifest's");
            if (!categories.Contains(metadata.Category)) failures.Add($"{type.Name}: category {metadata.Category} is not a Platform AuditCategory");
            if (!operations.Contains(metadata.Operation)) failures.Add($"{type.Name}: operation {metadata.Operation} is not a Platform AuditOperation");
            if (string.IsNullOrWhiteSpace(metadata.EntityType)) failures.Add($"{type.Name}: no entity type");
        }
        Assert.Empty(failures);
    }

    /// <summary>
    /// RequireEnlistment means a command that enlisted nothing files nothing — so a transaction that forgot the line would
    /// drop records silently. Every transaction in Persistence enlists before its commit, except the schema set-up
    /// transactions (<c>*Schema.cs</c>, no command) and the Capacity evaluation worker (<c>CapacityLeaseStore</c>, a
    /// background lease with no marked command in flight — an enlistment there would be a no-op by design).
    /// </summary>
    [Fact]
    public void Every_transaction_a_command_can_run_in_enlists_the_central_audit_event()
    {
        var persistence = Path.Combine(RepositoryRoot(), "services", "Diten.SupplyChainService", "src", "Diten.SupplyChainService.Persistence");
        var failures = new List<string>();
        var seen = 0;
        foreach (var file in Directory.GetFiles(persistence, "*.cs", SearchOption.AllDirectories)
                     .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")).Order(StringComparer.Ordinal))
        {
            var name = Path.GetFileName(file);
            var text = Regex.Replace(File.ReadAllText(file), @"//[^\n]*", string.Empty);
            if (name.EndsWith("Schema.cs", StringComparison.Ordinal) || name == "CapacityLeaseStore.cs")
            {
                Assert.DoesNotContain("CentralAuditTransaction.EnlistAsync(", text, StringComparison.Ordinal);
                continue;
            }
            var starts = Regex.Matches(text, @"StartTransaction\(|WithTransactionAsync\(").Select(m => m.Index).ToList();
            for (var i = 0; i < starts.Count; i++)
            {
                seen++;
                var end = i + 1 < starts.Count ? starts[i + 1] : text.Length;
                var block = text[starts[i]..end];
                // The commit that ends this block: CommitTransactionAsync, or the end of a WithTransactionAsync callback.
                var commit = block.IndexOf("CommitTransactionAsync(", StringComparison.Ordinal);
                var scope = commit < 0 ? block : block[..commit];
                if (!scope.Contains("CentralAuditTransaction.EnlistAsync(", StringComparison.Ordinal))
                    failures.Add($"{name}: transaction #{i + 1} does not enlist the central-audit event before it commits");
            }
        }
        // Carrier, Claim, Load, Return, S&OP, Shipment: one each; Capacity: plan, scenario, evaluation.
        Assert.Equal(9, seen);
        Assert.Empty(failures);
    }

    [Theory]
    [InlineData(200, true)] [InlineData(201, true)] [InlineData(202, true)]
    [InlineData(400, false)] [InlineData(404, false)] [InlineData(409, false)] [InlineData(422, false)] [InlineData(500, false)] [InlineData(503, false)]
    public void Only_a_2xx_envelope_is_a_success(int status, bool success)
    {
        Assert.Equal(success, new Response<object>(null, status).IsSuccessful);
        Assert.Equal(success, new SandopResult(status, success ? null : "CODE", null).IsSuccessful);
    }

    private static HashSet<string> EnumNames(string source, string name)
    {
        var body = Regex.Match(source, @"enum\s+" + name + @"\s*\{(?<body>[^}]*)\}").Groups["body"].Value;
        body = Regex.Replace(body, @"///[^\n]*", string.Empty);
        return Regex.Matches(body, @"(?<name>[A-Za-z]+)\s*=").Select(m => m.Groups["name"].Value).ToHashSet(StringComparer.Ordinal);
    }

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
