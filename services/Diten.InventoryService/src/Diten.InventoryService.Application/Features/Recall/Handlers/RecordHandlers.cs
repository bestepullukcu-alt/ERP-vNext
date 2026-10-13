using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Recall.Commands;
using Diten.InventoryService.Application.Features.Recall.Services;
using Diten.InventoryService.Domain.Recall.Entities;
using Diten.InventoryService.Domain.Recall.Enums;
using Diten.InventoryService.Domain.Recall.Repositories;
using Diten.InventoryService.Domain.Recall.Rules;
using Diten.InventoryService.Domain.Repositories;
using MediatR;

namespace Diten.InventoryService.Application.Features.Recall.Handlers;

// The append-only records of a running recall (pack §4.7, §4.8, §4.10): kept while the case is Active or in its effectiveness check,
// INSERT ONLY (a correction is a new record that supersedes), no If-Match (they do not change the case head). Each writes its record,
// a timeline event and an audit row in ONE transaction; the same key + the same body answers the first record (200), another body 400.

/// <summary>§4.41 recordNotification — KR-F: who was told, when, by which channel. Record only — no e-mail is sent.</summary>
public sealed class RecordRecallNotificationHandler : IRequestHandler<RecordRecallNotificationCommand, Response<RecallNotificationDto>>
{
    private readonly IRecallRepository _repository;
    private readonly RecallSupport _support;

    public RecordRecallNotificationHandler(IRecallRepository repository, RecallSupport support)
    {
        _repository = repository;
        _support = support;
    }

    public async Task<Response<RecallNotificationDto>> Handle(RecordRecallNotificationCommand request, CancellationToken ct)
    {
        var customer = RecallSupport.Trimmed(request.CustomerRef);
        var note = RecallSupport.Trimmed(request.Note);
        var fingerprint = RecallSupport.Fingerprint(("case", request.CaseId.ToString("D")), ("customerRef", customer),
            ("notifiedOn", request.NotifiedOn.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)), ("channel", request.Channel.ToString()),
            ("note", note), ("supersedesId", request.SupersedesId?.ToString("D")));
        if (await _repository.FindNotificationByKeyAsync(_support.UserId, request.IdempotencyKey, ct) is { } earlier)
        {
            return earlier.RequestFingerprint == fingerprint && earlier.CaseId == request.CaseId
                ? Response<RecallNotificationDto>.Success(RecallMapping.Notification(earlier))
                : RecallSupport.KeyReused<RecallNotificationDto>();
        }

        if (customer is null) return RecallSupport.Invalid<RecallNotificationDto>("customerRef is required.");
        if (note is { Length: > RecallCaseRules.MaxNoteLength }) return RecallSupport.Invalid<RecallNotificationDto>($"note must be at most {RecallCaseRules.MaxNoteLength} characters.");

        var c = await _repository.FindCaseAsync(request.CaseId, ct);
        if (c is null) return RecallSupport.CaseNotFound<RecallNotificationDto>();
        if (!RecallCaseRules.AcceptsRecords(c.Status))
        {
            return RecallSupport.InvalidTransition<RecallNotificationDto>($"A case in status {c.Status} takes no notification (Active or EffectivenessCheck only).");
        }

        var now = _support.Now();
        if (request.NotifiedOn > DateOnly.FromDateTime(now.UtcDateTime)) return RecallSupport.Invalid<RecallNotificationDto>("notifiedOn cannot be in the future.");

        var items = await _repository.ScopeItemsAsync(c.Id, c.ScopeRevision, ct);
        if (!RecallReads.Shipments(items).Any(s => s.CustomerRef == customer)) return UnknownCustomer<RecallNotificationDto>(customer);

        if (request.SupersedesId is { } superseded
            && !(await _repository.NotificationsAsync(c.Id, ct)).Any(n => n.Id == superseded && n.CustomerRef == customer))
        {
            return RecallSupport.Invalid<RecallNotificationDto>("supersedesId must name a notification of this case and customer.");
        }

