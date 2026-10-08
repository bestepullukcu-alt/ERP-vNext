using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.ESignature.Services;
using Diten.Platform.Application.Features.TenantOrganization.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.ESignature;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.ESignature.Handlers;

/// <summary>Â§4.9 â€” a consumer module (with the user's token) or a user asks one person to sign one subject.</summary>
public sealed class CreateSignatureRequestCommandHandler : IRequestHandler<CreateSignatureRequestCommand, Response<SignatureRequestDto>>
{
    private readonly IESignActorAccessor _actors;
    private readonly ISignableSubjectRegistry _subjects;
    private readonly IESignatureCatalog _catalog;
    private readonly IESignMeaningRepository _meanings;
    private readonly IUserReferenceValidator _users;
    private readonly IESignRequestRepository _requests;
    private readonly IESignNumberGenerator _numbers;
    private readonly IPlatformTransactionExecutor _transactions;
    private readonly ITransactionalAuditOutboxWriter _audit;

    public CreateSignatureRequestCommandHandler(IESignActorAccessor actors, ISignableSubjectRegistry subjects, IESignatureCatalog catalog,
        IESignMeaningRepository meanings, IUserReferenceValidator users, IESignRequestRepository requests, IESignNumberGenerator numbers,
        IPlatformTransactionExecutor transactions, ITransactionalAuditOutboxWriter audit)
    {
        _actors = actors;
        _subjects = subjects;
        _catalog = catalog;
        _meanings = meanings;
        _users = users;
        _requests = requests;
        _numbers = numbers;
        _transactions = transactions;
        _audit = audit;
    }

    public async Task<Response<SignatureRequestDto>> Handle(CreateSignatureRequestCommand request, CancellationToken ct)
    {
        var actor = _actors.Current();
        if (actor is null)
        {
            return ESignResponses.Actor<SignatureRequestDto>();
        }

        if (!ESignResponses.IdempotencyKeyOk(request.IdempotencyKey))
        {
            return ESignResponses.Validation<SignatureRequestDto>("idempotencyKey is required (8â€“100 characters).");
        }

        var replay = await _requests.GetByIdempotencyKeyAsync(actor.TenantId, actor.UserIdText, request.IdempotencyKey!, ct);
        if (replay is not null)
        {
            return Response<SignatureRequestDto>.Success(ESignatureMapping.ToDto(replay), 200);
        }

        var now = DateTime.UtcNow;
        if (request.SignerUserId is not { } signerId || signerId == Guid.Empty || string.IsNullOrWhiteSpace(request.MeaningCode))
        {
            return ESignResponses.Validation<SignatureRequestDto>("signerUserId and meaningCode are required.");
        }

        if (request.DueAtUtc is not { } due || due <= now)
        {
            return ESignResponses.Validation<SignatureRequestDto>("dueAtUtc must be in the future.");
        }

        if (request.Note is { Length: > 500 })
        {
            return ESignResponses.Validation<SignatureRequestDto>("note must be at most 500 characters.");
        }

        var resolution = await _subjects.ResolveAsync(request.Module ?? "", request.SubjectType ?? "", request.SubjectId ?? "", ct);
        if (resolution.Outcome != SignableSubjectOutcome.Found)
        {
            return ESignResponses.Fail<SignatureRequestDto>(ESignCodes.SubjectNotFound, 404, "The subject was not found.");
        }

        await _catalog.EnsureMeaningsAsync(actor.TenantId, ct);
        var meaning = await _meanings.GetAsync(actor.TenantId, request.MeaningCode, ct);
        var policy = await _catalog.GetEffectivePolicyAsync(actor.TenantId, resolution.Subject!.LegalEntityId, ct);
        var row = await _catalog.GetRowAsync(actor.TenantId, policy, request.ActionCode, ct);
        if (meaning is null || !meaning.IsActive
            || (row?.MeaningCode is { } fixedMeaning && !string.Equals(fixedMeaning, request.MeaningCode, StringComparison.Ordinal)))
        {
            return ESignResponses.Fail<SignatureRequestDto>(ESignCodes.MeaningNotAllowed, 409, "The meaning is not active or not allowed for this action.");
        }

        var signer = await _users.ValidateAsync(signerId, ct);
        if (!signer.IsSuccessful || signer.Data is not { Referenceable: true })
        {
            return ESignResponses.Fail<SignatureRequestDto>(ESignCodes.SignerNotFound, 404, "The signer is not a user of this tenant.");
        }

        var number = await _numbers.NextAsync(actor.TenantId, "SR", now.Year, ct);
        var entity = new ESignatureRequest
        {
            TenantId = actor.TenantId,
            CreatedBy = actor.UserIdText,
            RequestNumber = $"SR-{now.Year}-{number:D4}",
            Module = request.Module!,
            SubjectType = request.SubjectType!,
            SubjectId = request.SubjectId!,
            SubjectDisplay = resolution.Subject.Display,
            LegalEntityId = resolution.Subject.LegalEntityId,
            ActionCode = string.IsNullOrWhiteSpace(request.ActionCode) ? null : request.ActionCode,
            MeaningCode = meaning.Code,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            RequestedBy = actor.UserIdText,
            RequestedByName = actor.Name,
            RequestedAtUtc = now,
            SignerUserId = signerId.ToString("D"),
            DueAtUtc = due.ToUniversalTime(),
            IdempotencyKey = request.IdempotencyKey!
        };

        try
        {
            await _transactions.ExecuteAsync(async (session, txCt) =>
            {
                await _requests.InsertAsync(session, entity, txCt);

                await ESignatureAuditIntent.EnqueueAsync(_audit, session, actor.TenantId, ESignAuditEvents.RequestCreated, AuditOperation.Create,
                    "ESignatureRequest", entity.Id, AuditActorType.TenantUser, actor.UserId,
                    new Dictionary<string, object?>
                    {
                        ["requestNumber"] = entity.RequestNumber,
                        ["module"] = entity.Module,
                        ["subjectType"] = entity.SubjectType,
                        ["subjectId"] = entity.SubjectId,
                        ["meaningCode"] = entity.MeaningCode,
                        ["signerUserId"] = entity.SignerUserId,
                        ["dueAtUtc"] = entity.DueAtUtc
                    }, txCt);
                return true;
            }, ct);
        }
        catch (ESignatureDuplicateException)
        {
            var winner = await _requests.GetByIdempotencyKeyAsync(actor.TenantId, actor.UserIdText, request.IdempotencyKey!, ct);
            return winner is null
                ? ESignResponses.Fail<SignatureRequestDto>(ESignCodes.ValidationFailed, 409, "The request could not be recorded.")
                : Response<SignatureRequestDto>.Success(ESignatureMapping.ToDto(winner), 200);
        }
        catch (PlatformTransactionUnavailableException)
        {
            return ESignResponses.Fail<SignatureRequestDto>(ESignCodes.ReauthUnavailable, 503, "The request could not be recorded now.");
        }

        return Response<SignatureRequestDto>.Success(ESignatureMapping.ToDto(entity), 201);
    }
}

