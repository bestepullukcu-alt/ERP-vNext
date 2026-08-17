using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.Platform.Domain.Entities.InterfaceRegistry;

namespace Diten.Platform.Application.Features.InterfaceRegistry;

public static class InterfaceManifestHasher
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static string HashSnapshot(InterfaceDefinitionSnapshot snapshot)
    {
        var json = JsonSerializer.Serialize(snapshot, SerializerOptions);
        return ComputeSha256(json);
    }

    public static string HashManifest(IReadOnlyList<InterfaceDefinitionSnapshot> snapshots)
    {
        var json = JsonSerializer.Serialize(snapshots.OrderBy(x => x.InterfaceCode).ThenBy(x => x.InterfaceVersion), SerializerOptions);
        return ComputeSha256(json);
    }

    private static string ComputeSha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
