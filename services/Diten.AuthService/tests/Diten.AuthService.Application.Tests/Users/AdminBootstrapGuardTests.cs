using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Diten.AuthService.Application.Tests.Users;

/// <summary>
/// WP-AUTH-ADMIN-BOOTSTRAP-01 (BL-534 + BL-556) — two source guards over the checkout.
/// <list type="number">
/// <item>A fixed bcrypt hash (a known password's hash in the source) lives in ONE file: the development-only account seed
/// (<c>DevAccountSeed.cs</c>). It used to sit in the start-up seed every environment ran.</item>
/// <item>No document (any <c>*.md</c>) carries the dev password in clear. The password is recognised by its SHA-256 only,
/// so this guard does not write it into the repository either.</item>
/// </list>
/// </summary>
public sealed class AdminBootstrapGuardTests
{
    private const string DevOnlySeed = "services/Diten.AuthService/src/Diten.AuthService.Persistence/Seed/DevAccountSeed.cs";

    // A whole bcrypt hash: $2a$ / $2b$ / $2y$, cost, 53 characters of salt and digest.
    private static readonly Regex BcryptHash = new(@"\$2[aby]\$\d{2}\$[./A-Za-z0-9]{53}", RegexOptions.Compiled);

    // SHA-256 of the password the old documents printed next to admin@diten.com.
    private const string KnownDevPasswordSha256 = "3eb3fe66b31e3b4d10fa70b5cad49c7112294af6ae4e476a1c405155d45aa121";

    private static readonly string[] SourceRoots = ["services", "gateway", "frontend"];

    [Fact]
    public void A_fixed_bcrypt_hash_is_only_in_the_development_account_seed()
    {
        var root = RepositoryRoot();
        var found = SourceRoots
            .Select(relative => Path.Combine(root, relative))
            .Where(Directory.Exists)
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
            .Where(path => !Relative(root, path).Split('/').Any(part => part is "bin" or "obj"))
            .Select(path => (Path: Relative(root, path), Count: BcryptHash.Matches(File.ReadAllText(path)).Count))
            .Where(file => file.Count > 0)
            .ToList();

        Assert.True(found.Count == 1 && found[0] == (DevOnlySeed, 1),
            "A fixed bcrypt hash must appear exactly once, in " + DevOnlySeed + ". Found:\n"
            + string.Join('\n', found.Select(file => $"{file.Path} ×{file.Count}")));
    }

    [Fact]
    public void No_document_carries_the_dev_password()
    {
        var root = RepositoryRoot();
        var documents = Directory.EnumerateFiles(root, "*.md", SearchOption.AllDirectories)
            // Judged on the path INSIDE the checkout (a worktree itself lives under .claude/worktrees/).
            .Where(path => !Relative(root, path).Split('/').Any(part => part is "bin" or "obj" or ".git" or ".claude" or "node_modules"))
            .ToList();
        Assert.Contains(documents, path => Relative(root, path) == ".antigravity/rules/dev-runbook.md"); // non-vacuity

        var carriers = documents.Where(path => Tokens(File.ReadAllText(path)).Any(IsKnownDevPassword)).Select(path => Relative(root, path)).ToList();

        Assert.True(carriers.Count == 0, "Documents that print the dev password:\n" + string.Join('\n', carriers));
    }

    // The detector itself, measured on a token that is NOT the password (a different SHA-256), and on the shapes a document
    // quotes a value in.
    [Theory]
    [InlineData("`admin@diten.com` / `NotThePassword1!`", false)]
    [InlineData("Seed: admin@diten.com / NotThePassword1! (SuperAdmin)", false)]
    public void The_detector_reads_quoted_and_bare_tokens(string line, bool expected)
    {
        Assert.Equal(expected, Tokens(line).Any(IsKnownDevPassword));
        Assert.Contains("NotThePassword1!", Tokens(line)); // the token a password would be is cut out whole
    }

    private static bool IsKnownDevPassword(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).Equals(KnownDevPasswordSha256, StringComparison.OrdinalIgnoreCase);

    // Whitespace and the quoting / table characters a document wraps a value in; '!' and '@' stay inside the token.
    private static IEnumerable<string> Tokens(string text) =>
        text.Split([' ', '\t', '\r', '\n', '`', '"', '\'', '(', ')', '[', ']', '<', '>', '|', '*', ',', ';'], StringSplitOptions.RemoveEmptyEntries);

    private static string Relative(string root, string path) => Path.GetRelativePath(root, path).Replace('\\', '/');

    private static string RepositoryRoot()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(root.FullName, "AGENTS.md"))) root = root.Parent ?? throw new InvalidOperationException("Checkout required");
        return root.FullName;
    }
}
