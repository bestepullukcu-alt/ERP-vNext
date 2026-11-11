using System.Globalization;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Recall.Commands;
using Diten.InventoryService.Application.Features.Recall.Ports;
using Diten.InventoryService.Application.Features.Recall.Queries;
using Diten.InventoryService.Application.Features.Recall.Services;
using Diten.InventoryService.Domain.Recall.Entities;
using Diten.InventoryService.Domain.Recall.Repositories;
using Diten.InventoryService.Domain.Recall.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.Recall.Handlers;

/// <summary>§4.20 getSettings — the company's row, or the KR-G defaults at version 0 (<c>isDefault: true</c>).</summary>
public sealed class GetRecallSettingsHandler : IRequestHandler<GetRecallSettingsQuery, Response<RecallSettingsDto>>
{
    private readonly IRecallRepository _repository;
    private readonly RecallSupport _support;

    public GetRecallSettingsHandler(IRecallRepository repository, RecallSupport support)
    {
        _repository = repository;
        _support = support;
    }

    public async Task<Response<RecallSettingsDto>> Handle(GetRecallSettingsQuery request, CancellationToken ct)
    {
        var stored = await _repository.FindSettingsAsync(ct);
        return Response<RecallSettingsDto>.Success(RecallMapping.Settings(stored ?? RecallSettingsRules.Defaults(), _support.LegalEntityId, stored is null));
    }
}

/// <summary>§4.21 getSettingsHistory — one row per changed field of every save, newest first.</summary>
public sealed class GetRecallSettingsHistoryHandler : IRequestHandler<GetRecallSettingsHistoryQuery, Response<RecallSettingsHistoryPageDto>>
{
    private readonly IRecallRepository _repository;

    public GetRecallSettingsHistoryHandler(IRecallRepository repository) => _repository = repository;

    public async Task<Response<RecallSettingsHistoryPageDto>> Handle(GetRecallSettingsHistoryQuery request, CancellationToken ct)
    {
        if (RecallPaging.Parse(request.Page, request.PageSize) is not { } paging)
        {
            return RecallSupport.Invalid<RecallSettingsHistoryPageDto>($"page must be ≥ 1 and pageSize 1-{RecallContract.MaxPageSize}.");
        }

        var result = await _repository.ListSettingsHistoryAsync(paging.Page, paging.PageSize, ct);
        return Response<RecallSettingsHistoryPageDto>.Success(new RecallSettingsHistoryPageDto(
            result.Items.Select(RecallMapping.History).ToList(), result.Total, paging.Page, paging.PageSize));
    }
}

/// <summary>
/// §4.59 updateSettings — PUT, full replacement (pack §4.14, KR-G): ranges, the three class targets, reason ≥ 10, If-Match (<c>"0"</c>
/// creates the company's first row). Nothing changed → 409 <c>RECALL_NO_CHANGES</c>. Signature by the MOD-0022 policy of
/// <c>recall.settings.update</c> (CT-R16: not required by default; a sent signature is verified — fail-closed until R4). In ONE
/// transaction: the row, one history row per changed field, one audit row <c>recall.settings.updated</c>. The same key + the same body
/// answers the current settings (200); the same key + another body → 400 <c>IDEMPOTENCY_KEY_REUSED</c>.
/// </summary>
public sealed class UpdateRecallSettingsHandler : IRequestHandler<UpdateRecallSettingsCommand, Response<RecallSettingsDto>>
{
    private readonly IRecallRepository _repository;
    private readonly IRecallSignature _signature;
    private readonly RecallSupport _support;

    public UpdateRecallSettingsHandler(IRecallRepository repository, IRecallSignature signature, RecallSupport support)
    {
        _repository = repository;
        _signature = signature;
        _support = support;
    }

    public async Task<Response<RecallSettingsDto>> Handle(UpdateRecallSettingsCommand request, CancellationToken ct)
    {
        var input = request.Settings;
        var reason = request.Reason?.Trim();
        var signatureId = RecallSupport.Trimmed(request.SignatureId);
        var fingerprint = RecallSupport.Fingerprint(
            ("drillTargetHours", I(input.DrillTargetHours)), ("drillFrequencyWarningDays", I(input.DrillFrequencyWarningDays)),
            ("effectivenessTargetPercent", string.Join(",", input.EffectivenessTargetPercent.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => $"{p.Key}:{RecallSupport.D(p.Value)}"))),
            ("traceMaxDepth", I(input.TraceMaxDepth)), ("traceMaxNodes", I(input.TraceMaxNodes)), ("reason", reason), ("signatureId", signatureId));
        var proposal = RecallSettingsProposal.Fingerprint(input, reason); // R4: the same body without the signature (what the prepared change holds)

