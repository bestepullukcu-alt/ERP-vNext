using Diten.ManagementGovernanceService.Application.Features.Dws;
using Xunit;

namespace Diten.ManagementGovernanceService.Tests.Modules.Dws;

public sealed class DwsFunctionalQueryProjectionTests
{
    [Fact]
    public void Query_DTOs_expose_only_logical_structural_identity_and_allowlisted_fields()
    {
        AssertProperties<StructureSummaryDto>("CurrentWorkingRevisionNumber", "DefinitionVersion", "ExternalContextReference", "LatestRevisionNumber", "StructureDefinitionId");
        AssertProperties<StructureNodeDto>("Code", "Description", "LogicalNodeId", "ParentLogicalNodeId", "SiblingOrder", "Title");
        AssertProperties<StructuralDependencyDto>("FromLogicalNodeId", "ToLogicalNodeId");
        AssertProperties<StructureTreeDto>("Dependencies", "IsSealed", "Metadata", "Nodes", "RevisionNumber", "RevisionVersion", "Summary");
        AssertProperties<StructureValidationDto>("IsValid", "Issues", "RevisionNumber", "StructureDefinitionId");
        AssertProperties<StructureComparisonDto>("Dependencies", "LeftRevisionNumber", "Nodes", "RightRevisionNumber", "StructureDefinitionId");
        AssertProperties<BaselineComparisonDto>("Dependencies", "LeftBaselineNumber", "LeftContentHash", "Nodes", "RightBaselineNumber", "RightContentHash", "StructureDefinitionId");

        var dtoTypes = new[] { typeof(StructureSummaryDto), typeof(StructureNodeDto), typeof(StructuralDependencyDto), typeof(StructureTreeDto), typeof(StructureValidationDto), typeof(StructureComparisonDto), typeof(BaselineComparisonDto) };
        Assert.DoesNotContain(dtoTypes.SelectMany(type => type.GetProperties()), property => property.Name is "Id" or "StructureRevisionId" or "TenantId" or "SecuritySubjectId" or "EffectiveActorId" or "DelegatedActorId");
    }

    [Fact]
    public void Validation_issue_and_comparison_kinds_are_closed_and_deterministically_orderable()
    {
        Assert.Equal(
            new[] { "DependencyCycle", "DuplicateDependency", "DuplicateNodeCode", "DuplicateSiblingOrder", "HierarchyCycle", "MissingDependencyEndpoint", "MissingParent", "SelfParent" },
            Enum.GetNames<StructureValidationIssueCode>().Order(StringComparer.Ordinal));

        var logicalIds = new[] { Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa") };
        Assert.Equal(logicalIds.Order(), logicalIds.Order().ToArray());
    }

    private static void AssertProperties<T>(params string[] expected) => Assert.Equal(
        expected.Order(StringComparer.Ordinal),
        typeof(T).GetProperties().Select(property => property.Name).Order(StringComparer.Ordinal));
}
