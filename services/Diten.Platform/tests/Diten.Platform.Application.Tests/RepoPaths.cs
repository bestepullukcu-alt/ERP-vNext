namespace Diten.Platform.Application.Tests;

/// <summary>
/// Where the repository is, worked out by LOOKING rather than by counting.
///
/// ⚠ WHY THIS EXISTS. Four tests in this project reached their own source tree by climbing exactly five
/// directories out of <c>AppContext.BaseDirectory</c>:
///
///     Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", …)
///
/// That number is a guess about the build layout — bin/Debug/net8.0 under a project under tests/ under the
/// service — and it is right in exactly one checkout shape. Run the same suite from a git worktree, a
/// different configuration, or a nested output path and the climb lands somewhere else; the file is not
/// found, and the test fails for a reason that has nothing to do with what it asserts. Measured 2026-08-27:
/// this is what stopped the GSKU team from running the full suite while their own work was green.
///
/// Walking UP until a known repository marker appears has no such assumption. It is the pattern
/// <c>TenantArchitecture.ArchitectureTests</c> has used since the Mongo guard was written, and it survives
/// every layout above because it asks the filesystem instead of predicting it.
///
/// ⚠ AND THE MARKER IS AGENTS.md, NOT <c>.git</c>. Six other tests in this project already walked up
/// correctly — and still failed in a worktree, because they looked for <c>.git</c> with
/// <c>Directory.Exists</c>. In a normal clone <c>.git</c> is a directory; IN A GIT WORKTREE IT IS A FILE
/// containing a <c>gitdir:</c> pointer. So the walk ran off the top of the filesystem and threw "Could not
/// locate the repository root". That is the same mistake as counting five directories, wearing a better
/// disguise: both encode an assumption about the checkout instead of testing for something that is actually
/// there. AGENTS.md is a tracked file, so it exists in every checkout shape, worktrees included.
/// </summary>
public static class RepoPaths
{
    /// <summary>The repository root, found by walking up to the AGENTS.md marker.</summary>
    public static string Root()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException(
            $"Repo root not found above '{AppContext.BaseDirectory}' — no AGENTS.md on any parent.");
    }

    /// <summary>The repository's <c>services</c> directory.</summary>
    public static string Services() => Path.Combine(Root(), "services");

    /// <summary>The repository's <c>frontend</c> directory.</summary>
    public static string Frontend() => Path.Combine(Root(), "frontend");

    /// <summary>A path inside Diten.Platform.Application's source tree.</summary>
    public static string ApplicationSource(params string[] segments)
        => Path.Combine(
            new[] { Root(), "services", "Diten.Platform", "src", "Diten.Platform.Application" }
                .Concat(segments).ToArray());
}
