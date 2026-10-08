using System.Text.Json;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.ESignature.Services;
using Diten.Platform.Application.Features.TenantOrganization.Services;
using Diten.Platform.Domain.Entities.ESignature;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.ESignature.Handlers;

internal static class PolicyProjection
{
    public static PolicyDto ToDto(ESignaturePolicy policy, IReadOnlyList<ESignableAction> actions, string language,
        ESignaturePolicy? tenantDefault, bool tenantSampleApproval)
    {
        var byCode = actions.ToDictionary(a => a.ActionCode, StringComparer.Ordinal);
        var rows = policy.Rows.Select(row =>
        {
            byCode.TryGetValue(row.ActionCode, out var action);
            var name = action is null || action.Names.Count == 0 ? row.ActionCode : ESignatureLanguages.Pick(action.Names, language).Text;
            var tenantRow = tenantDefault?.Rows.FirstOrDefault(r => r.ActionCode == row.ActionCode);
            var differs = tenantDefault is not null && (tenantRow is null || !SameRow(tenantRow, row));
            return new PolicyRowDto(row.ActionCode, action?.Module ?? row.ActionCode.Split('.')[0], name, row.Required, row.MeaningCode,
                ESignatureWire.Name(row.VisualMode), ESignatureWire.Name(ESignatureSecondFactor.NotAvailable), differs);
        }).ToList();

        // CT-D15: SampleApprovalRequired is a tenant setting; an override echoes the tenant's value read-only.
        return new PolicyDto(ESignatureWire.Name(policy.Scope), policy.LegalEntityId, policy.Version, rows, policy.ReauthEvery,
            policy.ReauthWindowMinutes, tenantSampleApproval);
    }

    public static bool SameRow(ESignaturePolicyRow a, ESignaturePolicyRow b) =>
        a.Required == b.Required && a.MeaningCode == b.MeaningCode && a.VisualMode == b.VisualMode;

    /// <summary>Human-readable change lines (mockup <c>diffs()</c>), stored with the change row.</summary>
    public static List<string> Diff(ESignaturePolicy? before, ESignaturePolicy after)
    {
        var lines = new List<string>();
        if (before is null)
        {
            lines.Add($"{ESignatureWire.Name(after.Scope)} policy created");
        }
        else
        {
            if (before.ReauthEvery != after.ReauthEvery) lines.Add($"reauthEvery: {Bool(before.ReauthEvery)} → {Bool(after.ReauthEvery)}");
            if (before.ReauthWindowMinutes != after.ReauthWindowMinutes)
                lines.Add($"reauthWindowMinutes: {before.ReauthWindowMinutes} → {after.ReauthWindowMinutes}");
            if (before.SampleApprovalRequired != after.SampleApprovalRequired)
                lines.Add($"sampleApprovalRequired: {Bool(before.SampleApprovalRequired)} → {Bool(after.SampleApprovalRequired)}");
        }

        var beforeRows = before?.Rows.ToDictionary(r => r.ActionCode, StringComparer.Ordinal) ?? new Dictionary<string, ESignaturePolicyRow>();
        foreach (var row in after.Rows)
        {
            if (!beforeRows.TryGetValue(row.ActionCode, out var old))
            {
                if (before is not null) lines.Add($"{row.ActionCode}: added (required {Bool(row.Required)}, {Name(row.VisualMode)})");
                continue;
            }

            if (old.Required != row.Required) lines.Add($"{row.ActionCode}.required: {Bool(old.Required)} → {Bool(row.Required)}");
            if (old.MeaningCode != row.MeaningCode) lines.Add($"{row.ActionCode}.meaning: {old.MeaningCode ?? "-"} → {row.MeaningCode ?? "-"}");
            if (old.VisualMode != row.VisualMode) lines.Add($"{row.ActionCode}.visualMode: {Name(old.VisualMode)} → {Name(row.VisualMode)}");
        }

        foreach (var removed in beforeRows.Keys.Except(after.Rows.Select(r => r.ActionCode), StringComparer.Ordinal))
        {
            lines.Add($"{removed}: removed");
        }

        return lines;
    }

