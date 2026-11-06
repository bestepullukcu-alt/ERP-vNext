extern alias PlatformProducer;

using Diten.MdmService.Api.ModuleRegistration;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using PlatformCommon = PlatformProducer::Diten.Platform.Application.Common;
using PlatformContracts = PlatformProducer::Diten.Platform.Application.Contracts;
using PlatformEntitlements = PlatformProducer::Diten.Platform.Application.Features.Tenants.Commercial.Entitlements;
using PlatformEntitlementQueries = PlatformProducer::Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Queries;
using PlatformEntitlementHandlers = PlatformProducer::Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Handlers.QueryHandlers;
using PlatformGlobalApplicability = PlatformProducer::Diten.Platform.Application.Features.GlobalApplicability;
using PlatformModuleCatalog = PlatformProducer::Diten.Platform.Application.Features.ModuleCatalog.Services;
using PlatformRegistration = PlatformProducer::Diten.Platform.Application.Features.ModuleRegistration;

namespace Diten.MdmService.Application.Tests.ModuleRegistration;

/// <summary>
/// MOD-0290-FU06 T3 / T4-b (pack §18) — the PRODUCTION MDM manifest walks Platform's two PRODUCTION handlers:
/// <c>RegisterModuleManifestCommandHandler</c> (the push; the protected-owner guard runs for real) and
/// <c>GetTenantEntitledModulePermissionsQueryHandler</c> (the entitled read set Auth's exact-set check receives).
/// Nothing is copied: the manifest comes from <see cref="ProductItemSkuMasterManifestProvider"/>, the handlers from
/// Platform.Application (test-only reference, aliased — Platform.Application and MDM Infrastructure share the full name
/// <c>Diten.Platform.Application.Contracts.Eventing.EventTransportMessage</c>). Only the repositories, the Auth sync,
/// the taxonomy, the transaction and the entitlement decision are doubles (pack §18 table): the entitlement decision is
/// not measured here — the mediator says "entitled". The key is written out, never read from the constant.
/// </summary>
public sealed class ProductMasterReadKeyPlatformPathTests
{
    private const string ReadKey = "mdm.product-master.read";
    private const string ModuleOwner = "PRODUCT-ITEM-SKU-MASTER";
    private static readonly Guid TenantId = Guid.Parse("f0060000-0000-4000-8000-000000000001");

    [Fact]
    public async Task Production_manifest_syncs_the_read_key_with_the_module_owner_and_tenant_scope()
    {
        var push = await PushProductionManifestAsync();

        Assert.True(push.Result.IsSuccessful);
        Assert.Equal(ModuleOwner, push.Result.Data!.ModuleCode);
        Assert.Empty(push.Result.Data.PagesSkipped);
        Assert.Single(push.Synced, call => call == (ReadKey, ModuleOwner, "Tenant"));
        Assert.DoesNotContain(push.Synced, call => call.Key == ReadKey && call != (ReadKey, ModuleOwner, "Tenant"));

        // F-14: the stores keep what the handler created; page ids are unique and every action hangs off a stored page.
        Assert.Equal(push.Pages.Count, push.Pages.Select(page => page.Id).Distinct().Count());
        var pageIds = push.Pages.Select(page => page.Id).ToHashSet();
        Assert.All(push.Actions, action => Assert.Contains(action.PageDescriptorId, pageIds));
    }

