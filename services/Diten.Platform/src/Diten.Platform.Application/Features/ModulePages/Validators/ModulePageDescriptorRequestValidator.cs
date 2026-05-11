using Diten.Platform.Domain.Enums;
using FluentValidation;

namespace Diten.Platform.Application.Features.ModulePages.Validators;

public abstract class ModulePageDescriptorRequestValidator<T> : AbstractValidator<T>
{
    protected ModulePageDescriptorRequestValidator(
        Func<T, string?> pageCode,
        Func<T, string?> displayName,
        Func<T, string?> routePath,
        Func<T, string?> requiredPermission,
        Func<T, string?> parentPageCode,
        Func<T, string?> pageType,
        Func<T, string?> status,
        Func<T, int?> sortOrder)
    {
        RuleFor(x => pageCode(x))
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Sayfa kodu zorunludur.")
            .Must(value =>
            {
                var normalized = ModulePageDescriptorNormalizer.NormalizePageCode(value);
                return normalized.Length is >= 3 and <= 100
                    && normalized.Any(char.IsAsciiLetter)
                    && normalized.All(ch => char.IsAsciiLetterOrDigit(ch) || ch == '_');
            }).WithMessage("Sayfa kodu otomatik oluşturulamadı.");

        RuleFor(x => displayName(x))
            .NotEmpty().WithMessage("Görünen ad zorunludur.")
            .MaximumLength(200).WithMessage("Görünen ad 200 karakteri aşamaz.");

        RuleFor(x => routePath(x))
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Rota yolu zorunludur.")
            .Must(value => (value ?? string.Empty).Trim().StartsWith("/", StringComparison.Ordinal))
            .WithMessage("Rota /PPM/Projects formatında olmalıdır.")
            .Must(value => IsCanonicalRoutePath(value))
            .WithMessage("Rota /PPM/Projects formatında olmalıdır.")
            .MaximumLength(300).WithMessage("Rota yolu 300 karakteri aşamaz.");

        RuleFor(x => requiredPermission(x))
            .Must(value => string.IsNullOrWhiteSpace(value) || ModulePageDescriptorNormalizer.NormalizePermission(value).Length <= 200)
            .WithMessage("Permission 200 karakteri aşamaz.")
            .Must(value => IsCanonicalPermission(value))
            .When(x => !string.IsNullOrWhiteSpace(requiredPermission(x)))
            .WithMessage("İzin anahtarı ppm.projects.view formatında olmalıdır.");

        RuleFor(x => parentPageCode(x))
            .Must(value =>
            {
                if (string.IsNullOrWhiteSpace(value)) return true;
                var normalized = ModulePageDescriptorNormalizer.NormalizePageCode(value);
                return normalized.Length is >= 3 and <= 100
                    && normalized.Any(char.IsAsciiLetter)
                    && normalized.All(ch => char.IsAsciiLetterOrDigit(ch) || ch == '_');
            }).WithMessage("Üst sayfa kodu geçerli formatta olmalıdır.");

        RuleFor(x => pageType(x))
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Sayfa tipi zorunludur.")
            .Must(value => Enum.GetNames<ModulePageType>().Contains(value, StringComparer.Ordinal))
            .WithMessage("Geçerli sayfa tipi seçiniz.");

        RuleFor(x => status(x))
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Durum zorunludur.")
            .Must(value => Enum.GetNames<ModulePageStatus>().Contains(value, StringComparer.Ordinal))
            .WithMessage("Geçerli durum seçiniz.");

        RuleFor(x => sortOrder(x))
            .GreaterThanOrEqualTo(0).When(x => sortOrder(x).HasValue)
            .WithMessage("Sıralama değeri 0 veya daha büyük olmalıdır.");
    }

    private static bool IsCanonicalPermission(string? value)
    {
        var permission = ModulePageDescriptorNormalizer.NormalizePermission(value);
        var parts = permission.Split('.', StringSplitOptions.RemoveEmptyEntries);

        return parts.Length == 3
            && parts.All(part =>
                part.Length > 0
                && char.IsAsciiLetterLower(part[0])
                && part.All(ch => char.IsAsciiLetterOrDigit(ch) || ch == '-'));
    }

    private static bool IsCanonicalRoutePath(string? value)
    {
        var rawRoute = (value ?? string.Empty).Trim();
        if (rawRoute.Length == 0 || rawRoute.Any(char.IsWhiteSpace) || !rawRoute.StartsWith("/", StringComparison.Ordinal))
        {
            return false;
        }

        var route = ModulePageDescriptorNormalizer.NormalizeRoutePath(rawRoute);
        var parts = route.Split('/', StringSplitOptions.RemoveEmptyEntries);

        return parts.Length >= 2
            && parts.All(part =>
                part.Length > 0
                && part.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '-' or '{' or '}' or ':' or '_')
                && (char.IsAsciiLetterUpper(part[0]) || part[0] is '{' or ':'));
    }
}