        if (await _repository.FindSettingsSaveAsync(_support.UserId, request.IdempotencyKey, ct) is { } earlier)
        {
            return earlier.Detail.GetValueOrDefault("fingerprint") == fingerprint ? await CurrentAsync(ct) : RecallSupport.KeyReused<RecallSettingsDto>();
        }

        if (RecallCaseRules.Reason(reason) is { } reasonRefusal) return RecallSupport.Fail<RecallSettingsDto>(reasonRefusal);

        var stored = await _repository.FindSettingsAsync(ct);
        var before = stored ?? RecallSettingsRules.Defaults();
        var after = RecallSettingsProposal.Apply(before, input);
        if (RecallSettingsRules.Validate(after) is { } invalid) return RecallSupport.Fail<RecallSettingsDto>(invalid);
        if (before.Version != request.ExpectedVersion) return RecallSupport.Conflict<RecallSettingsDto>();

        var changes = RecallSettingsRules.Changes(before, after);
        if (changes.Count == 0) return RecallSupport.Fail<RecallSettingsDto>(RecallErrorCodes.NoChanges, "The settings are unchanged.", 409);

        // CT-R16 + R4: by the MOD-0022 policy row (default: not required). A signature binds ONLY to the caller's prepared change
        // (prepareSettingsChange, the subject recall.settings-change / rs-v1) of THIS proposal on THIS version, unexpired — anything else is 409.
        var now = _support.Now();
        var pending = await _repository.LatestSettingsChangeAsync(_support.UserId, ct);
        var requirement = await _signature.RequirementAsync(RecallContract.SettingsUpdateAction, pending?.Id ?? _support.LegalEntityId, ct);
        if (requirement.Required && signatureId is null) return RecallSupport.SignatureRequired<RecallSettingsDto>();
        if (signatureId is not null)
        {
            if (pending is null || pending.ExpiresAtUtc <= now || pending.SettingsVersion != before.Version
                || !string.Equals(pending.ProposalFingerprint, proposal, StringComparison.Ordinal))
            {
                return RecallSupport.SignatureMismatch<RecallSettingsDto>();
            }

            if (!await _signature.VerifyAsync(new RecallSignatureVerification(signatureId, RecallContract.SettingsUpdateAction, requirement.MeaningCode,
                    _support.UserId, RecallContract.SettingsSubjectType, pending.Id, pending.SetAtUtc), ct))
            {
                return RecallSupport.SignatureMismatch<RecallSettingsDto>();
            }
        }

        if (stored is null)
        {
            after.Id = Guid.NewGuid();
            after.CreatedAt = now;
        }

        after.UpdatedAt = now;
        after.UpdatedBy = _support.UserId;
        after.Version = request.ExpectedVersion + 1;

        var history = changes.Select(c => new RecallSettingsHistoryEntry
        {
            Id = Guid.NewGuid(),
            CreatedAt = now,
            Field = c.Field,
            OldValue = c.Old,
            NewValue = c.New,
            Reason = reason!,
            ChangedBy = _support.UserId,
            ChangedAtUtc = now,
            SettingsVersion = after.Version,
            SignatureId = signatureId,
            CorrelationId = _support.CorrelationId
        }).ToList();
        var audit = _support.Audit(RecallContract.AuditSettingsUpdated, RecallContract.SubjectSettings, after.Id, null, now, request.IdempotencyKey,
            new Dictionary<string, string?>
            {
                ["fields"] = string.Join(",", changes.Select(c => c.Field)),
                ["reason"] = reason,
                ["version"] = I(after.Version),
                ["signatureId"] = signatureId,
                ["fingerprint"] = fingerprint
            });

