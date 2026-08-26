using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;

/// <summary>
/// DCP-005 slice 1 — the write side of the task-type catalogue.
///
/// <para>Modelled on <c>TaskFieldDefinitionHandlers</c> in this same folder: same layering, same validation
/// placement, same response shapes. Nothing here is a new pattern.</para>
/// </summary>
public sealed class CreateTaskTypeHandler : IRequestHandler<CreateTaskTypeCommand, Response<Guid>>
{
    private readonly ITaskTypeRepository _types;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;

    public CreateTaskTypeHandler(
        ITaskTypeRepository types, ITenantContext tenantContext, ICurrentUserContext currentUser)
    {
        _types = types;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
    }

    public async Task<Response<Guid>> Handle(CreateTaskTypeCommand command, CancellationToken ct)
    {
        var request = command.Request;

        if (TaskTypeRules.ValidateShape(request.Code, request.Name, request.FunctionCode) is { } shapeInvalid)
        {
            return Response<Guid>.Fail(shapeInvalid.Message, 400, shapeInvalid.ReasonCode, command.CorrelationId);
        }

        if (TaskTypeRules.ValidateClassification(request.RecordClass, request.GqmsDomain) is { } classInvalid)
        {
            return Response<Guid>.Fail(classInvalid.Message, 400, classInvalid.ReasonCode, command.CorrelationId);
        }

        var code = TaskTypeRules.NormalizeCode(request.Code);
        var existing = await _types.ListAllAsync(ct);
        /*
         * Checked against EVERY type, retired ones included: a code freed by deactivation could be re-used for
         * different work, and every task opened under the old meaning would silently join the new one.
         */
        if (TaskTypeRules.ValidateCodeUnique(code, existing) is { } duplicate)
        {
            return Response<Guid>.Fail(duplicate.Message, 409, duplicate.ReasonCode, command.CorrelationId);
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
            IsActive = true,
            CreatedBy = _currentUser.ActorName
        };

        var created = await _types.CreateAsync(type, ct);
        return Response<Guid>.Success(created.Id, 201, command.CorrelationId);
    }

    internal static string? Trimmed(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// Edit a task type — everything except its code.
/// </summary>
public sealed class UpdateTaskTypeHandler : IRequestHandler<UpdateTaskTypeCommand, Response<NoContent>>
{
    private readonly ITaskTypeRepository _types;

    public UpdateTaskTypeHandler(ITaskTypeRepository types) => _types = types;

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

        type.Name = request.Name.Trim();
        type.Description = CreateTaskTypeHandler.Trimmed(request.Description);
        type.RecordClass = request.RecordClass;
        type.GqmsDomain = request.GqmsDomain;
        type.FunctionCode = TaskTypeRules.ParseFunctionCode(request.FunctionCode).Value?.ToString();
        type.IsQualityEvent = request.IsQualityEvent;
        type.GroupDocuments = TaskTypeRules.NormalizeDocuments(request.GroupDocuments);
        type.LocalDocuments = TaskTypeRules.NormalizeLocalDocuments(request.LocalDocuments);

        await _types.UpdateAsync(type, ct);
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

    public SetTaskTypeActiveHandler(ITaskTypeRepository types) => _types = types;

    public async Task<Response<NoContent>> Handle(SetTaskTypeActiveCommand command, CancellationToken ct)
    {
        var type = await _types.GetByIdAsync(command.Id, ct);
        if (type is null || type.DeletedAt is not null)
        {
            return Response<NoContent>.Fail(
                "Task type not found.", 404, TaskReasonCodes.NotFound, command.CorrelationId);
        }

        type.IsActive = command.Request.IsActive;
        await _types.UpdateAsync(type, ct);
        return Response<NoContent>.Success(204, command.CorrelationId);
    }
}
