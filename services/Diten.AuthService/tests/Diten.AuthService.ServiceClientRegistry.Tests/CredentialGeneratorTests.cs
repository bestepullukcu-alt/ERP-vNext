using System.Diagnostics;
using System.Reflection;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Infrastructure.Services;
using Diten.AuthService.ServiceClientCredentialGenerator;
using Diten.AuthService.ServiceClientRegistry.Tests.Support;

namespace Diten.AuthService.ServiceClientRegistry.Tests;

/// <summary>
/// MOD-0033-FU02 §F.2 — the offline generator. The raw secret these tests read back from the file is used only to
/// compare; it is never put into an assertion message (assertions on it are boolean).
/// </summary>
public sealed class CredentialGeneratorTests
{
    [Fact]
    public void Generate_WritesTheRawSecretOnceToTheNamedFile_AndPrintsOnlyHashAndVersion()
    {
        using var scratch = new ScratchDirectory();
        var secretFile = scratch.File("client.secret");
        var output = new StringWriter();
        var error = new StringWriter();

        var exit = GeneratorCommand.Run(["--secret-file", secretFile, "--credential-version", "v-test-1"], output, error);

        Assert.Equal(GeneratorCommand.Success, exit);
        var secret = File.ReadAllText(secretFile);
        Assert.True(secret.Length >= 43 && secret.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_'),
            "the secret file must hold one base64url secret of at least 256 bits and nothing else");
        var printed = Lines(output);
        Assert.Equal(["CredentialHash", "CredentialVersion", "SecretFile"], printed.Keys);
        Assert.Equal("v-test-1", printed["CredentialVersion"]);
        // The printed hash is the PRODUCTION hash of the file's secret: the production verifier accepts the pair.
        Assert.True(new ServiceClientCredentialVerifier().Verify(
            new ServiceClientIdentity { ActiveCredentialHash = printed["CredentialHash"], ActiveCredentialVersion = "v-test-1" },
            secret, DateTimeOffset.UtcNow), "the printed hash must verify the secret in the file");
        Assert.True(string.IsNullOrEmpty(error.ToString()), "nothing is written to standard error on success");
    }

    [Fact]
    public void Generate_NeverWritesTheRawSecretToAStandardStream()
    {
        using var scratch = new ScratchDirectory();
        var secretFile = scratch.File("client.secret");
        var output = new StringWriter();
        var error = new StringWriter();

        Assert.Equal(GeneratorCommand.Success,
            GeneratorCommand.Run(["--secret-file", secretFile, "--client-code", "leak-probe"], output, error));

        var secret = File.ReadAllText(secretFile);
        Assert.False(output.ToString().Contains(secret, StringComparison.Ordinal), "standard output carried the raw secret");
        Assert.False(error.ToString().Contains(secret, StringComparison.Ordinal), "standard error carried the raw secret");
    }

