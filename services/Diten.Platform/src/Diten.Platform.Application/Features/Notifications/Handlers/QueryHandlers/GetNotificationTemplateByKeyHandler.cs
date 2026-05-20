using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Notifications.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Notifications.Handlers.QueryHandlers;

public sealed class GetNotificationTemplateByKeyHandler
    : IRequestHandler<GetNotificationTemplateByKeyQuery, Response<NotificationTemplateDto>>
{
    private readonly INotificationTemplateRepository _repository;

    public GetNotificationTemplateByKeyHandler(INotificationTemplateRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<NotificationTemplateDto>> Handle(GetNotificationTemplateByKeyQuery request, CancellationToken ct)
    {
        var template = await _repository.GetActiveByKeyAsync(
            request.TenantId,
            request.IsPlatformDefault,
            NotificationParsing.NormalizeTemplateKey(request.TemplateKey),
            NotificationParsing.NormalizeLocale(request.Locale),
            request.Channel,
            ct);

        return template is null
            ? Response<NotificationTemplateDto>.Fail("Notification template not found.", 404)
            : Response<NotificationTemplateDto>.Success(template.ToDto());
    }
}
