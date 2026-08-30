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
        string[] reviewedAuditOutboxMembers =
        [
            "Diten.Platform.Infrastructure/Persistence/Models/AuditOutboxMessage.cs:CreatedAtUtc",
            "Diten.Platform.Infrastructure/Persistence/Models/AuditOutboxMessage.cs:NextAttemptAtUtc"
        ];
        var actualReviewedMembers = inventory
            .Where(item => reviewedAuditOutboxMembers.Contains(item, StringComparer.Ordinal))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(reviewedAuditOutboxMembers, actualReviewedMembers);

        // The 410-item remainder is current-main mechanical debt, not semantically reviewed by FU02.
        var grandfatheredBaseline = inventory
            .Where(item => !reviewedAuditOutboxMembers.Contains(item, StringComparer.Ordinal))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(410, grandfatheredBaseline.Length);
        Assert.Equal(
            "B29B5768362B1035E2412C61B6F25AF95254AF9FDC7C9ED17925F7229EEC9200",
            Fingerprint(grandfatheredBaseline));

        Assert.Equal(412, inventory.Length);
        Assert.Equal(
            "7B1C8E30FF78CD35C46AF2EB5D9C4BF4B6C57FD074DE972E80158F21F82DA46B",
            Fingerprint(inventory));
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

    private static string Fingerprint(IEnumerable<string> values) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", values))));

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
