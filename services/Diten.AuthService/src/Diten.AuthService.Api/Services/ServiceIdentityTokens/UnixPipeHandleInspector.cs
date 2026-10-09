using System.Runtime.InteropServices;

namespace Diten.AuthService.Api.Services.ServiceIdentityTokens;

/// <summary>
/// WP-AUTH-BL570-01 — the Unix twin of the Windows pipe checks in <see cref="ServiceClientSecretOutputSink"/>. It only
/// INSPECTS a descriptor the process already holds (fstat / fcntl / poll); it never opens a path.
///
/// <para><b>Fail-closed by construction.</b> <c>struct stat</c> differs per OS and CPU, so the field offsets below are
/// a table, and the table is PROVEN on the running machine before it is trusted: a self-check makes a private pipe and
/// socket pair and requires the expected answers (pipe = FIFO, socket = socket, a dup has the same identity, a second
/// pipe a different one, write access on the write end only, poll writable while the reader lives and hung up once it
/// is closed). Any other answer — an unknown OS / CPU, a missing libc entry point, a layout that reads wrong — makes
/// the platform unsupported and no secret is produced. Measured 2026-10-07 on macOS 26.6 arm64; macOS x64 and Linux
/// x64 / arm64 rows come from the system headers and are taken only once the self-check passes there.</para>
/// </summary>
public static class UnixPipeHandleInspector
{
    public const int TypeMask = 0xF000;
    public const int TypeFifo = 0x1000;
    public const int TypeSocket = 0xC000;

    private const int GetDescriptorFlags = 1; // F_GETFD, same on macOS and Linux
    private const int CloseOnExec = 1; // FD_CLOEXEC
    private const int GetStatusFlags = 3; // F_GETFL, same on macOS and Linux
    private const int AccessModeMask = 3; // O_ACCMODE
    private const int WriteOnly = 1;
    private const short PollOut = 0x4;
    private const short PollError = 0x8;
    private const short PollHangUp = 0x10;
    private const short PollInvalid = 0x20;

    public readonly record struct Identity(long Device, ulong Inode, int Type);

    // Internal (tests via InternalsVisibleTo, BL570 FIX1 R2): a wrong layout must be refused by the proof.
    internal sealed record Layout(int DeviceOffset, int DeviceSize, int ModeOffset, int ModeSize, int InodeOffset, bool MacOs);

    private static readonly Lazy<Layout?> Proven = new(() => Candidate() is { } candidate ? Prove(candidate) : null,
        LazyThreadSafetyMode.ExecutionAndPublication);

    public static bool IsSupported => Proven.Value is not null;

    /// <summary>The descriptor's identity, or null when it is not an open descriptor (or the platform is unsupported).</summary>
    public static Identity? Stat(int descriptor) => Proven.Value is { } layout ? Stat(descriptor, layout) : null;

    /// <summary>
    /// Opened WRITE-ONLY — exactly O_WRONLY (BL570 FIX1 R1). A read-write end (<c>exec 3&lt;&gt;fifo</c>) makes the
    /// process its own reader: poll never reports a hang-up and the secret would sit in a named FIFO's kernel buffer
    /// while "written once" is reported. The proof requires the same of a real pipe's write end.
    /// </summary>
    public static bool IsWriteOnly(int descriptor)
    {
        var flags = Fcntl(descriptor, GetStatusFlags);
        return flags >= 0 && (flags & AccessModeMask) == WriteOnly;
    }

    /// <summary>
    /// INHERITED across exec: FD_CLOEXEC clear (BL570 FIX1 M1). The runtime's own pipes (signal / wake-up) are
    /// close-on-exec; a descriptor the launcher handed over survived exec, so it is not.
    /// </summary>
    public static bool IsInherited(int descriptor)
    {
        var flags = Fcntl(descriptor, GetDescriptorFlags);
        return flags >= 0 && (flags & CloseOnExec) == 0;
    }

    /// <summary>
    /// Writable now, and its reader not gone: POLLOUT without POLLERR / POLLHUP / POLLNVAL (macOS reports a closed reader
    /// as POLLHUP, Linux as POLLERR). A point-in-time preflight, like the Windows connection-state check.
    /// </summary>
    public static bool PeerIsConnected(int descriptor)
    {
        if (Proven.Value is not { } layout) return false;
        var revents = PollWrite(descriptor, layout.MacOs);
        return revents is { } value && (value & PollOut) != 0 && (value & (PollError | PollHangUp | PollInvalid)) == 0;
    }

    internal static Identity? Stat(int descriptor, Layout layout)
    {
        var buffer = new byte[512];
        if (Fstat(descriptor, buffer, layout.MacOs) != 0) return null;
        long device = layout.DeviceSize == 4 ? BitConverter.ToInt32(buffer, layout.DeviceOffset) : BitConverter.ToInt64(buffer, layout.DeviceOffset);
        int mode = layout.ModeSize == 2 ? BitConverter.ToUInt16(buffer, layout.ModeOffset) : (int)BitConverter.ToUInt32(buffer, layout.ModeOffset);
        return new Identity(device, BitConverter.ToUInt64(buffer, layout.InodeOffset), mode & TypeMask);
    }

