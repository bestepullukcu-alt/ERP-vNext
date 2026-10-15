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
    // BL-454 — optional for the same reason as on the queue handler: registered in DI, absent from older test doubles.
    private readonly IEmailShellComposer? _shellComposer;
    private readonly Diten.Platform.Domain.Repositories.INotificationTemplateRepository? _templates;

    public RenderNotificationTemplatePreviewHandler(
        IEmailTemplateRenderer renderer,
        IEmailShellComposer? shellComposer = null,
        Diten.Platform.Domain.Repositories.INotificationTemplateRepository? templates = null)
    {
        _renderer = renderer;
        _shellComposer = shellComposer;
        _templates = templates;
    }

    public async Task<Response<RenderedEmailTemplateDto>> Handle(RenderNotificationTemplatePreviewQuery request, CancellationToken ct)
    {
        var variables = new List<TemplateVariableDefinition>(request.Request.Variables.Count);
        foreach (var definition in request.Request.Variables)
        {
            if (!NotificationParsing.TryParseVariableType(definition.Type, out var type))
            {
                return Response<RenderedEmailTemplateDto>.Fail(
                    $"Unknown template variable type '{definition.Type}' for variable '{definition.Name}'.",
                    400);
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

        var rendered = _renderer.Render(template, request.Request.SampleVariables);
        if (_shellComposer is null || !rendered.IsSuccessful || rendered.Data is null)
        {
            return rendered;
        }

        /*
         * BL-454 — the author sees the e-mail as it arrives. The editor has no tenant and no recipient, so the frame
         * is the platform's own (product name on the band, English); what it shows faithfully is the card, the
         * heading taken from the subject, and where the body sits. The fields the editor already read are untouched.
         */
        if (request.Request.TemplateId is { } savedId && _templates is not null)
        {
            template.Shell = (await _templates.GetByIdAsync(savedId, ct))?.Shell;
        }

        var framed = _shellComposer.Compose(
            TenantEmailIdentity.Platform, template, template.Locale,
            rendered.Data.Subject, rendered.Data.BodyHtml, rendered.Data.BodyText, request.Request.SampleVariables);

        return Response<RenderedEmailTemplateDto>.Success(
            rendered.Data with { BodyHtmlFramed = framed.BodyHtml, BodyTextFramed = framed.BodyText },
            rendered.StatusCode);
    }
}
