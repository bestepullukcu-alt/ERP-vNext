using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Contracts.DocumentRepository;
using Diten.Platform.Application.Features.ESignature.Services;
using Diten.Platform.Domain.Entities.ESignature;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.ESignature.Handlers;

/// <summary>
/// MOD-0022 E3 — §4.17 save a signature sample ("İmzam", KS-7 / KS-9 / KS-10). The password is proven for THIS save: a
/// ticket with intent <c>sample</c> and kind <c>password</c> — a KS-3 window ticket never saves a sample (mockup "bu
/// seçenekte şifre her zaman istenir"). The tenant's <c>SampleApprovalRequired</c> (CT-D15) decides Pending vs Active.
/// </summary>
public sealed class CreateSampleCommandHandler : IRequestHandler<CreateSampleCommand, Response<SampleDto>>
{
    private readonly IESignActorAccessor _actors;
    private readonly IESignatureSampleRepository _samples;
    private readonly IESignatureSampleEventRepository _events;
    private readonly IESignatureSampleBook _book;
    private readonly IESignatureCatalog _catalog;
    private readonly IESignTicketConsumer _tickets;
    private readonly IContentStorageGateway _storage;
    private readonly IESignNumberGenerator _numbers;
    private readonly IPlatformTransactionExecutor _transactions;
    private readonly ITransactionalAuditOutboxWriter _audit;
    private readonly ILogger<CreateSampleCommandHandler> _logger;

    public CreateSampleCommandHandler(IESignActorAccessor actors, IESignatureSampleRepository samples, IESignatureSampleEventRepository events,
        IESignatureSampleBook book, IESignatureCatalog catalog, IESignTicketConsumer tickets, IContentStorageGateway storage,
        IESignNumberGenerator numbers, IPlatformTransactionExecutor transactions, ITransactionalAuditOutboxWriter audit,
        ILogger<CreateSampleCommandHandler> logger)
    {
        _actors = actors;
        _samples = samples;
        _events = events;
        _book = book;
        _catalog = catalog;
        _tickets = tickets;
        _storage = storage;
        _numbers = numbers;
        _transactions = transactions;
        _audit = audit;
        _logger = logger;
    }

