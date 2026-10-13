using System.Text.RegularExpressions;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.ESignature.Services;
using Diten.Platform.Domain.Entities.ESignature;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.ESignature.Handlers;

/// <summary>§4.20 / §4.21 — tenant signature meanings (KS-12): seven names each; deactivation refused while a policy row uses it.</summary>
public sealed class MeaningHandlers :
    IRequestHandler<ListMeaningsQuery, Response<IReadOnlyList<MeaningDto>>>,
    IRequestHandler<CreateMeaningCommand, Response<MeaningDto>>,
    IRequestHandler<UpdateMeaningCommand, Response<MeaningDto>>,
    IRequestHandler<SetMeaningActiveCommand, Response<MeaningDto>>
{
    private const int MaxNameLength = 120;
    private const int MaxDescriptionLength = 500;

    private readonly IESignActorAccessor _actors;
    private readonly IESignatureCatalog _catalog;
    private readonly IESignMeaningRepository _meanings;
    private readonly IESignPolicyRepository _policies;
    private readonly IPlatformTransactionExecutor _transactions;
    private readonly ITransactionalAuditOutboxWriter _audit;

    public MeaningHandlers(IESignActorAccessor actors, IESignatureCatalog catalog, IESignMeaningRepository meanings, IESignPolicyRepository policies,
        IPlatformTransactionExecutor transactions, ITransactionalAuditOutboxWriter audit)
    {
        _actors = actors;
        _catalog = catalog;
        _meanings = meanings;
        _policies = policies;
        _transactions = transactions;
        _audit = audit;
    }

    public async Task<Response<IReadOnlyList<MeaningDto>>> Handle(ListMeaningsQuery request, CancellationToken ct)
    {
        var actor = _actors.Current();
        if (actor is null)
        {
            return ESignResponses.Actor<IReadOnlyList<MeaningDto>>();
        }

        await _catalog.EnsureMeaningsAsync(actor.TenantId, ct);
        var meanings = await _meanings.ListAsync(actor.TenantId, request.ActiveOnly, ct);
        IReadOnlyList<MeaningDto> items = meanings.Select(ESignatureMapping.ToDto).ToList();
        return Response<IReadOnlyList<MeaningDto>>.Success(items);
    }

    public async Task<Response<MeaningDto>> Handle(CreateMeaningCommand request, CancellationToken ct)
    {
        var actor = _actors.Current();
        if (actor is null)
        {
            return ESignResponses.Actor<MeaningDto>();
        }

        if (string.IsNullOrWhiteSpace(request.Code) || !ESignatureMeaning.CodePattern.IsMatch(request.Code))
        {
            return ESignResponses.Validation<MeaningDto>("code must match ^[A-Z][A-Z0-9_]{1,31}$.");
        }

        if (Invalid(request.Names, request.Description) is { } invalid)
        {
            return invalid;
        }

        await _catalog.EnsureMeaningsAsync(actor.TenantId, ct);
        var meaning = new ESignatureMeaning
        {
            TenantId = actor.TenantId,
            CreatedBy = actor.UserIdText,
            Code = request.Code,
            Names = Normalize(request.Names!),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            IsActive = true
        };

        var created = await WriteAsync(actor, meaning, ESignAuditEvents.MeaningCreated, AuditOperation.Create,
            (session, txCt) => _meanings.InsertAsync(session, meaning, txCt), ct);
        if (created.Failure is { } failure)
        {
            return failure;
        }

        return created.Written
            ? Response<MeaningDto>.Success(ESignatureMapping.ToDto(meaning), 201)
            : ESignResponses.Fail<MeaningDto>(ESignCodes.MeaningCodeExists, 409, "A meaning with this code already exists.");
    }

    public async Task<Response<MeaningDto>> Handle(UpdateMeaningCommand request, CancellationToken ct)
    {
        var actor = _actors.Current();
        if (actor is null)
        {
            return ESignResponses.Actor<MeaningDto>();
        }

        if (Invalid(request.Names, request.Description) is { } invalid)
        {
            return invalid;
        }

        var meaning = await _meanings.GetAsync(actor.TenantId, request.Code, ct);
        if (meaning is null)
        {
            return ESignResponses.Fail<MeaningDto>(ESignCodes.NotFound, 404, "Meaning not found.");
        }

        // The code is the path and never changes (V18). Names change for NEW signatures only: records keep MeaningTextAtSign.
        var expected = meaning.Version;
        meaning.Names = Normalize(request.Names!);
        meaning.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        return await ReplaceAsync(actor, meaning, expected, ESignAuditEvents.MeaningUpdated, AuditOperation.Update, ct);
    }

    public async Task<Response<MeaningDto>> Handle(SetMeaningActiveCommand request, CancellationToken ct)
    {
        var actor = _actors.Current();
        if (actor is null)
        {
            return ESignResponses.Actor<MeaningDto>();
        }

        var meaning = await _meanings.GetAsync(actor.TenantId, request.Code, ct);
        if (meaning is null)
        {
            return ESignResponses.Fail<MeaningDto>(ESignCodes.NotFound, 404, "Meaning not found.");
        }

        if (!request.Active && await _policies.AnyRowUsesMeaningAsync(actor.TenantId, meaning.Code, ct))
        {
            return ESignResponses.Fail<MeaningDto>(ESignCodes.MeaningInUse, 409, "A policy row uses this meaning; change the policy first.");
        }

        if (meaning.IsActive == request.Active)
        {
            return Response<MeaningDto>.Success(ESignatureMapping.ToDto(meaning));
        }

        var expected = meaning.Version;
        meaning.IsActive = request.Active;
        return await ReplaceAsync(actor, meaning, expected,
            request.Active ? ESignAuditEvents.MeaningActivated : ESignAuditEvents.MeaningDeactivated,
            request.Active ? AuditOperation.Activate : AuditOperation.Deactivate, ct);
    }

    private async Task<Response<MeaningDto>> ReplaceAsync(ESignActor actor, ESignatureMeaning meaning, int expected, string eventName,
        AuditOperation operation, CancellationToken ct)
    {
        meaning.Version = expected + 1;
        meaning.UpdatedAt = DateTimeOffset.UtcNow;
        meaning.UpdatedBy = actor.UserIdText;
        var replaced = await WriteAsync(actor, meaning, eventName, operation,
            (session, txCt) => _meanings.ReplaceAsync(session, meaning, expected, txCt), ct);
        if (replaced.Failure is { } failure)
        {
            return failure;
        }

        return replaced.Written
            ? Response<MeaningDto>.Success(ESignatureMapping.ToDto(meaning))
            : ESignResponses.Fail<MeaningDto>(ESignCodes.PolicyVersionConflict, 409, "The meaning was changed by someone else.");
    }

    private async Task<(bool Written, Response<MeaningDto>? Failure)> WriteAsync(ESignActor actor, ESignatureMeaning meaning, string eventName,
        AuditOperation operation, Func<IPlatformTransactionSession, CancellationToken, Task<bool>> write, CancellationToken ct)
    {
        try
        {
            var written = await _transactions.ExecuteAsync(async (session, txCt) =>
            {
                if (!await write(session, txCt))
                {
                    return false;
                }

                await ESignatureAuditIntent.EnqueueAsync(_audit, session, actor.TenantId, eventName, operation, "ESignatureMeaning", meaning.Id,
                    AuditActorType.TenantUser, actor.UserId,
                    new Dictionary<string, object?> { ["code"] = meaning.Code, ["isActive"] = meaning.IsActive }, txCt);
                return true;
            }, ct);
            return (written, null);
        }
        catch (ESignatureDuplicateException)
        {
            return (false, null); // create: the code exists (CODE_EXISTS); the transaction was aborted
        }
        catch (PlatformTransactionUnavailableException)
        {
            return (false, ESignResponses.Fail<MeaningDto>(ESignCodes.ReauthUnavailable, 503, "The meaning could not be saved now."));
        }
    }

    /// <summary>V19: all seven names, non-empty; 400 lists the missing languages.</summary>
    private static Response<MeaningDto>? Invalid(IReadOnlyDictionary<string, string>? names, string? description)
    {
        var missing = ESignatureLanguages.Missing(names);
        if (missing.Count > 0)
        {
            return Response<MeaningDto>.Fail(missing.Select(language => $"missing:{language}").ToList(), 400, ESignCodes.MeaningLanguageMissing);
        }

        if (names!.Values.Any(v => v.Trim().Length > MaxNameLength) || description is { Length: > MaxDescriptionLength })
        {
            return ESignResponses.Validation<MeaningDto>($"names at most {MaxNameLength} characters, description at most {MaxDescriptionLength}.");
        }

        return null;
    }

    private static Dictionary<string, string> Normalize(IReadOnlyDictionary<string, string> names) =>
        ESignatureLanguages.All.ToDictionary(language => language, language => names[language].Trim());
}

