using System.Text.RegularExpressions;
using Diten.ManufacturingService.Api.ModuleRegistration;
using Diten.ManufacturingService.Application.Common;
using Diten.ManufacturingService.Application.Features.Boms;
using Diten.ManufacturingService.Application.Features.Boms.Commands;
using Diten.Shared.Core;
using MediatR;
using Xunit;

namespace Diten.ManufacturingService.Tests.Registration;

/// <summary>
/// BL-501 pre-wiring (owner decision 2026-10-12: MOD-0193 writes to Platform's central audit log as well as the BOM
/// history). Measured on the production assemblies: every write command declares its central record, in values Platform
/// knows, with an entity id the forwarder can use — and the service carries NO forwarder of its own (AUD-001 K4).
/// </summary>
public sealed class CentralAuditDeclarationTests
{
    private static readonly Type[] WriteCommands = typeof(ICentralAuditDeclaration).Assembly.GetTypes()
        .Where(t => t is { IsClass: true, IsAbstract: false } && t.Name.EndsWith("Command", StringComparison.Ordinal)
            && t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>)))
        .ToArray();

    [Fact]
    public void Every_write_command_declares_its_central_audit_record()
    {
        Assert.Equal(4, WriteCommands.Length);
        Assert.Empty(WriteCommands.Where(t => !typeof(ICentralAuditDeclaration).IsAssignableFrom(t)).Select(t => t.Name));
    }

    [Fact]
    public void Category_and_operation_are_values_platform_defines_and_the_source_is_the_registered_module()
    {
        var enums = File.ReadAllText(Path.Combine(RepoRoot(), "services", "Diten.Platform", "src", "Diten.Platform.Domain", "Enums", "AuditEnums.cs"));
        var categories = Members(enums, "AuditCategory");
        var operations = Members(enums, "AuditOperation");
        var module = new BomRoutingsManifestProvider().GetManifest().ModuleCode;

        foreach (var declaration in Declarations())
        {
            Assert.Contains(declaration.CentralAudit.Category, categories);
            Assert.Contains(declaration.CentralAudit.Operation, operations);
            Assert.NotEqual("Unknown", declaration.CentralAudit.Operation);
            Assert.Equal(module, declaration.CentralAudit.SourceModule);
            Assert.Equal("BomVersion", declaration.CentralAudit.EntityType);
        }

        Assert.Equal(["Create", "Delete", "LifecycleTransition", "Update"], Declarations().Select(d => d.CentralAudit.Operation).Order());
    }

    [Fact]
    public void Each_command_names_the_entity_it_wrote()
    {
        var id = Guid.NewGuid();
        var created = Response<BomView>.Success(new BomView(id.ToString(), Guid.NewGuid(), 1, "Draft", null, null, [], null, null, null, 1, Guid.NewGuid()), 201);

        Assert.Equal(id, new CreateBomDraftCommand(null!).CentralAuditEntityId(created));
        Assert.Null(new CreateBomDraftCommand(null!).CentralAuditEntityId(Response<BomView>.Fail("x", 400)));
        Assert.Equal(id, new UpdateBomDraftCommand(id, null!).CentralAuditEntityId(null));
        Assert.Equal(id, new ReleaseBomVersionCommand(id, null!).CentralAuditEntityId(null));
        Assert.Equal(id, new DeleteBomDraftCommand(id, 1).CentralAuditEntityId(null));
    }

    /// <summary>K4: the forwarder is BL-501's single copy. A per-service behaviour or Platform audit client is a copy.</summary>
    [Fact]
    public void The_service_carries_no_forwarder_of_its_own()
    {
        var src = Path.Combine(RepoRoot(), "services", "Diten.ManufacturingService", "src");
        var offenders = Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"/api/internal/audit/append|class\s+\w*AuditForwarding\w*|IPlatformAuditForwarder"))
            .Select(Path.GetFileName)
            .ToList();
        Assert.Empty(offenders);
    }

    private static IEnumerable<ICentralAuditDeclaration> Declarations() =>
    [
        new CreateBomDraftCommand(null!),
        new UpdateBomDraftCommand(Guid.NewGuid(), null!),
        new ReleaseBomVersionCommand(Guid.NewGuid(), null!),
        new DeleteBomDraftCommand(Guid.NewGuid(), 1)
    ];

    private static HashSet<string> Members(string source, string enumName)
    {
        var body = Regex.Match(source, @"enum\s+" + enumName + @"\s*\{(?<body>[^}]*)\}").Groups["body"].Value;
        Assert.False(string.IsNullOrWhiteSpace(body), $"enum {enumName} not found");
        return Regex.Matches(Regex.Replace(body, @"///[^\n]*|//[^\n]*", string.Empty), @"(\w+)\s*=\s*\d+").Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("repository root (AGENTS.md) not found");
    }
}
