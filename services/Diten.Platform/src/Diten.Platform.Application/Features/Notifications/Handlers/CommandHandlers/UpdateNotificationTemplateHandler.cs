using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Notifications.Commands;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Notifications.Handlers.CommandHandlers;

public sealed class UpdateNotificationTemplateHandler
    : IRequestHandler<UpdateNotificationTemplateCommand, Response<NotificationTemplateDto>>
{
    private readonly INotificationTemplateRepository _repository;
    // BL-454 — optional so the handler built the old way still compiles; registered in DI, so production has it.
    private readonly Diten.Platform.Application.Contracts.ICurrentUserContext? _currentUser;

    /// <summary>What an operator's save writes into <c>UpdatedBy</c> when no signed-in actor is known.</summary>
    public const string OperatorActor = "operator";

    /// <summary>The reason code of a save refused because the template changed after the editor read it.</summary>
    public const string ReasonTemplateChanged = "TEMPLATE_CHANGED";

    public UpdateNotificationTemplateHandler(
        INotificationTemplateRepository repository,
        Diten.Platform.Application.Contracts.ICurrentUserContext? currentUser = null)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<Response<NotificationTemplateDto>> Handle(UpdateNotificationTemplateCommand request, CancellationToken ct)
    {
        var template = await _repository.GetByIdAsync(request.Id, ct);
        if (template is null)
        {
            return Response<NotificationTemplateDto>.Fail("Notification template not found.", 404);
        }

        // BL-454 — a save made from an OLDER read is refused, never written over what changed since (another operator, or
        // the seed carrying the template forward to a new version). An editor that sends no RowVersion is not checked.
        if (request.Request.RowVersion is { Length: > 0 } expected
            && !expected.AsSpan().SequenceEqual(NotificationMappings.RowVersionOf(template.Version)))
        {
            return Response<NotificationTemplateDto>.Fail(
                "The template was changed after it was opened. Reload it and save again.", 409, ReasonTemplateChanged);
        }

        var parse = CreateNotificationTemplateHandler.ParseRequest(request.Request);
        if (!parse.IsSuccessful)
        {
            return Response<NotificationTemplateDto>.Fail(parse.Errors, parse.StatusCode);
        }

        var (channel, status, variables) = parse.Data;
        var templateKey = NotificationParsing.NormalizeTemplateKey(request.Request.TemplateKey);
        var locale = NotificationParsing.NormalizeLocale(request.Request.Locale);
        var tenantId = request.Request.IsPlatformDefault ? null : request.TenantId;

        if (!request.Request.IsPlatformDefault && tenantId is null)
        {
            return Response<NotificationTemplateDto>.Fail("TenantId route is required for tenant-specific templates.", 400);
        }

        if (await _repository.ActiveTemplateExistsAsync(tenantId, request.Request.IsPlatformDefault, templateKey, locale, channel, request.Id, ct))
        {
            return Response<NotificationTemplateDto>.Fail("An active notification template already exists for this scope, locale, channel, and key.", 409);
        }

        template.TenantId = tenantId;
        template.IsPlatformDefault = request.Request.IsPlatformDefault;
        template.TemplateKey = templateKey;
        template.Channel = channel;
        template.Locale = locale;
        template.SubjectTemplate = request.Request.SubjectTemplate.Trim();
        template.BodyHtmlTemplate = request.Request.BodyHtmlTemplate.Trim();
        template.BodyTextTemplate = string.IsNullOrWhiteSpace(request.Request.BodyTextTemplate) ? null : request.Request.BodyTextTemplate.Trim();
        template.Variables = variables;
        template.Status = status;
        template.SemanticVersion = string.IsNullOrWhiteSpace(request.Request.SemanticVersion) ? null : request.Request.SemanticVersion.Trim();
        template.UpdatedAt = DateTimeOffset.UtcNow;
        // BL-454 — an operator's save is signed. The seed carries a template forward only while UpdatedBy is its own
        // stamp: a row an operator saved — even with identical content — is theirs from then on.
        template.UpdatedBy = _currentUser is { UserId: var userId } && userId != Guid.Empty ? userId.ToString() : OperatorActor;
        template.Version++;

        await _repository.UpdateAsync(template, ct);
        return Response<NotificationTemplateDto>.Success(template.ToDto());
    }
}
