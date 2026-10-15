using System.Text.RegularExpressions;
using Diten.BuildingBlocks.Email;
using Xunit;

namespace Diten.Platform.Application.Tests.Notifications;

/// <summary>
/// BL-454 — the product has ONE sender name and it is not a setting.
///
/// <para>AuthService said "Diten ERP" and Platform "Diten PPM", each from its own <c>Smtp:FromName</c>. This guard
/// reads the repository: no service may carry a sender-name setting or type a product name into a sender again.
/// The name is <see cref="EmailProduct.Name"/>; a tenant's mail is named by <see cref="EmailSender"/>.</para>
/// </summary>
public sealed partial class EmailSenderNameGuardTests
{
    private static readonly string Services = Path.Combine(RepoPaths.Root(), "services");

    [Fact]
    public void No_service_has_a_sender_name_setting()
    {
        var offenders = Directory
            .EnumerateFiles(Services, "appsettings*.json", SearchOption.AllDirectories)
            .Where(IsSource)
            .Where(path => File.ReadAllText(path).Contains("\"FromName\"", StringComparison.Ordinal))
            .Select(Relative)
            .ToList();

        Assert.True(offenders.Count == 0, "Sender-name setting found in: " + string.Join(", ", offenders));
    }

    [Fact]
    public void No_service_reads_a_sender_name_option_or_types_a_product_name_into_a_sender()
    {
        var offenders = new List<string>();
        foreach (var path in Directory.EnumerateFiles(Services, "*.cs", SearchOption.AllDirectories).Where(IsSource))
        {
            var lines = File.ReadAllLines(path);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.TrimStart().StartsWith("//", StringComparison.Ordinal) || line.TrimStart().StartsWith("*", StringComparison.Ordinal))
                {
                    continue;
                }

                if (SenderNameOption().IsMatch(line) || OldProductNameInSender().IsMatch(line))
                {
                    offenders.Add($"{Relative(path)}:{i + 1}");
                }
            }
        }

        Assert.True(offenders.Count == 0, "A sender name outside the one rule: " + string.Join(", ", offenders));
    }

    [Fact]
    public void The_guard_can_see_what_it_forbids()
    {
        // A guard that matches nothing is not a guard: these are the two shapes that were in the repository.
        Assert.Matches(SenderNameOption(), "            From = new MailAddress(_smtpOptions.FromEmail, _smtpOptions.FromName),");
        Assert.Matches(SenderNameOption(), "    public string FromName { get; set; } = \"Diten ERP\";");
        Assert.Matches(OldProductNameInSender(), "message.From.Add(new MailboxAddress(\"Diten PPM\", address));");
        Assert.DoesNotMatch(SenderNameOption(), "From = new MailAddress(_smtpOptions.FromEmail, EmailProduct.Name),");
    }

    private static bool IsSource(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Contains("/src/", StringComparison.Ordinal)
               && !normalized.Contains("/bin/", StringComparison.Ordinal)
               && !normalized.Contains("/obj/", StringComparison.Ordinal);
    }

    private static string Relative(string path) => Path.GetRelativePath(RepoPaths.Root(), path).Replace('\\', '/');

    [GeneratedRegex(@"\bFromName\b")]
    private static partial Regex SenderNameOption();

    [GeneratedRegex("Mail(box)?Address\\([^)]*\"Diten (ERP|PPM)\"")]
    private static partial Regex OldProductNameInSender();
}
