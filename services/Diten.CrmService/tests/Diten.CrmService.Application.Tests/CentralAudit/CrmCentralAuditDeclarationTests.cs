using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Diten.BuildingBlocks.CentralAudit;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.VisitEvidence;
using Diten.CrmService.Application.Features.VisitExecution;
using Diten.CrmService.Application.Features.VisitReport.V2;
using Diten.CrmService.Application.Features.Knowledge.Regulatory;
using Xunit;

namespace Diten.CrmService.Application.Tests.CentralAudit;

/// <summary>
/// Measured on the production assembly: every command in the Knowledge and Visit slices declares its central record in
/// values Platform knows (category <c>Crm</c>, an <c>AuditOperation</c> name) under the module code Platform's CRM manifest
/// registers.
/// </summary>
public sealed class CrmCentralAuditDeclarationTests
{
    /// <summary>The first CRM central-audit slice (owner request 2026-11-02).</summary>
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

    public static readonly string[] VisitCommands =
    [
        "CreateVisitEvidenceExceptionCommand", "UpdateVisitEvidenceExceptionCommand", "SubmitVisitEvidenceExceptionCommand",
        "WithdrawVisitEvidenceExceptionCommand", "DecideVisitEvidenceExceptionCommand", "CancelVisitEvidenceExceptionCommand",
        "EndVisitEvidenceExceptionEarlyCommand", "StartVisitExecutionCommand", "CaptureVisitExecutionCommand",
        "FinishVisitExecutionCommand", "SignVisitExecutionCommand", "UpdateVisitEvidencePolicyCommand",
        "UpdateDoctorProfileCommand", "UpdatePotentialThresholdCommand", "ReopenPlanningWeekCommand", "UploadShelfPhotoCommand"
    ];

