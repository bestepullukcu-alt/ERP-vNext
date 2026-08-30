using System.Text;
using Diten.PpmService.Application.Features.InvestmentCases.GateI.DecisionTrace;
using Diten.PpmService.Domain.GateI.DecisionTrace;
using Xunit;

namespace Diten.PpmService.Tests.GateI.DecisionTrace;

public sealed class DecisionTraceContractTests
{
    private static readonly Guid TenantId = Guid.Parse("20000000-0000-4000-8000-000000000001");
    private static readonly Guid ActorId = Guid.Parse("20000000-0000-4000-8000-000000000002");
    private static readonly Guid InvestmentCaseId = Guid.Parse("10000000-0000-4000-8000-000000000001");
    private static readonly Guid DecisionId = Guid.Parse("70000007-0000-4000-8000-000000000001");
    private static readonly Guid RevisionId = Guid.Parse("70000007-0000-4000-8000-000000000002");
    private static readonly DecisionTraceRequestBindingInput Binding = new("POST", "/internal/v1/decision-registry/decision-references/validate");

    [Fact]
    public void Governing_wrapper_serializes_exact_order_and_roundtrips()
    {
        var value = Governing(); var bytes = DecisionTraceReferenceCodec.Serialize(value);
        const string expected = "{\"ContractName\":\"ppm.investment-case-governing-decision-reference\",\"ContractVersion\":\"1.0\",\"InvestmentCaseContext\":{\"ContractName\":\"ppm.investment-case-context\",\"ContractVersion\":\"1.0\",\"InvestmentCaseId\":\"10000000-0000-4000-8000-000000000001\"},\"DecisionRevisionReference\":{\"ContractName\":\"management-governance.decision-reference\",\"ContractVersion\":\"1.0\",\"DecisionId\":\"70000007-0000-4000-8000-000000000001\",\"DecisionRevisionId\":\"70000007-0000-4000-8000-000000000002\",\"DecisionRevisionNumber\":3}}";
        Assert.Equal(expected, Encoding.UTF8.GetString(bytes));
        Assert.Equal(value, Assert.IsType<GoverningDecisionReferenceV1>(DecisionTraceReferenceCodec.Parse(bytes)));
    }

    [Fact]
    public void Wrappers_and_nested_values_have_exact_closed_shapes()
    {
        Assert.Equal(5, typeof(DecisionRevisionReferenceV1).GetProperties().Length);
        Assert.Equal(3, typeof(InvestmentCaseContextV1).GetProperties().Length);
        Assert.Equal(4, typeof(GoverningDecisionReferenceV1).GetProperties().Length);
        Assert.Equal(4, typeof(SupportingDecisionReferenceV1).GetProperties().Length);
    }

    [Theory, MemberData(nameof(MalformedContracts))]
    public void Strict_codec_rejects_contract_drift(string json) => Assert.Throws<DecisionTraceContractException>(() => DecisionTraceReferenceCodec.Parse(Encoding.UTF8.GetBytes(json)));
    public static IEnumerable<object[]> MalformedContracts()
    {
        var valid = Encoding.UTF8.GetString(DecisionTraceReferenceCodec.Serialize(Governing()));
        yield return [valid.Replace("\"ContractVersion\":\"1.0\"", "\"ContractVersion\":\"2.0\"", StringComparison.Ordinal)];
        yield return [valid.Replace("\"ContractName\"", "\"contractName\"", StringComparison.Ordinal)];
        yield return [valid.Replace("\"ContractVersion\":\"1.0\",", "\"ContractVersion\":\"1.0\",\"Extra\":true,", StringComparison.Ordinal)];
        yield return [valid.Replace("\"ContractVersion\":\"1.0\",", "\"ContractVersion\":\"1.0\",\"ContractVersion\":\"1.0\",", StringComparison.Ordinal)];
        yield return [valid.Replace("70000007-0000-4000-8000-000000000001", "70000007-0000-4000-8000-00000000000A", StringComparison.Ordinal)];
    }

