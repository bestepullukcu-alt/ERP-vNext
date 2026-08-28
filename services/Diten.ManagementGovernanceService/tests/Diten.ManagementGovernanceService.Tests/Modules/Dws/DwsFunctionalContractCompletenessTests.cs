using System.Reflection;
using Diten.ManagementGovernanceService.Application.Features.Dws;
using Diten.ManagementGovernanceService.Application.Features.Dws.Commands;
using Diten.ManagementGovernanceService.Application.Features.Dws.Queries;
using DwsAuthorizationManifest = Diten.ManagementGovernanceService.Application.Modules.Dws.DwsAuthorizationManifest;
using MediatR;
using Xunit;

namespace Diten.ManagementGovernanceService.Tests.Modules.Dws;

public sealed class DwsFunctionalContractCompletenessTests
{
    private static readonly Type[] Commands =
    [
        typeof(CreateStructureCommand), typeof(UpdateStructureMetadataCommand), typeof(AddStructureNodeCommand),
        typeof(MoveStructureNodeCommand), typeof(ReorderStructureNodeCommand), typeof(RemoveStructureNodeCommand),
        typeof(AddStructuralDependencyCommand), typeof(RemoveStructuralDependencyCommand),
        typeof(CreateStructureBaselineCommand), typeof(CreateNextStructureRevisionCommand)
    ];

    private static readonly Type[] Queries =
    [
        typeof(GetStructureByIdQuery), typeof(GetStructureTreeQuery), typeof(ValidateStructureQuery),
        typeof(CompareStructureRevisionsQuery), typeof(CompareStructureBaselinesQuery)
    ];

