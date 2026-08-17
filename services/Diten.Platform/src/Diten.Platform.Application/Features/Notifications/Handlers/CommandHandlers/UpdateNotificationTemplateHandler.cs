using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Notifications.Commands;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Notifications.Handlers.CommandHandlers;

public sealed class UpdateNotificationTemplateHandler
    : IRequestHandler<UpdateNotificationTemplateCommand, Response<NotificationTemplateDto>>
{
    private readonly INotificationTemplateRepository _repository;

    public UpdateNotificationTemplateHandler(INotificationTemplateRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<NotificationTemplateDto>> Handle(UpdateNotificationTemplateCommand request, CancellationToken ct)
    {
        var template = await _repository.GetByIdAsync(request.Id, ct);
        if (template is null)
        {
            return Response<NotificationTemplateDto>.Fail("Notification template not found.", 404);
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
        template.Version++;

        await _repository.UpdateAsync(template, ct);
        return Response<NotificationTemplateDto>.Success(template.ToDto());
    }
}
