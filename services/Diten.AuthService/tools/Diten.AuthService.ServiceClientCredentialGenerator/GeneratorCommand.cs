using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.ServiceClientCredentialGenerator;

/// <summary>
/// MOD-0033-FU02 §F.2 — the command line of the offline generator.
///
/// <para><c>--secret-file &lt;path&gt;</c> (required): where the RAW secret is written, once, owner-only. An existing
/// path is refused. <c>--credential-version &lt;text&gt;</c> (optional): the version label; a UTC timestamp when
/// omitted. <c>--client-code &lt;code&gt;</c> (optional): also prints the client id Auth will give that code.</para>
///
/// <para>Standard output carries the HASH and the version only. The raw secret is never written to a standard
/// stream, never accepted as input, and never placed in an exception message.</para>
/// </summary>
public static class GeneratorCommand
{
    public const int Success = 0;
    public const int UsageError = 2;
    public const int FileError = 3;
    public const int UnexpectedError = 4;

    public const string Usage =
        "usage: --secret-file <new file path> [--credential-version <label>] [--client-code <code>]";

    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (!TryParse(args, out var options))
        {
            // The argument list is not echoed: an operator who mistakenly passed a secret must not see it logged back.
            error.WriteLine(Usage);
            return UsageError;
        }

        try
        {
            var secretFile = Path.GetFullPath(options.SecretFile);
            if (File.Exists(secretFile) || Directory.Exists(secretFile))
            {
                error.WriteLine("refused: the secret file path already exists; nothing was generated.");
                return FileError;
            }

            string hash;
            try
            {
                hash = ServiceClientCredentialFactory.Create(secret => OwnerOnlyFile.CreateNew(secretFile, secret));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                error.WriteLine("refused: the secret file could not be created exclusively; nothing was generated.");
                return FileError;
            }

            output.WriteLine("CredentialHash=" + hash);
            output.WriteLine("CredentialVersion=" + options.CredentialVersion);
            if (options.ClientCode is not null)
            {
                output.WriteLine("ClientCode=" + options.ClientCode);
                output.WriteLine("ClientId=" + ServiceClientIdentityIds.FromClientCode(options.ClientCode).ToString("D"));
            }

            output.WriteLine("SecretFile=" + secretFile);
            return Success;
        }
        catch (Exception exception)
        {
            // Type name only: an exception message could quote a path or, in a future change, a value.
            error.WriteLine("failed: " + exception.GetType().Name);
            return UnexpectedError;
        }
    }

    private static bool TryParse(string[] args, out Options options)
    {
        options = default!;
        string? secretFile = null, version = null, clientCode = null;
        if (args.Length % 2 != 0) return false;
        for (var index = 0; index < args.Length; index += 2)
        {
            var value = args[index + 1];
            switch (args[index])
            {
                case "--secret-file" when secretFile is null: secretFile = value; break;
                case "--credential-version" when version is null: version = value; break;
                case "--client-code" when clientCode is null: clientCode = value; break;
                default: return false; // unknown, case-variant or duplicate flag — including any "--secret"
            }
        }

        version ??= "v" + DateTimeOffset.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'");
        // The client code takes the shape Auth's registry accepts for it (no comma: it travels in a header).
        if (string.IsNullOrWhiteSpace(secretFile) || !IsExactLabel(version, 64)
            || (clientCode is not null && (!IsExactLabel(clientCode, 128) || clientCode.Contains(','))))
        {
            return false;
        }

        options = new Options(secretFile, version, clientCode);
        return true;
    }

    private static bool IsExactLabel(string value, int maximum) =>
        value.Length is > 0 && value.Length <= maximum
        && string.Equals(value, value.Trim(), StringComparison.Ordinal)
        && !value.Any(char.IsControl);

    private sealed record Options(string SecretFile, string CredentialVersion, string? ClientCode);
}
