using System.Globalization;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Recall.Commands;
using Diten.InventoryService.Application.Features.Recall.Ports;
using Diten.InventoryService.Application.Features.Recall.Quarantine;
using Diten.InventoryService.Application.Features.Recall.Queries;
using Diten.InventoryService.Application.Features.Recall.Services;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Recall.Entities;
using Diten.InventoryService.Domain.Recall.Enums;
using Diten.InventoryService.Domain.Recall.Repositories;
using Diten.InventoryService.Domain.Recall.Rules;
using Diten.InventoryService.Domain.Recall.Trace;
using Diten.InventoryService.Domain.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.Recall.Handlers;

/// <summary>R3 shared: the wire shape of a request with its MOD-0175 holds read live (never copied).</summary>
public static class RecallR3Mapping
{
    public static RecallQuarantineRequestDto Request(RecallQuarantineRequest r, IReadOnlyDictionary<string, RecallHoldView> holds, IReadOnlyList<RecallHoldView>? existing = null)
    {
        var live = r.HoldIds.Where(holds.ContainsKey).Select(id => holds[id])
            .Concat(existing ?? []).DistinctBy(h => h.HoldId)
            .Select(h => new RecallHoldDto(h.HoldId, h.HoldNumber, h.Status, h.ReviewState, h.Quantity)).ToList();
        return new RecallQuarantineRequestDto(r.Id.ToString("D"), r.Status.ToString(), r.NotNeededReason?.ToString(),
            r.HoldIds.Count > 0 ? r.HoldIds : live.Select(h => h.HoldId).ToList(), r.Attempts, r.NodeKey, r.Revision,
            new RecallLotRefDto(r.LotId, r.LotNumber), r.WarehouseId, r.LocationId, r.SnapshotQuantity, r.PlannedQuantity, r.HeldQuantity, r.BaseUomId, r.RetryNumber,
            r.ErrorCode, r.ErrorMessage, r.Reservations.Select(x => new RecallReservationLineDto(x.ReservationId, x.HolderModule, x.SourceDocumentId, x.RemainingQuantity)).ToList(),
            live, RecallSupport.Wire(r.RequestedAtUtc), r.LastAttemptAtUtc is { } at ? RecallSupport.Wire(at) : null);
    }

    public static async Task<IReadOnlyList<RecallQuarantineRequestDto>> RequestsAsync(IRecallQuarantineRequester requester, IReadOnlyList<RecallQuarantineRequest> requests,
        CancellationToken ct)
    {
        var holds = (await requester.HoldsAsync(requests.SelectMany(r => r.HoldIds).Distinct(StringComparer.Ordinal).ToList(), ct))
            .ToDictionary(h => h.HoldId, StringComparer.Ordinal);
        return requests.Select(r => Request(r, holds)).ToList();
    }

    /// <summary>The lots the case blocks (active) — "in-scope lots" of the stock and returns tabs.</summary>
    public static async Task<IReadOnlyList<RecallLotBlock>> InScopeLotsAsync(IRecallRepository repository, RecallCase c, CancellationToken ct)
        => (await repository.LotBlocksOfCaseAsync(c.Id, ct)).Where(b => b.Active).DistinctBy(b => b.LotId).ToList();

    public static string StockKey(RecallStockBucket b) => TraceRules.StockKey(b.LotId, b.WarehouseId, b.LocationId, b.Status);
}

/// <summary>
/// §4.38 getCaseStock — "Eldeki stok": the LIVE stock lines (caller's company) of the lots the case blocks, each with its quarantine request
/// (KR-B) and the MOD-0175 hold status read now; requests whose stock moved stay listed with quantity 0. Segregated quantity per line.
/// </summary>
public sealed class GetRecallCaseStockHandler : IRequestHandler<GetRecallCaseStockQuery, Response<IReadOnlyList<RecallStockLineDto>>>
{
    private readonly IRecallRepository _repository;
    private readonly IRecallLedgerReader _ledger;
    private readonly IRecallQuarantineRequester _requester;

    public GetRecallCaseStockHandler(IRecallRepository repository, IRecallLedgerReader ledger, IRecallQuarantineRequester requester)
    {
        _repository = repository;
        _ledger = ledger;
        _requester = requester;
    }

