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
/// Phase 5 — managing the configurable field catalogue (pack §12 K1).
///
/// <para>Until now the catalogue could only be empty: the validator read it, nothing wrote it. These handlers are
/// the write side, and every rule they enforce exists because breaking it corrupts data that is already stored —
/// an edited code orphans values, a seventh section deletes items from the surface, a missing label puts a raw
/// key on screen.</para>
/// </summary>
public sealed class CreateTaskFieldDefinitionHandler
    : IRequestHandler<CreateTaskFieldDefinitionCommand, Response<Guid>>
{
    private readonly ITaskFieldDefinitionRepository _definitions;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;

    public CreateTaskFieldDefinitionHandler(
        ITaskFieldDefinitionRepository definitions,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser)
    {
        _definitions = definitions;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
    }

    public async Task<Response<Guid>> Handle(CreateTaskFieldDefinitionCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var code = request.Code?.Trim() ?? string.Empty;

        if (code.Length == 0)
        {
            return Response<Guid>.Fail(
                "A field definition needs a code.", 400, TaskReasonCodes.ValidationFailed, command.CorrelationId);
        }

        if (TaskFieldDefinitionRules.ValidateLabel(request.LabelResourceKey, request.LabelText) is { } labelInvalid)
        {
            return Response<Guid>.Fail(
                labelInvalid.Message, 400, labelInvalid.ReasonCode, command.CorrelationId);
        }

        // The code is the join key for every value stored under it, so a duplicate is not a naming annoyance —
        // it is two definitions claiming the same data.
        if (await _definitions.GetByCodeAsync(code, ct) is not null)
        {
            return Response<Guid>.Fail(
                $"A field definition with code '{code}' already exists.",
                409, TaskReasonCodes.FieldDefinitionCodeTaken, command.CorrelationId);
        }

        var existing = await _definitions.ListAllAsync(ct);
        if (TaskFieldDefinitionRules.ValidateSection(request.Section, existing) is { } sectionInvalid)
        {
            return Response<Guid>.Fail(
                sectionInvalid.Message, 400, sectionInvalid.ReasonCode, command.CorrelationId);
        }

        var definition = new TaskFieldDefinition
        {
            TenantId = _tenantContext.TenantId,
            Code = code,
            LabelResourceKey = Trimmed(request.LabelResourceKey),
            LabelText = Trimmed(request.LabelText),
            ValueType = request.ValueType,
            Section = request.Section.Trim(),
            Importance = request.Importance,
            IsRequired = request.IsRequired,
            SortOrder = request.SortOrder,
            OptionsSourceKind = request.OptionsSourceKind,
            OptionsSourceKey = Trimmed(request.OptionsSourceKey),
            AppliesToModuleCode = Trimmed(request.AppliesToModuleCode),
            // Stored, never evaluated — field-level authorization is BL-024 (see the request's own note).
            Classification = request.Classification,
            DefaultAccessState = request.DefaultAccessState,
            IsActive = request.IsActive,
            CreatedBy = _currentUser.ActorName
        };

        var created = await _definitions.CreateAsync(definition, ct);
        return Response<Guid>.Success(created.Id, 201, command.CorrelationId);
    }

    private static string? Trimmed(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class UpdateTaskFieldDefinitionHandler
    : IRequestHandler<UpdateTaskFieldDefinitionCommand, Response<NoContent>>
{
    private readonly ITaskFieldDefinitionRepository _definitions;
    private readonly ICurrentUserContext _currentUser;

    public UpdateTaskFieldDefinitionHandler(
        ITaskFieldDefinitionRepository definitions, ICurrentUserContext currentUser)
    {
        _definitions = definitions;
        _currentUser = currentUser;
    }

    public async Task<Response<NoContent>> Handle(UpdateTaskFieldDefinitionCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var definition = await _definitions.GetByIdAsync(command.Id, ct);
        if (definition is null || definition.DeletedAt is not null)
        {
            return Response<NoContent>.Fail(
                "Field definition not found.", 404,
                TaskReasonCodes.FieldDefinitionNotFound, command.CorrelationId);
        }

        if (TaskFieldDefinitionRules.ValidateLabel(request.LabelResourceKey, request.LabelText) is { } labelInvalid)
        {
            return Response<NoContent>.Fail(
                labelInvalid.Message, 400, labelInvalid.ReasonCode, command.CorrelationId);
        }

        var existing = await _definitions.ListAllAsync(ct);
        if (TaskFieldDefinitionRules.ValidateSection(request.Section, existing, excludingId: definition.Id)
            is { } sectionInvalid)
        {
            return Response<NoContent>.Fail(
                sectionInvalid.Message, 400, sectionInvalid.ReasonCode, command.CorrelationId);
        }

        /*
         * `Code` is NOT assigned, and it is not on the request either.
         *
         * Every TaskFieldValue already stored joins to its definition by CODE, not by id. Renaming
         * `regulatory.phase` would orphan every value written under it: the data survives, its label does not,
         * and the screen shows a column of values with no heading. The definition can be retired and a new one
         * created — that is a decision someone makes deliberately, not a side effect of an edit.
         */
        definition.LabelResourceKey = Trimmed(request.LabelResourceKey);
        definition.LabelText = Trimmed(request.LabelText);
        definition.ValueType = request.ValueType;
        definition.Section = request.Section.Trim();
        definition.Importance = request.Importance;
        definition.IsRequired = request.IsRequired;
        definition.SortOrder = request.SortOrder;
        definition.OptionsSourceKind = request.OptionsSourceKind;
        definition.OptionsSourceKey = Trimmed(request.OptionsSourceKey);
        definition.AppliesToModuleCode = Trimmed(request.AppliesToModuleCode);
        definition.Classification = request.Classification;
        definition.DefaultAccessState = request.DefaultAccessState;
        definition.IsActive = request.IsActive;
        definition.UpdatedBy = _currentUser.ActorName;

        if (!await _definitions.UpdateAsync(definition, request.ExpectedVersion, ct))
        {
            return Response<NoContent>.Fail(
                "The definition changed meanwhile; reload and retry.",
                409, TaskReasonCodes.ConcurrencyConflict, command.CorrelationId);
        }

        return Response<NoContent>.Success(204, command.CorrelationId);
    }

    private static string? Trimmed(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// Retire a definition. It is ALWAYS a deactivation, never a destruction.
///
/// <para><b>Why there is no hard delete, and no "is it in use?" check either.</b> Values already written under
/// this definition join to it by code. Removing the row would leave them readable but unexplained — a column of
/// data with no heading, which is worse than stale. So the row stays, <c>DeletedAt</c> is stamped and
/// <c>IsActive</c> goes false: new tasks stop being offered the field, and every value already stored keeps its
/// definition to render against.</para>
///
/// <para>Checking usage first was considered and rejected. It would have made the outcome depend on data volume
/// — the same button destroying a definition on a quiet tenant and refusing on a busy one — and the answer would
/// be stale the moment it was given. Deactivating unconditionally is one behaviour, always safe, and the caller
/// never has to ask which one they are about to get.</para>
/// </summary>
public sealed class DeleteTaskFieldDefinitionHandler
    : IRequestHandler<DeleteTaskFieldDefinitionCommand, Response<NoContent>>
{
    private readonly ITaskFieldDefinitionRepository _definitions;
    private readonly ICurrentUserContext _currentUser;

    public DeleteTaskFieldDefinitionHandler(
        ITaskFieldDefinitionRepository definitions, ICurrentUserContext currentUser)
    {
        _definitions = definitions;
        _currentUser = currentUser;
    }

    public async Task<Response<NoContent>> Handle(DeleteTaskFieldDefinitionCommand command, CancellationToken ct)
    {
        var definition = await _definitions.GetByIdAsync(command.Id, ct);
        if (definition is null || definition.DeletedAt is not null)
        {
            return Response<NoContent>.Fail(
                "Field definition not found.", 404,
                TaskReasonCodes.FieldDefinitionNotFound, command.CorrelationId);
        }

        // Both, for the same reason the recurrence rule sets both: the value validator checks IsActive and the
        // management surface checks DeletedAt, and a retired definition must be gone from both answers.
        definition.DeletedAt = DateTimeOffset.UtcNow;
        definition.IsActive = false;
        definition.UpdatedBy = _currentUser.ActorName;

        if (!await _definitions.UpdateAsync(definition, definition.Version, ct))
        {
            return Response<NoContent>.Fail(
                "The definition changed meanwhile; reload and retry.",
                409, TaskReasonCodes.ConcurrencyConflict, command.CorrelationId);
        }

        return Response<NoContent>.Success(204, command.CorrelationId);
    }
}
