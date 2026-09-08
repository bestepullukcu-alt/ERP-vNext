using Diten.MdmService.Api.Configuration;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.ValueObjects;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class GlobalProductRetirementRequestUnitTests
{
    private static readonly Guid Tenant = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Product = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Maker = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public void Factory_creates_exact_retirement_contract_and_stable_rehydration()
    {
        var template = Guid.NewGuid();
        var principal = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 9, 4, 10, 0, 0, TimeSpan.Zero));
        var factory = new GlobalProductRetirementRequestWorkflowStartRequestFactory(new(template, null,
            [principal], "RETIRE", true, true, TimeSpan.FromHours(1), Guid.NewGuid(), null,
            Guid.NewGuid(), null), clock);

        var plan = factory.Create(ApprovedProduct(), operationId, Maker, "Obsolete identity");
        var replay = factory.Rehydrate(plan.Operation);

        Assert.Equal("GlobalProductRetirement", plan.Operation.ObjectType);
        Assert.Equal(operationId.ToString("D"), plan.Operation.ObjectId);
        Assert.Equal(GlobalProductRetirementRequestCheckpoint.Prepared, plan.Operation.Checkpoint);
        Assert.Equal("Obsolete identity", plan.Operation.RequestReason);
        Assert.Equal(plan.TransportRequest.TemplateId, replay.TemplateId);
        Assert.Equal(plan.TransportRequest.ObjectType, replay.ObjectType);
        Assert.Equal(plan.TransportRequest.ObjectId, replay.ObjectId);
        Assert.Equal(plan.TransportRequest.IdempotencyKey, replay.IdempotencyKey);
        Assert.Equal(plan.TransportRequest.CandidatePrincipalIds, replay.CandidatePrincipalIds);
    }

    [Fact]
    public void Factory_rejects_any_identity_or_correction_template_alias()
    {
        var template = Guid.NewGuid();
        var factory = new GlobalProductRetirementRequestWorkflowStartRequestFactory(new(template, null,
            [Guid.NewGuid()], "RETIRE", false, false, null, template, null, Guid.NewGuid(), null),
            TimeProvider.System);

        var error = Assert.Throws<InvalidOperationException>(() =>
            factory.Create(ApprovedProduct(), Guid.NewGuid(), Maker, "Reason"));

        Assert.Equal("GLOBAL_PRODUCT_RETIREMENT_CONFIGURATION_INVALID", error.Message);
    }

    [Fact]
    public void Factory_rejects_case_only_template_code_alias()
    {
        var factory = new GlobalProductRetirementRequestWorkflowStartRequestFactory(new(null, "GP-RETIRE",
            [Guid.NewGuid()], "RETIRE", false, false, null, null, "gp-retire", null, "GP-CORRECT"),
            TimeProvider.System);

        var error = Assert.Throws<InvalidOperationException>(() =>
            factory.Create(ApprovedProduct(), Guid.NewGuid(), Maker, "Reason"));

        Assert.Equal("GLOBAL_PRODUCT_RETIREMENT_CONFIGURATION_INVALID", error.Message);
    }

    [Fact]
    public void Factory_rejects_active_correction_or_retirement_binding_before_workflow_plan()
    {
        var product = ApprovedProduct();
        product.ActiveLifecycleOperation = new(GlobalProductLifecycleOperationKind.Correction, Guid.NewGuid(), 3);
        var factory = new GlobalProductRetirementRequestWorkflowStartRequestFactory(new(Guid.NewGuid(), null,
            [Guid.NewGuid()], "RETIRE", false, false, null, Guid.NewGuid(), null, Guid.NewGuid(), null),
            TimeProvider.System);

        var error = Assert.Throws<InvalidOperationException>(() =>
            factory.Create(product, Guid.NewGuid(), Maker, "Reason"));

        Assert.Equal("GLOBAL_PRODUCT_RETIREMENT_FACTS_INVALID", error.Message);
    }

    [Fact]
    public void Reason_is_trimmed_unicode_scalar_bounded_and_control_free()
    {
        Assert.False(GlobalProductRetirementRequestWorkflowStartRequestFactory.HasValidReason("  valid  "));
        Assert.Equal("valid", GlobalProductRetirementRequestWorkflowStartRequestFactory.NormalizeReason("  valid  "));
        Assert.True(GlobalProductRetirementRequestWorkflowStartRequestFactory.HasValidReason(
            string.Concat(Enumerable.Repeat("😀", 2000))));
        Assert.False(GlobalProductRetirementRequestWorkflowStartRequestFactory.HasValidReason(
            string.Concat(Enumerable.Repeat("😀", 2001))));
        Assert.False(GlobalProductRetirementRequestWorkflowStartRequestFactory.HasValidReason("bad\nreason"));
    }

    [Fact]
    public void Fingerprint_covers_canonical_candidates_and_all_start_configuration_facts()
    {
        var template = Guid.NewGuid();
        var first = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var second = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var operationId = Guid.NewGuid();
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 9, 4, 10, 0, 0, TimeSpan.Zero));
        GlobalProductRetirementRequestStartConfiguration Base(IReadOnlyList<Guid> candidates) => new(
            template, null, candidates, "RETIRE", false, false, TimeSpan.FromHours(1),
            Guid.NewGuid(), null, Guid.NewGuid(), null);
        string Fingerprint(GlobalProductRetirementRequestStartConfiguration configuration) =>
            new GlobalProductRetirementRequestWorkflowStartRequestFactory(configuration, clock)
                .Create(ApprovedProduct(), operationId, Maker, "Business reason").Operation.OperationFingerprint;

        var baselineConfig = Base([second, first]);
        var baseline = Fingerprint(baselineConfig);
        Assert.Equal(baseline, Fingerprint(baselineConfig with { CandidatePrincipalIds = [first, second] }));
        Assert.NotEqual(baseline, Fingerprint(baselineConfig with { CandidatePrincipalIds = [Guid.NewGuid()] }));
        Assert.NotEqual(baseline, Fingerprint(baselineConfig with { ReasonCode = "OTHER" }));
        Assert.NotEqual(baseline, Fingerprint(baselineConfig with { CommentRequired = true }));
        Assert.NotEqual(baseline, Fingerprint(baselineConfig with { EvidenceRequired = true }));
        Assert.NotEqual(baseline, Fingerprint(baselineConfig with { DueAfter = TimeSpan.FromHours(2) }));
        var laterClock = new FixedTimeProvider(clock.GetUtcNow().AddSeconds(1));
        var laterDueAt = new GlobalProductRetirementRequestWorkflowStartRequestFactory(baselineConfig, laterClock)
            .Create(ApprovedProduct(), operationId, Maker, "Business reason").Operation.OperationFingerprint;
        Assert.NotEqual(baseline, laterDueAt);
    }

    [Theory]
    [InlineData(" RETIRE", "retirement-template")]
    [InlineData("RETIRE\n", "retirement-template")]
    [InlineData("RETIRE", " retirement-template")]
    [InlineData("RETIRE", "retirement-template\n")]
    public void Options_reject_non_exact_reason_and_template_text_before_runtime_mutation(
        string reasonCode,
        string templateCode)
    {
        var options = new GlobalProductRetirementRequestWorkflowOptions
        {
            Enabled = true,
            TemplateCode = templateCode,
            CandidatePrincipalIds = [Guid.NewGuid()],
            ReasonCode = reasonCode
        };

        Assert.Throws<InvalidOperationException>(() => options.ToConfiguration(
            null, "identity-template", null, "correction-template"));
    }

    private static GlobalProduct ApprovedProduct() => new()
    {
        Id = Product, TenantId = Tenant, CanonicalCode = "GP-000000000001",
        GlobalProductName = "Product", GlobalProductNameNormalized = "PRODUCT",
        LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved, Version = 3
    };

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
