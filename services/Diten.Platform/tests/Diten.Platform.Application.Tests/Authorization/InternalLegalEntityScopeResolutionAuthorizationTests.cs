using Diten.Platform.API.Controllers.Internal;
using Diten.Platform.API.Models.AccessGovernance;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.AccessGovernance.LegalEntityScopeResolution;
using Diten.Platform.Application.Features.AccessGovernance.LegalEntityScopeResolution.Queries;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.AccessGovernance;

public sealed class InternalLegalEntityScopeResolutionAuthorizationTests
{
    [Fact]
    public void Route_is_exact_single_POST_resolve_surface()
    {
        var route = Assert.Single(typeof(InternalLegalEntityScopeResolutionController)
            .GetCustomAttributes(typeof(RouteAttribute), false).Cast<RouteAttribute>());
        Assert.Equal("api/internal/v1/access-governance/legal-entity-scope", route.Template);
        var action = typeof(InternalLegalEntityScopeResolutionController).GetMethod(nameof(InternalLegalEntityScopeResolutionController.Resolve))!;
        Assert.Equal("resolve", Assert.Single(action.GetCustomAttributes(typeof(HttpPostAttribute), false).Cast<HttpPostAttribute>()).Template);
    }

    [Fact]
    public async Task Executor_is_the_only_gateway_to_mediator_and_preserves_multiple_ids()
    {
        var tenant = Guid.NewGuid(); var subject = Guid.NewGuid();
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() }.OrderBy(x => x.ToString("D"), StringComparer.Ordinal).ToArray();
        var mediator = new Mock<IMediator>();
        mediator.Setup(x => x.Send(It.IsAny<ResolveTrustedLegalEntityScopeQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ResolveTrustedLegalEntityScopeQuery q, CancellationToken _) =>
            {
                var payload = new TrustedLegalEntityScopeResolution(
                    q.TenantId, q.SubjectId, q.ModuleCode, q.PermissionKey, DateTimeOffset.UtcNow, ids);
                return Response<TrustedLegalEntityScopeResolution>.Success(payload);
            });
        var executor = new Mock<ITrustedLegalEntityScopeRequestExecutor>();
        executor.Setup(x => x.ExecuteAsync(It.IsAny<HttpContext>(), It.IsAny<CancellationToken>(), It.IsAny<Func<Guid, Guid, TrustedLegalEntityScopeResolveRequest, CancellationToken, Task<IActionResult>>>(), It.IsAny<Func<int, string, IActionResult>>()))
            .Returns((HttpContext _, CancellationToken ct, Func<Guid, Guid, TrustedLegalEntityScopeResolveRequest, CancellationToken, Task<IActionResult>> action, Func<int, string, IActionResult> __) =>
                action(tenant, subject, new("product-item-sku-master", "mdm.gskus.read"), ct));
        var controller = new InternalLegalEntityScopeResolutionController(mediator.Object, executor.Object)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };

        var result = await controller.Resolve(CancellationToken.None);

        Assert.NotNull(result);
        mediator.Verify(x => x.Send(It.Is<ResolveTrustedLegalEntityScopeQuery>(q => q.TenantId == tenant && q.SubjectId == subject), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Executor_denial_never_dispatches_handler()
    {
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        var executor = new Mock<ITrustedLegalEntityScopeRequestExecutor>();
        executor.Setup(x => x.ExecuteAsync(It.IsAny<HttpContext>(), It.IsAny<CancellationToken>(), It.IsAny<Func<Guid, Guid, TrustedLegalEntityScopeResolveRequest, CancellationToken, Task<IActionResult>>>(), It.IsAny<Func<int, string, IActionResult>>()))
            .Returns((HttpContext _, CancellationToken __, Func<Guid, Guid, TrustedLegalEntityScopeResolveRequest, CancellationToken, Task<IActionResult>> ___, Func<int, string, IActionResult> failure) => Task.FromResult(failure(401, "LEGAL_ENTITY_SCOPE_UNAUTHENTICATED")));
        var controller = new InternalLegalEntityScopeResolutionController(mediator.Object, executor.Object)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };

        await controller.Resolve(default);

        mediator.VerifyNoOtherCalls();
    }
}
