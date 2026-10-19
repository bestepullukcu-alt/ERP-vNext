using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.GlobalApplicability;
using Diten.Platform.Application.Features.ModuleCatalog;
using Diten.Platform.Application.Features.ModuleCatalog.Commands;
using Diten.Platform.Application.Features.ModuleCatalog.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.SubscriptionPlans;
using Diten.Platform.Application.Features.SubscriptionPlans.Commands;
using Diten.Platform.Application.Features.SubscriptionPlans.Handlers.CommandHandlers;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.GlobalApplicability;

/// <summary>
/// WP-PLATFORM-AUDIT-INTX-01 FIX2 — every plan and catalogue update hands its change (field names, scalar values before
/// and after) to the coordinator. Measured per handler with a coordinator that keeps what it was given; the delivery of
/// that change into audit_events is measured in TransactionOwnedAuditDeliveryMongoTests.
/// </summary>
public sealed class GlobalApplicabilityAuditChangeHandlerTests
{
    [Fact]
    public async Task The_seed_backfill_records_the_quota_map_it_filled_in()
    {
        var existing = new SubscriptionPlan { Code = "FREE", Name = "Free", IsActive = true, DefaultQuotas = null };
        var plans = PlanRepository(existing);
        var recorder = new RecordingCoordinator();

        await new SeedDefaultSubscriptionPlansCommandHandler(plans.Object, NullLogger<SeedDefaultSubscriptionPlansCommandHandler>.Instance, recorder, NoState())
            .Handle(new SeedDefaultSubscriptionPlansCommand(), CancellationToken.None);

        var change = Assert.Single(recorder.Changes.Where(c => c is not null))!;
        Assert.Equal(["DefaultQuotas"], change.ChangedFields);
        Assert.Equal(string.Empty, change.Before["DefaultQuotas"]);
        Assert.Contains("modules.max=", (string)change.After["DefaultQuotas"]!);
    }

    [Fact]
    public async Task Activating_a_plan_records_its_switch_before_and_after()
    {
        var plan = new SubscriptionPlan { Code = "PRO", Name = "Pro", IsActive = false };
        var recorder = new RecordingCoordinator();

        await new ActivateSubscriptionPlanCommandHandler(PlanRepository(plan).Object, NullLogger<ActivateSubscriptionPlanCommandHandler>.Instance, recorder, NoState())
            .Handle(new ActivateSubscriptionPlanCommand(plan.Id), CancellationToken.None);

        var change = Assert.Single(recorder.Changes)!;
        Assert.Equal(["IsActive"], change.ChangedFields);
        Assert.Equal(false, change.Before["IsActive"]);
        Assert.Equal(true, change.After["IsActive"]);
    }

    [Fact]
    public async Task A_catalogue_update_records_its_flags_and_names_the_operator_typed_fields_without_copying_them()
    {
        var item = new ModuleCatalogItem
        {
            ModuleCode = "CRM", ModuleName = "Old typed name", DisplayName = "Old display", Domain = "SALES", Service = "CRM",
            Status = ModuleCatalogStatus.Active, IsTenantAssignable = false, Icon = "bx bx-old", Origin = ModuleCatalogOrigin.Manual
        };
        var recorder = new RecordingCoordinator();
        var handler = new UpdateModuleCatalogItemCommandHandler(GlobalApplicabilityTestDependencies.Module(CatalogWith(item)), new Passthrough(), recorder, NoState());

        var result = await handler.Handle(new UpdateModuleCatalogItemCommand(item.Id, new UpdateModuleCatalogItemRequest(
            "CRM", "New typed name", "New display", null, "SALES", "CRM", "Active", item.ModuleVersion, false, true, 0, "bx bx-new")), CancellationToken.None);

        Assert.True(result.IsSuccessful, string.Join(";", result.Errors ?? []));
        var change = Assert.Single(recorder.Changes)!;
        Assert.Equal(["DisplayName", "Icon", "IsTenantAssignable", "ModuleName"], change.ChangedFields);
        Assert.Equal(false, change.Before["IsTenantAssignable"]);
        Assert.Equal(true, change.After["IsTenantAssignable"]);
        // Operator-typed text is named, never copied.
        foreach (var field in new[] { "DisplayName", "Icon", "ModuleName" })
        {
            Assert.False(change.Before.ContainsKey(field), field);
            Assert.False(change.After.ContainsKey(field), field);
        }
    }

    [Fact]
    public async Task Activating_a_catalogue_module_records_its_status_before_and_after()
    {
        var item = new ModuleCatalogItem { ModuleCode = "CRM", ModuleName = "Crm", DisplayName = "CRM", Status = ModuleCatalogStatus.Draft };
        var recorder = new RecordingCoordinator();
        var gate = new Mock<IWorkflowTransitionGate>();
        var handler = new ActivateModuleCatalogItemCommandHandler(GlobalApplicabilityTestDependencies.Module(CatalogWith(item)), gate.Object, recorder, NoState());

        var result = await handler.Handle(new ActivateModuleCatalogItemCommand(item.Id), CancellationToken.None);

        Assert.True(result.IsSuccessful, string.Join(";", result.Errors ?? []));
        var change = Assert.Single(recorder.Changes)!;
        Assert.Equal(["Status"], change.ChangedFields);
        Assert.Equal("Draft", change.Before["Status"]);
        Assert.Equal("Active", change.After["Status"]);
    }

