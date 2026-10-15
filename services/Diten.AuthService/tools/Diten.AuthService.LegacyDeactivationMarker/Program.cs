using Diten.AuthService.LegacyDeactivationMarker;

// BL-529 FIX8/FIX9 — marks the accounts an administrator deactivated before BL-529 and ends their links.
// See MarkerCommand for the arguments, the environment and the exit codes; post-deploy-steps.md §3 for when to run it.
try
{
    return await MarkerCommand.RunAsync(args, Environment.GetEnvironmentVariable, Console.Out, Console.Error);
}
catch (Exception failure)
{
    // FIX10 (K1) — the outermost net: whatever escapes is named by its TYPE only. A message can carry the connection
    // string (the driver quotes it), so no message, no stack trace, ever reaches the console from this tool.
    Console.Error.WriteLine($"unexpected failure ({failure.GetType().Name}).");
    return 1;
}
