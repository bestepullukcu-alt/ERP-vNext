using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Domain.Entities.Notifications;

public sealed class TemplateVariableDefinition
{
    public string Name { get; set; } = string.Empty;
    public TemplateVariableType Type { get; set; } = TemplateVariableType.String;
    public bool IsRequired { get; set; } = true;
}
