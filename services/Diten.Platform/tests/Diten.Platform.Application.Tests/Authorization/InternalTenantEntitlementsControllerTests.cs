using Diten.Platform.API.Controllers.Internal;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements;
using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Queries;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Authorization;

public sealed class InternalTenantEntitlementsControllerTests
{
    private const string ApiKey = "test-internal-api-key";
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task Unauthorized_request_returns_401_without_dispatch()
    {
        var mediator = new Mock<IMediator>();
        var controller = Build(mediator, authorized: false);

        var result = await controller.GetEntitledModulesWithPermissions(TenantId, CancellationToken.None);

        Assert.Equal(401, Assert.IsType<UnauthorizedObjectResult>(result).StatusCode);
        mediator.Verify(
            x => x.Send(It.IsAny<GetTenantEntitledModulePermissionsQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Successful_empty_is_returned_as_authoritative_200()
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(x => x.Send(It.IsAny<GetTenantEntitledModulePermissionsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response<IReadOnlyList<TenantEntitledModulePermissionsDto>>.Success([]));
        var controller = Build(mediator);

        var result = await controller.GetEntitledModulesWithPermissions(TenantId, CancellationToken.None);

        var response = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<Response<IReadOnlyList<TenantEntitledModulePermissionsDto>>>(response.Value);
        Assert.True(envelope.IsSuccessful);
        Assert.NotNull(envelope.Data);
        Assert.Empty(envelope.Data);
    }

    [Fact]
    public async Task Application_failure_preserves_failure_status_and_envelope()
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(x => x.Send(It.IsAny<GetTenantEntitledModulePermissionsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response<IReadOnlyList<TenantEntitledModulePermissionsDto>>.Fail(
                "projection unavailable", 503, "projection_unavailable", "corr-2"));
        var controller = Build(mediator);

        var result = await controller.GetEntitledModulesWithPermissions(TenantId, CancellationToken.None);

        var response = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, response.StatusCode);
        var envelope = Assert.IsType<Response<IReadOnlyList<TenantEntitledModulePermissionsDto>>>(response.Value);
        Assert.False(envelope.IsSuccessful);
        Assert.Null(envelope.Data);
        Assert.Equal("projection_unavailable", envelope.ReasonCode);
        Assert.Equal("corr-2", envelope.CorrelationId);
    }

    [Fact]
    public async Task Legacy_endpoint_preserves_application_failure()
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(x => x.Send(It.IsAny<GetTenantModuleEntitlementsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response<IReadOnlyList<TenantModuleEntitlementRowDto>>.Fail("projection unavailable", 503));
        var controller = Build(mediator);

        var result = await controller.GetEntitledModules(TenantId, CancellationToken.None);

        var response = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, response.StatusCode);
        var envelope = Assert.IsType<Response<IReadOnlyList<string>>>(response.Value);
        Assert.False(envelope.IsSuccessful);
        Assert.Null(envelope.Data);
    }

    [Fact]
    public async Task Legacy_endpoint_uses_HasAccess_and_includes_enabled_override()
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(x => x.Send(It.IsAny<GetTenantModuleEntitlementsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response<IReadOnlyList<TenantModuleEntitlementRowDto>>.Success(
            [
                new(TenantId, "product-item-sku-master", "Product", "ManualOverride", null, true, null,
                    "EnabledByOverride", null, false, true, null, null)
            ]));
        mediator
            .Setup(x => x.Send(It.IsAny<GetTenantModuleEffectiveAccessQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response<TenantModuleEffectiveAccessDto>.Success(
                new TenantModuleEffectiveAccessDto(
                    TenantId,
                    "product-item-sku-master",
                    "Product",
                    "ManualOverride",
                    Diten.Platform.Domain.Enums.TenantModuleEffectiveAccess.EnabledByOverride,
                    true,
                    null,
                    null)));
        var controller = Build(mediator);

        var result = await controller.GetEntitledModules(TenantId, CancellationToken.None);

        var response = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<Response<IReadOnlyList<string>>>(response.Value);
        Assert.Equal(new[] { "product-item-sku-master" }, envelope.Data);
    }

    private static InternalTenantEntitlementsController Build(Mock<IMediator> mediator, bool authorized = true)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AuthService:InternalApiKey"] = ApiKey
            })
            .Build();
        var controller = new InternalTenantEntitlementsController(
            mediator.Object,
            configuration,
            NullLogger<InternalTenantEntitlementsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        if (authorized)
        {
            controller.Request.Headers["X-Internal-Api-Key"] = ApiKey;
        }

        return controller;
    }
}
