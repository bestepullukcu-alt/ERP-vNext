using System.Globalization;
using System.Text.RegularExpressions;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Common.Upstream;
using Diten.InventoryService.Application.Features.Inventory.Commands;
using Diten.InventoryService.Application.Features.Inventory.Ports;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Repositories;
using Diten.InventoryService.Domain.Rules;

namespace Diten.InventoryService.Application.Features.Inventory.Services;

/// <summary>
/// createReservation / releaseReservation — ONE path for the HTTP handlers and the in-process port (MOD-0173-FU03 §8.7: "HTTP ile aynı
/// ayrıştırıcı, parmak izi, idempotency, bekçi, lot / depo kuralı, denetim"). The caller names the actor; the scope is the resolved one.
/// <list type="number">
/// <item>Structure (<see cref="ReservationRequestParser"/>) → 400 INVALID_REQUEST.</item>
/// <item>Idempotency (pack ASSUMPTION-0173-12..16): a committed key answers from the durable record — same fingerprint → 200 with the
/// original result; different → 400 IDEMPOTENCY_KEY_REUSED.</item>
/// <item>Scope (FU03 §8.2): the warehouse is an Active Warehouse node of the caller's company (422 UNKNOWN_LOCATION; LOCATION unreadable →
/// 503 LOCATION_MASTER_UNAVAILABLE); the lot is registered (UNKNOWN_LOT), of this SKU (LOT_SKU_MISMATCH), passes the lot rule of a sales
/// issue (LOT_BLOCKED / LOT_EXPIRED / LOT_RETEST_DUE; a Draft lot is accepted — ASSUMPTION-0174-07) and is not under recall
/// (LOT_UNDER_RECALL). PRODUCT-MASTER is not called (ASSUMPTION-S2-03).</item>
/// <item>One atomic create guarded on every level of the scope → 201, or 422 INSUFFICIENT_AVAILABLE + <c>details.level</c>.</item>
/// </list>
/// Every refusal writes ONE <c>InventoryReservationRefused</c> row (AUD-001 §3, <c>Outcome Failed</c>) after the answer was decided — no
/// reservation exists, so <c>ReservationId</c> is <see cref="Guid.Empty"/> and <c>Status</c> null (FU03 §4.4). A replay writes nothing.
/// </summary>
public sealed partial class ReservationCommandService
{
    /// <summary>FU03 §8.7: the batch limit of <see cref="CreateBatchAsync"/>.</summary>
    public const int MaxBatchLines = 100;

    private readonly IInventoryReservationRepository _reservations;
    private readonly ITenantContext _tenantContext;
    private readonly ICorrelationContext _correlation;
    private readonly ILocationMasterClient _locations;
    private readonly IReservationLotRule _lotRule;
    private readonly IPostingActorName? _actorName;

    public ReservationCommandService(
        IInventoryReservationRepository reservations,
        ITenantContext tenantContext,
        ICorrelationContext correlation,
        ILocationMasterClient locations,
        IReservationLotRule lotRule,
        IPostingActorName? actorName = null)
    {
        _reservations = reservations;
        _tenantContext = tenantContext;
        _correlation = correlation;
        _locations = locations;
        _lotRule = lotRule;
        _actorName = actorName; // DI always passes it; a hand-built service stores no name
    }

    // ── create ────────────────────────────────────────────────────────────────────────────────────────

