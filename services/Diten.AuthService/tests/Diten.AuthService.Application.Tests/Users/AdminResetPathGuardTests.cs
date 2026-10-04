using System.Text.RegularExpressions;
using Diten.AuthService.Persistence.Seed;

namespace Diten.AuthService.Application.Tests.Users;

/// <summary>
/// BL-529 — THE SOURCE GUARD: every administrator reset goes through <c>AdminPasswordReset</c>. A handler test only sees
/// the paths it calls; a future <c>user.UpdatePassword(…)</c> or <c>user.SetPasswordResetToken(…)</c> written straight
/// into an administrator path (the way the three original paths were) would reset without ending the old password, the
/// sessions or the audit row — and no behaviour test would notice. So this reads the production SOURCE (comments
/// stripped):
/// <list type="number">
/// <item><c>.UpdatePassword(</c> — the password hash changes — is called only by the reset helper itself and by the
/// account owner's own flows: change, forced change, the set-password link (tenant and platform).</item>
/// <item><c>.SetPasswordResetToken(</c> — a set-password link is issued — is called either INSIDE an
/// <c>AdminPasswordReset.ResetAsync(</c> call (an administrator's reset), or by the flows that are not resets: a new
/// account's invitation, the resend of a still-pending invitation, a new platform administrator's provisioning and the
/// self-service "forgot password".</item>
/// <item>Each administrator reset path calls <c>AdminPasswordReset.ResetAsync(</c>.</item>
/// </list>
/// <para>Sabotage: write <c>user.UpdatePassword(passwordHash);</c> back into InternalEventsController → rule 1 goes red.</para>
/// </summary>
public sealed class AdminResetPathGuardTests
{
    private const string Helper = "Diten.AuthService.Application/Features/Users/Services/AdminPasswordReset.cs";
    private const string PlatformAuth = "Diten.AuthService.Api/Controllers/PlatformAuthController.cs";
    private const string InternalEvents = "Diten.AuthService.Api/Controllers/InternalEventsController.cs";
    private const string AdminReset = "Diten.AuthService.Application/Features/Users/Handlers/CommandHandlers/AdminResetPasswordCommandHandler.cs";
    private const string Resend = "Diten.AuthService.Application/Features/Users/Handlers/CommandHandlers/ResendUserInvitationCommandHandler.cs";
    private const string CreateUser = "Diten.AuthService.Application/Features/Users/Handlers/CommandHandlers/CreateUserCommandHandler.cs";

    /// <summary>The account owner's own password flows — the only places outside the helper the hash may change.</summary>
    private static readonly (string File, string Member)[] OwnPasswordFlows =
    [
        (Helper, "ResetAsync"),
        ("Diten.AuthService.Application/Features/Auth/Handlers/CommandHandlers/ChangePasswordCommandHandler.cs", "Handle"),
        ("Diten.AuthService.Application/Features/Auth/Handlers/CommandHandlers/ForcedChangeTenantPasswordCommandHandler.cs", "Handle"),
        ("Diten.AuthService.Application/Features/Users/Handlers/CommandHandlers/SetTenantPasswordCommandHandler.cs", "Handle"),
        (PlatformAuth, "ForcedChangePassword"),
        (PlatformAuth, "ResetPassword")
    ];

    /// <summary>Links issued by flows that are not an administrator's reset of an existing password.</summary>
    private static readonly (string File, string Member)[] NonResetLinks =
    [
        (CreateUser, "CreateByInvitationAsync"),
        (Resend, "Handle"),
        (PlatformAuth, "ProvisionPlatformAdmin"),
        (PlatformAuth, "ForgotPassword")
    ];

    private static readonly Regex UpdatePasswordCall = new(@"\.UpdatePassword\s*\(", RegexOptions.Compiled);
    private static readonly Regex SetResetTokenCall = new(@"\.SetPasswordResetToken\s*\(", RegexOptions.Compiled);
    private static readonly Regex ResetAsyncCall = new(@"\bAdminPasswordReset\.ResetAsync\s*\(", RegexOptions.Compiled);
    private static readonly Regex MemberDeclaration = new(
        @"^[ \t]*(?:public|private|internal|protected)\b[^\r\n;=]*?\b(\w+)\s*(?:<[^>\r\n]*>)?\s*\(",
        RegexOptions.Compiled | RegexOptions.Multiline);

