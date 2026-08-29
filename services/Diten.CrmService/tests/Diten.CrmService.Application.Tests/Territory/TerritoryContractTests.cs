using Diten.CrmService.Application.Features.Territory;
using Diten.CrmService.Application.Features.Territory.Contract;
using Xunit;

namespace Diten.CrmService.Application.Tests.Territory;

public sealed class TerritoryContractTests
{
    private static readonly Guid TenantA = Guid.Parse("97c59330-dbc4-4665-b29c-0c26dbb5cc93");

    private static GetTerritoryContractHandler HandlerWithNoPublishedSets()
    {
        // Real validator over an all-unpublished catalog → every required set reports not-ready (fail-closed, no crash).
        var references = new TerritoryReferenceValidator(new FakeReferenceValidator(), new FakeMetadataReader(), new FakeCatalogReader());
        return new GetTerritoryContractHandler(TenantFactory.Tenant(TenantA), references);
    }

    [Fact]
    public async Task Contract_Reports_Module_Identity_And_Scope()
    {
        var response = await HandlerWithNoPublishedSets().Handle(new GetTerritoryContractQuery(), default);

        Assert.True(response.IsSuccessful);
        var dto = response.Data!;
        Assert.Equal("MOD-0151", dto.ModuleId);
        Assert.Equal("Territory Management", dto.ModuleName);
        Assert.Equal("Diten.CrmService", dto.Service);
        Assert.Contains("FU02B-lifecycle-computed-expiry-draft-soft-delete", dto.RuntimeScope);
        Assert.Equal(TenantA, dto.TenantId);
    }

    [Fact]
    public async Task Contract_Flags_Fu05_Apply_History_Coverage_Without_Workflow()
    {
        var dto = (await HandlerWithNoPublishedSets().Handle(new GetTerritoryContractQuery(), default)).Data!;

        Assert.True(dto.Features.TerritoryModels);
        Assert.True(dto.Features.TerritoryNodes);
        Assert.True(dto.Features.UiEnabled);

        // FU02B lifecycle stays exactly as it was.
        Assert.True(dto.Features.SupportsLifecycleActions);
        Assert.True(dto.Features.SupportsComputedExpiry);
        Assert.True(dto.Features.SupportsDraftSoftDelete);
        Assert.False(dto.Features.SupportsWorkflowActivation);
        Assert.False(dto.Features.SupportsApprovalTrace);

        // FU03 turns on rules + preview...
        Assert.True(dto.Features.AssignmentRules);
        Assert.True(dto.Features.SupportsAssignmentRules);
        Assert.True(dto.Features.SupportsAssignmentPreview);

        // FU04 turns on resource (people) assignments + their exclusivity guard...
        Assert.True(dto.Features.ResourceAssignments);
        Assert.True(dto.Features.SupportsResourceAssignments);
        Assert.True(dto.Features.SupportsResourceAssignmentLifecycle);
        Assert.True(dto.Features.SupportsResourceReplacement);
        Assert.True(dto.Features.SupportsResourceTransfer);
        Assert.True(dto.Features.SupportsCurrentResponsibility);
        Assert.True(dto.Features.SupportsPositionBasedResourceAssignment);
        Assert.True(dto.Features.SupportsResourceExclusivityGuard);

        Assert.True(dto.Features.AccountAssignmentApply);
        Assert.True(dto.Features.SupportsAccountAssignmentApply);
        Assert.True(dto.Features.SupportsAssignmentHistory);
        Assert.True(dto.Features.SupportsCoverageSummary);
        // FU05A — current coverage only projects through an active territory model.
        Assert.True(dto.Features.SupportsCoverageSummaryModelLifecycleGuard);

        // FU08 — controlled import/export. Resource assignment APPLY stays false: that is FU08A.
        Assert.True(dto.Features.SupportsTerritoryExport);
        Assert.True(dto.Features.SupportsTerritoryImportExport);
        Assert.True(dto.Features.SupportsTerritoryImportDryRun);
        Assert.True(dto.Features.SupportsTerritoryImportApply);
        Assert.False(dto.Features.SupportsResourceAssignmentImportApply);
        Assert.True(dto.Features.SupportsVisitRouteReadiness);
        Assert.True(dto.Features.SupportsContactDerivedCoverageReadiness);
        Assert.True(dto.Features.SupportsRouteCandidateReadiness);
        Assert.True(dto.Features.SupportsContactAvailabilityInputBoundary);
        Assert.True(dto.Features.SupportsVisitFrequencyInputBoundary);
        Assert.DoesNotContain(dto.Features.GetType().GetProperties(), p => p.Name is "SupportsVisitPlanning" or "SupportsRoutePlanning");
        Assert.False(dto.Features.WorkflowActivation);
        Assert.False(dto.Features.EvidencePack);
        Assert.False(dto.Features.ImportExport);
    }

    [Fact]
    public async Task Contract_RuntimeScope_Declares_Fu03()
    {
        var dto = (await HandlerWithNoPublishedSets().Handle(new GetTerritoryContractQuery(), default)).Data!;

        Assert.Contains("FU03-assignment-rules-and-preview", dto.RuntimeScope);
        Assert.Contains("FU04-resource-assignments", dto.RuntimeScope);
        Assert.Contains("FU05-account-assignment-apply-history", dto.RuntimeScope);
        Assert.Contains("FU05A-coverage-summary-model-lifecycle-guard", dto.RuntimeScope);
        Assert.Contains("FU08-import-export-hardening", dto.RuntimeScope);
        Assert.Contains("FU09A-visit-route-readiness-read-only", dto.RuntimeScope);
    }

    [Fact]
    public async Task Contract_Lists_All_Required_Reference_Sets()
    {
        var dto = (await HandlerWithNoPublishedSets().Handle(new GetTerritoryContractQuery(), default)).Data!;

        Assert.Equal(TerritoryReferenceSets.Required.Count, dto.RequiredReferenceSets.Count);
        Assert.Contains(dto.RequiredReferenceSets, r => r.SetCode == TerritoryReferenceSets.TerritoryLevel);
        Assert.Contains(dto.RequiredReferenceSets, r => r.SetCode == TerritoryReferenceSets.TerritoryModelStatus);
        Assert.Contains(dto.RequiredReferenceSets, r => r.SetCode == TerritoryReferenceSets.TerritoryNodeStatus);
    }

    [Fact]
    public async Task Contract_With_No_Published_Values_Is_Not_Ready_And_Does_Not_Crash()
    {
        var dto = (await HandlerWithNoPublishedSets().Handle(new GetTerritoryContractQuery(), default)).Data!;

        Assert.False(dto.IsReady);
        Assert.All(dto.RequiredReferenceSets, r => Assert.False(r.Ready));
        Assert.Equal(TerritoryReferenceSets.Required.Count, dto.MissingRequiredReferenceSets.Count);
    }

    [Fact]
    public async Task Contract_Exposes_Only_The_Five_Fu01_Permissions()
    {
        var dto = (await HandlerWithNoPublishedSets().Handle(new GetTerritoryContractQuery(), default)).Data!;

        Assert.Equal(TerritoryPermissions.All, dto.Permissions);
    }
}
