using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Quarantine.Commands;
using Diten.InventoryService.Application.Features.Quarantine.Queries;
using Diten.InventoryService.Application.Features.Quarantine.Services;
using Diten.InventoryService.Domain.Quarantine.Entities;
using Diten.InventoryService.Domain.Quarantine.Repositories;
using Diten.InventoryService.Domain.Quarantine.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.Quarantine.Handlers;

/// <summary>§4.17 getSettings — the resolved company's settings, or the defaults at version 0.</summary>
public sealed class GetSettingsHandler : IRequestHandler<GetSettingsQuery, Response<SettingsDto>>
{
    private readonly QuarantineFacts _facts;

    public GetSettingsHandler(QuarantineFacts facts) => _facts = facts;

    public async Task<Response<SettingsDto>> Handle(GetSettingsQuery request, CancellationToken ct)
        => Response<SettingsDto>.Success(QuarantineMapping.Settings(await _facts.SettingsAsync(ct)));
}

/// <summary>
/// §4.17 saveSettings (If-Match, version 0 = never saved). Rules: wait target 1–60 (V19, KQ-D); 1–10 approver candidates
/// with prefix <c>position:</c> / <c>user:</c>; every sample location an Active stockable location of its warehouse
/// (KQ-E, else 400 <c>QUARANTINE_SAMPLE_LOCATION_INVALID</c>). A member left out keeps its value.
/// </summary>
public sealed class SaveSettingsHandler : IRequestHandler<SaveSettingsCommand, Response<SettingsDto>>
{
    private readonly IQuarantineRepository _repository;
    private readonly QuarantineFacts _facts;
    private readonly QuarantineSupport _support;

    public SaveSettingsHandler(IQuarantineRepository repository, QuarantineFacts facts, QuarantineSupport support)
    {
        _repository = repository;
        _facts = facts;
        _support = support;
    }

    public async Task<Response<SettingsDto>> Handle(SaveSettingsCommand request, CancellationToken ct)
    {
        var settings = await _facts.SettingsAsync(ct);
        if (settings.Version != request.ExpectedVersion)
        {
            return QuarantineSupport.Conflict<SettingsDto>();
        }

        var wait = request.WaitTargetDays ?? settings.WaitTargetDays;
        if (wait is < QuarantineContract.MinWaitTargetDays or > QuarantineContract.MaxWaitTargetDays)
        {
            return QuarantineSupport.Invalid<SettingsDto>($"waitTargetDays must be {QuarantineContract.MinWaitTargetDays}-{QuarantineContract.MaxWaitTargetDays}.");
        }

        var candidates = request.ApproverCandidates ?? settings.ApproverCandidates;
        if (candidates.Count is < QuarantineContract.MinApproverCandidates or > QuarantineContract.MaxApproverCandidates
            || candidates.Any(c => !IsCandidate(c)) || candidates.Distinct(StringComparer.Ordinal).Count() != candidates.Count)
        {
            return QuarantineSupport.Invalid<SettingsDto>(
                $"approverCandidates must hold {QuarantineContract.MinApproverCandidates}-{QuarantineContract.MaxApproverCandidates} distinct entries 'position:<uuid>' or 'user:<uuid>'.");
        }

        var reconcile = request.ReconcileAfterSeconds ?? settings.ReconcileAfterSeconds;
        if (reconcile is < 0 or > 3600)
        {
            return QuarantineSupport.Invalid<SettingsDto>("reconcileAfterSeconds must be 0-3600.");
        }

        var samples = request.SampleLocations ?? settings.SampleLocations;
        foreach (var (warehouse, location) in samples)
        {
            if (!await _facts.IsStockableLocationOfWarehouseAsync(location, warehouse, ct))
            {
                return QuarantineSupport.Fail<SettingsDto>(QuarantineDecisionRules.SampleLocationInvalid,
                    $"Sample location {location} is not an Active stockable location of warehouse {warehouse}.", 400);
            }
        }

        var now = QuarantineSupport.Now();
        settings.TenantId = _support.TenantId;
        settings.LegalEntityId = _support.LegalEntityId;
        settings.WaitTargetDays = wait;
        settings.FourEyes = request.FourEyes ?? settings.FourEyes;
        settings.ApproverCandidates = [.. candidates];
        settings.SampleLocations = new Dictionary<string, string>(samples, StringComparer.Ordinal);
        settings.LotReleaseRequiresApproval = request.LotReleaseRequiresApproval ?? settings.LotReleaseRequiresApproval;
        settings.ReconcileAfterSeconds = reconcile;
        settings.UpdatedAt = now;
        settings.UpdatedBy = _support.UserId;
        settings.Version = request.ExpectedVersion + 1;
        var saved = await _repository.InTransactionAsync((session, c) => session.SaveSettingsAsync(settings, request.ExpectedVersion, c), ct);
        return saved ? Response<SettingsDto>.Success(QuarantineMapping.Settings(settings)) : QuarantineSupport.Conflict<SettingsDto>();
    }

    private static bool IsCandidate(string value)
    {
        var parts = value.Split(':', 2);
        return parts.Length == 2 && parts[0] is "position" or "user" && Guid.TryParse(parts[1], out _);
    }
}

