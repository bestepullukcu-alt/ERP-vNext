using System.IO.Pipes;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using Diten.AuthService.Application.Common.Interfaces;

namespace Diten.AuthService.Api.Services.ServiceIdentityTokens;

public sealed class ServiceClientSecretOutputSink : IServiceClientSecretOutputSink, IAsyncDisposable
{
    private AnonymousPipeClientStream? _pipe;
    private int _delivered;

    public Task PreflightAsync(string inheritedPipeHandle, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_pipe is not null || string.IsNullOrWhiteSpace(inheritedPipeHandle) || inheritedPipeHandle.Length > 128)
        {
            throw new InvalidOperationException("The inherited one-shot secret pipe is invalid.");
        }

        if (inheritedPipeHandle.IndexOfAny(['/', '\\', ':']) >= 0)
        {
            throw new InvalidOperationException("Secret output paths are not supported.");
        }

        ValidateInheritedPipeHandle(inheritedPipeHandle);

        try
        {
            _pipe = new AnonymousPipeClientStream(PipeDirection.Out, inheritedPipeHandle);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException)
        {
            throw new InvalidOperationException("The inherited one-shot secret pipe is unavailable.");
        }

        if (!_pipe.CanWrite)
        {
            _pipe.Dispose();
            _pipe = null;
            throw new InvalidOperationException("The inherited one-shot secret pipe is not writable.");
        }

        return Task.CompletedTask;
    }

    public async Task DeliverOnceAsync(
        Guid serviceClientIdentityId,
        string clientCode,
        string rawSecret,
        CancellationToken cancellationToken)
    {
        if (_pipe is null || Interlocked.CompareExchange(ref _delivered, 1, 0) != 0)
        {
            throw new InvalidOperationException("Secret output is unavailable or has already been consumed.");
        }

        if (serviceClientIdentityId == Guid.Empty
            || string.IsNullOrWhiteSpace(clientCode)
            || string.IsNullOrWhiteSpace(rawSecret))
        {
            throw new InvalidOperationException("Secret output payload is invalid.");
        }

        byte[]? payload = null;
        try
        {
            payload = JsonSerializer.SerializeToUtf8Bytes(new SecretDeliveryEnvelope(
                serviceClientIdentityId,
                clientCode,
                rawSecret));
            if (payload.Length > 4096)
            {
                throw new InvalidOperationException("Secret output payload exceeds the one-shot pipe budget.");
            }

            await _pipe.WriteAsync(payload, cancellationToken);
            await _pipe.FlushAsync(cancellationToken);
        }
        finally
        {
            if (payload is not null)
            {
                Array.Clear(payload);
            }

            await _pipe.DisposeAsync();
            _pipe = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_pipe is not null)
        {
            await _pipe.DisposeAsync();
            _pipe = null;
        }
    }

    private static void ValidateInheritedPipeHandle(string inheritedPipeHandle)
    {
        if (!long.TryParse(
                inheritedPipeHandle,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var numericHandle))
        {
            throw new InvalidOperationException("The inherited one-shot secret pipe handle is invalid.");
        }

        if (!OperatingSystem.IsWindows())
        {
            if (numericHandle is >= 0 and <= 2 || numericHandle > int.MaxValue)
            {
                throw new InvalidOperationException("Standard streams cannot be used for secret output.");
            }

            return;
        }

        var handle = new IntPtr(numericHandle);
        if (handle == IntPtr.Zero
            || handle == new IntPtr(-1)
            || handle == GetStdHandle(StandardInputHandle)
            || handle == GetStdHandle(StandardOutputHandle)
            || handle == GetStdHandle(StandardErrorHandle))
        {
            throw new InvalidOperationException("Standard streams cannot be used for secret output.");
        }

        if (GetFileType(handle) != FileTypePipe)
        {
            throw new InvalidOperationException("The inherited secret output handle is not a pipe.");
        }
    }

    private const int StandardInputHandle = -10;
    private const int StandardOutputHandle = -11;
    private const int StandardErrorHandle = -12;
    private const uint FileTypePipe = 0x0003;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int standardHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint GetFileType(IntPtr handle);

    private sealed record SecretDeliveryEnvelope(Guid ServiceClientIdentityId, string ClientCode, string Secret);
}
