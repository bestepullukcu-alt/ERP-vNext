using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Notifications.Queries;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Notifications.Handlers.QueryHandlers;

public sealed class GetNotificationDispatchByIdHandler
    : IRequestHandler<GetNotificationDispatchByIdQuery, Response<NotificationDispatchDto>>
{
    private readonly INotificationDispatchRepository _repository;
    public GetNotificationDispatchByIdHandler(INotificationDispatchRepository repository) => _repository = repository;
    public async Task<Response<NotificationDispatchDto>> Handle(GetNotificationDispatchByIdQuery request, CancellationToken ct)
    {
        var dispatch = await _repository.GetByIdForTenantAsync(request.TenantId, request.DispatchId, ct);
        return dispatch is null
            ? Response<NotificationDispatchDto>.Fail("Notification dispatch not found.", 404)
            : Response<NotificationDispatchDto>.Success(dispatch.ToDto());
    }
}

public sealed class GetNotificationDispatchListHandler
    : IRequestHandler<GetNotificationDispatchListQuery, Response<IReadOnlyList<NotificationDispatchListItemDto>>>
{
    private readonly INotificationDispatchRepository _repository;
    public GetNotificationDispatchListHandler(INotificationDispatchRepository repository) => _repository = repository;
    public async Task<Response<IReadOnlyList<NotificationDispatchListItemDto>>> Handle(GetNotificationDispatchListQuery request, CancellationToken ct)
    {
        NotificationDispatchStatus? status = null;
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<NotificationDispatchStatus>(request.Status, ignoreCase: true, out var parsedStatus)
                || !Enum.IsDefined(parsedStatus))
            {
                return Response<IReadOnlyList<NotificationDispatchListItemDto>>.Fail("Unknown dispatch status filter.", 400);
            }

            status = parsedStatus;
        }

        if (request.QueuedFrom is not null && request.QueuedTo is not null && request.QueuedFrom > request.QueuedTo)
        {
            return Response<IReadOnlyList<NotificationDispatchListItemDto>>.Fail("QueuedFrom cannot be later than QueuedTo.", 400);
        }

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var templateKey = string.IsNullOrWhiteSpace(request.TemplateKey)
            ? null
            : NotificationParsing.NormalizeTemplateKey(request.TemplateKey);

        var items = await _repository.ListByTenantAsync(
            request.TenantId,
            (page - 1) * pageSize,
            pageSize,
            status,
            request.QueuedFrom,
            request.QueuedTo,
            templateKey,
            ct);
        return Response<IReadOnlyList<NotificationDispatchListItemDto>>.Success(items.Select(x => x.ToListItemDto()).ToArray());
    }
}
