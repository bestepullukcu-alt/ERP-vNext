using Diten.MdmService.Api.Configuration;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class GlobalProductCorrectionUnitTests
{
    [Fact]
    public void Factory_creates_bounded_immutable_proposal_and_exact_correction_object_type()
    {
        var templateId = Guid.NewGuid();
        var factory = new GlobalProductCorrectionWorkflowStartRequestFactory(new(
            templateId, null, [Guid.NewGuid()], "CORRECTION", false, false, null,
            Guid.NewGuid(), null), TimeProvider.System);
        var product = ApprovedProduct();

        var plan = factory.Create(product, Guid.NewGuid(), Guid.NewGuid(), "  Corrected Name  ");

        Assert.Equal("Corrected Name", plan.Operation.ProposedGlobalProductName);
        Assert.Equal("CORRECTED NAME", plan.Operation.ProposedGlobalProductNameNormalized);
        Assert.Equal("GlobalProductCorrection", plan.Operation.ObjectType);
        Assert.Equal(plan.Operation.OperationId.ToString("D"), plan.Operation.ObjectId);
        Assert.Equal(plan.Operation.ObjectType, plan.TransportRequest.ObjectType);
    }

    [Fact]
    public void Factory_fails_closed_when_identity_and_correction_template_are_equal()
    {
        var templateId = Guid.NewGuid();
        var factory = new GlobalProductCorrectionWorkflowStartRequestFactory(new(
            templateId, null, [Guid.NewGuid()], "CORRECTION", false, false, null,
            templateId, null), TimeProvider.System);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            factory.Create(ApprovedProduct(), Guid.NewGuid(), Guid.NewGuid(), "Corrected"));

        Assert.Equal("GLOBAL_PRODUCT_CORRECTION_CONFIGURATION_INVALID", exception.Message);
    }

    [Fact]
    public void Options_are_default_disabled_and_reject_identity_template_alias()
    {
        var options = new GlobalProductCorrectionWorkflowOptions();
        Assert.False(options.Enabled);
        Assert.Throws<InvalidOperationException>(() => options.ToConfiguration(null, null));

        var templateId = Guid.NewGuid();
        options.Enabled = true;
        options.TemplateId = templateId;
        options.CandidatePrincipalIds = [Guid.NewGuid()];
        Assert.Throws<InvalidOperationException>(() => options.ToConfiguration(templateId, null));
    }

    [Fact]
    public void Mixed_selector_kinds_fail_closed_without_authoritative_template_resolution()
    {
        var options = new GlobalProductCorrectionWorkflowOptions
        {
            Enabled = true,
            TemplateId = Guid.NewGuid(),
            CandidatePrincipalIds = [Guid.NewGuid()]
        };

        Assert.Throws<InvalidOperationException>(() =>
            options.ToConfiguration(null, "identity-template"));

        var reverse = new GlobalProductCorrectionWorkflowOptions
        {
            Enabled = true,
            TemplateCode = "correction-template",
            CandidatePrincipalIds = [Guid.NewGuid()]
        };
        Assert.Throws<InvalidOperationException>(() =>
            reverse.ToConfiguration(Guid.NewGuid(), null));

        Assert.Throws<InvalidOperationException>(() => new GlobalProductCorrectionWorkflowOptions
        {
            Enabled = true,
            TemplateCode = "same-template",
            CandidatePrincipalIds = [Guid.NewGuid()]
        }.ToConfiguration(null, "same-template"));
    }

    [Fact]
    public void Unicode_scalar_limit_is_enforced()
    {
        var valid = string.Concat(Enumerable.Repeat("😀", 200));
        var invalid = valid + "x";
        Assert.True(GlobalProductNameRules.HasValidLength(valid));
        Assert.False(GlobalProductNameRules.HasValidLength(invalid));
    }

    [Fact]
    public void Cancel_audit_is_reserved_but_not_creatable_by_correction_runtime()
    {
        Assert.Throws<ArgumentException>(() => GlobalProductCorrectionAuditIntentFactory.Create(
            ApprovedProduct(), 1, Guid.NewGuid(), Guid.NewGuid(),
            ProductAuditOperation.GlobalProductCorrectionCancelled, "No cancellation", DateTimeOffset.UtcNow));
    }

    [Theory]
    [InlineData(" CORRECTION", "template")]
    [InlineData("CORRECTION\n", "template")]
    [InlineData("CORRECTION", " template")]
    [InlineData("CORRECTION", "template\n")]
    public void Options_reject_non_exact_reason_and_template_text_before_runtime_mutation(
        string reasonCode,
        string templateCode)
    {
        var options = new GlobalProductCorrectionWorkflowOptions
        {
            Enabled = true,
            TemplateCode = templateCode,
            CandidatePrincipalIds = [Guid.NewGuid()],
            ReasonCode = reasonCode
        };

        Assert.Throws<InvalidOperationException>(() => options.ToConfiguration(null, "identity-template"));
    }

    [Fact]
    public void Options_reject_case_only_identity_template_alias()
    {
        var options = new GlobalProductCorrectionWorkflowOptions
        {
            Enabled = true,
            TemplateCode = "CORRECTION-TEMPLATE",
            CandidatePrincipalIds = [Guid.NewGuid()]
        };

        Assert.Throws<InvalidOperationException>(() =>
            options.ToConfiguration(null, "correction-template"));
    }

    [Fact]
    public void Fingerprint_covers_full_transport_tuple_and_canonical_candidate_set()
    {
        var template = Guid.NewGuid();
        var identity = Guid.NewGuid();
        var first = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var second = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var operationId = Guid.NewGuid();
        var maker = Guid.NewGuid();
        var clock = new FixedTimeProvider(new(2026, 9, 4, 10, 0, 0, TimeSpan.Zero));
        var product = ApprovedProduct();
        GlobalProductCorrectionStartConfiguration Base(IReadOnlyList<Guid> candidates) => new(
            template, null, candidates, "CORRECTION", false, false, TimeSpan.FromHours(1), identity, null);
        string Fingerprint(GlobalProductCorrectionStartConfiguration configuration,
            GlobalProduct? target = null) => new GlobalProductCorrectionWorkflowStartRequestFactory(
                configuration, clock).Create(target ?? product, operationId, maker, "Corrected")
                .Operation.OperationFingerprint;

        var baselineConfig = Base([second, first]);
        var baseline = Fingerprint(baselineConfig);
        Assert.Equal(baseline, Fingerprint(baselineConfig with { CandidatePrincipalIds = [first, second] }));
        Assert.NotEqual(baseline, Fingerprint(baselineConfig with { CandidatePrincipalIds = [Guid.NewGuid()] }));
        Assert.NotEqual(baseline, Fingerprint(baselineConfig with { ReasonCode = "OTHER" }));
        Assert.NotEqual(baseline, Fingerprint(baselineConfig with { CommentRequired = true }));
        Assert.NotEqual(baseline, Fingerprint(baselineConfig with { EvidenceRequired = true }));
        Assert.NotEqual(baseline, Fingerprint(baselineConfig with { DueAfter = TimeSpan.FromHours(2) }));
        var changedRef = ApprovedProduct();
        changedRef.Id = product.Id;
        changedRef.TenantId = product.TenantId;
        changedRef.Version = product.Version;
        changedRef.CanonicalCode = "GP-OTHER";
        Assert.NotEqual(baseline, Fingerprint(baselineConfig, changedRef));
    }

    private static GlobalProduct ApprovedProduct()
    {
        var name = "Current Name";
        return new()
        {
            Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), CanonicalCode = "GP-TEST",
            GlobalProductName = name, GlobalProductNameNormalized = GlobalProductNameRules.NormalizeDuplicateKey(name),
            LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved
        };
    }


    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
