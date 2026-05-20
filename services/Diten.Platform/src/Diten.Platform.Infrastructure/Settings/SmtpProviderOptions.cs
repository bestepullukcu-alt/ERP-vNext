namespace Diten.Platform.Infrastructure.Settings;

public sealed class SmtpProviderOptions
{
    public const string SectionName = "MessagingProviders:Smtp";

    public int SendTimeoutSeconds { get; set; } = 30;
    public int MaxRecipientsPerMessage { get; set; } = 100;
    public bool AllowInsecureTlsInDevelopment { get; set; }
    public int SubjectLogPreviewLength { get; set; } = 80;
}
