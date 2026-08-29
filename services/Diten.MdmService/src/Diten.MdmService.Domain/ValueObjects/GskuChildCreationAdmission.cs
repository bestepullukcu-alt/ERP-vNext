using System.Security.Cryptography;
using System.Text;

namespace Diten.MdmService.Domain.ValueObjects;

public enum GskuChildIdentityKind
{
    Lsku = 1,
    FinishedGood = 2
}

public sealed class GskuChildCreationAdmission
{
    public const int MaximumActiveAdmissions = 32;

    public GskuChildIdentityKind ChildKind { get; set; }
    public string CreationCommandId { get; set; } = string.Empty;
    public string RequestFingerprint { get; set; } = string.Empty;
    public DateTimeOffset AcquiredAtUtc { get; set; }

    public static string ComputeRequestFingerprint(
        Guid gskuId,
        GskuChildIdentityKind childKind,
        string creationCommandId,
        string? marketCode = null)
    {
        var values = new[]
        {
            gskuId.ToString("D"),
            ((int)childKind).ToString(System.Globalization.CultureInfo.InvariantCulture),
            creationCommandId,
            marketCode ?? string.Empty
        };
        var canonical = string.Concat(values.Select(value => $"{value.Length}:{value}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
