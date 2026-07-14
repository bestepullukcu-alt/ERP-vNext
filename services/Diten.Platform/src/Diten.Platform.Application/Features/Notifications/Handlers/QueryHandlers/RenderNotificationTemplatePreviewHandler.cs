using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Notifications.Queries;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.Notifications.Handlers.QueryHandlers;

public sealed class RenderNotificationTemplatePreviewHandler
    : IRequestHandler<RenderNotificationTemplatePreviewQuery, Response<RenderedEmailTemplateDto>>
{
    private readonly IEmailTemplateRenderer _renderer;

    public RenderNotificationTemplatePreviewHandler(IEmailTemplateRenderer renderer) => _renderer = renderer;

    public Task<Response<RenderedEmailTemplateDto>> Handle(RenderNotificationTemplatePreviewQuery request, CancellationToken ct)
    {
        var variables = new List<TemplateVariableDefinition>(request.Request.Variables.Count);
        foreach (var definition in request.Request.Variables)
        {
            if (!NotificationParsing.TryParseVariableType(definition.Type, out var type))
            {
                return Task.FromResult(Response<RenderedEmailTemplateDto>.Fail(
                    $"Unknown template variable type '{definition.Type}' for variable '{definition.Name}'.",
                    400));
            }

            variables.Add(new TemplateVariableDefinition
            {
                Name = definition.Name.Trim(),
                Type = type,
                IsRequired = definition.IsRequired
            });
        }

        // Transient template: renders unsaved editor content only and is never persisted.
        var template = new NotificationTemplate
        {
            TemplateKey = "preview.transient",
            Channel = NotificationChannelCode.Email,
            Locale = "en",
            SubjectTemplate = request.Request.SubjectTemplate,
            BodyHtmlTemplate = request.Request.BodyHtmlTemplate ?? string.Empty,
            BodyTextTemplate = request.Request.BodyTextTemplate,
            Variables = variables,
            Status = NotificationTemplateStatus.Draft
        };

        return Task.FromResult(_renderer.Render(template, request.Request.SampleVariables));
    }
}
