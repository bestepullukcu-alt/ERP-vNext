using Diten.ManagementGovernanceService.Application.Features.Dws;
using Diten.ManagementGovernanceService.Application.Features.Dws.Commands;
using Diten.ManagementGovernanceService.Application.Features.Dws.Queries;
using Diten.ManagementGovernanceService.Application.Features.Dws.Validators;
using Diten.ManagementGovernanceService.Domain.Modules.Dws;
using Xunit;

namespace Diten.ManagementGovernanceService.Tests.Modules.Dws;

public sealed class DwsFunctionalValidatorTests
{
    private static readonly Guid Tenant = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Subject = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Actor = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public void All_fifteen_validators_accept_only_well_formed_typed_requests()
    {
        var definition = Guid.NewGuid();
        var node = Guid.NewGuid();
        var other = Guid.NewGuid();
        var commandContext = CommandContext();
        var queryContext = QueryContext();

        new CreateStructureValidator().Validate(new(new(new("ppm.external-context-reference", "1.0", ExternalContextKind.Project, Guid.NewGuid()), " Plan ", " Description "), commandContext));
        new UpdateStructureMetadataValidator().Validate(new(new(definition, "Plan", null, 1), commandContext));
        new AddStructureNodeValidator().Validate(new(new(definition, null, "ROOT", "Root", null, 0, 1), commandContext));
        new MoveStructureNodeValidator().Validate(new(new(definition, node, null, 0, 1), commandContext));
        new ReorderStructureNodeValidator().Validate(new(new(definition, node, 1, 1), commandContext));
        new RemoveStructureNodeValidator().Validate(new(new(definition, node, 1), commandContext));
        new AddStructuralDependencyValidator().Validate(new(new(definition, node, other, 1), commandContext));
        new RemoveStructuralDependencyValidator().Validate(new(new(definition, node, other, 1), commandContext));
        new CreateStructureBaselineValidator().Validate(new(new(definition, 1), commandContext));
        new CreateNextStructureRevisionValidator().Validate(new(new(definition, 1, null, 1), commandContext));
        new GetStructureByIdValidator().Validate(new(definition, queryContext));
        new GetStructureTreeValidator().Validate(new(definition, 1, queryContext));
        new ValidateStructureValidator().Validate(new(definition, null, queryContext));
        new CompareStructureRevisionsValidator().Validate(new(definition, 1, 2, queryContext));
        new CompareStructureBaselinesValidator().Validate(new(definition, 1, 2, queryContext));
    }

    [Fact]
    public void Trusted_context_keeps_subject_effective_and_delegated_actor_distinct()
    {
        new DwsTrustedActorContext(Tenant, Subject, Actor, Guid.NewGuid(), "key").RequireCommand();
        new DwsTrustedActorContext(Tenant, Subject, Actor, null, null).RequireQuery();

        AssertCode(DwsErrors.AuthenticationRequired, () => new DwsTrustedActorContext(Tenant, Subject, Subject, Subject, "key").RequireCommand());
        AssertCode(DwsErrors.AuthenticationRequired, () => new DwsTrustedActorContext(Tenant, Subject, Actor, Actor, "key").RequireCommand());
        AssertCode(DwsErrors.InvalidRequest, () => new DwsTrustedActorContext(Tenant, Subject, Actor, null, null).RequireCommand());
        AssertCode(DwsErrors.InvalidRequest, () => new DwsTrustedActorContext(Tenant, Subject, Actor, null, "query-key").RequireQuery());
    }

    [Fact]
    public void Validators_reject_empty_identity_invalid_ranges_self_edges_and_ambiguous_revision_source()
    {
        AssertCode(DwsErrors.InvalidRequest, () => new GetStructureByIdValidator().Validate(new(Guid.Empty, QueryContext())));
        AssertCode(DwsErrors.InvalidRequest, () => new UpdateStructureMetadataValidator().Validate(new(new(Guid.NewGuid(), "Plan", null, 0), CommandContext())));
        var same = Guid.NewGuid();
        AssertCode(DwsErrors.InvalidStructure, () => new AddStructuralDependencyValidator().Validate(new(new(Guid.NewGuid(), same, same, 1), CommandContext())));
        AssertCode(DwsErrors.InvalidRequest, () => new CompareStructureRevisionsValidator().Validate(new(Guid.NewGuid(), 1, 1, QueryContext())));
        AssertCode(DwsErrors.InvalidRequest, () => new CreateNextStructureRevisionValidator().Validate(new(new(Guid.NewGuid(), 1, 1, 1), CommandContext())));
    }

    private static DwsTrustedActorContext CommandContext() => new(Tenant, Subject, Actor, null, "idempotency-key");
    private static DwsTrustedActorContext QueryContext() => new(Tenant, Subject, Actor, null, null);
    private static void AssertCode(string code, Action action) => Assert.Equal(code, Assert.Throws<DwsValidationException>(action).Code);
}