    public async Task<Response<IReadOnlyList<RecallStockLineDto>>> Handle(GetRecallCaseStockQuery request, CancellationToken ct)
    {
        if (!RecallSupport.TryParseId(request.CaseId, out var caseId) || await _repository.FindCaseAsync(caseId, ct) is not { } c)
        {
            return RecallSupport.CaseNotFound<IReadOnlyList<RecallStockLineDto>>();
        }

        var lots = await RecallR3Mapping.InScopeLotsAsync(_repository, c, ct);
        var buckets = await _ledger.StockOfLotsAsync(lots.Select(l => l.LotId).ToList(), ct);
        var requests = (await _repository.QuarantineRequestsAsync(c.Id, ct)).GroupBy(r => r.NodeKey, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.Revision).First(), StringComparer.Ordinal);
        var holds = (await _requester.HoldsAsync(requests.Values.SelectMany(r => r.HoldIds).Distinct(StringComparer.Ordinal).ToList(), ct))
            .ToDictionary(h => h.HoldId, StringComparer.Ordinal);
        var openByLot = new Dictionary<string, IReadOnlyList<RecallHoldView>>(StringComparer.Ordinal);
        foreach (var lotId in requests.Values.Where(r => r.NotNeededReason == RecallNotNeededReason.AlreadyQuarantined).Select(r => r.LotId).Distinct(StringComparer.Ordinal))
        {
            openByLot[lotId] = await _requester.OpenHoldsOfLotAsync(lotId, ct);
        }

        var segregated = (await _repository.SegregationsAsync(c.Id, ct)).GroupBy(s => s.NodeKey, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Sum(s => s.Quantity), StringComparer.Ordinal);
        var numbers = lots.ToDictionary(l => l.LotId, l => l.LotNumber ?? l.LotId, StringComparer.Ordinal);

        RecallQuarantineRequestDto? RequestOf(string key)
        {
            if (!requests.TryGetValue(key, out var r)) return null;
            var existing = r.NotNeededReason == RecallNotNeededReason.AlreadyQuarantined && openByLot.TryGetValue(r.LotId, out var open)
                ? open.Where(h => h.WarehouseId == r.WarehouseId && h.LocationId == r.LocationId).ToList()
                : null;
            return RecallR3Mapping.Request(r, holds, existing);
        }

        var lines = buckets.Select(b =>
        {
            var key = RecallR3Mapping.StockKey(b);
            return new RecallStockLineDto(key, new RecallLotRefDto(b.LotId, numbers.GetValueOrDefault(b.LotId, b.LotId)),
                new RecallItemRefDto(b.ItemId.ToString("D"), b.SkuId.ToString("D"), WireNames.ToWire(b.SkuLevel)), b.WarehouseId, b.LocationId,
                WireNames.ToWire(b.Status), b.OnHand, b.BaseUomId, RequestOf(key), segregated.GetValueOrDefault(key));
        }).ToList();
        var live = lines.Select(l => l.NodeKey).ToHashSet(StringComparer.Ordinal);
        foreach (var r in requests.Values.Where(r => !live.Contains(r.NodeKey)))
        {
            lines.Add(new RecallStockLineDto(r.NodeKey, new RecallLotRefDto(r.LotId, r.LotNumber),
                new RecallItemRefDto(r.ItemId.ToString("D"), r.SkuId.ToString("D"), WireNames.ToWire(r.SkuLevel)), r.WarehouseId, r.LocationId, null, 0m,
                r.BaseUomId ?? string.Empty, RequestOf(r.NodeKey), segregated.GetValueOrDefault(r.NodeKey)));
        }

        return Response<IReadOnlyList<RecallStockLineDto>>.Success(lines.OrderBy(l => l.NodeKey, StringComparer.Ordinal).ToList());
    }
}

/// <summary>
/// §4.39 requestQuarantine — "Karantina isteği gönder" for live AVAILABLE stock lines of the case's lots that have no request (stock that arrived
/// later — transfer in). Same port, same key scheme, the caller is the actor (CT-R2). 202 with the requests after the dispatch.
/// </summary>
public sealed class RequestRecallQuarantineHandler : IRequestHandler<RequestRecallQuarantineCommand, Response<IReadOnlyList<RecallQuarantineRequestDto>>>
{
    private readonly IRecallRepository _repository;
    private readonly IRecallLedgerReader _ledger;
    private readonly IRecallQuarantineRequester _requester;
    private readonly RecallQuarantineDispatcher _dispatcher;
    private readonly RecallSupport _support;

    public RequestRecallQuarantineHandler(IRecallRepository repository, IRecallLedgerReader ledger, IRecallQuarantineRequester requester,
        RecallQuarantineDispatcher dispatcher, RecallSupport support)
    {
        _repository = repository;
        _ledger = ledger;
        _requester = requester;
        _dispatcher = dispatcher;
        _support = support;
    }