    [Fact]
    public void Generate_CreatesTheSecretFileOwnerOnly()
    {
        if (OperatingSystem.IsWindows()) return; // POSIX mode bits; the Windows branch sets a protected ACL instead.
        using var scratch = new ScratchDirectory();
        var secretFile = scratch.File("client.secret");

        Assert.Equal(GeneratorCommand.Success,
            GeneratorCommand.Run(["--secret-file", secretFile], new StringWriter(), new StringWriter()));

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(secretFile));
    }

    [Fact]
    public void Generate_WhenTheSecretFileAlreadyExists_RefusesAndLeavesItUntouched()
    {
        using var scratch = new ScratchDirectory();
        var secretFile = scratch.File("client.secret");
        File.WriteAllText(secretFile, "the-secret-already-in-use");
        var output = new StringWriter();

        var exit = GeneratorCommand.Run(["--secret-file", secretFile], output, new StringWriter());

        Assert.Equal(GeneratorCommand.FileError, exit);
        Assert.Equal("the-secret-already-in-use", File.ReadAllText(secretFile));
        Assert.DoesNotContain("CredentialHash", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void OwnerOnlyFile_WhenThePathAlreadyExists_ThrowsAndLeavesTheContentUntouched()
    {
        using var scratch = new ScratchDirectory();
        var path = scratch.File("existing.secret");
        File.WriteAllText(path, "the-secret-already-in-use");

        Assert.ThrowsAny<IOException>(() => OwnerOnlyFile.CreateNew(path, "replacement"));

        Assert.Equal("the-secret-already-in-use", File.ReadAllText(path));
    }

    [Theory]
    [InlineData("--secret", "an-outside-secret")]
    [InlineData("--client-secret", "an-outside-secret")]
    [InlineData("--Secret-File", "case-variant")]
    [InlineData("--secret-file", "duplicate")]
    [InlineData("--client-code", "a,code-the-registry-would-refuse")]
    public void Generate_WhenGivenASecretOrAnyUnknownArgument_RefusesAndGeneratesNothing(string flag, string value)
    {
        using var scratch = new ScratchDirectory();
        var secretFile = scratch.File("client.secret");
        var output = new StringWriter();
        var error = new StringWriter();

        var exit = GeneratorCommand.Run(["--secret-file", secretFile, flag, value], output, error);

        Assert.Equal(GeneratorCommand.UsageError, exit);
        Assert.False(File.Exists(secretFile), "nothing may be generated when an unknown argument is present");
        Assert.DoesNotContain(value, output.ToString() + error, StringComparison.Ordinal); // the argument is not echoed
    }

    [Fact]
    public void Factory_OffersNoWayToSupplyASecret()
    {
        // The only public entry point takes a SINK for the generated secret; nothing accepts secret material.
        var entryPoints = typeof(ServiceClientCredentialFactory)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);

        var create = Assert.Single(entryPoints);
        Assert.Equal(nameof(ServiceClientCredentialFactory.Create), create.Name);
        Assert.Equal([typeof(ServiceClientCredentialFactory.RawSecretSink)], create.GetParameters().Select(parameter => parameter.ParameterType));
    }

    [Fact]
    public void Generate_ProducesADifferentSecretEveryTime()
    {
        using var scratch = new ScratchDirectory();
        var first = scratch.File("first.secret");
        var second = scratch.File("second.secret");

        GeneratorCommand.Run(["--secret-file", first], new StringWriter(), new StringWriter());
        GeneratorCommand.Run(["--secret-file", second], new StringWriter(), new StringWriter());

        Assert.False(string.Equals(File.ReadAllText(first), File.ReadAllText(second), StringComparison.Ordinal),
            "two runs produced the same secret");
    }

    [Fact]
    public void Generate_WithClientCode_PrintsTheIdAuthWillGiveThatClient()
    {
        using var scratch = new ScratchDirectory();
        var output = new StringWriter();

        GeneratorCommand.Run(["--secret-file", scratch.File("client.secret"), "--client-code", "some-client"], output, new StringWriter());

        Assert.Equal(ServiceClientIdentityIds.FromClientCode("some-client").ToString("D"), Lines(output)["ClientId"]);
    }

    /// <summary>
    /// The real process: its real standard output and standard error are captured and scanned, and its standard input
    /// carries a would-be secret that must be ignored.
    /// </summary>
    [Fact]
    public async Task GeneratorProcess_IgnoresStandardInput_AndItsRealStreamsCarryNoSecret()
    {
        using var scratch = new ScratchDirectory();
        var secretFile = scratch.File("client.secret");
        var assembly = Path.Combine(AppContext.BaseDirectory, "Diten.AuthService.ServiceClientCredentialGenerator.dll");
        Assert.True(File.Exists(assembly), "the generator assembly must sit next to the tests");
        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false
        };
        foreach (var argument in new[] { assembly, "--secret-file", secretFile }) start.ArgumentList.Add(argument);

        using var process = Process.Start(start)!;
        const string offered = "secret-offered-on-standard-input";
        await process.StandardInput.WriteLineAsync(offered);
        process.StandardInput.Close();
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        Assert.Equal(GeneratorCommand.Success, process.ExitCode);
        var secret = File.ReadAllText(secretFile);
        Assert.False(string.Equals(secret, offered, StringComparison.Ordinal), "the generator used a secret from standard input");
        Assert.False((output + error).Contains(secret, StringComparison.Ordinal), "a standard stream carried the raw secret");
        Assert.Contains("CredentialHash=pbkdf2-sha256$", output, StringComparison.Ordinal);
    }

    private static Dictionary<string, string> Lines(StringWriter writer) => writer.ToString()
        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(line => line.Split('=', 2))
        .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.Ordinal);
}
