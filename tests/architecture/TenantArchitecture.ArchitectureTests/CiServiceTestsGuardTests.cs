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
 *     reason. A project merged later (MDM Api, Auth ServiceClientRegistry) is red here until it gets its row.
 *     NotInThisJob is EMPTY since BL-590 (Eventing and the acceptance host got rows); the rule does not need it —
 *     it compares the projects on disk with the matrix — and it stays as the place for a future CT decision;
 *   • every matrix row's project exists;
 *   • every job has timeout-minutes; every `dotnet test` has --blame-hang-timeout and writes a TRX; the skipped=0
 *     step runs on that TRX with `if: always()`;
 *   • mongod is pinned (version, a 64-hex SHA-256 checked with sha256sum, ubuntu-22.04) and DITEN_TEST_MONGOD is set;
 *   • BL-590: the job-local mongod for MDM and Eventing is a one-node replica set (--replSet, the bounded initiator,
 *     directConnection=true); RabbitMQ is pinned by tag AND digest, with a bounded readiness wait; Auth rows carry the
 *     owned-Mongo flag (takes effect with BL-570 FIX3);
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

    /// <summary>
    /// Service test projects deliberately NOT in the job — each a Control Tower decision, with its reason. Empty since
    /// BL-590 (Platform.Eventing and the account-kind acceptance host got their rows).
    /// </summary>
    private static readonly Dictionary<string, string> NotInThisJob = new(StringComparer.Ordinal);

    private static readonly string[] ServiceTestRoots =
    [
        "services/Diten.Platform/tests", "services/Diten.AuthService/tests", "services/Diten.MdmService/tests"
    ];

    [Fact]
    public void Every_service_test_project_is_a_matrix_row_or_named_with_its_reason()
    {
        var root = RepoRoot();
        var rows = MatrixProjects(Workflow());

        var missing = UnrunProjects(root, rows, NotInThisJob.Keys);
        Assert.True(missing.Length == 0,
            "Service test projects CI never runs — add a matrix row to " + WorkflowPath + ", or name it in NotInThisJob "
            + "with a Control Tower decision: " + string.Join(", ", missing));
        var gone = rows.Where(row => !File.Exists(Path.Combine(root, row))).ToArray();
        Assert.True(gone.Length == 0, "Matrix rows whose project does not exist: " + string.Join(", ", gone));
        Assert.Empty(rows.Intersect(NotInThisJob.Keys));
        Assert.True(rows.Count >= 6, $"only {rows.Count} matrix rows read — the guard is blind");
        Assert.All(NotInThisJob, entry => Assert.False(string.IsNullOrWhiteSpace(entry.Value), entry.Key));
    }

    // BL-590: the rule does not lean on the exclusion list. A tree with one more service test project than the matrix
    // names — the real matrix, an empty list — is red with exactly that project named. (A temporary tree: nothing is
    // added to, or removed from, the repository.)
    [Fact]
    public void A_new_service_test_project_without_a_matrix_row_is_named_even_with_an_empty_exclusion_list()
    {
        var root = Path.Combine(Path.GetTempPath(), "bl590-guard-" + Guid.NewGuid().ToString("N"));
        var rows = MatrixProjects(Workflow());
        const string ghost = "services/Diten.Platform/tests/Diten.Platform.Ghost.Tests/Diten.Platform.Ghost.Tests.csproj";
        try
        {
            foreach (var project in rows.Append(ghost))
            {
                var path = Path.Combine(root, project);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, "<Project />");
            }

            Assert.Equal(new[] { ghost }, UnrunProjects(root, rows, []));
            Assert.Empty(UnrunProjects(root, rows.Append(ghost).ToHashSet(StringComparer.Ordinal), []));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    // BL-590: MDM writes in transactions — the job-local mongod (MDM, Eventing) is a one-node replica set, initiated by
    // the bounded python3 initiator and reached with directConnection=true; every URI the tests read gets it.
    [Fact]
    public void The_job_local_mongod_is_a_one_node_replica_set_for_mdm_and_eventing()
    {
        var workflow = Workflow();
        var step = Step(workflow, "Start a job-local mongod replica set (MDM, Eventing)");
        Assert.Contains("if: startsWith(matrix.suite, 'mdm-') || matrix.suite == 'platform-eventing'", step, StringComparison.Ordinal);
        Assert.Matches(@"--replSet rs0\b", step);
        Assert.Matches(@"python3 scripts/mongo_rs_initiate\.py --port ""\$MONGO_PORT"" --set rs0 --timeout \d+", step);
        Assert.Contains("uri=\"mongodb://127.0.0.1:$MONGO_PORT/?directConnection=true\"", step, StringComparison.Ordinal);
        foreach (var variable in new[] { "MONGO_TEST_URI", "MDM_TEST_MONGO", "Eventing__MongoDb__ConnectionString" })
            Assert.Contains($"echo \"{variable}=$uri\" >> \"$GITHUB_ENV\"", step, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(RepoRoot(), "scripts", "mongo_rs_initiate.py")));
    }

    // BL-590: RabbitMQ pinned by tag and digest, a bounded readiness wait with a named failure, no credential written.
    [Fact]
    public void RabbitMq_is_pinned_and_waited_for_and_writes_no_credential()
    {
        var workflow = Workflow();
        Assert.Matches(@"(?m)^  RABBITMQ_IMAGE: rabbitmq:\d+\.\d+\.\d+@sha256:[0-9a-f]{64}$", workflow);
        var step = Step(workflow, "Start RabbitMQ (Eventing)");
        Assert.Contains("if: matrix.suite == 'platform-eventing'", step, StringComparison.Ordinal);
        Assert.Contains("docker run -d --name rabbitmq --network host \"$RABBITMQ_IMAGE\"", step, StringComparison.Ordinal);
        Assert.Matches(@"for attempt in \$\(seq 1 \d+\)", step);
        Assert.Contains("RABBITMQ_NOT_READY", step, StringComparison.Ordinal);
        Assert.Contains("Eventing__RabbitMq__IntegrationTestsEnabled=true", step, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"(?i)(Username|Password|RABBITMQ_DEFAULT_)", step);
    }

    // BL-590 item 4: Auth rows carry the owned-Mongo flag (BL-570 FIX3 makes it bite; today it is harmless).
    [Fact]
    public void Auth_rows_require_an_owned_mongo()
    {
        var step = Step(Workflow(), "Owned Mongo is required (Auth)");
        Assert.Contains("if: startsWith(matrix.suite, 'auth-')", step, StringComparison.Ordinal);
        Assert.Contains("echo \"DITEN_REQUIRE_OWNED_MONGO=1\" >> \"$GITHUB_ENV\"", step, StringComparison.Ordinal);
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

    /// <summary>Service test projects on disk under <paramref name="root"/> that no matrix row runs and no entry excuses.</summary>
    private static string[] UnrunProjects(string root, IReadOnlySet<string> rows, IEnumerable<string> excused) =>
        ServiceTestRoots
            .Where(dir => Directory.Exists(Path.Combine(root, dir)))
            .SelectMany(dir => Directory.EnumerateFiles(Path.Combine(root, dir), "*.Tests.csproj", SearchOption.AllDirectories))
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .Where(path => !path.Contains("/bin/", StringComparison.Ordinal) && !path.Contains("/obj/", StringComparison.Ordinal))
            .Except(rows)
            .Except(excused)
            .Order(StringComparer.Ordinal)
            .ToArray();

    /// <summary>One step of the workflow, from its "- name:" line to the next step (or the end).</summary>
    private static string Step(string workflow, string name)
    {
        var start = workflow.IndexOf("- name: " + name + "\n", StringComparison.Ordinal);
        Assert.True(start >= 0, $"no step named '{name}'");
        var next = workflow.IndexOf("\n      - name: ", start + 1, StringComparison.Ordinal);
        return next < 0 ? workflow[start..] : workflow[start..next];
    }

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