/// <summary>Â§4.10 â€” scope=mine (default and only scope): requests where I am the nominated signer.</summary>
public sealed class ListSignatureRequestsQueryHandler : IRequestHandler<ListSignatureRequestsQuery, Response<SignatureRequestPageDto>>
{
    private readonly IESignActorAccessor _actors;
    private readonly IESignRequestRepository _requests;

    public ListSignatureRequestsQueryHandler(IESignActorAccessor actors, IESignRequestRepository requests)
    {
        _actors = actors;
        _requests = requests;
    }

    public async Task<Response<SignatureRequestPageDto>> Handle(ListSignatureRequestsQuery request, CancellationToken ct)
    {
        var actor = _actors.Current();
        if (actor is null)
        {
            return ESignResponses.Actor<SignatureRequestPageDto>();
        }

        if (!string.IsNullOrWhiteSpace(request.Scope) && !string.Equals(request.Scope, "mine", StringComparison.OrdinalIgnoreCase))
        {
            return ESignResponses.Validation<SignatureRequestPageDto>("scope must be mine.");
        }

        ESignatureRequestStatus? status = null;
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!ESignatureWire.TryParse<ESignatureRequestStatus>(request.Status, out var parsed))
            {
                return ESignResponses.Validation<SignatureRequestPageDto>("status is not a request status.");
            }

            status = parsed;
        }

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize <= 0 ? 25 : request.PageSize, 1, 100);
        var (items, total, waiting) = await _requests.ListForSignerAsync(actor.TenantId, actor.UserIdText,
            new ESignatureRequestQuery(request.Module, status, request.FromUtc, request.ToUtc), (page - 1) * pageSize, pageSize, ct);
        return Response<SignatureRequestPageDto>.Success(new SignatureRequestPageDto(
            items.Select(ESignatureMapping.ToDto).ToList(), total, page, pageSize, waiting));
    }
}

