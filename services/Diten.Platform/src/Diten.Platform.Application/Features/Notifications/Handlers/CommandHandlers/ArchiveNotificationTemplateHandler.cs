using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Notifications.Commands;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Notifications.Handlers.CommandHandlers;

public sealed class ArchiveNotificationTemplateHandler
    : IRequestHandler<ArchiveNotificationTemplateCommand, Response<NoContent>>
{
    private readonly INotificationTemplateRepository _repository;

    public ArchiveNotificationTemplateHandler(INotificationTemplateRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<NoContent>> Handle(ArchiveNotificationTemplateCommand request, CancellationToken ct)
    {
        var template = await _repository.GetByIdAsync(request.Id, ct);
        if (template is null)
        {
            return Response<NoContent>.Fail("Notification template not found.", 404);
        }

        await _repository.ArchiveAsync(request.Id, ct);
        return Response<NoContent>.Success(204);
    }
}