        var record = new RecallCustomerNotification
        {
            Id = Guid.NewGuid(),
            CreatedAt = now,
            CaseId = c.Id,
            CustomerRef = customer,
            NotifiedOn = new DateTimeOffset(request.NotifiedOn.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
            Channel = request.Channel,
            Note = note,
            SupersedesId = request.SupersedesId,
            RecordedBy = _support.UserId,
            RecordedAtUtc = now,
            IdempotencyKey = request.IdempotencyKey,
            RequestFingerprint = fingerprint
        };
        try
        {
            await _repository.InTransactionAsync<bool>(async (session, tx) =>
            {
                await session.InsertNotificationAsync(record, tx);
                await session.InsertEventAsync(_support.Event(c.Id, RecallCaseEventKind.CustomerNotified, now, note,
                    new Dictionary<string, string?> { ["customerRef"] = customer, ["channel"] = request.Channel.ToString(), ["notificationId"] = record.Id.ToString("D") }), tx);
                await session.InsertAuditAsync(_support.Audit(RecallContract.AuditCustomerNotified, RecallContract.SubjectCase, c.Id, c.Number, now, request.IdempotencyKey,
                    new Dictionary<string, string?> { ["customerRef"] = customer, ["channel"] = request.Channel.ToString(), ["notificationId"] = record.Id.ToString("D") }), tx);
                return true;
            }, ct);
        }
        catch (RecallDuplicateException)
        {
            return await _repository.FindNotificationByKeyAsync(_support.UserId, request.IdempotencyKey, ct) is { } twin && twin.RequestFingerprint == fingerprint
                ? Response<RecallNotificationDto>.Success(RecallMapping.Notification(twin))
                : RecallSupport.KeyReused<RecallNotificationDto>();
        }

        return Response<RecallNotificationDto>.Success(RecallMapping.Notification(record), 201);
    }

    internal static Response<T> UnknownCustomer<T>(string customer)
        => Response<T>.Fail(RecallErrorCodes.UnknownCustomer, "The customer is not a customer of the approved scope of this case.", 404,
            new Dictionary<string, object?> { ["customerRef"] = customer });
}

/// <summary>
/// §4.42 declareConsumption — the customer declares a quantity consumed / used. The unit is the customer's shipment unit of that SKU in
/// the approved scope; consumed (effective declarations) + returned (R1: 0) may not exceed shipped (422 <c>RECALL_QUANTITY_EXCEEDS_SHIPPED</c>).
/// </summary>
public sealed class DeclareRecallConsumptionHandler : IRequestHandler<DeclareRecallConsumptionCommand, Response<RecallConsumptionDto>>
{
    private readonly IRecallRepository _repository;
    private readonly RecallSupport _support;

    public DeclareRecallConsumptionHandler(IRecallRepository repository, RecallSupport support)
    {
        _repository = repository;
        _support = support;
    }

    public async Task<Response<RecallConsumptionDto>> Handle(DeclareRecallConsumptionCommand request, CancellationToken ct)
    {
        var customer = RecallSupport.Trimmed(request.CustomerRef);
        var evidence = RecallSupport.Trimmed(request.Evidence);
        var fingerprint = RecallSupport.Fingerprint(("case", request.CaseId.ToString("D")), ("customerRef", customer), ("skuId", request.SkuId.ToString("D")),
            ("skuLevel", request.SkuLevel.ToString()), ("quantity", RecallSupport.D(request.Quantity)),
            ("declaredOn", request.DeclaredOn.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)), ("evidence", evidence),
            ("supersedesId", request.SupersedesId?.ToString("D")));
        if (await _repository.FindConsumptionByKeyAsync(_support.UserId, request.IdempotencyKey, ct) is { } earlier)
        {
            return earlier.RequestFingerprint == fingerprint && earlier.CaseId == request.CaseId
                ? Response<RecallConsumptionDto>.Success(RecallMapping.Consumption(earlier))
                : RecallSupport.KeyReused<RecallConsumptionDto>();
        }

        if (customer is null) return RecallSupport.Invalid<RecallConsumptionDto>("customerRef is required.");
        if (request.Quantity <= 0m) return RecallSupport.Invalid<RecallConsumptionDto>("quantity must be greater than zero.");
        if (evidence is null || evidence.Length > RecallCaseRules.MaxNoteLength)
        {
            return RecallSupport.Invalid<RecallConsumptionDto>($"evidence is required (1-{RecallCaseRules.MaxNoteLength} characters).");
        }

        var c = await _repository.FindCaseAsync(request.CaseId, ct);
        if (c is null) return RecallSupport.CaseNotFound<RecallConsumptionDto>();
        if (!RecallCaseRules.AcceptsRecords(c.Status))
        {
            return RecallSupport.InvalidTransition<RecallConsumptionDto>($"A case in status {c.Status} takes no declaration (Active or EffectivenessCheck only).");
        }

        var now = _support.Now();
        if (request.DeclaredOn > DateOnly.FromDateTime(now.UtcDateTime)) return RecallSupport.Invalid<RecallConsumptionDto>("declaredOn cannot be in the future.");