    [Fact]
    public void S2S_binding_is_lower_hex_and_sensitive_to_every_dimension()
    {
        var request = Request(); var original = DecisionTraceRequestBinding.Compute(Binding, TenantId, request);
        Assert.Equal(64, original.Length);
        Assert.All(original, character => Assert.True(character is >= '0' and <= '9' or >= 'a' and <= 'f'));
        Assert.NotEqual(original, DecisionTraceRequestBinding.Compute(Binding with { Method = "PUT" }, TenantId, request));
        Assert.NotEqual(original, DecisionTraceRequestBinding.Compute(Binding with { Path = Binding.Path + "/" }, TenantId, request));
        Assert.NotEqual(original, DecisionTraceRequestBinding.Compute(Binding, Guid.NewGuid(), request));
        Assert.NotEqual(original, DecisionTraceRequestBinding.Compute(Binding, TenantId, request with { Mode = DecisionTraceValidationMode.NewReferenceEligibility }));
        Assert.NotEqual(original, DecisionTraceRequestBinding.Compute(Binding, TenantId, new(new GoverningDecisionReferenceV1(new(Guid.NewGuid()), Governing().DecisionRevisionReference), request.Mode)));
        Assert.Throws<ArgumentException>(() => DecisionTraceRequestBinding.Compute(Binding with { Method = "post" }, TenantId, request));
    }

    [Theory]
    [InlineData(DecisionTraceValidationMode.CurrentSelectionEligibility)]
    [InlineData((DecisionTraceValidationMode)999)]
    public async Task Closed_mode_guard_returns_400_before_provider(DecisionTraceValidationMode mode)
    {
        var request = Request(mode); var port = new RecordingPort(new(DecisionReferenceProviderResultKind.Unavailable));
        var result = await new DecisionTraceValidationService(port).ValidateAsync(request, Binding, Trusted(request), default);
        Assert.Equal(400, result.StatusCode); AssertZeroEffects(port);
    }

    [Fact]
    public async Task Request_binding_mismatch_is_401_before_provider()
    {
        var request = Request(); var port = Port(); var context = Trusted(request) with { RequestHash = DecisionTraceRequestBinding.Compute(Binding with { Path = Binding.Path + "/wrong" }, TenantId, request) };
        var result = await new DecisionTraceValidationService(port).ValidateAsync(request, Binding, context, default);
        Assert.Equal(401, result.StatusCode); AssertZeroEffects(port);
    }

    [Fact]
    public async Task Receiver_path_substitution_is_401_even_with_matching_hash()
    {
        var request = Request();
        var port = Port();
        var substituted = Binding with { Path = Binding.Path + "/substituted" };
        var context = Trusted(request) with
        {
            RequestHash = DecisionTraceRequestBinding.Compute(substituted, TenantId, request)
        };
        var result = await new DecisionTraceValidationService(port)
            .ValidateAsync(request, substituted, context, default);
        Assert.Equal(401, result.StatusCode);
        AssertZeroEffects(port);
    }

    [Theory]
    [InlineData("Issuer", 401)] [InlineData("Audience", 401)] [InlineData("Client", 401)] [InlineData("TokenFamily", 401)] [InlineData("Owner", 403)] [InlineData("Operation", 403)] [InlineData("Permission", 403)]
    public async Task Exact_identity_and_authority_guards_fail_before_provider(string dimension, int status)
    {
        var request = Request(); var context = Trusted(request);
        context = dimension switch { "Issuer" => context with { Issuer = "wrong" }, "Audience" => context with { Audience = "wrong" }, "Client" => context with { ClientId = "credential-alone" }, "TokenFamily" => context with { TokenFamily = "service-token" }, "Owner" => context with { OwnerModule = "MOD-0008" }, "Operation" => context with { Operation = "wrong" }, _ => context with { Permission = "wrong" } };
        var port = Port(); var result = await new DecisionTraceValidationService(port).ValidateAsync(request, Binding, context, default);
        Assert.Equal(status, result.StatusCode); AssertZeroEffects(port);
    }

    [Theory]
    [InlineData(TrustedAuthorityState.Denied, TrustedAuthorityState.Current)]
    [InlineData(TrustedAuthorityState.Current, TrustedAuthorityState.Denied)]
    public async Task Entitlement_and_explicit_grant_are_both_required(TrustedAuthorityState entitlement, TrustedAuthorityState grant)
    {
        var request = Request(); var port = Port(); var result = await new DecisionTraceValidationService(port).ValidateAsync(request, Binding, Trusted(request) with { EntitlementState = entitlement, ExplicitTenantGrantState = grant }, default);
        Assert.Equal(403, result.StatusCode); AssertZeroEffects(port);
    }

    [Theory]
    [InlineData(TrustedAuthorityState.Stale, 409)] [InlineData(TrustedAuthorityState.Unavailable, 503)]
    public async Task Freshness_fence_is_fail_closed(TrustedAuthorityState state, int status)
    {
        var request = Request(); var port = Port(); var result = await new DecisionTraceValidationService(port).ValidateAsync(request, Binding, Trusted(request) with { PrincipalFreshness = state }, default);
        Assert.Equal(status, result.StatusCode); AssertZeroEffects(port);
    }

