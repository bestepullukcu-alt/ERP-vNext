using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.Platform.Application.Tests.Persistence;

/// <summary>What the sweeper decided about one database, and why.</summary>
public sealed record SweepDecision(bool Drop, string Reason)
{
    public static SweepDecision Keep(string reason) => new(false, reason);
    public static readonly SweepDecision Sweep = new(true, "owned prefix, harness marker, stale, not this run");
}

/// <summary>The marker the harness stamps into every database it opens.</summary>
public sealed record HarnessMarker(string Harness, Guid RunId, DateTime TouchedAtUtc);

/*
 * ⚠ THIS IS A DELETE PATH THAT RUNS INSIDE THE TEST SUITE. Read that sentence again before changing anything
 * here. It drops MongoDB databases on a developer's machine, unattended, at the start of a run.
 *
 * WHY IT EXISTS. The harness drops its own database when it is done, but "when it is done" never arrives if
 * mongod dies mid-run — which is the exact failure this whole body of work is about. Measured on 2026-08-26:
 * 19 databases on this machine, 6 of them test residue, 3 of those named under a scheme the harness stopped
 * using two stages ago. Nobody would ever have removed them by hand.
 *
 * WHY IT IS NARROW, DELIBERATELY. This session measured how weak string-matching guards are. So a name match
 * is NOT sufficient here — it is only the first of four conditions, and the load-bearing one is a MARKER the
 * harness itself wrote. A database that this harness did not create carries no marker and is therefore
 * untouchable, whatever it is called. Production databases, a colleague's scratch database, `admin`,
 * `config`, `local`: none of them can be reached by this code, because none of them was stamped by it.
 *
 * ⚠ THE PREFIX IS NOT A PARAMETER. Callers cannot widen it. A sweeper that accepts "which prefix should I
 * delete?" from its caller is one typo away from dropping the development database, and the typo would live
 * in a test file nobody reviews as carefully as production code.
 */
public static class MongoResidueSweeper
{
    /// <summary>The only prefix this sweeper can ever act on. Not a parameter, and not configurable.</summary>
    public const string OwnedPrefix = "diten_platform_itest";

    /// <summary>The collection the marker lives in. Two leading underscores so it cannot collide with a real one.</summary>
    public const string MarkerCollection = "__diten_harness_marker";

    public const string MarkerId = "harness";

    /// <summary>The value the marker must carry. A marker written by anything else is not ours.</summary>
    public const string MarkerHarness = nameof(MongoIntegrationHarness);

    /*
     * A database is only residue once nothing has touched it for this long. The harness re-stamps the marker
     * every time it OPENS a database, so anything the current run uses stays fresh no matter how long the
     * run takes. The window therefore only has to outlast a concurrent suite started by someone else on the
     * same machine — an hour is generous for that and cheap to be wrong about in the safe direction.
     */
    public static readonly TimeSpan ResidueMaxAge = TimeSpan.FromHours(1);