    internal static Layout? Candidate()
    {
        if (!BitConverter.IsLittleEndian) return null;
        if (OperatingSystem.IsMacOS() && RuntimeInformation.ProcessArchitecture is Architecture.Arm64 or Architecture.X64)
            return new Layout(0, 4, 4, 2, 8, MacOs: true); // struct stat with 64-bit inodes
        if (OperatingSystem.IsLinux() && RuntimeInformation.ProcessArchitecture == Architecture.X64)
            return new Layout(0, 8, 24, 4, 8, MacOs: false);
        if (OperatingSystem.IsLinux() && RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
            return new Layout(0, 8, 16, 4, 8, MacOs: false); // asm-generic
        return null;
    }

    internal static Layout? Prove(Layout layout)
    {
        var opened = new List<int>();
        try
        {
            var first = new int[2];
            var second = new int[2];
            var sockets = new int[2];
            if (Pipe(first) != 0) return null;
            opened.AddRange(first);
            if (Pipe(second) != 0) return null;
            opened.AddRange(second);
            if (SocketPair(1, 1, 0, sockets) != 0) return null; // AF_UNIX, SOCK_STREAM
            opened.AddRange(sockets);
            var copy = Dup(first[1]);
            if (copy < 0) return null;
            opened.Add(copy);

            var reader = Stat(first[0], layout);
            var writer = Stat(first[1], layout);
            var writerCopy = Stat(copy, layout);
            var other = Stat(second[1], layout);
            var socket = Stat(sockets[0], layout);
            if (reader?.Type != TypeFifo || writer?.Type != TypeFifo || socket?.Type != TypeSocket) return null;
            if (writerCopy is null || writerCopy.Value.Device != writer.Value.Device || writerCopy.Value.Inode != writer.Value.Inode)
                return null;
            if (other is null || (other.Value.Device == writer.Value.Device && other.Value.Inode == writer.Value.Inode)) return null;
            if ((Fcntl(first[1], GetStatusFlags) & AccessModeMask) != WriteOnly || (Fcntl(first[0], GetStatusFlags) & AccessModeMask) != 0)
                return null;

            var live = PollWrite(first[1], layout.MacOs);
            if (live is not { } before || (before & PollOut) == 0 || (before & (PollError | PollHangUp | PollInvalid)) != 0) return null;
            Close(first[0]);
            opened.Remove(first[0]);
            var gone = PollWrite(first[1], layout.MacOs);
            if (gone is not { } after || (after & (PollError | PollHangUp)) == 0) return null;
            return layout;
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException or MarshalDirectiveException)
        {
            return null;
        }
        finally
        {
            foreach (var descriptor in opened) Close(descriptor);
        }
    }

    private static short? PollWrite(int descriptor, bool macOs)
    {
        var entry = new PollDescriptor { Descriptor = descriptor, Events = PollOut };
        var result = macOs ? PollMac(ref entry, 1, 0) : PollLinux(ref entry, 1, 0);
        return result < 0 ? null : entry.ReturnedEvents;
    }

    private static int Fstat(int descriptor, byte[] buffer, bool macOs) =>
        macOs && RuntimeInformation.ProcessArchitecture == Architecture.X64
            ? FstatInode64(descriptor, buffer)
            : FstatPlain(descriptor, buffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct PollDescriptor
    {
        public int Descriptor;
        public short Events;
        public short ReturnedEvents;
    }

    [DllImport("libc", EntryPoint = "fstat", SetLastError = true)]
    private static extern int FstatPlain(int descriptor, byte[] buffer);
    [DllImport("libc", EntryPoint = "fstat$INODE64", SetLastError = true)]
    private static extern int FstatInode64(int descriptor, byte[] buffer);
    [DllImport("libc", EntryPoint = "fcntl", SetLastError = true)]
    private static extern int Fcntl(int descriptor, int command);
    // nfds_t: unsigned int on macOS, unsigned long on Linux.
    [DllImport("libc", EntryPoint = "poll", SetLastError = true)]
    private static extern int PollMac(ref PollDescriptor descriptors, uint count, int timeout);
    [DllImport("libc", EntryPoint = "poll", SetLastError = true)]
    private static extern int PollLinux(ref PollDescriptor descriptors, nuint count, int timeout);
    [DllImport("libc", EntryPoint = "pipe", SetLastError = true)]
    private static extern int Pipe(int[] descriptors);
    [DllImport("libc", EntryPoint = "socketpair", SetLastError = true)]
    private static extern int SocketPair(int domain, int type, int protocol, int[] descriptors);
    [DllImport("libc", EntryPoint = "dup", SetLastError = true)]
    private static extern int Dup(int descriptor);
    [DllImport("libc", EntryPoint = "close", SetLastError = true)]
    private static extern int Close(int descriptor);
}
