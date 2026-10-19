using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Domain.Entities.Notifications;

public sealed class NotificationTemplate : BaseEntity
{
    public Guid? TenantId { get; set; }
    public bool IsPlatformDefault { get; set; }
    public string TemplateKey { get; set; } = string.Empty;
    public NotificationChannelCode Channel { get; set; } = NotificationChannelCode.Email;
    public string Locale { get; set; } = "en";
    public string SubjectTemplate { get; set; } = string.Empty;
    public string BodyHtmlTemplate { get; set; } = string.Empty;
    public string? BodyTextTemplate { get; set; }
    public List<TemplateVariableDefinition> Variables { get; set; } = [];
    public NotificationTemplateStatus Status { get; set; } = NotificationTemplateStatus.Draft;
    public string? SemanticVersion { get; set; }

    /// <summary>
    /// BL-454 — what this template hands to the e-mail shell besides its body. Null on every template written
    /// before the shell existed and on any template that has nothing to add: the body is then framed with the
    /// rendered subject as its heading and no action.
    /// </summary>
    public NotificationTemplateShell? Shell { get; set; }
}

/// <summary>
/// BL-454 — the parts of the e-mail shell a template fills. Every field is optional and every one is CONTENT: how
/// the heading, the table or the button look is the shell's business (<c>Diten.BuildingBlocks.Email.EmailShell</c>).
/// <c>{{Variable}}</c> tokens are rendered as plain text and encoded by the shell.
/// </summary>
public sealed class NotificationTemplateShell
{
    public string? HeadingTemplate { get; set; }

    /// <summary>The information table. A row whose value renders empty is left out.</summary>
    public List<NotificationTemplateShellRow> InfoRows { get; set; } = [];

    public string? ActionLabel { get; set; }

    /// <summary>The NAME of the variable that carries the action's address — never an address typed into a template.</summary>
    public string? ActionUrlVariable { get; set; }

    /// <summary>The closing line inside the card ("You received this because…").</summary>
    public string? FootnoteTemplate { get; set; }
}

public sealed class NotificationTemplateShellRow
{
    public string Label { get; set; } = string.Empty;
    public string ValueTemplate { get; set; } = string.Empty;
}
