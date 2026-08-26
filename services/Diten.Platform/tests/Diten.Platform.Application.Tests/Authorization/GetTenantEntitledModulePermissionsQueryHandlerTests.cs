using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements;
using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Queries;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Authorization;

public sealed class GetTenantEntitledModulePermissionsQueryHandlerTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task Successful_empty_is_an_authoritative_empty_result()
    {
        var fixture = Build(Response<IReadOnlyList<TenantModuleEntitlementRowDto>>.Success([]));

        var result = await fixture.Handler.Handle(new(TenantId), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
    }

    [Fact]
    public async Task Provider_failure_is_not_converted_to_successful_empty()
    {
        var fixture = Build(Response<IReadOnlyList<TenantModuleEntitlementRowDto>>.Fail(
            "catalog unavailable", 503, "catalog_unavailable", "corr-1"));

        var result = await fixture.Handler.Handle(new(TenantId), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(503, result.StatusCode);
        Assert.Equal("catalog_unavailable", result.ReasonCode);
        Assert.Equal("corr-1", result.CorrelationId);
        Assert.Equal(new[] { "catalog unavailable" }, result.Errors);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task Null_projection_data_is_unavailable_not_authoritative_empty()
    {
        var fixture = Build(Response<IReadOnlyList<TenantModuleEntitlementRowDto>>.Success());

        var result = await fixture.Handler.Handle(new(TenantId), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(503, result.StatusCode);
        Assert.Equal("tenant_entitlement_projection_unavailable", result.ReasonCode);
        Assert.Null(result.Data);
    }

    [Theory]
    [InlineData(TenantModuleEffectiveAccess.Active, true)]
    [InlineData(TenantModuleEffectiveAccess.EnabledByOverride, true)]
    [InlineData(TenantModuleEffectiveAccess.BlockedByOverride, false)]
    [InlineData(TenantModuleEffectiveAccess.Expired, false)]
    [InlineData(TenantModuleEffectiveAccess.NoAccess, false)]
    public async Task Authoritative_projection_uses_canonical_HasAccess(
        TenantModuleEffectiveAccess access,
        bool hasAccess)
    {
        var fixture = Build(Response<IReadOnlyList<TenantModuleEntitlementRowDto>>.Success(
            [Row("product-item-sku-master", access.ToString())]));
        fixture.Mediator
            .Setup(x => x.Send(
                It.Is<GetTenantModuleEffectiveAccessQuery>(q =>
                    q.TenantId == TenantId && q.ModuleCode == "product-item-sku-master"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Effective("product-item-sku-master", access, hasAccess));
        fixture.Pages
            .Setup(x => x.GetByModuleAsync("product-item-sku-master", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await fixture.Handler.Handle(new(TenantId), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(hasAccess ? 1 : 0, result.Data!.Count);
    }

    [Fact]
    public async Task Duplicate_and_blank_codes_are_normalized_and_descriptor_permissions_are_unioned()
    {
        var fixture = Build(Response<IReadOnlyList<TenantModuleEntitlementRowDto>>.Success(
            [Row("product-item-sku-master", "Active"), Row("PRODUCT-ITEM-SKU-MASTER", "EnabledByOverride"), Row(" ", "Active")]));
        fixture.Mediator
            .Setup(x => x.Send(It.IsAny<GetTenantModuleEffectiveAccessQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Effective("product-item-sku-master", TenantModuleEffectiveAccess.EnabledByOverride, true));
        var page = new ModulePageDescriptor
        {
            TenantId = Guid.Empty,
            ModuleCode = "product-item-sku-master",
            PageCode = "FINISHED_GOODS",
            DisplayName = "Finished Goods",
            RoutePath = "/MDM/FinishedGoods",
            RequiredPermission = " mdm.finished-goods.read "
        };
        fixture.Pages
            .Setup(x => x.GetByModuleAsync("product-item-sku-master", It.IsAny<CancellationToken>()))
            .ReturnsAsync([page]);
        fixture.Actions
            .Setup(x => x.GetByPageAsync(page.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ModulePageActionDescriptor
                {
                    TenantId = Guid.Empty,
                    PageDescriptorId = page.Id,
                    PermissionKey = "mdm.finished-goods.read"
                },
                new ModulePageActionDescriptor
                {
                    TenantId = Guid.Empty,
                    PageDescriptorId = page.Id,
                    PermissionKey = " mdm.finished-goods.create "
                }
            ]);

        var result = await fixture.Handler.Handle(new(TenantId), CancellationToken.None);

        var module = Assert.Single(result.Data!);
        Assert.Equal("product-item-sku-master", module.ModuleCode, ignoreCase: true);
        Assert.Equal(2, module.PermissionKeys.Count);
        Assert.Contains("mdm.finished-goods.read", module.PermissionKeys, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("mdm.finished-goods.create", module.PermissionKeys, StringComparer.OrdinalIgnoreCase);
        fixture.Mediator.Verify(
            x => x.Send(It.IsAny<GetTenantModuleEffectiveAccessQuery>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Effective_access_failure_is_propagated_before_descriptor_reads()
    {
        var fixture = Build(Response<IReadOnlyList<TenantModuleEntitlementRowDto>>.Success([Row("MDM", "Active")]));
        fixture.Mediator
            .Setup(x => x.Send(It.IsAny<GetTenantModuleEffectiveAccessQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response<TenantModuleEffectiveAccessDto>.Fail("access unavailable", 503));

        var result = await fixture.Handler.Handle(new(TenantId), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(503, result.StatusCode);
        fixture.Pages.Verify(
            x => x.GetByModuleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Cross_tenant_projection_row_fails_closed_before_effective_access_or_descriptor_reads()
    {
        var otherTenant = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var row = Row("MDM", "Active") with { TenantId = otherTenant };
        var fixture = Build(Response<IReadOnlyList<TenantModuleEntitlementRowDto>>.Success([row]));

        var result = await fixture.Handler.Handle(new(TenantId), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(503, result.StatusCode);
        fixture.Mediator.Verify(
            x => x.Send(It.IsAny<GetTenantModuleEffectiveAccessQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
        fixture.Pages.Verify(
            x => x.GetByModuleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Descriptor_reads_use_platform_scope_and_restore_the_original_tenant_scope()
    {
        var fixture = Build(Response<IReadOnlyList<TenantModuleEntitlementRowDto>>.Success([Row("MDM", "Active")]));
        fixture.TenantContext.SetTenant(TenantId);
        fixture.Mediator
            .Setup(x => x.Send(It.IsAny<GetTenantModuleEffectiveAccessQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Effective("MDM", TenantModuleEffectiveAccess.Active, true));
        fixture.Pages
            .Setup(x => x.GetByModuleAsync("MDM", It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                Assert.True(fixture.TenantContext.IsPlatformContext);
                Assert.Equal(Guid.Empty, fixture.TenantContext.TargetTenantId);
                return [];
            });

        var result = await fixture.Handler.Handle(new(TenantId), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.False(fixture.TenantContext.IsPlatformContext);
        Assert.Equal(TenantId, fixture.TenantContext.TenantId);
    }

    private static Fixture Build(Response<IReadOnlyList<TenantModuleEntitlementRowDto>> entitlements)
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(x => x.Send(
                It.Is<GetTenantModuleEntitlementsQuery>(q => q.TenantId == TenantId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(entitlements);
        return new Fixture(mediator, new Mock<IModulePageDescriptorRepository>(), new Mock<IModulePageActionDescriptorRepository>());
    }

    private static TenantModuleEntitlementRowDto Row(string code, string access) =>
        new(TenantId, code, code, "Plan", null, true, null, access, null, true, false, null, null);

    private static Response<TenantModuleEffectiveAccessDto> Effective(
        string code,
        TenantModuleEffectiveAccess access,
        bool hasAccess) =>
        Response<TenantModuleEffectiveAccessDto>.Success(
            new TenantModuleEffectiveAccessDto(TenantId, code, code, "test", access, hasAccess, null, null));

    private sealed class Fixture
    {
        public Fixture(
            Mock<IMediator> mediator,
            Mock<IModulePageDescriptorRepository> pages,
            Mock<IModulePageActionDescriptorRepository> actions)
        {
            Mediator = mediator;
            Pages = pages;
            Actions = actions;
            TenantContext = new TenantContext();
            Handler = new(mediator.Object, pages.Object, actions.Object, TenantContext);
        }

        public Mock<IMediator> Mediator { get; }
        public Mock<IModulePageDescriptorRepository> Pages { get; }
        public Mock<IModulePageActionDescriptorRepository> Actions { get; }
        public TenantContext TenantContext { get; }
        public GetTenantEntitledModulePermissionsQueryHandler Handler { get; }
    }
}
