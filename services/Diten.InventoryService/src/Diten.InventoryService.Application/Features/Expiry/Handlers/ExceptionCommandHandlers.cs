using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Expiry.Commands;
using Diten.InventoryService.Application.Features.Expiry.Ports;
using Diten.InventoryService.Application.Features.Expiry.Services;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Expiry.Entities;
using Diten.InventoryService.Domain.Expiry.Enums;
using Diten.InventoryService.Domain.Expiry.Repositories;
using Diten.InventoryService.Domain.Expiry.Rules;
using Diten.InventoryService.Domain.Repositories;
using Diten.InventoryService.Domain.Traceability.Repositories;
using MediatR;

namespace Diten.InventoryService.Application.Features.Expiry.Handlers;

/// <summary>
/// §4.12 openException — manual "İstisna aç" (origin Manual, ASSUMPTION-0176-10: any of the four types). The lot is the tenant's
/// (MOD-0174 CT-D3); the warehouse, when given, a Warehouse of the caller's company. One ACTIVE case per type + lot + warehouse
/// (409 <c>EXPIRY_EXCEPTION_ALREADY_OPEN</c>). SLA due = opening + the company policy's hours for the type. The opener is the
/// case's origin user (CT-F10: may not resolve it). Case + Opened event + audit row in ONE transaction.
/// </summary>
public sealed class OpenExpiryExceptionHandler : IRequestHandler<OpenExpiryExceptionCommand, Response<ExpiryExceptionDetailDto>>
{
    private readonly IExpiryRepository _repository;
    private readonly ILotRepository _lots;
    private readonly ILocationRepository _locations;
    private readonly ExpiryReads _reads;
    private readonly ExpirySupport _support;
    private readonly Ports.IExpiryReadModel _stock;

    public OpenExpiryExceptionHandler(
        IExpiryRepository repository, ILotRepository lots, ILocationRepository locations, ExpiryReads reads, ExpirySupport support,
        Ports.IExpiryReadModel stock)
    {
        _stock = stock;
        _repository = repository;
        _lots = lots;
        _locations = locations;
        _reads = reads;
        _support = support;
    }

    /// <summary>FU-0176-F1-UOM: the base unit of the lot's stock (in the warehouse when named); none readable → null ("birim yok").</summary>
    private async Task<string?> StockUnitAsync(Domain.Traceability.Entities.Lot lot, string? warehouse, CancellationToken ct)
        => (await _stock.LotStockAsync(warehouse, lot.SkuId, ct)).FirstOrDefault(r => r.Lot.Id == lot.Id)?.BaseUomId is { Length: > 0 } unit ? unit : null;