    public async Task<Response<IReadOnlyList<RecallQuarantineRequestDto>>> Handle(RequestRecallQuarantineCommand request, CancellationToken ct)
    {
        var keys = request.NodeKeys.Distinct(StringComparer.Ordinal).ToList();
        if (keys.Count is < 1 or > 50 || keys.Count != request.NodeKeys.Count)
        {
            return RecallSupport.Invalid<IReadOnlyList<RecallQuarantineRequestDto>>("nodeKeys: 1-50 distinct stock line keys.");
        }

        var fingerprint = RecallSupport.Fingerprint(("op", "requestQuarantine"), ("id", request.CaseId.ToString("D")), ("keys", string.Join("|", keys.Order(StringComparer.Ordinal))));
        var c = await _repository.FindCaseAsync(request.CaseId, ct);
        if (c is null) return RecallSupport.CaseNotFound<IReadOnlyList<RecallQuarantineRequestDto>>();

        var all = await _repository.QuarantineRequestsAsync(c.Id, ct);
        var earlier = all.Where(r => r.CommandKey == request.IdempotencyKey && r.RequestedBy == _support.UserId && r.RetryNumber == 0).ToList();
        if (earlier.Count > 0)
        {
            return earlier.All(r => r.CommandFingerprint == fingerprint)
                ? Response<IReadOnlyList<RecallQuarantineRequestDto>>.Success(await RecallR3Mapping.RequestsAsync(_requester, earlier, ct), 202)
                : RecallSupport.KeyReused<IReadOnlyList<RecallQuarantineRequestDto>>();
        }

        if (!RecallCaseRules.IsUnderRecall(c.Status) || c.ApprovedScope is null)
        {
            return RecallSupport.InvalidTransition<IReadOnlyList<RecallQuarantineRequestDto>>($"A case in status {c.Status} takes no quarantine request.");
        }

        var existing = all.Select(r => r.NodeKey).ToHashSet(StringComparer.Ordinal);
        if (keys.FirstOrDefault(existing.Contains) is { } taken)
        {
            return Response<IReadOnlyList<RecallQuarantineRequestDto>>.Fail(RecallErrorCodes.QuarantineRequestExists,
                "This stock line already has a quarantine request; use \"Yeniden dene\" on it.", 409, new Dictionary<string, object?> { ["nodeKey"] = taken });
        }

        var lots = await RecallR3Mapping.InScopeLotsAsync(_repository, c, ct);
        var numbers = lots.ToDictionary(l => l.LotId, l => l.LotNumber ?? l.LotId, StringComparer.Ordinal);
        var lines = (await _ledger.StockOfLotsAsync(lots.Select(l => l.LotId).ToList(), ct)).Where(b => b.Status == StockStatus.Available)
            .ToDictionary(RecallR3Mapping.StockKey, StringComparer.Ordinal);
        if (keys.FirstOrDefault(k => !lines.ContainsKey(k)) is { } unknown)
        {
            return RecallSupport.Invalid<IReadOnlyList<RecallQuarantineRequestDto>>($"nodeKey '{unknown}' is not a live AVAILABLE stock line of a lot this case blocks.");
        }

        var now = _support.Now();
        var revision = c.ApprovedScope.Revision;
        var created = keys.Select(k =>
        {
            var b = lines[k];
            var r = RecallQuarantinePlanner.New(c, revision, k, b.LotId, numbers.GetValueOrDefault(b.LotId, b.LotId), b.ItemId, b.SkuId, b.SkuLevel, b.WarehouseId,
                b.LocationId, b.OnHand, b.BaseUomId, RecallQuarantineRequestStatus.Pending, null, _support.UserId, now);
            r.CommandKey = request.IdempotencyKey;
            r.CommandFingerprint = fingerprint;
            return r;
        }).ToList();
        try
        {
            await _repository.InTransactionAsync<bool>(async (session, tx) =>
            {
                await session.InsertQuarantineRequestsAsync(created, tx);
                await session.InsertAuditAsync(_support.Audit(RecallContract.AuditQuarantineRequested, RecallContract.SubjectCase, c.Id, c.Number, now, request.IdempotencyKey,
                    new Dictionary<string, string?> { ["nodeKeys"] = string.Join(",", keys), ["source"] = "requestQuarantine" }), tx);
                return true;
            }, ct);
        }
        catch (RecallDuplicateException)
        {
            return Response<IReadOnlyList<RecallQuarantineRequestDto>>.Fail(RecallErrorCodes.QuarantineRequestExists, "A request for one of these stock lines was just created.", 409);
        }

        var after = await _dispatcher.DispatchAsync(created.Select(r => r.Id).ToList(), ct);
        return Response<IReadOnlyList<RecallQuarantineRequestDto>>.Success(await RecallR3Mapping.RequestsAsync(_requester, after, ct), 202);
    }
}