        var shipments = RecallReads.Shipments(await _repository.ScopeItemsAsync(c.Id, c.ScopeRevision, ct)).Where(s => s.CustomerRef == customer).ToList();
        if (shipments.Count == 0) return RecordRecallNotificationHandler.UnknownCustomer<RecallConsumptionDto>(customer);

        var ofSku = shipments.Where(s => s.SkuId == request.SkuId && s.SkuLevel == request.SkuLevel).ToList();
        var units = ofSku.Select(s => s.BaseUomId!).Distinct(StringComparer.Ordinal).ToList();
        if (units.Count > 1) return RecallSupport.Invalid<RecallConsumptionDto>("The customer's shipments of this SKU carry more than one unit; R1 cannot place the declaration.");

        var all = await _repository.ConsumptionsAsync(c.Id, ct);
        if (request.SupersedesId is { } superseded && !RecallReads.Effective(all).Any(d => d.Id == superseded && d.CustomerRef == customer && d.SkuId == request.SkuId))
        {
            return RecallSupport.Invalid<RecallConsumptionDto>("supersedesId must name a current declaration of this case, customer and SKU.");
        }

        var shipped = ofSku.Sum(s => s.Quantity ?? 0m);
        var consumed = RecallReads.Effective(all).Where(d => d.CustomerRef == customer && d.SkuId == request.SkuId && d.Id != request.SupersedesId).Sum(d => d.Quantity);
        const decimal returned = 0m; // R1: return links are R3
        if (consumed + returned + request.Quantity > shipped)
        {
            return Response<RecallConsumptionDto>.Fail(RecallErrorCodes.QuantityExceedsShipped,
                "Consumed + returned would exceed the quantity shipped to this customer.", 422,
                new Dictionary<string, object?>
                {
                    ["shipped"] = shipped, ["returned"] = returned, ["consumed"] = consumed, ["requested"] = request.Quantity
                });
        }

        var record = new RecallConsumptionDeclaration
        {
            Id = Guid.NewGuid(),
            CreatedAt = now,
            CaseId = c.Id,
            CustomerRef = customer,
            SkuId = request.SkuId,
            SkuLevel = request.SkuLevel,
            BaseUomId = units[0],
            Quantity = request.Quantity,
            DeclaredOn = new DateTimeOffset(request.DeclaredOn.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
            Evidence = evidence,
            SupersedesId = request.SupersedesId,
            RecordedBy = _support.UserId,
            RecordedAtUtc = now,
            IdempotencyKey = request.IdempotencyKey,
            RequestFingerprint = fingerprint
        };
        try
        {
            await _repository.InTransactionAsync<bool>(async (session, tx) =>
            {
                await session.InsertConsumptionAsync(record, tx);
                var refs = new Dictionary<string, string?>
                {
                    ["customerRef"] = customer, ["skuId"] = request.SkuId.ToString("D"), ["quantity"] = RecallSupport.D(request.Quantity), ["declarationId"] = record.Id.ToString("D")
                };
                await session.InsertEventAsync(_support.Event(c.Id, RecallCaseEventKind.ConsumptionDeclared, now, evidence, refs), tx);
                await session.InsertAuditAsync(_support.Audit(RecallContract.AuditConsumptionDeclared, RecallContract.SubjectCase, c.Id, c.Number, now, request.IdempotencyKey, refs), tx);
                return true;
            }, ct);
        }
        catch (RecallDuplicateException)
        {
            return await _repository.FindConsumptionByKeyAsync(_support.UserId, request.IdempotencyKey, ct) is { } twin && twin.RequestFingerprint == fingerprint
                ? Response<RecallConsumptionDto>.Success(RecallMapping.Consumption(twin))
                : RecallSupport.KeyReused<RecallConsumptionDto>();
        }

        return Response<RecallConsumptionDto>.Success(RecallMapping.Consumption(record), 201);
    }
}

/// <summary>
/// §4.40 confirmSegregation — the warehouse confirms the physical separation of an in-scope stock item (record only, no movement). R1: by
/// the node key of an effectively included Stock item of the approved scope; the "quarantine request held" condition
/// (<c>RECALL_SEGREGATION_NOT_HELD</c>) comes with the requests in R3.
/// </summary>
public sealed class ConfirmRecallSegregationHandler : IRequestHandler<ConfirmRecallSegregationCommand, Response<RecallSegregationDto>>
{
    private readonly IRecallRepository _repository;
    private readonly ILocationRepository _locations;
    private readonly RecallSupport _support;

    public ConfirmRecallSegregationHandler(IRecallRepository repository, ILocationRepository locations, RecallSupport support)
    {
        _repository = repository;
        _locations = locations;
        _support = support;
    }