    public async Task<Response<ExpiryExceptionDetailDto>> Handle(OpenExpiryExceptionCommand request, CancellationToken ct)
    {
        var fields = new List<(string Name, string? Value)>
        {
            ("type", request.Type.ToString()), ("lotId", request.LotId.ToString("D")), ("warehouseId", request.WarehouseId?.ToString("D")),
            ("quantity", ExpirySupport.D(request.Quantity)), ("description", request.Description?.Trim())
        };
        if (request.BaseUomId is not null) fields.Add(("baseUomId", request.BaseUomId.Trim())); // FEFO 1.1.0: joins only when sent (old replays match)
        var fingerprint = ExpirySupport.Fingerprint([.. fields]);
        if (await _repository.FindCaseByCreateKeyAsync(_support.UserId, request.IdempotencyKey, ct) is { } earlier)
        {
            return earlier.CreateRequestFingerprint == fingerprint
                ? Response<ExpiryExceptionDetailDto>.Success(await _reads.DetailAsync(earlier, ct))
                : ExpirySupport.KeyReused<ExpiryExceptionDetailDto>();
        }

        if (ExpiryExceptionRules.Reason(request.Description) is { } refusal)
        {
            return ExpirySupport.Fail<ExpiryExceptionDetailDto>(refusal.Code, $"description must be {ExpiryExceptionRules.MinReasonLength}-{ExpiryExceptionRules.MaxReasonLength} characters.", refusal.Status);
        }

        if (request.Quantity is <= 0)
        {
            return ExpirySupport.Invalid<ExpiryExceptionDetailDto>("quantity must be greater than zero.");
        }

        if (request.BaseUomId is { } uom && uom.Trim().Length is < 1 or > 32)
        {
            return ExpirySupport.Invalid<ExpiryExceptionDetailDto>("baseUomId must be 1-32 characters.");
        }

        var lot = await _lots.FindAsync(request.LotId, ct);
        if (lot is null)
        {
            return ExpirySupport.Fail<ExpiryExceptionDetailDto>(ExpiryErrorCodes.UnknownLot, "Lot not found.", 404);
        }

        if (request.WarehouseId is { } warehouseId
            && (await _locations.FindAsync(warehouseId, ct) is not { Type: LocationType.Warehouse }))
        {
            return ExpirySupport.Fail<ExpiryExceptionDetailDto>(InventoryErrorCodes.UnknownLocation, "warehouseId is not a warehouse of the company.", 404);
        }

        var lotId = lot.Id.ToString("D");
        var warehouse = request.WarehouseId?.ToString("D");
        var dedupKey = ExpiryExceptionRules.DedupKey(request.Type, lotId, warehouse);
        if (await _repository.FindActiveCaseByDedupKeyAsync(dedupKey, ct) is { } active)
        {
            return AlreadyOpen(active.Id);
        }

        var policy = await _repository.FindPolicyAsync(ct) ?? ExpiryPolicyRules.Defaults();
        var now = _support.Now();
        var exceptionCase = new FefoExceptionCase
        {
            Id = Guid.NewGuid(),
            CreatedAt = now,
            Type = request.Type,
            Origin = ExpiryExceptionOrigin.Manual,
            Status = ExpiryExceptionStatus.Open,
            ItemId = lot.ItemId,
            SkuId = lot.SkuId,
            SkuLevel = lot.SkuLevel,
            LotId = lotId,
            LotNumber = lot.LotNumber,
            ExpiryDate = lot.ExpiryDate,
            RetestDate = lot.RetestDate,
            WarehouseId = warehouse,
            Quantity = request.Quantity,
            BaseUomId = request.BaseUomId?.Trim() ?? await StockUnitAsync(lot, warehouse, ct),
            Description = request.Description!.Trim(),
            OpenedAtUtc = now,
            OpenedBy = _support.UserId,
            OriginUserId = _support.UserId,
            DueAtUtc = ExpiryExceptionRules.DueAt(now, policy, request.Type),
            CreateIdempotencyKey = request.IdempotencyKey,
            CreateRequestFingerprint = fingerprint,
            DedupKey = dedupKey
        };

        try
        {
            await _repository.InTransactionAsync<bool>(async (session, c) =>
            {
                exceptionCase.Number = await session.NextNumberAsync(now.UtcDateTime.Year, c);
                await session.InsertCaseAsync(exceptionCase, c);
                await session.InsertEventAsync(_support.Event(exceptionCase.Id, ExpiryExceptionEventAction.Opened, now, exceptionCase.Description,
                    idempotencyKey: request.IdempotencyKey, fingerprint: fingerprint), c);
                await session.InsertAuditAsync(_support.Audit(ExpiryContract.AuditExceptionOpened, ExpiryContract.SubjectException, exceptionCase.Id,
                    exceptionCase.Number, now, request.IdempotencyKey, new Dictionary<string, string?>
                    {
                        ["type"] = ExpiryWire.Code(exceptionCase.Type),
                        ["origin"] = exceptionCase.Origin.ToString(),
                        ["lotId"] = lotId,
                        ["warehouseId"] = warehouse,
                        ["dueAtUtc"] = ExpirySupport.Wire(exceptionCase.DueAtUtc)
                    }), c);
                return true;
            }, ct);
        }
        catch (ExpiryDuplicateException)
        {
            // a concurrent twin: the same key (replay) or another active case of the same type + lot + warehouse
            if (await _repository.FindCaseByCreateKeyAsync(_support.UserId, request.IdempotencyKey, ct) is { } twin)
            {
                return twin.CreateRequestFingerprint == fingerprint
                    ? Response<ExpiryExceptionDetailDto>.Success(await _reads.DetailAsync(twin, ct))
                    : ExpirySupport.KeyReused<ExpiryExceptionDetailDto>();
            }

            return await _repository.FindActiveCaseByDedupKeyAsync(dedupKey, ct) is { } other
                ? AlreadyOpen(other.Id)
                : ExpirySupport.Conflict<ExpiryExceptionDetailDto>();
        }

        return Response<ExpiryExceptionDetailDto>.Success(await _reads.DetailAsync(exceptionCase, ct), 201);
    }