/// <summary>
/// §4.39a retryQuarantineRequest — "Yeniden dene" (QA): only BlockedByReservation / NothingAvailable / Failed (409
/// <c>RECALL_QUARANTINE_REQUEST_NOT_RETRYABLE</c> otherwise). The next 0175 key of the family (<c>…:a{n}</c>, CT-R2: the first key's body is
/// replay-bound), a fresh bucket quantity, the caller as actor. The recall still never touches a reservation.
/// </summary>
public sealed class RetryRecallQuarantineHandler : IRequestHandler<RetryRecallQuarantineCommand, Response<RecallQuarantineRequestDto>>
{
    private readonly IRecallRepository _repository;
    private readonly IRecallQuarantineRequester _requester;
    private readonly RecallQuarantineDispatcher _dispatcher;
    private readonly RecallSupport _support;

    public RetryRecallQuarantineHandler(IRecallRepository repository, IRecallQuarantineRequester requester, RecallQuarantineDispatcher dispatcher, RecallSupport support)
    {
        _repository = repository;
        _requester = requester;
        _dispatcher = dispatcher;
        _support = support;
    }

    public async Task<Response<RecallQuarantineRequestDto>> Handle(RetryRecallQuarantineCommand request, CancellationToken ct)
    {
        var fingerprint = RecallSupport.Fingerprint(("op", "retryQuarantine"), ("id", request.RequestId.ToString("D")));
        var r = await _repository.FindQuarantineRequestAsync(request.RequestId, ct);
        if (r is null || r.CaseId != request.CaseId || await _repository.FindCaseAsync(request.CaseId, ct) is not { } c)
        {
            return RecallSupport.CaseNotFound<RecallQuarantineRequestDto>();
        }

        if (r.CommandKey == request.IdempotencyKey && r.RetryNumber > 0)
        {
            return r.CommandFingerprint == fingerprint
                ? Response<RecallQuarantineRequestDto>.Success((await RecallR3Mapping.RequestsAsync(_requester, [r], ct))[0], 202)
                : RecallSupport.KeyReused<RecallQuarantineRequestDto>();
        }

        if (!RecallCaseRules.IsUnderRecall(c.Status) || !RecallQuarantinePlanner.IsRetryable(r.Status))
        {
            return Response<RecallQuarantineRequestDto>.Fail(RecallErrorCodes.QuarantineRequestNotRetryable,
                $"A request in status {r.Status} is not retried (only BlockedByReservation, NothingAvailable and Failed, while the case is under recall).", 409);
        }

        var now = _support.Now();
        var expected = r.Version;
        r.RetryNumber++;
        r.QuarantineKey = RecallContract.QuarantineKey(r.CaseId, r.Revision, r.NodeKey, r.RetryNumber);
        r.Status = RecallQuarantineRequestStatus.Pending;
        r.PlannedQuantity = null;
        r.ErrorCode = null;
        r.ErrorMessage = null;
        r.Reservations = [];
        r.RequestedBy = _support.UserId;
        r.CommandKey = request.IdempotencyKey;
        r.CommandFingerprint = fingerprint;
        r.Version = expected + 1;
        r.UpdatedAt = now;
        var refs = new Dictionary<string, string?> { ["requestId"] = r.Id.ToString("D"), ["nodeKey"] = r.NodeKey, ["quarantineKey"] = r.QuarantineKey };
        var saved = await _repository.InTransactionAsync(async (session, tx) =>
        {
            if (!await session.ReplaceQuarantineRequestAsync(r, expected, tx)) return false;
            await session.InsertEventAsync(_support.Event(c.Id, RecallCaseEventKind.QuarantineRetried, now, $"Yeniden dene: {r.NodeKey}", refs), tx);
            await session.InsertAuditAsync(_support.Audit(RecallContract.AuditQuarantineRetried, RecallContract.SubjectCase, c.Id, c.Number, now, request.IdempotencyKey, refs), tx);
            return true;
        }, ct);
        if (!saved) return RecallSupport.Conflict<RecallQuarantineRequestDto>();

        var after = await _dispatcher.DispatchAsync([r.Id], ct);
        return Response<RecallQuarantineRequestDto>.Success((await RecallR3Mapping.RequestsAsync(_requester, after, ct))[0], 202);
    }
}

/// <summary>
/// §4.42a getCaseReturns — "Geri dönüşler": the linked returns and the candidates = CUSTOMER_RETURN movements of the lots the case blocks (caller's
/// company), not linked to this case, each with its eligibility (posted before the approval, into AVAILABLE, or linked to another case).
/// </summary>
public sealed class GetRecallCaseReturnsHandler : IRequestHandler<GetRecallCaseReturnsQuery, Response<RecallReturnsDto>>
{
    private readonly IRecallRepository _repository;
    private readonly IRecallLedgerReader _ledger;

    public GetRecallCaseReturnsHandler(IRecallRepository repository, IRecallLedgerReader ledger)
    {
        _repository = repository;
        _ledger = ledger;
    }

