using Diten.Platform.Application.Features.Tenants.Commands;
using FluentValidation;

namespace Diten.Platform.Application.Features.Tenants.Validators;

public sealed class UpdateTenantBrandingCommandValidator : AbstractValidator<UpdateTenantBrandingCommand>
{
    private static readonly string[] AllowedPrefixes =
    [
        "data:image/png;base64,",
        "data:image/jpeg;base64,",
        "data:image/webp;base64,",
        "data:image/svg+xml;base64,",
        "data:image/x-icon;base64,",
        "data:image/vnd.microsoft.icon;base64,"
    ];

    public UpdateTenantBrandingCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.Request).NotNull();

        When(x => x.Request != null, () =>
        {
            RuleFor(x => x.Request.LogoDataUrl)
                .Must(BeNullOrSupportedImageDataUrl)
                .WithMessage("LogoDataUrl must be a supported image data URL.")
                .MaximumLength(1_400_000)
                .When(x => !string.IsNullOrWhiteSpace(x.Request.LogoDataUrl));

            RuleFor(x => x.Request.FaviconDataUrl)
                .Must(BeNullOrSupportedImageDataUrl)
                .WithMessage("FaviconDataUrl must be a supported image data URL.")
                .MaximumLength(350_000)
                .When(x => !string.IsNullOrWhiteSpace(x.Request.FaviconDataUrl));
        });
    }

    private static bool BeNullOrSupportedImageDataUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        return AllowedPrefixes.Any(prefix => value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }
}
