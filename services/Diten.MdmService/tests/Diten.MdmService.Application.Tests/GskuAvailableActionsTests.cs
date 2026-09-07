using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.ValueObjects;
using System.Reflection;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class GskuAvailableActionsTests
{
    [Fact]
    public void Draft_actions_are_permission_gated_and_ordered()
    {
        var gsku = new Gsku { LifecycleStatus = ProductIdentityLifecycleStatus.Draft };

        Assert.Equal(["DETAILS", "EDIT", "SUBMIT"], Actions(gsku,
            new Actor(Guid.NewGuid(), FirstGskuIdentityLifecyclePermissions.Update,
                FirstGskuIdentityLifecyclePermissions.Submit)));
        Assert.Equal(["DETAILS"], Actions(gsku, new Actor(Guid.NewGuid())));
    }

    [Fact]
    public void Withdrawal_is_exposed_only_to_the_original_human_submitter()
    {
        var maker = Guid.NewGuid();
        var gsku = new Gsku
        {
            LifecycleStatus = ProductIdentityLifecycleStatus.PendingIdentityApproval,
            IdentityWorkflowBinding = new FirstGskuIdentityWorkflowBinding { SubmitterSubjectId = maker }
        };

        Assert.Equal(["DETAILS", "WITHDRAW_APPROVAL"], Actions(gsku,
            new Actor(maker, FirstGskuIdentityLifecyclePermissions.Withdraw)));
        Assert.Equal(["DETAILS"], Actions(gsku,
            new Actor(Guid.NewGuid(), FirstGskuIdentityLifecyclePermissions.Withdraw)));
        Assert.Equal(["DETAILS"], Actions(gsku, new Actor(null,
            FirstGskuIdentityLifecyclePermissions.Withdraw)));
    }

    [Fact]
    public void Correction_is_exposed_only_for_approved_idle_gsku_with_exact_permission()
    {
        var gsku = new Gsku { LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved };
        var authorized = new Actor(Guid.NewGuid(), GskuCorrectionPermissions.Request);

        Assert.Equal(["DETAILS", "REQUEST_CORRECTION"], Actions(gsku, authorized));

        gsku.ActiveLifecycleOperation = new(GskuLifecycleOperationKind.Correction, Guid.NewGuid(), 0);
        Assert.Equal(["DETAILS"], Actions(gsku, authorized));

        gsku.ActiveLifecycleOperation = null;
        Assert.Equal(["DETAILS"], Actions(gsku, new Actor(Guid.NewGuid())));
        Assert.Equal(["DETAILS"], Actions(gsku,
            new Actor(null, GskuCorrectionPermissions.Request)));
    }

    [Fact]
    public void Retirement_request_is_exposed_only_for_approved_idle_gsku_with_exact_permission()
    {
        var gsku = new Gsku { LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved };
        var authorized = new Actor(Guid.NewGuid(), GskuRetirementRequestPermissions.Request);

        Assert.Equal(["DETAILS", "REQUEST_RETIREMENT"], Actions(gsku, authorized));

        gsku.ActiveLifecycleOperation = new(GskuLifecycleOperationKind.Correction, Guid.NewGuid(), 0);
        Assert.Equal(["DETAILS"], Actions(gsku, authorized));

        gsku.ActiveLifecycleOperation = null;
        Assert.Equal(["DETAILS"], Actions(gsku, new Actor(Guid.NewGuid())));
        Assert.Equal(["DETAILS"], Actions(gsku,
            new Actor(null, GskuRetirementRequestPermissions.Request)));
    }

    [Theory]
    [InlineData(ProductIdentityLifecycleStatus.Retired)]
    public void Non_green_lifecycle_actions_fail_closed(ProductIdentityLifecycleStatus status)
    {
        var gsku = new Gsku { LifecycleStatus = status };
        Assert.Equal(["DETAILS"], Actions(gsku, new Actor(Guid.NewGuid(),
            FirstGskuIdentityLifecyclePermissions.Update,
            FirstGskuIdentityLifecyclePermissions.Submit,
            FirstGskuIdentityLifecyclePermissions.Withdraw)));
    }

    private static IReadOnlyList<string> Actions(Gsku gsku, IProductIdentityLifecycleActorContext actor) =>
        Assert.IsAssignableFrom<IReadOnlyList<string>>(
            typeof(GetGskuByIdHandler)
                .GetMethod("BuildAvailableActions", BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(null, [gsku, actor]));

    private sealed class Actor(Guid? subjectId, params string[] permissions)
        : IProductIdentityLifecycleActorContext
    {
        public bool TryResolveCanonicalHumanSubject(out Guid resolved)
        {
            resolved = subjectId ?? Guid.Empty;
            return subjectId.HasValue;
        }

        public bool HasPermission(string permission) => permissions.Contains(permission, StringComparer.Ordinal);
    }
}
