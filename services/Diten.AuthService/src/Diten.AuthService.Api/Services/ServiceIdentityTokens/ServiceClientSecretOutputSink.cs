using System.Globalization;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Diten.AuthService.Application.Common.Interfaces;
using Microsoft.Win32.SafeHandles;

namespace Diten.AuthService.Api.Services.ServiceIdentityTokens;

/// <summary>One-shot output only to an inherited, validated OS pipe. Never opens an output path.</summary>
public sealed class ServiceClientSecretOutputSink : IServiceClientSecretOutputSink, IAsyncDisposable
{
    private AnonymousPipeClientStream? _pipe;
    private int _preflightAttempted;
    private int _delivered;

    public Task PreflightAsync(string inheritedPipeHandle, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Interlocked.Exchange(ref _preflightAttempted, 1) != 0
            || string.IsNullOrWhiteSpace(inheritedPipeHandle) || inheritedPipeHandle.Length > 128
            || inheritedPipeHandle.IndexOfAny(['/', '\\', ':']) >= 0) throw Invalid();
        ValidateInheritedPipeHandle(inheritedPipeHandle);
        try
        {
            _pipe = new AnonymousPipeClientStream(PipeDirection.Out, inheritedPipeHandle);
            if (!_pipe.CanWrite) throw Invalid();
            ValidateConnectedPeer(_pipe.SafePipeHandle);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or InvalidOperationException)
        {
            _pipe?.Dispose();
            _pipe = null;
            throw Invalid();
        }
        return Task.CompletedTask;
    }

    public async Task DeliverOnceAsync(Guid serviceClientIdentityId, string clientCode, string rawSecret,
        CancellationToken cancellationToken)
    {
        if (_pipe is null || Interlocked.CompareExchange(ref _delivered, 1, 0) != 0) throw Invalid();
        byte[]? payload = null;
        try
        {
            if (serviceClientIdentityId == Guid.Empty || string.IsNullOrWhiteSpace(clientCode)
                || clientCode.Length > 128 || clientCode.Any(char.IsControl)
                || string.IsNullOrWhiteSpace(rawSecret) || rawSecret.Length > 1024) throw Invalid();
            payload = JsonSerializer.SerializeToUtf8Bytes(new SecretDeliveryEnvelope(serviceClientIdentityId,
                clientCode, rawSecret));
            if (payload.Length > 4096) throw Invalid();
            await _pipe.WriteAsync(payload, cancellationToken);
            await _pipe.FlushAsync(cancellationToken);
        }
        finally
        {
            if (payload is not null) CryptographicOperations.ZeroMemory(payload);
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

    private static void ValidateInheritedPipeHandle(string value)
    {
        if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            || number <= 2 || value != number.ToString(CultureInfo.InvariantCulture)) throw Invalid();
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("This operational secret sink requires Windows pipe validation.");
        if (IntPtr.Size == 4 && number > int.MaxValue) throw Invalid();
        var handle = new IntPtr(number);
        if (handle == GetStdHandle(-10) || handle == GetStdHandle(-11) || handle == GetStdHandle(-12)
            || GetFileType(handle) != FileTypePipe) throw Invalid();
    }

    private static InvalidOperationException Invalid() => new("The inherited one-shot secret pipe is invalid or unavailable.");

    private static void ValidateConnectedPeer(SafePipeHandle handle)
    {
        // Anonymous pipes use the Windows named-pipe implementation. FilePipeLocalInformation
        // exposes connection state without reading/writing payload or waiting for peer I/O.
        // GetFileType/GetNamedPipeHandleState can still succeed after the reader has closed.
        // This is a point-in-time preflight, not a guarantee against a later peer disconnect.
        var size = checked((uint)Marshal.SizeOf<FilePipeLocalInformation>());
        var status = NtQueryInformationFile(handle, out var completion, out var information, size,
            FilePipeLocalInformationClass);
        if (status != 0 || completion.Status != IntPtr.Zero || completion.Information.ToUInt64() != size
            || information.NamedPipeState != FilePipeConnectedState) throw Invalid();
    }

    private const int FilePipeLocalInformationClass = 24;
    private const uint FilePipeConnectedState = 3;
    [StructLayout(LayoutKind.Sequential)]
    private struct IoStatusBlock
    {
        public IntPtr Status;
        public UIntPtr Information;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct FilePipeLocalInformation
    {
        public uint NamedPipeType;
        public uint NamedPipeConfiguration;
        public uint MaximumInstances;
        public uint CurrentInstances;
        public uint InboundQuota;
        public uint ReadDataAvailable;
        public uint OutboundQuota;
        public uint WriteQuotaAvailable;
        public uint NamedPipeState;
        public uint NamedPipeEnd;
    }
    [DllImport("ntdll.dll", ExactSpelling = true)]
    private static extern int NtQueryInformationFile(SafePipeHandle handle, out IoStatusBlock completion,
        out FilePipeLocalInformation information, uint length, int informationClass);

    private const uint FileTypePipe = 0x0003;
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int standardHandle);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint GetFileType(IntPtr handle);
    private sealed record SecretDeliveryEnvelope(Guid ServiceClientIdentityId, string ClientCode, string Secret);
}