    private static Response<ExpiryExceptionDetailDto> AlreadyOpen(Guid existing)
        => Response<ExpiryExceptionDetailDto>.Fail(ExpiryErrorCodes.ExceptionAlreadyOpen,
            "An active exception of this type already exists for this lot and warehouse.", 409,
            new Dictionary<string, object?> { ["exceptionId"] = existing.ToString("D") });
}

/// <summary>
/// The shared skeleton of the If-Match commands on one case: the caller's earlier event with the same key answers the current case
/// (same body) or 400; then the case (404), then the command's own rule; the transition is written with its event + audit row in
/// ONE transaction, guarded by the version (stale → 409 <c>EXPIRY_CONFLICT</c>).
/// </summary>
public abstract class ExpiryCaseCommandHandler
{
    protected ExpiryCaseCommandHandler(IExpiryRepository repository, ExpiryReads reads, ExpirySupport support)
    {
        Repository = repository;
        Reads = reads;
        Support = support;
    }

    protected IExpiryRepository Repository { get; }

    protected ExpiryReads Reads { get; }

    protected ExpirySupport Support { get; }

    /// <summary>null = not a replay; otherwise the answer to the replay.</summary>
    protected async Task<Response<ExpiryExceptionDetailDto>?> ReplayAsync(Guid caseId, string key, string fingerprint, CancellationToken ct)
    {
        var earlier = await Repository.FindEventByKeyAsync(caseId, Support.UserId, key, ct);
        if (earlier is null) return null;
        return earlier.RequestFingerprint == fingerprint ? await Reads.CurrentAsync(caseId, ct) : ExpirySupport.KeyReused<ExpiryExceptionDetailDto>();
    }

    protected async Task<Response<ExpiryExceptionDetailDto>> WriteAsync(
        FefoExceptionCase exceptionCase, int expectedVersion, FefoExceptionEvent caseEvent, ExpiryAuditRecord audit, CancellationToken ct,
        Func<Response<ExpiryExceptionDetailDto>>? onDuplicate = null)
    {
        exceptionCase.Version = expectedVersion + 1;
        exceptionCase.UpdatedAt = caseEvent.AtUtc;
        bool saved;
        try
        {
            saved = await Repository.InTransactionAsync(async (session, c) =>
            {
                if (!await session.ReplaceCaseAsync(exceptionCase, expectedVersion, c)) return false;
                await session.InsertEventAsync(caseEvent, c);
                await session.InsertAuditAsync(audit, c);
                return true;
            }, ct);
        }
        catch (ExpiryDuplicateException)
        {
            return onDuplicate?.Invoke() ?? ExpirySupport.Conflict<ExpiryExceptionDetailDto>();
        }

        return saved ? Response<ExpiryExceptionDetailDto>.Success(await Reads.DetailAsync(exceptionCase, ct)) : ExpirySupport.Conflict<ExpiryExceptionDetailDto>();
    }

    protected Dictionary<string, string?> Detail(FefoExceptionCase c, params (string Key, string? Value)[] extra)
    {
        var detail = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["type"] = ExpiryWire.Code(c.Type),
            ["status"] = c.Status.ToString()
        };
        foreach (var (key, value) in extra) detail[key] = value;
        return detail;
    }
}

/// <summary>§4.13 takeException — "İncelemeye al": Open → InReview, the caller becomes the assignee. Taken again by the assignee = current.</summary>
public sealed class TakeExpiryExceptionHandler : ExpiryCaseCommandHandler, IRequestHandler<TakeExpiryExceptionCommand, Response<ExpiryExceptionDetailDto>>
{
    public TakeExpiryExceptionHandler(IExpiryRepository repository, ExpiryReads reads, ExpirySupport support) : base(repository, reads, support)
    {
    }