    public async Task<Response<RecallSegregationDto>> Handle(ConfirmRecallSegregationCommand request, CancellationToken ct)
    {
        var nodeKey = RecallSupport.Trimmed(request.NodeKey);
        var note = RecallSupport.Trimmed(request.Note);
        var fingerprint = RecallSupport.Fingerprint(("case", request.CaseId.ToString("D")), ("nodeKey", nodeKey), ("locationId", request.LocationId.ToString("D")),
            ("quantity", RecallSupport.D(request.Quantity)), ("note", note));
        if (await _repository.FindSegregationByKeyAsync(_support.UserId, request.IdempotencyKey, ct) is { } earlier)
        {
            return earlier.RequestFingerprint == fingerprint && earlier.CaseId == request.CaseId
                ? Response<RecallSegregationDto>.Success(RecallMapping.Segregation(earlier))
                : RecallSupport.KeyReused<RecallSegregationDto>();
        }

        if (nodeKey is null) return RecallSupport.Invalid<RecallSegregationDto>("nodeKey is required.");
        if (request.Quantity <= 0m) return RecallSupport.Invalid<RecallSegregationDto>("quantity must be greater than zero.");
        if (note is { Length: > RecallCaseRules.MaxNoteLength }) return RecallSupport.Invalid<RecallSegregationDto>($"note must be at most {RecallCaseRules.MaxNoteLength} characters.");

        var c = await _repository.FindCaseAsync(request.CaseId, ct);
        if (c is null) return RecallSupport.CaseNotFound<RecallSegregationDto>();
        if (!RecallCaseRules.AcceptsRecords(c.Status))
        {
            return RecallSupport.InvalidTransition<RecallSegregationDto>($"A case in status {c.Status} takes no segregation (Active or EffectivenessCheck only).");
        }

        var items = await _repository.ScopeItemsAsync(c.Id, c.ScopeRevision, ct);
        var byKey = items.ToDictionary(i => i.NodeKey, StringComparer.Ordinal);
        if (!byKey.TryGetValue(nodeKey, out var stock) || stock.Kind != RecallScopeItemKind.Stock || !RecallScopeRules.EffectivelyIncluded(stock, byKey))
        {
            return RecallSupport.Invalid<RecallSegregationDto>("nodeKey must name an included Stock item of the approved scope.");
        }

        if (request.Quantity > (stock.Quantity ?? 0m)) return RecallSupport.Invalid<RecallSegregationDto>("quantity cannot exceed the stock item's quantity.");
        if (await _locations.FindAsync(request.LocationId, ct) is null)
        {
            return RecallSupport.Fail<RecallSegregationDto>(InventoryErrorCodes.UnknownLocation, "locationId is not a location of the company.", 404);
        }

        var now = _support.Now();
        var record = new RecallSegregationConfirmation
        {
            Id = Guid.NewGuid(),
            CreatedAt = now,
            CaseId = c.Id,
            NodeKey = nodeKey,
            WarehouseId = stock.WarehouseId,
            LocationId = request.LocationId.ToString("D"),
            Quantity = request.Quantity,
            Note = note,
            ConfirmedBy = _support.UserId,
            ConfirmedAtUtc = now,
            IdempotencyKey = request.IdempotencyKey,
            RequestFingerprint = fingerprint
        };
        try
        {
            await _repository.InTransactionAsync<bool>(async (session, tx) =>
            {
                await session.InsertSegregationAsync(record, tx);
                var refs = new Dictionary<string, string?>
                {
                    ["nodeKey"] = nodeKey, ["locationId"] = record.LocationId, ["quantity"] = RecallSupport.D(request.Quantity), ["segregationId"] = record.Id.ToString("D")
                };
                await session.InsertEventAsync(_support.Event(c.Id, RecallCaseEventKind.SegregationConfirmed, now, note, refs), tx);
                await session.InsertAuditAsync(_support.Audit(RecallContract.AuditSegregationConfirmed, RecallContract.SubjectCase, c.Id, c.Number, now, request.IdempotencyKey, refs), tx);
                return true;
            }, ct);
        }
        catch (RecallDuplicateException)
        {
            return await _repository.FindSegregationByKeyAsync(_support.UserId, request.IdempotencyKey, ct) is { } twin && twin.RequestFingerprint == fingerprint
                ? Response<RecallSegregationDto>.Success(RecallMapping.Segregation(twin))
                : RecallSupport.KeyReused<RecallSegregationDto>();
        }

        return Response<RecallSegregationDto>.Success(RecallMapping.Segregation(record), 201);
    }
}