    public async Task<Response<ReservationResponseDto>> CreateAsync(CreateReservationCommand request, Guid actorUserId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var line = await ParseAsync(request, actorUserId, now, cancellationToken);
        if (line.Answer is null && await _reservations.FindByIdempotencyKeyAsync(line.Parsed!.IdempotencyKey, cancellationToken) is { } committed)
        {
            line = line with { Answer = await ReplayAsync(line, committed, cancellationToken) };
        }

        if (line.Answer is null)
        {
            line = await CheckScopeAsync(line, actorUserId, now, cancellationToken);
        }

        if (line.Answer is { } answer)
        {
            return answer;
        }

        var result = await _reservations.CreateAsync(line.Creation!, now, cancellationToken);
        switch (result.Outcome)
        {
            case ReservationCreateOutcome.Created:
                return Response<ReservationResponseDto>.Success(InventoryMapping.ToCreatedReservation(result.Reservation!), 201);

            case ReservationCreateOutcome.InsufficientAvailable:
                return await InsufficientAsync(line, result.Available ?? 0m, result.Requested ?? 0m, result.Level, cancellationToken);

            case ReservationCreateOutcome.IdempotencyKeyTaken:
            {
                var winner = await _reservations.FindByIdempotencyKeyAsync(line.Parsed!.IdempotencyKey, cancellationToken)
                    ?? throw new InvalidOperationException("Reservation idempotency key reported taken but no reservation carries it.");
                return await ReplayAsync(line, winner, cancellationToken);
            }

            default:
                throw new InvalidOperationException($"Unhandled reservation outcome {result.Outcome}.");
        }
    }

    /// <summary>
    /// FU03 §8.7 (Q-06): 1…100 lines, all or nothing in ONE transaction. Every line's own key: all keys already committed → the replays
    /// (200); some → 400 IDEMPOTENCY_KEY_REUSED (a mixed replay is not accepted), nothing written. One refused line → nothing written,
    /// the line carries its code / level / free quantity.
    /// </summary>
    public async Task<ReservationBatchResponse> CreateBatchAsync(
        IReadOnlyList<CreateReservationCommand> requests, Guid actorUserId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        if (requests.Count is 0 or > MaxBatchLines)
        {
            await WriteRefusalAsync(new RefusalContext(actorUserId, null), InventoryErrorCodes.InvalidRequest, null, null, cancellationToken);
            return new ReservationBatchResponse(400, InventoryErrorCodes.InvalidRequest, []);
        }

        var lines = new List<PreparedLine>(requests.Count);
        foreach (var request in requests)
        {
            lines.Add(await ParseAsync(request, actorUserId, now, cancellationToken));
        }

        var keys = lines.Select(l => l.Parsed?.IdempotencyKey).Where(k => k is not null).Cast<string>().ToList();
        if (keys.Count != keys.Distinct(StringComparer.Ordinal).Count())
        {
            await WriteRefusalAsync(new RefusalContext(actorUserId, null), InventoryErrorCodes.InvalidRequest, null, null, cancellationToken);
            return new ReservationBatchResponse(400, InventoryErrorCodes.InvalidRequest, lines.Select((l, i) => NotWritten(i, l)).ToList());
        }

        if (lines.Any(l => l.Parsed is null))
        {
            return await RefuseBatchAsync(lines, 400, InventoryErrorCodes.InvalidRequest, cancellationToken);
        }

        if (await ReplayBatchAsync(lines, keys, cancellationToken) is { } replayed)
        {
            return replayed;
        }

        for (var i = 0; i < lines.Count; i++)
        {
            lines[i] = await CheckScopeAsync(lines[i], actorUserId, now, cancellationToken);
        }

        if (lines.Any(l => l.Answer is not null))
        {
            var status = lines.Any(l => l.Answer?.StatusCode == 503) ? 503 : 422;
            return await RefuseBatchAsync(lines, status, lines.First(l => l.Answer is not null).Answer!.ErrorCode, cancellationToken);
        }

        var result = await _reservations.CreateBatchAsync(lines.Select(l => l.Creation!).ToList(), now, cancellationToken);
        switch (result.Outcome)
        {
            case ReservationCreateOutcome.Created:
                return new ReservationBatchResponse(201, null, result.Reservations
                    .Select((r, i) => new ReservationBatchLine(i, r.IdempotencyKey, true, InventoryMapping.ToCreatedReservation(r))).ToList());

            case ReservationCreateOutcome.InsufficientAvailable:
            {
                var index = result.RefusedIndex ?? 0;
                var refused = await InsufficientAsync(lines[index], result.Available ?? 0m, result.Requested ?? 0m, result.Level, cancellationToken);
                lines[index] = lines[index] with { Answer = refused };
                return new ReservationBatchResponse(422, InventoryErrorCodes.InsufficientAvailable, lines.Select(LineOf).ToList());
            }

            case ReservationCreateOutcome.IdempotencyKeyTaken:
                return await ReplayBatchAsync(lines, keys, cancellationToken)
                       ?? throw new InvalidOperationException("A batch key was reported taken but no reservation carries it.");

            default:
                throw new InvalidOperationException($"Unhandled reservation outcome {result.Outcome}.");
        }
    }

