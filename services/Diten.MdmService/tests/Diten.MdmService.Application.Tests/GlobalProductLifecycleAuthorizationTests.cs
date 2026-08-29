using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Commands;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Handlers.CommandHandlers;
using Diten.MdmService.Domain.Enums;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class GlobalProductLifecycleAuthorizationTests
{
    [Fact]
    public async Task Submit_without_exact_permission_is_forbidden_before_repository_access()
    {
        var repository = new LifecycleTestGlobalProductRepository(
            LifecycleTestData.Product(ProductIdentityLifecycleStatus.Draft, 0));
        var handler = new SubmitGlobalProductIdentityHandler(
            repository,
            new LifecycleTestActor(LifecycleTestData.Maker));

        var response = await handler.Handle(
            new SubmitGlobalProductIdentityCommand(new(
                LifecycleTestData.ProductId,
                0,
                LifecycleTestData.Binding())),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(403, response.StatusCode);
        Assert.Contains("PRODUCT_IDENTITY_LIFECYCLE_FORBIDDEN", response.Errors);
        Assert.Equal(0, repository.SubmitCalls);
    }

    [Fact]
    public async Task Submit_with_unresolvable_actor_is_forbidden_before_repository_access()
    {
        var repository = new LifecycleTestGlobalProductRepository(
            LifecycleTestData.Product(ProductIdentityLifecycleStatus.Draft, 0));
        var actor = new LifecycleTestActor(
            LifecycleTestData.Maker,
            ProductIdentityLifecyclePermissions.GlobalProductSubmit)
        {
            IsResolvable = false
        };
        var handler = new SubmitGlobalProductIdentityHandler(repository, actor);

        var response = await handler.Handle(
            new SubmitGlobalProductIdentityCommand(new(
                LifecycleTestData.ProductId,
                0,
                LifecycleTestData.Binding())),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(403, response.StatusCode);
        Assert.Equal(0, repository.SubmitCalls);
    }

    [Fact]
    public async Task Retire_without_exact_permission_is_forbidden_before_repository_access()
    {
        var repository = new LifecycleTestGlobalProductRepository(
            LifecycleTestData.Product(ProductIdentityLifecycleStatus.IdentityApproved, 2));
        var handler = new RetireGlobalProductIdentityHandler(
            repository,
            new LifecycleTestActor(LifecycleTestData.Approver),
            TimeProvider.System);

        var response = await handler.Handle(
            new RetireGlobalProductIdentityCommand(new(
                LifecycleTestData.ProductId,
                2,
                Guid.NewGuid(),
                "IDENTITY_RETIRED",
                null)),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(403, response.StatusCode);
        Assert.Equal(0, repository.RetireCalls);
    }
}
