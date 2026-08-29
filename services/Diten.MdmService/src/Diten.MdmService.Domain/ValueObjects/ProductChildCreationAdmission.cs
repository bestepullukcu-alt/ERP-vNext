using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Diten.MdmService.Domain.ValueObjects;

public sealed class ProductChildCreationAdmission
{
    public const int MaximumActiveAdmissions = 32;

    public string CreationCommandId { get; set; } = string.Empty;
    public string RequestFingerprint { get; set; } = string.Empty;
    public DateTimeOffset AcquiredAtUtc { get; set; }

    public static string ComputeRequestFingerprint(
        Guid globalProductId,
        string creationCommandId,
        Guid gskuReservationId,
        decimal packQuantity,
        string packUomCode)
    {
        var facts = string.Join('|',
            creationCommandId,
            globalProductId.ToString("D"),
            gskuReservationId.ToString("D"),
            packQuantity.ToString("G29", CultureInfo.InvariantCulture),
            packUomCode);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(facts)));
    }
}