/// <summary>§4.22 / §4.23 — the global signable-action catalog (CT-D12).</summary>
public sealed class SignableActionHandlers :
    IRequestHandler<ListSignableActionsQuery, Response<IReadOnlyList<SignableActionDto>>>,
    IRequestHandler<RegisterSignableActionsCommand, Response<RegisterActionsResultDto>>
{
    private static readonly Regex ModulePattern = new("^[a-z][a-z0-9-]{1,40}$", RegexOptions.Compiled);
    private static readonly Regex ActionPattern = new("^[a-z][a-z0-9-]*(\\.[a-z][a-z0-9-]*){1,4}$", RegexOptions.Compiled);

    private readonly IESignActionRepository _actions;
    private readonly IPlatformTransactionExecutor _transactions;
    private readonly IAuditService _audit;

    public SignableActionHandlers(IESignActionRepository actions, IPlatformTransactionExecutor transactions, IAuditService audit)
    {
        _actions = actions;
        _transactions = transactions;
        _audit = audit;
    }

    public async Task<Response<IReadOnlyList<SignableActionDto>>> Handle(ListSignableActionsQuery request, CancellationToken ct)
    {
        var actions = await _actions.ListAsync(ct);
        IReadOnlyList<SignableActionDto> items = actions.Select(ESignatureMapping.ToDto).ToList();
        return Response<IReadOnlyList<SignableActionDto>>.Success(items);
    }

    public async Task<Response<RegisterActionsResultDto>> Handle(RegisterSignableActionsCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Module) || !ModulePattern.IsMatch(request.Module) || request.Actions is not { Count: > 0 and <= 200 })
        {
            return ESignResponses.Validation<RegisterActionsResultDto>("module and 1–200 actions are required.");
        }

        var now = DateTime.UtcNow;
        var actions = new List<ESignableAction>();
        foreach (var input in request.Actions)
        {
            if (string.IsNullOrWhiteSpace(input.ActionCode) || !ActionPattern.IsMatch(input.ActionCode))
            {
                return ESignResponses.Validation<RegisterActionsResultDto>($"actionCode '{input.ActionCode}' is not a lowercase dotted code.");
            }

            // A module registers only under its own prefix — never another module's actions (CT-D12).
            if (!input.ActionCode.StartsWith(request.Module + ".", StringComparison.Ordinal))
            {
                return ESignResponses.Fail<RegisterActionsResultDto>(ESignCodes.ActionPrefixMismatch, 409,
                    $"'{input.ActionCode}' is outside the module's own '{request.Module}.' prefix.");
            }

            var missing = ESignatureLanguages.Missing(input.Names);
            if (missing.Count > 0)
            {
                return Response<RegisterActionsResultDto>.Fail(missing.Select(l => $"missing:{l}").ToList(), 400, ESignCodes.MeaningLanguageMissing);
            }

            if (!ESignatureWire.TryParse<ESignatureVisualMode>(input.DefaultVisualMode ?? "optional", out var visualMode))
            {
                return ESignResponses.Validation<RegisterActionsResultDto>($"defaultVisualMode of '{input.ActionCode}' is not a visual mode.");
            }

            actions.Add(new ESignableAction
            {
                ActionCode = input.ActionCode,
                Module = request.Module,
                Names = ESignatureLanguages.All.ToDictionary(l => l, l => input.Names![l].Trim()),
                DefaultRequired = input.DefaultRequired,
                DefaultMeaningCode = string.IsNullOrWhiteSpace(input.DefaultMeaningCode) ? null : input.DefaultMeaningCode,
                DefaultVisualMode = visualMode,
                RegisteredAtUtc = now,
                CreatedBy = "internal:" + request.Module
            });
        }

        try
        {
            var registered = await _transactions.ExecuteAsync(async (session, txCt) =>
            {
                var changed = 0;
                foreach (var action in actions)
                {
                    if (await _actions.UpsertAsync(session, action, txCt))
                    {
                        changed++;
                    }
                }

                return changed;
            }, ct);

            // The catalog is global (no tenant), so its audit event is platform-global, written by a service actor.
            await _audit.AppendAsync(new AuditAppendRequest
            {
                CorrelationId = Guid.NewGuid(),
                RequestType = ESignAuditEvents.ActionsRegistered,
                ActorType = AuditActorType.Service,
                Category = AuditCategory.ESignature,
                EntityType = "ESignableAction",
                Operation = AuditOperation.Update,
                IsPlatformGlobal = true,
                Metadata = new Dictionary<string, object?>
                {
                    ["module"] = request.Module,
                    ["registered"] = registered,
                    ["unchanged"] = actions.Count - registered,
                    ["actionCodes"] = string.Join(",", actions.Select(a => a.ActionCode))
                },
                SourceModule = ESignAuditEvents.SourceModule
            }, ct);
            return Response<RegisterActionsResultDto>.Success(new RegisterActionsResultDto(registered, actions.Count - registered));
        }
        catch (ESignatureDuplicateException)
        {
            return ESignResponses.Fail<RegisterActionsResultDto>(ESignCodes.ValidationFailed, 409, "A parallel registration of the same action won; retry.");
        }
        catch (PlatformTransactionUnavailableException)
        {
            return ESignResponses.Fail<RegisterActionsResultDto>(ESignCodes.ReauthUnavailable, 503, "The actions could not be registered now.");
        }
    }
}