    public async Task<Response<RecallReturnsDto>> Handle(GetRecallCaseReturnsQuery request, CancellationToken ct)
    {
        if (!RecallSupport.TryParseId(request.CaseId, out var caseId) || await _repository.FindCaseAsync(caseId, ct) is not { } c)
        {
            return RecallSupport.CaseNotFound<RecallReturnsDto>();
        }

        var links = await _repository.ReturnLinksAsync(c.Id, ct);
        var lots = await RecallR3Mapping.InScopeLotsAsync(_repository, c, ct);
        var numbers = lots.ToDictionary(l => l.LotId, l => l.LotNumber ?? l.LotId, StringComparer.Ordinal);
        var linkedHere = links.Select(l => l.TransactionId).ToHashSet(StringComparer.Ordinal);
        var candidates = new List<RecallReturnCandidateDto>();
        foreach (var t in await _ledger.CustomerReturnsAsync(lots.Select(l => l.LotId).ToList(), ct))
        {
            if (linkedHere.Contains(t.TransactionId)) continue;
            var reason = await RecallReturnRules.IneligibleAsync(_repository, c, t, ct);
            candidates.Add(new RecallReturnCandidateDto(t.TransactionId, t.Number, new RecallLotRefDto(t.LotId!, numbers.GetValueOrDefault(t.LotId!, t.LotId!)),
                t.SkuId.ToString("D"), t.BaseQuantity, t.BaseUomId, t.ToStockStatus is { } s ? WireNames.ToWire(s) : null, RecallSupport.Wire(t.PostedAtUtc),
                reason is null, reason));
        }

        return Response<RecallReturnsDto>.Success(new RecallReturnsDto(links.Select(RecallReturnRules.Dto).ToList(), candidates));
    }
}

/// <summary>Pack §8.6 KR-H — the eligibility of a return candidate (the same answer for the list and the link).</summary>
public static class RecallReturnRules
{
    public const string NotQuarantined = "NotQuarantined";
    public const string NotInScope = "NotInScope";
    public const string BeforeApproval = "BeforeApproval";
    public const string AlreadyLinked = "AlreadyLinked";

    public static async Task<string?> IneligibleAsync(IRecallRepository repository, RecallCase c, RecallLedgerMovement t, CancellationToken ct)
    {
        if (await repository.FindReturnLinkByTransactionAsync(t.TransactionId, ct) is not null) return AlreadyLinked;
        if (c.ScopeApprovedAtUtc is not { } approvedAt || t.PostedAtUtc < approvedAt) return BeforeApproval;
        if (t.ToStockStatus is not (StockStatus.Quarantine or StockStatus.QualityInspection or StockStatus.Blocked)) return NotQuarantined;
        return null;
    }

    public static RecallReturnLinkDto Dto(RecallReturnLink l)
        => new(l.Id.ToString("D"), l.Source.ToString(), l.TransactionId, l.TransactionNumber, new RecallLotRefDto(l.LotId, l.LotNumber), l.SkuId.ToString("D"), l.Quantity,
            l.BaseUomId, l.CustomerRef, l.ReturnReference, l.ToStockStatus is { } s ? WireNames.ToWire(s) : null, RecallSupport.Wire(l.PostedAtUtc),
            new RecallUserAtDto(l.LinkedBy.ToString("D"), RecallSupport.Wire(l.LinkedAtUtc)));
}

/// <summary>
/// §4.43 linkReturn — "İadeyi bağla" (warehouse, KR-H). The transaction is a CUSTOMER_RETURN of the caller's company of a lot the case blocks
/// (422 <c>RECALL_RETURN_NOT_ELIGIBLE</c> otherwise, also when posted before the approval or the case is not under recall); into AVAILABLE →
/// 422 <c>RECALL_RETURN_NOT_QUARANTINED</c> (returned recalled goods must be held); linked already → 409 <c>RECALL_RETURN_ALREADY_LINKED</c> (one
/// case per return); the customer must be a customer of the approved scope (404 <c>RECALL_UNKNOWN_CUSTOMER</c>). 0177 writes NO movement.
/// </summary>
public sealed class LinkRecallReturnHandler : IRequestHandler<LinkRecallReturnCommand, Response<RecallReturnLinkDto>>
{
    private readonly IRecallRepository _repository;
    private readonly IRecallLedgerReader _ledger;
    private readonly RecallSupport _support;

    public LinkRecallReturnHandler(IRecallRepository repository, IRecallLedgerReader ledger, RecallSupport support)
    {
        _repository = repository;
        _ledger = ledger;
        _support = support;
    }