/// <summary>§4.19 listBlockReasons (the five CT-Q8 seeds exist from the first read).</summary>
public sealed class ListBlockReasonsHandler : IRequestHandler<ListBlockReasonsQuery, Response<IReadOnlyList<BlockReasonDto>>>
{
    private readonly IQuarantineRepository _repository;

    public ListBlockReasonsHandler(IQuarantineRepository repository) => _repository = repository;

    public async Task<Response<IReadOnlyList<BlockReasonDto>>> Handle(ListBlockReasonsQuery request, CancellationToken ct)
    {
        if (request.ActiveOnly is not (null or "true" or "false"))
        {
            return QuarantineSupport.Invalid<IReadOnlyList<BlockReasonDto>>("activeOnly must be true or false.");
        }

        var reasons = await _repository.BlockReasonsAsync(ct);
        IReadOnlyList<BlockReasonDto> body = reasons
            .Where(r => request.ActiveOnly != "true" || r.IsActive)
            .OrderBy(r => r.Code, StringComparer.Ordinal)
            .Select(QuarantineMapping.BlockReason).ToList();
        return Response<IReadOnlyList<BlockReasonDto>>.Success(body);
    }
}

/// <summary>§4.19 createBlockReason / updateBlockReason (V20).</summary>
public sealed class BlockReasonCommandHandlers :
    IRequestHandler<CreateBlockReasonCommand, Response<BlockReasonDto>>,
    IRequestHandler<UpdateBlockReasonCommand, Response<BlockReasonDto>>
{
    private readonly IQuarantineRepository _repository;
    private readonly QuarantineSupport _support;

    public BlockReasonCommandHandlers(IQuarantineRepository repository, QuarantineSupport support)
    {
        _repository = repository;
        _support = support;
    }

    public async Task<Response<BlockReasonDto>> Handle(CreateBlockReasonCommand request, CancellationToken ct)
    {
        if (!BlockReasonSeeds.IsValidCode(request.Code))
        {
            return QuarantineSupport.Invalid<BlockReasonDto>("code must match ^[A-Z][A-Z0-9_]{1,31}$.");
        }

        if (NamesRefusal(request.Names) is { } refusal)
        {
            return refusal;
        }

        var existing = await _repository.BlockReasonsAsync(ct);
        if (existing.Any(r => r.Code == request.Code))
        {
            return QuarantineSupport.Fail<BlockReasonDto>(QuarantineErrorCodes.BlockReasonExists, "This block-reason code already exists.", 409);
        }

        var reason = new BlockReason
        {
            Id = Guid.NewGuid(),
            TenantId = _support.TenantId,
            LegalEntityId = null,
            CreatedAt = QuarantineSupport.Now(),
            Code = request.Code!,
            Names = Trimmed(request.Names!),
            IsActive = request.IsActive
        };
        try
        {
            await _repository.InTransactionAsync<bool>(async (session, c) =>
            {
                await session.InsertBlockReasonAsync(reason, c);
                return true;
            }, ct);
        }
        catch (QuarantineDuplicateException)
        {
            return QuarantineSupport.Fail<BlockReasonDto>(QuarantineErrorCodes.BlockReasonExists, "This block-reason code already exists.", 409);
        }

        return Response<BlockReasonDto>.Success(QuarantineMapping.BlockReason(reason), 201);
    }

    public async Task<Response<BlockReasonDto>> Handle(UpdateBlockReasonCommand request, CancellationToken ct)
    {
        var reason = (await _repository.BlockReasonsAsync(ct)).FirstOrDefault(r => r.Code == request.Code);
        if (reason is null)
        {
            return QuarantineSupport.Invalid<BlockReasonDto>("Unknown block-reason code.");
        }

        if (request.Names is not null && NamesRefusal(request.Names) is { } refusal)
        {
            return refusal;
        }

        if (request.IsActive == false && reason.FirstUsedAtUtc is not null)
        {
            return QuarantineSupport.Fail<BlockReasonDto>(QuarantineErrorCodes.BlockReasonInUse, "A used block-reason code cannot be deactivated.", 409);
        }

        var expected = reason.Version;
        if (request.Names is not null) reason.Names = Trimmed(request.Names);
        if (request.IsActive is { } active) reason.IsActive = active;
        reason.UpdatedAt = QuarantineSupport.Now();
        reason.Version = expected + 1;
        var saved = await _repository.InTransactionAsync((session, c) => session.ReplaceBlockReasonAsync(reason, expected, c), ct);
        return saved ? Response<BlockReasonDto>.Success(QuarantineMapping.BlockReason(reason)) : QuarantineSupport.Conflict<BlockReasonDto>();
    }

    private static Response<BlockReasonDto>? NamesRefusal(IReadOnlyDictionary<string, string>? names)
        => names is null || QuarantineContract.Languages.Any(l => string.IsNullOrWhiteSpace(names.GetValueOrDefault(l)))
           || names.Keys.Any(k => !QuarantineContract.Languages.Contains(k)) || names.Values.Any(v => v.Trim().Length > 100)
            ? QuarantineSupport.Fail<BlockReasonDto>(QuarantineErrorCodes.BlockReasonLanguageMissing,
                "names needs exactly the seven languages en, tr, fr, es, zh, ar, ru, each 1-100 characters.", 400)
            : null;

    private static Dictionary<string, string> Trimmed(IReadOnlyDictionary<string, string> names)
        => names.ToDictionary(n => n.Key, n => n.Value.Trim(), StringComparer.Ordinal);
}