    private static string Bool(bool value) => value ? "true" : "false";
    private static string Name(ESignatureVisualMode mode) => ESignatureWire.Name(mode);
}

/// <summary>§4.14 / §4.16 history / §4.15 — reads.</summary>
public sealed class PolicyQueryHandlers :
    IRequestHandler<GetPolicyQuery, Response<PolicyViewDto>>,
    IRequestHandler<GetPolicyHistoryQuery, Response<IReadOnlyList<PolicyHistoryEntryDto>>>,
    IRequestHandler<GetEffectivePolicyQuery, Response<EffectivePolicyDto>>
{
    private readonly IESignActorAccessor _actors;
    private readonly IESignatureCatalog _catalog;
    private readonly IESignPolicyRepository _policies;
    private readonly IESignActionRepository _actions;
    private readonly IESignaturePolicyChangeRepository _changes;
    private readonly IESignMeaningRepository _meanings;
    private readonly ISignableSubjectRegistry _subjects;
    private readonly ILegalEntityReferenceValidator _legalEntities;

    public PolicyQueryHandlers(IESignActorAccessor actors, IESignatureCatalog catalog, IESignPolicyRepository policies, IESignActionRepository actions,
        IESignaturePolicyChangeRepository changes, IESignMeaningRepository meanings, ISignableSubjectRegistry subjects,
        ILegalEntityReferenceValidator legalEntities)
    {
        _actors = actors;
        _catalog = catalog;
        _policies = policies;
        _actions = actions;
        _changes = changes;
        _meanings = meanings;
        _subjects = subjects;
        _legalEntities = legalEntities;
    }

    public async Task<Response<PolicyViewDto>> Handle(GetPolicyQuery request, CancellationToken ct)
    {
        var actor = _actors.Current();
        if (actor is null)
        {
            return ESignResponses.Actor<PolicyViewDto>();
        }

        var tenantPolicy = await _catalog.GetTenantPolicyAsync(actor.TenantId, ct);
        var actions = await _actions.ListAsync(ct);
        var overrides = await _policies.ListOverridesAsync(actor.TenantId, ct);
        var selected = request.LegalEntityId is { } id ? overrides.FirstOrDefault(o => o.LegalEntityId == id) : null;

        var tenantDto = PolicyProjection.ToDto(tenantPolicy, actions, actor.Language, null, tenantPolicy.SampleApprovalRequired);
        var overrideDto = selected is null
            ? null
            : PolicyProjection.ToDto(selected, actions, actor.Language, tenantPolicy, tenantPolicy.SampleApprovalRequired);

        // The legal entities with an override (names from MDM). MDM owns the full list; the screen offers the others there.
        var legalEntities = new List<PolicyLegalEntityDto>();
        foreach (var policy in overrides)
        {
            var reference = await _legalEntities.ValidateAsync(policy.LegalEntityId!.Value, ct);
            var name = reference.Data?.DisplayName ?? reference.Data?.LegalName ?? policy.LegalEntityId.Value.ToString("D");
            legalEntities.Add(new PolicyLegalEntityDto(policy.LegalEntityId.Value, name, true));
        }

        return Response<PolicyViewDto>.Success(new PolicyViewDto(tenantDto, overrideDto, overrideDto ?? tenantDto, legalEntities));
    }

    public async Task<Response<IReadOnlyList<PolicyHistoryEntryDto>>> Handle(GetPolicyHistoryQuery request, CancellationToken ct)
    {
        var actor = _actors.Current();
        if (actor is null)
        {
            return ESignResponses.Actor<IReadOnlyList<PolicyHistoryEntryDto>>();
        }

        var rows = await _changes.ListAsync(actor.TenantId, request.LegalEntityId, ct);
        IReadOnlyList<PolicyHistoryEntryDto> items = rows
            .Select(c => new PolicyHistoryEntryDto(c.ChangedAtUtc, c.ChangedByName, ESignatureWire.Name(c.Scope), c.LegalEntityId, c.Changes, c.Reason))
            .ToList();
        return Response<IReadOnlyList<PolicyHistoryEntryDto>>.Success(items);
    }

    public async Task<Response<EffectivePolicyDto>> Handle(GetEffectivePolicyQuery request, CancellationToken ct)
    {
        var actor = _actors.Current();
        if (actor is null)
        {
            return ESignResponses.Actor<EffectivePolicyDto>();
        }

        var resolution = await _subjects.ResolveAsync(request.Module ?? "", request.SubjectType ?? "", request.SubjectId ?? "", ct);
        if (resolution.Outcome != SignableSubjectOutcome.Found)
        {
            return ESignResponses.Fail<EffectivePolicyDto>(ESignCodes.SubjectNotFound, 404, "The subject was not found.");
        }

        var subject = resolution.Subject!;
        await _catalog.EnsureMeaningsAsync(actor.TenantId, ct);
        var policy = await _catalog.GetEffectivePolicyAsync(actor.TenantId, subject.LegalEntityId, ct);
        var row = await _catalog.GetRowAsync(actor.TenantId, policy, request.ActionCode, ct);
        var meanings = await _meanings.ListAsync(actor.TenantId, activeOnly: true, ct);
        var allowed = row?.MeaningCode is { } fixedMeaning ? meanings.Where(m => m.Code == fixedMeaning).ToList() : meanings.ToList();

        return Response<EffectivePolicyDto>.Success(new EffectivePolicyDto(
            row?.Required ?? false,
            row?.MeaningCode,
            ESignatureWire.Name(row?.VisualMode ?? ESignatureVisualMode.Optional),
            policy.ReauthEvery,
            policy.ReauthWindowMinutes,
            allowed.Select(ESignatureMapping.ToDto).ToList(),
            new SignableSubjectDto(subject.Display, subject.Summary.Select(l => new SummaryLineDto(l.Label, l.Value)).ToList(),
                subject.LegalEntityId, subject.Fields),
            MySample: null, // E3 (samples) — no sample exists before it
            MyPendingSample: null,
            new SignerIdentityDto(actor.Login, actor.Name)));
    }
}

