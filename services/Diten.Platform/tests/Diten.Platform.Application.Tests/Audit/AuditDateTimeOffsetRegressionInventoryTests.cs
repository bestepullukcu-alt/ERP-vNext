using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace Diten.Platform.Application.Tests.Audit;

public sealed class AuditDateTimeOffsetRegressionInventoryTests
{
    private static readonly Regex MemberPattern = new(
        @"\bDateTimeOffset\??\s+([A-Za-z_][A-Za-z0-9_]*)\s*\{",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    [Fact]
    public void PersistedDateTimeOffsetInventory_HasNoUnreviewedSurfaceChange()
    {
        var inventory = Inventory();
        var fingerprint = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", inventory))));

        Assert.Contains(
            inventory,
            item => item.EndsWith("Persistence/Models/AuditOutboxMessage.cs:NextAttemptAtUtc", StringComparison.Ordinal));
        Assert.Contains(
            inventory,
            item => item.EndsWith("Persistence/Models/AuditOutboxMessage.cs:CreatedAtUtc", StringComparison.Ordinal));
        Assert.Equal(212, inventory.Length);
        Assert.True(
            string.Equals(
                "BB2B9C2A7FE20CA64C0B229F677C5F0A7A30C70407ED0F41D3DF11D9090BE190",
                fingerprint,
                StringComparison.Ordinal),
            $"inventory count={inventory.Length}; fingerprint={fingerprint}");
    }

    [Fact]
    public void GlobalDateTimeOffsetSerializer_RemainsAbsent()
    {
        var source = string.Join(
            "\n",
            SourceFiles().Select(File.ReadAllText));

        Assert.DoesNotMatch(
            new Regex(@"RegisterSerializer\s*<\s*DateTimeOffset\s*>", RegexOptions.CultureInvariant),
            source);
        Assert.DoesNotContain("new DateTimeOffsetSerializer", source, StringComparison.Ordinal);
    }

    private static string[] Inventory()
    {
        var src = PlatformSourceRoot();
        return SourceFiles()
            .SelectMany(file => MemberPattern.Matches(File.ReadAllText(file))
                .Select(match => $"{Path.GetRelativePath(src, file).Replace('\\', '/')}:{match.Groups[1].Value}"))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<string> SourceFiles()
    {
        var src = PlatformSourceRoot();
        var roots = new[]
        {
            Path.Combine(src, "Diten.Platform.Domain"),
            Path.Combine(src, "Diten.Platform.Infrastructure")
        };

        return roots
            .SelectMany(root => Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                           && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .OrderBy(file => file, StringComparer.Ordinal);
    }

    private static string PlatformSourceRoot() => Path.Combine(
        RepoRoot(),
        "services",
        "Diten.Platform",
        "src");

    private static string RepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