    public async Task<Response<ExpiryExceptionDetailDto>> Handle(TakeExpiryExceptionCommand request, CancellationToken ct)
    {
        var fingerprint = ExpirySupport.Fingerprint(("op", "take"), ("id", request.ExceptionId.ToString("D")));
        if (await ReplayAsync(request.ExceptionId, request.IdempotencyKey, fingerprint, ct) is { } replay) return replay;

        var c = await Repository.FindCaseAsync(request.ExceptionId, ct);
        if (c is null) return ExpirySupport.CaseNotFound<ExpiryExceptionDetailDto>();
        if (c.Status == ExpiryExceptionStatus.InReview && c.AssigneeUserId == Support.UserId)
        {
            return Response<ExpiryExceptionDetailDto>.Success(await Reads.DetailAsync(c, ct));
        }

        if (!ExpiryExceptionRules.CanTake(c.Status))
        {
            return ExpirySupport.InvalidTransition<ExpiryExceptionDetailDto>($"An exception in status {c.Status} cannot be taken into review.");
        }

        if (c.Version != request.ExpectedVersion) return ExpirySupport.Conflict<ExpiryExceptionDetailDto>();

        var now = Support.Now();
        c.Status = ExpiryExceptionStatus.InReview;
        c.AssigneeUserId = Support.UserId;
        c.TakenAtUtc = now;
        return await WriteAsync(c, request.ExpectedVersion,
            Support.Event(c.Id, ExpiryExceptionEventAction.Taken, now, idempotencyKey: request.IdempotencyKey, fingerprint: fingerprint),
            Support.Audit(ExpiryContract.AuditExceptionTaken, ExpiryContract.SubjectException, c.Id, c.Number, now, request.IdempotencyKey, Detail(c)), ct);
    }
}

/// <summary>§4.14 commentException — append-only (no If-Match, no version change); not on a Closed / Cancelled case.</summary>
public sealed class CommentExpiryExceptionHandler : IRequestHandler<CommentExpiryExceptionCommand, Response<ExpiryExceptionEventDto>>
{
    private readonly IExpiryRepository _repository;
    private readonly ExpirySupport _support;

    public CommentExpiryExceptionHandler(IExpiryRepository repository, ExpirySupport support)
    {
        _repository = repository;
        _support = support;
    }

    public async Task<Response<ExpiryExceptionEventDto>> Handle(CommentExpiryExceptionCommand request, CancellationToken ct)
    {
        var text = request.Text?.Trim();
        var fingerprint = ExpirySupport.Fingerprint(("op", "comment"), ("id", request.ExceptionId.ToString("D")), ("text", text));
        if (await _repository.FindEventByKeyAsync(request.ExceptionId, _support.UserId, request.IdempotencyKey, ct) is { } earlier)
        {
            return earlier.RequestFingerprint == fingerprint
                ? Response<ExpiryExceptionEventDto>.Success(await PositionedAsync(earlier, ct))
                : ExpirySupport.KeyReused<ExpiryExceptionEventDto>();
        }

        if (text is not { Length: >= 1 and <= ExpiryExceptionRules.MaxCommentLength })
        {
            return ExpirySupport.Invalid<ExpiryExceptionEventDto>($"text must be 1-{ExpiryExceptionRules.MaxCommentLength} characters.");
        }

        var c = await _repository.FindCaseAsync(request.ExceptionId, ct);
        if (c is null) return ExpirySupport.CaseNotFound<ExpiryExceptionEventDto>();
        if (!ExpiryExceptionRules.CanComment(c.Status))
        {
            return ExpirySupport.InvalidTransition<ExpiryExceptionEventDto>($"An exception in status {c.Status} takes no comments.");
        }

        var now = _support.Now();
        var comment = _support.Event(c.Id, ExpiryExceptionEventAction.Commented, now, text, idempotencyKey: request.IdempotencyKey, fingerprint: fingerprint);
        try
        {
            await _repository.InTransactionAsync<bool>(async (session, tx) =>
            {
                await session.InsertEventAsync(comment, tx);
                await session.InsertAuditAsync(_support.Audit(ExpiryContract.AuditExceptionCommented, ExpiryContract.SubjectException, c.Id, c.Number, now,
                    request.IdempotencyKey, new Dictionary<string, string?> { ["length"] = text.Length.ToString() }), tx);
                return true;
            }, ct);
        }
        catch (ExpiryDuplicateException)
        {
            return await _repository.FindEventByKeyAsync(request.ExceptionId, _support.UserId, request.IdempotencyKey, ct) is { } twin && twin.RequestFingerprint == fingerprint
                ? Response<ExpiryExceptionEventDto>.Success(await PositionedAsync(twin, ct))
                : ExpirySupport.KeyReused<ExpiryExceptionEventDto>();
        }

        return Response<ExpiryExceptionEventDto>.Success(await PositionedAsync(comment, ct), 201);
    }