    public async Task<Response<SampleDto>> Handle(CreateSampleCommand request, CancellationToken ct)
    {
        var actor = _actors.Current();
        if (actor is null)
        {
            return ESignResponses.Actor<SampleDto>();
        }

        if (!ESignResponses.IdempotencyKeyOk(request.IdempotencyKey))
        {
            return ESignResponses.Validation<SampleDto>("idempotencyKey is required (8–100 characters).");
        }

        var replay = await _samples.GetByIdempotencyKeyAsync(actor.TenantId, actor.UserIdText, request.IdempotencyKey!, ct);
        if (replay is not null)
        {
            return Response<SampleDto>.Success((await _book.ToDtosAsync(actor.TenantId, [replay], ct))[0], 200);
        }

        if (string.IsNullOrWhiteSpace(request.Ticket))
        {
            return ESignResponses.Fail<SampleDto>(ESignCodes.TicketRequired, 400, "A re-authentication ticket is required.");
        }

        if (!ESignatureWire.TryParse<ESignatureSampleKind>(request.Kind, out var kind))
        {
            return ESignResponses.Validation<SampleDto>("kind must be drawn or uploaded.");
        }

        // Image first (no ticket is spent on a bad image).
        var (png, failure) = ESignatureImageInput.ReadPng(request.PngBase64);
        switch (failure)
        {
            case ESignatureImageInput.Failure.Empty:
                return ESignResponses.Fail<SampleDto>(ESignCodes.DrawingEmpty, 400, "A sample needs a PNG.");
            case ESignatureImageInput.Failure.TooLarge:
                return ESignResponses.Fail<SampleDto>(ESignCodes.ImageTooLarge, 413, "The sample image is larger than 1 MB.");
            case ESignatureImageInput.Failure.NotPng:
                return ESignResponses.Fail<SampleDto>(ESignCodes.ImageNotPng, 415, "The sample image is not a PNG.");
        }

        var strokes = kind == ESignatureSampleKind.Drawn ? request.StrokesJson : null;
        if (strokes is not null && !ESignatureImageInput.StrokesValid(strokes))
        {
            return ESignResponses.Validation<SampleDto>("strokes must be at most 256 KB of points between 0 and 1.");
        }

        // The password, for this save: intent sample, kind password — a window ticket is refused even if AuthService issued one.
        var consumed = await _tickets.ConsumeAsync(request.Ticket, actor.TenantId, actor.UserId, "sample", ct);
        if (consumed.Outcome == TicketConsumeOutcome.Unavailable)
        {
            return ESignResponses.Fail<SampleDto>(ESignCodes.ReauthUnavailable, 503, "Re-authentication could not be checked.");
        }

        if (consumed.Outcome == TicketConsumeOutcome.Invalid)
        {
            return ESignResponses.Fail<SampleDto>(ESignCodes.TicketInvalid, 403, "The re-authentication ticket is not valid.");
        }

        if (consumed.Kind != ESignatureReauthKind.Password)
        {
            return ESignResponses.Fail<SampleDto>(ESignCodes.ReauthRequired, 403, "Saving a signature sample always requires your password.");
        }

        var sampleId = Guid.NewGuid();
        var store = await _storage.StoreAsync(new ContentStoreRequest(
            actor.TenantId, Guid.Empty, ContentStorageScope.ESignatureImages, sampleId, sampleId, "sample.png", "image/png",
            new MemoryStream(png!), actor.UserIdText, $"tenant/{actor.TenantId:D}/esign-samples/{actor.UserIdText}"), ct);
        if (!store.IsSuccessful || store.Data is null)
        {
            _logger.LogWarning("E-signature sample image could not be stored. Status={Status}", store.StatusCode);
            return ESignResponses.Fail<SampleDto>(ESignCodes.ReauthUnavailable, 503, "The sample image could not be stored.");
        }

        var now = ESignatureDigest.ToStoredPrecision(DateTime.UtcNow);
        var number = await _numbers.NextAsync(actor.TenantId, "IMZ", now.Year, ct);
        var sample = new ESignatureSample
        {
            Id = sampleId,
            TenantId = actor.TenantId,
            CreatedBy = actor.UserIdText,
            SampleNumber = $"IMZ-{now.Year}-{number:D4}",
            OwnerUserId = actor.UserIdText,
            OwnerLogin = actor.Login,
            OwnerName = actor.Name,
            Kind = kind,
            Format = strokes is null ? ESignatureSampleFormat.Png : ESignatureSampleFormat.PngAndVector,
            ContentId = store.Data.ContentId.ToString("D"),
            StorageProvider = store.Data.StorageProvider,
            StoragePath = store.Data.ObjectKey,
            ImageHash = ESignatureDigest.ImageHash(png),
            VectorJson = strokes,
            CreatedAtUtc = now,
            TicketRef = ESignTicketRef.Of(request.Ticket),
            IdempotencyKey = request.IdempotencyKey!
        };

        var approvalRequired = (await _catalog.GetTenantPolicyAsync(actor.TenantId, ct)).SampleApprovalRequired;
        try
        {
            await _transactions.ExecuteAsync(async (session, txCt) =>
            {
                await _samples.InsertAsync(session, sample, txCt);
                await _events.InsertAsync(session, ESignatureSampleBook.Event(actor.TenantId, sample.Id, actor.UserIdText, 0,
                    ESignatureSampleEventKind.Submitted, actor.UserIdText, actor.Name, now, null), txCt);
                if (!approvalRequired)
                {
                    await _book.ActivateAsync(session, sample, ESignatureSampleEventKind.Activated, 1, actor.UserIdText, actor.Name, now, txCt);
                }

                await ESignatureAuditIntent.EnqueueAsync(_audit, session, actor.TenantId, ESignAuditEvents.SampleSubmitted, AuditOperation.Create,
                    "ESignatureSample", sample.Id, AuditActorType.TenantUser, actor.UserId,
                    new Dictionary<string, object?>
                    {
                        ["sampleNumber"] = sample.SampleNumber,
                        ["kind"] = ESignatureWire.Name(kind),
                        ["format"] = ESignatureWire.Name(sample.Format),
                        ["imageHash"] = sample.ImageHash,
                        ["approvalRequired"] = approvalRequired
                    }, txCt);
                return true;
            }, ct);
        }
        catch (ESignatureDuplicateException)
        {
            var winner = await _samples.GetByIdempotencyKeyAsync(actor.TenantId, actor.UserIdText, request.IdempotencyKey!, ct);
            return winner is null
                ? ESignResponses.Fail<SampleDto>(ESignCodes.ValidationFailed, 409, "The sample could not be recorded.")
                : Response<SampleDto>.Success((await _book.ToDtosAsync(actor.TenantId, [winner], ct))[0], 200);
        }
        catch (ESignatureSampleConflictException)
        {
            return ESignResponses.Fail<SampleDto>(ESignCodes.ValidationFailed, 409, "Your active sample changed meanwhile; retry.");
        }
        catch (PlatformTransactionUnavailableException)
        {
            return ESignResponses.Fail<SampleDto>(ESignCodes.ReauthUnavailable, 503, "The sample could not be recorded now.");
        }

        return Response<SampleDto>.Success((await _book.ToDtosAsync(actor.TenantId, [sample], ct))[0], 201);
    }
}

