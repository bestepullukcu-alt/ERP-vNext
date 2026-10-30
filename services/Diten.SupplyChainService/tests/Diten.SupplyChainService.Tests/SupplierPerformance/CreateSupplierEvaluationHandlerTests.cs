using System.Net;
using System.Text.Json;
using Diten.SupplyChainService.Application.Features.SupplierPerformance;
using Diten.SupplyChainService.Application.Features.SupplierPerformance.Commands;
using Diten.SupplyChainService.Application.Features.SupplierPerformance.Handlers.CommandHandlers;
using Diten.SupplyChainService.Domain.Features.SupplierPerformance;
using Xunit;
namespace Diten.SupplyChainService.Tests.SupplierPerformance;

/// <summary>
/// T-0147-RP-07 without a race: the state the concurrency tests only sometimes reach — the winner's commit is visible to a local
/// read and not yet to the majority snapshot — held still by a fake repository. Both clash paths must re-read with afterClash.
/// </summary>
public sealed class CreateSupplierEvaluationHandlerTests
{
    private const string Body = """{"supplierId":"SUP-2001","periodStart":"2026-07-01","periodEnd":"2026-07-31","metrics":[{"metricCode":"OTD","measuredValue":"90","weight":"25"},{"metricCode":"QTY_ACC","measuredValue":"80","weight":"25"},{"metricCode":"QUAL_ACC","measuredValue":"100","weight":"25"}]}""";

    private sealed class MajorityBehindRepository(bool activePeriod, EvaluationReceipt? winner) : IEvaluationRepository
    {
        public Task<EvaluationReceipt?> FindCreateReceiptAsync(SupplierPerformanceScope scope, string key, CancellationToken ct, bool afterClash = false) =>
            Task.FromResult(afterClash ? winner : null);
        public Task<bool> ActivePeriodExistsAsync(SupplierPerformanceScope scope, string supplierId, string periodStart, string periodEnd, CancellationToken ct) => Task.FromResult(activePeriod);
        public Task<EvaluationCommitOutcome> CreateAsync(SupplierPerformanceScope scope, string key, string fingerprint, SupplierEvaluation evaluation, string responseBody, CancellationToken ct) =>
            Task.FromResult(EvaluationCommitOutcome.Clash);
        public Task<SupplierEvaluation?> GetAsync(SupplierPerformanceScope scope, Guid evaluationId, CancellationToken ct) => throw new NotSupportedException();
        public Task<(IReadOnlyList<SupplierEvaluation> Items, long Total)> ListAsync(SupplierPerformanceScope scope, EvaluationFilter filter, int page, int pageSize, CancellationToken ct) => throw new NotSupportedException();
        public Task<SupplierEvaluation?> GetPublishedAsync(SupplierPerformanceScope scope, Guid scorecardId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<SupplierEvaluation>> ListPublishedAsync(SupplierPerformanceScope scope, ScorecardFilter filter, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class Eligible : ISupplierEligibilityReader
    { public Task<string> CanonicalSupplierIdAsync(string requestedSupplierId, CancellationToken ct) => Task.FromResult(FakeProcurement.Code); }

    private static Task<Diten.SupplyChainService.Application.Common.Response<EvaluationResponse>> Handle(IEvaluationRepository repository)
    {
        var context = new SupplierPerformanceRequestContext { Scope = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), CorrelationId = Guid.NewGuid(), IdempotencyKey = "k" };
        return new CreateSupplierEvaluationHandler(repository, context, new Eligible(), TimeProvider.System)
            .Handle(new CreateSupplierEvaluationCommand(JsonDocument.Parse(Body).RootElement), CancellationToken.None);
    }

    private static EvaluationReceipt Receipt() => new(
        SupplierPerformanceRequestFingerprint.Create(SupplierPerformanceWire.Parse(JsonDocument.Parse(Body).RootElement)), 201,
        JsonSerializer.Serialize(new EvaluationResponse("id", FakeProcurement.Code, "2026-07-01", "2026-07-31", "DRAFT", [], "90.00", "LOW", 1, Guid.NewGuid()), new JsonSerializerOptions(JsonSerializerDefaults.Web)));

    [Theory]
    [InlineData(true)]  // the active-period check of stage 7 saw the winner
    [InlineData(false)] // the unique index of stage 8 refused the write
    public async Task Handle_WhenTheWinnersReceiptIsVisibleOnlyAfterAClash_ShouldReplay(bool activePeriod)
    {
        var answer = await Handle(new MajorityBehindRepository(activePeriod, Receipt()));

        Assert.Equal(((int)HttpStatusCode.OK, "id"), (answer.StatusCode, answer.Data!.EvaluationId));
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task Handle_WhenAClashHasNoReceiptForThisKey_ShouldAnswerDuplicate(bool activePeriod)
    {
        var answer = await Handle(new MajorityBehindRepository(activePeriod, null));

        Assert.Equal((409, "DUPLICATE_EVALUATION"), (answer.StatusCode, answer.ErrorCode));
    }
}