/// <summary>Â§4.11 â€” only the signer or the requester may read a request; anyone else gets 404.</summary>
public sealed class GetSignatureRequestQueryHandler : IRequestHandler<GetSignatureRequestQuery, Response<SignatureRequestDto>>
{
    private readonly IESignActorAccessor _actors;
    private readonly IESignRequestRepository _requests;

    public GetSignatureRequestQueryHandler(IESignActorAccessor actors, IESignRequestRepository requests)
    {
        _actors = actors;
        _requests = requests;
    }

    public async Task<Response<SignatureRequestDto>> Handle(GetSignatureRequestQuery request, CancellationToken ct)
    {
        var actor = _actors.Current();
        if (actor is null)
        {
            return ESignResponses.Actor<SignatureRequestDto>();
        }

        var entity = await _requests.GetAsync(actor.TenantId, request.Id, ct);
        return entity is null || (entity.SignerUserId != actor.UserIdText && entity.RequestedBy != actor.UserIdText)
            ? ESignResponses.Fail<SignatureRequestDto>(ESignCodes.NotFound, 404, "Signature request not found.")
            : Response<SignatureRequestDto>.Success(ESignatureMapping.ToDto(entity));
    }
}

/// <summary>Reject (Â§4.12, the nominated signer) and cancel (Â§4.13, the requester): guarded transition + audit, one transaction.</summary>
public sealed class SignatureRequestDecisionHandlers :
    IRequestHandler<RejectSignatureRequestCommand, Response<SignatureRequestDto>>,
    IRequestHandler<CancelSignatureRequestCommand, Response<SignatureRequestDto>>
{
    private readonly IESignActorAccessor _actors;
    private readonly IESignRequestRepository _requests;
    private readonly IPlatformTransactionExecutor _transactions;
    private readonly ITransactionalAuditOutboxWriter _audit;

    public SignatureRequestDecisionHandlers(IESignActorAccessor actors, IESignRequestRepository requests,
        IPlatformTransactionExecutor transactions, ITransactionalAuditOutboxWriter audit)
    {
        _actors = actors;
        _requests = requests;
        _transactions = transactions;
        _audit = audit;
    }

    public async Task<Response<SignatureRequestDto>> Handle(RejectSignatureRequestCommand request, CancellationToken ct)
    {
        var actor = _actors.Current();
        if (actor is null)
        {
            return ESignResponses.Actor<SignatureRequestDto>();
        }

        var entity = await _requests.GetAsync(actor.TenantId, request.Id, ct);
        if (entity is null)
        {
            return ESignResponses.Fail<SignatureRequestDto>(ESignCodes.NotFound, 404, "Signature request not found.");
        }

        if (entity.SignerUserId != actor.UserIdText)
        {
            return ESignResponses.Fail<SignatureRequestDto>(ESignCodes.SignerNotNominated, 403, "You are not the signer of this request.");
        }

        if (!entity.IsOpen)
        {
            return entity.Status == ESignatureRequestStatus.Rejected && request.IdempotencyKey is not null
                ? Response<SignatureRequestDto>.Success(ESignatureMapping.ToDto(entity), 200)
                : ESignResponses.Fail<SignatureRequestDto>(ESignCodes.RequestNotOpen, 409, "The signature request is not waiting.");
        }

        if (!ESignatureRules.ReasonOk(request.Reason))
        {
            return ESignResponses.Fail<SignatureRequestDto>(ESignCodes.ReasonTooShort, 400, "The reason must be 10â€“1000 characters.");
        }

        return await DecideAsync(actor, entity, e => e.Reject(request.Reason!.Trim(), DateTime.UtcNow), ESignAuditEvents.RequestRejected, ct);
    }

    public async Task<Response<SignatureRequestDto>> Handle(CancelSignatureRequestCommand request, CancellationToken ct)
    {
        var actor = _actors.Current();
        if (actor is null)
        {
            return ESignResponses.Actor<SignatureRequestDto>();
        }

        var entity = await _requests.GetAsync(actor.TenantId, request.Id, ct);
        if (entity is null || entity.RequestedBy != actor.UserIdText)
        {
            return ESignResponses.Fail<SignatureRequestDto>(ESignCodes.NotFound, 404, "Signature request not found.");
        }

        if (!entity.IsOpen)
        {
            return ESignResponses.Fail<SignatureRequestDto>(ESignCodes.RequestNotOpen, 409, "The signature request is not waiting.");
        }

        if (!ESignatureRules.ReasonOk(request.Reason))
        {
            return ESignResponses.Fail<SignatureRequestDto>(ESignCodes.ReasonTooShort, 400, "The reason must be 10â€“1000 characters.");
        }

        return await DecideAsync(actor, entity, e => e.Cancel(request.Reason!.Trim(), DateTime.UtcNow), ESignAuditEvents.RequestCancelled, ct);
    }

    private async Task<Response<SignatureRequestDto>> DecideAsync(ESignActor actor, ESignatureRequest entity, Action<ESignatureRequest> decide,
        string eventName, CancellationToken ct)
    {
        var expected = entity.Version;
        decide(entity);
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.UpdatedBy = actor.UserIdText;
        try
        {
            var saved = await _transactions.ExecuteAsync(async (session, txCt) =>
            {
                if (!await _requests.SaveDecisionAsync(session, entity, expected, txCt))
                {
                    return false;
                }

                await ESignatureAuditIntent.EnqueueAsync(_audit, session, actor.TenantId, eventName, AuditOperation.LifecycleTransition,
                    "ESignatureRequest", entity.Id, AuditActorType.TenantUser, actor.UserId,
                    new Dictionary<string, object?> { ["requestNumber"] = entity.RequestNumber, ["status"] = ESignatureWire.Name(entity.Status) },
                    txCt);
                return true;
            }, ct);
            if (!saved)
            {
                return ESignResponses.Fail<SignatureRequestDto>(ESignCodes.RequestNotOpen, 409, "The signature request was decided meanwhile.");
            }
        }
        catch (PlatformTransactionUnavailableException)
        {
            return ESignResponses.Fail<SignatureRequestDto>(ESignCodes.ReauthUnavailable, 503, "The decision could not be recorded now.");
        }

        entity.Version = expected + 1;
        return Response<SignatureRequestDto>.Success(ESignatureMapping.ToDto(entity));
    }
}

