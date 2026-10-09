using System.Diagnostics;
using System.Text.RegularExpressions;

namespace TenantArchitecture.ArchitectureTests;

/*
 * THE GUARD — THE SERVICE TESTS RUN IN CI, AND A SKIPPED TEST IS RED (BL-577, 2026-10-09).
 *
 * WHY THIS FILE EXISTS. Until BL-577 CI (phase1-gates.yml) only BUILT the service solutions: about 11 000 Platform,
 * Auth and MDM tests never ran on Linux, and Linux-only behaviour (BL-570: the Auth operator commands' Unix secret
 * channel and lock) was never measured. .github/workflows/service-tests.yml runs them. This guard keeps that file
 * honest; it runs in phase1 (this project), on every push.
 *
 * WHAT IS CHECKED, all on the file's text (no YAML parser in this project; adding a package is a download):
 *   • every service test project of Platform / Auth / MDM is a matrix row — or is named in NotInThisJob with its
 *     reason. A project merged later (MDM Api, Auth ServiceClientRegistry) is red here until it gets its row;
 *   • every matrix row's project exists;
 *   • every job has timeout-minutes; every `dotnet test` has --blame-hang-timeout and writes a TRX; the skipped=0
 *     step runs on that TRX with `if: always()`;
 *   • mongod is pinned (version, a 64-hex SHA-256 checked with sha256sum, ubuntu-22.04) and DITEN_TEST_MONGOD is set;
 *   • triggers are pull_request + push to main + workflow_dispatch (Control Tower decision), permissions are
 *     contents: read, no secret is read, and every action is one of the three allowed, pinned to a commit SHA.
 * And scripts/check_trx_no_skipped.py is run on fixture TRX files: 0 skipped → 0; a skipped test → 1 and its name;
 * no file → 1; malformed XML → 1; a file without results → 1.
 *
 * ⚠ IT READS TEXT. A step assembled by a reusable workflow or a composite action evades it — none is allowed here.
 */
public class CiServiceTestsGuardTests
{
    private const string WorkflowPath = ".github/workflows/service-tests.yml";
    private const string ScriptPath = "scripts/check_trx_no_skipped.py";

    /// <summary>Service test projects deliberately NOT in the job — each a Control Tower decision, with its reason.</summary>
    private static readonly Dictionary<string, string> NotInThisJob = new(StringComparer.Ordinal)
    {
        ["services/Diten.Platform/tests/Diten.Platform.Eventing.Tests/Diten.Platform.Eventing.Tests.csproj"] =
            "RabbitMQ integration tests; they skip without a broker and the job starts none (BL-577 report)",
        ["services/Diten.AuthService/tests/Diten.AuthService.AccountKindAcceptanceHost.Tests/Diten.AuthService.AccountKindAcceptanceHost.Tests.csproj"] =
            "the account-kind acceptance host's own tests; not in BL-577's list (BL-577 report)",
    };

    private static readonly string[] ServiceTestRoots =
    [
        "services/Diten.Platform/tests", "services/Diten.AuthService/tests", "services/Diten.MdmService/tests"
    ];