    private async Task<ExpiryExceptionEventDto> PositionedAsync(FefoExceptionEvent e, CancellationToken ct)
    {
        var events = await _repository.EventsAsync(e.CaseId, ct);
        var position = events.OrderBy(x => x.Sequence).Select((x, i) => (x.Id, Seq: i + 1)).First(x => x.Id == e.Id).Seq;
        return ExpiryMapping.Event(e, position);
    }
}

/// <summary>
/// §4.15 prepareResolution — the decision the caller is about to sign: resolution + reason, held 15 minutes on the case (If-Match,
/// version + 1). Refused like the resolution itself (status, SoD) so the user is not sent to sign what cannot be applied. Answers
/// what the MOD-0022 dialog needs: required (effective policy), action code, meaning, visual mode, subject.
/// </summary>
public sealed class PrepareExpiryResolutionHandler : IRequestHandler<PrepareExpiryResolutionCommand, Response<ExpiryResolutionPreparedDto>>
{
    private readonly IExpiryRepository _repository;
    private readonly IExpiryResolutionSignature _signature;
    private readonly ExpirySupport _support;

    public PrepareExpiryResolutionHandler(IExpiryRepository repository, IExpiryResolutionSignature signature, ExpirySupport support)
    {
        _repository = repository;
        _signature = signature;
        _support = support;
    }

    public async Task<Response<ExpiryResolutionPreparedDto>> Handle(PrepareExpiryResolutionCommand request, CancellationToken ct)
    {
        var reason = request.Reason?.Trim();
        var fingerprint = ExpirySupport.Fingerprint(("op", "prepare"), ("id", request.ExceptionId.ToString("D")),
            ("resolution", request.Resolution.ToString()), ("reason", reason));
        var c = await _repository.FindCaseAsync(request.ExceptionId, ct);
        if (c is null) return ExpirySupport.CaseNotFound<ExpiryResolutionPreparedDto>();

        if (c.PendingResolution is { } pending && pending.SetBy == _support.UserId && pending.IdempotencyKey == request.IdempotencyKey)
        {
            return pending.RequestFingerprint == fingerprint
                ? await PreparedAsync(c, pending, ct)
                : ExpirySupport.KeyReused<ExpiryResolutionPreparedDto>();
        }

        if (ExpiryExceptionRules.Reason(reason) is { } refusal) return ExpirySupport.Fail<ExpiryResolutionPreparedDto>(refusal);
        if (!ExpiryExceptionRules.CanResolve(c.Status))
        {
            return ExpirySupport.InvalidTransition<ExpiryResolutionPreparedDto>($"An exception in status {c.Status} cannot be resolved.");
        }

        if (ExpiryExceptionRules.SeparationOfDuties(c, _support.UserId) is { } sod) return ExpirySupport.Fail<ExpiryResolutionPreparedDto>(sod);
        if (c.Version != request.ExpectedVersion) return ExpirySupport.Conflict<ExpiryResolutionPreparedDto>();

        var now = _support.Now();
        var prepared = new PendingResolution
        {
            Resolution = request.Resolution,
            Reason = reason!,
            SetBy = _support.UserId,
            SetAtUtc = now,
            ExpiresAtUtc = now + ExpiryExceptionRules.PendingResolutionLifetime,
            IdempotencyKey = request.IdempotencyKey,
            RequestFingerprint = fingerprint
        };
        c.PendingResolution = prepared;
        c.Version = request.ExpectedVersion + 1;
        c.UpdatedAt = now;
        var saved = await _repository.InTransactionAsync(async (session, tx) =>
        {
            if (!await session.ReplaceCaseAsync(c, request.ExpectedVersion, tx)) return false;
            await session.InsertAuditAsync(_support.Audit(ExpiryContract.AuditResolutionPrepared, ExpiryContract.SubjectException, c.Id, c.Number, now,
                request.IdempotencyKey, new Dictionary<string, string?>
                {
                    ["resolution"] = request.Resolution.ToString(),
                    ["expiresAtUtc"] = ExpirySupport.Wire(prepared.ExpiresAtUtc)
                }), tx);
            return true;
        }, ct);
        return saved ? await PreparedAsync(c, prepared, ct) : ExpirySupport.Conflict<ExpiryResolutionPreparedDto>();
    }