/// <summary>§4.16 — save the tenant default or a company override; revert an override. Reason ≥ 10, history + audit in one transaction.</summary>
public sealed class PolicyCommandHandlers :
    IRequestHandler<SavePolicyCommand, Response<PolicyDto>>,
    IRequestHandler<RevertLegalEntityPolicyCommand, Response<NoContent>>
{
    private readonly IESignActorAccessor _actors;
    private readonly IESignatureCatalog _catalog;
    private readonly IESignPolicyRepository _policies;
    private readonly IESignActionRepository _actions;
    private readonly IESignMeaningRepository _meanings;
    private readonly IESignaturePolicyChangeRepository _changes;
    private readonly ILegalEntityReferenceValidator _legalEntities;
    private readonly IPlatformTransactionExecutor _transactions;
    private readonly ITransactionalAuditOutboxWriter _audit;

    public PolicyCommandHandlers(IESignActorAccessor actors, IESignatureCatalog catalog, IESignPolicyRepository policies, IESignActionRepository actions,
        IESignMeaningRepository meanings, IESignaturePolicyChangeRepository changes, ILegalEntityReferenceValidator legalEntities,
        IPlatformTransactionExecutor transactions, ITransactionalAuditOutboxWriter audit)
    {
        _actors = actors;
        _catalog = catalog;
        _policies = policies;
        _actions = actions;
        _meanings = meanings;
        _changes = changes;
        _legalEntities = legalEntities;
        _transactions = transactions;
        _audit = audit;
    }

    public async Task<Response<PolicyDto>> Handle(SavePolicyCommand request, CancellationToken ct)
    {
        var actor = _actors.Current();
        if (actor is null)
        {
            return ESignResponses.Actor<PolicyDto>();
        }

        if (!ESignatureRules.ReasonOk(request.Reason))
        {
            return ESignResponses.Fail<PolicyDto>(ESignCodes.ReasonTooShort, 400, "The reason must be 10–1000 characters.");
        }

        if (request.ReauthWindowMinutes is < ESignaturePolicy.MinWindowMinutes or > ESignaturePolicy.MaxWindowMinutes)
        {
            return ESignResponses.Validation<PolicyDto>("reauthWindowMinutes must be 1–60.");
        }

        await _catalog.EnsureMeaningsAsync(actor.TenantId, ct);
        var actions = await _actions.ListAsync(ct);
        var rowsResult = await BuildRowsAsync(actor.TenantId, request.Rows ?? [], actions, ct);
        if (rowsResult.Failure is { } failure)
        {
            return failure;
        }

        var tenantPolicy = await _catalog.GetTenantPolicyAsync(actor.TenantId, ct);
        ESignaturePolicy? before;
        ESignaturePolicy after;
        if (request.LegalEntityId is { } legalEntityId)
        {
            var reference = await _legalEntities.ValidateAsync(legalEntityId, ct);
            if (!reference.IsSuccessful || reference.Data is not { Referenceable: true })
            {
                return ESignResponses.Fail<PolicyDto>(ESignCodes.NotFound, 404, "The legal entity is not in this tenant.");
            }

            before = await _policies.GetLegalEntityAsync(actor.TenantId, legalEntityId, ct);
            if (before is null ? request.ExpectedVersion is not null : request.ExpectedVersion != before.Version)
            {
                return ESignResponses.Fail<PolicyDto>(ESignCodes.PolicyVersionConflict, 409, "The policy was changed by someone else.");
            }

            after = before is null
                ? new ESignaturePolicy { TenantId = actor.TenantId, Scope = ESignaturePolicyScope.LegalEntity, LegalEntityId = legalEntityId, CreatedBy = actor.UserIdText }
                : Clone(before);
            after.SampleApprovalRequired = tenantPolicy.SampleApprovalRequired; // CT-D15: tenant-only, echoed
        }
        else
        {
            before = tenantPolicy;
            if (request.ExpectedVersion != before.Version)
            {
                return ESignResponses.Fail<PolicyDto>(ESignCodes.PolicyVersionConflict, 409, "The policy was changed by someone else.");
            }

            after = Clone(before);
            after.SampleApprovalRequired = request.SampleApprovalRequired ?? before.SampleApprovalRequired;
        }

        after.Rows = rowsResult.Rows!;
        after.ReauthEvery = request.ReauthEvery;
        after.ReauthWindowMinutes = request.ReauthWindowMinutes;
        after.UpdatedAt = DateTimeOffset.UtcNow;
        after.UpdatedBy = actor.UserIdText;
        var expected = before?.Version ?? 0;
        after.Version = before is null ? 1 : before.Version + 1;

        var change = new ESignaturePolicyChange
        {
            TenantId = actor.TenantId,
            CreatedBy = actor.UserIdText,
            Scope = after.Scope,
            LegalEntityId = after.LegalEntityId,
            Changes = PolicyProjection.Diff(before, after),
            BeforeJson = before is null ? null : JsonSerializer.Serialize(Snapshot(before)),
            AfterJson = JsonSerializer.Serialize(Snapshot(after)),
            Reason = request.Reason!.Trim(),
            ChangedBy = actor.UserIdText,
            ChangedByName = actor.Name,
            ChangedAtUtc = DateTime.UtcNow
        };

        try
        {
            var saved = await _transactions.ExecuteAsync(async (session, txCt) =>
            {
                var written = before is null
                    ? await _policies.InsertAsync(session, after, txCt)
                    : await _policies.ReplaceAsync(session, after, expected, txCt);
                if (!written)
                {
                    return false;
                }

                await _changes.InsertAsync(session, change, txCt);
                await ESignatureAuditIntent.EnqueueAsync(_audit, session, actor.TenantId, ESignAuditEvents.PolicyChanged, AuditOperation.Update,
                    "ESignaturePolicy", after.Id, AuditActorType.TenantUser, actor.UserId,
                    new Dictionary<string, object?>
                    {
                        ["scope"] = ESignatureWire.Name(after.Scope),
                        ["legalEntityId"] = after.LegalEntityId,
                        ["version"] = after.Version,
                        ["changes"] = change.Changes.Count,
                        ["changeId"] = change.Id
                    }, txCt);
                return true;
            }, ct);
            if (!saved)
            {
                return ESignResponses.Fail<PolicyDto>(ESignCodes.PolicyVersionConflict, 409, "The policy was changed by someone else.");
            }
        }
        catch (ESignatureDuplicateException)
        {
            // A parallel first save created the company override meanwhile: the caller's expectedVersion (null) is stale.
            return ESignResponses.Fail<PolicyDto>(ESignCodes.PolicyVersionConflict, 409, "The policy was changed by someone else.");
        }
        catch (PlatformTransactionUnavailableException)
        {
            return ESignResponses.Fail<PolicyDto>(ESignCodes.ReauthUnavailable, 503, "The policy could not be saved now.");
        }

        var tenantForDiff = after.Scope == ESignaturePolicyScope.LegalEntity ? tenantPolicy : null;
        return Response<PolicyDto>.Success(PolicyProjection.ToDto(after, actions, actor.Language, tenantForDiff,
            after.Scope == ESignaturePolicyScope.Tenant ? after.SampleApprovalRequired : tenantPolicy.SampleApprovalRequired));
    }

    public async Task<Response<NoContent>> Handle(RevertLegalEntityPolicyCommand request, CancellationToken ct)
    {
        var actor = _actors.Current();
        if (actor is null)
        {
            return ESignResponses.Actor<NoContent>();
        }

        if (!ESignatureRules.ReasonOk(request.Reason))
        {
            return ESignResponses.Fail<NoContent>(ESignCodes.ReasonTooShort, 400, "The reason must be 10–1000 characters.");
        }

        var existing = await _policies.GetLegalEntityAsync(actor.TenantId, request.LegalEntityId, ct);
        if (existing is null)
        {
            return ESignResponses.Fail<NoContent>(ESignCodes.NotFound, 404, "This legal entity has no policy override.");
        }

        var change = new ESignaturePolicyChange
        {
            TenantId = actor.TenantId,
            CreatedBy = actor.UserIdText,
            Scope = ESignaturePolicyScope.LegalEntity,
            LegalEntityId = request.LegalEntityId,
            Changes = ["override reverted to the tenant default"],
            BeforeJson = JsonSerializer.Serialize(Snapshot(existing)),
            AfterJson = null,
            Reason = request.Reason!.Trim(),
            ChangedBy = actor.UserIdText,
            ChangedByName = actor.Name,
            ChangedAtUtc = DateTime.UtcNow
        };

        try
        {
            var removed = await _transactions.ExecuteAsync(async (session, txCt) =>
            {
                if (!await _policies.DeleteOverrideAsync(session, actor.TenantId, request.LegalEntityId, existing.Version, txCt))
                {
                    return false;
                }

                await _changes.InsertAsync(session, change, txCt);
                await ESignatureAuditIntent.EnqueueAsync(_audit, session, actor.TenantId, ESignAuditEvents.PolicyOverrideReverted, AuditOperation.Delete,
                    "ESignaturePolicy", existing.Id, AuditActorType.TenantUser, actor.UserId,
                    new Dictionary<string, object?> { ["legalEntityId"] = request.LegalEntityId, ["changeId"] = change.Id }, txCt);
                return true;
            }, ct);
            if (!removed)
            {
                return ESignResponses.Fail<NoContent>(ESignCodes.PolicyVersionConflict, 409, "The policy was changed by someone else.");
            }
        }
        catch (PlatformTransactionUnavailableException)
        {
            return ESignResponses.Fail<NoContent>(ESignCodes.ReauthUnavailable, 503, "The policy could not be reverted now.");
        }

        return Response<NoContent>.Success(204);
    }

    private async Task<(List<ESignaturePolicyRow>? Rows, Response<PolicyDto>? Failure)> BuildRowsAsync(
        Guid tenantId, IReadOnlyList<PolicyRowInput> inputs, IReadOnlyList<ESignableAction> actions, CancellationToken ct)
    {
        var known = actions.Select(a => a.ActionCode).ToHashSet(StringComparer.Ordinal);
        var rows = new List<ESignaturePolicyRow>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var input in inputs)
        {
            if (string.IsNullOrWhiteSpace(input.ActionCode) || !known.Contains(input.ActionCode) || !seen.Add(input.ActionCode))
            {
                return (null, ESignResponses.Validation<PolicyDto>($"Unknown or repeated action '{input.ActionCode}'."));
            }

            // KS-11 / V17: the second factor cannot be enabled in v1.
            if (!string.IsNullOrWhiteSpace(input.SecondFactor) && !string.Equals(input.SecondFactor, "notAvailable", StringComparison.OrdinalIgnoreCase))
            {
                return (null, ESignResponses.Fail<PolicyDto>(ESignCodes.SecondFactorNotAvailable, 400, "A second factor is not available yet."));
            }

            if (!ESignatureWire.TryParse<ESignatureVisualMode>(input.VisualMode, out var visualMode))
            {
                return (null, ESignResponses.Validation<PolicyDto>($"visualMode of '{input.ActionCode}' must be none, optional, savedAccepted or drawRequired."));
            }

            if (!string.IsNullOrWhiteSpace(input.MeaningCode))
            {
                var meaning = await _meanings.GetAsync(tenantId, input.MeaningCode, ct);
                if (meaning is null || !meaning.IsActive)
                {
                    return (null, ESignResponses.Validation<PolicyDto>($"Unknown or inactive meaning '{input.MeaningCode}'."));
                }
            }

            rows.Add(new ESignaturePolicyRow
            {
                ActionCode = input.ActionCode,
                Required = input.Required,
                MeaningCode = string.IsNullOrWhiteSpace(input.MeaningCode) ? null : input.MeaningCode,
                VisualMode = visualMode,
                SecondFactor = ESignatureSecondFactor.NotAvailable
            });
        }

        return (rows, null);
    }

    private static ESignaturePolicy Clone(ESignaturePolicy source) => new()
    {
        Id = source.Id,
        TenantId = source.TenantId,
        CreatedAt = source.CreatedAt,
        CreatedBy = source.CreatedBy,
        Scope = source.Scope,
        LegalEntityId = source.LegalEntityId,
        Rows = source.Rows.Select(r => new ESignaturePolicyRow
        {
            ActionCode = r.ActionCode, Required = r.Required, MeaningCode = r.MeaningCode, VisualMode = r.VisualMode, SecondFactor = r.SecondFactor
        }).ToList(),
        ReauthEvery = source.ReauthEvery,
        ReauthWindowMinutes = source.ReauthWindowMinutes,
        SampleApprovalRequired = source.SampleApprovalRequired,
        Version = source.Version
    };

    private static object Snapshot(ESignaturePolicy p) => new
    {
        scope = ESignatureWire.Name(p.Scope),
        p.LegalEntityId,
        p.Version,
        p.ReauthEvery,
        p.ReauthWindowMinutes,
        p.SampleApprovalRequired,
        rows = p.Rows.Select(r => new { r.ActionCode, r.Required, r.MeaningCode, visualMode = ESignatureWire.Name(r.VisualMode) })
    };
}