    /// <summary>Step 1: structure and fingerprint; a structural refusal is decided (and audited) here.</summary>
    private async Task<PreparedLine> ParseAsync(CreateReservationCommand request, Guid actorUserId, DateTimeOffset now, CancellationToken ct)
    {
        var (parsed, errors) = ReservationRequestParser.Parse(request, now);
        if (parsed is null)
        {
            var context = RefusalContext.Unparsed(actorUserId, request);
            await WriteRefusalAsync(context, errors[0].Code, null, null, ct);
            return new PreparedLine(null, string.Empty, null, context,
                Response<ReservationResponseDto>.Fail(errors[0].Code, errors.Select(e => e.Message).ToList(), 400));
        }

        return new PreparedLine(parsed, ReservationFingerprint.Compute(parsed), null, RefusalContext.Of(actorUserId, parsed), null);
    }

    /// <summary>Step 3 (after the replay check): the warehouse / lot rules, then the documents to write. A refusal is audited here.</summary>
    private async Task<PreparedLine> CheckScopeAsync(PreparedLine line, Guid actorUserId, DateTimeOffset now, CancellationToken ct)
    {
        var (lot, refusal) = await ScopeRefusalAsync(line.Parsed!, now, ct);
        if (refusal is not null)
        {
            await WriteRefusalAsync(line.Refusal, refusal.ErrorCode!, null, null, ct);
            return line with { Answer = refusal };
        }

        return line with { Creation = Build(line.Parsed!, line.Fingerprint, lot, actorUserId, now) };
    }

    private async Task<(Guid? LotId, Response<ReservationResponseDto>? Refusal)> ScopeRefusalAsync(ParsedReservation parsed, DateTimeOffset now, CancellationToken ct)
    {
        if (parsed.WarehouseId is { } warehouseId)
        {
            LocationReference? node;
            try
            {
                node = await _locations.GetLocationAsync(warehouseId, ct);
            }
            catch (LocationMasterUnavailableException)
            {
                return (null, Response<ReservationResponseDto>.Fail(InventoryErrorCodes.LocationMasterUnavailable,
                    "LOCATION master could not be read; the reservation was not created.", 503));
            }

            if (node is null || node.Type != nameof(LocationType.Warehouse) || node.Status != nameof(LocationStatus.Active)
                || !string.Equals(node.LegalEntityId, _tenantContext.LegalEntityId.ToString("D"), StringComparison.Ordinal))
            {
                return (null, Response<ReservationResponseDto>.Fail(InventoryErrorCodes.UnknownLocation,
                    "warehouseId is not an Active warehouse of this company.", 422,
                    new Dictionary<string, object?> { ["locationId"] = warehouseId }));
            }
        }

        if (parsed.LotId is not { } lotText)
        {
            return (null, null);
        }

        var check = await _lotRule.CheckAsync(lotText, parsed.SkuId, parsed.SkuLevel, now, ct);
        return check.Accepted
            ? (check.LotId, null)
            : (null, Response<ReservationResponseDto>.Fail(check.Code!, check.Message ?? check.Code!, 422, check.Details));
    }