/// <summary>§4.17–4.19 reads: my samples, a sample's image, pending samples, the decision log.</summary>
public sealed class SampleQueryHandlers :
    IRequestHandler<ListMySamplesQuery, Response<IReadOnlyList<SampleDto>>>,
    IRequestHandler<GetSampleImageQuery, Response<SampleImageFile>>,
    IRequestHandler<ListPendingSamplesQuery, Response<IReadOnlyList<SampleDto>>>,
    IRequestHandler<ListSampleDecisionsQuery, Response<IReadOnlyList<SampleDecisionLogDto>>>
{
    private readonly IESignActorAccessor _actors;
    private readonly IESignatureSampleRepository _samples;
    private readonly IESignatureSampleEventRepository _events;
    private readonly IESignatureSampleBook _book;
    private readonly IESignatureRecordRepository _records;
    private readonly IContentStorageGateway _storage;

    public SampleQueryHandlers(IESignActorAccessor actors, IESignatureSampleRepository samples, IESignatureSampleEventRepository events,
        IESignatureSampleBook book, IESignatureRecordRepository records, IContentStorageGateway storage)
    {
        _actors = actors;
        _samples = samples;
        _events = events;
        _book = book;
        _records = records;
        _storage = storage;
    }

    public async Task<Response<IReadOnlyList<SampleDto>>> Handle(ListMySamplesQuery request, CancellationToken ct)
    {
        var actor = _actors.Current();
        return actor is null
            ? ESignResponses.Actor<IReadOnlyList<SampleDto>>()
            : Response<IReadOnlyList<SampleDto>>.Success(await _book.ToDtosAsync(actor.TenantId,
                await _samples.ListByOwnerAsync(actor.TenantId, actor.UserIdText, ct), ct));
    }

    public async Task<Response<SampleImageFile>> Handle(GetSampleImageQuery request, CancellationToken ct)
    {
        var actor = _actors.Current();
        if (actor is null)
        {
            return ESignResponses.Actor<SampleImageFile>();
        }

        var sample = await _samples.GetAsync(actor.TenantId, request.Id, ct);
        var allowed = sample is not null
                      && (sample.OwnerUserId == actor.UserIdText
                          || request.CanApprove
                          || (request.CanReadSignatures && (await _records.GetSampleUsageAsync(actor.TenantId, [sample.Id], ct)).Count > 0));
        if (!allowed)
        {
            return ESignResponses.Fail<SampleImageFile>(ESignCodes.NotFound, 404, "Sample not found.");
        }

        var opened = await _storage.OpenReadAsync(sample!.StorageProvider, sample.StoragePath, ct);
        if (!opened.IsSuccessful || opened.Data is null)
        {
            return ESignResponses.Fail<SampleImageFile>(ESignCodes.NotFound, 404, "Sample image not found.");
        }

        await using var stream = opened.Data.Content;
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        // Never serve bytes that no longer match the recorded hash.
        return ESignatureDigest.ImageHash(bytes) == sample.ImageHash
            ? Response<SampleImageFile>.Success(new SampleImageFile(bytes, sample.ImageHash))
            : ESignResponses.Fail<SampleImageFile>(ESignCodes.NotFound, 404, "Sample image not found.");
    }

    public async Task<Response<IReadOnlyList<SampleDto>>> Handle(ListPendingSamplesQuery request, CancellationToken ct)
    {
        var actor = _actors.Current();
        if (actor is null)
        {
            return ESignResponses.Actor<IReadOnlyList<SampleDto>>();
        }

        var candidates = await _samples.ListByIdsAsync(actor.TenantId, (await _events.ListSubmittedSampleIdsAsync(actor.TenantId, ct)).Distinct().ToList(), ct);
        IReadOnlyList<SampleDto> pending = (await _book.ToDtosAsync(actor.TenantId, candidates, ct))
            .Where(s => s.Status == ESignatureWire.Name(ESignatureSampleStatus.Pending))
            .OrderBy(s => s.CreatedAtUtc)
            .ToList();
        return Response<IReadOnlyList<SampleDto>>.Success(pending);
    }

    public async Task<Response<IReadOnlyList<SampleDecisionLogDto>>> Handle(ListSampleDecisionsQuery request, CancellationToken ct)
    {
        var actor = _actors.Current();
        if (actor is null)
        {
            return ESignResponses.Actor<IReadOnlyList<SampleDecisionLogDto>>();
        }

        var decisions = await _events.ListDecisionsAsync(actor.TenantId, 500, ct);
        var samples = (await _samples.ListByIdsAsync(actor.TenantId, decisions.Select(d => d.SampleId).Distinct().ToList(), ct)).ToDictionary(s => s.Id);
        IReadOnlyList<SampleDecisionLogDto> items = decisions
            .Where(d => samples.ContainsKey(d.SampleId))
            .Select(d => new SampleDecisionLogDto(d.AtUtc, d.ByName, samples[d.SampleId].OwnerName, samples[d.SampleId].SampleNumber,
                d.Event == ESignatureSampleEventKind.Approved ? "approved" : "rejected", d.Reason))
            .ToList();
        return Response<IReadOnlyList<SampleDecisionLogDto>>.Success(items);
    }
}