    [Fact]
    public void The_password_hash_changes_only_in_the_reset_helper_and_the_owners_own_flows()
    {
        var offenders = Calls(UpdatePasswordCall)
            .Where(c => !OwnPasswordFlows.Contains((c.File, c.Member)))
            .Select(c => $"{c.File} :: {c.Member}")
            .ToArray();

        Assert.True(offenders.Length == 0,
            "The password hash is changed outside AdminPasswordReset and the owner's own flows (an administrator reset that " +
            "does not end the old password, the sessions and the audit row):\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void A_set_password_link_is_issued_only_inside_the_reset_helper_or_by_a_flow_that_is_not_a_reset()
    {
        var offenders = Calls(SetResetTokenCall)
            .Where(c => !c.InsideResetAsync && !NonResetLinks.Contains((c.File, c.Member)))
            .Select(c => $"{c.File} :: {c.Member}")
            .ToArray();

        Assert.True(offenders.Length == 0,
            "A set-password link is issued outside AdminPasswordReset.ResetAsync by a flow that is not on the non-reset list:\n"
            + string.Join("\n", offenders));
    }

    [Theory]
    [InlineData(AdminReset, 1)]
    [InlineData(Resend, 1)]
    [InlineData(InternalEvents, 1)]
    [InlineData(PlatformAuth, 2)] // the re-invitation of an existing administrator + the administrator's reset
    public void Every_administrator_reset_path_goes_through_the_helper(string file, int expected)
    {
        var source = WithoutComments(File.ReadAllText(Path.Combine(SrcRoot(), file)));
        Assert.Equal(expected, ResetAsyncCall.Matches(source).Count);
    }

    [Fact]
    public void The_guard_sees_the_calls_it_reasons_about()
    {
        // Self-check: the scan finds the helper's own hash write and the links written inside ResetAsync arguments — a
        // regex that matched nothing would make the two rules above pass vacuously.
        var updates = Calls(UpdatePasswordCall).ToArray();
        var links = Calls(SetResetTokenCall).ToArray();
        Assert.Contains(updates, c => c.File == Helper && c.Member == "ResetAsync");
        Assert.Contains(links, c => c.File == AdminReset && c.InsideResetAsync);
        Assert.Contains(links, c => c.File == CreateUser && !c.InsideResetAsync);
    }

    private sealed record Call(string File, string Member, bool InsideResetAsync);

    private static IEnumerable<Call> Calls(Regex call)
    {
        foreach (var (full, relative) in SourceFiles())
        {
            var source = WithoutComments(File.ReadAllText(full));
            foreach (Match match in call.Matches(source))
            {
                yield return new Call(relative, EnclosingMember(source, match.Index), IsInsideResetAsync(source, match.Index));
            }
        }
    }

    private static string EnclosingMember(string source, int index)
    {
        var name = "?";
        foreach (Match declaration in MemberDeclaration.Matches(source))
        {
            if (declaration.Index > index)
            {
                break;
            }

            name = declaration.Groups[1].Value;
        }

        return name;
    }

    // True when the call sits in the argument list of an AdminPasswordReset.ResetAsync( call (its lambdas included).
    private static bool IsInsideResetAsync(string source, int index)
    {
        foreach (Match reset in ResetAsyncCall.Matches(source))
        {
            if (reset.Index > index)
            {
                break;
            }

            var depth = 0;
            for (var i = reset.Index + reset.Length - 1; i < index; i++)
            {
                if (source[i] == '(') depth++;
                else if (source[i] == ')') depth--;
                if (depth == 0) break;
            }

            if (depth > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<(string Full, string Relative)> SourceFiles()
    {
        var root = SrcRoot();
        return Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Select(f => (f, Path.GetRelativePath(root, f).Replace(Path.DirectorySeparatorChar, '/')));
    }

    private static string SrcRoot()
    {
        var directory = Path.GetDirectoryName(typeof(DataSeeder).Assembly.Location);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory, "services", "Diten.AuthService", "src");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            var sibling = Path.Combine(directory, "src", "Diten.AuthService.Persistence", "Seed");
            if (Directory.Exists(sibling))
            {
                return Path.Combine(directory, "src");
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        throw new DirectoryNotFoundException("services/Diten.AuthService/src was not found above the test assembly.");
    }

    // Line and block comments out (string literals in these files carry no "//" that matters to the scan).
    private static string WithoutComments(string source)
        => Regex.Replace(Regex.Replace(source, @"/\*.*?\*/", " ", RegexOptions.Singleline), @"//[^\r\n]*", string.Empty);
}
