using Diten.Platform.API.Configuration;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.BusinessReferenceData;

public sealed class BusinessReferenceDataVerifiedMarketOperationalMongoTests : IAsyncLifetime
{
    private BusinessReferenceDataTestHarness _harness=null!;
    public async Task InitializeAsync()=>_harness=await BusinessReferenceDataTestHarness.CreateAsync();
    public Task DisposeAsync()=>_harness.DisposeAsync().AsTask();

    [Fact]
    public async Task Exact249Artifact_PublishesTwAndReplaysOneDurableIdentityWithoutAssignments()
    {
        var path=BusinessReferenceDataTestHarness.GetSeedPath("mod-0290-market-reference.json");
        var facts=Facts(path,"market-run"); var eligibility=new Eligibility(facts); var loader=_harness.CreateLoader(eligibility: new RuntimeBusinessReferenceDataPublicationEligibility(), marketEligibility:eligibility);
        var first=await loader.LoadVerifiedMarketCatalogFromFileAsync(path,facts.ActorId,facts.IdempotencyNamespace,eligibility.Authorization,facts);
        var replay=await loader.LoadVerifiedMarketCatalogFromFileAsync(path,facts.ActorId,facts.IdempotencyNamespace,eligibility.Authorization,facts);
        Assert.Empty(first.BlockedConflicts); Assert.Equal(1,replay.SetsAlreadyLoaded);
        var publication=await _harness.Repository.GetVerifiedPublicationAsync("market",facts.CatalogVersion,facts.CatalogFingerprint);
        Assert.NotNull(publication); Assert.Equal(249,publication.Version.Values.Count); Assert.Contains(publication.Version.Values,x=>x.ValueCode=="TW"&&x.DisplayName=="Taiwan (Province of China)");
        Assert.Equal(BusinessReferenceDataPublishOperationState.COMPLETED,publication.Operation.OperationState); Assert.Equal(BusinessReferenceDataPublishCheckpoint.COMPLETION_VERIFIED,publication.Operation.PublishCheckpoint);
        var operations=await _harness.Database.GetCollection<BusinessReferenceDataPublishOperation>("business_reference_data_publish_operations").Find(FilterDefinition<BusinessReferenceDataPublishOperation>.Empty).ToListAsync();
        Assert.Single(operations); Assert.StartsWith("market-run:",operations[0].IdempotencyKey,StringComparison.Ordinal);
        Assert.Equal(0,await _harness.Database.GetCollection<BusinessReferenceDataTenantAssignment>("business_reference_data_tenant_assignments").CountDocumentsAsync(FilterDefinition<BusinessReferenceDataTenantAssignment>.Empty));
    }

    [Fact]
    public async Task DifferentNamespace_CreatesDistinctOperationIdentity()
    {
        var path=BusinessReferenceDataTestHarness.GetSeedPath("mod-0290-market-reference.json");
        foreach(var ns in new[]{"run-a","run-b"}) { var facts=Facts(path,ns); var eligibility=new Eligibility(facts); await _harness.CreateLoader(marketEligibility:eligibility).LoadVerifiedMarketCatalogFromFileAsync(path,facts.ActorId,ns,eligibility.Authorization,facts); }
        var keys=await _harness.Database.GetCollection<BusinessReferenceDataPublishOperation>("business_reference_data_publish_operations").Find(FilterDefinition<BusinessReferenceDataPublishOperation>.Empty).Project(x=>x.IdempotencyKey).ToListAsync();
        Assert.Equal(2,keys.Distinct(StringComparer.Ordinal).Count()); Assert.Contains(keys,x=>x.StartsWith("run-a:",StringComparison.Ordinal)); Assert.Contains(keys,x=>x.StartsWith("run-b:",StringComparison.Ordinal));
    }