    public async Task<Response<RecallReturnLinkDto>> Handle(LinkRecallReturnCommand request, CancellationToken ct)
    {
        var transactionId = RecallSupport.Trimmed(request.TransactionId);
        var customer = RecallSupport.Trimmed(request.CustomerRef);
        var reference = RecallSupport.Trimmed(request.ReturnReference);
        if (transactionId is null || customer is null || customer.Length > RecallContract.MaxCustomerRefLength || reference is { Length: > 100 })
        {
            return RecallSupport.Invalid<RecallReturnLinkDto>("transactionId and customerRef (1-200) are required; returnReference at most 100.");
        }

        var fingerprint = RecallSupport.Fingerprint(("op", "linkReturn"), ("id", request.CaseId.ToString("D")), ("tx", transactionId), ("customer", customer), ("ref", reference));
        if (await _repository.FindReturnLinkByKeyAsync(_support.UserId, request.IdempotencyKey, ct) is { } earlier)
        {
            return earlier.RequestFingerprint == fingerprint ? Response<RecallReturnLinkDto>.Success(RecallReturnRules.Dto(earlier), 200) : RecallSupport.KeyReused<RecallReturnLinkDto>();
        }

        var c = await _repository.FindCaseAsync(request.CaseId, ct);
        if (c is null) return RecallSupport.CaseNotFound<RecallReturnLinkDto>();
        if (!RecallCaseRules.IsUnderRecall(c.Status)) return NotEligible($"A case in status {c.Status} takes no return link.", "CaseNotUnderRecall");

        var t = await _ledger.TransactionAsync(transactionId, ct);
        if (t is null || t.Type != MovementType.CustomerReturn) return NotEligible("transactionId does not name a CUSTOMER_RETURN of this company.", RecallReturnRules.NotInScope);
        var lots = await RecallR3Mapping.InScopeLotsAsync(_repository, c, ct);
        var lot = lots.FirstOrDefault(l => l.LotId == t.LotId);
        if (lot is null) return NotEligible("The return is not of a lot this case blocks.", RecallReturnRules.NotInScope);

        switch (await RecallReturnRules.IneligibleAsync(_repository, c, t, ct))
        {
            case RecallReturnRules.AlreadyLinked:
                return Response<RecallReturnLinkDto>.Fail(RecallErrorCodes.ReturnAlreadyLinked, "This return is already linked to a recall case.", 409);
            case RecallReturnRules.BeforeApproval:
                return NotEligible("The return was posted before the scope was approved.", RecallReturnRules.BeforeApproval);
            case RecallReturnRules.NotQuarantined:
                return Response<RecallReturnLinkDto>.Fail(RecallErrorCodes.ReturnNotQuarantined,
                    "A returned recalled lot must be held: this return went into AVAILABLE (post it into QUARANTINE / QUALITY_INSPECTION).", 422,
                    new Dictionary<string, object?> { ["transactionId"] = t.TransactionId, ["toStockStatus"] = t.ToStockStatus is { } s ? WireNames.ToWire(s) : null });
        }

        var customers = RecallReads.Shipments(await _repository.ScopeItemsAsync(c.Id, c.ApprovedScope?.Revision ?? c.ScopeRevision, ct))
            .Select(s => s.CustomerRef).ToHashSet(StringComparer.Ordinal);
        if (!customers.Contains(customer)) return RecordRecallNotificationHandler.UnknownCustomer<RecallReturnLinkDto>(customer);

        var now = _support.Now();
        var link = new RecallReturnLink
        {
            Id = Guid.NewGuid(), CreatedAt = now, CaseId = c.Id, Source = RecallReturnSource.ManualCustomerReturn, TransactionId = t.TransactionId,
            TransactionNumber = t.Number, LotId = lot.LotId, LotNumber = lot.LotNumber ?? lot.LotId, SkuId = t.SkuId, Quantity = t.BaseQuantity, BaseUomId = t.BaseUomId,
            ToStockStatus = t.ToStockStatus, PostedAtUtc = t.PostedAtUtc, CustomerRef = customer, ReturnReference = reference, LinkedBy = _support.UserId, LinkedAtUtc = now,
            IdempotencyKey = request.IdempotencyKey, RequestFingerprint = fingerprint
        };
        var refs = new Dictionary<string, string?>
        {
            ["transactionId"] = t.TransactionId, ["quantity"] = t.BaseQuantity.ToString(CultureInfo.InvariantCulture), ["baseUomId"] = t.BaseUomId, ["customerRef"] = customer,
            ["source"] = link.Source.ToString()
        };
        try
        {
            await _repository.InTransactionAsync<bool>(async (session, tx) =>
            {
                await session.InsertReturnLinkAsync(link, tx);
                await session.InsertEventAsync(_support.Event(c.Id, RecallCaseEventKind.ReturnLinked, now, $"{t.Number ?? t.TransactionId} → {customer}", refs,
                    idempotencyKey: request.IdempotencyKey, fingerprint: fingerprint), tx);
                await session.InsertAuditAsync(_support.Audit(RecallContract.AuditReturnLinked, RecallContract.SubjectCase, c.Id, c.Number, now, request.IdempotencyKey, refs), tx);
                return true;
            }, ct);
        }
        catch (RecallDuplicateException)
        {
            return Response<RecallReturnLinkDto>.Fail(RecallErrorCodes.ReturnAlreadyLinked, "This return is already linked to a recall case.", 409);
        }

        return Response<RecallReturnLinkDto>.Success(RecallReturnRules.Dto(link), 201);
    }