/// <summary>
/// System-only expiry (Â§4.13 "Expiry is system-only"): Waiting requests past <c>DueAtUtc</c> â†’ Expired, each with its audit
/// event, in the CURRENT tenant scope (the Hangfire sweeper begins one scope per active tenant).
/// </summary>
public sealed class ExpireSignatureRequestsCommandHandler : IRequestHandler<ExpireSignatureRequestsCommand, Response<int>>
{
    private readonly ITenantContext _tenant;
    private readonly IESignRequestRepository _requests;
    private readonly IPlatformTransactionExecutor _transactions;
    private readonly ITransactionalAuditOutboxWriter _audit;

    public ExpireSignatureRequestsCommandHandler(ITenantContext tenant, IESignRequestRepository requests,
        IPlatformTransactionExecutor transactions, ITransactionalAuditOutboxWriter audit)
    {
        _tenant = tenant;
        _requests = requests;
        _transactions = transactions;
        _audit = audit;
    }

    public async Task<Response<int>> Handle(ExpireSignatureRequestsCommand request, CancellationToken ct)
    {
        if (!_tenant.IsResolved || _tenant.TenantId == Guid.Empty)
        {
            return Response<int>.Fail("A tenant scope is required.", 400, ESignCodes.ValidationFailed);
        }

        var now = DateTime.UtcNow;
        var overdue = await _requests.ListOverdueAsync(_tenant.TenantId, now, Math.Clamp(request.MaxItems, 1, 500), ct);
        var expired = 0;
        foreach (var entity in overdue)
        {
            var expected = entity.Version;
            entity.Expire(now);
            entity.UpdatedAt = DateTimeOffset.UtcNow;
            entity.UpdatedBy = "system:esign-expiry";
            var saved = await _transactions.ExecuteAsync(async (session, txCt) =>
            {
                if (!await _requests.SaveDecisionAsync(session, entity, expected, txCt))
                {
                    return false; // decided meanwhile (signed / rejected / cancelled): nothing to expire
                }

                await ESignatureAuditIntent.EnqueueAsync(_audit, session, entity.TenantId, ESignAuditEvents.RequestExpired,
                    AuditOperation.LifecycleTransition, "ESignatureRequest", entity.Id, AuditActorType.System, null,
                    new Dictionary<string, object?> { ["requestNumber"] = entity.RequestNumber, ["dueAtUtc"] = entity.DueAtUtc }, txCt);
                return true;
            }, ct);
            if (saved)
            {
                expired++;
            }
        }

        return Response<int>.Success(expired, 200, null);
    }
}
