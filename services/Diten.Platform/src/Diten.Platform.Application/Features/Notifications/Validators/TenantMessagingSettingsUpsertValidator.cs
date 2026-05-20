using Diten.Platform.Application.Features.Notifications.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.Notifications.Validators;

public sealed class UpsertTenantMessagingSettingsValidator : AbstractValidator<UpsertTenantMessagingSettingsCommand>
{
    public UpsertTenantMessagingSettingsValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty()
            .WithMessage("Target tenant id is required.");

        RuleFor(x => x.Request.ProviderCode)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(value => NotificationParsing.TryParseProvider(value, out _))
            .WithMessage("ProviderCode must be Fake, Smtp, or SendGrid.");

        RuleFor(x => x.Request.SenderEmail)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);

        RuleFor(x => x.Request.SenderName)
            .MaximumLength(160);

        RuleFor(x => x.Request.ReplyToEmail)
            .EmailAddress()
            .MaximumLength(256)
            .When(x => !string.IsNullOrWhiteSpace(x.Request.ReplyToEmail));

        RuleFor(x => x.Request.Host)
            .NotEmpty()
            .When(x => IsSmtp(x.Request.ProviderCode))
            .WithMessage("Host is required for SMTP provider.");

        RuleFor(x => x.Request.Host)
            .MaximumLength(256);

        RuleFor(x => x.Request.Port)
            .NotNull()
            .When(x => IsSmtp(x.Request.ProviderCode))
            .WithMessage("Port is required for SMTP provider.");

        RuleFor(x => x.Request.Port)
            .InclusiveBetween(1, 65535)
            .When(x => x.Request.Port.HasValue);

        RuleFor(x => x.Request.ApiBaseUrl)
            .Must(value => Uri.TryCreate(value, UriKind.Absolute, out var uri)
                           && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            .When(x => !string.IsNullOrWhiteSpace(x.Request.ApiBaseUrl))
            .WithMessage("ApiBaseUrl must be an absolute HTTP/HTTPS URL.");

        RuleFor(x => x.Request.CredentialSecretRef)
            .MaximumLength(512)
            .Must(value => !NotificationParsing.LooksLikeRawSecret(value))
            .WithMessage("CredentialSecretRef must be a secret reference and must not contain a raw password/API key/token.");

        RuleFor(x => x.Request.FallbackPolicy)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(value => NotificationParsing.TryParseFallbackPolicy(value, out _))
            .WithMessage("FallbackPolicy must be UsePlatformDefault, DisableSending, or FailFast.");
    }

    private static bool IsSmtp(string providerCode) =>
        NotificationParsing.TryParseProvider(providerCode, out var provider) &&
        provider == Domain.Enums.MessagingProviderCode.Smtp;
}
