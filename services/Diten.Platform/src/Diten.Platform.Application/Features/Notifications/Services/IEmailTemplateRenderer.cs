using Diten.Platform.Application.Common;
using Diten.Platform.Domain.Entities.Notifications;

namespace Diten.Platform.Application.Features.Notifications.Services;

public interface IEmailTemplateRenderer
{
    Response<RenderedEmailTemplateDto> Render(
        NotificationTemplate template,
        IReadOnlyDictionary<string, object?> variables);
}
