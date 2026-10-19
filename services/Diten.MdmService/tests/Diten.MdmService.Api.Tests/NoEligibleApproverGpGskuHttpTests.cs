using System.Net;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Persistence.Repositories;
using MongoDB.Driver;
using Xunit;
using Obs = Diten.MdmService.Api.Tests.NoEligibleApproverObservations;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// WP-MDM-NO-ELIGIBLE-APPROVER-01 over HTTP — Platform refuses a start because the maker would be the approval's only
/// eligible approver (segregation of duties; 400 WORKFLOW_NO_ELIGIBLE_APPROVER, nothing written). Every flow answers
/// 409 PRODUCT_IDENTITY_WORKFLOW_NO_ELIGIBLE_APPROVER, nothing is quarantined and no retry is planned. An IDENTITY
/// flow waits with its maker (AwaitingMakerReplay, the record a draft); a REQUEST flow gives its lock back (None).
/// </summary>
public sealed partial class GskuLifecycleHttpTests
{
    private static readonly string[] NeapGpPermissions =
    [
        "mdm.global-products.read", "mdm.global-products.submit", "mdm.global-products.request-correction",
        "mdm.global-products.request-retirement"
    ];

    private void NeapRefuseTheNextStart() =>
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Invalid, "WORKFLOW_NO_ELIGIBLE_APPROVER", false);

    private static void NeapAssertAnswer((int Status, string[] Errors) answer)
    {
        Assert.Equal(StatusCodes409, answer.Status);
        Assert.Equal(ProductIdentityWorkflowMakerCodes.NoEligibleApprover, answer.Errors.FirstOrDefault());
    }

    // The Global Product screen reads a 409 only when it carries a single error (GlobalProductsController).
    private static void NeapAssertGpAnswer((int Status, string[] Errors) answer)
    {
        Assert.Equal(StatusCodes409, answer.Status);
        Assert.Equal(ProductIdentityWorkflowMakerCodes.NoEligibleApprover, Assert.Single(answer.Errors));
    }

    private const int StatusCodes409 = 409;

    [Fact]
    public async Task NEAP_GP_identity_submit_waits_with_its_maker_and_says_no_eligible_approver()
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.Draft);
        NeapRefuseTheNextStart();

        var answer = await Obs.ReadAsync(await SendAsync(HttpMethod.Post, $"/api/global-products/{product:D}/submit",
            new { expectedVersion = 0 }, host.Token(_tenant, _maker, NeapGpPermissions)));

        var op = Assert.Single(await host.Database.GetCollection<GlobalProductIdentityWorkflowOperation>(
                GlobalProductIdentityWorkflowOperationRepository.CollectionName)
            .Find(x => x.TenantId == _tenant && x.GlobalProductId == product).ToListAsync());
        var gp = await NeapGpAsync(product);
        Obs.Record("GP kimlik", answer.Status, answer.Errors, op.Checkpoint, op.RecoveryDisposition, op.LastFailureCode,
            op.NextAttemptAtUtcTicksV1, $"GP {gp.LifecycleStatus} v{gp.Version}");
        NeapAssertGpAnswer(answer);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.AwaitingMakerReplay, op.Checkpoint);
        Assert.NotEqual(ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired, op.RecoveryDisposition);
        Assert.Null(op.NextAttemptAtUtcTicksV1);
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, gp.LifecycleStatus);
    }

    [Fact]
    public async Task NEAP_GP_correction_request_gives_the_lock_back_and_says_no_eligible_approver()
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved);
        NeapRefuseTheNextStart();

        var answer = await Obs.ReadAsync(await SendAsync(HttpMethod.Post, $"/api/global-products/{product:D}/correction-requests",
            new { expectedVersion = 0, globalProductName = $"Corrected {product:N}" }, host.Token(_tenant, _maker, NeapGpPermissions)));

        var op = Assert.Single(await host.Database.GetCollection<GlobalProductCorrectionOperation>(
                GlobalProductCorrectionOperationRepository.CollectionName)
            .Find(x => x.TenantId == _tenant && x.GlobalProductId == product).ToListAsync());
        var gp = await NeapGpAsync(product);
        Obs.Record("GP düzeltme", answer.Status, answer.Errors, op.Checkpoint, op.RecoveryDisposition, op.LastFailureCode,
            op.NextAttemptAtUtcTicksV1, $"GP {gp.LifecycleStatus} lock={(gp.ActiveLifecycleOperation is null ? "free" : "held")}");
        NeapAssertGpAnswer(answer);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.None, op.RecoveryDisposition);
        Assert.Null(op.NextAttemptAtUtcTicksV1);
        Assert.Null(gp.ActiveLifecycleOperation);
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, gp.LifecycleStatus);
    }

    [Fact]
    public async Task NEAP_GP_retirement_request_gives_the_lock_back_and_says_no_eligible_approver()
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved);
        NeapRefuseTheNextStart();

        var answer = await Obs.ReadAsync(await GprHttpAsync(product, Guid.NewGuid()));

        var op = Assert.Single(await host.Database.GetCollection<GlobalProductRetirementRequestOperation>(
                GlobalProductRetirementRequestOperationRepository.CollectionName)
            .Find(x => x.TenantId == _tenant && x.GlobalProductId == product).ToListAsync());
        var gp = await NeapGpAsync(product);
        Obs.Record("GP emeklilik", answer.Status, answer.Errors, op.Checkpoint, op.RecoveryDisposition, op.LastFailureCode,
            op.NextAttemptAtUtcTicksV1, $"GP {gp.LifecycleStatus} lock={(gp.ActiveLifecycleOperation is null ? "free" : "held")}");
        NeapAssertGpAnswer(answer);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.None, op.RecoveryDisposition);
        Assert.Null(op.NextAttemptAtUtcTicksV1);
        Assert.Null(gp.ActiveLifecycleOperation);
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, gp.LifecycleStatus);
    }

    [Fact]
    public async Task NEAP_first_GSKU_identity_submit_waits_with_its_maker_and_says_no_eligible_approver()
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved);
        var (gsku, version) = await CreateAsync(product);
        NeapRefuseTheNextStart();

        var answer = await Obs.ReadAsync(await SubmitAsync(gsku, version));

        var op = Assert.Single(await host.Database.GetCollection<FirstGskuIdentityWorkflowOperation>(
                FirstGskuIdentityWorkflowOperationRepository.CollectionName)
            .Find(x => x.TenantId == _tenant && x.GskuId == gsku).ToListAsync());
        var stored = await NeapGskuAsync(gsku);
        Obs.Record("İlk GSKU kimlik", answer.Status, answer.Errors, op.Checkpoint, op.RecoveryDisposition, op.LastFailureCode,
            op.NextAttemptAtUtcTicksV1, $"GSKU {stored.LifecycleStatus} v{stored.Version} (önce v{version})");
        NeapAssertAnswer(answer);
        Assert.Equal(FirstGskuIdentityWorkflowCheckpoint.AwaitingMakerReplay, op.Checkpoint);
        Assert.NotEqual(ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired, op.RecoveryDisposition);
        Assert.Null(op.NextAttemptAtUtcTicksV1);
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, stored.LifecycleStatus);
    }

    [Fact]
    public async Task NEAP_GSKU_correction_request_gives_the_lock_back_and_says_no_eligible_approver()
    {
        var approved = await ApprovedGskuAsync();
        NeapRefuseTheNextStart();

        var answer = await Obs.ReadAsync(await PostAsync($"/api/gskus/{approved.Id:D}/correction-requests",
            new { expectedGskuVersion = approved.Version, packQuantity = 3m, packUomCode = "C62" }));

        var op = Assert.Single(await host.Database.GetCollection<GskuCorrectionWorkflowOperation>(
                GskuCorrectionWorkflowOperationRepository.CollectionName)
            .Find(x => x.TenantId == _tenant && x.GskuId == approved.Id).ToListAsync());
        var stored = await NeapGskuAsync(approved.Id);
        Obs.Record("GSKU düzeltme", answer.Status, answer.Errors, op.Checkpoint, op.RecoveryDisposition, op.LastFailureCode,
            op.NextAttemptAtUtcTicksV1, $"GSKU {stored.LifecycleStatus} lock={(stored.ActiveLifecycleOperation is null ? "free" : "held")}");
        NeapAssertAnswer(answer);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.None, op.RecoveryDisposition);
        Assert.Null(op.NextAttemptAtUtcTicksV1);
        Assert.Null(stored.ActiveLifecycleOperation);
    }

    [Fact]
    public async Task NEAP_GSKU_retirement_request_gives_the_lock_back_and_says_no_eligible_approver()
    {
        var approved = await ApprovedGskuAsync();
        NeapRefuseTheNextStart();

        var answer = await Obs.ReadAsync(await PostAsync($"/api/gskus/{approved.Id:D}/retirement-requests",
            new { expectedGskuVersion = approved.Version, requestReason = "PRODUCT_DISCONTINUED" }));

        var op = Assert.Single(await host.Database.GetCollection<GskuRetirementRequestOperation>(
                GskuRetirementRequestOperationRepository.CollectionName)
            .Find(x => x.TenantId == _tenant && x.GskuId == approved.Id).ToListAsync());
        var stored = await NeapGskuAsync(approved.Id);
        Obs.Record("GSKU emeklilik", answer.Status, answer.Errors, op.Checkpoint, op.RecoveryDisposition, op.LastFailureCode,
            op.NextAttemptAtUtcTicksV1, $"GSKU {stored.LifecycleStatus} lock={(stored.ActiveLifecycleOperation is null ? "free" : "held")}");
        NeapAssertAnswer(answer);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.None, op.RecoveryDisposition);
        Assert.Null(op.NextAttemptAtUtcTicksV1);
        Assert.Null(stored.ActiveLifecycleOperation);
    }

    // Platform answers every repeat of the refused start the same way (WorkflowInstanceStartCoordinator: nothing is
    // reserved on the first call; a reservation met on a replay is closed and the same 400 answered). Measured here: the
    // maker's repeat sends the start again and is refused the same way; the recovery worker never looks such a start up
    // (so its start-result lookup — 409 WORKFLOW_START_NOT_COMPLETED, MDM Incomplete — is not reached) and never
    // restarts it. Nothing is quarantined.
    [Fact]
    public async Task NEAP_A_repeated_GP_submit_is_refused_the_same_way_and_the_worker_never_looks_it_up()
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.Draft);
        var key = Guid.NewGuid();
        var token = host.Token(_tenant, _maker, NeapGpPermissions);
        NeapRefuseTheNextStart();
        var first = await Obs.ReadAsync(await SendAsync(HttpMethod.Post, $"/api/global-products/{product:D}/submit",
            new { expectedVersion = 0 }, token, key));
        NeapAssertGpAnswer(first);

        NeapRefuseTheNextStart();
        host.Engine.NextLookupFailure = (ProductIdentityWorkflowTransportOutcome.Incomplete, "WORKFLOW_START_NOT_COMPLETED");
        var startsBefore = host.Engine.StartCalls;
        var repeat = await Obs.ReadAsync(await SendAsync(HttpMethod.Post, $"/api/global-products/{product:D}/submit",
            new { expectedVersion = 0 }, token, key));
        var lookupAsked = host.Engine.NextLookupFailure is null;
        var startedAgain = host.Engine.StartCalls - startsBefore;
        host.Engine.NextLookupFailure = null;
        host.Engine.NextStartFailure = null;

        // The recovery worker is where a start result is looked up: one cycle with Platform's "start not completed".
        host.Engine.NextLookupFailure = (ProductIdentityWorkflowTransportOutcome.Incomplete, "WORKFLOW_START_NOT_COMPLETED");
        var workerStartsBefore = host.Engine.StartCalls;
        await host.RunAsync<Diten.MdmService.Api.Services.ProductItemSkuMaster.ProductIdentityWorkflowRecoveryRunner>(
            runner => runner.RunCycleAsync());
        var workerLookupAsked = host.Engine.NextLookupFailure is null;
        var workerStarts = host.Engine.StartCalls - workerStartsBefore;
        host.Engine.NextLookupFailure = null;

        var op = Assert.Single(await host.Database.GetCollection<GlobalProductIdentityWorkflowOperation>(
                GlobalProductIdentityWorkflowOperationRepository.CollectionName)
            .Find(x => x.TenantId == _tenant && x.GlobalProductId == product).ToListAsync());
        Obs.Record("GP kimlik tekrar + işçi (Incomplete)", repeat.Status, repeat.Errors, op.Checkpoint, op.RecoveryDisposition,
            op.LastFailureCode, op.NextAttemptAtUtcTicksV1,
            $"GP {(await NeapGpAsync(product)).LifecycleStatus} makerRepeat: lookupAsked={lookupAsked} startCalls={startedAgain}; worker: lookupAsked={workerLookupAsked} startCalls={workerStarts}");
        NeapAssertGpAnswer(repeat);
        Assert.Equal(1, startedAgain);
        Assert.False(lookupAsked);
        Assert.False(workerLookupAsked);
        Assert.Equal(0, workerStarts);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.AwaitingMakerReplay, op.Checkpoint);
        Assert.NotEqual(ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired, op.RecoveryDisposition);
        Assert.Null(op.NextAttemptAtUtcTicksV1);
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, (await NeapGpAsync(product)).LifecycleStatus);
    }

    private Task<GlobalProduct> NeapGpAsync(Guid product) =>
        host.Database.GetCollection<GlobalProduct>("mdm_global_products").Find(x => x.Id == product).SingleAsync();

    private Task<Gsku> NeapGskuAsync(Guid gsku) =>
        host.Database.GetCollection<Gsku>("mdm_gskus").Find(x => x.Id == gsku).SingleAsync();
}