    private static Response<RecallReturnLinkDto> NotEligible(string message, string reason)
        => Response<RecallReturnLinkDto>.Fail(RecallErrorCodes.ReturnNotEligible, message, 422, new Dictionary<string, object?> { ["reason"] = reason });
}

/// <summary>
/// §4.36 reviseScope — CT-R5 (R3, pack §8.3 step 5): an Active case with an approved scope opens revision r+1 from out-of-scope findings of the
/// LIVE trace (nodes not in the approved snapshot; Lot / Stock / Shipment of the root / forward side). The approved items are copied unchanged
/// (their marks are final — "mevcut engeller kalır"); only the delta items are new, included, and may be marked here. The delta waits for its own
/// signed approval (prepare + approveScope, same signature rule, the reviser cannot approve — CT-R1); the case stays Active, every block stays.
/// A Submitted snapshot of the live trace is frozen for the delta's approval re-trace.
/// </summary>
public sealed class ReviseRecallScopeHandler : RecallCaseCommandHandler, IRequestHandler<ReviseRecallScopeCommand, Response<RecallCaseDetailDto>>
{
    private readonly Trace.RecallTraceService _trace;

    public ReviseRecallScopeHandler(IRecallRepository repository, RecallReads reads, RecallSupport support, Trace.RecallTraceService trace)
        : base(repository, reads, support)
        => _trace = trace;

