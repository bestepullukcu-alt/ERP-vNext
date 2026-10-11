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
 *   • WP-CI-SERVICE-TESTS-03: a service test project that is NOT in its service's solution is never compiled by the
 *     phase1 gates (they build the solution) — phase1-gates.yml must build it on its own, and never `dotnet test` it;
 *   • BL-590: the job-local mongod for MDM and Eventing is a one-node replica set (--replSet, the bounded initiator,
 *     directConnection=true); RabbitMQ is pinned by tag AND digest, with a bounded readiness wait; Auth rows carry the
 *     owned-Mongo flag (takes effect with BL-570 FIX3);
 *   • triggers are pull_request + push to main + workflow_dispatch (Control Tower decision), permissions are
 *     contents: read, no secret is read, and every action is one of the three allowed, pinned to a commit SHA.
 * And scripts/check_trx_no_skipped.py is run on fixture TRX files: 0 skipped → 0; a skipped test → 1 and its name;
 * no file → 1; malformed XML → 1; a file without results → 1.
 *
 * FIX1 (WP-CI-SERVICE-TESTS-03): EVERY file under .github/workflows — only the named ones may exist; each has exactly one
 * top-level `permissions: contents: read` and none below it; no pull_request_target / workflow_run / issue_comment; no
 * `secrets` outside comments; every action at its ONE commit in ActionPins (also a `uses:` value on the next line); no
 * continue-on-error, no job-level if:, no step-level if: on the test / gates / mongod install steps; the mongod variables
 * never overridden by an env: key; phase1's build lines (in run_phase1_gates.sh) match the out-of-solution projects both
 * ways; every live-broker test class runs in LiveRabbitMqCollection.
 *
 * FIX2 (CT decision — the regex race ends, no YAML package): the reading is FAIL-CLOSED. Only FULL-LINE comments are
 * dropped (Code); an inline `#` and what follows it is scanned like any text, so a false positive is red, never a pass.
 * Every search ignores case. Keys (permissions, uses, if, run, secrets, the mongod names, exclude, on) are counted
 * ANYWHERE — block or flow form, quoted or not — and any count other than the expected one is red. A run: block may
 * not neutralise a failure (`||`, `; true`, `|| :`, `exit 0`, `set +e`); the test and gates lines are exact; the gates
 * script starts with `set -euo pipefail`; every if: in a file is one of an expected list; no matrix exclude, no
 * --filter; the Release prerequisite steps (mdm-app, platform-bgjobs) are pinned; the skip allow-list caps each code.
 *
 * WP-CI-SERVICE-TESTS-04 (BL-604): EVERY test project under services/ (all services, Building Blocks included; pattern
 * *Tests.csproj, so ArchitectureTests / IntegrationTests count) is a matrix row or named in NotInThisJob with its reason —
 * the "not yet" list (Crm, Enterprise Strategy, HCM, MG Integration / Architecture, PPM). A row whose project is in NO
 * solution under services/ is compiled by phase1 (both ways, as before).
 *
 * ⚠ IT READS TEXT. A step assembled by a reusable workflow or a composite action evades it — none is allowed here.
 */
public class CiServiceTestsGuardTests
{
    private const string WorkflowPath = ".github/workflows/service-tests.yml";
    private const string ScriptPath = "scripts/check_trx_no_skipped.py";

    /// <summary>
    /// Service test projects NOT (yet) in the job — each a Control Tower decision, with its reason (WP-CI-SERVICE-TESTS-04,
    /// BL-604 measurement: B = red, its owner fixes it (XMC-001); C = the test infrastructure first). Removing a name
    /// without adding its row is red; a row gained removes the name (the two lists may not overlap).
    /// </summary>
    private static readonly Dictionary<string, string> NotInThisJob = new(StringComparer.Ordinal)
    {
        // Main sync (2026-10-11, PR #134 MVP-6): two projects that arrived with origin/main. Both fail closed on a Mongo URI
        // of their own that no CI row provides; wiring them is their owners' change (XMC-001), not a merge resolution.
        ["services/Diten.Platform/tests/Diten.Platform.CompositionGuard.Tests/Diten.Platform.CompositionGuard.Tests.csproj"] =
            "main (MVP-6): needs PLATFORM_COMPOSITION_GUARD_MONGO_URI (a disposable mongod); could take PlatformTestMongo — owner's decision",
        ["services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Diten.SupplyChainService.Tests.csproj"] =
            "main (SCE / MVP-6): needs MVP6_MOD0192_MONGO_URI (its own replica set, CapacityTestMongo); SCE's file",
        ["services/Diten.CrmService/tests/Diten.CrmService.Application.Tests/Diten.CrmService.Application.Tests.csproj"] =
            "B (Crm): 1 red — PdfSharp needs a system font (ContentSetRevisionRenderTests)",
        ["services/Diten.EnterpriseStrategyService/tests/Diten.Application.Tests/Diten.Application.Tests.csproj"] =
            "B (Enterprise Strategy): 6 red, RBAC assertions among them",
        ["services/Diten.EnterpriseStrategyService/tests/Diten.EnterpriseStrategy.EndToEnd.Tests/Diten.EnterpriseStrategy.EndToEnd.Tests.csproj"] =
            "B (Enterprise Strategy): 2 red",
        ["services/Diten.HcmService/tests/Diten.HcmService.Application.Tests/Diten.HcmService.Application.Tests.csproj"] =
            "C → B (HCM): EmployeeDraftSessionRepositoryMongoTests uses the SHARED mongodb://localhost:27017",
        ["services/Diten.ManagementGovernanceService/tests/Diten.ManagementGovernanceService.IntegrationTests/Diten.ManagementGovernanceService.IntegrationTests.csproj"] =
            "C (MG): its own mongod at a fixed /opt/homebrew/bin/mongod — must read DITEN_TEST_MONGOD",
        ["services/Diten.ManagementGovernanceService/tests/Diten.ManagementGovernanceService.ArchitectureTests/Diten.ManagementGovernanceService.ArchitectureTests.csproj"] =
            "B (MG): 2 red; loads its sibling test projects' DLLs (needs the MG solution built)",
        ["services/Diten.PpmService/tests/Diten.PpmService.Tests/Diten.PpmService.Tests.csproj"] =
            "green, but waits for the PPM team's approval (owner relays it)",
        ["services/Diten.PpmService/tests/Diten.PpmService.IntegrationTests/Diten.PpmService.IntegrationTests.csproj"] =
            "B + C (PPM): 10 red, fixed /opt/homebrew/bin/mongod; and the PPM team's approval"
    };

