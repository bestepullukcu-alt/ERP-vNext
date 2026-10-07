using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text.Json;
using Diten.AuthService.Api.Services.ServiceIdentityTokens;
using Xunit;

namespace Diten.AuthService.Application.Tests.ServiceIdentityTokens;

/// <summary>Runs on Unix only; on Windows it is skipped BY NAME (the Windows path has its own tests).</summary>
public sealed class UnixFactAttribute : FactAttribute
{
    public UnixFactAttribute()
    {
        if (OperatingSystem.IsWindows()) Skip = "UNIX_ONLY: the Windows pipe path is measured by ServiceClientOperationalProvisioningRunnerTests.";
    }
}

/// <summary>
/// WP-AUTH-BL570-01 — the secret channel on Unix keeps the Windows rule: the secret goes ONLY to an inherited, verified
/// OS pipe, once; never a path, never stdout / stderr / a log. A descriptor that is a regular file, /dev/null, a
/// terminal or a socket is refused, and so is a DUPLICATE of stdout / stderr (the same pipe object under another
/// number). All descriptors here are disposable test objects; the "secret" is random test data.
/// </summary>
[Collection(ConsoleCaptureCollection.Name)]
public sealed class ServiceClientSecretOutputSinkUnixTests
{
    [UnixFact]
    public async Task A_real_anonymous_pipe_receives_the_secret_once_and_it_appears_nowhere_else()
    {
        Assert.True(UnixPipeHandleInspector.IsSupported, "This Unix platform must pass the inspector's self-check.");
        using var pipe = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
        var secret = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        var (stdout, stderr) = await CaptureConsoleAsync(async () =>
        {
            await using var sink = new ServiceClientSecretOutputSink();
            await sink.PreflightAsync(pipe.GetClientHandleAsString(), CancellationToken.None);
            await sink.DeliverOnceAsync(Guid.NewGuid(), "fixture-client", secret, CancellationToken.None);
            await Assert.ThrowsAnyAsync<Exception>(() => sink.DeliverOnceAsync(Guid.NewGuid(), "fixture-client", secret, CancellationToken.None));
        });

        using var reader = new StreamReader(pipe);
        var line = await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5));
        using var payload = JsonDocument.Parse(line!);
        Assert.True(payload.RootElement.GetProperty("Secret").GetString() == secret, "The OS pipe must carry the secret.");
        Assert.False(stdout.Contains(secret, StringComparison.Ordinal) || stderr.Contains(secret, StringComparison.Ordinal),
            "The secret must not reach stdout or stderr.");
    }

    [UnixFact]
    public async Task A_regular_file_dev_null_a_terminal_or_a_socket_is_refused_and_nothing_is_written()
    {
        var path = Path.Combine(Path.GetTempPath(), "diten-unix-sink-negative-" + Guid.NewGuid().ToString("N"));
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose);
        using var devNull = new FileStream("/dev/null", FileMode.Open, FileAccess.Write);
        var terminal = PosixOpenPt(2 | (OperatingSystem.IsMacOS() ? 0x20000 : 0x100)); // O_RDWR | O_NOCTTY
        var sockets = new int[2];
        Assert.True(terminal > 2, "A pseudo-terminal is needed for the terminal case.");
        Assert.Equal(1, IsATty(terminal));
        Assert.Equal(0, SocketPair(1, 1, 0, sockets));
        try
        {
            var cases = new (string Name, string Handle)[]
            {
                ("regular file", file.SafeFileHandle.DangerousGetHandle().ToInt64().ToString()),
                ("/dev/null", devNull.SafeFileHandle.DangerousGetHandle().ToInt64().ToString()),
                ("terminal", terminal.ToString()),
                ("socket", sockets[0].ToString())
            };
            foreach (var (name, handle) in cases)
            {
                await using var sink = new ServiceClientSecretOutputSink();
                await Assert.ThrowsAnyAsync<Exception>(() => sink.PreflightAsync(handle, CancellationToken.None));
                // Refused at preflight: there is no channel, so no secret can be delivered anywhere.
                await Assert.ThrowsAnyAsync<Exception>(() => sink.DeliverOnceAsync(Guid.NewGuid(), "fixture-client", "never-written", CancellationToken.None));
            }
            Assert.Equal(0, file.Length);
        }
        finally
        {
            Close(terminal);
            Close(sockets[0]);
            Close(sockets[1]);
        }
    }

    // The twin of the Windows GetStdHandle comparison, by IDENTITY: under `dotnet test` stdout and stderr are pipes, so
    // a duplicate of them passes a plain "is it a pipe" test and would carry the secret to whatever reads stdout.
    [UnixFact]
    public async Task A_duplicate_of_stdout_or_stderr_is_refused_even_though_it_is_a_pipe()
    {
        Assert.Equal(UnixPipeHandleInspector.TypeFifo, UnixPipeHandleInspector.Stat(1)?.Type);
        foreach (var standard in new[] { 1, 2 })
        {
            var copy = Dup(standard);
            Assert.True(copy > 2);
            try
            {
                Assert.Equal(UnixPipeHandleInspector.TypeFifo, UnixPipeHandleInspector.Stat(copy)?.Type);
                await using var sink = new ServiceClientSecretOutputSink();
                await Assert.ThrowsAnyAsync<Exception>(() => sink.PreflightAsync(copy.ToString(), CancellationToken.None));
            }
            finally
            {
                Close(copy);
            }
        }
    }

    private static async Task<(string Stdout, string Stderr)> CaptureConsoleAsync(Func<Task> action)
    {
        var (out0, err0) = (Console.Out, Console.Error);
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        Console.SetOut(stdout);
        Console.SetError(stderr);
        try { await action(); }
        finally
        {
            Console.SetOut(out0);
            Console.SetError(err0);
        }
        return (stdout.ToString(), stderr.ToString());
    }

    [DllImport("libc", EntryPoint = "dup", SetLastError = true)]
    private static extern int Dup(int descriptor);
    [DllImport("libc", EntryPoint = "close", SetLastError = true)]
    private static extern int Close(int descriptor);
    [DllImport("libc", EntryPoint = "posix_openpt", SetLastError = true)]
    private static extern int PosixOpenPt(int flags);
    [DllImport("libc", EntryPoint = "isatty", SetLastError = true)]
    private static extern int IsATty(int descriptor);
    [DllImport("libc", EntryPoint = "socketpair", SetLastError = true)]
    private static extern int SocketPair(int domain, int type, int protocol, int[] descriptors);
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ConsoleCaptureCollection
{
    public const string Name = "Console capture (process-wide Console.Out)";
}
