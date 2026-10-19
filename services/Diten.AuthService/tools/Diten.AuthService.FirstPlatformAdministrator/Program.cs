using Diten.AuthService.FirstPlatformAdministrator;

// WP-AUTH-ADMIN-BOOTSTRAP-01 — a new environment's first platform administrator. See FirstPlatformAdministratorCommand for
// the arguments, the environment and the exit codes.
try
{
    return await FirstPlatformAdministratorCommand.RunAsync(args, Environment.GetEnvironmentVariable, Console.Out, Console.Error);
}
catch (Exception failure)
{
    // The outermost net: whatever escapes is named by its TYPE only — a message can carry a connection string (the driver
    // quotes it) or a value read from a document.
    Console.Error.WriteLine($"unexpected failure ({failure.GetType().Name}).");
    return 1;
}
