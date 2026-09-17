using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Services;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;

/// <summary>
/// DCP-005 slice 1 — the write side of the task-type catalogue.
///
/// <para>Modelled on <c>TaskFieldDefinitionHandlers</c> in this same folder: same layering, same validation
/// placement, same response shapes. Nothing here is a new pattern.</para>
/// </summary>
public sealed class CreateTaskTypeHandler : IRequestHandler<CreateTaskTypeCommand, Response<CreateTaskTypeResultDto>>
{
    private readonly ITaskTypeRepository _types;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;
    private readonly IControlledDocumentEffectivenessPort _effectiveness;

    public CreateTaskTypeHandler(
        ITaskTypeRepository types, ITenantContext tenantContext, ICurrentUserContext currentUser,
        IControlledDocumentEffectivenessPort effectiveness)
    {
        _types = types;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _effectiveness = effectiveness;
    }

    public async Task<Response<CreateTaskTypeResultDto>> Handle(CreateTaskTypeCommand command, CancellationToken ct)
    {
        var request = command.Request;

        if (TaskTypeRules.ValidateShape(request.Code, request.Name, request.FunctionCode) is { } shapeInvalid)
        {
            return Response<CreateTaskTypeResultDto>.Fail(shapeInvalid.Message, 400, shapeInvalid.ReasonCode, command.CorrelationId);
        }

        if (TaskTypeRules.ValidateClassification(request.RecordClass, request.GqmsDomain) is { } classInvalid)
        {
            return Response<CreateTaskTypeResultDto>.Fail(classInvalid.Message, 400, classInvalid.ReasonCode, command.CorrelationId);
        }

        if (TaskTypeRules.ValidateReviewMeetingRequirement(request.ReviewMeetingRequirement) is { } meetingInvalid)
        {
            return Response<CreateTaskTypeResultDto>.Fail(meetingInvalid.Message, 400, meetingInvalid.ReasonCode, command.CorrelationId);
        }

        var (outcomes, outcomesInvalid) = TaskTypeRules.NormalizeClosureOutcomes(
            request.ClosureOutcomes?.Select(ToOutcome));
        if (outcomesInvalid is { } outcomeError)
        {
            return Response<CreateTaskTypeResultDto>.Fail(
                outcomeError.Message, 400, outcomeError.ReasonCode, command.CorrelationId);
        }

        var code = TaskTypeRules.NormalizeCode(request.Code);
        var existing = await _types.ListAllAsync(ct);
        /*
         * Checked against EVERY type, retired ones included: a code freed by deactivation could be re-used for
         * different work, and every task opened under the old meaning would silently join the new one.
         */
        if (TaskTypeRules.ValidateCodeUnique(code, existing) is { } duplicate)
        {
            return Response<CreateTaskTypeResultDto>.Fail(duplicate.Message, 409, duplicate.ReasonCode, command.CorrelationId);
        }

        var type = new TaskType
        {
            TenantId = _tenantContext.TenantId,
            Code = code,
            Name = request.Name.Trim(),
            Description = Trimmed(request.Description),
            RecordClass = request.RecordClass,
            GqmsDomain = request.GqmsDomain,
            // Stored in the canonical spelling the closed list uses, so `mfg` and `MFG` are one value.
            FunctionCode = TaskTypeRules.ParseFunctionCode(request.FunctionCode).Value?.ToString(),
            IsQualityEvent = request.IsQualityEvent,
            GroupDocuments = TaskTypeRules.NormalizeDocuments(request.GroupDocuments),
            LocalDocuments = TaskTypeRules.NormalizeLocalDocuments(request.LocalDocuments),
            ClosureOutcomes = outcomes!,
            // Null takes Optional — the entity's own default, which changes no type's behaviour.
            ReviewMeetingRequirement = request.ReviewMeetingRequirement ?? TaskReviewMeetingRequirement.Optional,
            RequiresDeliverableOnCompletion = request.RequiresDeliverableOnCompletion,
            CreatedBy = _currentUser.ActorName
        };

        // Kural 4 v2 (sahip 2026-09-15, Blueprint/SAP/Oracle kıyasıyla doğrulandı — WP-CT-DECISION-BENCHMARK-01):
        // creation is NEVER refused for a document reason. A type born with a non-Effective or unverifiable
        // governing document is simply saved INACTIVE — the same posture SAP's "Created" status and Veeva's
        // "draft document assigns no training" take. Only /active and an active type's own document-changing
        // edit are hard gates now.
        var check = await TaskTypeEffectivenessGate.CheckAsync(_effectiveness, type, ct);
        type.IsActive = check.Outcome == TaskTypeEffectivenessOutcome.AllEffective;

        var created = await _types.CreateAsync(type, ct);
        var result = new CreateTaskTypeResultDto(
            created.Id,
            created.IsActive,
            BlockingDocuments: check.Outcome == TaskTypeEffectivenessOutcome.SomeBlocked ? check.BlockingDetails : [],
            EffectivenessUnavailable: check.Outcome == TaskTypeEffectivenessOutcome.RegisterUnavailable);
        return Response<CreateTaskTypeResultDto>.Success(result, 201, command.CorrelationId);
    }

