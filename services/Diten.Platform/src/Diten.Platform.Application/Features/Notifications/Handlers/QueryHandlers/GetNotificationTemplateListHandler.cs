using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Notifications.Queries;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Notifications.Handlers.QueryHandlers;

public sealed class GetNotificationTemplateListHandler
    : IRequestHandler<GetNotificationTemplateListQuery, Response<IReadOnlyList<NotificationTemplateDto>>>
{
    private readonly INotificationTemplateRepository _repository;

    public GetNotificationTemplateListHandler(INotificationTemplateRepository repository) => _repository = repository;

    public async Task<Response<IReadOnlyList<NotificationTemplateDto>>> Handle(GetNotificationTemplateListQuery request, CancellationToken ct)
    {
        NotificationTemplateStatus? status = null;
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!NotificationParsing.TryParseTemplateStatus(request.Status, out var parsedStatus))
            {
                return Response<IReadOnlyList<NotificationTemplateDto>>.Fail("Unknown template status filter.", 400);
            }

            status = parsedStatus;
        }

        NotificationChannelCode? channel = null;
        if (!string.IsNullOrWhiteSpace(request.Channel))
        {
            if (!NotificationParsing.TryParseChannel(request.Channel, out var parsedChannel))
            {
                return Response<IReadOnlyList<NotificationTemplateDto>>.Fail("Unknown channel filter.", 400);
            }

            channel = parsedChannel;
        }

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var locale = string.IsNullOrWhiteSpace(request.Locale) ? null : NotificationParsing.NormalizeLocale(request.Locale);
        var templateKey = string.IsNullOrWhiteSpace(request.TemplateKey) ? null : NotificationParsing.NormalizeTemplateKey(request.TemplateKey);

        var items = await _repository.ListAsync(
            request.TenantId,
            request.IsPlatformDefault,
            status,
            locale,
            channel,
            templateKey,
            (page - 1) * pageSize,
            pageSize,
            ct);

        return Response<IReadOnlyList<NotificationTemplateDto>>.Success(items.Select(x => x.ToDto()).ToArray());
    }
}
