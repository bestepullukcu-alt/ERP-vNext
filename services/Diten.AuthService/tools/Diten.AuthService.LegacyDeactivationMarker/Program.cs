using Diten.AuthService.LegacyDeactivationMarker;

// BL-529 FIX8/FIX9 — marks the accounts an administrator deactivated before BL-529 and ends their links.
// See MarkerCommand for the arguments, the environment and the exit codes; post-deploy-steps.md §3 for when to run it.
return await MarkerCommand.RunAsync(args, Environment.GetEnvironmentVariable, Console.Out, Console.Error);