    public static readonly string[] VisitReportV2Commands =
    [
        "SaveVisitReportV2DraftCommand", "SubmitVisitReportV2Command", "AmendVisitReportV2Command"
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

    public static TheoryData<string, string, string> VisitCommandMetadata() => new()
    {
        { "CreateVisitEvidenceExceptionCommand", "Create", "VisitEvidenceException" },
        { "UpdateVisitEvidenceExceptionCommand", "Update", "VisitEvidenceException" },
        { "SubmitVisitEvidenceExceptionCommand", "LifecycleTransition", "VisitEvidenceException" },
        { "WithdrawVisitEvidenceExceptionCommand", "LifecycleTransition", "VisitEvidenceException" },
        { "DecideVisitEvidenceExceptionCommand", "LifecycleTransition", "VisitEvidenceException" },
        { "CancelVisitEvidenceExceptionCommand", "LifecycleTransition", "VisitEvidenceException" },
        { "EndVisitEvidenceExceptionEarlyCommand", "LifecycleTransition", "VisitEvidenceException" },
        { "StartVisitExecutionCommand", "Create", "VisitExecution" },
        { "CaptureVisitExecutionCommand", "Update", "VisitExecution" },
        { "FinishVisitExecutionCommand", "LifecycleTransition", "VisitExecution" },
        { "SignVisitExecutionCommand", "LifecycleTransition", "VisitExecution" },
        { "UpdateVisitEvidencePolicyCommand", "Update", "VisitEvidencePolicy" },
        { "UpdateDoctorProfileCommand", "Update", "DoctorProfile" },
        { "UpdatePotentialThresholdCommand", "Update", "PotentialThreshold" },
        { "ReopenPlanningWeekCommand", "LifecycleTransition", "PlanningSession" },
        { "UploadShelfPhotoCommand", "Update", "VisitReport" }
    };

    public static TheoryData<string, string, string> VisitReportV2CommandMetadata() => new()
    {
        { "SaveVisitReportV2DraftCommand", "Update", "VisitReport" },
        { "SubmitVisitReportV2Command", "LifecycleTransition", "VisitReport" },
        { "AmendVisitReportV2Command", "Update", "VisitReport" }
    };

    [Fact]
    public void The_slice_is_the_34_knowledge_commands()
    {
        Assert.Equal(34, KnowledgeCommands.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void The_visit_slice_is_exactly_the_16_requested_commands()
    {
        Assert.Equal(16, VisitCommands.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(16, VisitCommandMetadata().Count);
    }

    [Fact]
    public void The_visit_report_v2_slice_is_exactly_the_three_requested_commands()
    {
        Assert.Equal(3, VisitReportV2Commands.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(3, VisitReportV2CommandMetadata().Count);
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

    [Theory]
    [MemberData(nameof(VisitCommandMetadata))]
    public void Each_visit_command_declares_the_expected_crm_record_platform_can_file(
        string name,
        string operation,
        string entityType)
    {
        Assert.Contains(name, VisitCommands);
        var type = Assert.Single(typeof(CrmCentralAudit).Assembly.GetTypes(), t => t.Name == name);
        Assert.True(typeof(ICentralAuditDeclaration).IsAssignableFrom(type), $"{name} carries no ICentralAuditDeclaration");

        var metadata = ((ICentralAuditDeclaration)RuntimeHelpers.GetUninitializedObject(type)).CentralAudit;

        Assert.Equal(CrmCentralAudit.Category, metadata.Category);
        Assert.Equal(operation, metadata.Operation);
        Assert.Equal(entityType, metadata.EntityType);
        Assert.Equal(CrmCentralAudit.SourceModule, metadata.SourceModule);
        Assert.True(CentralAuditPlatformValues.Categories.ContainsKey(metadata.Category), $"{name}: unknown category");
        Assert.True(CentralAuditPlatformValues.Operations.ContainsKey(metadata.Operation), $"{name}: unknown operation");
        Assert.True(CentralAuditPlatformValues.IsKnown(metadata));
    }

    [Theory]
    [MemberData(nameof(VisitReportV2CommandMetadata))]
    public void Each_visit_report_v2_command_declares_the_expected_crm_record_platform_can_file(
        string name,
        string operation,
        string entityType)
    {
        Assert.Contains(name, VisitReportV2Commands);
        var type = Assert.Single(typeof(CrmCentralAudit).Assembly.GetTypes(), t => t.Name == name);
        Assert.True(typeof(ICentralAuditDeclaration).IsAssignableFrom(type), $"{name} carries no ICentralAuditDeclaration");

        var metadata = ((ICentralAuditDeclaration)RuntimeHelpers.GetUninitializedObject(type)).CentralAudit;

        Assert.Equal(CrmCentralAudit.Category, metadata.Category);
        Assert.Equal(operation, metadata.Operation);
        Assert.Equal(entityType, metadata.EntityType);
        Assert.Equal(CrmCentralAudit.SourceModule, metadata.SourceModule);
        Assert.True(CentralAuditPlatformValues.IsKnown(metadata));
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

    [Fact]
    public void Visit_creates_name_only_the_id_in_their_succeeded_response()
    {
        var exception = new CreateVisitEvidenceExceptionCommand(new VisitEvidenceExceptionInput(null, null, null, null, null));
        var exceptionId = Guid.NewGuid();
        var exceptionDto = (VisitEvidenceExceptionDto)RuntimeHelpers.GetUninitializedObject(typeof(VisitEvidenceExceptionDto)) with { Id = exceptionId };
        var execution = new StartVisitExecutionCommand(Guid.NewGuid(), null, null, null, null, null, null, null);
        var executionId = Guid.NewGuid();
        var executionDto = (VisitExecutionDto)RuntimeHelpers.GetUninitializedObject(typeof(VisitExecutionDto)) with { Id = executionId };

        Assert.Equal(exceptionId, exception.CentralAuditEntityId(Response<VisitEvidenceExceptionDto>.Success(exceptionDto)));
        Assert.Null(exception.CentralAuditEntityId(Response<VisitEvidenceExceptionDto>.Fail("nope")));
        Assert.Null(exception.CentralAuditEntityId(null));
        Assert.Equal(executionId, execution.CentralAuditEntityId(Response<VisitExecutionDto>.Success(executionDto)));
        Assert.Null(execution.CentralAuditEntityId(Response<VisitExecutionDto>.Fail("nope")));
        Assert.Null(execution.CentralAuditEntityId(null));
    }

    [Fact]
    public void Visit_report_v2_save_and_submit_name_only_a_non_empty_id_from_a_succeeded_response()
    {
        var body = new VisitReportV2Body();
        var save = new SaveVisitReportV2DraftCommand(Guid.NewGuid(), body, null);
        var submit = new SubmitVisitReportV2Command(Guid.NewGuid(), body, null);
        var reportId = Guid.NewGuid();
        var result = new VisitReportV2SaveResultDto(reportId, 1, "submitted", [], []);
        var emptyResult = result with { VisitReportId = Guid.Empty };
        var nullResult = result with { VisitReportId = null };

        Assert.Equal(reportId, save.CentralAuditEntityId(Response<VisitReportV2SaveResultDto>.Success(result)));
        Assert.Null(save.CentralAuditEntityId(Response<VisitReportV2SaveResultDto>.Success(emptyResult)));
        Assert.Null(save.CentralAuditEntityId(Response<VisitReportV2SaveResultDto>.Success(nullResult)));
        Assert.Null(save.CentralAuditEntityId(Response<VisitReportV2SaveResultDto>.Fail("nope")));
        Assert.Null(save.CentralAuditEntityId(null));

        Assert.Equal(reportId, submit.CentralAuditEntityId(Response<VisitReportV2SaveResultDto>.Success(result)));
        Assert.Null(submit.CentralAuditEntityId(Response<VisitReportV2SaveResultDto>.Success(emptyResult)));
        Assert.Null(submit.CentralAuditEntityId(Response<VisitReportV2SaveResultDto>.Success(nullResult)));
        Assert.Null(submit.CentralAuditEntityId(Response<VisitReportV2SaveResultDto>.Fail("nope")));
        Assert.Null(submit.CentralAuditEntityId(null));
    }

    [Fact]
    public void Visit_report_v2_amend_names_the_request_visit_report_id()
    {
        var reportId = Guid.NewGuid();
        var command = new AmendVisitReportV2Command(reportId, "reason", new VisitReportV2Body(), null);

        Assert.Equal(reportId, command.CentralAuditEntityId(null));
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
