using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Notifications.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Notifications.Handlers.QueryHandlers;

public sealed class GetNotificationTemplateByIdHandler
    : IRequestHandler<GetNotificationTemplateByIdQuery, Response<NotificationTemplateDto>>
{
    private readonly INotificationTemplateRepository _repository;

    public GetNotificationTemplateByIdHandler(INotificationTemplateRepository repository) => _repository = repository;

    public async Task<Response<NotificationTemplateDto>> Handle(GetNotificationTemplateByIdQuery request, CancellationToken ct)
    {
        var template = await _repository.GetByIdAsync(request.Id, ct);
        return template is null
            ? Response<NotificationTemplateDto>.Fail("Notification template not found.", 404)
            : Response<NotificationTemplateDto>.Success(template.ToDto());
    }
}