    private ReservationCreation Build(ParsedReservation parsed, string fingerprint, Guid? lotId, Guid actorUserId, DateTimeOffset now)
    {
        var reservation = new InventoryReservation
        {
            Id = Guid.NewGuid(),
            CreatedAt = now,
            Version = 1,
            ItemId = parsed.ItemId,
            SkuId = parsed.SkuId,
            SkuLevel = parsed.SkuLevel,
            WarehouseId = parsed.WarehouseId,
            LotId = lotId,
            Quantity = parsed.Quantity,
            RemainingQuantity = parsed.Quantity,
            Status = ReservationStatus.Active,
            ReservationType = parsed.ReservationType,
            SourceModule = parsed.SourceModule,
            SourceDocumentId = parsed.SourceDocumentId,
            SourceLineId = parsed.SourceLineId,
            Priority = parsed.Priority,
            ExpiresAt = parsed.ExpiresAt,
            IdempotencyKey = parsed.IdempotencyKey,
            RequestFingerprint = fingerprint,
            CorrelationId = _correlation.CorrelationId,
            CreatedBy = actorUserId
        };

        return new ReservationCreation(
            reservation,
            new InventoryReservationAuditRecord
            {
                Id = Guid.NewGuid(),
                CreatedAt = now,
                Action = "InventoryReservationCreated",
                ReservationId = reservation.Id,
                ItemId = reservation.ItemId,
                SkuId = reservation.SkuId,
                SkuLevel = reservation.SkuLevel,
                WarehouseId = reservation.WarehouseId,
                LotId = reservation.LotId,
                ChangedQuantity = reservation.Quantity,
                RemainingQuantity = reservation.Quantity,
                Status = ReservationStatus.Active,
                Outcome = ReservationAuditOutcomes.Succeeded,
                SourceModule = reservation.SourceModule,
                ActorUserId = reservation.CreatedBy,
                CorrelationId = reservation.CorrelationId,
                LeSource = _tenantContext.LegalEntitySource,
                OccurredAt = now
            },
            new InventoryReservationOutboxMessage
            {
                Id = Guid.NewGuid(),
                CreatedAt = now,
                AggregateId = reservation.Id,
                CorrelationId = reservation.CorrelationId,
                OccurredAt = now,
                Payload = new InventoryReservationChangedPayload
                {
                    ReservationId = reservation.Id,
                    Change = "Created",
                    ItemId = reservation.ItemId,
                    SkuId = reservation.SkuId,
                    SkuLevel = reservation.SkuLevel,
                    WarehouseId = reservation.WarehouseId,
                    LotId = reservation.LotId,
                    ChangedQuantity = reservation.Quantity,
                    RemainingQuantity = reservation.Quantity,
                    Status = ReservationStatus.Active,
                    SourceModule = reservation.SourceModule,
                    SourceDocumentId = reservation.SourceDocumentId,
                    SourceLineId = reservation.SourceLineId
                }
            });
    }

    private async Task<Response<ReservationResponseDto>> ReplayAsync(PreparedLine line, InventoryReservation committed, CancellationToken ct)
    {
        if (string.Equals(committed.RequestFingerprint, line.Fingerprint, StringComparison.Ordinal))
        {
            return Response<ReservationResponseDto>.Success(InventoryMapping.ToCreatedReservation(committed), 200);
        }

        await WriteRefusalAsync(line.Refusal, InventoryErrorCodes.IdempotencyKeyReused, null, null, ct);
        return Response<ReservationResponseDto>.Fail(InventoryErrorCodes.IdempotencyKeyReused,
            "This idempotency key was already used for a different reservation.", 400);
    }

    private async Task<Response<ReservationResponseDto>> InsufficientAsync(
        PreparedLine line, decimal available, decimal requested, string? level, CancellationToken ct)
    {
        await WriteRefusalAsync(line.Refusal, InventoryErrorCodes.InsufficientAvailable, level, (available, requested), ct);
        return Response<ReservationResponseDto>.Fail(InventoryErrorCodes.InsufficientAvailable,
            "Insufficient unreserved available stock for this reservation scope.", 422,
            new Dictionary<string, object?>
            {
                ["level"] = level,
                ["available"] = InventoryQuantity.ToWire(available),
                ["requested"] = InventoryQuantity.ToWire(requested)
            });
    }