        bool saved;
        try
        {
            saved = await _repository.InTransactionAsync(async (session, c) =>
            {
                if (!await session.SaveSettingsAsync(after, request.ExpectedVersion, c)) return false;
                await session.InsertSettingsHistoryAsync(history, c);
                await session.InsertAuditAsync(audit, c);
                return true;
            }, ct);
        }
        catch (RecallDuplicateException)
        {
            saved = false; // two concurrent first saves of the company: the unique (tenant, company) row admits one
        }

        return saved
            ? Response<RecallSettingsDto>.Success(RecallMapping.Settings(after, _support.LegalEntityId, false))
            : RecallSupport.Conflict<RecallSettingsDto>();
    }

    private async Task<Response<RecallSettingsDto>> CurrentAsync(CancellationToken ct)
    {
        var stored = await _repository.FindSettingsAsync(ct);
        return Response<RecallSettingsDto>.Success(RecallMapping.Settings(stored ?? RecallSettingsRules.Defaults(), _support.LegalEntityId, stored is null));
    }

    private static string I(int value) => value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// WP-0177-R4 — one settings proposal (the PUT / prepare body without key or signature): applied to the current row the same way by
/// <c>updateSettings</c> and <c>prepareSettingsChange</c>, and fingerprinted so a signed save is the SAME proposal that was prepared (CT-R16).
/// </summary>
public static class RecallSettingsProposal
{
    public static RecallSettings Apply(RecallSettings before, RecallSettingsInput input)
    {
        var after = before.Copy();
        after.DrillTargetHours = input.DrillTargetHours;
        after.DrillFrequencyWarningDays = input.DrillFrequencyWarningDays;
        after.EffectivenessTargetPercent = new Dictionary<string, decimal>(input.EffectivenessTargetPercent, StringComparer.Ordinal);
        after.TraceMaxDepth = input.TraceMaxDepth;
        after.TraceMaxNodes = input.TraceMaxNodes;
        return after;
    }

    public static string Fingerprint(RecallSettingsInput input, string? reason) => RecallSupport.Fingerprint(
        ("drillTargetHours", I(input.DrillTargetHours)), ("drillFrequencyWarningDays", I(input.DrillFrequencyWarningDays)),
        ("effectivenessTargetPercent", string.Join(",", input.EffectivenessTargetPercent.OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => $"{p.Key}:{RecallSupport.D(p.Value)}"))),
        ("traceMaxDepth", I(input.TraceMaxDepth)), ("traceMaxNodes", I(input.TraceMaxNodes)), ("reason", reason));

    private static string I(int value) => value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// R4 prepareSettingsChange (RECALL 1.3.0, CT-R16) — the same checks as <c>updateSettings</c> (reason ≥ 10, ranges, If-Match, something
/// changes), then the proposal is kept 15 minutes as a <see cref="RecallSettingsChange"/> of the caller: the MOD-0022 subject
/// <c>recall.settings-change</c> (<c>rs-v1</c>). In ONE transaction: the change + one audit row <c>recall.settings.change-prepared</c> (product
/// trail; the central audit forwarder comes with BL-501 / WP-MVP1-AUDIT1). The same key + the same body answers the same preparation (200);
/// another body → 400 <c>IDEMPOTENCY_KEY_REUSED</c>. The answer is the RECALL <c>PendingActionResponse</c>: <c>caseId</c> carries the change id.
/// </summary>
public sealed class PrepareRecallSettingsChangeHandler : IRequestHandler<PrepareRecallSettingsChangeCommand, Response<RecallPendingActionResponseDto>>
{
    public const string ActionName = "UpdateSettings";

    private readonly IRecallRepository _repository;
    private readonly IRecallSignature _signature;
    private readonly RecallSupport _support;

    public PrepareRecallSettingsChangeHandler(IRecallRepository repository, IRecallSignature signature, RecallSupport support)
    {
        _repository = repository;
        _signature = signature;
        _support = support;
    }