    private async Task<Response<ExpiryResolutionPreparedDto>> PreparedAsync(FefoExceptionCase c, PendingResolution pending, CancellationToken ct)
    {
        var action = ExpiryExceptionRules.ActionCode(pending.Resolution);
        var requirement = await _signature.RequirementAsync(action, c.Id, ct);
        return Response<ExpiryResolutionPreparedDto>.Success(new ExpiryResolutionPreparedDto(
            c.Id.ToString("D"), pending.Resolution.ToString(), requirement.Required, action, requirement.MeaningCode, requirement.VisualMode,
            new ExpirySubjectRefDto(ExpiryContract.ESignModule, ExpiryContract.ExceptionSubjectType, c.Id.ToString("D")),
            ExpirySupport.Wire(pending.ExpiresAtUtc), c.Version));
    }
}

/// <summary>
/// §4.16 resolveException — accept / fix / cancel (pack §8.4). Order: reason → case → status (409 INVALID_TRANSITION) → SoD (CT-F10,
/// 409 <c>EXPIRY_SOD_VIOLATION</c>) → the caller's own unexpired prepared decision, equal in resolution and reason (409
/// <c>EXPIRY_PENDING_RESOLUTION_MISMATCH</c>) → signature by policy (KF-E: required and missing → 400 <c>EXPIRY_SIGNATURE_REQUIRED</c>;
/// sent and not verified → 409 <c>EXPIRY_SIGNATURE_MISMATCH</c>) → If-Match. Only the signature id is kept. A signature id closes
/// one case (unique index → 409 MISMATCH).
/// </summary>
public sealed class ResolveExpiryExceptionHandler : ExpiryCaseCommandHandler, IRequestHandler<ResolveExpiryExceptionCommand, Response<ExpiryExceptionDetailDto>>
{
    private readonly IExpiryResolutionSignature _signature;

    public ResolveExpiryExceptionHandler(IExpiryRepository repository, ExpiryReads reads, ExpirySupport support, IExpiryResolutionSignature signature)
        : base(repository, reads, support) => _signature = signature;

    public async Task<Response<ExpiryExceptionDetailDto>> Handle(ResolveExpiryExceptionCommand request, CancellationToken ct)
    {
        var reason = request.Reason?.Trim();
        var signatureId = string.IsNullOrWhiteSpace(request.SignatureId) ? null : request.SignatureId.Trim();
        var fingerprint = ExpirySupport.Fingerprint(("op", "resolve"), ("id", request.ExceptionId.ToString("D")),
            ("resolution", request.Resolution.ToString()), ("reason", reason), ("signatureId", signatureId));
        if (await ReplayAsync(request.ExceptionId, request.IdempotencyKey, fingerprint, ct) is { } replay) return replay;

        if (ExpiryExceptionRules.Reason(reason) is { } refusal) return ExpirySupport.Fail<ExpiryExceptionDetailDto>(refusal);
        var c = await Repository.FindCaseAsync(request.ExceptionId, ct);
        if (c is null) return ExpirySupport.CaseNotFound<ExpiryExceptionDetailDto>();
        if (!ExpiryExceptionRules.CanResolve(c.Status))
        {
            return ExpirySupport.InvalidTransition<ExpiryExceptionDetailDto>($"An exception in status {c.Status} cannot be resolved.");
        }

        if (ExpiryExceptionRules.SeparationOfDuties(c, Support.UserId) is { } sod) return ExpirySupport.Fail<ExpiryExceptionDetailDto>(sod);

        var now = Support.Now();
        if (c.PendingResolution is not { } pending || pending.SetBy != Support.UserId || pending.ExpiresAtUtc <= now
            || pending.Resolution != request.Resolution || !string.Equals(pending.Reason, reason, StringComparison.Ordinal))
        {
            return ExpirySupport.Fail<ExpiryExceptionDetailDto>(ExpiryErrorCodes.PendingResolutionMismatch,
                "Prepare this resolution first (prepareResolution) and resolve with the same resolution and reason within 15 minutes.", 409);
        }

        var action = ExpiryExceptionRules.ActionCode(request.Resolution);
        var requirement = await _signature.RequirementAsync(action, c.Id, ct);
        if (requirement.Required && signatureId is null)
        {
            return ExpirySupport.Fail<ExpiryExceptionDetailDto>(ExpiryErrorCodes.SignatureRequired, "This resolution requires an e-signature.", 400);
        }

        if (signatureId is not null && !await _signature.VerifyAsync(new ExpirySignatureVerification(
                signatureId, action, requirement.MeaningCode, Support.UserId, c.Id, request.Resolution, reason!, pending.SetAtUtc), ct))
        {
            return SignatureMismatch();
        }

        if (c.Version != request.ExpectedVersion) return ExpirySupport.Conflict<ExpiryExceptionDetailDto>();

        c.Status = ExpiryExceptionRules.StatusAfter(request.Resolution);
        c.Resolution = request.Resolution;
        c.ResolutionReason = reason;
        c.ResolutionSignatureId = signatureId;
        c.ResolvedAtUtc = now;
        c.ResolvedBy = Support.UserId;
        c.PendingResolution = null;
        if (ExpiryExceptionRules.IsTerminal(c.Status))
        {
            c.DedupKey = null;
        }

        var cancelled = c.Status == ExpiryExceptionStatus.Cancelled;
        return await WriteAsync(c, request.ExpectedVersion,
            Support.Event(c.Id, cancelled ? ExpiryExceptionEventAction.Cancelled : ExpiryExceptionEventAction.Resolved, now, reason, request.Resolution,
                signatureId, request.IdempotencyKey, fingerprint),
            Support.Audit(cancelled ? ExpiryContract.AuditExceptionCancelled : ExpiryContract.AuditExceptionResolved, ExpiryContract.SubjectException,
                c.Id, c.Number, now, request.IdempotencyKey, Detail(c, ("resolution", request.Resolution.ToString()), ("reason", reason),
                    ("signatureId", signatureId), ("signatureRequired", requirement.Required ? "true" : "false"))),
            ct, SignatureMismatch);
    }