    internal static string? Trimmed(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>The wire shape as the entity. Normalisation and refusal are the rules' job, not this mapping's.</summary>
    internal static TaskClosureOutcome ToOutcome(TaskClosureOutcomeDto dto) => new()
    {
        Code = dto.Code,
        LabelResourceKey = dto.LabelResourceKey,
        LabelText = dto.LabelText,
        Disposition = dto.Disposition,
        RequiresReason = dto.RequiresReason,
        SortOrder = dto.SortOrder
    };
}

/// <summary>
/// Edit a task type — everything except its code.
/// </summary>
public sealed class UpdateTaskTypeHandler : IRequestHandler<UpdateTaskTypeCommand, Response<NoContent>>
{
    private readonly ITaskTypeRepository _types;
    private readonly IControlledDocumentEffectivenessPort _effectiveness;

    public UpdateTaskTypeHandler(ITaskTypeRepository types, IControlledDocumentEffectivenessPort effectiveness)
    {
        _types = types;
        _effectiveness = effectiveness;
    }

    public async Task<Response<NoContent>> Handle(UpdateTaskTypeCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var type = await _types.GetByIdAsync(command.Id, ct);
        if (type is null || type.DeletedAt is not null)
        {
            return Response<NoContent>.Fail(
                "Task type not found.", 404, TaskReasonCodes.NotFound, command.CorrelationId);
        }

        /*
         * ⚠ THE CODE IS REFUSED, NOT IGNORED. The screen sends it read-only, so a request carrying a different
         * one is either a client bug or somebody bypassing the form — and quietly keeping the stored value would
         * report success for a change the caller asked for and did not get.
         */
        if (TaskTypeRules.ValidateCodeUnchanged(type.Code, request.Code) is { } codeChanged)
        {
            return Response<NoContent>.Fail(
                codeChanged.Message, 400, codeChanged.ReasonCode, command.CorrelationId);
        }

        if (TaskTypeRules.ValidateShape(type.Code, request.Name, request.FunctionCode) is { } shapeInvalid)
        {
            return Response<NoContent>.Fail(
                shapeInvalid.Message, 400, shapeInvalid.ReasonCode, command.CorrelationId);
        }

        if (TaskTypeRules.ValidateClassification(request.RecordClass, request.GqmsDomain) is { } classInvalid)
        {
            return Response<NoContent>.Fail(
                classInvalid.Message, 400, classInvalid.ReasonCode, command.CorrelationId);
        }

        if (TaskTypeRules.ValidateReviewMeetingRequirement(request.ReviewMeetingRequirement) is { } meetingInvalid)
        {
            return Response<NoContent>.Fail(
                meetingInvalid.Message, 400, meetingInvalid.ReasonCode, command.CorrelationId);
        }

        var newGroupDocuments = TaskTypeRules.NormalizeDocuments(request.GroupDocuments);
        var newLocalDocuments = TaskTypeRules.NormalizeLocalDocuments(request.LocalDocuments);

        // Kural 4 v2 (sahip 2026-09-15) — an ACTIVE type whose bound documents actually CHANGE is hard-gated,
        // the SAME refusal /active gives (409/503); editing anything else, or re-posting the same document set,
        // never re-checks (HaveDocumentsChanged — an already-active type is not retroactively re-examined just
        // because it was saved again). Checked BEFORE any field is assigned below, so a refusal here writes
        // NOTHING — not even the unrelated fields this same request carried.
        if (type.IsActive
            && TaskTypeEffectivenessGate.HaveDocumentsChanged(type.GroupDocuments, type.LocalDocuments, newGroupDocuments, newLocalDocuments))
        {
            var probe = new TaskType
            {
                TenantId = type.TenantId, Code = type.Code, Name = type.Name,
                GroupDocuments = newGroupDocuments, LocalDocuments = newLocalDocuments
            };
            var check = await TaskTypeEffectivenessGate.CheckAsync(_effectiveness, probe, ct);
            if (TaskTypeEffectivenessGate.ToBlockingResponse<NoContent>(check, command.CorrelationId) is { } blocked)
            {
                return blocked;
            }
        }

        type.Name = request.Name.Trim();
        type.Description = CreateTaskTypeHandler.Trimmed(request.Description);
        type.RecordClass = request.RecordClass;
        type.GqmsDomain = request.GqmsDomain;
        type.FunctionCode = TaskTypeRules.ParseFunctionCode(request.FunctionCode).Value?.ToString();
        type.IsQualityEvent = request.IsQualityEvent;
        type.GroupDocuments = newGroupDocuments;
        type.LocalDocuments = newLocalDocuments;

        /*
         * Null is "not asking" — the stored value stays. Only the TYPE changes: no task opened under it is read or
         * written here, so the setting is forward-looking by construction.
         */
        if (request.ReviewMeetingRequirement is { } reviewMeetingRequirement)
        {
            type.ReviewMeetingRequirement = reviewMeetingRequirement;
        }

        type.RequiresDeliverableOnCompletion = request.RequiresDeliverableOnCompletion;

        /*
         * ⚠ NULL IS "NOT ASKING", AND THIS BRANCH IS THE WHOLE REASON THE FIELD IS NULLABLE.
         *
         * An update is a FULL REPLACE everywhere else on this record, which is right for fields the editor
         * draws. The editor does NOT draw this one yet — so replacing on null would make every save from the
         * current screen silently delete a type's outcome dictionary, and the dictionary would look like it had
         * never been configured. An empty LIST still clears it: that is a caller who knows about the field and
         * is asking.
         */
        if (request.ClosureOutcomes is not null)
        {
            var (outcomes, outcomesInvalid) = TaskTypeRules.NormalizeClosureOutcomes(
                request.ClosureOutcomes.Select(CreateTaskTypeHandler.ToOutcome));
            if (outcomesInvalid is { } outcomeError)
            {
                return Response<NoContent>.Fail(
                    outcomeError.Message, 400, outcomeError.ReasonCode, command.CorrelationId);
            }

            type.ClosureOutcomes = outcomes!;
        }

        // WP-PSS-MOD0024-TASK-TYPE-CONCURRENCY-01 (BL-375) — two managers editing the same type at once used to
        // have the second silently overwrite the first's change with no warning at all.
        if (!await _types.UpdateAsync(type, request.ExpectedVersion, ct))
        {
            return Response<NoContent>.Fail(
                "The task type changed meanwhile; reload and retry.",
                409, TaskReasonCodes.ConcurrencyConflict, command.CorrelationId);
        }

        return Response<NoContent>.Success(204, command.CorrelationId);
    }
}

/// <summary>
/// Retire or restore a task type.
///
/// <para><b>There is no delete, and that is the rule rather than an omission.</b> A type that has been used is
/// part of the identity of every task opened under it; removing it would make those tasks unreadable in exactly
/// the way this product refuses for folders and controlled documents. Retiring keeps the past legible and stops
/// the type appearing on new work — which is the whole of what "delete" was ever wanted for here.</para>
/// </summary>
public sealed class SetTaskTypeActiveHandler : IRequestHandler<SetTaskTypeActiveCommand, Response<NoContent>>
{
    private readonly ITaskTypeRepository _types;
    private readonly IControlledDocumentEffectivenessPort _effectiveness;