    [Fact]
    public async Task A_code_list_is_recorded_sorted_whatever_order_it_was_given_in()
    {
        var plan = new SubscriptionPlan { Code = "PRO", Name = "Pro", IsActive = true, IncludedModuleKeys = ["CRM"] };
        var recorder = new RecordingCoordinator();

        await Update(plan, recorder, ["HR", "CRM", "ABC"]);

        var change = Assert.Single(recorder.Changes)!;
        Assert.Equal("CRM", change.Before["IncludedModuleKeys"]);
        Assert.Equal("ABC,CRM,HR", change.After["IncludedModuleKeys"]);
    }

    [Fact]
    public async Task The_same_codes_in_another_order_are_no_change_no_record_and_no_version()
    {
        var plan = new SubscriptionPlan { Code = "PRO", Name = "Pro", IsActive = true, IncludedModuleKeys = ["CRM", "HR"] };
        var recorder = new RecordingCoordinator();

        await Update(plan, recorder, ["HR", "CRM"]);

        Assert.Equal([false], recorder.Effective);
        Assert.Empty(recorder.Changes);
    }

    [Fact]
    public async Task The_same_features_in_another_order_are_no_change_no_record_and_no_version()
    {
        // INTX FIX3 — the feature list is a set too, like the module list.
        var plan = new SubscriptionPlan { Code = "PRO", Name = "Pro", IsActive = true, IncludedFeatures = ["export", "api"], IncludedModuleKeys = ["CRM"] };
        var recorder = new RecordingCoordinator();

        await Update(plan, recorder, ["CRM"], features: ["api", "export"]);

        Assert.Equal([false], recorder.Effective);
        Assert.Empty(recorder.Changes);
    }

    private static Task Update(SubscriptionPlan plan, RecordingCoordinator recorder, IReadOnlyList<string> modules, IReadOnlyList<string>? features = null) =>
        new UpdateSubscriptionPlanCommandHandler(PlanRepository(plan).Object, NullLogger<UpdateSubscriptionPlanCommandHandler>.Instance, recorder, NoState())
            .Handle(new UpdateSubscriptionPlanCommand(plan.Id, new UpdateSubscriptionPlanRequest(
                plan.Code, plan.Name, plan.Description, plan.IsActive, plan.IsDefault, plan.SortOrder, plan.PriceMonthly, plan.PriceYearly,
                plan.Currency, plan.IsTrialPlan, plan.TrialDurationDays, plan.DefaultQuotas, features ?? plan.IncludedFeatures, modules)), CancellationToken.None);

    private static Mock<ITransactionalSubscriptionPlanRepository> PlanRepository(SubscriptionPlan plan)
    {
        var plans = new Mock<ITransactionalSubscriptionPlanRepository>();
        plans.Setup(x => x.GetByIdAsync(It.IsAny<IPlatformTransactionSession>(), plan.Id, It.IsAny<CancellationToken>())).ReturnsAsync(plan);
        plans.Setup(x => x.GetByCodeAsync(It.IsAny<IPlatformTransactionSession>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IPlatformTransactionSession _, string code, CancellationToken _) => code == plan.Code ? plan : null);
        return plans;
    }

    private static IModuleCatalogRepository CatalogWith(ModuleCatalogItem item)
    {
        var catalog = new Mock<IModuleCatalogRepository>();
        catalog.Setup(x => x.GetByIdAsync(item.Id, It.IsAny<CancellationToken>())).ReturnsAsync(item);
        return catalog.Object;
    }

    private static IGlobalApplicabilityStateRepository NoState() => new Mock<IGlobalApplicabilityStateRepository>().Object;

    private sealed class Passthrough : Diten.Platform.Application.Features.ModuleCatalog.Services.IModuleTaxonomyResolver
    {
        public Task<string> ResolveDomainCodeAsync(string? rawDomain, CancellationToken ct = default) => Task.FromResult(rawDomain?.Trim() ?? string.Empty);
        public Task<string> ResolveServiceCodeAsync(string? rawService, CancellationToken ct = default) => Task.FromResult(rawService?.Trim() ?? string.Empty);
    }

    /// <summary>Runs each body once and keeps whether it changed anything and the change it reported.</summary>
    private sealed class RecordingCoordinator : IGlobalApplicabilityTransactionCoordinator
    {
        private sealed class Session : IPlatformTransactionSession { public Guid TransactionId { get; } = Guid.NewGuid(); }

        public List<GlobalApplicabilityAuditChange?> Changes { get; } = [];
        public List<bool> Effective { get; } = [];

        public async Task<T> ExecuteAsync<T>(GlobalApplicabilityMutationDescriptor descriptor,
            Func<IPlatformTransactionSession, CancellationToken, Task<GlobalApplicabilityMutation<T>>> body, CancellationToken cancellationToken = default)
        {
            var mutation = await body(new Session(), cancellationToken);
            Effective.Add(mutation.EffectiveStateChanged);
            if (mutation.EffectiveStateChanged)
            {
                Changes.Add(mutation.AuditChange);
            }

            return mutation.Result;
        }

        public Task<T> ExecuteBatchAsync<T>(Func<IPlatformTransactionSession, CancellationToken, Task<GlobalApplicabilityBatchMutation<T>>> body,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
