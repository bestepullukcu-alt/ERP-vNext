namespace Diten.AuthService.Application.Tests.Testing;

/// <summary>
/// Console.Out and Console.Error are process-wide. A test file that redirects them must put its class in a collection
/// that never runs in parallel (ConsoleCaptureCollection, or AccountKindAcceptance). In parallel, another class's host
/// or redirect takes the writer and the capture loses lines: the e-sign log test lost its request line in about one
/// CI run in three (2026-11-03) until it joined ConsoleCaptureCollection.
/// </summary>
public sealed class ConsoleRedirectGateGuardTests
{
    // Split so this file's own text is not read as a redirect.
    private const string SetOut = "Console.SetOut" + "(";
    private const string SetError = "Console.SetError" + "(";

    private static readonly string[] SerialCollections =
    [
        "[Collection(Diten.AuthService.Application.Tests.ServiceIdentityTokens.ConsoleCaptureCollection.Name)]",
        "[Collection(ConsoleCaptureCollection.Name)]",
        "[Collection(\"AccountKindAcceptance\")]"
    ];

    [Fact]
    public void Every_test_file_that_redirects_the_console_runs_in_a_serial_collection()
    {
        var root = ProjectRoot();
        var redirecting = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                           && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(path => File.ReadAllText(path) is var text
                           && (text.Contains(SetOut, StringComparison.Ordinal) || text.Contains(SetError, StringComparison.Ordinal)))
            .ToList();

        // Presence: the five known redirecting files must be found, or the scan is blind.
        Assert.True(redirecting.Count >= 5, $"only {redirecting.Count} redirecting files found — the scan is blind");

        var parallel = redirecting
            .Where(path => File.ReadAllText(path) is var text && !SerialCollections.Any(c => text.Contains(c, StringComparison.Ordinal)))
            .Select(path => Path.GetRelativePath(root, path))
            .ToList();
        Assert.True(parallel.Count == 0, "console redirect outside a serial collection: " + string.Join(", ", parallel));
    }

    private static string ProjectRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Diten.AuthService.Application.Tests.csproj")))
            current = current.Parent;
        return current?.FullName ?? throw new InvalidOperationException("Test project root not found.");
    }
}