    [Fact]
    public async Task Delegated_proof_and_effective_actor_must_bind()
    {
        var request = Request(); var port = Port(); var result = await new DecisionTraceValidationService(port).ValidateAsync(request, Binding, Trusted(request) with { DelegatedActorId = Guid.NewGuid() }, default);
        Assert.Equal(401, result.StatusCode); AssertZeroEffects(port);
    }

    [Theory]
    [InlineData(DecisionReferenceProviderResultKind.AuthenticationFailure, 401)] [InlineData(DecisionReferenceProviderResultKind.PermissionDenied, 403)]
    [InlineData(DecisionReferenceProviderResultKind.NotFound, 404)] [InlineData(DecisionReferenceProviderResultKind.Ineligible, 409)]
    [InlineData(DecisionReferenceProviderResultKind.Stale, 409)] [InlineData(DecisionReferenceProviderResultKind.Conflict, 409)]
    [InlineData(DecisionReferenceProviderResultKind.UnsupportedVersion, 503)] [InlineData(DecisionReferenceProviderResultKind.Timeout, 503)]
    [InlineData(DecisionReferenceProviderResultKind.Unavailable, 503)] [InlineData(DecisionReferenceProviderResultKind.Malformed, 503)] [InlineData(DecisionReferenceProviderResultKind.Indeterminate, 503)]
    public async Task Provider_results_map_exactly(DecisionReferenceProviderResultKind kind, int status)
    {
        var request = Request(); var port = new RecordingPort(new(kind)); var result = await new DecisionTraceValidationService(port).ValidateAsync(request, Binding, Trusted(request), default);
        Assert.Equal(status, result.StatusCode); AssertOneReadNoWrites(port);
    }

    [Fact]
    public async Task Missing_and_cross_tenant_are_identical_nondisclosing_404()
    {
        var request = Request(); var missingPort = new RecordingPort(new(DecisionReferenceProviderResultKind.NotFound)); var crossTenantPort = new RecordingPort(new(DecisionReferenceProviderResultKind.NotFound));
        var missing = await new DecisionTraceValidationService(missingPort).ValidateAsync(request, Binding, Trusted(request), default);
        var crossTenant = await new DecisionTraceValidationService(crossTenantPort).ValidateAsync(request, Binding, Trusted(request), default);
        Assert.Equal(missing, crossTenant); Assert.Equal(DecisionTraceFailureCodes.NotFound, missing.FailureCode); Assert.Null(missing.Reference);
        AssertOneReadNoWrites(missingPort); AssertOneReadNoWrites(crossTenantPort);
    }

    [Fact]
    public async Task Exact_resolved_reference_is_contract_valid_but_non_runtime_fence_returns_503()
    {
        var request = Request(); var valid = Result(request, true, DecisionReferenceDisposition.Published); var acceptedPort = new RecordingPort(valid);
        var fenced = await new DecisionTraceValidationService(acceptedPort).ValidateAsync(request, Binding, Trusted(request), default); Assert.Equal(503, fenced.StatusCode); Assert.Equal(DecisionTraceFailureCodes.NonRuntimeContractOnly, fenced.FailureCode); AssertOneReadNoWrites(acceptedPort);
        var mismatch = await Run(valid with { Reference = new(DecisionId, Guid.NewGuid(), 3) }, request); Assert.Equal(503, mismatch.StatusCode); Assert.Equal(DecisionTraceFailureCodes.DependencyUnavailable, mismatch.FailureCode);
        Assert.Equal(503, (await Run(valid with { Disposition = null }, request)).StatusCode);
    }

