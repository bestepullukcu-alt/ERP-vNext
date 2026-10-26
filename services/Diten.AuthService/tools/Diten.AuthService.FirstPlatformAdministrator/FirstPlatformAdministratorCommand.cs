using Diten.AuthService.Application.Features.Users.Services;
using Diten.AuthService.Infrastructure.Services;
using Diten.AuthService.Infrastructure.Settings;
using Diten.AuthService.Persistence.Operations;
using Diten.AuthService.ServiceClientCredentialGenerator;
using Diten.BuildingBlocks.Security.Secrets;
using Diten.Platform.Infrastructure.Persistence.Configurations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Diten.AuthService.FirstPlatformAdministrator;

/// <summary>
/// WP-AUTH-ADMIN-BOOTSTRAP-01 (BL-534 + BL-556, owner decision 2026-10-15) — creates a NEW environment's first platform
/// administrator. Outside Development no start-up creates an account any more; this command is how the first one is made.
/// <list type="bullet">
/// <item>The Auth account (<see cref="FirstPlatformAdministratorAccount"/>) and the Platform record
/// (<see cref="FirstPlatformAdministratorRecord"/>) are written under ONE new id: the account first (an inactive
/// invitation), the record last — the link works only once both exist.</item>
/// <item>No password is made, printed or stored. The account gets the tenant administrator invitation's one-time
/// set-password link: Auth's production token generator makes it, Auth's production hasher hashes it with the service's
/// own JWT secret (only the hash is stored), Auth's production builder makes the URL. The link is written to a NEW file
/// only the current user can read (<see cref="OwnerOnlyFile"/>); the output names the file, never the link.</item>
/// <item>Only for an environment with no platform administrator: if either side holds one (or anything under this id,
/// address or user name), NOTHING is written — a second run changes nothing.</item>
/// </list>
/// <para>Arguments: <c>--email &lt;address&gt; --frontend-base-url &lt;url&gt; --link-file &lt;new path&gt;
/// [--user-name &lt;name&gt;] [--display-name &lt;name&gt;]</c>. The connections and the secret come from the environment
/// (<see cref="AuthConnectionVariable"/>, <see cref="AuthDatabaseVariable"/>, <see cref="PlatformConnectionVariable"/>,
/// <see cref="PlatformDatabaseVariable"/>, <see cref="JwtSecretVariable"/>), never the command line, and are never
/// written to any output: a failure names only the exception's type.</para>
/// <para>Exit codes: 0 created · 1 connection or unexpected failure · 2 usage · 3 refused, nothing changed (an
/// administrator, or something under this id / address / user name, exists) · 4 stopped part-way (the Auth account was
/// written, the Platform record was not; named by id).</para>
/// </summary>
public static class FirstPlatformAdministratorCommand
{
    public const string AuthConnectionVariable = "DITEN_AUTH_MONGO_CONNECTION";
    public const string AuthDatabaseVariable = "DITEN_AUTH_MONGO_DATABASE";
    public const string PlatformConnectionVariable = "DITEN_PLATFORM_MONGO_CONNECTION";
    public const string PlatformDatabaseVariable = "DITEN_PLATFORM_MONGO_DATABASE";

    /// <summary>The Auth service's own JWT secret variable: the link's hash must be the one Auth computes when it is used.</summary>
    public const string JwtSecretVariable = "JwtSettings__Secret";

    public const int Created = 0;
    public const int Failure = 1;
    public const int UsageError = 2;
    public const int Refused = 3;
    public const int PartWay = 4;

    public const string Usage =
        "usage: --email <address> --frontend-base-url <url> --link-file <new path> [--user-name <name>] [--display-name <name>]";

