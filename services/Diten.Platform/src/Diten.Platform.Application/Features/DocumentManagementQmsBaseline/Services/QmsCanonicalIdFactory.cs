using System.Security.Cryptography;
using System.Text;

namespace Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Services;

/// <summary>
/// Deterministic CanonicalId generation. The same <c>tenant + sourceBaselineKey + normalized full path</c> always
/// yields the same id, so re-import and manifest hashing are reproducible. Format conforms to the parent pack rule
/// <c>^CAN-[A-Z0-9]{2,10}-[A-Z0-9]{2,16}-[0-9]{3,6}$</c>.
/// </summary>
public static class QmsCanonicalIdFactory
{
    public static string Create(Guid tenantId, string sourceBaselineKey, string normalizedFullPath)
    {
        var material = $"{tenantId:N}|{sourceBaselineKey}|{QmsFolderPathNormalizer.CaseInsensitiveKey(normalizedFullPath)}";
        return Build(material);
    }

    /// <summary>
    /// QMS register import extension — governance identity pending. Register-backed stable CanonicalId derives
    /// identity from the register's stable
    /// <c>folder_id</c> (e.g. <c>ENT-00</c>) instead of the full path, so the identity is invariant under a later
    /// folder rename/move. Same format/regex as <see cref="Create"/>, still tenant- and baseline-scoped and
    /// deterministic. Use this only when the source row carries a register folder id; otherwise fall back to
    /// <see cref="Create"/> so legacy (path-hash) imports keep their exact historical identity.
    /// </summary>
    public static string CreateFromRegisterFolderId(Guid tenantId, string sourceBaselineKey, string registerFolderId)
    {
        // The "fid|" tag keeps the register-id keyspace disjoint from the path keyspace so a folder_id that
        // happens to equal a full path can never collide with a path-derived id.
        var material = $"{tenantId:N}|{sourceBaselineKey}|fid|{registerFolderId.Trim().ToLowerInvariant()}";
        return Build(material);
    }

    private static string Build(string material)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(material));

        var hex = Convert.ToHexString(hash); // uppercase 0-9A-F, length 64
        var middle = hex[..12];              // [A-Z0-9]{12}  ∈ {2,16}
        var suffix = (BitConverter.ToUInt32(hash, 12) % 1000000).ToString("D6"); // [0-9]{6} ∈ {3,6}

        return $"CAN-QMS-{middle}-{suffix}";
    }
}