    public SetTaskTypeActiveHandler(ITaskTypeRepository types, IControlledDocumentEffectivenessPort effectiveness)
    {
        _types = types;
        _effectiveness = effectiveness;
    }

    public async Task<Response<NoContent>> Handle(SetTaskTypeActiveCommand command, CancellationToken ct)
    {
        var type = await _types.GetByIdAsync(command.Id, ct);
        if (type is null || type.DeletedAt is not null)
        {
            return Response<NoContent>.Fail(
                "Task type not found.", 404, TaskReasonCodes.NotFound, command.CorrelationId);
        }

        // Kural 4 (DCP-005 Adım 3, G3 — sahip 2026-09-15, Kalite teyidi bekliyor) — ONLY the pasif→aktif edge is
        // gated: deactivating never checks (a manager must always be able to retire a type), and a type that is
        // already active never re-checks itself just because this endpoint was called with IsActive=true again.
        if (command.Request.IsActive && !type.IsActive)
        {
            var check = await TaskTypeEffectivenessGate.CheckAsync(_effectiveness, type, ct);
            if (TaskTypeEffectivenessGate.ToBlockingResponse<NoContent>(check, command.CorrelationId) is { } blocked)
            {
                return blocked;
            }
        }

        type.IsActive = command.Request.IsActive;

        // Same write path as the full edit, same protection (BL-375): a manager retiring a type must not silently
        // discard a colleague's edit that landed between this read and this write.
        if (!await _types.UpdateAsync(type, command.Request.ExpectedVersion, ct))
        {
            return Response<NoContent>.Fail(
                "The task type changed meanwhile; reload and retry.",
                409, TaskReasonCodes.ConcurrencyConflict, command.CorrelationId);
        }

        return Response<NoContent>.Success(204, command.CorrelationId);
    }
}