    [Fact]
    public void Every_service_test_project_is_a_matrix_row_or_named_with_its_reason()
    {
        var root = RepoRoot();
        var discovered = ServiceTestRoots
            .Where(dir => Directory.Exists(Path.Combine(root, dir)))
            .SelectMany(dir => Directory.EnumerateFiles(Path.Combine(root, dir), "*.Tests.csproj", SearchOption.AllDirectories))
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .Where(path => !path.Contains("/bin/", StringComparison.Ordinal) && !path.Contains("/obj/", StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);
        var rows = MatrixProjects(Workflow());

        var missing = discovered.Except(rows).Except(NotInThisJob.Keys).Order(StringComparer.Ordinal).ToArray();
        Assert.True(missing.Length == 0,
            "Service test projects CI never runs — add a matrix row to " + WorkflowPath + ", or name it in NotInThisJob "
            + "with a Control Tower decision: " + string.Join(", ", missing));
        var gone = rows.Where(row => !File.Exists(Path.Combine(root, row))).ToArray();
        Assert.True(gone.Length == 0, "Matrix rows whose project does not exist: " + string.Join(", ", gone));
        Assert.Empty(rows.Intersect(NotInThisJob.Keys));
        Assert.True(rows.Count >= 4, $"only {rows.Count} matrix rows read — the guard is blind");
    }

    [Fact]
    public void Every_job_has_a_timeout_and_every_test_run_a_hang_timeout_and_a_result_file()
    {
        var workflow = Workflow();
        var jobs = Jobs(workflow);
        Assert.NotEmpty(jobs);
        foreach (var (name, body) in jobs)
            Assert.True(Regex.IsMatch(body, @"^    timeout-minutes: *\d+\s*$", RegexOptions.Multiline),
                $"job '{name}' has no timeout-minutes — a hung job holds a runner for six hours");

        // A command continued with "\" is one command; a comment that mentions dotnet test is not one.
        var testRuns = Regex.Matches(workflow.Replace("\\\n", " "), @"(?m)^ *dotnet test\b.*$")
            .Select(match => match.Value).ToArray();
        Assert.NotEmpty(testRuns);
        Assert.All(testRuns, run =>
        {
            Assert.Matches(@"--blame-hang-timeout \d+m", run);
            Assert.Contains("--logger \"trx;", run, StringComparison.Ordinal);
        });
        Assert.Matches(@"- name: Skipped = 0\s+if: always\(\)\s+run: python3 scripts/check_trx_no_skipped\.py ", workflow);
    }

    [Fact]
    public void Mongod_is_pinned_by_version_and_digest_and_the_tests_are_told_where_it_is()
    {
        var workflow = Workflow();
        Assert.Matches(@"(?m)^  MONGODB_VERSION: ""7\.0\.\d+""$", workflow);
        Assert.Matches(@"(?m)^  MONGODB_SHA256: [0-9a-f]{64}$", workflow);
        Assert.Contains("| sha256sum --check --strict", workflow, StringComparison.Ordinal);
        Assert.Matches(@"(?m)^    runs-on: ubuntu-22\.04$", workflow);
        Assert.Matches(@"echo ""DITEN_TEST_MONGOD=\$bin/mongod"" >> ""\$GITHUB_ENV""", workflow);
    }

    [Fact]
    public void Triggers_permissions_and_actions_are_the_decided_ones()
    {
        var workflow = Workflow();
        var on = Regex.Match(workflow, @"(?ms)^on:\n(.*?)^\S").Groups[1].Value;
        Assert.Equal(["pull_request", "push", "workflow_dispatch"],
            Regex.Matches(on, @"(?m)^  ([a-z_]+):").Select(match => match.Groups[1].Value).ToArray());
        Assert.Matches(@"(?m)^  push:\n    branches: \[main\]$", on);
        Assert.Matches(@"(?m)^permissions:\n  contents: read\n(?!  )", workflow);
        Assert.DoesNotContain("secrets.", workflow, StringComparison.Ordinal);

        var uses = Regex.Matches(workflow, @"uses: *(\S+)").Select(match => match.Groups[1].Value).ToArray();
        Assert.NotEmpty(uses);
        Assert.All(uses, action => Assert.Matches(
            @"^actions/(checkout|setup-dotnet|upload-artifact)@[0-9a-f]{40}$", action));
    }

    // ------------------------------------------------------------------------------------- the skipped=0 script

    [Fact]
    public void The_skip_check_passes_a_result_file_where_every_test_ran()
    {
        var (code, output) = RunScript(Trx(("Diten.A.Passes", "Passed"), ("Diten.A.Fails", "Failed")));
        Assert.True(code == 0, output);
    }

    [Fact]
    public void The_skip_check_fails_on_one_skipped_test_and_names_it()
    {
        var (code, output) = RunScript(Trx(("Diten.A.Passes", "Passed"), ("Diten.A.WasSkipped", "NotExecuted")));
        Assert.True(code == 1, output);
        Assert.Contains("Diten.A.WasSkipped", output, StringComparison.Ordinal);
        Assert.Contains("*** FAILED ***", output, StringComparison.Ordinal);
    }

    [Fact]
    public void The_skip_check_fails_when_the_result_file_is_missing()
    {
        var (code, output) = RunScriptOn(Path.Combine(Path.GetTempPath(), "bl577-" + Guid.NewGuid().ToString("N") + ".trx"));
        Assert.True(code == 1, output);
        Assert.Contains("NOTHING WAS CHECKED", output, StringComparison.Ordinal);
    }

    [Fact]
    public void The_skip_check_fails_on_a_malformed_result_file()
    {
        var (code, output) = RunScript("<TestRun><Results><UnitTestResult outcome=\"Passed\"");
        Assert.True(code == 1, output);
    }

    [Fact]
    public void The_skip_check_fails_on_a_result_file_without_results()
    {
        var (code, output) = RunScript(Trx());
        Assert.True(code == 1, output);
    }

    private static string Trx(params (string Name, string Outcome)[] results) =>
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n"
        + "<TestRun xmlns=\"http://microsoft.com/schemas/VisualStudio/TeamTest/2010\"><Results>"
        + string.Concat(results.Select(result => $"<UnitTestResult testName=\"{result.Name}\" outcome=\"{result.Outcome}\" />"))
        + "</Results><ResultSummary outcome=\"Completed\"><Counters "
        + $"total=\"{results.Length}\" executed=\"{results.Count(r => r.Outcome != "NotExecuted")}\" "
        + $"notExecuted=\"{results.Count(r => r.Outcome == "NotExecuted")}\" /></ResultSummary></TestRun>";

    private static (int Code, string Output) RunScript(string trx)
    {
        var path = Path.Combine(Path.GetTempPath(), "bl577-" + Guid.NewGuid().ToString("N") + ".trx");
        File.WriteAllText(path, trx);
        try
        {
            return RunScriptOn(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static (int Code, string Output) RunScriptOn(string trxPath)
    {
        var start = new ProcessStartInfo("python3")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add(Path.Combine(RepoRoot(), ScriptPath));
        start.ArgumentList.Add(trxPath);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        Assert.True(process.WaitForExit(60_000), "the skip check did not finish in 60 s");
        return (process.ExitCode, output);
    }

    // ------------------------------------------------------------------------------------------------- reading

    private static string Workflow() => File.ReadAllText(Path.Combine(RepoRoot(), WorkflowPath)).Replace("\r\n", "\n");

    private static HashSet<string> MatrixProjects(string workflow) =>
        Regex.Matches(workflow, @"(?m)^ +project: *(\S+)\s*$").Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>Each job under `jobs:` (two-space key) with its body, up to the next job or the end.</summary>
    private static List<(string Name, string Body)> Jobs(string workflow)
    {
        var jobsAt = Regex.Match(workflow, @"(?m)^jobs:\n");
        Assert.True(jobsAt.Success, "no jobs: block");
        var section = workflow[(jobsAt.Index + jobsAt.Length)..];
        var keys = Regex.Matches(section, @"(?m)^  ([A-Za-z0-9_-]+):\s*$").ToArray();
        return keys.Select((key, index) => (key.Groups[1].Value,
            section[key.Index..(index + 1 < keys.Length ? keys[index + 1].Index : section.Length)])).ToList();
    }

    /// <summary>The repository root — the same walk-up-to-AGENTS.md as DocsPathGuardTests.RepoRoot().</summary>
    private static string RepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md"))) return current.FullName;
            current = current.Parent;
        }

        throw new InvalidOperationException("Repo root not found.");
    }
}
