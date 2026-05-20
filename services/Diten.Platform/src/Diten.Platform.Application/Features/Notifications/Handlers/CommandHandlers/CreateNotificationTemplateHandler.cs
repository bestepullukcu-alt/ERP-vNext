using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Notifications.Commands;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Notifications.Handlers.CommandHandlers;

public sealed class CreateNotificationTemplateHandler
    : IRequestHandler<CreateNotificationTemplateCommand, Response<NotificationTemplateDto>>
{
    private readonly INotificationTemplateRepository _repository;

    public CreateNotificationTemplateHandler(INotificationTemplateRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<NotificationTemplateDto>> Handle(CreateNotificationTemplateCommand request, CancellationToken ct)
    {
        var parse = ParseRequest(request.Request);
        if (!parse.IsSuccessful)
        {
            return Response<NotificationTemplateDto>.Fail(parse.Errors, parse.StatusCode);
        }

        var (channel, status, variables) = parse.Data;
        var templateKey = NotificationParsing.NormalizeTemplateKey(request.Request.TemplateKey);
        var locale = NotificationParsing.NormalizeLocale(request.Request.Locale);
        var isPlatformDefault = request.Request.IsPlatformDefault;
        var tenantId = isPlatformDefault ? null : request.TenantId;

        if (!isPlatformDefault && tenantId is null)
        {
            return Response<NotificationTemplateDto>.Fail("TenantId route is required for tenant-specific templates.", 400);
        }

        if (await _repository.ActiveTemplateExistsAsync(tenantId, isPlatformDefault, templateKey, locale, channel, ct: ct))
        {
            return Response<NotificationTemplateDto>.Fail("An active notification template already exists for this scope, locale, channel, and key.", 409);
        }

        var template = new NotificationTemplate
        {
            TenantId = tenantId,
            IsPlatformDefault = isPlatformDefault,
            TemplateKey = templateKey,
            Channel = channel,
            Locale = locale,
            SubjectTemplate = request.Request.SubjectTemplate.Trim(),
            BodyHtmlTemplate = request.Request.BodyHtmlTemplate.Trim(),
            BodyTextTemplate = string.IsNullOrWhiteSpace(request.Request.BodyTextTemplate) ? null : request.Request.BodyTextTemplate.Trim(),
            Variables = variables,
            Status = status,
            SemanticVersion = string.IsNullOrWhiteSpace(request.Request.SemanticVersion) ? null : request.Request.SemanticVersion.Trim()
        };

        await _repository.CreateAsync(template, ct);
        return Response<NotificationTemplateDto>.Success(template.ToDto(), 201);
    }

    internal static Response<(Domain.Enums.NotificationChannelCode Channel, Domain.Enums.NotificationTemplateStatus Status, List<TemplateVariableDefinition> Variables)> ParseRequest(NotificationTemplateUpsertRequest request)
    {
        if (!NotificationParsing.TryParseChannel(request.Channel, out var channel))
        {
            return Response<(Domain.Enums.NotificationChannelCode, Domain.Enums.NotificationTemplateStatus, List<TemplateVariableDefinition>)>.Fail("Channel is invalid.", 400);
        }

        if (!NotificationParsing.TryParseTemplateStatus(request.Status, out var status))
        {
            return Response<(Domain.Enums.NotificationChannelCode, Domain.Enums.NotificationTemplateStatus, List<TemplateVariableDefinition>)>.Fail("Status is invalid.", 400);
        }

        var variables = new List<TemplateVariableDefinition>();
        foreach (var variable in request.Variables)
        {
            if (!NotificationParsing.TryParseVariableType(variable.Type, out var type))
            {
                return Response<(Domain.Enums.NotificationChannelCode, Domain.Enums.NotificationTemplateStatus, List<TemplateVariableDefinition>)>.Fail($"Variable type is invalid for {variable.Name}.", 400);
            }

            variables.Add(new TemplateVariableDefinition
            {
                Name = variable.Name.Trim(),
                Type = type,
                IsRequired = variable.IsRequired
            });
        }

        return Response<(Domain.Enums.NotificationChannelCode, Domain.Enums.NotificationTemplateStatus, List<TemplateVariableDefinition>)>.Success((channel, status, variables));
    }
}