    [Fact]
    public async Task Cancellation_propagates_and_read_path_has_zero_write_residue()
    {
        var request = Request(); using var source = new CancellationTokenSource(); source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new DecisionTraceValidationService(new CancellationPort()).ValidateAsync(request, Binding, Trusted(request), source.Token));
        Assert.False(DecisionTraceReadOnlyContract.RequiresIdempotencyKey); Assert.False(DecisionTraceReadOnlyContract.PersistsReceipt); Assert.False(DecisionTraceReadOnlyContract.PersistsAuditIntent); Assert.False(DecisionTraceReadOnlyContract.PersistsOutbox); Assert.False(DecisionTraceReadOnlyContract.PersistsCache); Assert.False(DecisionTraceReadOnlyContract.UsesLastKnownGoodAllow); Assert.False(DecisionTraceReadOnlyContract.AccessesProducerPersistence); Assert.False(DecisionTraceReadOnlyContract.MutatesInvestmentCase);
    }

    [Fact]
    public void Mod0023_and_runtime_surfaces_are_absent()
    {
        var types = typeof(DecisionTraceValidationService).Assembly.GetTypes().Concat(typeof(DecisionRevisionReferenceV1).Assembly.GetTypes()).Where(type => type.Namespace?.Contains("GateI.DecisionTrace", StringComparison.Ordinal) == true).ToArray();
        Assert.DoesNotContain(types, type => type.FullName?.Contains("ApprovalOutcome", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(types, type => type.Name.Contains("Controller", StringComparison.Ordinal) || type.Name.Contains("DbContext", StringComparison.Ordinal) || type.Name.Contains("Repository", StringComparison.Ordinal));
        Assert.DoesNotContain(typeof(DecisionTraceValidationService).Assembly.GetReferencedAssemblies(), assembly => assembly.Name is "Diten.PpmService.Persistence" or "Diten.PpmService.Infrastructure" or "Diten.PpmService.Api");
    }

    private static GoverningDecisionReferenceV1 Governing() => new(new(InvestmentCaseId), new(DecisionId, RevisionId, 3));
    private static DecisionTraceValidationRequest Request(DecisionTraceValidationMode mode = DecisionTraceValidationMode.HistoricalResolve) => new(Governing(), mode);
    private static DecisionReferenceProviderResult Result(DecisionTraceValidationRequest request, bool eligible, DecisionReferenceDisposition disposition) => new(DecisionReferenceProviderResultKind.Resolved, request.Reference.DecisionRevisionReference, request.Mode, true, eligible, disposition);
    private static RecordingPort Port() => new(new(DecisionReferenceProviderResultKind.Unavailable));
    private static async Task<DecisionTraceValidationOutcome> Run(DecisionReferenceProviderResult result, DecisionTraceValidationRequest request) => await new DecisionTraceValidationService(new RecordingPort(result)).ValidateAsync(request, Binding, Trusted(request), default);
    private static DecisionTraceTrustedContext Trusted(DecisionTraceValidationRequest request) => new(TenantId, ActorId, Guid.Parse("20000000-0000-4000-8000-000000000003"), DecisionTraceProducerProfile.Issuer, DecisionTraceProducerProfile.Audience, DecisionTraceProducerProfile.ClientId, DecisionTraceProducerProfile.TokenFamily, DecisionTraceProducerProfile.ProtocolScope, DecisionTraceProducerProfile.OwnerModule, DecisionTraceProducerProfile.Operation, DecisionTraceProducerProfile.Permission, DecisionTraceRequestBinding.Compute(Binding, TenantId, request), true, true, true, ActorId, TrustedAuthorityState.Current, TrustedAuthorityState.Current, TrustedAuthorityState.Current, TrustedAuthorityState.Current, TrustedAuthorityState.Current);
    private static void AssertZeroEffects(RecordingPort port) { Assert.Equal(0, port.Calls); Assert.Equal(0, port.Reads); Assert.Equal(0, port.Mutations); Assert.Equal(0, port.Receipts); Assert.Equal(0, port.Audits); Assert.Equal(0, port.Outbox); }
    private static void AssertOneReadNoWrites(RecordingPort port) { Assert.Equal(1, port.Calls); Assert.Equal(1, port.Reads); Assert.Equal(0, port.Mutations); Assert.Equal(0, port.Receipts); Assert.Equal(0, port.Audits); Assert.Equal(0, port.Outbox); }

    private sealed class RecordingPort(DecisionReferenceProviderResult result) : IDecisionReferenceValidationPort
    {
        public int Calls { get; private set; } public int Reads => Calls; public int Mutations => 0; public int Receipts => 0; public int Audits => 0; public int Outbox => 0;
        public Task<DecisionReferenceProviderResult> ValidateAsync(DecisionTraceValidationRequest request, DecisionTraceTrustedContext trustedContext, CancellationToken cancellationToken) { Calls++; return Task.FromResult(result); }
    }
    private sealed class CancellationPort : IDecisionReferenceValidationPort { public Task<DecisionReferenceProviderResult> ValidateAsync(DecisionTraceValidationRequest request, DecisionTraceTrustedContext trustedContext, CancellationToken cancellationToken) => Task.FromCanceled<DecisionReferenceProviderResult>(cancellationToken); }
}
