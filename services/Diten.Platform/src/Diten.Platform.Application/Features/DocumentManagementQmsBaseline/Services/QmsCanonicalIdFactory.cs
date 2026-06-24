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
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(material));

        var hex = Convert.ToHexString(hash); // uppercase 0-9A-F, length 64
        var middle = hex[..12];              // [A-Z0-9]{12}  ∈ {2,16}
        var suffix = (BitConverter.ToUInt32(hash, 12) % 1000000).ToString("D6"); // [0-9]{6} ∈ {3,6}

        return $"CAN-QMS-{middle}-{suffix}";
    }
}