/// <summary>
/// §4.19 approve / reject (CT-D5) and §4.18 deactivate / withdraw. Every decision is one insert-only event plus its audit
/// intent in one transaction; a parallel second decision on the same sample hits the (sample, sequence) index and gets 409.
/// </summary>
public sealed class SampleCommandHandlers :
    IRequestHandler<DecideSampleCommand, Response<SampleDto>>,
    IRequestHandler<DeactivateSampleCommand, Response<SampleDto>>,
    IRequestHandler<WithdrawSampleCommand, Response<SampleDto>>
{
    private readonly IESignActorAccessor _actors;
    private readonly IESignatureSampleRepository _samples;
    private readonly IESignatureSampleEventRepository _events;
    private readonly IESignSampleOwnerStateRepository _owners;
    private readonly IESignatureSampleBook _book;
    private readonly IESignatureRecordRepository _records;
    private readonly IPlatformTransactionExecutor _transactions;
    private readonly ITransactionalAuditOutboxWriter _audit;

    public SampleCommandHandlers(IESignActorAccessor actors, IESignatureSampleRepository samples, IESignatureSampleEventRepository events,
        IESignSampleOwnerStateRepository owners, IESignatureSampleBook book, IESignatureRecordRepository records,
        IPlatformTransactionExecutor transactions, ITransactionalAuditOutboxWriter audit)
    {
        _actors = actors;
        _samples = samples;
        _events = events;
        _owners = owners;
        _book = book;
        _records = records;
        _transactions = transactions;
        _audit = audit;
    }

    public async Task<Response<SampleDto>> Handle(DecideSampleCommand request, CancellationToken ct)
    {
        var actor = _actors.Current();
        if (actor is null)
        {
            return ESignResponses.Actor<SampleDto>();
        }

        var sample = await _samples.GetAsync(actor.TenantId, request.Id, ct);
        if (sample is null)
        {
            return ESignResponses.Fail<SampleDto>(ESignCodes.NotFound, 404, "Sample not found.");
        }

        // CT-D5 / V21 — the owner never decides their own sample, whatever keys they hold.
        if (ESignatureSampleRules.IsSelfDecision(sample, actor.UserIdText))
        {
            return ESignResponses.Fail<SampleDto>(ESignCodes.SelfApprovalForbidden, 403, "You cannot decide your own signature sample.");
        }

        if (!request.Approve && !ESignatureRules.ReasonOk(request.Reason))
        {
            return ESignResponses.Fail<SampleDto>(ESignCodes.ReasonTooShort, 400, "The reason must be 10–1000 characters.");
        }

        var now = ESignatureDigest.ToStoredPrecision(DateTime.UtcNow);
        try
        {
            await _transactions.ExecuteAsync(async (session, txCt) =>
            {
                var events = await _events.ListBySampleAsync(actor.TenantId, sample.Id, session, txCt);
                if (ESignatureSampleRules.Project(events) != ESignatureSampleStatus.Pending)
                {
                    throw new ESignRejectedException(ESignCodes.SampleNotPending, 409, "The sample is not waiting for a decision.");
                }

                if (request.Approve)
                {
                    await _book.ActivateAsync(session, sample, ESignatureSampleEventKind.Approved, events.Count, actor.UserIdText, actor.Name, now, txCt);
                }
                else
                {
                    await _events.InsertAsync(session, ESignatureSampleBook.Event(actor.TenantId, sample.Id, sample.OwnerUserId, events.Count,
                        ESignatureSampleEventKind.Rejected, actor.UserIdText, actor.Name, now, request.Reason!.Trim()), txCt);
                }

                await ESignatureAuditIntent.EnqueueAsync(_audit, session, actor.TenantId,
                    request.Approve ? ESignAuditEvents.SampleApproved : ESignAuditEvents.SampleRejected,
                    request.Approve ? AuditOperation.Activate : AuditOperation.LifecycleTransition,
                    "ESignatureSample", sample.Id, AuditActorType.TenantUser, actor.UserId,
                    new Dictionary<string, object?>
                    {
                        ["sampleNumber"] = sample.SampleNumber,
                        ["ownerUserId"] = sample.OwnerUserId,
                        ["decision"] = request.Approve ? "approved" : "rejected",
                        ["reasonLength"] = request.Reason?.Trim().Length
                    }, txCt);
                return true;
            }, ct);
        }
        catch (ESignRejectedException rejected)
        {
            return ESignResponses.Fail<SampleDto>(rejected.Code, rejected.Status, rejected.Message);
        }
        catch (Exception ex) when (ex is ESignatureDuplicateException or ESignatureSampleConflictException)
        {
            return ESignResponses.Fail<SampleDto>(ESignCodes.SampleNotPending, 409, "The sample was decided meanwhile.");
        }
        catch (PlatformTransactionUnavailableException)
        {
            return ESignResponses.Fail<SampleDto>(ESignCodes.ReauthUnavailable, 503, "The decision could not be recorded now.");
        }

        return Response<SampleDto>.Success((await _book.ToDtosAsync(actor.TenantId, [sample], ct))[0]);
    }

    public Task<Response<SampleDto>> Handle(DeactivateSampleCommand request, CancellationToken ct) =>
        OwnerTransitionAsync(request.Id, withdraw: false, ct);

    public Task<Response<SampleDto>> Handle(WithdrawSampleCommand request, CancellationToken ct) =>
        OwnerTransitionAsync(request.Id, withdraw: true, ct);

    /// <summary>Deactivate (Active → Inactive) or withdraw (unused and not Active). Owner only — anyone else gets 404.</summary>
    private async Task<Response<SampleDto>> OwnerTransitionAsync(Guid id, bool withdraw, CancellationToken ct)
    {
        var actor = _actors.Current();
        if (actor is null)
        {
            return ESignResponses.Actor<SampleDto>();
        }

        var sample = await _samples.GetAsync(actor.TenantId, id, ct);
        if (sample is null || sample.OwnerUserId != actor.UserIdText)
        {
            return ESignResponses.Fail<SampleDto>(ESignCodes.NotFound, 404, "Sample not found.");
        }

        var now = ESignatureDigest.ToStoredPrecision(DateTime.UtcNow);
        try
        {
            await _transactions.ExecuteAsync(async (session, txCt) =>
            {
                var events = await _events.ListBySampleAsync(actor.TenantId, sample.Id, session, txCt);
                var status = ESignatureSampleRules.Project(events);
                if (withdraw)
                {
                    var used = (await _records.GetSampleUsageAsync(actor.TenantId, [sample.Id], txCt)).GetValueOrDefault(sample.Id);
                    if (!ESignatureSampleRules.CanWithdraw(status, used))
                    {
                        throw new ESignRejectedException(ESignCodes.SampleInUse, 409, "A used or active sample cannot be withdrawn; deactivate it.");
                    }
                }
                else if (status != ESignatureSampleStatus.Active)
                {
                    throw new ESignRejectedException(ESignCodes.SampleNotActive, 409, "Only the active sample can be deactivated.");
                }

                await _events.InsertAsync(session, ESignatureSampleBook.Event(actor.TenantId, sample.Id, sample.OwnerUserId, events.Count,
                    withdraw ? ESignatureSampleEventKind.Withdrawn : ESignatureSampleEventKind.Deactivated, actor.UserIdText, actor.Name, now, null), txCt);
                if (!withdraw && !await _owners.MoveActiveAsync(session, actor.TenantId, actor.UserIdText, sample.Id, null, txCt))
                {
                    throw new ESignatureSampleConflictException();
                }

                await ESignatureAuditIntent.EnqueueAsync(_audit, session, actor.TenantId,
                    withdraw ? ESignAuditEvents.SampleWithdrawn : ESignAuditEvents.SampleDeactivated,
                    withdraw ? AuditOperation.LifecycleTransition : AuditOperation.Deactivate,
                    "ESignatureSample", sample.Id, AuditActorType.TenantUser, actor.UserId,
                    new Dictionary<string, object?> { ["sampleNumber"] = sample.SampleNumber }, txCt);
                return true;
            }, ct);
        }
        catch (ESignRejectedException rejected)
        {
            return ESignResponses.Fail<SampleDto>(rejected.Code, rejected.Status, rejected.Message);
        }
        catch (Exception ex) when (ex is ESignatureDuplicateException or ESignatureSampleConflictException)
        {
            return ESignResponses.Fail<SampleDto>(ESignCodes.ValidationFailed, 409, "The sample changed meanwhile; retry.");
        }
        catch (PlatformTransactionUnavailableException)
        {
            return ESignResponses.Fail<SampleDto>(ESignCodes.ReauthUnavailable, 503, "The change could not be recorded now.");
        }

        return Response<SampleDto>.Success((await _book.ToDtosAsync(actor.TenantId, [sample], ct))[0]);
    }
}
