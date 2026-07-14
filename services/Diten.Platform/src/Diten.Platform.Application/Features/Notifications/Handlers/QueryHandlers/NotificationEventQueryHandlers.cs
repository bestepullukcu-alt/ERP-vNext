using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Notifications.Queries;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Notifications.Handlers.QueryHandlers;

public sealed class GetNotificationEventListHandler
    : IRequestHandler<GetNotificationEventListQuery, Response<IReadOnlyList<NotificationEventDefinitionDto>>>
{
    private readonly INotificationEventDefinitionRepository _repository;
    public GetNotificationEventListHandler(INotificationEventDefinitionRepository repository) => _repository = repository;

    public async Task<Response<IReadOnlyList<NotificationEventDefinitionDto>>> Handle(GetNotificationEventListQuery request, CancellationToken ct)
    {
        NotificationChannelCode? channel = null;
        if (!string.IsNullOrWhiteSpace(request.Channel))
        {
            if (!NotificationParsing.TryParseChannel(request.Channel, out var c))
                return Response<IReadOnlyList<NotificationEventDefinitionDto>>.Fail("Unknown channel filter.", 400);
            channel = c;
        }

        NotificationEventStatus? status = null;
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<NotificationEventStatus>(request.Status, ignoreCase: true, out var s) || !Enum.IsDefined(s))
                return Response<IReadOnlyList<NotificationEventDefinitionDto>>.Fail("Unknown status filter.", 400);
            status = s;
        }

        NotificationEventUsageType? usageType = null;
        if (!string.IsNullOrWhiteSpace(request.UsageType))
        {
            if (!Enum.TryParse<NotificationEventUsageType>(request.UsageType, ignoreCase: true, out var u) || !Enum.IsDefined(u))
                return Response<IReadOnlyList<NotificationEventDefinitionDto>>.Fail("Unknown usageType filter.", 400);
            usageType = u;
        }

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 500);
        var items = await _repository.ListAsync(
            string.IsNullOrWhiteSpace(request.OwnerModuleId) ? null : request.OwnerModuleId,
            channel, status, request.CanTenantOverride, usageType,
            (page - 1) * pageSize, pageSize, ct);

        return Response<IReadOnlyList<NotificationEventDefinitionDto>>.Success(items.Select(x => x.ToDto()).ToArray());
    }
}

public sealed class GetNotificationEventByCodeHandler
    : IRequestHandler<GetNotificationEventByCodeQuery, Response<NotificationEventDefinitionDto>>
{
    private readonly INotificationEventDefinitionRepository _repository;
    public GetNotificationEventByCodeHandler(INotificationEventDefinitionRepository repository) => _repository = repository;

    public async Task<Response<NotificationEventDefinitionDto>> Handle(GetNotificationEventByCodeQuery request, CancellationToken ct)
    {
        var entity = await _repository.GetByEventCodeAsync(request.EventCode, ct);
        return entity is null
            ? Response<NotificationEventDefinitionDto>.Fail("Notification event not found.", 404)
            : Response<NotificationEventDefinitionDto>.Success(entity.ToDto());
    }
}

public sealed class GetNotificationEventTemplateContractHandler
    : IRequestHandler<GetNotificationEventTemplateContractQuery, Response<NotificationEventTemplateContractDto>>
{
    private readonly INotificationEventDefinitionRepository _repository;
    public GetNotificationEventTemplateContractHandler(INotificationEventDefinitionRepository repository) => _repository = repository;

    public async Task<Response<NotificationEventTemplateContractDto>> Handle(GetNotificationEventTemplateContractQuery request, CancellationToken ct)
    {
        var entity = await _repository.GetByEventCodeAsync(request.EventCode, ct);
        return entity is null
            ? Response<NotificationEventTemplateContractDto>.Fail("Notification event not found.", 404)
            : Response<NotificationEventTemplateContractDto>.Success(entity.ToTemplateContractDto());
    }
}

public sealed class GetActiveTemplateSlotsHandler
    : IRequestHandler<GetActiveTemplateSlotsQuery, Response<IReadOnlyList<NotificationEventTemplateSlotDto>>>
{
    private readonly INotificationEventDefinitionRepository _repository;
    public GetActiveTemplateSlotsHandler(INotificationEventDefinitionRepository repository) => _repository = repository;

    public async Task<Response<IReadOnlyList<NotificationEventTemplateSlotDto>>> Handle(GetActiveTemplateSlotsQuery request, CancellationToken ct)
    {
        // ListActiveAsync returns only Status == Active; Deprecated/Archived are excluded by construction.
        var items = await _repository.ListActiveAsync(ct);
        return Response<IReadOnlyList<NotificationEventTemplateSlotDto>>.Success(items.Select(x => x.ToSlotDto()).ToArray());
    }
}
