using Marker = Diten.AuthService.Persistence.Operations.LegacyDeactivationMarker;
using MongoDB.Driver;

namespace Diten.AuthService.LegacyDeactivationMarker;

/// <summary>
/// BL-529 FIX8/FIX9 — the tool's whole behaviour, apart from the process (testable with writers and an environment).
/// <list type="bullet">
/// <item><c>(no args)</c> — DRY RUN: lists and counts, writes nothing but its audit row.</item>
/// <item><c>--apply</c> — marks (targeted, conditional, idempotent).</item>
/// <item><c>--expect N</c> — with or without <c>--apply</c>: nothing is written unless exactly N accounts are found.</item>
/// </list>
/// The connection comes from the environment (<see cref="ConnectionVariable"/>, <see cref="DatabaseVariable"/>), NEVER the
/// command line, and is NEVER written to any output: a failure names only the exception's type.
/// <para>Exit codes: 0 done · 1 connection or unexpected failure (nothing reached the database; no audit row) · 2 usage (no
/// audit row) · 3 stopped part-way (what was written and what was not reached are listed; the audit row is still written)
/// · 4 fewer marked than found (accounts changed meanwhile; listed) · 5 the count was not the expected one (nothing
/// written; audit row "refused") · 6 the work is done but its audit row could not be written.</para>
/// <para>FIX10 (K2) — 6 is its own code: "done, but not on record" is neither a connection failure nor a success, and an
/// operator must not mistake it for either.</para>
/// </summary>
public static class MarkerCommand
{
    public const string ConnectionVariable = "DITEN_AUTH_MONGO_CONNECTION";
    public const string DatabaseVariable = "DITEN_AUTH_MONGO_DATABASE";

    /// <param name="afterFind">TEST SEAM — runs between the list and the mark (a change landing meanwhile).</param>
    public static async Task<int> RunAsync(
        string[] args, Func<string, string?> environment, TextWriter output, TextWriter error, Func<Task>? afterFind = null)
    {
        if (!TryParse(args, out var apply, out var expect))
        {
            error.WriteLine("usage: Diten.AuthService.LegacyDeactivationMarker [--apply] [--expect N]");
            return 2;
        }

        var connection = environment(ConnectionVariable);
        var databaseName = environment(DatabaseVariable);
        if (string.IsNullOrWhiteSpace(connection) || string.IsNullOrWhiteSpace(databaseName))
        {
            error.WriteLine($"{ConnectionVariable} and {DatabaseVariable} must be set.");
            return 2;
        }

        IMongoDatabase database;
        Marker.Findings findings;
        try
        {
            database = Marker.OpenDatabase(connection, databaseName);
            findings = await Marker.FindAsync(database);
        }
        catch (Exception failure)
        {
            // Never the message: a driver exception can carry the connection string.
            error.WriteLine($"connection error ({failure.GetType().Name}); nothing was written.");
            return 1;
        }

        output.WriteLine($"Database: {databaseName}");
        output.WriteLine($"Deactivated before BL-529 (inactive, unmarked, not a pending invitation): {findings.Found.Count}");
        foreach (var account in findings.Found)
        {
            output.WriteLine($"  tenant {account.TenantId:D}  user {account.UserId:D}");
        }

        output.WriteLine($"Skipped as pending invitations (left alone): {findings.SkippedPending.Count}");
        foreach (var (account, expires) in findings.SkippedPending)
        {
            output.WriteLine($"  tenant {account.TenantId:D}  user {account.UserId:D}  link valid until {(expires is { } at ? at.ToString("u") : "-")}");
        }

        if (expect is { } expected && expected != findings.Found.Count)
        {
            error.WriteLine($"Expected {expected} account(s), found {findings.Found.Count}: nothing was written.");
            await RecordAsync(database, Marker.ModeRefused, findings, [], [], [], error);
            return 5;
        }

        if (!apply)
        {
            output.WriteLine("DRY RUN — nothing written. Repeat until it reports 0, then run with --apply.");
            return await RecordAsync(database, Marker.ModeDryRun, findings, [], [], [], error) ? 0 : 6;
        }

        if (afterFind is not null)
        {
            await afterFind();
        }

        Marker.MarkResult result;
        try
        {
            result = await Marker.MarkAsync(database, findings.Found);
        }
        catch (Marker.PartialMarkException partial)
        {
            error.WriteLine($"STOPPED PART-WAY ({partial.InnerException?.GetType().Name}): {partial.Marked.Count} marked, {partial.Remaining.Count} not reached.");
            foreach (var account in partial.Marked)
            {
                output.WriteLine($"  marked       tenant {account.TenantId:D}  user {account.UserId:D}");
            }

            foreach (var account in partial.Remaining)
            {
                output.WriteLine($"  not reached  tenant {account.TenantId:D}  user {account.UserId:D}");
            }

            // FIX10 item 3 — the part-way run is on record too; if even that fails, the exit stays 3 (it says the most).
            await RecordAsync(database, Marker.ModeApply, findings, partial.Marked, [], partial.Remaining, error);
            return 3;
        }

        // FIX10 (K3) — the warning comes BEFORE the summary line, so "APPLIED" is never the last word of a partial result.
        if (result.NotMarked.Count > 0)
        {
            error.WriteLine($"WARNING: {result.NotMarked.Count} account(s) changed after the list was read and were left as they are:");
            foreach (var account in result.NotMarked)
            {
                error.WriteLine($"  tenant {account.TenantId:D}  user {account.UserId:D}");
            }
        }

        output.WriteLine($"APPLIED — {result.Marked.Count} of {findings.Found.Count} marked, their links ended.");
        var recorded = await RecordAsync(database, Marker.ModeApply, findings, result.Marked, result.NotMarked, [], error);
        return result.NotMarked.Count > 0 ? 4 : recorded ? 0 : 6;
    }

    private static async Task<bool> RecordAsync(
        IMongoDatabase database, string mode, Marker.Findings findings,
        IReadOnlyList<Marker.Account> marked, IReadOnlyList<Marker.Account> notMarked,
        IReadOnlyList<Marker.Account> notReached, TextWriter error)
    {
        try
        {
            await Marker.RecordRunAsync(database, mode, findings, marked, notMarked, notReached);
            return true;
        }
        catch (Exception failure)
        {
            error.WriteLine($"the audit row could not be written ({failure.GetType().Name}).");
            return false;
        }
    }

    private static bool TryParse(string[] args, out bool apply, out int? expect)
    {
        apply = false;
        expect = null;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--apply" when !apply:
                    apply = true;
                    break;
                case "--expect" when expect is null && i + 1 < args.Length && int.TryParse(args[i + 1], out var n) && n >= 0:
                    expect = n;
                    i++;
                    break;
                default:
                    return false;
            }
        }

        return true;
    }
}