    /// <summary>All keys committed → the replays (or 400 for a differing body); some → 400 (mixed); none → null (go on).</summary>
    private async Task<ReservationBatchResponse?> ReplayBatchAsync(List<PreparedLine> lines, IReadOnlyCollection<string> keys, CancellationToken ct)
    {
        var committed = (await _reservations.FindByIdempotencyKeysAsync(keys, ct)).ToDictionary(r => r.IdempotencyKey, StringComparer.Ordinal);
        if (committed.Count == 0)
        {
            return null;
        }

        var reused = false;
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (committed.Count < lines.Count)
            {
                if (committed.ContainsKey(line.Parsed!.IdempotencyKey))
                {
                    await WriteRefusalAsync(line.Refusal, InventoryErrorCodes.IdempotencyKeyReused, null, null, ct);
                    lines[i] = line with { Answer = Response<ReservationResponseDto>.Fail(InventoryErrorCodes.IdempotencyKeyReused,
                        "Part of this batch was already processed: a mixed replay is not accepted.", 400) };
                }

                reused = true;
                continue;
            }

            var answer = await ReplayAsync(line, committed[line.Parsed!.IdempotencyKey], ct);
            reused |= !answer.IsSuccessful;
            lines[i] = line with { Answer = answer };
        }

        return reused
            ? new ReservationBatchResponse(400, InventoryErrorCodes.IdempotencyKeyReused, lines.Select(LineOf).ToList())
            : new ReservationBatchResponse(200, null, lines.Select(LineOf).ToList());
    }

    private static Task<ReservationBatchResponse> RefuseBatchAsync(List<PreparedLine> lines, int status, string? code, CancellationToken ct)
        => Task.FromResult(new ReservationBatchResponse(status, code, lines.Select(LineOf).ToList()));

    private static ReservationBatchLine LineOf(PreparedLine line, int index)
    {
        if (line.Answer is not { } answer)
        {
            return NotWritten(index, line);
        }

        if (answer.IsSuccessful)
        {
            return new ReservationBatchLine(index, line.Parsed?.IdempotencyKey, true, answer.Data);
        }

        return new ReservationBatchLine(index, line.Parsed?.IdempotencyKey, false, null, answer.ErrorCode, string.Join(" ", answer.Errors),
            answer.ErrorDetails?.GetValueOrDefault("level") as string,
            answer.ErrorDetails?.GetValueOrDefault("available") as string,
            answer.ErrorDetails?.GetValueOrDefault("requested") as string);
    }

    private static ReservationBatchLine NotWritten(int index, PreparedLine line) => new(index, line.Parsed?.IdempotencyKey, false);

    // ── release ───────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// releaseReservation (pack ASSUMPTION-0173-17). Order of refusals: missing <c>If-Match</c> → 400; unknown or other-scope id → 404;
    /// stale ETag, or a reservation that is no longer ACTIVE (released, consumed, expired — due ones are expired first in the same
    /// transaction) → 409 RESERVATION_CONFLICT. An ETag not of the form <c>W/"n"</c> can never match, so it is a 409 too (after the 404).
    /// FU03: the remaining quantity is freed on every level of the reservation's scope.
    /// </summary>
    public async Task<Response<ReservationResponseDto>> ReleaseAsync(string? reservationIdText, string? ifMatch, Guid actorUserId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            return Response<ReservationResponseDto>.Fail(InventoryErrorCodes.InvalidRequest, "The If-Match header is required.", 400);
        }

        if (!WireParsing.TryParseUuid(reservationIdText, out var reservationId))
        {
            return NotFound();
        }

        var match = WeakETag().Match(ifMatch.Trim());
        var expectedVersion = match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var v)
            ? v
            : -1;

        var result = await _reservations.ReleaseAsync(
            reservationId, expectedVersion, _correlation.CorrelationId, actorUserId, DateTimeOffset.UtcNow, cancellationToken);

        return result.Outcome switch
        {
            ReservationReleaseOutcome.Released => Response<ReservationResponseDto>.Success(
                InventoryMapping.ToReleasedReservation(result.Reservation!, result.AvailableAfter ?? 0m), 200),
            ReservationReleaseOutcome.NotFound => NotFound(),
            ReservationReleaseOutcome.Conflict => Response<ReservationResponseDto>.Fail(InventoryErrorCodes.ReservationConflict,
                "The reservation changed or is no longer active; reload it and retry with its current ETag.", 409),
            _ => throw new InvalidOperationException($"Unhandled release outcome {result.Outcome}.")
        };
    }

    /// <summary>No declared code for an unknown reservation — the Error body carries none (ASSUMPTION-S2-07); getReservation answers the same.</summary>
    public static Response<T> NotFound<T>() => Response<T>.Fail("Reservation not found.", 404);

    private static Response<ReservationResponseDto> NotFound() => NotFound<ReservationResponseDto>();

    [GeneratedRegex("^W/\"(\\d{1,9})\"$", RegexOptions.CultureInvariant)]
    private static partial Regex WeakETag();

    // ── refusal audit (FU03 §4.4, §21) ────────────────────────────────────────────────────────────────

    private Task WriteRefusalAsync(RefusalContext context, string errorCode, string? level, (decimal Available, decimal Requested)? numbers, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        return _reservations.WriteRefusalAsync(new InventoryReservationAuditRecord
        {
            Id = Guid.NewGuid(),
            CreatedAt = now,
            Action = ReservationAuditActions.Refused,
            ReservationId = Guid.Empty,
            ItemId = context.ItemId ?? Guid.Empty,
            SkuId = context.SkuId ?? Guid.Empty,
            SkuLevel = context.SkuLevel ?? default,
            ChangedQuantity = 0m,
            RemainingQuantity = 0m,
            Status = null,
            Outcome = ReservationAuditOutcomes.Failed,
            ErrorCode = errorCode,
            RequestedQuantity = numbers?.Requested ?? context.Requested,
            UncoveredQuantity = numbers?.Available,
            Level = level,
            WarehouseId = context.WarehouseId,
            LotId = context.LotId,
            SourceModule = context.SourceModule,
            ActorUserId = context.ActorUserId,
            ActorDisplayName = NameOf(context.ActorUserId),
            CorrelationId = _correlation.CorrelationId,
            LeSource = _tenantContext.LegalEntitySource,
            OccurredAt = now
        }, ct);
    }

    /// <summary>§4.4: the actor's name only when the actor IS the resolved scope user (<c>InternalMovementPostingService.NameOf</c> rule).</summary>
    private string? NameOf(Guid actorUserId)
        => actorUserId != Guid.Empty && actorUserId == _tenantContext.UserId && !string.IsNullOrWhiteSpace(_actorName?.Current) ? _actorName!.Current : null;

    private sealed record PreparedLine(
        ParsedReservation? Parsed,
        string Fingerprint,
        ReservationCreation? Creation,
        RefusalContext Refusal,
        Response<ReservationResponseDto>? Answer);

    /// <summary>What a refusal row says about the request: the parsed values, or what could be read of an unparsable one.</summary>
    private sealed record RefusalContext(
        Guid ActorUserId,
        decimal? Requested,
        Guid? ItemId = null,
        Guid? SkuId = null,
        SkuLevel? SkuLevel = null,
        string? WarehouseId = null,
        Guid? LotId = null,
        string? SourceModule = null)
    {
        public static RefusalContext Of(Guid actor, ParsedReservation parsed)
            => new(actor, parsed.Quantity, parsed.ItemId, parsed.SkuId, parsed.SkuLevel, parsed.WarehouseId,
                RegisteredLotId(parsed.LotId), parsed.SourceModule);

        public static RefusalContext Unparsed(Guid actor, CreateReservationCommand request)
            => new(actor,
                InventoryQuantity.TryParsePositive(request.Quantity, out var quantity) ? quantity : null,
                WireParsing.TryParseUuid(request.ItemId, out var item) ? item : null,
                WireParsing.TryParseUuid(request.SkuId, out var sku) ? sku : null,
                WireNames.TryParseSkuLevel(request.SkuLevel, out var level) ? level : null,
                string.IsNullOrWhiteSpace(request.WarehouseId) ? null : Truncate(request.WarehouseId.Trim()),
                RegisteredLotId(request.LotId?.Trim()),
                string.IsNullOrWhiteSpace(request.SourceModule) ? null : Truncate(request.SourceModule.Trim()));

        private static string Truncate(string text) => text.Length <= 100 ? text : text[..100];

        /// <summary>The lot id as the audit row stores it: only the canonical lower-case UUID form a registered lot has.</summary>
        private static Guid? RegisteredLotId(string? text)
            => Guid.TryParseExact(text, "D", out var id) && string.Equals(text, id.ToString("D"), StringComparison.Ordinal) ? id : null;
    }
}
