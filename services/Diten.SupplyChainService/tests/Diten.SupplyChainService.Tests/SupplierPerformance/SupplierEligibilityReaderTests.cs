using System.Net;
using Diten.SupplyChainService.Application.Features.SupplierPerformance;
using Diten.SupplyChainService.Domain.Features.SupplierPerformance;
using Diten.SupplyChainService.Infrastructure.Features.SupplierPerformance;
using Microsoft.Extensions.Configuration;
using Xunit;
namespace Diten.SupplyChainService.Tests.SupplierPerformance;

/// <summary>T-0147-V1-ELIG-01 at the reader: the D2 mapping table of MOD-0147 §0.0, against <see cref="FakeProcurement"/>.</summary>
public sealed class SupplierEligibilityReaderTests
{
    private readonly FakeProcurement _procurement = new();
    private readonly SupplierPerformanceRequestContext _context = new()
    {
        Scope = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), CorrelationId = Guid.NewGuid(), Authorization = "Bearer caller-token"
    };

    private SupplierEligibilityReader Reader(string? baseUrl = "http://gateway.test/", TimeSpan? timeout = null) => new(
        new HttpClient(_procurement) { Timeout = timeout ?? TimeSpan.FromSeconds(5) },
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { [SupplierEligibilityReader.BaseUrlKey] = baseUrl }).Build(),
        _context);

    private async Task<SupplierPerformanceFailure> Refused(string supplierId = FakeProcurement.Code, SupplierEligibilityReader? reader = null) =>
        await Assert.ThrowsAsync<SupplierPerformanceFailure>(() => (reader ?? Reader()).CanonicalSupplierIdAsync(supplierId, CancellationToken.None));

    private void Answer(HttpStatusCode status, string? body) => _procurement.Answer = _ => (status, body);

    [Fact]
    public async Task Read_WhenProcurementAnswersTheDeclared200_ShouldReturnTheCanonicalCodeAndForwardTheCallersContext()
    {
        Assert.Equal(FakeProcurement.Code, await Reader().CanonicalSupplierIdAsync(FakeProcurement.Code, CancellationToken.None));

        var call = Assert.Single(_procurement.Calls);
        Assert.Equal("/api/suppliers/SUP-2001/eligibility", call.Path);
        Assert.Equal("Bearer caller-token", call.Authorization);
        Assert.Equal(_context.Scope.TenantId.ToString(), call.Tenant);
        Assert.Equal(_context.Scope.LegalEntityId.ToString(), call.LegalEntity);
        Assert.Equal(_context.CorrelationId.ToString(), call.Correlation);
    }

    [Fact]
    public async Task Read_WhenTheRequestNamesTheMasterUuid_ShouldResolveItToTheCanonicalCode()
    {
        Assert.Equal(FakeProcurement.Code, await Reader().CanonicalSupplierIdAsync(FakeProcurement.MasterId.ToUpperInvariant(), CancellationToken.None));
    }

    // Q1, Q2: any relationship status and onboarding status may be evaluated.
    [Theory]
    [InlineData("Active", "Approved")] [InlineData("OnHold", "Approved")] [InlineData("Blocked", "Approved")] [InlineData("Inactive", "Approved")]
    [InlineData("Active", "Draft")] [InlineData("Active", "InReview")] [InlineData("Active", "Rejected")] [InlineData("Active", null)]
    public async Task Read_WhenTheRelationshipIsBlockedOrNotApproved_ShouldStillBeEvaluable(string status, string? onboarding)
    {
        Answer(HttpStatusCode.OK, FakeProcurement.Eligibility(status: status, onboarding: onboarding).ToJsonString());

        Assert.Equal(FakeProcurement.Code, await Reader().CanonicalSupplierIdAsync(FakeProcurement.Code, CancellationToken.None));
    }

    [Fact]
    public async Task Read_WhenProcurementAnswers404UnknownSupplier_ShouldAnswer404UnknownSupplier()
    {
        Answer(HttpStatusCode.NotFound, FakeProcurement.Error("UNKNOWN_SUPPLIER"));

        var failure = await Refused();

        Assert.Equal((404, "UNKNOWN_SUPPLIER"), (failure.Status, failure.Code));
    }

    [Fact]
    public async Task Read_WhenA200SaysThereIsNoRelationship_ShouldAnswer404UnknownSupplier()
    {
        Answer(HttpStatusCode.OK, FakeProcurement.Eligibility(masterId: null, status: null, onboarding: null, relationship: false).ToJsonString());

        Assert.Equal((404, "UNKNOWN_SUPPLIER"), ((await Refused()).Status, (await Refused()).Code));
    }

    [Theory]
    [InlineData("{\"error\":{\"code\":\"LEGAL_ENTITY_LOOKUP_FORBIDDEN\",\"message\":\"x\",\"correlationId\":\"req-9\"},\"contractVersion\":\"v1\"}")]
    [InlineData(null)]
    public async Task Read_WhenProcurementAnswers403_ShouldAnswer403Forbidden(string? body)
    {
        Answer(HttpStatusCode.Forbidden, body);

        var failure = await Refused();

        Assert.Equal((403, "FORBIDDEN"), (failure.Status, failure.Code));
    }

    // Every other 404: UNKNOWN_LEGAL_ENTITY (C2), a route 404 before MOD-0140 DL-3, a ProblemDetails 404 without code, no body.
    [Theory]
    [InlineData("{\"error\":{\"code\":\"UNKNOWN_LEGAL_ENTITY\",\"message\":\"x\",\"correlationId\":\"req-5\"},\"contractVersion\":\"v1\"}")]
    [InlineData("{\"type\":\"https://tools.ietf.org/html/rfc9110#section-15.5.5\",\"title\":\"Not Found\",\"status\":404}")]
    [InlineData("{\"error\":{\"code\":\"SOMETHING_ELSE\"}}")]
    [InlineData("not json")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Read_WhenProcurementAnswersAnyOther404_ShouldFailClosedWith503(string? body)
    {
        Answer(HttpStatusCode.NotFound, body);

        var failure = await Refused();

        Assert.Equal((503, "SUPPLIER_BASE_UNAVAILABLE"), (failure.Status, failure.Code));
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)] [InlineData(HttpStatusCode.Unauthorized)] [InlineData(HttpStatusCode.Conflict)]
    [InlineData(HttpStatusCode.UnprocessableEntity)] [InlineData(HttpStatusCode.InternalServerError)] [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)] [InlineData(HttpStatusCode.GatewayTimeout)] [InlineData(HttpStatusCode.Created)]
    public async Task Read_WhenProcurementAnswersAnUnexpectedStatus_ShouldFailClosedWith503(HttpStatusCode status)
    {
        Answer(status, status == HttpStatusCode.ServiceUnavailable ? FakeProcurement.Error("DEPENDENCY_UNAVAILABLE") : FakeProcurement.Eligibility().ToJsonString());

        Assert.Equal((503, "SUPPLIER_BASE_UNAVAILABLE"), ((await Refused()).Status, (await Refused()).Code));
    }

    // AC-04: a malformed 200 is a dependency failure.
    [Theory]
    [InlineData("status", null)] [InlineData("status", "Unknown")] [InlineData("supplierMasterId", null)] [InlineData("supplierMasterId", "not-a-uuid")]
    [InlineData("supplierId", null)] [InlineData("supplierId", "sup-2001")] [InlineData("supplierId", "LEGACY-17")] [InlineData("onboardingStatus", "Done")]
    [InlineData("relationshipExists", "true")] [InlineData("eligibleForTransactions", null)] [InlineData("purchasingEnabled", "yes")]
    public async Task Read_WhenThe200DoesNotMatchTheSupplierEligibilitySchema_ShouldFailClosedWith503(string field, string? value)
    {
        var body = FakeProcurement.Eligibility();
        body[field] = value;
        Answer(HttpStatusCode.OK, body.ToJsonString());

        Assert.Equal((503, "SUPPLIER_BASE_UNAVAILABLE"), ((await Refused()).Status, (await Refused()).Code));
    }

    [Theory]
    [InlineData("supplierId")] [InlineData("supplierMasterId")] [InlineData("relationshipExists")] [InlineData("status")]
    [InlineData("onboardingStatus")] [InlineData("purchasingEnabled")] [InlineData("eligibleForTransactions")]
    public async Task Read_WhenA200LacksARequiredField_ShouldFailClosedWith503(string field)
    {
        var body = FakeProcurement.Eligibility();
        body.Remove(field);
        Answer(HttpStatusCode.OK, body.ToJsonString());

        Assert.Equal(503, (await Refused()).Status);
    }

    [Theory]
    [InlineData("[]")] [InlineData("null")] [InlineData("{")] [InlineData("")]
    public async Task Read_WhenThe200IsNotAnObject_ShouldFailClosedWith503(string body)
    {
        Answer(HttpStatusCode.OK, body);

        Assert.Equal(503, (await Refused()).Status);
    }

    // Natig P1, review F-2: the answer must name the requested supplier.
    [Fact]
    public async Task Read_WhenThe200NamesAnotherValidSupplier_ShouldFailClosedWith503()
    {
        Answer(HttpStatusCode.OK, FakeProcurement.Eligibility(supplierId: "SUP-9999", masterId: Guid.NewGuid().ToString()).ToJsonString());

        Assert.Equal(503, (await Refused(FakeProcurement.Code)).Status);
        Assert.Equal(503, (await Refused(FakeProcurement.MasterId)).Status);
    }

    [Fact]
    public async Task Read_WhenProcurementIsUnreachableOrSlowOrUnconfigured_ShouldFailClosedWith503()
    {
        _procurement.Throw = new HttpRequestException("connection refused");
        Assert.Equal(503, (await Refused()).Status);

        _procurement.Throw = null; _procurement.Delay = TimeSpan.FromSeconds(2);
        Assert.Equal(503, (await Refused(reader: Reader(timeout: TimeSpan.FromMilliseconds(200)))).Status);

        _procurement.Delay = TimeSpan.Zero;
        var calls = _procurement.Calls.Count;
        Assert.Equal(503, (await Refused(reader: Reader(baseUrl: null))).Status);
        Assert.Equal(503, (await Refused(reader: Reader(baseUrl: "ftp://gateway.test/"))).Status);
        Assert.Equal(calls, _procurement.Calls.Count);
    }
}
