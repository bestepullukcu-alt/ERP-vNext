extern alias MdmInfrastructure;

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Diten.AuthService.DevTrustBootstrap;
using Diten.AuthService.ServiceClientRegistry.Tests.Support;
using Microsoft.Extensions.Configuration;
using MdmInfrastructure::Diten.MdmService.Infrastructure.Workflow;

namespace Diten.AuthService.ServiceClientRegistry.Tests;

/// <summary>
/// MOD-0033-FU02 §F.3 — the Development bootstrap, always against a temporary target (never this checkout's real
/// settings directories). Secrets read back from the generated files are only compared, never put in a message.
/// </summary>
public sealed class DevTrustBootstrapTests
{
    private static readonly string Tenant = "5b0c7f0e-2f8b-4a57-9f0b-6c1d2e3f4a5b";
    private static readonly string Approver = "9c2d1e0f-3a4b-4c5d-8e6f-7a8b9c0d1e2f";

    [Fact]
    public void Bootstrap_WritesThreeOwnerOnlyFiles_AndPrintsNothingSecret()
    {
        using var targets = new Targets();
        var output = new StringWriter();
        var error = new StringWriter();

        var exit = DevTrustBootstrapCommand.Run(targets.Arguments(Tenant, Approver), output, error);

        Assert.Equal(DevTrustBootstrapCommand.Success, exit);
        Assert.True(string.IsNullOrEmpty(error.ToString()), "nothing is written to standard error on success");
        foreach (var file in targets.Files)
        {
            Assert.True(File.Exists(file), "each service gets its local settings file");
            if (!OperatingSystem.IsWindows())
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(file));
        }

