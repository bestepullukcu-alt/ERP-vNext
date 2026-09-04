using Diten.PpmService.Domain.Entities;
using Xunit;

namespace Diten.PpmService.Tests;

public sealed class DomainContractTests
{
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _actor = Guid.NewGuid();

    [Fact]
    public void Portfolio_lifecycle_is_closed_and_archived_is_not_referenceable()
    {
        var entity = new Portfolio(_tenant, _actor, " P-1 ", "Portfolio", null, null);

        Assert.Equal("P-1", entity.Code);
        Assert.True(entity.IsReferenceable);
        Assert.True(entity.CanTransitionTo(PortfolioLifecycleState.Active));
        entity.Transition(_actor, PortfolioLifecycleState.Active);
        entity.Transition(_actor, PortfolioLifecycleState.Archived);

        Assert.False(entity.IsReferenceable);
        Assert.False(entity.CanTransitionTo(PortfolioLifecycleState.Active));
        Assert.Throws<InvalidOperationException>(() => entity.Transition(_actor, PortfolioLifecycleState.Draft));
    }

    [Fact]
    public void Initiative_valid_and_invalid_transitions_are_deterministic()
    {
        var entity = new Initiative(_tenant, _actor, "I-1", "Initiative", null, null, null);

        Assert.True(entity.IsReferenceable);
        Assert.False(entity.CanTransitionTo(InitiativeLifecycleState.Completed));
        entity.Transition(_actor, InitiativeLifecycleState.Active);
        entity.Transition(_actor, InitiativeLifecycleState.OnHold);
        entity.Transition(_actor, InitiativeLifecycleState.Completed);

        Assert.False(entity.IsReferenceable);
        Assert.Throws<InvalidOperationException>(() => entity.Transition(_actor, InitiativeLifecycleState.Active));
    }

    [Fact]
    public void Program_soft_delete_is_idempotent_and_removes_referenceability()
    {
        var entity = new Diten.PpmService.Domain.Entities.Program(
            _tenant, _actor, "PRG-1", "Program", null, null, null);

        var before = entity.Version;
        entity.SoftDelete(_actor);
        var deletedVersion = entity.Version;
        entity.SoftDelete(_actor);

        Assert.True(entity.IsDeleted);
        Assert.NotNull(entity.DeletedAtUtc);
        Assert.Equal(before + 1, deletedVersion);
        Assert.Equal(deletedVersion, entity.Version);
        Assert.False(entity.IsReferenceable);
    }

    [Fact]
    public void Project_requires_exactly_one_typed_non_empty_parent()
    {
        Assert.Throws<ArgumentException>(() => new Project(
            _tenant, _actor, "P-1", "Project", null,
            ProjectParentType.Initiative, Guid.Empty, null));

        var parentId = Guid.NewGuid();
        var entity = new Project(
            _tenant, _actor, "P-1", "Project", null,
            ProjectParentType.Program, parentId, null);

        Assert.Equal(ProjectParentType.Program, entity.ParentType);
        Assert.Equal(parentId, entity.ParentId);
    }

    [Fact]
    public void Project_referenceability_is_derived_from_lifecycle_and_soft_delete()
    {
        var entity = new Project(
            _tenant, _actor, "P-1", "Project", null,
            ProjectParentType.Initiative, Guid.NewGuid(), null);

        Assert.True(entity.IsReferenceable);
        entity.Transition(_actor, ProjectLifecycleState.Planned);
        entity.Transition(_actor, ProjectLifecycleState.Active);
        entity.Transition(_actor, ProjectLifecycleState.Completed);
        Assert.False(entity.IsReferenceable);
    }

    [Fact]
    public void Text_is_trimmed_and_normalized_to_NFC()
    {
        var entity = new Portfolio(_tenant, _actor, " Cafe\u0301 ", " Name ", null, null);

        Assert.Equal("Café", entity.Code);
        Assert.Equal("Name", entity.Name);
        Assert.True(entity.Code.IsNormalized());
    }
}