    /// <summary>WP-CI-SERVICE-TESTS-04: every service and Building Blocks — the whole services/ tree.</summary>
    // Main sync (CI follow-up 3 review): the gateway's tests too (gateway/Diten.ApiGateway.Tests, a matrix row).
    private static readonly string[] ServiceTestRoots = ["services", "gateway"];

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
        Assert.True(rows.Count >= 19, $"only {rows.Count} matrix rows read — the guard is blind");
        var stale = NotInThisJob.Keys.Where(project => !File.Exists(Path.Combine(root, project))).ToArray();
        Assert.True(stale.Length == 0, "NotInThisJob names projects that do not exist: " + string.Join(", ", stale));
        Assert.All(NotInThisJob, entry => Assert.False(string.IsNullOrWhiteSpace(entry.Value), entry.Key));
    }

    // WP-CI-SERVICE-TESTS-03 (CT decision b): phase1 builds each service's solution and runs no service test. A test
    // project outside its solution (e3d3a7331 broke Platform.Eventing.Tests, unseen) must be built by its own phase1 step
    // — build only: its tests need a broker / an owned mongod and run in service-tests.yml.
    // FIX1: the build lines live in scripts/run_phase1_gates.sh (a local gates run = CI) and the rule is two-way — every
    // project outside its solution has exactly its build line, and a build line for a project that is IN its solution
    // (or gone) is red too, so moving a project into the solution means removing its line.
    [Fact]
    public void Every_service_test_project_outside_its_solution_is_built_by_phase1()
    {
        var root = RepoRoot();
        var gates = GatesText();
        // WP-CI-SERVICE-TESTS-04: "outside" = a matrix row's project that NO solution under services/ lists (Building
        // Blocks has none; Email.Tests is in Diten.Platform.sln, Secrets.Tests in none).
        var solutions = Directory.EnumerateFiles(Path.Combine(root, "services"), "*.sln", SearchOption.AllDirectories)
            .Select(File.ReadAllText).ToList();
        Assert.True(solutions.Count >= 3, $"only {solutions.Count} solutions found — the scan is blind");
        var outside = MatrixProjects(Workflow())
            .Where(project => !solutions.Any(solution => solution.Contains(Path.GetFileName(project), StringComparison.Ordinal)))
            .Order(StringComparer.Ordinal)
            .ToList();
        Assert.True(outside.Count >= 11, $"only {outside.Count} out-of-solution rows found — the scan is blind");

        var built = Regex.Matches(gates, @"(?m)^dotnet build ((?:services|gateway)/[^ ]+Tests\.csproj) -c Debug\s*$")
            .Select(match => match.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        var unbuilt = outside.Except(built).ToArray();
        var surplus = built.Except(outside).ToArray();
        Assert.True(unbuilt.Length == 0,
            "Service test projects outside their solution that phase1 never compiles — add a build line to "
            + "scripts/run_phase1_gates.sh: " + string.Join(", ", unbuilt));
        Assert.True(surplus.Length == 0,
            "Build lines in scripts/run_phase1_gates.sh for test projects that are in their solution (or gone) — remove: "
            + string.Join(", ", surplus));
        Assert.All(outside, project => Assert.DoesNotContain("dotnet test " + project, gates, StringComparison.Ordinal));
        var phase1 = WorkflowText("phase1-gates.yml");
        Assert.Contains("run: ./scripts/run_phase1_gates.sh", phase1, StringComparison.Ordinal);
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
            Assert.True(Regex.IsMatch(body, @"^    timeout-minutes: *[1-9]\d*\s*$", RegexOptions.Multiline),
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
        // FIX1: the check runs with the job's suite, so only that suite's allow-list codes can pass.
        // Hardening: the whole step, exact — ` | cat` (no pipefail in the default bash -e) would turn its red green.
        Assert.Equal(
            "- name: Skipped = 0\n        if: always()\n"
            + "        run: python3 scripts/check_trx_no_skipped.py --suite \"$SUITE\" \"$RUNNER_TEMP/test-results/$SUITE.trx\"\n",
            Step(Code(workflow), "Skipped = 0"));
        // …and phase1 runs the allow-list's own tests (scripts/tests) on every push.
        Assert.Matches(@"(?m)^python3 -m unittest discover -s scripts/tests -p 'test_\*\.py'$", GatesText());
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
        Assert.Equal(["pull_request", "push", "workflow_dispatch"], Triggers(Code(workflow)));
        Assert.Matches(@"(?m)^on:\n  pull_request:\n  push:\n    branches: \[main\]\n  workflow_dispatch:\n", Code(workflow));
        AssertReadOnlyAndPinned(workflow);
    }

    // WP-CI-SERVICE-TESTS-03 (CT decision): phase1-gates.yml keeps its own triggers, but the same rule — a read-only
    // token, no secret, every action one of the allowed ones pinned to a commit SHA, not a tag.
    [Fact]
    public void Phase1_has_a_read_only_token_and_every_action_pinned_to_a_commit()
    {
        AssertReadOnlyAndPinned(WorkflowText("phase1-gates.yml"));
    }

    /// <summary>
    /// A secret or token reference, any case: `secrets` as a context (`${{ secrets.X }}`, `secrets['X']`, `secrets: inherit`,
    /// `toJSON(secrets)`) — not a path segment or a suite name (`Security.Secrets.Tests`, `bb-secrets`; WP-CI-SERVICE-TESTS-04
    /// rows), so a word preceded by a letter, `.`, `/` or `-` is not one; `github.token`, `github_token`, `github['token']`,
    /// and any `toJSON(` (it can dump a whole context).
    /// </summary>
    private const string SecretReference = @"(?i)(?<![\w./-])secrets\b|\bgithub[._]token\b|\bgithub\s*\[|\btoJSON\s*\(";

    [Theory]
    [InlineData("TOKEN: \"#${{ secrets.X }}\"", true)]
    [InlineData("TOKEN: ${{ SECRETS.X }}", true)]
    [InlineData("TOKEN: ${{secrets.X}}", true)]
    [InlineData("secrets: inherit", true)]
    [InlineData("X: ${{ toJSON(secrets) }}", true)]
    [InlineData("T: ${{ Secrets['X'] }}", true)]
    [InlineData("T: ${{ github['token'] }}", true)]
    [InlineData("T: ${{ github.token }}", true)]
    [InlineData("- suite: bb-secrets", false)]
    [InlineData("project: services/Diten.Building.Blocks/Diten.BuildingBlocks.Security.Secrets.Tests/x.csproj", false)]
    public void A_secret_reference_is_recognised_and_a_project_name_is_not(string line, bool reference)
    {
        Assert.Equal(reference, Regex.IsMatch(line, SecretReference));
    }

    private static void AssertReadOnlyAndPinned(string workflow)
    {
        var code = Code(workflow);
        AssertOneReadOnlyToken(code);
        Assert.DoesNotMatch(SecretReference, code);

        var uses = Uses(workflow);
        Assert.NotEmpty(uses);
        Assert.All(uses, action =>
        {
            var at = action.IndexOf('@');
            Assert.True(at > 0 && ActionPins.TryGetValue(action[..at], out var pin) && action[(at + 1)..] == pin,
                $"'{action}' is not an allowed action at its one recorded commit");
        });
    }

    // ------------------------------------------------------------------------------------- every workflow (FIX1)

    /// <summary>
    /// FIX1 (CT decision): the ONE commit each allowed action runs at — both workflows, the same entry. A tag, a typo, a
    /// commit from a fork network or 40 zeros is red; upgrading an action is a deliberate edit of this table (with the
    /// tag ↔ SHA measured by `git ls-remote`). Dependabot is not used (owner's decision): pins are raised by hand.
    /// </summary>
    private static readonly Dictionary<string, string> ActionPins = new(StringComparer.Ordinal)
    {
        ["actions/checkout"] = "11d5960a326750d5838078e36cf38b85af677262",       // v4.4.0
        ["actions/setup-dotnet"] = "67a3573c9a986a3f9c594539f4ab511d57bb3ce9",   // v4.3.1
        ["actions/upload-artifact"] = "ea165f8d65b6e75b540449e92b4886f43607fa02" // v4.6.2
    };

    /// <summary>The workflow files the repository is allowed to hold — a new one is added here on purpose, with review.</summary>
    private static readonly string[] NamedWorkflows = ["phase1-gates.yml", "service-tests.yml"];

    [Fact]
    public void Only_the_named_workflow_files_exist()
    {
        var present = Directory.EnumerateFiles(Path.Combine(RepoRoot(), ".github", "workflows"))
            .Where(path => path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.True(present.SequenceEqual(NamedWorkflows.Order(StringComparer.Ordinal)),
            "Workflow files outside the guard's named list (add it to NamedWorkflows, with its rules): "
            + string.Join(", ", present.Except(NamedWorkflows)) + " / missing: " + string.Join(", ", NamedWorkflows.Except(present!)));
    }

    // One top-level read-only token per file and none below it: a job or step `permissions:` (write-all, contents:
    // write) would otherwise widen the token past the rule.
    [Fact]
    public void Every_workflow_has_one_read_only_token_and_no_job_or_step_permissions()
    {
        foreach (var name in NamedWorkflows)
        {
            var code = Code(WorkflowText(name));
            Assert.True(KeyCount(code, "permissions") == 1, $"{name}: {KeyCount(code, "permissions")} permissions keys (block or flow, any case)");
            AssertOneReadOnlyToken(code);
        }
    }

    // Triggers that run with the base repository's token on code from a fork or a comment are not used.
    [Fact]
    public void No_workflow_uses_a_privileged_trigger_or_reads_a_secret()
    {
        foreach (var name in NamedWorkflows)
        {
            var code = Code(WorkflowText(name));
            Assert.True(!Regex.IsMatch(code, @"(?i)\b(pull_request_target|workflow_run|issue_comment)\b"), $"{name}: privileged trigger");
            Assert.True(!Regex.IsMatch(code, SecretReference), $"{name}: reads a secret");
            Assert.True(KeyCount(code, "on") == 1, $"{name}: {KeyCount(code, "on")} on: keys");
        }
    }

    [Fact]
    public void Phase1_triggers_are_push_and_pull_request()
    {
        Assert.Equal(["push", "pull_request"], Triggers(Code(WorkflowText("phase1-gates.yml"))));
    }

    [Fact]
    public void Every_action_in_every_workflow_is_at_its_one_recorded_commit()
    {
        foreach (var name in NamedWorkflows) AssertReadOnlyAndPinned(WorkflowText(name));
        Assert.Equal(ActionPins.Keys.Order(StringComparer.Ordinal),
            NamedWorkflows.SelectMany(name => Uses(WorkflowText(name))).Select(action => action[..action.IndexOf('@')])
                .Distinct().Order(StringComparer.Ordinal));
    }

    // A red test step must stay red: no continue-on-error anywhere, no job-level `if:`, and EVERY `if:` in a file is one of
    // its expected conditions, in order (FIX2) — the test / gates / mongod-install steps have none. No run: block may
    // neutralise a failure; the test and gates commands are exact; the gates script fails fast; no matrix exclude and no
    // --filter (a row or a test cannot be dropped quietly).
    private static readonly Dictionary<string, string[]> ExpectedIfs = new(StringComparer.Ordinal)
    {
        ["phase1-gates.yml"] = [],
        ["service-tests.yml"] =
        [
            "matrix.suite == 'mdm-app'",
            "matrix.suite == 'platform-bgjobs'",
            "startsWith(matrix.suite, 'mdm-') || matrix.suite == 'platform-eventing'",
            "matrix.suite == 'platform-eventing'",
            "startsWith(matrix.suite, 'auth-')",
            "always()",
            "always()"
        ]
    };

    /// <summary>Hardening: the $GITHUB_ENV writes each file makes (measured; a new one is a deliberate edit here).</summary>
    private static readonly Dictionary<string, int> ExpectedGithubEnvWrites = new(StringComparer.Ordinal)
    {
        ["phase1-gates.yml"] = 0,
        ["service-tests.yml"] = 9
    };

    [Fact]
    public void Nothing_can_turn_a_red_test_or_build_green()
    {
        foreach (var name in NamedWorkflows)
        {
            var code = Code(WorkflowText(name));
            Assert.True(!Regex.IsMatch(code, @"(?i)\bcontinue-on-error\b"), $"{name}: continue-on-error");
            Assert.True(!Regex.IsMatch(code, @"(?im)^    [""']?if[""']?[ \t]*:"), $"{name}: a job-level if:");
            Assert.Equal(ExpectedIfs[name], KeyValues(code, "if"));
            Assert.True(KeyCount(code, "exclude") == 0, $"{name}: a matrix exclude:");
            Assert.True(!Regex.IsMatch(code, @"(?i)--filter\b"), $"{name}: dotnet test --filter");

            var runs = RunBlocks(code);
            Assert.True(runs.Count == KeyCount(code, "run"), $"{name}: {KeyCount(code, "run")} run: keys but {runs.Count} blocks read");
            // Hardening: the ban applies to the WHOLE text (a `run: | # x`, `|2`, `&a |`, `!!str |` or a plain multi-line
            // scalar no longer escapes a run-value reader), except the if: lines — the one legitimate `||` is in an if:.
            var lines = code.Split('\n').Where(line => !Regex.IsMatch(line, @"(?i)^\s*(?:-\s*)?[""']?if[""']?[ \t]*:")).ToArray();
            foreach (var line in lines)
            {
                var neutraliser = Regex.Match(line, @"(?i)\|\||;\s*true\b|\bexit\s+0\b|\bset\s+\+|\btrap\b");
                Assert.True(!neutraliser.Success, $"{name}: a line can turn a failure green ('{neutraliser.Value}'): {line}");
            }
            // Hardening: no shell override (`defaults.run.shell: true {0}` ends every run: with 0), no PATH injection.
            Assert.True(KeyCount(code, "shell") == 0 && KeyCount(code, "defaults") == 0, $"{name}: shell: / defaults:");
            Assert.True(!Regex.IsMatch(code, @"(?i)GITHUB_PATH"), $"{name}: writes GITHUB_PATH");
            Assert.True(Regex.Matches(code, @"(?i)GITHUB_ENV").Count == ExpectedGithubEnvWrites[name],
                $"{name}: {Regex.Matches(code, @"(?i)GITHUB_ENV").Count} GITHUB_ENV writes, expected {ExpectedGithubEnvWrites[name]}");
            // Hardening (cheap, fail-closed): no YAML node properties (&anchor, *alias, !tag), no `? key`, no hex escapes.
            var property = Regex.Match(code, @"(?m)(?:^[ \t]*-|:)[ \t]+[&*!]|^[ \t]*\?[ \t]|\\[xuU][0-9A-Fa-f]");
            Assert.True(!property.Success, $"{name}: a YAML anchor / alias / tag / complex key / escape ('{property.Value}')");
        }

        foreach (var (file, step) in new[]
                 {
                     ("service-tests.yml", "Test"), ("service-tests.yml", "Install mongod (pinned version and digest)"),
                     ("phase1-gates.yml", "Run Phase 1 Gates")
                 })
            Assert.True(!Regex.IsMatch(Step(WorkflowText(file), step), @"(?im)^[ \t]+[""']?if[""']?[ \t]*:"), $"{file} '{step}' has an if:");

        Assert.Equal(
            "- name: Test\n        run: |\n"
            + "          dotnet test \"$PROJECT\" -c Debug --blame-hang --blame-hang-timeout 20m \\\n"
            + "            --logger \"trx;LogFileName=$SUITE.trx\" --results-directory \"$RUNNER_TEMP/test-results\"\n",
            Step(Code(WorkflowText("service-tests.yml")), "Test"));
        Assert.Single(Regex.Matches(Code(WorkflowText("phase1-gates.yml")), @"(?m)^        run: \./scripts/run_phase1_gates\.sh$"));
        Assert.Matches(@"(?m)^on:\n  push:\n    branches: \[""\*\*""\]\n  pull_request:\n", Code(WorkflowText("phase1-gates.yml")));
        // Hardening: the job-level SUITE / PROJECT the test and skip steps read are exactly the matrix values.
        Assert.Single(Regex.Matches(Code(WorkflowText("service-tests.yml")),
            @"(?m)^    env:\n      SUITE: \$\{\{ matrix\.suite \}\}\n      PROJECT: \$\{\{ matrix\.project \}\}\n    steps:\n"));
        Assert.Equal(2, Regex.Matches(Code(WorkflowText("service-tests.yml")), @"(?<![\w.$-])[""']?(?:SUITE|PROJECT)[""']?[ \t]*:").Count);

        var gates = GatesText();
        Assert.Equal("#!/usr/bin/env bash", gates.Split('\n')[0]);
        Assert.Equal("set -euo pipefail",
            gates.Split('\n').Skip(1).First(line => line.Trim().Length > 0 && !line.TrimStart().StartsWith('#')));
        // Hardening: the two legitimate `|| true` (the audit-ledger greps that may match nothing — measured lines 74 and
        // 86) and nothing else; no exit 0, no trap, no `set +` of any form, no --filter.
        Assert.Equal(2, Regex.Matches(gates, @"\|\|").Count);
        Assert.Equal(2, Regex.Matches(gates, @"\| grep -E '\^(?:-|\\\+)- '[^\n]*\|\| true\)").Count);
        Assert.DoesNotMatch(@"(?i)\bexit\s+0\b|\btrap\b|\bset\s+\+|--filter\b", gates);
        foreach (var project in new[]
                 {
                     "tests/tenancy/TenantArchitecture.TenancyTests/TenantArchitecture.TenancyTests.csproj",
                     "tests/architecture/TenantArchitecture.ArchitectureTests/TenantArchitecture.ArchitectureTests.csproj",
                     "frontend/Diten.Web.Tests/Diten.Web.Tests.csproj"
                 })
            Assert.Single(Regex.Matches(gates, $@"(?m)^dotnet test {Regex.Escape(project)} -c Debug$"));
    }

    // The Release assemblies a row's tests load from bin/Release (FIX1 mdm-app; FIX2 platform-bgjobs: its child-host test
    // starts Platform.API's Release DLL — measured red on a clean tree without this step). Pinned, with their condition.
    [Fact]
    public void The_release_prerequisite_steps_are_pinned_to_their_rows()
    {
        var workflow = Code(WorkflowText("service-tests.yml"));
        Assert.Equal(
            "- name: Build the Release assemblies the MDM cross-service tests load (MDM)\n"
            + "        if: matrix.suite == 'mdm-app'\n        run: |\n"
            + "          dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Release\n"
            + "          dotnet build services/Diten.MdmService/src/Diten.MdmService.Api/Diten.MdmService.Api.csproj -c Release\n",
            Step(workflow, "Build the Release assemblies the MDM cross-service tests load (MDM)"));
        Assert.Equal(
            "- name: Build the Release Platform API the child-host test starts (Platform BackgroundJobs)\n"
            + "        if: matrix.suite == 'platform-bgjobs'\n"
            + "        run: dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.Platform.API.csproj -c Release\n",
            Step(workflow, "Build the Release Platform API the child-host test starts (Platform BackgroundJobs)"));
    }

    // The tests find mongod through these two variables, set by the (unconditional) install step only — no env: key may
    // override them (an empty DITEN_TEST_MONGOD would turn owned-mongod tests into "not found").
    [Fact]
    public void The_mongod_variables_are_set_by_the_install_step_and_never_overridden()
    {
        var workflow = WorkflowText("service-tests.yml");
        var install = Step(workflow, "Install mongod (pinned version and digest)");
        Assert.Contains("echo \"DITEN_TEST_MONGOD=$bin/mongod\" >> \"$GITHUB_ENV\"", install, StringComparison.Ordinal);
        Assert.Contains("echo \"DITEN_ITEST_MONGOD_BIN_DIR=$bin\" >> \"$GITHUB_ENV\"", install, StringComparison.Ordinal);
        var code = Code(workflow);
        foreach (var variable in new[] { "DITEN_TEST_MONGOD", "DITEN_ITEST_MONGOD_BIN_DIR" })
            Assert.True(KeyCount(code, variable) == 0, $"an env: key (block or flow, any case) overrides {variable}");
        // FIX2: the only assignments — export, $GITHUB_ENV, anything with "=" — are the install step's two lines.
        var assignments = Regex.Matches(code, @"(?i)DITEN_I?TEST_MONGOD(?:_BIN_DIR)?[ \t]*(?:=|<<)").ToArray();
        Assert.True(assignments.Length == 2, $"{assignments.Length} assignments of a mongod variable, expected the install step's 2");
        var installAt = code.IndexOf("- name: Install mongod (pinned version and digest)\n", StringComparison.Ordinal);
        Assert.All(assignments, assignment =>
            Assert.True(assignment.Index > installAt && assignment.Index < installAt + Step(code, "Install mongod (pinned version and digest)").Length,
                "a mongod variable is assigned outside the install step"));
    }

    // FIX1: every test class that opens a LIVE broker joins LiveRabbitMqCollection (one at a time) — a third one running
    // in parallel would bring back the cross-test delivery race. FIX2: any declaration form (modifiers, nesting,
    // record), any opener (CreateUsingRabbitMq, UsingRabbitMq, ConnectionFactory), and each call checked against the
    // OUTERMOST class whose braces contain it — a later or nested class cannot hide behind an earlier correct one.
    [Fact]
    public void Every_class_that_opens_a_live_broker_runs_in_the_live_rabbitmq_collection()
    {
        var root = RepoRoot();
        var callers = Directory.EnumerateFiles(Path.Combine(root, "services"), "*.cs", SearchOption.AllDirectories)
            .Where(path => path.Replace('\\', '/').Contains("/tests/", StringComparison.Ordinal)
                           && !path.Contains("/bin/", StringComparison.Ordinal) && !path.Contains("/obj/", StringComparison.Ordinal))
            .Select(path => (Path: path, Text: File.ReadAllText(path)))
            .Where(file => Regex.IsMatch(file.Text, LiveBrokerOpener))
            .ToList();
        Assert.True(callers.Count >= 2, $"only {callers.Count} live-broker test files found — the scan is blind");
        foreach (var (path, text) in callers)
            foreach (var problem in LiveBrokerCallsOutsideTheCollection(text))
                Assert.Fail($"{Path.GetRelativePath(root, path)}: {problem}");
    }

    // The rule on synthetic sources: what FIX1's reader missed is red now.
    [Theory]
    [InlineData("[Collection(LiveRabbitMqCollection.Name)]\npublic class A { void M() { Bus.Factory.CreateUsingRabbitMq(c => {}); } }\n", 0)]
    [InlineData("public abstract partial class A { void M() { Bus.Factory.CreateUsingRabbitMq(c => {}); } }\n", 1)]
    [InlineData("    internal static class A { void M() { x.UsingRabbitMq((c, f) => {}); } }\n", 1)]
    [InlineData("public sealed record A { void M() { new ConnectionFactory(); } }\n", 1)]
    [InlineData("[Collection(LiveRabbitMqCollection.Name)]\npublic class A { void M() { } }\npublic class B { void M() { Bus.Factory.CreateUsingRabbitMq(c => {}); } }\n", 1)]
    [InlineData("[Collection(LiveRabbitMqCollection.Name)]\npublic class A { private sealed class Inner { void M() { new ConnectionFactory(); } } }\n", 0)]
    [InlineData("public class A { [Collection(LiveRabbitMqCollection.Name)]\n private class Inner { void M() { new ConnectionFactory(); } } }\n", 1)]
    [InlineData("// [Collection(LiveRabbitMqCollection.Name)]\npublic class A { void M() { Bus.Factory.CreateUsingRabbitMq(c => {}); } }\n", 1)]
    public void A_live_broker_call_is_checked_against_its_outermost_class(string source, int problems)
    {
        Assert.Equal(problems, LiveBrokerCallsOutsideTheCollection(source).Count);
    }

    private const string LiveBrokerOpener = @"\b(?:CreateUsingRabbitMq|UsingRabbitMq|ConnectionFactory)\b";

    private static List<string> LiveBrokerCallsOutsideTheCollection(string text)
    {
        // Hardening: a `// [Collection(...)]` comment is not the attribute — full-line // comments are removed first.
        text = Regex.Replace(text, @"(?m)^[ \t]*//.*$", "");
        var classes = Regex.Matches(text,
                @"(?m)^[ \t]*(?:(?:public|internal|private|protected|sealed|abstract|partial|static|file|unsafe|new)\s+)*(?:class|record(?:\s+class)?|struct)\s+\w+")
            .Select(declaration =>
            {
                var open = text.IndexOf('{', declaration.Index + declaration.Length);
                var depth = 0;
                var close = text.Length;
                for (var i = open; open >= 0 && i < text.Length; i++)
                {
                    if (text[i] == '{') depth++;
                    else if (text[i] == '}' && --depth == 0) { close = i; break; }
                }
                return (Declaration: declaration, Start: declaration.Index, End: close);
            })
            .ToList();
        var problems = new List<string>();
        foreach (Match call in Regex.Matches(text, LiveBrokerOpener))
        {
            var outermost = classes.Where(c => c.Start < call.Index && call.Index < c.End).OrderBy(c => c.Start).FirstOrDefault();
            if (outermost.Declaration is null)
            {
                problems.Add($"{call.Value} outside any class");
                continue;
            }

            var before = text[..outermost.Start].TrimEnd();
            if (!before.EndsWith("[Collection(LiveRabbitMqCollection.Name)]", StringComparison.Ordinal))
                problems.Add($"{outermost.Declaration.Value.Trim()} opens a live broker ({call.Value}) outside LiveRabbitMqCollection");
        }

        return problems;
    }

    private static string WorkflowText(string name) => Text(Path.Combine(".github", "workflows", name));

    private static string GatesText() => Text(Path.Combine("scripts", "run_phase1_gates.sh"));

    /// <summary>
    /// Hardening (CT, last round): a repository text file with CRLF normalised — and then NO other YAML line break left
    /// (lone CR, NEL U+0085, LS U+2028, PS U+2029): YAML would break the line there, this reader (and Code) would not, so
    /// `# x&lt;CR&gt;  actions: write` would read as one comment line.
    /// </summary>
    private static string Text(string relative)
    {
        var text = File.ReadAllText(Path.Combine(RepoRoot(), relative)).Replace("\r\n", "\n");
        var odd = Regex.Match(text, "[\r\u0085\u2028\u2029]");
        Assert.True(!odd.Success, $"{relative}: a line break other than LF (U+{(odd.Success ? (int)odd.Value[0] : 0):X4}) at {odd.Index}");
        return text;
    }

    /// <summary>
    /// FIX2 (CT decision): the text without its FULL-LINE comments. An inline `#` is NOT cut — YAML lets `#` stand inside a
    /// value (`TOKEN: "#${{ secrets.X }}"`), and a reader that cuts there is blind; scanning the comment text too can only
    /// make the guard red (a false positive), never green.
    /// </summary>
    private static string Code(string text)
        => string.Join("\n", text.Split('\n').Where(line => !Regex.IsMatch(line, @"^\s*#")));

    /// <summary>A YAML key anywhere — block or flow (`{ a: b }`), quoted or not, any case; not part of a longer word.</summary>
    private static Regex Key(string name)
        => new($@"(?<![\w.$-])[""']?{Regex.Escape(name)}[""']?[ \t]*:(?!:)", RegexOptions.IgnoreCase);

    private static int KeyCount(string code, string name) => Key(name).Matches(code).Count;

    /// <summary>The rest of the line after each occurrence of the key, trimmed (a flow value keeps its tail: never equal).</summary>
    private static string[] KeyValues(string code, string name)
        => Key(name).Matches(code).Select(match => code[(match.Index + match.Length)..].Split('\n')[0].Trim()).ToArray();

    /// <summary>The single top-level token: `permissions:` at column 0 whose block is exactly `  contents: read`, blank lines
    /// included in the block (a key after a blank line still belongs to it).</summary>
    private static void AssertOneReadOnlyToken(string code)
    {
        Assert.True(!Regex.IsMatch(code, @"(?i)\b(?:write-all|read-all)\b"), "write-all / read-all");
        var lines = code.Split('\n');
        var at = Array.FindIndex(lines, line => Regex.IsMatch(line, @"^permissions:[ \t]*$"));
        Assert.True(at >= 0, "no top-level `permissions:` block");
        var block = lines.Skip(at + 1).TakeWhile(line => line.Length == 0 || char.IsWhiteSpace(line[0]))
            .Where(line => line.Trim().Length > 0).ToArray();
        Assert.Equal(["  contents: read"], block);
    }

    /// <summary>The trigger names under the top-level `on:` (to the next column-0 key; full-line comments already gone).</summary>
    private static string[] Triggers(string code)
    {
        var lines = code.Split('\n');
        var at = Array.FindIndex(lines, line => Regex.IsMatch(line, @"^on:[ \t]*$"));
        Assert.True(at >= 0, "no top-level block `on:`");
        return lines.Skip(at + 1).TakeWhile(line => line.Length == 0 || char.IsWhiteSpace(line[0]))
            .Select(line => Regex.Match(line, @"^  [""']?([A-Za-z_]+)[""']?[ \t]*:", RegexOptions.IgnoreCase))
            .Where(match => match.Success).Select(match => match.Groups[1].Value).ToArray();
    }

    /// <summary>Each `run:` value: the rest of its line, or — after a block indicator — the lines indented below the key.</summary>
    private static List<string> RunBlocks(string code)
    {
        var lines = code.Split('\n');
        var blocks = new List<string>();
        for (var i = 0; i < lines.Length; i++)
        {
            foreach (Match key in Key("run").Matches(lines[i]))
            {
                var value = lines[i][(key.Index + key.Length)..].Trim();
                if (!Regex.IsMatch(value, @"^[>|][-+]?$"))
                {
                    blocks.Add(value);
                    continue;
                }

                var indent = lines[i].Length - lines[i].TrimStart().Length;
                blocks.Add(string.Join("\n", lines.Skip(i + 1)
                    .TakeWhile(line => line.Trim().Length == 0 || line.Length - line.TrimStart().Length > indent)));
            }
        }

        return blocks;
    }

    /// <summary>
    /// Every action a workflow names — `uses:` anywhere (block or flow, quoted, any case), its value on the same line or,
    /// after a block indicator, on the next. Every `uses` key must yield a value, so a form this reader misses is red.
    /// </summary>
    private static string[] Uses(string workflow)
    {
        var code = Code(workflow);
        var values = new List<string>();
        foreach (Match key in Key("uses").Matches(code))
        {
            var rest = code[(key.Index + key.Length)..];
            var value = Regex.Match(rest, @"^[ \t]*(?:[>|][-+]?[ \t]*\n[ \t]*)?[""']?([^\s#,}""']+)");
            if (value.Success) values.Add(value.Groups[1].Value);
        }

        var keys = KeyCount(code, "uses");
        Assert.True(keys == values.Count, $"{keys} uses: keys but {values.Count} values read");
        return values.ToArray();
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

    // Main sync (CI follow-up 3 review): a suite name is the row's identity — its TRX file, its artifact, its allow-list
    // key and every `matrix.suite ==` condition. Two rows with one name would overwrite each other's results.
    [Fact]
    public void Every_matrix_suite_name_is_unique()
    {
        var suites = Regex.Matches(Code(Workflow()), @"(?m)^ +- suite: *(\S+)\s*$").Select(match => match.Groups[1].Value).ToArray();
        Assert.True(suites.Length >= 19, $"only {suites.Length} suites read — the guard is blind");
        var repeated = suites.GroupBy(name => name, StringComparer.Ordinal).Where(group => group.Count() > 1).Select(group => group.Key).ToArray();
        Assert.True(repeated.Length == 0, "matrix suite names used more than once: " + string.Join(", ", repeated));
        Assert.Equal(suites.Length, MatrixProjects(Workflow()).Count);
    }

    // Main sync (CI follow-up 3 review): the BL-609 page-help ceiling check is CALLED, with the base it was given — a
    // removed call or a dropped `--base` would leave a green step that compared nothing.
    [Fact]
    public void The_page_help_ceiling_check_is_called_with_the_base()
    {
        var gates = GatesText();
        Assert.Single(Regex.Matches(gates, @"(?m)^check_page_help_ledger_ceiling\(\) \{$"));
        Assert.Single(Regex.Matches(gates, @"(?m)^check_page_help_ledger_ceiling$"));
        Assert.Single(Regex.Matches(gates, @"(?m)^    python3 scripts/check_page_help_ledger_ceiling\.py --base ""\$base""$"));
        Assert.Single(Regex.Matches(gates, @"(?m)^    python3 scripts/check_page_help_ledger_ceiling\.py$"));
        Assert.True(gates.IndexOf("\ncheck_page_help_ledger_ceiling\n", StringComparison.Ordinal)
                    > gates.IndexOf("check_page_help_ledger_ceiling() {", StringComparison.Ordinal),
            "the ceiling check is called before it is defined");
        Assert.Matches(@"(?m)^python3 -m unittest discover -s scripts/tests -p 'test_\*\.py'$", gates);
        Assert.True(File.Exists(Path.Combine(RepoRoot(), "scripts", "tests", "test_check_page_help_ledger_ceiling.py")));
    }

    // ------------------------------------------------------------------------------------------------- reading

    private static string Workflow() => Text(WorkflowPath);

    /// <summary>Service test projects on disk under <paramref name="root"/> that no matrix row runs and no entry excuses.</summary>
    private static string[] UnrunProjects(string root, IReadOnlySet<string> rows, IEnumerable<string> excused) =>
        ServiceTestRoots
            .Where(dir => Directory.Exists(Path.Combine(root, dir)))
            .SelectMany(dir => Directory.EnumerateFiles(Path.Combine(root, dir), "*Tests.csproj", SearchOption.AllDirectories))
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
