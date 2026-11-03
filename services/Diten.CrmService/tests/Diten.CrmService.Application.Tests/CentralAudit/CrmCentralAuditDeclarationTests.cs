using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Diten.BuildingBlocks.CentralAudit;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Knowledge.Regulatory;
using Xunit;

namespace Diten.CrmService.Application.Tests.CentralAudit;

/// <summary>
/// CRM-AUD-KN-01 (a) — measured on the production assembly: each of the 34 Knowledge write commands declares its central
/// record in values Platform knows (category <c>Crm</c>, an <c>AuditOperation</c> name) under the module code Platform's
/// CRM manifest registers.
/// </summary>
public sealed class CrmCentralAuditDeclarationTests
{
    /// <summary>The first slice (owner request 2026-11-02). The other 19 CRM commands are the next slice.</summary>
    public static readonly string[] KnowledgeCommands =
    [
        "AddKnowledgePathClaimCommand", "ArrangeKnowledgePathClaimCommand", "RemoveKnowledgePathClaimCommand", "BindKnowledgePathChainCommand",
        "AddKnowledgePathRevisionNoteCommand", "ResolveKnowledgePathRevisionNoteCommand", "SubmitKnowledgePathReviewCommand",
        "WithdrawKnowledgePathReviewCommand", "DecideKnowledgePathRevisionCommand",
        "ReleaseKnowledgePathRevisionCommand", "RenderKnowledgePathRevisionCommand", "WithdrawKnowledgePathReleaseCommand",
        "CreateObjectionResponseCommand", "UpdateObjectionResponseCommand", "CopyObjectionResponseCommand", "SubmitObjectionResponseCommand",
        "DecideObjectionResponseCommand", "ApproveObjectionResponseCommand", "WithdrawObjectionResponseCommand", "RetireObjectionResponseCommand",
        "CreateSafetyTextCommand", "UpdateSafetyTextCommand", "SubmitSafetyTextCommand", "DecideSafetyTextCommand",
        "WithdrawSafetyTextCommand", "ArchiveSafetyTextCommand", "NewSafetyTextVersionCommand",
        "CreateCountryLegalProfileCommand", "UpdateCountryLegalProfileCommand", "SubmitCountryLegalProfileCommand",
        "DecideCountryLegalProfileCommand", "WithdrawCountryLegalProfileCommand", "ArchiveCountryLegalProfileCommand",
        "NewCountryLegalProfileVersionCommand"
    ];

    public static TheoryData<string> Commands()
    {
        var data = new TheoryData<string>();
        foreach (var name in KnowledgeCommands)
        {
            data.Add(name);
        }

        return data;
    }

    [Fact]
    public void The_slice_is_the_34_knowledge_commands()
    {
        Assert.Equal(34, KnowledgeCommands.Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [MemberData(nameof(Commands))]
    public void Each_knowledge_command_declares_a_crm_record_platform_can_file(string name)
    {
        var type = Assert.Single(typeof(CrmCentralAudit).Assembly.GetTypes(), t => t.Name == name);
        Assert.Contains(".Features.Knowledge.", type.Namespace, StringComparison.Ordinal);
        Assert.True(typeof(ICentralAuditDeclaration).IsAssignableFrom(type), $"{name} carries no ICentralAuditDeclaration");

        var metadata = ((ICentralAuditDeclaration)RuntimeHelpers.GetUninitializedObject(type)).CentralAudit;

        Assert.Equal("Crm", metadata.Category);
        Assert.True(CentralAuditPlatformValues.Categories.ContainsKey(metadata.Category), $"{name}: category {metadata.Category} is not a Platform name");
        Assert.True(CentralAuditPlatformValues.Operations.ContainsKey(metadata.Operation), $"{name}: operation {metadata.Operation} is not a Platform name");
        Assert.True(CentralAuditPlatformValues.IsKnown(metadata));
        Assert.Equal(CrmCentralAudit.SourceModule, metadata.SourceModule);
        Assert.False(string.IsNullOrWhiteSpace(metadata.EntityType), name);
    }

    /// <summary>The source module is the module code Platform's CRM manifest registers — read from that file, not copied.</summary>
    [Fact]
    public void The_source_module_is_the_code_platforms_crm_manifest_registers()
    {
        var manifest = File.ReadAllText(Path.Combine(
            RepoRoot(), "services", "Diten.Platform", "src", "Diten.Platform.Application", "Features", "Crm", "SelfRegistration",
            "CrmManifestProvider.cs"));

        var code = Regex.Match(manifest, "ModuleCode:\\s*\"(?<code>[^\"]+)\"").Groups["code"].Value;

        Assert.Equal(code, CrmCentralAudit.SourceModule);
    }

    [Fact]
    public void A_create_names_the_id_in_its_succeeded_response_and_a_failed_envelope_names_none()
    {
        var command = new CreateSafetyTextCommand(Guid.NewGuid(), null, "tr", "tr", "body");
        var created = Guid.NewGuid();
        var dto = (SafetyTextDto)RuntimeHelpers.GetUninitializedObject(typeof(SafetyTextDto)) with { Id = created };

        Assert.Equal(created, command.CentralAuditEntityId(Response<SafetyTextDto>.Success(dto)));
        Assert.Null(command.CentralAuditEntityId(Response<SafetyTextDto>.Fail("nope")));
        Assert.Null(command.CentralAuditEntityId(null));
    }

    private static string RepoRoot()
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

        throw new InvalidOperationException($"Repo root not found above '{AppContext.BaseDirectory}'.");
    }
}
