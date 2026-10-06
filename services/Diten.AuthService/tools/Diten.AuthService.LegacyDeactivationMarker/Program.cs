using Diten.AuthService.Persistence.Operations;

// BL-529 FIX8 item 1 (a) — marks the accounts an administrator deactivated before BL-529 and ends their links.
//   DRY RUN (default):  lists and counts, writes nothing.
//   --apply:            writes (targeted, conditional, idempotent).
// The connection comes from the environment, NEVER the command line (a connection string carries a secret):
//   DITEN_AUTH_MONGO_CONNECTION, DITEN_AUTH_MONGO_DATABASE.
// Running it against shared or live data is the owner's step.

var apply = args.Length == 1 && args[0] == "--apply";
if (args.Length > 1 || (args.Length == 1 && !apply))
{
    Console.Error.WriteLine("usage: Diten.AuthService.LegacyDeactivationMarker [--apply]");
    return 2;
}

var connection = Environment.GetEnvironmentVariable("DITEN_AUTH_MONGO_CONNECTION");
var databaseName = Environment.GetEnvironmentVariable("DITEN_AUTH_MONGO_DATABASE");
if (string.IsNullOrWhiteSpace(connection) || string.IsNullOrWhiteSpace(databaseName))
{
    Console.Error.WriteLine("DITEN_AUTH_MONGO_CONNECTION and DITEN_AUTH_MONGO_DATABASE must be set.");
    return 2;
}

var result = await LegacyDeactivationMarker.RunAsync(connection, databaseName, apply);
Console.WriteLine(result.DryRun ? "DRY RUN — nothing written." : $"APPLIED — {result.Marked} account(s) marked, their links ended.");
Console.WriteLine($"Deactivated before BL-529, not a pending invitation, not marked: {result.Found.Count}");
foreach (var account in result.Found)
{
    Console.WriteLine($"  tenant {account.TenantId:D}  user {account.UserId:D}");
}

return 0;
