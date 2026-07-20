using Microsoft.Extensions.Localization;

namespace Diten.Web.Services.Navigation;

// FEAT-NAV-L10N — generic, code-keyed localization for the tenant navigation (sidebar + Ctrl+K), shared so both
// render identical labels. Each node is localized by a resx key derived from its STABLE code
// (Nav.Domain.{code} / Nav.Module.{code} / Nav.Page.{code}) in the current request culture — NOT a hardcoded map,
// so any future self-registered module localizes by shipping its keys. Precedence:
//   • a tenant OVERRIDE (free text) is rendered AS-TYPED, never localized;
//   • a DEFAULT name is localized by code; a missing key (ResourceNotFound) falls back to the server default name
//     — never the raw key.
public interface INavNameLocalizer
{
    string Domain(string? domainCode, string serverName, bool isOverride);
    string Module(string? moduleCode, string serverName, bool isOverride);
    string Page(string? pageCode, string serverName);
}

public sealed class NavNameLocalizer : INavNameLocalizer
{
    private const string DomainPrefix = "Nav.Domain.";
    private const string ModulePrefix = "Nav.Module.";
    private const string PagePrefix = "Nav.Page.";

    private readonly IStringLocalizer<SharedResource> _localizer;

    public NavNameLocalizer(IStringLocalizer<SharedResource> localizer)
    {
        _localizer = localizer;
    }

    public string Domain(string? domainCode, string serverName, bool isOverride) =>
        isOverride ? serverName : LocalizeOrDefault(DomainPrefix, domainCode, serverName);

    public string Module(string? moduleCode, string serverName, bool isOverride) =>
        isOverride ? serverName : LocalizeOrDefault(ModulePrefix, moduleCode, serverName);

    public string Page(string? pageCode, string serverName) =>
        LocalizeOrDefault(PagePrefix, pageCode, serverName);

    private string LocalizeOrDefault(string prefix, string? code, string serverName)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return serverName;
        }

        // Normalize the code to its canonical key form BEFORE lookup — the running catalog emits codes in
        // inconsistent casing/separators ("access-governance", "ACCESS-GOVERNANCE", "PlatformSharedServices",
        // "PLATFORM-SHARED-SERVICES"), so a literal key would silently miss and fall back to English. resx keys are
        // authored in this same normalized form.
        var localized = _localizer[prefix + Normalize(code)];
        return localized.ResourceNotFound || string.IsNullOrWhiteSpace(localized.Value)
            ? serverName
            : localized.Value;
    }

    // Canonical key form: uppercase, every non-alphanumeric char removed. Absorbs the catalog's casing/separator
    // drift so a resx key matches regardless of how the code is emitted:
    //   "access-governance" / "ACCESS-GOVERNANCE" / "Access Governance" → "ACCESSGOVERNANCE"
    //   "PlatformSharedServices" / "PLATFORM-SHARED-SERVICES"           → "PLATFORMSHAREDSERVICES"
    //   "ROLE_PERMISSIONS"                                              → "ROLEPERMISSIONS"
    // Must stay identical to the normalize() the guard test (NavL10nContractTests) applies to its expected codes.
    public static string Normalize(string code)
    {
        var buffer = new char[code.Length];
        var length = 0;
        foreach (var ch in code)
        {
            if (char.IsLetterOrDigit(ch))
            {
                buffer[length++] = char.ToUpperInvariant(ch);
            }
        }

        return new string(buffer, 0, length);
    }
}