    [Fact]
    public async Task Every_key_synced_under_the_module_is_in_the_entitled_read_set()
    {
        var push = await PushProductionManifestAsync();
        Assert.True(push.Result.IsSuccessful);

        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        mediator
            .Setup(x => x.Send(
                It.Is<PlatformEntitlementQueries.GetTenantModuleEntitlementsQuery>(q => q.TenantId == TenantId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(PlatformCommon.Response<IReadOnlyList<PlatformEntitlements.TenantModuleEntitlementRowDto>>.Success(
                new PlatformEntitlements.TenantModuleEntitlementRowDto[]
                {
                    new(TenantId, "product-item-sku-master", "product-item-sku-master", "Addon", null, true, null,
                        nameof(TenantModuleEffectiveAccess.Active), null, true, false, null, null)
                }));
        mediator
            .Setup(x => x.Send(
                It.Is<PlatformEntitlementQueries.GetTenantModuleEffectiveAccessQuery>(q =>
                    q.TenantId == TenantId && q.ModuleCode == "product-item-sku-master"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(PlatformCommon.Response<PlatformEntitlements.TenantModuleEffectiveAccessDto>.Success(
                new PlatformEntitlements.TenantModuleEffectiveAccessDto(TenantId, "product-item-sku-master", "product-item-sku-master", "Addon",
                    TenantModuleEffectiveAccess.Active, true, null, null)));

        var handler = new PlatformEntitlementHandlers.GetTenantEntitledModulePermissionsQueryHandler(
            mediator.Object, push.PageRepository, push.ActionRepository, new TenantContext());
        var answer = await handler.Handle(new PlatformEntitlementQueries.GetTenantEntitledModulePermissionsQuery(TenantId), CancellationToken.None);

        Assert.True(answer.IsSuccessful);
        var module = Assert.Single(answer.Data!);
        Assert.Contains(ReadKey, module.PermissionKeys, StringComparer.OrdinalIgnoreCase);

        // The input of Auth's GrantModuleWithKeysAsync exact-set check: what Platform synced under this module's owner
        // must be exactly what the entitled read returns for the module.
        var syncedUnderModule = push.Synced
            .Where(call => string.Equals(call.Owner, ModuleOwner, StringComparison.Ordinal))
            .Select(call => call.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains(ReadKey, syncedUnderModule);
        Assert.True(
            syncedUnderModule.SetEquals(module.PermissionKeys),
            "Synced under the module but not entitled: " + string.Join(", ", syncedUnderModule.Except(module.PermissionKeys, StringComparer.OrdinalIgnoreCase))
            + " | entitled but not synced under the module: " + string.Join(", ", module.PermissionKeys.Except(syncedUnderModule, StringComparer.OrdinalIgnoreCase)));
    }

    private static async Task<Push> PushProductionManifestAsync()
    {
        var pages = new List<ModulePageDescriptor>();
        var actions = new List<ModulePageActionDescriptor>();
        var synced = new List<(string? Key, string? Owner, string? Scope)>();

        var catalog = new Mock<ITransactionalModuleCatalogRepository>(MockBehavior.Strict);
        catalog.Setup(x => x.GetByCodeIncludingDeletedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ModuleCatalogItem?)null);
        catalog.Setup(x => x.GetByCodeAsync(It.IsAny<IPlatformTransactionSession>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ModuleCatalogItem?)null);
        catalog.Setup(x => x.CreateAsync(It.IsAny<IPlatformTransactionSession>(), It.IsAny<ModuleCatalogItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IPlatformTransactionSession _, ModuleCatalogItem item, CancellationToken _) => item);

        // In-memory stores: they assign NO id (BaseEntity's initializer does) and keep the very object they are given.
        var pageRepository = new Mock<IModulePageDescriptorRepository>(MockBehavior.Strict);
        pageRepository.Setup(x => x.GetByModuleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string moduleCode, CancellationToken _) => pages
                .Where(page => !page.IsDeleted && string.Equals(page.ModuleCode, moduleCode, StringComparison.OrdinalIgnoreCase))
                .ToList());
        pageRepository.Setup(x => x.CreateAsync(It.IsAny<ModulePageDescriptor>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ModulePageDescriptor page, CancellationToken _) => { pages.Add(page); return page; });
        pageRepository.Setup(x => x.UpdateAsync(It.IsAny<ModulePageDescriptor>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        pageRepository.Setup(x => x.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid id, CancellationToken _) => { pages.Single(page => page.Id == id).IsDeleted = true; return Task.CompletedTask; });

        var actionRepository = new Mock<IModulePageActionDescriptorRepository>(MockBehavior.Strict);
        actionRepository.Setup(x => x.GetByPageAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid pageId, CancellationToken _) => actions
                .Where(action => !action.IsDeleted && action.PageDescriptorId == pageId)
                .ToList());
        actionRepository.Setup(x => x.CreateAsync(It.IsAny<ModulePageActionDescriptor>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ModulePageActionDescriptor action, CancellationToken _) => { actions.Add(action); return action; });
        actionRepository.Setup(x => x.UpdateAsync(It.IsAny<ModulePageActionDescriptor>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        actionRepository.Setup(x => x.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid id, CancellationToken _) => { actions.Single(action => action.Id == id).IsDeleted = true; return Task.CompletedTask; });

        var permissionSync = new Mock<PlatformContracts.ICatalogPermissionSyncService>(MockBehavior.Strict);
        permissionSync
            .Setup(x => x.SyncPermissionAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string? key, string? _, string? owner, string? scope, CancellationToken _) =>
            {
                synced.Add((key, owner, scope));
                return PlatformContracts.CatalogPermissionSyncStatus.Synced;
            });

        var taxonomy = new Mock<PlatformModuleCatalog.IModuleTaxonomyResolver>(MockBehavior.Strict);
        taxonomy.Setup(x => x.ResolveServiceCodeAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string? raw, CancellationToken _) => raw ?? string.Empty);

        var domains = new Mock<IModuleDomainRepository>(MockBehavior.Strict);
        domains.Setup(x => x.QueryAsync(It.IsAny<ModuleDomainQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<ModuleDomain>)[new ModuleDomain { Code = "MASTERDATAMANAGEMENT", DisplayName = "Master Data Management" }], 1L));

        var applicabilityState = new Mock<IGlobalApplicabilityStateRepository>(MockBehavior.Strict);
        applicabilityState
            .Setup(x => x.UpsertModuleCatalogAsync(It.IsAny<IPlatformTransactionSession>(), It.IsAny<ModuleCatalogItem>(), It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new PlatformRegistration.RegisterModuleManifestCommandHandler(
            catalog.Object,
            pageRepository.Object,
            actionRepository.Object,
            permissionSync.Object,
            taxonomy.Object,
            domains.Object,
            NullLogger<PlatformRegistration.RegisterModuleManifestCommandHandler>.Instance,
            new InlineCoordinator(),
            applicabilityState.Object);

        var result = await handler.Handle(
            new PlatformRegistration.RegisterModuleManifestCommand(
                new ProductItemSkuMasterManifestProvider().GetManifest(),
                TrustedProducerOwnerCode: "DITENMDMSERVICE",
                PushedOverInternalEndpoint: true),
            CancellationToken.None);

        return new Push(result, pages, actions, synced, pageRepository.Object, actionRepository.Object);
    }

    private sealed record Push(
        PlatformCommon.Response<PlatformRegistration.ModuleManifestReconcileResult> Result,
        IReadOnlyList<ModulePageDescriptor> Pages,
        IReadOnlyList<ModulePageActionDescriptor> Actions,
        IReadOnlyList<(string? Key, string? Owner, string? Scope)> Synced,
        IModulePageDescriptorRepository PageRepository,
        IModulePageActionDescriptorRepository ActionRepository);

    private sealed class Session : IPlatformTransactionSession
    {
        public Guid TransactionId { get; } = Guid.NewGuid();
    }

    /// <summary>
    /// Runs the body inline and writes the projection when the state changed — written here because Platform's own
    /// GlobalApplicabilityTestDependencies.InlineCoordinator is private to the Platform test project.
    /// </summary>
    private sealed class InlineCoordinator : PlatformGlobalApplicability.IGlobalApplicabilityTransactionCoordinator
    {
        public async Task<T> ExecuteAsync<T>(
            PlatformGlobalApplicability.GlobalApplicabilityMutationDescriptor descriptor,
            Func<IPlatformTransactionSession, CancellationToken, Task<PlatformGlobalApplicability.GlobalApplicabilityMutation<T>>> body,
            CancellationToken cancellationToken = default)
        {
            var session = new Session();
            var mutation = await body(session, cancellationToken);
            if (mutation.EffectiveStateChanged && mutation.WriteProjectionAsync is not null)
            {
                await mutation.WriteProjectionAsync(session, 1, cancellationToken);
            }

            return mutation.Result;
        }

        public async Task<T> ExecuteBatchAsync<T>(
            Func<IPlatformTransactionSession, CancellationToken, Task<PlatformGlobalApplicability.GlobalApplicabilityBatchMutation<T>>> body,
            CancellationToken cancellationToken = default)
        {
            var session = new Session();
            var mutation = await body(session, cancellationToken);
            ulong version = 0;
            foreach (var change in mutation.EffectiveChanges)
            {
                await change.WriteProjectionAsync(session, ++version, cancellationToken);
            }

            return mutation.Result;
        }
    }
}

/// <summary>
/// F-8 — CompositionRootGuardTests scans the Diten.* assemblies loaded in the process. Loading Platform.Application
/// when this test assembly loads makes that scan the same whatever order the tests run in (alone or with T3 / T4-b).
/// </summary>
internal static class PlatformProducerAssemblyLoader
{
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void Load() =>
        _ = typeof(PlatformProducer::Diten.Platform.Application.Features.ModuleRegistration.RegisterModuleManifestCommandHandler).Assembly;
}
