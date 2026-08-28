using Xunit;

namespace Diten.Platform.Application.Tests.Navigation;

// FEAT-NAV-L10N — this file used to ALSO assert that every nav code had a resx key in all seven tenant languages,
// from module/domain/page lists TYPED BY HAND right here. That is why three live defects shipped green on
// 2026-08-10: `tasks`, TASK_RECURRENCE_RULES and the work-aggregation nav page were simply never added to those
// lists, so nothing asserted them. A guard whose expectation set is maintained by memory guards nothing.
//
// The key-coverage assertions now live in frontend/Diten.Web.Tests/Navigation/NavManifestL10nGuardTests, which
// DERIVES the expected set from the manifest provider sources and applies the shipping
// NavNameLocalizer.Normalize — no hand list, and no second copy of the key transform (this file used to carry
// one, which is the same drift risk one level down).
//
// What stays here: the precedence pin. The localizer lives in the web app and cannot be referenced from the
// Platform test assembly, so its contract is asserted against its SOURCE — the same approach as the
// password-bridge AuthGateway guard.
public sealed class NavL10nContractTests
{
    //   • an OVERRIDE is rendered as-typed (isOverride short-circuits before any lookup);
    //   • a missing key (ResourceNotFound) falls back to the server default name — never the raw key.
    [Fact]
    public void NavNameLocalizer_encodes_the_override_and_fallback_precedence()
    {
        var source = File.ReadAllText(Path.Combine(
            RepoRoot(), "frontend", "Diten.Web", "Services", "Navigation", "NavNameLocalizer.cs"));

        Assert.Contains("isOverride ? serverName", source);       // override → as-typed, no localization
        Assert.Contains("ResourceNotFound", source);              // missing key detected
        Assert.Contains("? serverName", source);                  // ...and falls back to the server default
        Assert.Contains("Nav.Domain.", source);
        Assert.Contains("Nav.Module.", source);
        Assert.Contains("Nav.Page.", source);
        // The lookup MUST normalize the code before building the key (else casing/separator drift silently misses).
        Assert.Contains("Normalize(code)", source);
        Assert.Contains("char.IsLetterOrDigit", source);
        Assert.Contains("ToUpperInvariant", source);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Resources")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repo root (frontend/Diten.Web/Resources) from the test output directory.");
    }
}
