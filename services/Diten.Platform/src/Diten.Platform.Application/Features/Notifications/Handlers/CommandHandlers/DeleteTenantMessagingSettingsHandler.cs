using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Notifications.Commands;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Notifications.Handlers.CommandHandlers;

public sealed class DeleteTenantMessagingSettingsHandler
    : IRequestHandler<DeleteTenantMessagingSettingsCommand, Response<NoContent>>
{
    private readonly ITenantMessagingSettingsRepository _repository;

    public DeleteTenantMessagingSettingsHandler(ITenantMessagingSettingsRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<NoContent>> Handle(DeleteTenantMessagingSettingsCommand request, CancellationToken ct)
    {
        var existing = await _repository.GetByTenantIdAsync(request.TenantId, ct);
        if (existing is null)
        {
            return Response<NoContent>.Fail("Tenant messaging settings not found.", 404);
        }

        await _repository.SoftDeleteTenantAsync(request.TenantId, ct);
        return Response<NoContent>.Success(204);
    }
}