    public async Task<Response<RecallCaseDetailDto>> Handle(ReviseRecallScopeCommand request, CancellationToken ct)
    {
        var reason = RecallSupport.Trimmed(request.Reason);
        var add = request.AddNodeKeys.Distinct(StringComparer.Ordinal).ToList();
        var fingerprint = RecallSupport.Fingerprint(("op", "revise"), ("id", request.CaseId.ToString("D")), ("add", string.Join("|", add.Order(StringComparer.Ordinal))),
            ("marks", string.Join("|", request.Marks.Select(m => $"{m.NodeKey}={m.Included}:{RecallSupport.Trimmed(m.ExclusionReason)}"))), ("reason", reason));
        if (await ReplayCaseAsync(request.CaseId, request.IdempotencyKey, fingerprint, ct) is { } replay) return replay;
        if (add.Count == 0) return RecallSupport.Invalid<RecallCaseDetailDto>("addNodeKeys: at least one out-of-scope finding.");
        if (RecallCaseRules.Reason(reason) is { } reasonRefusal) return RecallSupport.Fail<RecallCaseDetailDto>(reasonRefusal);

        var c = await Repository.FindCaseAsync(request.CaseId, ct);
        if (c is null) return RecallSupport.CaseNotFound<RecallCaseDetailDto>();
        if (!RecallCaseRules.CanRevise(c))
        {
            return RecallSupport.InvalidTransition<RecallCaseDetailDto>(c.PendingRevision is not null
                ? "A revision is already waiting for approval."
                : $"A case in status {c.Status} cannot be revised (an approved, Active case only).");
        }

        if (c.ApprovedSnapshotId is not { } approvedId || await Repository.FindSnapshotAsync(approvedId, ct) is not { } approved)
        {
            return RecallSupport.InvalidTransition<RecallCaseDetailDto>("The case has no approved snapshot to revise against.");
        }

        var live = await _trace.RunAsync(Guid.Parse(c.RootLotId), c.Direction, null, ct);
        if (live is null) return RecallSupport.Fail<RecallCaseDetailDto>(RecallErrorCodes.UnknownLot, "Lot not found.", 404);
        var revision = c.ScopeRevision + 1;
        var now = Support.Now();
        var approvedKeys = approved.NodeKeys.ToHashSet(StringComparer.Ordinal);
        var current = await Repository.ScopeItemsAsync(c.Id, c.ScopeRevision, ct);
        var currentKeys = current.Select(i => i.NodeKey).ToHashSet(StringComparer.Ordinal);
        var findings = Trace.RecallTraceService.ScopeItems(live, c.Id, revision, Support.UserId, now)
            .Where(i => !approvedKeys.Contains(i.NodeKey) && !currentKeys.Contains(i.NodeKey)).ToDictionary(i => i.NodeKey, StringComparer.Ordinal);
        if (add.FirstOrDefault(k => !findings.ContainsKey(k)) is { } notFinding)
        {
            return RecallSupport.Invalid<RecallCaseDetailDto>($"'{notFinding}' is not an out-of-scope finding of the live trace (Lot / Stock / Shipment not in the approved scope).");
        }

        var delta = add.Select(k => findings[k]).ToList();
        foreach (var item in delta) item.IsNew = true;
        foreach (var mark in request.Marks)
        {
            var target = delta.FirstOrDefault(d => d.NodeKey == mark.NodeKey);
            if (target is null) return RecallSupport.Fail<RecallCaseDetailDto>(RecallErrorCodes.ScopeItemNotMarkable, "Only the delta's items are marked in a revision; the approved scope is final (CT-R5).", 400);
            if (!mark.Included && RecallCaseRules.Reason(RecallSupport.Trimmed(mark.ExclusionReason), "exclusionReason") is not null)
            {
                return RecallSupport.Fail<RecallCaseDetailDto>(RecallErrorCodes.ScopeExclusionReasonRequired, "An exclusion needs a reason of 10-1000 characters.", 400);
            }

            target.Included = mark.Included;
            target.ExclusionReason = mark.Included ? null : RecallSupport.Trimmed(mark.ExclusionReason);
            target.MarkedBy = Support.UserId;
            target.MarkedAtUtc = now;
        }

        // the approved items, copied as they are into r+1 (their marks are final), + the delta
        var copied = current.Select(i => new RecallScopeItem
        {
            Id = Guid.NewGuid(), CreatedAt = now, CaseId = c.Id, Revision = revision, NodeKey = i.NodeKey, ParentKey = i.ParentKey, Kind = i.Kind, IsRoot = i.IsRoot,
            LotId = i.LotId, LotNumber = i.LotNumber, ItemId = i.ItemId, SkuId = i.SkuId, SkuLevel = i.SkuLevel, WarehouseId = i.WarehouseId, LocationId = i.LocationId,
            StockStatus = i.StockStatus, SourceDocumentId = i.SourceDocumentId, CustomerRef = i.CustomerRef, Quantity = i.Quantity, BaseUomId = i.BaseUomId,
            Included = i.Included, ExclusionReason = i.ExclusionReason, Origin = i.Origin, IsNew = false, AddedBy = i.AddedBy, AddedAtUtc = i.AddedAtUtc,
            MarkedBy = i.MarkedBy, MarkedAtUtc = i.MarkedAtUtc
        }).ToList();
        var items = copied.Concat(delta).ToList();
        var seal = RecallScopeRules.Sha256(items, revision);
        if (c.Version != request.ExpectedVersion) return RecallSupport.Conflict<RecallCaseDetailDto>();

        c.ScopeRevision = revision;
        var snapshot = _trace.Snapshot(live, c, RecallSnapshotPurpose.Submitted, now);
        c.SubmittedSnapshotId = snapshot.Id;
        c.SubmittedScope = new RecallScopeSeal { Revision = revision, Sha256 = seal, CanonicalVersion = RecallScopeRules.CanonicalVersion, ItemCount = items.Count, ComputedAtUtc = now };
        c.ScopeSubmittedBy = Support.UserId;
        c.ScopeSubmittedAtUtc = now;
        c.PendingAction = null;
        c.PendingRevision = new RecallScopeRevision { Revision = revision, Reason = reason!, AddedNodeKeys = add, RevisedBy = Support.UserId, RevisedAtUtc = now };
        var refs = new Dictionary<string, string?>
        {
            ["revision"] = revision.ToString(CultureInfo.InvariantCulture), ["added"] = string.Join(",", add), ["scopeSha256"] = seal, ["traceSha256"] = live.Sha256
        };
        var saved = await SaveAsync(c, request.ExpectedVersion,
            Support.Event(c.Id, RecallCaseEventKind.ScopeRevised, now, reason, refs, idempotencyKey: request.IdempotencyKey, fingerprint: fingerprint),
            Support.Audit(RecallContract.AuditScopeRevised, RecallContract.SubjectCase, c.Id, c.Number, now, request.IdempotencyKey, Detail(c, [.. refs.Select(r => (r.Key, r.Value))])),
            ct, async (session, tx) =>
            {
                await session.InsertScopeItemsAsync(items, tx);
                await session.InsertSnapshotAsync(snapshot, tx);
                await session.InsertAuditAsync(RecallTraceAudit.Of(Support, live, "reviseScope", c.Id, c.Number, now, request.IdempotencyKey), tx);
            });
        return saved ? Response<RecallCaseDetailDto>.Success(await Reads.DetailAsync(c, ct), 201) : RecallSupport.Conflict<RecallCaseDetailDto>();
    }
}