    public async Task<Response<RecallPendingActionResponseDto>> Handle(PrepareRecallSettingsChangeCommand request, CancellationToken ct)
    {
        var input = request.Settings;
        var reason = request.Reason?.Trim();
        var proposal = RecallSettingsProposal.Fingerprint(input, reason);
        var fingerprint = RecallSupport.Fingerprint(("proposal", proposal), ("version", request.ExpectedVersion.ToString(CultureInfo.InvariantCulture)));
        if (await _repository.FindSettingsChangeByKeyAsync(_support.UserId, request.IdempotencyKey, ct) is { } earlier)
        {
            return earlier.ProposalFingerprint == proposal && earlier.SettingsVersion == request.ExpectedVersion
                ? await PreparedAsync(earlier, ct)
                : RecallSupport.KeyReused<RecallPendingActionResponseDto>();
        }

        if (RecallCaseRules.Reason(reason) is { } reasonRefusal) return RecallSupport.Fail<RecallPendingActionResponseDto>(reasonRefusal);

        var before = await _repository.FindSettingsAsync(ct) ?? RecallSettingsRules.Defaults();
        var after = RecallSettingsProposal.Apply(before, input);
        if (RecallSettingsRules.Validate(after) is { } invalid) return RecallSupport.Fail<RecallPendingActionResponseDto>(invalid);
        if (before.Version != request.ExpectedVersion) return RecallSupport.Conflict<RecallPendingActionResponseDto>();
        var changes = RecallSettingsRules.Changes(before, after);
        if (changes.Count == 0) return RecallSupport.Fail<RecallPendingActionResponseDto>(RecallErrorCodes.NoChanges, "The settings are unchanged.", 409);

        var now = _support.Now();
        var change = new RecallSettingsChange
        {
            Id = Guid.NewGuid(),
            CreatedAt = now,
            SettingsVersion = before.Version,
            DrillTargetHours = after.DrillTargetHours,
            DrillFrequencyWarningDays = after.DrillFrequencyWarningDays,
            EffectivenessTargetPercent = new Dictionary<string, decimal>(after.EffectivenessTargetPercent, StringComparer.Ordinal),
            TraceMaxDepth = after.TraceMaxDepth,
            TraceMaxNodes = after.TraceMaxNodes,
            Reason = reason!,
            Changes = changes.Select(c => new RecallSettingsFieldChange { Field = c.Field, Old = c.Old, New = c.New }).ToList(),
            SetBy = _support.UserId,
            SetAtUtc = now,
            ExpiresAtUtc = now + RecallCaseRules.PendingActionLifetime,
            IdempotencyKey = request.IdempotencyKey,
            ProposalFingerprint = proposal
        };
        var audit = _support.Audit(RecallContract.AuditSettingsChangePrepared, RecallContract.SubjectSettings, change.Id, null, now, request.IdempotencyKey,
            new Dictionary<string, string?>
            {
                ["fields"] = string.Join(",", changes.Select(c => c.Field)),
                ["settingsVersion"] = change.SettingsVersion.ToString(CultureInfo.InvariantCulture),
                ["expiresAtUtc"] = RecallSupport.Wire(change.ExpiresAtUtc),
                ["fingerprint"] = fingerprint
            });

        try
        {
            await _repository.InTransactionAsync(async (session, c) =>
            {
                await session.InsertSettingsChangeAsync(change, c);
                await session.InsertAuditAsync(audit, c);
                return true;
            }, ct);
        }
        catch (RecallDuplicateException)
        {
            // the same key twice at once: the first preparation stands
            return await _repository.FindSettingsChangeByKeyAsync(_support.UserId, request.IdempotencyKey, ct) is { } first && first.ProposalFingerprint == proposal
                ? await PreparedAsync(first, ct)
                : RecallSupport.KeyReused<RecallPendingActionResponseDto>();
        }

        return await PreparedAsync(change, ct);
    }

    private async Task<Response<RecallPendingActionResponseDto>> PreparedAsync(RecallSettingsChange change, CancellationToken ct)
    {
        var requirement = await _signature.RequirementAsync(RecallContract.SettingsUpdateAction, change.Id, ct);
        return Response<RecallPendingActionResponseDto>.Success(new RecallPendingActionResponseDto(
            change.Id.ToString("D"), ActionName, requirement.Required, RecallContract.SettingsUpdateAction, requirement.MeaningCode, requirement.VisualMode,
            new RecallSubjectRefDto(RecallContract.ESignModule, RecallContract.SettingsSubjectType, change.Id.ToString("D")), RecallSupport.Wire(change.ExpiresAtUtc),
            change.SettingsVersion));
    }
}