    private static Response<ExpiryExceptionDetailDto> SignatureMismatch()
        => ExpirySupport.Fail<ExpiryExceptionDetailDto>(ExpiryErrorCodes.SignatureMismatch,
            "The signature does not belong to this prepared resolution (signer, action, meaning, subject or already used).", 409);
}

/// <summary>§4.17 closeException — "İstisnayı kapat": Accepted / Fixed → Closed. No signature (mockup), no SoD.</summary>
public sealed class CloseExpiryExceptionHandler : ExpiryCaseCommandHandler, IRequestHandler<CloseExpiryExceptionCommand, Response<ExpiryExceptionDetailDto>>
{
    public CloseExpiryExceptionHandler(IExpiryRepository repository, ExpiryReads reads, ExpirySupport support) : base(repository, reads, support)
    {
    }

    public async Task<Response<ExpiryExceptionDetailDto>> Handle(CloseExpiryExceptionCommand request, CancellationToken ct)
    {
        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        var fingerprint = ExpirySupport.Fingerprint(("op", "close"), ("id", request.ExceptionId.ToString("D")), ("note", note));
        if (await ReplayAsync(request.ExceptionId, request.IdempotencyKey, fingerprint, ct) is { } replay) return replay;
        if (note is { Length: > ExpiryContract.MaxNoteLength })
        {
            return ExpirySupport.Invalid<ExpiryExceptionDetailDto>($"note must be at most {ExpiryContract.MaxNoteLength} characters.");
        }

        var c = await Repository.FindCaseAsync(request.ExceptionId, ct);
        if (c is null) return ExpirySupport.CaseNotFound<ExpiryExceptionDetailDto>();
        if (!ExpiryExceptionRules.CanClose(c.Status))
        {
            return ExpirySupport.InvalidTransition<ExpiryExceptionDetailDto>($"An exception in status {c.Status} cannot be closed (only Accepted or Fixed).");
        }

        if (c.Version != request.ExpectedVersion) return ExpirySupport.Conflict<ExpiryExceptionDetailDto>();

        var now = Support.Now();
        c.Status = ExpiryExceptionStatus.Closed;
        c.ClosedAtUtc = now;
        c.ClosedBy = Support.UserId;
        c.DedupKey = null;
        return await WriteAsync(c, request.ExpectedVersion,
            Support.Event(c.Id, ExpiryExceptionEventAction.Closed, now, note, idempotencyKey: request.IdempotencyKey, fingerprint: fingerprint),
            Support.Audit(ExpiryContract.AuditExceptionClosed, ExpiryContract.SubjectException, c.Id, c.Number, now, request.IdempotencyKey,
                Detail(c, ("resolution", c.Resolution?.ToString()))), ct);
    }
}