    /// <param name="beforePlatformWrite">TEST SEAM — runs after the Auth account was written, before the Platform record.</param>
    public static async Task<int> RunAsync(
        string[] args, Func<string, string?> environment, TextWriter output, TextWriter error,
        Func<Task>? beforePlatformWrite = null, CancellationToken ct = default)
    {
        if (!TryParse(args, out var options, out var usageProblem))
        {
            error.WriteLine(usageProblem);
            error.WriteLine(Usage);
            return UsageError;
        }

        var authConnection = environment(AuthConnectionVariable);
        var authDatabaseName = environment(AuthDatabaseVariable);
        var platformConnection = environment(PlatformConnectionVariable);
        var platformDatabaseName = environment(PlatformDatabaseVariable);
        var jwtSecret = environment(JwtSecretVariable);
        if (string.IsNullOrWhiteSpace(authConnection) || string.IsNullOrWhiteSpace(authDatabaseName)
            || string.IsNullOrWhiteSpace(platformConnection) || string.IsNullOrWhiteSpace(platformDatabaseName)
            || string.IsNullOrWhiteSpace(jwtSecret))
        {
            error.WriteLine($"{AuthConnectionVariable}, {AuthDatabaseVariable}, {PlatformConnectionVariable}, "
                            + $"{PlatformDatabaseVariable} and {JwtSecretVariable} must be set.");
            return UsageError;
        }

        // The link file is checked before anything is read or written: a link that cannot be handed over must not exist.
        var linkPath = Path.GetFullPath(options.LinkFile);
        if (File.Exists(linkPath) || Directory.Exists(linkPath) || !Directory.Exists(Path.GetDirectoryName(linkPath)))
        {
            error.WriteLine("--link-file must be a NEW file in an existing directory; nothing was changed.");
            return UsageError;
        }

        IMongoDatabase authDatabase;
        IMongoDatabase platformDatabase;
        var id = Guid.NewGuid();
        var platformRequest = new FirstPlatformAdministratorRecord.Request(id, options.Email, options.UserName, options.DisplayName);
        string? blocker;
        try
        {
            // Platform's BSON setup first (it refuses an incompatible serializer already registered; Auth's is the same).
            FirstPlatformAdministratorRecord.PrepareSerialization();
            authDatabase = FirstPlatformAdministratorAccount.OpenDatabase(authConnection, authDatabaseName);
            platformDatabase = FirstPlatformAdministratorAccount.OpenDatabase(platformConnection, platformDatabaseName);
            blocker = await FirstPlatformAdministratorAccount.FindBlockerAsync(authDatabase, id, options.Email, options.UserName, ct)
                      ?? await FirstPlatformAdministratorRecord.FindBlockerAsync(platformDatabase, platformRequest, ct);
        }
        catch (Exception failure)
        {
            // Never the message: a driver exception can carry the connection string.
            error.WriteLine($"connection error ({failure.GetType().Name}); nothing was changed.");
            return Failure;
        }

        if (blocker is not null)
        {
            error.WriteLine($"REFUSED — {blocker}. Nothing was changed; this command only creates an environment's first "
                            + "platform administrator (further administrators are invited from the Platform console).");
            return Refused;
        }

        // Auth's production pieces, configured as the Auth service is: the same JWT secret, so the stored hash is the one the
        // set-password door computes from the link.
        var jwtSettings = Options.Create(new JwtSettings { Secret = jwtSecret });
        var tokens = new TokenService(jwtSettings, new JwtSecretRotationResolver(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["JwtSettings:Secret"] = jwtSecret }).Build()));
        var setupToken = tokens.GenerateRefreshToken();
        var expiresAtUtc = DateTime.UtcNow.Add(FirstPlatformAdministratorAccount.LinkLifetime);
        var link = new PlatformAuthEmailService(
                Options.Create(new SmtpOptions()),
                Options.Create(new PlatformServiceOptions { FrontendBaseUrl = options.FrontendBaseUrl }))
            .BuildPlatformPasswordResetUrl(options.Email, setupToken);
        var (firstName, lastName) = SplitName(options.DisplayName);
        var accountRequest = new FirstPlatformAdministratorAccount.Request(
            id,
            options.Email,
            options.UserName,
            firstName,
            lastName,
            await AdminPasswordReset.UnusableHashAsync(new PasswordHasher(), tokens, ct),
            new RefreshTokenHasher(jwtSettings).Hash(setupToken),
            expiresAtUtc);

        try
        {
            OwnerOnlyFile.CreateNew(linkPath, link + Environment.NewLine);
        }
        catch (Exception failure)
        {
            error.WriteLine($"the link file could not be created ({failure.GetType().Name}); nothing was changed.");
            return Failure;
        }

        bool accountWritten;
        try
        {
            accountWritten = await FirstPlatformAdministratorAccount.TryCreateAsync(authDatabase, accountRequest, ct);
        }
        catch (Exception failure)
        {
            error.WriteLine($"the Auth account could not be written ({failure.GetType().Name}); nothing was changed. "
                            + "The link file is void: delete it.");
            return Failure;
        }

        if (!accountWritten)
        {
            error.WriteLine("REFUSED — an account appeared meanwhile; nothing was changed. The link file is void: delete it.");
            return Refused;
        }

        if (beforePlatformWrite is not null)
        {
            await beforePlatformWrite();
        }

        bool recordWritten;
        Exception? recordFailure = null;
        try
        {
            recordWritten = await FirstPlatformAdministratorRecord.TryCreateAsync(platformDatabase, platformRequest, DateTimeOffset.UtcNow, ct);
        }
        catch (Exception failure)
        {
            recordWritten = false;
            recordFailure = failure;
        }

        if (!recordWritten)
        {
            error.WriteLine($"STOPPED PART-WAY{(recordFailure is null ? string.Empty : $" ({recordFailure.GetType().Name})")}: "
                            + $"the Auth account {id:D} was written (an inactive invitation), the Platform record was not. "
                            + "The link does not work without the record. Remove that Auth account, delete the link file, "
                            + "and run the command again.");
            return PartWay;
        }

        output.WriteLine($"CREATED — platform administrator {id:D} (Auth account and Platform record, one id).");
        output.WriteLine($"The one-time set-password link was written to {linkPath} (readable by this user only); it is "
                         + $"valid until {expiresAtUtc:yyyy-MM-dd HH:mm} UTC and works once.");
        output.WriteLine("Hand it to the administrator over a trusted channel, then delete the file. No password was made.");
        return Created;
    }

    private static (string FirstName, string LastName) SplitName(string displayName)
    {
        var parts = displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => ("Platform", "Admin"),
            1 => (parts[0], "Admin"),
            _ => (parts[0], string.Join(' ', parts.Skip(1)))
        };
    }

    public sealed record Arguments(string Email, string FrontendBaseUrl, string LinkFile, string UserName, string DisplayName);

    internal static bool TryParse(string[] args, out Arguments options, out string problem)
    {
        options = new Arguments(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        string[] known = ["--email", "--frontend-base-url", "--link-file", "--user-name", "--display-name"];
        for (var i = 0; i < args.Length; i++)
        {
            if (!known.Contains(args[i]) || values.ContainsKey(args[i]) || i + 1 >= args.Length || string.IsNullOrWhiteSpace(args[i + 1]))
            {
                problem = $"unexpected or incomplete argument: {args[i]}";
                return false;
            }

            values[args[i]] = args[++i].Trim();
        }

        if (!values.TryGetValue("--email", out var email) || !values.TryGetValue("--frontend-base-url", out var root)
            || !values.TryGetValue("--link-file", out var linkFile))
        {
            problem = "--email, --frontend-base-url and --link-file are required.";
            return false;
        }

        email = email.ToLowerInvariant();
        var at = email.IndexOf('@');
        if (at <= 0 || at != email.LastIndexOf('@') || at == email.Length - 1 || email.Any(char.IsWhiteSpace))
        {
            problem = "--email is not an address.";
            return false;
        }

        // The link points at the console the administrator opens: https, or http only on this machine (a local install).
        if (!Uri.TryCreate(root, UriKind.Absolute, out var uri)
            || !(uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback))
            || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
        {
            problem = "--frontend-base-url must be an https URL (http only for localhost) with no user info, query or fragment.";
            return false;
        }

        var userName = values.TryGetValue("--user-name", out var name) ? name : email[..at];
        var displayName = values.TryGetValue("--display-name", out var display) ? display : "Platform Administrator";
        options = new Arguments(email, root.TrimEnd('/'), linkFile, userName, displayName);
        problem = string.Empty;
        return true;
    }
}
