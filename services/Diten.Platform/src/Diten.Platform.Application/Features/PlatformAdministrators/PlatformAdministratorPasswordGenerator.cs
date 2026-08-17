using System.Security.Cryptography;

namespace Diten.Platform.Application.Features.PlatformAdministrators;

public static class PlatformAdministratorPasswordGenerator
{
    private const string Uppercase = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Lowercase = "abcdefghijkmnopqrstuvwxyz";
    private const string Digits = "23456789";
    private const string Specials = "!@$%*?";

    public static string Generate()
    {
        var chars = new List<char>
        {
            Pick(Uppercase),
            Pick(Lowercase),
            Pick(Digits),
            Pick(Specials)
        };

        var all = Uppercase + Lowercase + Digits + Specials;
        while (chars.Count < 14)
        {
            chars.Add(Pick(all));
        }

        return new string(chars.OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue)).ToArray());
    }

    private static char Pick(string source) => source[RandomNumberGenerator.GetInt32(source.Length)];
}
