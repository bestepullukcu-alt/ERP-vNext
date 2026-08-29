using System.Security.Cryptography;
using System.Text;
using Diten.PpmService.Application.Features.BenefitCommitments.GateI.BenefitRealization;
using Diten.PpmService.Domain.GateI.BenefitRealization;
using Xunit;

namespace Diten.PpmService.Tests.GateI.BenefitRealization;

public sealed class OutcomeReferenceContractTests
{
    private static readonly Guid BenefitId = Guid.Parse("11700000-0000-4000-8000-000000000001");
    private static readonly Guid OutcomeId = Guid.Parse("72000000-0000-4000-8000-000000000003");
    private static readonly Guid VersionId = Guid.Parse("72000000-0000-4000-8000-000000000010");
    private static readonly Guid TenantId = Guid.Parse("72000000-0000-4000-8000-000000000004");
    private static readonly Guid ActorId = Guid.Parse("72000000-0000-4000-8000-000000000002");

    [Fact]
    public void Exact_wrapper_and_nested_reference_round_trip_as_nine_owned_fields()
    {
        var value = ValidWrapper();
        var bytes = BenefitCommitmentOutcomeReferenceV1Codec.Serialize(value);
        var json = Encoding.UTF8.GetString(bytes);

        Assert.Equal(
            "{\"ContractName\":\"ppm.benefit-commitment-outcome-reference\",\"ContractVersion\":\"1.0\",\"BenefitCommitmentId\":\"11700000-0000-4000-8000-000000000001\",\"OutcomeReference\":{\"contractName\":\"diten.decision-intelligence.outcome-reference\",\"contractVersion\":\"1.0\",\"outcomeId\":\"72000000-0000-4000-8000-000000000003\",\"outcomeVersionId\":\"72000000-0000-4000-8000-000000000010\",\"outcomeVersionNumber\":3}}",
            json);
        Assert.Equal(value, BenefitCommitmentOutcomeReferenceV1Codec.ParseStrict(bytes));
    }

