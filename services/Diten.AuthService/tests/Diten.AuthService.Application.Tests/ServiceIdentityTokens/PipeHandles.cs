using System.IO.Pipes;
using System.Runtime.InteropServices;
using Xunit;

namespace Diten.AuthService.Application.Tests.ServiceIdentityTokens;

/// <summary>
/// WP-AUTH-BL570-01 FIX1 M9 — one owner per descriptor. On Unix the client handle string IS the server's descriptor
/// number, and a sink that opens it owns the same fd the server still owns: both close it (a double close that can
/// close an unrelated, reused descriptor). The sink therefore gets its own duplicate and the server drops its copy.
/// Windows keeps its handle as before.
/// </summary>
internal static class PipeHandles
{
    public static string ForSink(AnonymousPipeServerStream pipe)
    {
        var handle = pipe.GetClientHandleAsString();
        if (OperatingSystem.IsWindows()) return handle;
        var copy = Dup(int.Parse(handle, System.Globalization.CultureInfo.InvariantCulture));
        Assert.True(copy > 2, "dup of the client end failed");
        pipe.DisposeLocalCopyOfClientHandle();
        return copy.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    [DllImport("libc", EntryPoint = "dup", SetLastError = true)]
    private static extern int Dup(int descriptor);
}
