using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Infrastructure.Settings;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Configurations;

/// <summary>
/// The platform-default messaging settings row — <b>where email is actually sent from.</b>
///
/// <para><b>The defect this closes.</b> <c>QueueEmailNotificationHandler</c> asks
/// <c>ITenantMessagingSettingsResolver</c> for settings before it does anything else. With no tenant row it falls
/// back to the platform default; with no platform default either it refuses with
/// "Platform default messaging settings were not found or are disabled." Nothing in the product ever created that
/// row — <c>UpsertTenantMessagingSettingsHandler</c> is the only writer and it needs an operator. So on a fresh
/// database EVERY notification from EVERY producer died at the first line of the handler, before templates,
/// locale, provider or recipients were ever consulted.</para>
///
/// <para><b>Why config was not enough, and why this fixes that too.</b> <c>appsettings</c> has had a populated
/// <c>Smtp</c> block the whole time — host, port, credentials, from-address — and it reads exactly like the thing
/// that configures email. It does not: it feeds <c>SmtpMessagingProvider</c>, which is only reached AFTER the
/// settings record resolves. Two sources of truth, one of them silent. This seed makes the <c>Smtp</c> block mean
/// what it looks like it means by deriving the record from it.</para>
///
/// <para><b>Idempotent, and never an override.</b> A platform default that already exists — including one an
/// operator created or edited through the notifications screen — is left exactly alone. This only fills the hole.
/// </para>
/// </summary>
public static class NotificationMessagingSettingsSeed
{
    /// <summary>
    /// The credential is stored as a REFERENCE, never a value. <c>ConfigurationSecretsProvider</c> resolves this
    /// key from configuration in development and demands the <c>Smtp__Password</c> environment variable in
    /// production, so the seeded row carries no secret material on disk in either environment.
    /// </summary>
    public const string CredentialSecretRef = "Smtp:Password";

    public static async Task EnsureSeededAsync(
        IMongoDatabase database,
        SmtpOptions smtp,
        CancellationToken ct = default)
    {
        var collection = database.GetCollection<TenantMessagingSettings>("notification_tenant_messaging_settings");

        // Same predicate TenantMessagingSettingsRepository.GetPlatformDefaultAsync uses. Matching it exactly is
        // the point: seeding a row the resolver would not find is the same as seeding nothing.
        var exists = await collection.Find(x =>
                x.IsDeleted == false
                && x.TenantId == null
                && x.IsPlatformDefault)
            .AnyAsync(ct);

        if (exists)
        {
            return;
        }

        await collection.InsertOneAsync(Build(smtp), cancellationToken: ct);
    }

    internal static TenantMessagingSettings Build(SmtpOptions smtp)
    {
        /*
         * SMTP when it is configured and switched on, Fake otherwise — and Fake is a deliberate choice rather than
         * a refusal. A Fake provider still resolves settings, still finds a template, still WRITES the dispatch
         * record and still publishes the queued event; the mail simply goes nowhere. That is an environment an
         * operator can inspect. Refusing instead leaves the notification tables empty, which is precisely the
         * state that made this defect take two rounds to find.
         */
        var useSmtp = smtp.Enabled && !string.IsNullOrWhiteSpace(smtp.Host) && !string.IsNullOrWhiteSpace(smtp.FromEmail);

        return new TenantMessagingSettings
        {
            TenantId = null,
            IsPlatformDefault = true,
            ProviderCode = useSmtp ? MessagingProviderCode.Smtp : MessagingProviderCode.Fake,
            SenderEmail = string.IsNullOrWhiteSpace(smtp.FromEmail) ? "no-reply@diten.local" : smtp.FromEmail.Trim(),
            SenderName = string.IsNullOrWhiteSpace(smtp.FromName) ? null : smtp.FromName.Trim(),
            Host = useSmtp ? smtp.Host.Trim() : null,
            Port = useSmtp ? smtp.Port : null,
            UseSsl = smtp.EnableSsl,
            CredentialSecretRef = useSmtp ? CredentialSecretRef : null,
            IsEnabled = true,
            FallbackPolicy = NotificationFallbackPolicy.UsePlatformDefault,
            CreatedBy = "system.seed",
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}
