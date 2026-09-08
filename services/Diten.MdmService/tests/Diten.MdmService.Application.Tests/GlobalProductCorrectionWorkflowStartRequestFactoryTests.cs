using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class GlobalProductCorrectionWorkflowStartRequestFactoryTests
{
    [Fact]
    public void Fingerprint_is_stable_and_contains_no_delegated_credential()
    {
        var template = Guid.NewGuid();
        var factory = new GlobalProductCorrectionWorkflowStartRequestFactory(new(
            template, null, [Guid.NewGuid()], "CORRECTION", false, false, null, Guid.NewGuid(), null),
            TimeProvider.System);
        var product = new GlobalProduct
        {
            Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), CanonicalCode = "GP-TEST",
            GlobalProductName = "Original", GlobalProductNameNormalized = "ORIGINAL",
            LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved
        };
        var operationId = Guid.NewGuid();
        var maker = Guid.NewGuid();

        var first = factory.Create(product, operationId, maker, "Corrected");
        var second = factory.Create(product, operationId, maker, "Corrected");

        Assert.Equal(first.Operation.OperationFingerprint, second.Operation.OperationFingerprint);
        Assert.DoesNotContain("token", first.Operation.OperationFingerprint, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("GlobalProductCorrection", first.TransportRequest.ObjectType);
    }

    [Fact]
    public void Fingerprint_canonicalizes_candidate_order_and_changes_for_any_start_fact_drift()
    {
        var template = Guid.NewGuid();
        var identity = Guid.NewGuid();
        var firstCandidate = Guid.NewGuid();
        var secondCandidate = Guid.NewGuid();
        var product = new GlobalProduct
        {
            Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), CanonicalCode = "GP-FINGERPRINT",
            GlobalProductName = "Original", GlobalProductNameNormalized = "ORIGINAL",
            LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved
        };
        var operation = Guid.NewGuid();
        var maker = Guid.NewGuid();
        var first = new GlobalProductCorrectionWorkflowStartRequestFactory(new(
            template, null, [firstCandidate, secondCandidate], "CORRECTION", false, false, null,
            identity, null), TimeProvider.System).Create(product, operation, maker, "Corrected");
        var reordered = new GlobalProductCorrectionWorkflowStartRequestFactory(new(
            template, null, [secondCandidate, firstCandidate], "CORRECTION", false, false, null,
            identity, null), TimeProvider.System).Create(product, operation, maker, "Corrected");
        var drifted = new GlobalProductCorrectionWorkflowStartRequestFactory(new(
            template, null, [firstCandidate, secondCandidate], "CORRECTION", true, false, null,
            identity, null), TimeProvider.System).Create(product, operation, maker, "Corrected");

        Assert.Equal(first.Operation.OperationFingerprint, reordered.Operation.OperationFingerprint);
        Assert.NotEqual(first.Operation.OperationFingerprint, drifted.Operation.OperationFingerprint);
    }
}