    [Theory]
    [InlineData("ActualValue")]
    [InlineData("measurementId")]
    [InlineData("period")]
    [InlineData("evidence")]
    [InlineData("realizationState")]
    [InlineData("OutcomeMeasurementReference")]
    [InlineData("isReferenceable")]
    public void Forbidden_actual_value_fields_are_never_serialized(string forbidden)
    {
        Assert.DoesNotContain(forbidden, Encoding.UTF8.GetString(BenefitCommitmentOutcomeReferenceV1Codec.Serialize(ValidWrapper())), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("{\"ContractName\":\"ppm.benefit-commitment-outcome-reference\",\"ContractVersion\":\"1.0\",\"BenefitCommitmentId\":\"11700000-0000-4000-8000-000000000001\",\"OutcomeReference\":{},\"extra\":true}")]
    [InlineData("{\"contractname\":\"ppm.benefit-commitment-outcome-reference\"}")]
    [InlineData("{\"ContractName\":\"ppm.benefit-commitment-outcome-reference\",\"ContractName\":\"ppm.benefit-commitment-outcome-reference\"}")]
    [InlineData("null")]
    [InlineData("[]")]
    public void Extra_case_changed_duplicate_or_non_object_payload_is_400(string json)
    {
        var exception = Assert.Throws<OutcomeReferenceContractException>(() =>
            BenefitCommitmentOutcomeReferenceV1Codec.ParseStrict(Encoding.UTF8.GetBytes(json)));
        Assert.Equal(OutcomeReferenceContractError.Malformed, exception.Error);
    }

    [Theory]
    [InlineData("HistoricalResolve", true)]
    [InlineData("NewReferenceEligibility", true)]
    [InlineData("CurrentSelectionEligibility", true)]
    [InlineData("historicalresolve", false)]
    [InlineData("Current", false)]
    public void Mode_tokens_are_closed_and_ordinal(string token, bool accepted)
    {
        Assert.Equal(accepted, Enum.TryParse<OutcomeReferenceValidationMode>(token, ignoreCase: false, out _));
    }

    [Fact]
    public async Task Exact_s2s_context_and_same_reference_are_accepted_without_receipt_or_mutation()
    {
        var bytes = BenefitCommitmentOutcomeReferenceV1Codec.Serialize(ValidWrapper());
        var port = new StubPort(new(OutcomeReferenceAuthorityDisposition.Accepted, ValidWrapper().OutcomeReference));
        var result = await new BenefitCommitmentOutcomeReferenceValidator(port).ValidateAsync(
            bytes, OutcomeReferenceValidationMode.HistoricalResolve, ValidContext(bytes));

        Assert.Equal(200, result.StatusCode);
        Assert.Equal(1, port.CallCount);
        Assert.Equal(TenantId, port.LastRequest!.TenantId);
        Assert.Equal(ActorId, port.LastRequest.EffectiveActorId);
        Assert.Equal(ValidWrapper(), result.Reference);
    }

    [Fact]
    public async Task Repeated_read_only_validation_is_stable_and_never_uses_a_consumer_receipt_or_cache()
    {
        var bytes = BenefitCommitmentOutcomeReferenceV1Codec.Serialize(ValidWrapper());
        var port = new StubPort(new(OutcomeReferenceAuthorityDisposition.Accepted, ValidWrapper().OutcomeReference));
        var validator = new BenefitCommitmentOutcomeReferenceValidator(port);
        var first = await validator.ValidateAsync(bytes, OutcomeReferenceValidationMode.HistoricalResolve, ValidContext(bytes));
        var second = await validator.ValidateAsync(bytes, OutcomeReferenceValidationMode.HistoricalResolve, ValidContext(bytes));
        Assert.Equal(first, second);
        Assert.Equal(2, port.CallCount);
    }

    [Fact]
    public void S2S_request_binding_is_sensitive_to_method_path_tenant_operation_and_body()
    {
        var body = BenefitCommitmentOutcomeReferenceV1Codec.Serialize(ValidWrapper());
        var baseline = GateICanonicalRequestBinding.Compute("POST", "/test-only/gate-i-c/outcome-reference", TenantId,
            BenefitCommitmentOutcomeReferenceValidator.Operation, body);
        Assert.NotEqual(baseline, GateICanonicalRequestBinding.Compute("PUT", "/test-only/gate-i-c/outcome-reference", TenantId, BenefitCommitmentOutcomeReferenceValidator.Operation, body));
        Assert.NotEqual(baseline, GateICanonicalRequestBinding.Compute("POST", "/test-only/gate-i-c/other", TenantId, BenefitCommitmentOutcomeReferenceValidator.Operation, body));
        Assert.NotEqual(baseline, GateICanonicalRequestBinding.Compute("POST", "/test-only/gate-i-c/outcome-reference", Guid.NewGuid(), BenefitCommitmentOutcomeReferenceValidator.Operation, body));
        Assert.NotEqual(baseline, GateICanonicalRequestBinding.Compute("POST", "/test-only/gate-i-c/outcome-reference", TenantId, "outcome-tracking.outcomes.read", body));
        Assert.NotEqual(baseline, GateICanonicalRequestBinding.Compute("POST", "/test-only/gate-i-c/outcome-reference", TenantId, BenefitCommitmentOutcomeReferenceValidator.Operation, Encoding.UTF8.GetBytes("{}")));
    }

    [Theory]
    [InlineData(OutcomeReferenceAuthorityDisposition.PermissionDenied, 403)]
    [InlineData(OutcomeReferenceAuthorityDisposition.MissingOrNonDisclosable, 404)]
    [InlineData(OutcomeReferenceAuthorityDisposition.IneligibleOrConflicting, 409)]
    [InlineData(OutcomeReferenceAuthorityDisposition.Unavailable, 503)]
    [InlineData(OutcomeReferenceAuthorityDisposition.Timeout, 503)]
    [InlineData(OutcomeReferenceAuthorityDisposition.Malformed, 503)]
    [InlineData(OutcomeReferenceAuthorityDisposition.Indeterminate, 503)]
    public async Task Producer_dispositions_map_without_reclassification(OutcomeReferenceAuthorityDisposition disposition, int status)
    {
        var bytes = BenefitCommitmentOutcomeReferenceV1Codec.Serialize(ValidWrapper());
        var result = await new BenefitCommitmentOutcomeReferenceValidator(new StubPort(new(disposition))).ValidateAsync(
            bytes, OutcomeReferenceValidationMode.NewReferenceEligibility, ValidContext(bytes));
        Assert.Equal(status, result.StatusCode);
    }

    [Fact]
    public async Task Missing_reference_is_non_disclosing_404_with_no_consumer_residue()
    {
        var result = await ValidateNonDisclosureScenarioAsync(NonDisclosureScenario.Missing);

        Assert.Equal(404, result.StatusCode);
        Assert.Equal("gate_i_outcome_not_found", result.Code);
        Assert.Null(result.Reference);
    }

    [Fact]
    public async Task Cross_tenant_reference_is_indistinguishable_404_with_no_consumer_residue()
    {
        var missing = await ValidateNonDisclosureScenarioAsync(NonDisclosureScenario.Missing);
        var crossTenant = await ValidateNonDisclosureScenarioAsync(NonDisclosureScenario.CrossTenant);

        Assert.Equal(missing, crossTenant);
        Assert.Equal(404, crossTenant.StatusCode);
        Assert.Equal("gate_i_outcome_not_found", crossTenant.Code);
        Assert.Null(crossTenant.Reference);
    }

    [Fact]
    public async Task Current_selection_is_400_and_never_calls_authority()
    {
        var bytes = BenefitCommitmentOutcomeReferenceV1Codec.Serialize(ValidWrapper());
        var port = new StubPort(new(OutcomeReferenceAuthorityDisposition.Accepted, ValidWrapper().OutcomeReference));
        var result = await new BenefitCommitmentOutcomeReferenceValidator(port).ValidateAsync(
            bytes, OutcomeReferenceValidationMode.CurrentSelectionEligibility, ValidContext(bytes));
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(0, port.CallCount);
    }

    [Fact]
    public async Task Unknown_numeric_mode_is_400_and_never_calls_authority()
    {
        var bytes = BenefitCommitmentOutcomeReferenceV1Codec.Serialize(ValidWrapper());
        var port = new StubPort(new(OutcomeReferenceAuthorityDisposition.Accepted, ValidWrapper().OutcomeReference));
        var result = await new BenefitCommitmentOutcomeReferenceValidator(port).ValidateAsync(
            bytes, (OutcomeReferenceValidationMode)999, ValidContext(bytes));
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(0, port.CallCount);
    }

    [Theory]
    [InlineData("audience")]
    [InlineData("client")]
    [InlineData("scope")]
    [InlineData("request-hash")]
    public async Task Trusted_context_or_request_binding_failure_is_401_without_disclosure(string mutation)
    {
        var bytes = BenefitCommitmentOutcomeReferenceV1Codec.Serialize(ValidWrapper());
        var context = ValidContext(bytes);
        context = mutation switch
        {
            "audience" => context with { Audience = "diten-fpa-service" },
            "client" => context with { ClientId = "diten.fpa" },
            "scope" => context with { Scope = "diten.s2s.invoke" },
            _ => context with { RequestHash = new string('a', 64) }
        };
        var port = new StubPort(new(OutcomeReferenceAuthorityDisposition.Accepted));
        var result = await new BenefitCommitmentOutcomeReferenceValidator(port).ValidateAsync(
            bytes, OutcomeReferenceValidationMode.HistoricalResolve, context);
        Assert.Equal(401, result.StatusCode);
        Assert.Equal(0, port.CallCount);
    }

    [Theory]
    [InlineData("operation")]
    [InlineData("permission")]
    [InlineData("owner")]
    [InlineData("entitlement")]
    public async Task Permission_manifest_or_entitlement_failure_is_403(string mutation)
    {
        var bytes = BenefitCommitmentOutcomeReferenceV1Codec.Serialize(ValidWrapper());
        var context = ValidContext(bytes);
        context = mutation switch
        {
            "operation" => context with { Operation = "outcome-tracking.outcomes.read" },
            "permission" => context with { Permission = "decision-intelligence.outcomes.read" },
            "owner" => context with { OwnerModule = "MOD-0138" },
            _ => context with { EntitlementGranted = false }
        };
        var port = new StubPort(new(OutcomeReferenceAuthorityDisposition.Accepted));
        var result = await new BenefitCommitmentOutcomeReferenceValidator(port).ValidateAsync(
            bytes, OutcomeReferenceValidationMode.HistoricalResolve, context);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal(0, port.CallCount);
    }

    [Fact]
    public async Task Unknown_versions_are_503_and_never_downgrade()
    {
        var bytes = BenefitCommitmentOutcomeReferenceV1Codec.Serialize(ValidWrapper());
        var changed = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes).Replace("\"ContractVersion\":\"1.0\"", "\"ContractVersion\":\"2.0\"", StringComparison.Ordinal));
        var port = new StubPort(new(OutcomeReferenceAuthorityDisposition.Accepted));
        var result = await new BenefitCommitmentOutcomeReferenceValidator(port).ValidateAsync(
            changed, OutcomeReferenceValidationMode.HistoricalResolve, ValidContext(changed));
        Assert.Equal(503, result.StatusCode);
        Assert.Equal(0, port.CallCount);
    }

    [Fact]
    public async Task Changed_authority_tuple_is_malformed_503()
    {
        var bytes = BenefitCommitmentOutcomeReferenceV1Codec.Serialize(ValidWrapper());
        var foreign = ValidWrapper().OutcomeReference with { OutcomeVersionNumber = 4 };
        var result = await new BenefitCommitmentOutcomeReferenceValidator(
            new StubPort(new(OutcomeReferenceAuthorityDisposition.Accepted, foreign))).ValidateAsync(
            bytes, OutcomeReferenceValidationMode.HistoricalResolve, ValidContext(bytes));
        Assert.Equal(503, result.StatusCode);
    }

    [Fact]
    public async Task Cancellation_propagates_and_is_not_reclassified()
    {
        var bytes = BenefitCommitmentOutcomeReferenceV1Codec.Serialize(ValidWrapper());
        using var source = new CancellationTokenSource();
        source.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            new BenefitCommitmentOutcomeReferenceValidator(new StubPort(new(OutcomeReferenceAuthorityDisposition.Accepted)))
                .ValidateAsync(bytes, OutcomeReferenceValidationMode.HistoricalResolve, ValidContext(bytes), source.Token));
    }

    private static BenefitCommitmentOutcomeReferenceV1 ValidWrapper() => new(
        BenefitCommitmentOutcomeReferenceV1.ExactContractName,
        BenefitCommitmentOutcomeReferenceV1.ExactContractVersion,
        BenefitId,
        new OutcomeReferenceV1(OutcomeReferenceV1.ExactContractName, OutcomeReferenceV1.ExactContractVersion, OutcomeId, VersionId, 3));

    private static GateIS2SServerContext ValidContext(ReadOnlySpan<byte> bytes) => new(
        IsAuthenticated: true,
        TenantId: TenantId,
        EffectiveActorId: ActorId,
        DelegatedActorId: ActorId,
        DelegationVerified: true,
        Audience: BenefitCommitmentOutcomeReferenceValidator.Audience,
        ClientId: BenefitCommitmentOutcomeReferenceValidator.ClientId,
        OwnerModule: BenefitCommitmentOutcomeReferenceValidator.OwnerModule,
        Scope: BenefitCommitmentOutcomeReferenceValidator.Scope,
        Method: "POST",
        Path: "/internal/v1/decision-intelligence/outcome-tracking/outcome-references/validate",
        Operation: BenefitCommitmentOutcomeReferenceValidator.Operation,
        Permission: BenefitCommitmentOutcomeReferenceValidator.Permission,
        RequestHash: GateICanonicalRequestBinding.Compute("POST", "/internal/v1/decision-intelligence/outcome-tracking/outcome-references/validate", TenantId,
            BenefitCommitmentOutcomeReferenceValidator.Operation, bytes),
        EntitlementGranted: true);

    private static async Task<OutcomeReferenceValidationResult> ValidateNonDisclosureScenarioAsync(NonDisclosureScenario scenario)
    {
        var bytes = BenefitCommitmentOutcomeReferenceV1Codec.Serialize(ValidWrapper());
        var port = new NonDisclosurePort(scenario);
        var result = await new BenefitCommitmentOutcomeReferenceValidator(port).ValidateAsync(
            bytes, OutcomeReferenceValidationMode.HistoricalResolve, ValidContext(bytes));

        Assert.Equal(1, port.ReadCount);
        Assert.Equal(0, port.MutationCount);
        Assert.Equal(0, port.ReceiptCount);
        Assert.Equal(0, port.AuditCount);
        Assert.Equal(0, port.OutboxCount);
        return result;
    }

    private enum NonDisclosureScenario { Missing, CrossTenant }

    private sealed class NonDisclosurePort(NonDisclosureScenario scenario) : IOutcomeReferenceAuthorityPort
    {
        public int ReadCount { get; private set; }
        public int MutationCount => 0;
        public int ReceiptCount => 0;
        public int AuditCount => 0;
        public int OutboxCount => 0;

        public Task<OutcomeReferenceAuthorityResult> ValidateAsync(
            OutcomeReferenceAuthorityRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.NotEqual(Guid.Empty, request.TenantId);
            Assert.True(scenario is NonDisclosureScenario.Missing or NonDisclosureScenario.CrossTenant);
            ReadCount++;
            return Task.FromResult(new OutcomeReferenceAuthorityResult(
                OutcomeReferenceAuthorityDisposition.MissingOrNonDisclosable));
        }
    }

    private sealed class StubPort(OutcomeReferenceAuthorityResult result) : IOutcomeReferenceAuthorityPort
    {
        public int CallCount { get; private set; }
        public OutcomeReferenceAuthorityRequest? LastRequest { get; private set; }
        public Task<OutcomeReferenceAuthorityResult> ValidateAsync(OutcomeReferenceAuthorityRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastRequest = request;
            return Task.FromResult(result);
        }
    }
}