    [Fact]
    public void Functional_surface_has_exact_ten_commands_and_five_queries_with_typed_MediatR_results()
    {
        Assert.Equal(10, Commands.Length);
        Assert.Equal(5, Queries.Length);
        Assert.Equal(15, Commands.Concat(Queries).Select(type => type.Name).Distinct(StringComparer.Ordinal).Count());

        foreach (var request in Commands.Concat(Queries))
        {
            var mediatr = request.GetInterfaces().Single(type =>
                type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IRequest<>));
            var response = mediatr.GetGenericArguments().Single();
            Assert.Equal(typeof(Response<>), response.GetGenericTypeDefinition());
            Assert.NotEqual(typeof(DwsLocalResult), response.GetGenericArguments().Single());
        }
    }

    [Fact]
    public void Request_and_result_shapes_match_the_pack_allowlist()
    {
        AssertProperties<CreateStructureRequest>("Description", "ExternalContextReference", "Name");
        AssertProperties<UpdateStructureMetadataRequest>("Description", "ExpectedRevisionVersion", "Name", "StructureDefinitionId");
        AssertProperties<AddStructureNodeRequest>("Code", "Description", "ExpectedRevisionVersion", "ParentLogicalNodeId", "SiblingOrder", "StructureDefinitionId", "Title");
        AssertProperties<MoveStructureNodeRequest>("ExpectedRevisionVersion", "LogicalNodeId", "NewParentLogicalNodeId", "NewSiblingOrder", "StructureDefinitionId");
        AssertProperties<ReorderStructureNodeRequest>("ExpectedRevisionVersion", "LogicalNodeId", "SiblingOrder", "StructureDefinitionId");
        AssertProperties<RemoveStructureNodeRequest>("ExpectedRevisionVersion", "LogicalNodeId", "StructureDefinitionId");
        AssertProperties<AddStructuralDependencyRequest>("ExpectedRevisionVersion", "FromLogicalNodeId", "StructureDefinitionId", "ToLogicalNodeId");
        AssertProperties<RemoveStructuralDependencyRequest>("ExpectedRevisionVersion", "FromLogicalNodeId", "StructureDefinitionId", "ToLogicalNodeId");
        AssertProperties<CreateStructureBaselineRequest>("ExpectedRevisionVersion", "StructureDefinitionId");
        AssertProperties<CreateNextStructureRevisionRequest>("ExpectedDefinitionVersion", "SourceBaselineNumber", "SourceRevisionNumber", "StructureDefinitionId");

        AssertProperties<CreateStructureResult>("DefinitionVersion", "RevisionNumber", "RevisionVersion", "StructureDefinitionId");
        AssertProperties<UpdateStructureMetadataResult>("OutcomeKind", "RevisionNumber", "RevisionVersion", "StructureDefinitionId");
        AssertProperties<AddStructureNodeResult>("LogicalNodeId", "RevisionNumber", "RevisionVersion", "StructureDefinitionId");
        AssertProperties<MoveStructureNodeResult>("LogicalNodeId", "OutcomeKind", "ParentLogicalNodeId", "RevisionNumber", "RevisionVersion", "SiblingOrder", "StructureDefinitionId");
        AssertProperties<ReorderStructureNodeResult>("LogicalNodeId", "OutcomeKind", "RevisionNumber", "RevisionVersion", "SiblingOrder", "StructureDefinitionId");
        AssertProperties<RemoveStructureNodeResult>("LogicalNodeId", "Removed", "RevisionNumber", "RevisionVersion", "StructureDefinitionId");
        AssertProperties<AddStructuralDependencyResult>("FromLogicalNodeId", "RevisionNumber", "RevisionVersion", "StructureDefinitionId", "ToLogicalNodeId");
        AssertProperties<RemoveStructuralDependencyResult>("FromLogicalNodeId", "Removed", "RevisionNumber", "RevisionVersion", "StructureDefinitionId", "ToLogicalNodeId");
        AssertProperties<CreateStructureBaselineResult>("BaselineNumber", "CanonicalizationVersion", "ContentHash", "DefinitionVersion", "SourceRevisionNumber", "StructureDefinitionId");
        AssertProperties<CreateNextStructureRevisionResult>("DefinitionVersion", "NewRevisionNumber", "RevisionVersion", "StructureDefinitionId");
    }

    [Fact]
    public void Permission_and_self_registration_manifests_are_exact_and_two_way_complete()
    {
        Assert.Equal(15, DwsAuthorizationManifest.Entries.Count);
        Assert.Equal(6, DwsAuthorizationManifest.Entries.Select(entry => entry.Permission).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            Commands.Concat(Queries).Select(type => type.Name).Order(StringComparer.Ordinal),
            DwsAuthorizationManifest.Entries.Select(entry => entry.Operation).Order(StringComparer.Ordinal));
        Assert.Equal(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["CreateStructureCommand"] = "management-governance.dws.create",
                ["UpdateStructureMetadataCommand"] = "management-governance.dws.update",
                ["AddStructureNodeCommand"] = "management-governance.dws.update",
                ["MoveStructureNodeCommand"] = "management-governance.dws.update",
                ["ReorderStructureNodeCommand"] = "management-governance.dws.update",
                ["RemoveStructureNodeCommand"] = "management-governance.dws.update",
                ["AddStructuralDependencyCommand"] = "management-governance.dws.update",
                ["RemoveStructuralDependencyCommand"] = "management-governance.dws.update",
                ["CreateStructureBaselineCommand"] = "management-governance.dws.baseline",
                ["CreateNextStructureRevisionCommand"] = "management-governance.dws.update",
                ["GetStructureByIdQuery"] = "management-governance.dws.read",
                ["GetStructureTreeQuery"] = "management-governance.dws.read",
                ["ValidateStructureQuery"] = "management-governance.dws.validate",
                ["CompareStructureRevisionsQuery"] = "management-governance.dws.compare",
                ["CompareStructureBaselinesQuery"] = "management-governance.dws.compare"
            },
            DwsAuthorizationManifest.Entries.ToDictionary(entry => entry.Operation, entry => entry.Permission, StringComparer.Ordinal));

        var registration = DwsSelfRegistration.Contract;
        Assert.Equal("MOD-0354", registration.ModuleCode);
        Assert.Equal("tenant", registration.Shell);
        Assert.Equal("/management-governance/delivery-execution/structures", registration.RoutePath);
        Assert.Equal(
            DwsAuthorizationManifest.Entries.Select(entry => entry.Permission).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal),
            registration.Permissions);
    }

    private static void AssertProperties<T>(params string[] expected) => Assert.Equal(
        expected.Order(StringComparer.Ordinal),
        typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public).Select(property => property.Name).Order(StringComparer.Ordinal));
}
