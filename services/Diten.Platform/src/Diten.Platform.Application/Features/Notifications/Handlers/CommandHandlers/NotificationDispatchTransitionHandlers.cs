using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Notifications.Commands;
using Diten.Platform.Contracts.Events.Notifications;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Notifications.Handlers.CommandHandlers;

public sealed class MarkNotificationDispatchSentHandler
    : IRequestHandler<MarkNotificationDispatchSentCommand, Response<NotificationDispatchDto>>
{
    private readonly INotificationDispatchRepository _repository;
    private readonly IEventBus _eventBus;
    public MarkNotificationDispatchSentHandler(INotificationDispatchRepository repository, IEventBus eventBus)
    {
        _repository = repository;
        _eventBus = eventBus;
    }
    public async Task<Response<NotificationDispatchDto>> Handle(MarkNotificationDispatchSentCommand request, CancellationToken ct)
    {
        var dispatch = await _repository.GetByIdForTenantAsync(request.TenantId, request.DispatchId, ct);
        if (dispatch is null) return Response<NotificationDispatchDto>.Fail("Notification dispatch not found.", 404);
        if (!dispatch.TryMarkSent(request.ProviderMessageId, DateTimeOffset.UtcNow)) return Response<NotificationDispatchDto>.Fail("Invalid dispatch status transition.", 409);
        await _repository.UpdateAsync(dispatch, ct);
        await _eventBus.PublishAsync(
            new NotificationDispatchSentV1(
                dispatch.Id,
                dispatch.TenantId,
                dispatch.TemplateKey,
                dispatch.Locale,
                dispatch.ProviderCode.ToString(),
                dispatch.ProviderMessageId,
                dispatch.RetryCount,
                dispatch.SentAt ?? DateTimeOffset.UtcNow,
                dispatch.CorrelationId),
            new EventPublishOptions { TenantId = dispatch.TenantId, CausationId = dispatch.CausationId },
            ct);
        return Response<NotificationDispatchDto>.Success(dispatch.ToDto());
    }
}

public sealed class MarkNotificationDispatchFailedHandler
    : IRequestHandler<MarkNotificationDispatchFailedCommand, Response<NotificationDispatchDto>>
{
    private readonly INotificationDispatchRepository _repository;
    private readonly IEventBus _eventBus;
    public MarkNotificationDispatchFailedHandler(INotificationDispatchRepository repository, IEventBus eventBus)
    {
        _repository = repository;
        _eventBus = eventBus;
    }
    public async Task<Response<NotificationDispatchDto>> Handle(MarkNotificationDispatchFailedCommand request, CancellationToken ct)
    {
        var dispatch = await _repository.GetByIdForTenantAsync(request.TenantId, request.DispatchId, ct);
        if (dispatch is null) return Response<NotificationDispatchDto>.Fail("Notification dispatch not found.", 404);
        if (request.RetryCount is { } retryCount)
        {
            dispatch.RetryCount = Math.Max(0, retryCount);
        }
        if (request.NextRetryAt is { } nextRetryAt)
        {
            dispatch.NextRetryAt = nextRetryAt;
        }
        if (!dispatch.TryMarkFailed(request.ErrorCode, request.ErrorMessage, DateTimeOffset.UtcNow)) return Response<NotificationDispatchDto>.Fail("Invalid dispatch status transition.", 409);
        await _repository.UpdateAsync(dispatch, ct);
        await _eventBus.PublishAsync(
            new NotificationDispatchFailedV1(
                dispatch.Id,
                dispatch.TenantId,
                dispatch.TemplateKey,
                dispatch.Locale,
                dispatch.ProviderCode.ToString(),
                dispatch.ErrorCode ?? request.ErrorCode,
                dispatch.RetryCount,
                dispatch.NextRetryAt,
                dispatch.FailedAt ?? DateTimeOffset.UtcNow,
                dispatch.CorrelationId),
            new EventPublishOptions { TenantId = dispatch.TenantId, CausationId = dispatch.CausationId },
            ct);
        return Response<NotificationDispatchDto>.Success(dispatch.ToDto());
    }
}

public sealed class CancelNotificationDispatchHandler
    : IRequestHandler<CancelNotificationDispatchCommand, Response<NotificationDispatchDto>>
{
    private readonly INotificationDispatchRepository _repository;
    private readonly IEventBus _eventBus;
    public CancelNotificationDispatchHandler(INotificationDispatchRepository repository, IEventBus eventBus)
    {
        _repository = repository;
        _eventBus = eventBus;
    }
    public async Task<Response<NotificationDispatchDto>> Handle(CancelNotificationDispatchCommand request, CancellationToken ct)
    {
        var dispatch = await _repository.GetByIdForTenantAsync(request.TenantId, request.DispatchId, ct);
        if (dispatch is null) return Response<NotificationDispatchDto>.Fail("Notification dispatch not found.", 404);
        if (!dispatch.TryCancel(DateTimeOffset.UtcNow)) return Response<NotificationDispatchDto>.Fail("Invalid dispatch status transition.", 409);
        await _repository.UpdateAsync(dispatch, ct);
        await _eventBus.PublishAsync(
            new NotificationDispatchCancelledV1(
                dispatch.Id,
                dispatch.TenantId,
                dispatch.TemplateKey,
                dispatch.Locale,
                dispatch.ProviderCode.ToString(),
                dispatch.UpdatedAt ?? DateTimeOffset.UtcNow,
                dispatch.CorrelationId),
            new EventPublishOptions { TenantId = dispatch.TenantId, CausationId = dispatch.CausationId },
            ct);
        return Response<NotificationDispatchDto>.Success(dispatch.ToDto());
    }
}