    [Fact]
    public async Task NamespacedCheckpointCrash_ReplaysSameOperationToVerifiedCompletion()
    {
        var path = BusinessReferenceDataTestHarness.GetSeedPath("mod-0290-market-reference.json");
        var facts = Facts(path, "checkpoint-run");
        var eligibility = new Eligibility(facts);
        var observer = new ThrowOnceAtCheckpointObserver(BusinessReferenceDataPublishCheckpoint.TARGET_VERSION_WRITTEN);

        await Assert.ThrowsAsync<InjectedPublishCrashException>(() =>
            _harness.CreateLoader(observer, marketEligibility: eligibility)
                .LoadVerifiedMarketCatalogFromFileAsync(path, facts.ActorId, facts.IdempotencyNamespace, eligibility.Authorization, facts));

        var interrupted = Assert.Single(await _harness.Database
            .GetCollection<BusinessReferenceDataPublishOperation>("business_reference_data_publish_operations")
            .Find(FilterDefinition<BusinessReferenceDataPublishOperation>.Empty)
            .ToListAsync());
        Assert.Equal(BusinessReferenceDataPublishOperationState.RECOVERY_REQUIRED, interrupted.OperationState);
        Assert.Equal(BusinessReferenceDataPublishCheckpoint.TARGET_VERSION_WRITTEN, interrupted.PublishCheckpoint);
        Assert.StartsWith("checkpoint-run:", interrupted.IdempotencyKey, StringComparison.Ordinal);

        await _harness.CreateLoader(marketEligibility: eligibility)
            .LoadVerifiedMarketCatalogFromFileAsync(path, facts.ActorId, facts.IdempotencyNamespace, eligibility.Authorization, facts);

        var completed = await _harness.Repository.GetPublishOperationByIdAsync(interrupted.PublishOperationId);
        Assert.NotNull(completed);
        Assert.Equal(BusinessReferenceDataPublishOperationState.COMPLETED, completed.OperationState);
        Assert.Equal(BusinessReferenceDataPublishCheckpoint.COMPLETION_VERIFIED, completed.PublishCheckpoint);
        Assert.NotNull(await _harness.Repository.GetVerifiedPublicationAsync("market", facts.CatalogVersion, facts.CatalogFingerprint));
    }

    [Fact]
    public async Task SameNamespacedOperationKey_WithDifferentFingerprint_IsConflict()
    {
        var path = BusinessReferenceDataTestHarness.GetSeedPath("mod-0290-market-reference.json");
        var facts = Facts(path, "conflict-run");
        var eligibility = new Eligibility(facts);
        await _harness.CreateLoader(marketEligibility: eligibility)
            .LoadVerifiedMarketCatalogFromFileAsync(path, facts.ActorId, facts.IdempotencyNamespace, eligibility.Authorization, facts);
        var persisted = Assert.Single(await _harness.Database
            .GetCollection<BusinessReferenceDataPublishOperation>("business_reference_data_publish_operations")
            .Find(FilterDefinition<BusinessReferenceDataPublishOperation>.Empty)
            .ToListAsync());

        var conflict = await _harness.Repository.CreateOrGetPublishOperationAsync(new BusinessReferenceDataPublishOperation
        {
            TenantId = persisted.TenantId,
            BusinessReferenceDataSetId = persisted.BusinessReferenceDataSetId,
            BusinessReferenceDataVersionId = persisted.BusinessReferenceDataVersionId,
            IdempotencyKey = persisted.IdempotencyKey,
            ExpectedPublishedVersionId = persisted.ExpectedPublishedVersionId,
            ExpectedSetVersion = persisted.ExpectedSetVersion,
            ExpectedTargetVersionToken = persisted.ExpectedTargetVersionToken,
            CatalogVersion = persisted.CatalogVersion,
            CatalogFingerprint = new string('0', 64),
            CreatedBy = "actor"
        });

        Assert.Equal(BusinessReferenceDataPublishOperationCreateOutcome.Conflict, conflict.Outcome);
        Assert.Equal(persisted.PublishOperationId, conflict.Operation.PublishOperationId);
    }

    private VerifiedMarketOperationalFacts Facts(string path,string ns)=>new(Path.GetFullPath(path),VerifiedMarketOperationalProvisioningOptions.LockedCatalogVersion,VerifiedMarketOperationalProvisioningOptions.LockedCatalogFingerprint,_harness.ReferenceTenantId,"actor",ns);
    private sealed class Eligibility : IBusinessReferenceDataVerifiedMarketOperationalEligibility { private readonly VerifiedMarketOperationalFacts _facts; public AuthorizationToken Authorization {get;}=new(); public Eligibility(VerifiedMarketOperationalFacts facts)=>_facts=facts; public Task<VerifiedMarketOperationalEligibilityDecision> EvaluateAsync(CancellationToken ct=default)=>Task.FromResult(new VerifiedMarketOperationalEligibilityDecision(true,"ok",_facts,Authorization)); public bool IsAuthorized(IBusinessReferenceDataVerifiedMarketOperationalAuthorization authorization,VerifiedMarketOperationalFacts facts)=>ReferenceEquals(authorization,Authorization)&&facts==_facts; }
    private sealed class AuthorizationToken:IBusinessReferenceDataVerifiedMarketOperationalAuthorization;
}