        AssertCarriesNoSecret(output.ToString() + error, targets);
    }

    [Fact]
    public void Bootstrap_WhenRunASecondTime_SaysAlreadySetUpAndChangesNothing()
    {
        using var targets = new Targets();
        Assert.Equal(DevTrustBootstrapCommand.Success,
            DevTrustBootstrapCommand.Run(targets.Arguments(Tenant, Approver), new StringWriter(), new StringWriter()));
        var before = targets.Fingerprint();
        var output = new StringWriter();
        var error = new StringWriter();

        var exit = DevTrustBootstrapCommand.Run(targets.Arguments(Tenant, Approver), output, error);

        Assert.Equal(DevTrustBootstrapCommand.Success, exit);
        Assert.Contains("already set up", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(before, targets.Fingerprint());
        Assert.Equal(3, targets.AllEntries().Length); // no stray temporary file either
        AssertCarriesNoSecret(output.ToString() + error, targets);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Bootstrap_WhenOnlySomeFilesExist_RefusesAndChangesNothing(int existingIndex)
    {
        using var targets = new Targets();
        File.WriteAllText(targets.Files[existingIndex], "{ \"SomethingOfTheDevelopers\": true }");
        var before = targets.Fingerprint();
        var output = new StringWriter();
        var error = new StringWriter();

        var exit = DevTrustBootstrapCommand.Run(targets.Arguments(Tenant, Approver), output, error);

        Assert.Equal(DevTrustBootstrapCommand.StateConflict, exit);
        Assert.Equal(before, targets.Fingerprint());
        Assert.Single(targets.AllEntries());
        Assert.Contains("partial setup", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Bootstrap_WhenAllFilesExistButAreNotASetup_RefusesAndChangesNothing()
    {
        using var targets = new Targets();
        foreach (var file in targets.Files) File.WriteAllText(file, "{}");
        var before = targets.Fingerprint();

        var exit = DevTrustBootstrapCommand.Run(targets.Arguments(Tenant, Approver), new StringWriter(), new StringWriter());

        Assert.Equal(DevTrustBootstrapCommand.StateConflict, exit);
        Assert.Equal(before, targets.Fingerprint());
    }

    [Theory]
    [InlineData("--client-secret", "an-outside-secret")]
    [InlineData("--private-key", "an-outside-key")]
    [InlineData("--tenant-id", "not-a-guid")]
    [InlineData("--approver-user-id", "00000000-0000-0000-0000-000000000000")]
    public void Bootstrap_WhenGivenASecretOrAMalformedArgument_RefusesAndWritesNothing(string flag, string value)
    {
        using var targets = new Targets();
        var output = new StringWriter();
        var error = new StringWriter();

        var exit = DevTrustBootstrapCommand.Run([.. targets.Arguments(Tenant, Approver), flag, value], output, error);

        Assert.Equal(DevTrustBootstrapCommand.UsageError, exit);
        Assert.Empty(targets.AllEntries());
        Assert.DoesNotContain(value, output.ToString() + error, StringComparison.Ordinal);
    }

    /// <summary>
    /// The URL goes into MDM's settings and MDM sends the RAW client secret there: a Development tool must not be
    /// able to point that secret at another machine. Refused before anything is written; the message names the
    /// argument and never repeats the value (it can carry user info).
    /// </summary>
    [Theory]
    [InlineData("https://auth.example.com")]                  // another machine
    [InlineData("http://192.168.1.10:5056")]                  // another machine, by address
    [InlineData("http://0.0.0.0:5056")]                       // not a loopback name
    [InlineData("http://localhost.example.com:5056")]         // merely STARTS with "localhost"
    [InlineData("http://localhost.:5056")]                    // not exactly "localhost"
    [InlineData("http://127.0.0.1.example.com:5056")]         // merely starts with the loopback address
    [InlineData("http://localhost:5056@auth.example.com")]    // "localhost" is the user info here; the host is remote
    [InlineData("http://someone@localhost:5056")]             // user info on a local host
    [InlineData("http://someone:something@127.0.0.1:5056")]   // user info with a password part
    [InlineData("http://localhost:5056/?next=1")]             // query
    [InlineData("http://localhost:5056/#fragment")]           // fragment
    [InlineData("http://localhost:5056/api")]                 // a path
    [InlineData("ftp://localhost")]                           // not http/https
    [InlineData(" http://localhost:5056")]                    // not exact
    [InlineData("localhost:5056")]                            // no scheme
    public void Bootstrap_WhenTheAuthUrlIsNotAPlainUrlOfThisMachine_RefusesNamingTheArgument_AndWritesNothing(string url)
    {
        using var targets = new Targets();
        var output = new StringWriter();
        var error = new StringWriter();

        var exit = DevTrustBootstrapCommand.Run([.. targets.Arguments(Tenant, Approver), "--auth-base-url", url], output, error);

        Assert.Equal(DevTrustBootstrapCommand.UsageError, exit);
        Assert.Empty(targets.AllEntries());
        Assert.Contains("--auth-base-url", error.ToString(), StringComparison.Ordinal);
        Assert.Contains("nothing was changed", error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(url, output.ToString() + error, StringComparison.Ordinal);
        Assert.DoesNotContain("someone", output.ToString() + error, StringComparison.Ordinal);
        Assert.False(DevTrustBootstrapCommand.IsLocalAuthBaseUrl(url));
    }

    /// <summary>What the bootstrap accepts is local AND is a value MDM's own validator accepts — written as given.</summary>
    [Theory]
    [InlineData("http://localhost:5056")]
    [InlineData("http://localhost:5056/")]
    [InlineData("https://localhost")]
    [InlineData("http://127.0.0.1:5056")]
    [InlineData("http://[::1]:5056")]
    public void Bootstrap_WhenTheAuthUrlIsThisMachine_WritesItAsGiven_AndMdmsOwnValidatorAcceptsIt(string url)
    {
        using var targets = new Targets();
        var error = new StringWriter();

        var exit = DevTrustBootstrapCommand.Run([.. targets.Arguments(Tenant, Approver), "--auth-base-url", url], new StringWriter(), error);

        Assert.Equal(DevTrustBootstrapCommand.Success, exit);
        Assert.True(string.IsNullOrEmpty(error.ToString()), "nothing is written to standard error on success");
        var mdm = new ConfigurationBuilder().AddJsonFile(targets.Files[2], optional: false, reloadOnChange: false).Build()
            .GetSection(AuthProductIdentityWorkflowServiceIdentityProviderOptions.SectionName)
            .Get<AuthProductIdentityWorkflowServiceIdentityProviderOptions>()!;
        Assert.Equal(url, mdm.AuthBaseUrl);
        AuthProductIdentityWorkflowServiceIdentityProvider.EnsureValidConfiguration(mdm); // throws when MDM would refuse it
    }

    [Fact]
    public void Bootstrap_ByDefault_PointsMdmAtAuthOnThisMachine()
    {
        Assert.True(DevTrustBootstrapCommand.IsLocalAuthBaseUrl(DevTrustBootstrapCommand.DefaultAuthBaseUrl));
        using var targets = new Targets();

        Assert.Equal(DevTrustBootstrapCommand.Success,
            DevTrustBootstrapCommand.Run(targets.Arguments(Tenant, Approver), new StringWriter(), new StringWriter()));

        using var mdm = JsonDocument.Parse(File.ReadAllText(targets.Files[2]));
        Assert.Equal(DevTrustBootstrapCommand.DefaultAuthBaseUrl, mdm.RootElement
            .GetProperty("AuthProductIdentityWorkflowServiceIdentityProvider").GetProperty("AuthBaseUrl").GetString());
    }

    // WP-MDM-GSKU-ACCEPT-01 FIX1 (B6): the GSKU identity, correction and retirement-request flows and their workers are
    // switched on with the same approvers — before this, every GSKU submit in Development answered 503 CONFIGURATION.
    [Fact]
    public void Bootstrap_SwitchesOnTheThreeGskuFlowsAndTheirWorkers_WithTheSameApprovers()
    {
        using var targets = new Targets();

        Assert.Equal(DevTrustBootstrapCommand.Success,
            DevTrustBootstrapCommand.Run(targets.Arguments(Tenant, Approver), new StringWriter(), new StringWriter()));

        using var mdm = JsonDocument.Parse(File.ReadAllText(targets.Files[2]));
        foreach (var flow in new[] { "FirstGskuIdentityWorkflow", "GskuCorrectionWorkflow", "GskuRetirementRequestWorkflow" })
        {
            var section = mdm.RootElement.GetProperty(flow);
            Assert.True(section.GetProperty("Enabled").GetBoolean(), flow);
            Assert.Equal([Approver], section.GetProperty("CandidatePrincipalIds").EnumerateArray()
                .Select(item => item.GetString()!).ToArray());
            Assert.True(mdm.RootElement.GetProperty(flow + "Worker").GetProperty("Enabled").GetBoolean(), flow + "Worker");
        }
    }

    // A non-default client code writes Platform's start entries itself — the GSKU ones too, in the tracked order.
    [Fact]
    public void Bootstrap_WithAnotherClientCode_AuthorizesTheGskuStartsForThatClient()
    {
        using var targets = new Targets();
        var arguments = targets.Arguments(Tenant, Approver).Concat(["--client-code", "diten-mdm-workflow-2"]).ToArray();

        Assert.Equal(DevTrustBootstrapCommand.Success,
            DevTrustBootstrapCommand.Run(arguments, new StringWriter(), new StringWriter()));

        using var platform = JsonDocument.Parse(File.ReadAllText(targets.Files[1]));
        var entries = platform.RootElement.GetProperty("TrustedWorkflowStartAuthorization").GetProperty("Entries")
            .EnumerateArray().Select(entry => (entry.GetProperty("ObjectType").GetString(), entry.GetProperty("TemplateCode").GetString()))
            .ToArray();
        // LSKU FIX1 (B4): the two LSKU starts follow, in the tracked order.
        Assert.Equal([("GlobalProduct", "GLOBAL-PRODUCT-IDENTITY"), ("gsku", "GSKU-IDENTITY"),
            ("GskuCorrection", "GSKU-CORRECTION"), ("GskuRetirementRequest", "GSKU-RETIREMENT-REQUEST"),
            ("lsku", "LSKU-IDENTITY"), ("LskuRetirementRequest", "LSKU-RETIREMENT-REQUEST")], entries);
    }

    // WP-MDM-LSKU-ACCEPT-01 FIX1 (B4): the LSKU identity and retirement-request flows and their workers are switched on
    // with the same approvers — without them every LSKU submit in Development answers 503 CONFIGURATION and a decision
    // taken in the Task Center never reaches the LSKU.
    [Fact]
    public void Bootstrap_SwitchesOnTheTwoLskuFlowsAndTheirWorkers_WithTheSameApprovers()
    {
        using var targets = new Targets();

        Assert.Equal(DevTrustBootstrapCommand.Success,
            DevTrustBootstrapCommand.Run(targets.Arguments(Tenant, Approver), new StringWriter(), new StringWriter()));

        using var mdm = JsonDocument.Parse(File.ReadAllText(targets.Files[2]));
        foreach (var flow in new[] { "LskuIdentityWorkflow", "LskuRetirementRequestWorkflow" })
        {
            var section = mdm.RootElement.GetProperty(flow);
            Assert.True(section.GetProperty("Enabled").GetBoolean(), flow);
            Assert.Equal([Approver], section.GetProperty("CandidatePrincipalIds").EnumerateArray()
                .Select(item => item.GetString()!).ToArray());
            Assert.True(mdm.RootElement.GetProperty(flow + "Worker").GetProperty("Enabled").GetBoolean(), flow + "Worker");
        }
    }

    [Fact]
    public void Bootstrap_WhenTwoTargetsAreTheSameDirectoryInAnotherSpelling_RefusesAndWritesNothing()
    {
        using var targets = new Targets();
        string[] arguments =
        [
            "--auth-dir", targets.Auth, "--platform-dir", targets.Auth.ToUpperInvariant(), "--mdm-dir", targets.Mdm,
            "--tenant-id", Tenant, "--approver-user-id", Approver
        ];

        var exit = DevTrustBootstrapCommand.Run(arguments, new StringWriter(), new StringWriter());

        Assert.Equal(DevTrustBootstrapCommand.UsageError, exit);
        Assert.Empty(targets.AllEntries());
    }

    [Fact]
    public void Bootstrap_WithoutAnApprover_Refuses_SoTheCandidateListIsNeverEmpty()
    {
        using var targets = new Targets();
        var arguments = targets.Arguments(Tenant, Approver).TakeWhile(argument => argument != "--approver-user-id").ToArray();

        var exit = DevTrustBootstrapCommand.Run(arguments, new StringWriter(), new StringWriter());

        Assert.Equal(DevTrustBootstrapCommand.UsageError, exit);
        Assert.Empty(targets.AllEntries());
    }

    /// <summary>
    /// The script itself (scripts/dev/setup-service-trust.sh), twice, against a temporary target: its real standard
    /// output and standard error are captured and scanned.
    /// </summary>
    [Fact]
    public async Task Script_RunTwiceAgainstATemporaryTarget_SecondRunChangesNothing_AndNoOutputCarriesASecret()
    {
        if (OperatingSystem.IsWindows()) return; // the bash wrapper; Windows has setup-service-trust.ps1
        using var targets = new Targets();
        var script = Path.Combine(RepositoryRoot(), "scripts", "dev", "setup-service-trust.sh");
        Assert.True(File.Exists(script), "the bootstrap script must exist at scripts/dev/setup-service-trust.sh");

        var first = await RunScriptAsync(script, targets);
        var afterFirst = targets.Fingerprint();
        var second = await RunScriptAsync(script, targets);

        Assert.Equal(0, first.ExitCode);
        Assert.Equal(0, second.ExitCode);
        Assert.Equal(3, targets.AllEntries().Length);
        Assert.Equal(afterFirst, targets.Fingerprint());
        Assert.Contains("already set up", second.Output, StringComparison.Ordinal);
        AssertCarriesNoSecret(first.Output + first.Error + second.Output + second.Error, targets);
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunScriptAsync(string script, Targets targets)
    {
        var start = new ProcessStartInfo("bash")
        {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false
        };
        // A prebuilt bootstrap assembly: a test must not start a build while it is itself running.
        start.Environment["DITEN_TRUST_BOOTSTRAP_DLL"] =
            Path.Combine(AppContext.BaseDirectory, "Diten.AuthService.DevTrustBootstrap.dll");
        foreach (var argument in new[] { script }.Concat(targets.Arguments(Tenant, Approver))) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, await output, await error);
    }

    /// <summary>Every secret the three files hold, checked against the captured text piece by piece.</summary>
    private static void AssertCarriesNoSecret(string captured, Targets targets)
    {
        using var auth = JsonDocument.Parse(File.ReadAllText(targets.Files[0]));
        using var mdm = JsonDocument.Parse(File.ReadAllText(targets.Files[2]));
        var privateKey = auth.RootElement.GetProperty("ServiceIdentityTokenIssuer").GetProperty("ActivePrivateKeyPem").GetString()!;
        var hash = auth.RootElement.GetProperty("ServiceClientRegistry").GetProperty("Clients")[0].GetProperty("CredentialHash").GetString()!;
        var secret = mdm.RootElement.GetProperty("AuthProductIdentityWorkflowServiceIdentityProvider").GetProperty("ActiveClientSecret").GetString()!;

        Assert.False(captured.Contains(secret, StringComparison.Ordinal), "the output carried the raw client secret");
        Assert.False(captured.Contains(hash, StringComparison.Ordinal), "the output carried the credential hash");
        Assert.False(captured.Contains("PRIVATE KEY", StringComparison.Ordinal), "the output carried a private key block");
        foreach (var line in privateKey.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                     .Where(line => !line.StartsWith("-----", StringComparison.Ordinal)))
        {
            Assert.False(captured.Contains(line, StringComparison.Ordinal), "the output carried private key material");
        }
    }

    internal static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
            directory = directory.Parent ?? throw new InvalidOperationException("A checkout is required.");
        return directory.FullName;
    }

    internal sealed class Targets : IDisposable
    {
        private readonly ScratchDirectory _scratch = new();

        public Targets()
        {
            Auth = _scratch.SubDirectory("auth");
            Platform = _scratch.SubDirectory("platform");
            Mdm = _scratch.SubDirectory("mdm");
        }

        public string Auth { get; }
        public string Platform { get; }
        public string Mdm { get; }

        public string[] Files =>
        [
            Path.Combine(Auth, DevTrustBootstrapCommand.LocalSettingsFileName),
            Path.Combine(Platform, DevTrustBootstrapCommand.LocalSettingsFileName),
            Path.Combine(Mdm, DevTrustBootstrapCommand.LocalSettingsFileName)
        ];

        public string[] Arguments(string tenant, string approver) =>
        [
            "--auth-dir", Auth, "--platform-dir", Platform, "--mdm-dir", Mdm,
            "--tenant-id", tenant, "--approver-user-id", approver
        ];

        public string[] AllEntries() => Directory.GetFileSystemEntries(_scratch.Path, "*", SearchOption.AllDirectories)
            .Where(File.Exists).ToArray();

        /// <summary>Content digest and last-write time of every file under the target.</summary>
        public string[] Fingerprint() => AllEntries().Order(StringComparer.Ordinal)
            .Select(file => $"{file}|{Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)))}|{File.GetLastWriteTimeUtc(file).Ticks}")
            .ToArray();

        public void Dispose() => _scratch.Dispose();
    }
}