    /*
     * The owned prefix as a WHOLE SEGMENT, optionally followed by `_`-separated lowercase tokens.
     * `diten_platform_itest`            → matches (the shared database)
     * `diten_platform_itest_task_comment_order` → matches (a scoped database)
     * `diten_platform_itest_9f2c…`      → matches (residue from the old Guid-named scheme)
     * `diten_platform_itestX`           → does NOT match: the prefix is not a whole segment
     * `x_diten_platform_itest`          → does NOT match: it must be the start
     * `diten_platform_itest_Task`       → does NOT match: uppercase is not this grammar
     */
    private static readonly System.Text.RegularExpressions.Regex OwnedName =
        new($"^{OwnedPrefix}(_[a-z0-9]+)*$", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// The whole decision, as a pure function, so every rule below can be proved without a live server.
    /// ALL FOUR conditions must hold. Any one of them missing means keep.
    /// </summary>
    public static SweepDecision Decide(
        string databaseName,
        HarnessMarker? marker,
        Guid currentRunId,
        DateTime utcNow)
    {
        // 1. The name must be one this harness could have produced.
        if (!OwnedName.IsMatch(databaseName))
        {
            return SweepDecision.Keep("name is outside the owned prefix grammar");
        }

        // 2. It must carry OUR marker. This is what makes the name match non-load-bearing: a database that
        //    merely looks like ours, but was not stamped by us, is never touched.
        if (marker is null)
        {
            return SweepDecision.Keep("no harness marker — this database was not created by us");
        }

        if (!string.Equals(marker.Harness, MarkerHarness, StringComparison.Ordinal))
        {
            return SweepDecision.Keep($"marker belongs to '{marker.Harness}', not us");
        }

        if (marker.RunId == Guid.Empty)
        {
            return SweepDecision.Keep("marker carries no run id");
        }

        // 3. Never the run that is happening right now.
        if (marker.RunId == currentRunId)
        {
            return SweepDecision.Keep("this run owns it");
        }

        // 4. Never something still in use. The marker is re-stamped on every open, so a live suite's
        //    databases are always inside the window.
        if (utcNow - marker.TouchedAtUtc < ResidueMaxAge)
        {
            return SweepDecision.Keep($"touched {utcNow - marker.TouchedAtUtc:g} ago — still active");
        }

        return SweepDecision.Sweep;
    }

    /// <summary>
    /// Applies <see cref="Decide"/> across the server and drops what it selects.
    ///
    /// ⚠ IT NEVER THROWS INTO THE TEST. A cleanup failure — a dropped connection, a permissions problem, a
    /// race with another run — must not surface as the failure of whatever test happened to start first.
    /// That is how a real defect gets attributed to housekeeping and dismissed. Problems are RETURNED, and
    /// the caller reports them separately.
    /// </summary>
    public static async Task<SweepReport> SweepAsync(
        IMongoClient client,
        Guid currentRunId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var dropped = new List<string>();
        var problems = new List<string>();

        List<string> names;
        try
        {
            names = await (await client.ListDatabaseNamesAsync(cancellationToken)).ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            return new SweepReport(dropped, new[] { $"could not list databases: {ex.Message}" });
        }

        foreach (var name in names)
        {
            // Cheap check first: never even READ from a database outside the grammar.
            if (!OwnedName.IsMatch(name))
            {
                continue;
            }

            try
            {
                var marker = await ReadMarkerAsync(client.GetDatabase(name), cancellationToken);
                if (!Decide(name, marker, currentRunId, utcNow).Drop)
                {
                    continue;
                }

                await client.DropDatabaseAsync(name, cancellationToken);
                dropped.Add(name);
            }
            catch (OperationCanceledException)
            {
                problems.Add($"{name}: sweep cancelled");
                break;
            }
            catch (Exception ex)
            {
                problems.Add($"{name}: {ex.Message}");
            }
        }

        return new SweepReport(dropped, problems);
    }

    /// <summary>Stamps (or re-stamps) the marker. Called every time the harness opens a database.</summary>
    public static Task TouchAsync(
        IMongoDatabase database,
        Guid runId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
        => database.GetCollection<BsonDocument>(MarkerCollection).ReplaceOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", MarkerId),
            new BsonDocument
            {
                { "_id", MarkerId },
                { "harness", MarkerHarness },
                { "runId", runId.ToString() },
                { "touchedAtUtc", utcNow }
            },
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);

    private static async Task<HarnessMarker?> ReadMarkerAsync(
        IMongoDatabase database,
        CancellationToken cancellationToken)
    {
        var document = await database.GetCollection<BsonDocument>(MarkerCollection)
            .Find(Builders<BsonDocument>.Filter.Eq("_id", MarkerId))
            .FirstOrDefaultAsync(cancellationToken);

        if (document is null)
        {
            return null;
        }

        var harness = document.GetValue("harness", BsonNull.Value);
        var runId = document.GetValue("runId", BsonNull.Value);
        var touched = document.GetValue("touchedAtUtc", BsonNull.Value);

        if (!harness.IsString || !runId.IsString || !touched.IsValidDateTime)
        {
            // A marker we cannot read is a marker we do not trust, and an untrusted marker means keep.
            return null;
        }

        return Guid.TryParse(runId.AsString, out var parsed)
            ? new HarnessMarker(harness.AsString, parsed, touched.ToUniversalTime())
            : null;
    }
}

/// <summary>What one sweep did, and what went wrong while doing it.</summary>
public sealed record SweepReport(IReadOnlyList<string> Dropped, IReadOnlyList<string> Problems);
