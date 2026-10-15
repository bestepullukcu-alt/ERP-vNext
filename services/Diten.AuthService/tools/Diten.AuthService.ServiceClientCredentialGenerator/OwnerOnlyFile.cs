using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;

namespace Diten.AuthService.ServiceClientCredentialGenerator;

/// <summary>
/// Creates a NEW file that only its owner can read or write, and writes it once. An existing path is refused —
/// never opened, never truncated — so a second run cannot replace a secret that is already in use.
/// </summary>
public static class OwnerOnlyFile
{
    /// <exception cref="IOException">The path already exists, or the file could not be created exclusively.</exception>
    public static void CreateNew(string path, ReadOnlySpan<char> content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        var bytes = new byte[Encoding.UTF8.GetByteCount(content)];
        try
        {
            Encoding.UTF8.GetBytes(content, bytes);
            // Opening is the exclusive create: if it throws, nothing of ours exists and nothing is removed.
            using var stream = OperatingSystem.IsWindows() ? OpenWindows(fullPath) : OpenUnix(fullPath);
            try
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            catch
            {
                // The file is ours (we just created it): a half-written secret must not be left behind.
                stream.Dispose();
                File.Delete(fullPath);
                throw;
            }
        }
        finally
        {
            Array.Clear(bytes);
        }
    }

    [UnsupportedOSPlatform("windows")]
    private static FileStream OpenUnix(string fullPath) => new(fullPath, new FileStreamOptions
    {
        // CreateNew is O_CREAT|O_EXCL: it fails on an existing file AND on an existing symbolic link.
        Mode = FileMode.CreateNew,
        Access = FileAccess.Write,
        Share = FileShare.None,
        UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite
    });

    [SupportedOSPlatform("windows")]
    private static FileStream OpenWindows(string fullPath)
    {
        // The ACL is part of the create call, so the file never exists with inherited (wider) permissions.
        var owner = WindowsIdentity.GetCurrent().User
            ?? throw new IOException("The current Windows user could not be resolved.");
        var security = new FileSecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.SetOwner(owner);
        security.AddAccessRule(new FileSystemAccessRule(owner, FileSystemRights.FullControl, AccessControlType.Allow));
        return new FileInfo(fullPath).Create(
            FileMode.CreateNew, FileSystemRights.Write, FileShare.None, 4096, FileOptions.None, security);
    }
}
