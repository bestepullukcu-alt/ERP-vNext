using Diten.ManagementGovernanceService.Application.DecisionRegistry.GateI;
using Diten.ManagementGovernanceService.Domain.DecisionRegistry;
using Xunit;

namespace Diten.ManagementGovernanceService.Tests.DecisionRegistry.GateI;

public sealed class DecisionReferenceValidationProviderTests
{
    private static readonly Guid Tenant = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid Case = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid Decision = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid Revision = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    [Fact]
    public async Task Exact_profile_and_published_revision_are_accepted_without_writes()
    {
        var reader = new Reader(Snapshot(DecisionDisposition.Published));
        var result = await Provider(reader).ValidateAsync(Request(), Context(), default);
        Assert.Equal(200, result.StatusCode);
        Assert.True(result.Resolved);
        Assert.True(result.Eligible);
        Assert.Equal(1, reader.Reads);
    }

    [Theory]
    [InlineData(DecisionReferenceValidationMode.HistoricalResolve, 200)]
    [InlineData(DecisionReferenceValidationMode.NewReferenceEligibility, 409)]
    [InlineData(DecisionReferenceValidationMode.CurrentSelectionEligibility, 400)]
    public async Task Mode_matrix_is_exact(DecisionReferenceValidationMode mode, int expected)
    {
        var result = await Provider(new Reader(Snapshot(DecisionDisposition.Withdrawn)))
            .ValidateAsync(Request(mode: mode), Context(), default);
        Assert.Equal(expected, result.StatusCode);
    }

    [Fact]
    public async Task Cross_tenant_is_non_disclosing_404()
    {
        var result = await Provider(new Reader(Snapshot(tenant: Guid.NewGuid())))
            .ValidateAsync(Request(), Context(), default);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task Wrong_operation_is_403_before_reader()
    {
        var reader = new Reader(Snapshot());
        var result = await Provider(reader).ValidateAsync(Request(), Context(operation: "alias"), default);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal(0, reader.Reads);
    }

    [Fact]
    public async Task Provider_is_default_disabled()
    {
        var result = await new DecisionReferenceValidationProvider(new Reader(Snapshot()), DecisionReferenceProviderOptions.Disabled)
            .ValidateAsync(Request(), Context(), default);
        Assert.Equal(503, result.StatusCode);
    }

    private static DecisionReferenceValidationProvider Provider(Reader reader) => new(reader, new(true));
    private static DecisionReferenceValidationRequest Request(DecisionReferenceValidationMode mode = DecisionReferenceValidationMode.NewReferenceEligibility) =>
        new(DecisionReferenceValidationProfile.GoverningWrapper, "1.0", DecisionReferenceValidationProfile.ContextName, "1.0", Case,
            new DecisionRevisionReferenceV1(Decision, Revision, 1), mode);
    private static GateITrustedContext Context(string operation = DecisionReferenceValidationProfile.Operation) =>
        new(true, Tenant, Guid.NewGuid(), DecisionReferenceValidationProfile.ClientId, DecisionReferenceValidationProfile.Audience,
            DecisionReferenceValidationProfile.Scope, operation, DecisionReferenceValidationProfile.Permission, new string('a', 64));
    private static DecisionReferenceSnapshot Snapshot(DecisionDisposition disposition = DecisionDisposition.Published, Guid? tenant = null) =>
        new(true, tenant ?? Tenant, Decision, Revision, 1, "ppm.investment-case", Case, disposition, true, false);

    private sealed class Reader(DecisionReferenceSnapshot snapshot) : IDecisionReferenceSnapshotReader
    {
        public int Reads { get; private set; }
        public Task<DecisionReferenceSnapshot?> ReadAsync(Guid tenantId, Guid decisionId, Guid revisionId, CancellationToken cancellationToken)
        { Reads++; return Task.FromResult<DecisionReferenceSnapshot?>(snapshot); }
    }
}
