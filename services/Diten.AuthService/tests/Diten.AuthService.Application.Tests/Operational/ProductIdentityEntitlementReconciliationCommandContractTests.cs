using Diten.AuthService.Api.Operational;
using Diten.AuthService.Application.Common.Entitlements;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Diten.AuthService.Application.Common.Services;
using Diten.AuthService.Application.Tests.Roles;

namespace Diten.AuthService.Application.Tests.Operational;

[CollectionDefinition("Entitlement process environment", DisableParallelization = true)]
public sealed class EntitlementProcessEnvironmentCollection { }

[Collection("Entitlement process environment")]
public sealed class ProductIdentityEntitlementReconciliationCommandContractTests
{
    // FU18/FU19 closed source inventory, including the 2026-09-30 factual correction.
    // 2026-10-01 (WP-MDM-TAKEOVER-STABILIZE-01) — rows re-measured after the merge onto main; every changed row below
    // is a writer that already exists on main (c5d8f8e9f), attributed call by call:
    //   UpdateUserCommandHandler   UpdateAsync -> UpdateForTenantAsync, + RevokeAllByUserAsync   (users module, main)
    //   SetAccountKindCommandHandler UpdateForTenantAsync                                        (account kind, 7e22cb103)
    //   DataSeeder  BulkWriteAsync 3->6, rpCol.InsertOneAsync 11->17, IncrementAsync 3->4, + col.UpdateOneAsync
    //               (catalog/export-grant/scope seeds added on main)
    // The AddPersistence fingerprint pinned in Bootstrap_WhenOperationalPathExists_… moved in the same round
    // (F318BCF7… -> 447B2703…), for three changes that are all main's: GuidSerializerRegistration.EnsureStandard(),
    // the IUserListReader registration, and the mock-user seed becoming opt-in. Nothing in it is from this branch.
    // The DataSeeder `col.UpdateOneAsync` row is BL-411's scope-correction write (TenantRouteScopeCorrections).
    // The two rows that were left undecided in that round are decided (owner, 2026-10-01; WP-AUTH-TAKEOVER-DECISIONS-01):
    //   the Development-only local tenant-admin reset endpoint is REMOVED (the ordinary "Reset password" action does
    //     the same job), so its writer row no longer exists — nothing was added for it;
    //   DataSeeder|marks.ReplaceOneAsync is classified as a BOOKKEEPING write — see ApprovedBookkeepingWriterCalls.
    //     The scanner now binds a typed collection opened with the type's OWN CollectionName constant
    //     (GetCollection<T>(T.CollectionName)); any other non-literal collection name stays UNCLASSIFIED and fails closed.
    // 2026-10-09 (WP-CT-LANE-TKO-MERGE-01; CT kararı, koşullu onay) — rows re-measured after merging the takeover onto the CT
    // lane (79c36baec ← 07bcdbd8b). This inventory came from the takeover only; the lane's Auth work was outside it. 21 rows
    // ADDED, 28 REMOVED, and for EVERY one the merged call count equals the lane's (79c36baec) — each is the lane's accepted
    // work, attributed call by call (base 438343540 → lane):
    //   BL-529 admin reset invalidates (7c7668acd fix 1, 67ed2c4b9 fix 2, 7935b8143 fix 3, 1fb7df344 fix 4, 7d5cab37a FIX8):
    //     SESSION REVOKES — only ever NARROW access: RevokeLiveSessionsAsync (PlatformAuthController ×2, ChangePassword,
    //       ForcedChangeTenantPassword, SetTenantPassword, AdminPasswordReset), IssuedSessionGuard.RevokeIfLiveAsync,
    //       RefreshTokenRepository UpdateManyAsync 1→2 / UpdateOneAsync 1→3; replacing RevokeAllByUserAsync
    //       (PlatformAuthController 4→1, ChangePassword, ForcedChangeTenantPassword, SetTenantPassword) and
    //       RefreshTokenCommandHandler UpdateAsync.
    //     USER WRITES NARROWED TO THEIR OWN FIELDS — UserRepository UpdateOneAsync 1→5 and FindOneAndUpdateAsync (conditional,
    //       field-targeted writes) replacing whole-document UpdateAsync / UpdateForTenantAsync / ReplaceOneAsync in
    //       PlatformAuthController (5), InternalEventsController, ChangePassword, ForcedChangeTenantPassword, Login (2),
    //       PlatformLogin, VerifyMfa, AdminResetPassword, ResendUserInvitation, SetAccountKind, SetTenantPassword,
    //       SetUserActiveStatus, UpdateUser and DataSeeder userCol.ReplaceOneAsync.
    //     PLATFORM RESET TOKEN — PlatformAuthController _userRepository.SetPasswordResetTokenAsync (67ed2c4b9).
    //     OPERATOR MARKER — LegacyDeactivationMarker users.UpdateOneAsync (7d5cab37a; the post-deploy marker tool).
    //   WP-ROLES-CLOSE-01 (00d3e39b9, 089da345b) — the export backfill moved out of DataSeeder into ExportGrantBackfillRunner,
    //     one rule for every "{module}.export" key: runner versions.IncrementAsync, rpCol.UpdateOneAsync (grant source
    //     correction), the generic InsertOnceAsync<T> (T|collection.InsertOneAsync: grants, audit rows and marks), and
    //     markCol.UpdateOneAsync (BOOKKEEPING, below); DataSeeder versionService.IncrementAsync 4→3, rpCol.InsertOneAsync
    //     17→16. The takeover's own +1 / +1 / +1 (BackfillBrandsExportAsync: IncrementAsync, rpCol.InsertOneAsync,
    //     marks.ReplaceOneAsync) are gone with it: the brands key is ExportGrantBackfill.Brands of that one runner.
    //   a357507e4 (Platform, audit round) — RegisterModuleManifestCommandHandler UpsertModuleCatalogAsync 2→3 and
    //     TenantSubscriptionTransactionWriter _tenants.UpdateAsync 2→1.
    // The AddPersistence fingerprint (Bootstrap_…, 447B2703…) did NOT change in this merge.
    // 2026-10-09 (WP-CT-LANE-BL521-MERGE-01; CT kararı A) — rows re-measured after merging BL-521 (6dea5a0c4) onto the lane
    // (bc1c92e41). BL-521 never ran this inventory (the test is the lane's), so its writers arrive here; 16 ADDED, 5 REMOVED,
    // each counted at base ef28b2f7c / lane / BL-521 / merged and attributed by `git log -S` over ef28b2f7c..6dea5a0c4:
    //   AUTHORITY, role convergence moved into its own file — BL-521 FIX4 796c5a592 removed PlatformAuthController's
    //     SyncPlatformRolesAsync (_roleRepository.UpsertSystemRoleAsync, _userRoleRepository.AssignAsync / RevokeAsync: REMOVED);
    //     FIX5 7130dc5c5 (item 12) PlatformAdministratorRoleConvergence roles.UpsertSystemRoleAsync, userRoles.AssignAsync /
    //     RevokeAsync (ADDED) — Auth's platform roles converge to the Platform RECORD, never the reverse;
    //     UserRoleRepository Collection.DeleteOneAsync -> DeleteManyAsync (7130dc5c5, FIX5 item 4(c): a revoke removes EVERY
    //     matching assignment row — a duplicate left behind kept the role alive) and + UpdateOneAsync (7130dc5c5: AssignAsync
    //     is an upsert with $setOnInsert, so a concurrent grant of the same user + role + tenant cannot write a duplicate).
    //   AUTHORITY MARKER — PlatformAccountDiscriminator users.SetPlatformActorTypeAsync and UserRepository Collection.UpdateOneAsync
    //     5 -> 6 (14c96c16f, FIX7 item 1: the record's actor type written onto the account at a platform mint; targeted $set,
    //     fail-closed). 5 is the lane's (BL-529), +1 is BL-521's.
    //   DECORATOR FORWARDS — PlatformAccountScopedUserRepository _inner.CreateAsync / SoftDeleteAsync (14ce67b04, FIX6 item 13),
    //     _inner.SetPlatformActorTypeAsync (14c96c16f) and _inner.SetPasswordResetTokenAsync (THIS MERGE: BL-529's link write
    //     through BL-521's decorator, guarded for a hidden caller — CT condition 1). Counted only since the scanner binds the
    //     concrete UserRepository (gate change, see AuthorityCalls; CT to decide).
    //   PLATFORM GUARDED WRITES — Assign/Suspend/Update _repository.UpdateGuardedAsync, Delete/BulkDelete
    //     _repository.SoftDeleteGuardedAsync (f45c31af7, FIX3: the last active platform super admin protected by a transaction plus
    //     a shared guard document). The handlers' earlier UpdateAsync / SoftDeleteAsync rows are unchanged.
    //   Not a row: PlatformAuthController RevokeAllByUserAsync stays at the lane's 1 — the merge first brought back the old
    //     shared tail's sweep after a NEW account's creation (BL-529 7935b8143 had taken it out; such an account has no
    //     session); it is removed again.
    // 2026-10-09 (WP-CT-LANE-BL521-MERGE-01; CT kararı A, koşul 3) — the AddPersistence fingerprint moved 447B2703… -> 51D35FF8…
    // for ONE change, BL-521's: FIX6 item 13 (D') 14ce67b04 registers the Mongo UserRepository as itself and IUserRepository as
    // PlatformAccountScopedUserRepository over it (a tenant actor's view loses the platform accounts). Measured: the merged
    // Persistence/DependencyInjection.cs differs from the lane (bc1c92e41) in that one hunk only; nothing else in AddPersistence.
    // 2026-10-10 (WP-CT-LANE-BL521-MERGE-01 FIX1, small 3) — the scanner's verbs gained Try… / Record… / Reset… and it binds the
    // receivers they reach (I…Recorder: audit only, NON-AUTHORITY above; I…Client; AccountKindWriter; AdminPasswordReset by
    // name; a namespace-qualified `new …Repository(`). 38 rows became VISIBLE, 0 removed. All but one are calls that were
    // already there (count at HEAD a59560dc4 = count now), attributed to where each first appeared (`git log -S`):
    //   BL-529 targeted writes — TryWriteChangesAsync (7935b8143: PlatformAuthController ×3, ChangePassword, ForcedChangeTenant,
    //     ResendUserInvitation, SetAccountKind, SetTenantPassword, SetUserActiveStatus, UpdateUser, AdminPasswordReset users.),
    //     RecordLoginOutcomeAsync (7c7668acd: Login, PlatformLogin, VerifyMfa), RecordLoginFailureAsync (67ed2c4b9: Login),
    //     refresh TryRotateAsync (67ed2c4b9), DataSeeder userRepository.TryWriteChangesAsync (7d5cab37a, BL-529 FIX8);
    //     AdminPasswordReset.ResetAsync (7c7668acd: InternalEvents, PlatformAuth ×2, ResendUserInvitation; 0fc13fbff:
    //     AdminResetPassword) — the ONE reset path.
    //   BL-521 decorator forwards (a59560dc4, this merge) — _inner.TryWriteChangesAsync / RecordLoginOutcomeAsync /
    //     RecordLoginFailureAsync (each guarded for a hidden caller, on the stored row since FIX1 small 2).
    //   WP-EMAIL-LANG-01 (2026-10-13, CT lane merge, owner correction by hand) — UpdateMyPreferencesCommandHandler
    //     users.TryWriteChangesAsync: the signed-in user's own preferred e-mail language (tenant_user only, one field,
    //     audited); written on its own branch, it met this inventory only when both reached the lane. Never grants.
    //   WP-USERS-ROLES-CLOSE-FIX1 FIX4 item 3 (2026-10-13, owner correction by hand, not a regeneration) — the sign-in CLAIM:
    //     LoginCommandHandler _userRepository.TryClaimSignInAsync, its decorator forward _inner.TryClaimSignInAsync, and the one
    //     targeted write behind it (UserRepository Collection.UpdateOneAsync 6 → 7). It clears the failure count and the lock
    //     ONLY where the stored row is not locked and below the threshold — a stricter form of the reset RecordLoginOutcomeAsync
    //     already made; it never grants, never widens a session or a role.
    //   WP-USERS-ROLES-CLOSE-FIX1 FIX5 (2026-10-13, owner correction by hand) — the failure count and the lockout became ONE
    //     atomic write (the existing Collection.FindOneAndUpdateAsync, now a pipeline update that sets the lock and starts the
    //     count over at the threshold); the separate lockout UpdateOneAsync is gone: UserRepository UpdateOneAsync 7 → 6.
    //   THIS ROUND (FIX1 small 6) — PlatformAuthController _refreshTokenRepository.RevokeIfLiveAsync: the platform forced change
    //     revokes the token it just issued when the account was deactivated meanwhile (only ever NARROWS access).
    //   Older, now visible — Users quota _quota.TryConsumeUserSeatAsync (f0ac41e5a: CreateUser, SetUserActiveStatus,
    //     UpdateUser); account kind _kindWriter.RecordAsync (7e22cb103 SetAccountKind, 88ab4137b UpdateUser); service-client
    //     operational TryReserve / TryComplete / TryMarkRecoveryRequired (e36511825); inbox _inboxRepository.TryInsertAsync
    //     (8c3498bef); Platform quota TryConsumeEntitlementAsync (d3098d729 ×2) / TryConsumeAsync (1cca77a8f), audit outbox
    //     TryEnqueueAsync (d3098d729, 261f99105), tenant registry RecordAdminInvitationAsync (e3d3a7331).
    // This is a conservative lexical regression guard, not C# compiler/semantic proof or a writer fence.
    // It deliberately has no git-history dependency: shallow checkouts compare against frozen call rows.
    [Fact]
    public void ClosedWriterInventory_WhenAuthorityMutationCallChanges_RequiresPackCorrection()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(root.FullName, "AGENTS.md"))) root = root.Parent ?? throw new InvalidOperationException("Checkout required");
        var actual = WriterRoots.SelectMany(relative => Directory.EnumerateFiles(Path.Combine(root.FullName, relative), "*.cs", SearchOption.AllDirectories))
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(part => part is "obj" or "bin"))
            .SelectMany(path => AuthorityCalls(Path.GetRelativePath(root.FullName, path).Replace('\\', '/'), File.ReadAllText(path)))
            .GroupBy(row => row, StringComparer.Ordinal).Select(group => group.Key + "|" + group.Count())
            .Order(StringComparer.Ordinal).ToArray();
        var expected = (FrozenWriterCalls + "\n" + OwnerCorrection20261014WriterCalls + "\n" + OwnerCorrection20261014B1WriterCalls + "\n" + ApprovedOperationalWriterCalls + "\n" + ApprovedBookkeepingWriterCalls
            + "\n" + ApprovedDeclaredServiceClientWriterCalls).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Order(StringComparer.Ordinal).ToArray();
        var unclassified = actual.Where(row => row.Contains("|UNCLASSIFIED|", StringComparison.Ordinal)).ToArray();
        Assert.True(unclassified.Length == 0, "UNCLASSIFIED writer calls (bind their receiver or fix the call):\n" + string.Join('\n', unclassified));
        Assert.True(expected.SequenceEqual(actual, StringComparer.Ordinal),
            "Closed inventory changed; do not regenerate without owner correction.\nADDED:\n" + string.Join('\n', actual.Except(expected, StringComparer.Ordinal)) +
            "\nREMOVED:\n" + string.Join('\n', expected.Except(actual, StringComparer.Ordinal)));
    }

    [Theory]
    [InlineData("IUserRepository users; await users.UpdateAsync(user, ct);", "IUserRepository|users.UpdateAsync")]
    [InlineData("IRefreshTokenRepository tokens; await tokens.UpdateAsync(token, ct);", "IRefreshTokenRepository|tokens.UpdateAsync")]
    [InlineData("var rows = database.GetCollection<RolePermission>(\"rolePermissions\"); await rows.InsertOneAsync(row);", "RolePermission|rows.InsertOneAsync")]
    [InlineData("await Raw(\"roles\").DeleteOneAsync(filter);", "roles|Raw.DeleteOneAsync")]
    [InlineData("var rows = database.GetCollection<BsonDocument>(\"users\"); await rows.ReplaceOneAsync(filter, user);", "users|rows.ReplaceOneAsync")]
    [InlineData("await mystery.SaveAsync(value);", "UNCLASSIFIED|mystery.SaveAsync")]
    [InlineData("IMongoCollection<User> users; users.ReplaceOne(filter, user);", "User|users.ReplaceOne")]
    [InlineData("database.RunCommand<BsonDocument>(writeCommand);", "UNCLASSIFIED|database.RunCommand[unclassified]")]
    [InlineData("database.RunCommand<BsonDocument>(new BsonDocument(\"drop\", \"users\"));", "UNCLASSIFIED|database.RunCommand[drop:users]")]
    [InlineData("await database.GetCollection<User>(\"users\").InsertOneAsync(user);", "User|inlineCollection0.InsertOneAsync")]
    [InlineData("await InsertExistingAsync(\"rolePermissions\", rows, session, ct);", "rolePermissions|this.InsertExistingAsync")]
    [InlineData("var marks = database.GetCollection<PermissionReconciliationMark>(PermissionReconciliationMark.CollectionName); await marks.ReplaceOneAsync(filter, mark);", "PermissionReconciliationMark|marks.ReplaceOneAsync")]
    [InlineData("var rows = database.GetCollection<Role>(PermissionReconciliationMark.CollectionName); await rows.ReplaceOneAsync(filter, role);", "UNCLASSIFIED|rows.ReplaceOneAsync")]
    [InlineData("var rows = database.GetCollection<Role>(collectionName); await rows.ReplaceOneAsync(filter, role);", "UNCLASSIFIED|rows.ReplaceOneAsync")]
    [InlineData("var rows = database.GetCollection<BsonDocument>(BsonDocument.CollectionName); await rows.ReplaceOneAsync(filter, row);", "UNCLASSIFIED|rows.ReplaceOneAsync")]
    // Merge WP-CT-LANE-BL521-MERGE-01 (CT gate condition) — ONLY the concrete UserRepository is bound by name (BL-521 FIX6
    // 14ce67b04's decorator field); a writer call through any OTHER concrete, non-interface type stays UNCLASSIFIED and fails
    // closed — including a name that merely contains it.
    [InlineData("private readonly UserRepository _inner; await _inner.CreateAsync(user, ct);", "UserRepository|_inner.CreateAsync")]
    [InlineData("private readonly RoleRepository _roles; await _roles.CreateAsync(role, ct);", "UNCLASSIFIED|_roles.CreateAsync")]
    [InlineData("private readonly RefreshTokenRepository _tokens; await _tokens.RevokeAllByUserAsync(id, tenant, ct);", "UNCLASSIFIED|_tokens.RevokeAllByUserAsync")]
    [InlineData("private readonly PlatformAccountScopedUserRepository _scoped; await _scoped.SoftDeleteAsync(id, tenant, ct);", "UNCLASSIFIED|_scoped.SoftDeleteAsync")]
    // FIX1 (CT survivor M4): concrete types that merely START with "User" are not UserRepository either.
    [InlineData("private readonly UserRoleRepository _userRoles; await _userRoles.AssignAsync(grant, ct);", "UNCLASSIFIED|_userRoles.AssignAsync")]
    [InlineData("private readonly UserSessionRepository _sessions; await _sessions.RevokeAsync(id, ct);", "UNCLASSIFIED|_sessions.RevokeAsync")]
    public void ClosedWriterInventory_WhenSyntheticWriterAdded_DetectsExactCall(string source, string signature)
    {
        Assert.Equal(["new-writer.cs|" + signature], AuthorityCalls("new-writer.cs", source));
    }

    [Fact]
    public void ClosedWriterInventory_WhenExistingWriterGainsSameCall_PreservesMultiplicity()
    {
        const string source = "IUserRepository users; await users.UpdateAsync(user, ct);";
        var original = AuthorityCalls("known.cs", source);
        var modified = AuthorityCalls("known.cs", source + " await users.UpdateAsync(other, ct);");
        Assert.Single(original); Assert.Equal(2, modified.Count); Assert.Equal(original[0], modified[1]);
    }

    [Fact]
    public void ClosedWriterInventory_WhenCredentialFileAlsoWritesUser_DoesNotBypassFile()
    {
        const string source = "IMongoCollection<ServiceClientIdentity> clients; await clients.ReplaceOneAsync(filter, client); IUserRepository users; await users.UpdateAsync(user, ct);";
        Assert.Equal(["CredentialRotation.cs|IUserRepository|users.UpdateAsync"], AuthorityCalls("CredentialRotation.cs", source));
        Assert.Empty(AuthorityCalls("CredentialRotation.cs", "IMongoCollection<ServiceClientIdentity> clients; await clients.ReplaceOneAsync(filter, client);"));
    }

    [Theory]
    [InlineData("serviceClientOperationalProvisioningOperations", "insert:Journal")]
    [InlineData("users", "insert:users")]
    public void ClosedWriterInventory_WhenJournalTargetChanges_TracksExactCollectionAndUserWriter(string collection, string command)
    {
        var source = "private const string Journal = \"" + collection + "\"; IMongoDatabase _database; "
            + "IMongoCollection<ServiceClientOperationalProvisioningOperation> _collection; "
            + "await _collection.UpdateOneAsync(filter, update); "
            + "var command = new BsonDocument { { \"insert\", Journal } }; "
            + "await _database.RunCommandAsync<BsonDocument>(command, cancellationToken: ct); "
            + "IUserRepository users; await users.UpdateAsync(user, ct);";
        Assert.Equal(new[]
        {
            "Journal.cs|ServiceClientOperationalProvisioningOperation|_collection.UpdateOneAsync",
            "Journal.cs|IMongoDatabase|_database.RunCommandAsync[" + command + "]",
            "Journal.cs|IUserRepository|users.UpdateAsync"
        }, AuthorityCalls("Journal.cs", source));
    }

    [Theory]
    [InlineData("IMongoDatabase _database; await _database.RunCommandAsync<BsonDocument>(command, cancellationToken: ct);", "unclassified")]
    [InlineData("IMongoDatabase _database; var command = new BsonDocument { { \"insert\", Journal } }; await _database.RunCommandAsync<BsonDocument>(command, cancellationToken: ct);", "insert:unresolved-Journal")]
    public void ClosedWriterInventory_WhenJournalCommandIsUnresolved_DoesNotMatchApprovedCall(string source, string command)
    {
        Assert.Equal(["Journal.cs|IMongoDatabase|_database.RunCommandAsync[" + command + "]"], AuthorityCalls("Journal.cs", source));
    }

    [Fact]
    public void ClosedWriterInventory_WhenCommentsContainExamples_DoesNotCountThemAsCalls()
    {
        Assert.Empty(AuthorityCalls("comments.cs", "// await users.UpdateAsync(user);\n/* Raw(\"roles\").DeleteOneAsync(x); */"));
    }

    private static List<string> AuthorityCalls(string path, string source)
    {
        // Preserve quoted collection names while removing comments. Raw/interpolated/dynamic/reflection forms
        // are outside semantic inference; unclassified mutation-shaped calls remain visible and fail closed.
        source = Regex.Replace(source, "\"(?:\\\\.|[^\"\\\\])*\"|//[^\\r\\n]*|/\\*[\\s\\S]*?\\*/", match =>
            match.Value.StartsWith('"') ? match.Value : new string(' ', match.Length));
        var bindings = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        void Bind(string name, string type)
        {
            if (!bindings.TryGetValue(name, out var types)) bindings[name] = types = new(StringComparer.Ordinal);
            types.Add(type);
        }
        // Merge WP-CT-LANE-BL521-MERGE-01 — the concrete UserRepository is bound BY NAME (and only it): BL-521 FIX6's
        // PlatformAccountScopedUserRepository (14ce67b04) writes through a `UserRepository _inner` field, which no interface
        // pattern matched, so its forwarded writes were UNCLASSIFIED. Bound, each one is a counted row like any other writer.
        // FIX1 (small 3): with the Try / Record / Reset verbs, the receivers they reach are bound too — the audit recorders
        // (I…Recorder, non-authority below), the cross-service clients (I…Client) and the account-kind writer.
        // Owner correction 2026-10-14 (WP-LANE-MAIN-SYNC-02 A2): a NULLABLE declaration (`IESignReauthTicketService? x`,
        // `IMongoCollection<T>? x`) binds the same way — before, the `?` left main's new receivers UNCLASSIFIED.
        foreach (Match declaration in Regex.Matches(source, @"\b(?<type>I\w+(?:Repository|Service|Writer|Recorder|Client)|UserRepository|AccountKindWriter|IMongoDatabase|TenantSubscriptionTransactionWriter|IMongoCollection\s*<\s*\w+\s*>)\??\s+(?<name>\w+)"))
        {
            var type = declaration.Groups["type"].Value;
            if (type.StartsWith("IMongoCollection", StringComparison.Ordinal)) type = Regex.Match(type, @"<\s*(\w+)").Groups[1].Value;
            Bind(declaration.Groups["name"].Value, type);
        }
        foreach (Match collection in Regex.Matches(source, "(?<name>\\w+)\\s*=\\s*\\w+\\.GetCollection<(?<type>\\w+)>\\(\\s*\"(?<collection>[^\"]+)\""))
            Bind(collection.Groups["name"].Value, collection.Groups["type"].Value == "BsonDocument" ? collection.Groups["collection"].Value : collection.Groups["type"].Value);
        // A typed collection opened with the type's OWN name constant — GetCollection<T>(T.CollectionName) — binds to T.
        // Nothing wider: another type's constant, a variable, or a BsonDocument collection stays unbound (UNCLASSIFIED).
        foreach (Match collection in Regex.Matches(source, @"(?<name>\w+)\s*=\s*\w+\.GetCollection<(?<type>\w+)>\(\s*\k<type>\.CollectionName\s*\)"))
            if (collection.Groups["type"].Value != "BsonDocument") Bind(collection.Groups["name"].Value, collection.Groups["type"].Value);
        foreach (Match constructed in Regex.Matches(source, @"\bvar\s+(?<name>\w+)\s*=\s*new\s+(?:\w+\.)*(?<type>\w+(?:Repository|Service|Writer))\s*\("))
            Bind(constructed.Groups["name"].Value, constructed.Groups["type"].Value);
        var inherited = Regex.Match(source, @":\s*(?:Global)?RepositoryBase<(?<type>\w+)>");
        if (inherited.Success) Bind("Collection", inherited.Groups["type"].Value);
        Bind("TenantSubscriptionCommandSupport", "TenantSubscriptionCommandSupport");
        // FIX1 (small 3): BL-529's one administrator-reset helper is a static class — its calls are counted by its name.
        Bind("AdminPasswordReset", "AdminPasswordReset");
        foreach (Match alias in Regex.Matches(source, @"\bvar\s+(?<name>\w+)\s*=\s*(?<source>\w+)\s*;"))
            if (bindings.TryGetValue(alias.Groups["source"].Value, out var aliasTypes))
                foreach (var type in aliasTypes.ToArray()) Bind(alias.Groups["name"].Value, type);
        var inline = 0;
        source = Regex.Replace(source, "\\b\\w+\\.GetCollection<(?<type>\\w+)>\\(\\s*\"(?<collection>[^\"]+)\"\\)\\s*(?=\\.)", match =>
        {
            var receiver = "inlineCollection" + inline++;
            Bind(receiver, match.Groups["type"].Value == "BsonDocument" ? match.Groups["collection"].Value : match.Groups["type"].Value);
            return receiver;
        });
        var strings = Regex.Matches(source, "\"(?:\\\\.|[^\"\\\\])*\"");
        var calls = new List<string>();
        foreach (Match call in Regex.Matches(source, "(?:(?<raw>Raw)\\(\\s*\"(?<collection>[^\"]+)\"\\)|(?<receiver>\\b\\w+))\\s*\\.\\s*(?<method>(?:Create|Update|Delete|Insert|Replace|Remove|Revoke|Assign|Upsert|SoftDelete|BulkWrite|FindOneAndUpdate|Increment|Reactivate|Save|Set|Activate|Deactivate|Suspend|Provision|Grant|Try|Record|Reset)\\w*Async|(?:InsertOne|InsertMany|ReplaceOne|UpdateOne|UpdateMany|DeleteOne|DeleteMany|BulkWrite|FindOneAndUpdate|FindOneAndDelete|FindOneAndReplace)|RunCommand(?:Async)?)(?:\\s*<[^>]+>)?\\s*\\("))
        {
            if (strings.Any(text => call.Index >= text.Index && call.Index < text.Index + text.Length)) continue;
            var receiver = call.Groups["raw"].Success ? "Raw" : call.Groups["receiver"].Value;
            var method = call.Groups["method"].Value;
            if (receiver == "Indexes") continue; // Index metadata is not an entity-state mutation call.
            var types = call.Groups["raw"].Success ? [call.Groups["collection"].Value] : bindings.TryGetValue(receiver, out var known) ? known.ToArray() : [];
            var authority = types.Where(type => !NonAuthorityBindings.Contains(type, StringComparer.Ordinal)).ToArray();
            if (types.Length != 0 && authority.Length == 0) continue;
            var binding = authority.Length == 0 ? "UNCLASSIFIED" : string.Join('+', authority);
            if (method.StartsWith("RunCommand", StringComparison.Ordinal))
            {
                var arguments = source[(call.Index + call.Length)..];
                var direct = Regex.Match(arguments, "^\\s*new\\s+BsonDocument\\(\\s*\"(?<op>[^\"]+)\"\\s*,\\s*(?:\"(?<target>[^\"]+)\"|(?<target>\\d+))");
                var variable = Regex.Match(arguments, @"^\s*(?:session\s*,\s*)?(?<name>\w+)\s*[,)]");
                var definition = variable.Success ? Regex.Match(source, "\\bvar\\s+" + Regex.Escape(variable.Groups["name"].Value) + "\\s*=\\s*new\\s+BsonDocument\\s*\\{\\s*\\{\\s*\"(?<op>[^\"]+)\"\\s*,\\s*(?<target>\\w+)") : Match.Empty;
                var operation = direct.Success ? direct : definition;
                var target = operation.Groups["target"].Value;
                if (operation.Success && target == "Journal")
                {
                    var journals = Regex.Matches(source, "\\bconst\\s+string\\s+Journal\\s*=\\s*\"(?<collection>[^\"]+)\"\\s*;");
                    target = journals.Count == 1 ? journals[0].Groups["collection"].Value : "unresolved-Journal";
                    if (target == "serviceClientOperationalProvisioningOperations") target = "Journal";
                }
                method += operation.Success ? "[" + operation.Groups["op"].Value + ":" + target + "]" : "[unclassified]";
            }
            calls.Add(path + "|" + binding + "|" + receiver + "." + method);
        }
        foreach (Match call in Regex.Matches(source, "\\bawait\\s+InsertExistingAsync\\(\\s*\"(?<collection>[^\"]+)\""))
            calls.Add(path + "|" + call.Groups["collection"].Value + "|this.InsertExistingAsync");
        return calls;
    }

    private static readonly string[] NonAuthorityBindings =
    [
        "ServiceClientIdentity", "ServiceClientTenantGrant", "ServiceClientCredentialOperation", "ServiceClientCredentialRotationOperation",
        "AuthAuditEntry", "AuthAuditLog", "IntegrationEventInbox", "ProcessedIntegrationEvent", "MfaChallenge", "IMfaChallengeService", "ITenantUserMembershipRepository",
        "ITenantDomainRepository", "ITenantLoginSettingsRepository",
        // FIX1 (small 3): the audit recorders write the AUDIT TRAIL only (AuthAuditLog, already non-authority above, and the
        // Platform forward) — never a user, a role, a grant or a session.
        "IUserAuditRecorder", "IRoleAuditRecorder", "IRbacAuditRecorder"
    ];
    private static readonly string[] WriterRoots =
    [
        "services/Diten.AuthService/src/Diten.AuthService.Application", "services/Diten.AuthService/src/Diten.AuthService.Api",
        "services/Diten.AuthService/src/Diten.AuthService.Persistence",
        "services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants",
        "services/Diten.Platform/src/Diten.Platform.Application/Features/SubscriptionPlans",
        "services/Diten.Platform/src/Diten.Platform.Application/Features/ModuleCatalog",
        "services/Diten.Platform/src/Diten.Platform.Application/Features/ModuleRegistration",
        "services/Diten.Platform/src/Diten.Platform.Application/Features/ModulePages",
        "services/Diten.Platform/src/Diten.Platform.Application/Features/PlatformAdministrators"
    ];
    // DataSeeder rpCol.InsertOneAsync 16 and versionService.IncrementAsync 3: BRAND-FIX3 M-4 (93f148bc8) had raised them to 18 / 5
    //   with BackfillBrandsExportAsync (mdm.brands.export to every role holding mdm.brands.read, and its version bump). That
    //   method — and BackfillUsersExportAsync — no longer exist: WP-ROLES-CLOSE-01 (00d3e39b9, 089da345b) moved the export
    //   backfill out of DataSeeder into ExportGrantBackfillRunner, whose writes are the ExportGrantBackfillRunner rows below
    //   (the brands key is ExportGrantBackfill.Brands of that one runner; see the header).
    // BRAND-FIX3 M-4 · CT kararı 2026-10-07 · UsersExportGrantBackfill emsali · tek yönlü, kiracı başına bir kez, kalıcı işaretli
    // Owner correction 2026-10-14 (WP-LANE-MAIN-SYNC-02 A2, main-sync: MOD-0022) — four rows: _eSignTickets.RevokeWindowsAsync
    // in ChangePassword, ForcedChangeTenantPassword, Logout and SetTenantPassword. Each closes the e-signature password
    // windows of the user whose password / session just changed: it only ever NARROWS access. (The fifth approved row,
    // DataSeeder audit.InsertOneAsync, is an AuthAuditLog write — a non-authority binding, so it is not a counted row.)
    private const string OwnerCorrection20261014WriterCalls = """
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Auth/Handlers/CommandHandlers/ChangePasswordCommandHandler.cs|IESignReauthTicketService|_eSignTickets.RevokeWindowsAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Auth/Handlers/CommandHandlers/ForcedChangeTenantPasswordCommandHandler.cs|IESignReauthTicketService|_eSignTickets.RevokeWindowsAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Auth/Handlers/CommandHandlers/LogoutCommandHandler.cs|IESignReauthTicketService|_eSignTickets.RevokeWindowsAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Users/Handlers/CommandHandlers/SetTenantPasswordCommandHandler.cs|IESignReauthTicketService|_eSignTickets.RevokeWindowsAsync|1
    """;
    // Owner correction 2026-10-14 (WP-LANE-MAIN-SYNC-02 B1, owner decision) — nine changes, each with its reason:
    //   RoleProvisioningService _roleRepository.CreateSystemRoleIfMissingAsync|1 — WP-0018-ROLE1 (main): $setOnInsert only, an
    //     existing role is never overwritten; it REPLACES _roleRepository.UpsertSystemRoleAsync|1 (removed from the frozen list).
    //   RoleRepository Collection.UpdateOneAsync 2 → 3 — the same work (the insert-only system-role write).
    //   ESignReauthRepositories FindOneAndUpdateAsync / UpdateManyAsync / UpdateOneAsync — MOD-0022 E1 (main): the single-use
    //     ticket consume, the password window, the bulk revoke; each only NARROWS access.
    //   ESignReauthHandlers _users.RecordLoginFailureAsync / RecordLoginOutcomeAsync / TryClaimSignInAsync — the lane's lockout
    //     model (one atomic failure write, sign-in time only, the conditional claim) on main's re-authentication door.
    private const string OwnerCorrection20261014B1WriterCalls = """
    services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/RoleProvisioningService.cs|IRoleRepository|_roleRepository.CreateSystemRoleIfMissingAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/RoleRepository.cs|Role|Collection.UpdateOneAsync|3
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/ESignReauthRepositories.cs|ESignReauthTicket|Collection.FindOneAndUpdateAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/ESignReauthRepositories.cs|ESignReauthTicket|Collection.UpdateManyAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/ESignReauthRepositories.cs|ESignReauthTicket|Collection.UpdateOneAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/ESignReauth/ESignReauthHandlers.cs|IUserRepository|_users.RecordLoginFailureAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/ESignReauth/ESignReauthHandlers.cs|IUserRepository|_users.RecordLoginOutcomeAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/ESignReauth/ESignReauthHandlers.cs|IUserRepository|_users.TryClaimSignInAsync|1
    """;
    private const string FrozenWriterCalls = """
    services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/InternalEventsController.cs|AdminPasswordReset|AdminPasswordReset.ResetAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/InternalEventsController.cs|IIntegrationEventInboxRepository|_inboxRepository.TryInsertAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/InternalEventsController.cs|IUserRepository|_userRepository.CreateAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/InternalEventsController.cs|IUserRoleRepository|_userRoleRepository.AssignAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/InternalPermissionsController.cs|IFullCatalogPermissionGrantService|_fullCatalogGrantService.GrantToFullCatalogRolesAsync|2
    services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/InternalPermissionsController.cs|IPermissionRepository|_permissionRepository.CreateAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/InternalPermissionsController.cs|IPermissionRepository|_permissionRepository.DeleteAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/InternalPermissionsController.cs|IPermissionRepository|_permissionRepository.ReactivateAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/InternalPermissionsController.cs|IPermissionRepository|_permissionRepository.UpdateAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/InternalPermissionsController.cs|IRolePermissionRepository|_rolePermissionRepository.RemoveByPermissionIdAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/PlatformAuthController.cs|AdminPasswordReset|AdminPasswordReset.ResetAsync|2
    services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/PlatformAuthController.cs|IRefreshTokenRepository|_refreshTokenRepository.CreateAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/PlatformAuthController.cs|IRefreshTokenRepository|_refreshTokenRepository.RevokeAllByUserAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/PlatformAuthController.cs|IRefreshTokenRepository|_refreshTokenRepository.RevokeIfLiveAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/PlatformAuthController.cs|IRefreshTokenRepository|_refreshTokenRepository.RevokeLiveSessionsAsync|2
    services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/PlatformAuthController.cs|IUserRepository|_userRepository.CreateAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/PlatformAuthController.cs|IUserRepository|_userRepository.SetPasswordResetTokenAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/PlatformAuthController.cs|IUserRepository|_userRepository.TryWriteChangesAsync|3
    services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/EntitlementPermissionSyncService.cs|IRolePermissionRepository|_rolePermissions.AssignAsync|2
    services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/EntitlementPermissionSyncService.cs|IRolePermissionRepository|_rolePermissions.RemoveByIdAsync|2
    services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/EntitlementPermissionSyncService.cs|IRoleRepository|_roles.UpsertSystemRoleAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/FullCatalogPermissionGrantService.cs|IRolePermissionRepository|_rolePermissionRepository.AssignAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/PlatformAccountDiscriminator.cs|IUserRepository|users.SetPlatformActorTypeAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/PlatformAdministratorRoleConvergence.cs|IRoleRepository|roles.UpsertSystemRoleAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/PlatformAdministratorRoleConvergence.cs|IUserRoleRepository|userRoles.AssignAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/PlatformAdministratorRoleConvergence.cs|IUserRoleRepository|userRoles.RevokeAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/RoleProvisioningService.cs|IRolePermissionRepository|_rolePermissionRepository.AssignAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Auth/Handlers/CommandHandlers/ChangePasswordCommandHandler.cs|IRefreshTokenRepository|_refreshTokenRepository.RevokeLiveSessionsAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Auth/Handlers/CommandHandlers/ChangePasswordCommandHandler.cs|IUserRepository|_userRepository.TryWriteChangesAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Auth/Handlers/CommandHandlers/ForcedChangeTenantPasswordCommandHandler.cs|IRefreshTokenRepository|_refreshTokenRepository.CreateAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Auth/Handlers/CommandHandlers/ForcedChangeTenantPasswordCommandHandler.cs|IRefreshTokenRepository|_refreshTokenRepository.RevokeLiveSessionsAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Auth/Handlers/CommandHandlers/ForcedChangeTenantPasswordCommandHandler.cs|IUserRepository|_userRepository.TryWriteChangesAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Auth/Handlers/CommandHandlers/LoginCommandHandler.cs|IRefreshTokenRepository|_refreshTokenRepository.CreateAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Auth/Handlers/CommandHandlers/LoginCommandHandler.cs|IUserRepository|_userRepository.RecordLoginFailureAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Auth/Handlers/CommandHandlers/LoginCommandHandler.cs|IUserRepository|_userRepository.RecordLoginOutcomeAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Auth/Handlers/CommandHandlers/LoginCommandHandler.cs|IUserRepository|_userRepository.TryClaimSignInAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Auth/Handlers/CommandHandlers/LogoutCommandHandler.cs|IRefreshTokenRepository|_refreshTokenRepository.UpdateAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Auth/Handlers/CommandHandlers/PlatformLoginCommandHandler.cs|IRefreshTokenRepository|_refreshTokenRepository.CreateAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Auth/Handlers/CommandHandlers/PlatformLoginCommandHandler.cs|IUserRepository|_userRepository.RecordLoginOutcomeAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Auth/Handlers/CommandHandlers/RefreshTokenCommandHandler.cs|IRefreshTokenRepository|_refreshTokenRepository.CreateAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Auth/Handlers/CommandHandlers/RefreshTokenCommandHandler.cs|IRefreshTokenRepository|_refreshTokenRepository.RevokeAllByUserAsync|2
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Auth/Handlers/CommandHandlers/RefreshTokenCommandHandler.cs|IRefreshTokenRepository|_refreshTokenRepository.TryRotateAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Auth/Handlers/CommandHandlers/RegisterCommandHandler.cs|IRefreshTokenRepository|_refreshTokenRepository.CreateAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Auth/Handlers/CommandHandlers/RegisterCommandHandler.cs|IUserRepository|_userRepository.CreateAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Auth/Handlers/CommandHandlers/RegisterCommandHandler.cs|IUserRoleRepository|_userRoleRepository.AssignAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Auth/Handlers/CommandHandlers/VerifyMfaCommandHandler.cs|IRefreshTokenRepository|_refreshTokenRepository.CreateAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Auth/Handlers/CommandHandlers/VerifyMfaCommandHandler.cs|IUserRepository|_userRepository.RecordLoginOutcomeAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Auth/Services/IssuedSessionGuard.cs|IRefreshTokenRepository|refreshTokens.RevokeIfLiveAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Permissions/Handlers/CreatePermissionCommandHandler.cs|IPermissionRepository|_permissionRepository.CreateAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Permissions/Handlers/DeletePermissionCommandHandler.cs|IPermissionRepository|_permissionRepository.DeleteAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Roles/Handlers/CommandHandlers/AssignPermissionCommandHandler.cs|IRoleAssignmentVersionService|_versionService.IncrementAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Roles/Handlers/CommandHandlers/AssignPermissionCommandHandler.cs|IRolePermissionRepository|_rolePermissionRepository.AssignAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Roles/Handlers/CommandHandlers/CreateRoleCommandHandler.cs|IRoleAssignmentVersionService|_versionService.IncrementAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Roles/Handlers/CommandHandlers/CreateRoleCommandHandler.cs|IRoleRepository|_roleRepository.CreateAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Roles/Handlers/CommandHandlers/DeleteRoleCommandHandler.cs|IRoleAssignmentVersionService|_versionService.IncrementAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Roles/Handlers/CommandHandlers/DeleteRoleCommandHandler.cs|IRoleRepository|_roleRepository.DeleteAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Roles/Handlers/CommandHandlers/RevokePermissionCommandHandler.cs|IRefreshTokenRepository|_refreshTokenRepository.RevokeAllByUserAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Roles/Handlers/CommandHandlers/RevokePermissionCommandHandler.cs|IRoleAssignmentVersionService|_versionService.IncrementAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Roles/Handlers/CommandHandlers/RevokePermissionCommandHandler.cs|IRolePermissionRepository|_rolePermissionRepository.RevokeAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Roles/Handlers/CommandHandlers/UpdateRoleCommandHandler.cs|IRoleAssignmentVersionService|_versionService.IncrementAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Roles/Handlers/CommandHandlers/UpdateRoleCommandHandler.cs|IRoleRepository|_roleRepository.UpdateAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/ServiceIdentityTokens/Operational/ServiceClientOperationalProvisioningService.cs|IServiceClientOperationalProvisioningOperationRepository|_operations.TryCompleteAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/ServiceIdentityTokens/Operational/ServiceClientOperationalProvisioningService.cs|IServiceClientOperationalProvisioningOperationRepository|_operations.TryMarkRecoveryRequiredAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/ServiceIdentityTokens/Operational/ServiceClientOperationalProvisioningService.cs|IServiceClientOperationalProvisioningOperationRepository|_operations.TryReserveAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Users/Handlers/CommandHandlers/AdminResetPasswordCommandHandler.cs|AdminPasswordReset|AdminPasswordReset.ResetAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Users/Handlers/CommandHandlers/AssignRoleCommandHandler.cs|IRoleAssignmentVersionService|_versionService.IncrementAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Users/Handlers/CommandHandlers/AssignRoleCommandHandler.cs|IUserRoleRepository|_userRoleRepository.AssignAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Users/Handlers/CommandHandlers/CreateUserCommandHandler.cs|IUserQuotaClient|_quota.TryConsumeUserSeatAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Users/Handlers/CommandHandlers/CreateUserCommandHandler.cs|IUserRepository|_userRepository.CreateAsync|2
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Users/Handlers/CommandHandlers/DeleteUserCommandHandler.cs|IUserRepository|_userRepository.SoftDeleteAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Users/Handlers/CommandHandlers/ResendUserInvitationCommandHandler.cs|AdminPasswordReset|AdminPasswordReset.ResetAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Users/Handlers/CommandHandlers/ResendUserInvitationCommandHandler.cs|IUserRepository|_userRepository.TryWriteChangesAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Users/Handlers/CommandHandlers/RevokeRoleCommandHandler.cs|IRefreshTokenRepository|_refreshTokenRepository.RevokeAllByUserAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Users/Handlers/CommandHandlers/RevokeRoleCommandHandler.cs|IRoleAssignmentVersionService|_versionService.IncrementAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Users/Handlers/CommandHandlers/RevokeRoleCommandHandler.cs|IUserRoleRepository|_userRoleRepository.RevokeAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Users/Handlers/CommandHandlers/SetAccountKindCommandHandler.cs|AccountKindWriter|_kindWriter.RecordAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Users/Handlers/CommandHandlers/SetAccountKindCommandHandler.cs|IUserRepository|_userRepository.TryWriteChangesAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Users/Handlers/CommandHandlers/SetTenantPasswordCommandHandler.cs|IRefreshTokenRepository|_refreshTokenRepository.RevokeLiveSessionsAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Users/Handlers/CommandHandlers/SetTenantPasswordCommandHandler.cs|IUserRepository|_userRepository.TryWriteChangesAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Users/Handlers/CommandHandlers/SetUserActiveStatusCommandHandler.cs|IRefreshTokenRepository|_refreshTokenRepository.RevokeAllByUserAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Users/Handlers/CommandHandlers/SetUserActiveStatusCommandHandler.cs|IUserQuotaClient|_quota.TryConsumeUserSeatAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Users/Handlers/CommandHandlers/SetUserActiveStatusCommandHandler.cs|IUserRepository|_userRepository.TryWriteChangesAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Users/Handlers/CommandHandlers/UpdateMyPreferencesCommandHandler.cs|IUserRepository|users.TryWriteChangesAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Users/Handlers/CommandHandlers/UpdateUserCommandHandler.cs|AccountKindWriter|_kindWriter.RecordAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Users/Handlers/CommandHandlers/UpdateUserCommandHandler.cs|IRefreshTokenRepository|_refreshTokens.RevokeAllByUserAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Users/Handlers/CommandHandlers/UpdateUserCommandHandler.cs|IUserQuotaClient|_quota.TryConsumeUserSeatAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Users/Handlers/CommandHandlers/UpdateUserCommandHandler.cs|IUserRepository|_userRepository.TryWriteChangesAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Users/Services/AdminPasswordReset.cs|IRefreshTokenRepository|refreshTokens.RevokeLiveSessionsAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Users/Services/AdminPasswordReset.cs|IUserRepository|users.TryWriteChangesAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Operations/LegacyDeactivationMarker.cs|User|users.UpdateOneAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/GlobalRepositoryBase.cs|TEntity|Collection.InsertOneAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/GlobalRepositoryBase.cs|TEntity|Collection.ReplaceOneAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/PermissionRepository.cs|Permission|Collection.UpdateOneAsync|2
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/PlatformAccountScopedUserRepository.cs|UserRepository|_inner.CreateAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/PlatformAccountScopedUserRepository.cs|UserRepository|_inner.RecordLoginFailureAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/PlatformAccountScopedUserRepository.cs|UserRepository|_inner.RecordLoginOutcomeAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/PlatformAccountScopedUserRepository.cs|UserRepository|_inner.TryClaimSignInAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/PlatformAccountScopedUserRepository.cs|UserRepository|_inner.SetPasswordResetTokenAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/PlatformAccountScopedUserRepository.cs|UserRepository|_inner.SetPlatformActorTypeAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/PlatformAccountScopedUserRepository.cs|UserRepository|_inner.SoftDeleteAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/PlatformAccountScopedUserRepository.cs|UserRepository|_inner.TryWriteChangesAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/RefreshTokenRepository.cs|RefreshToken|Collection.UpdateManyAsync|2
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/RefreshTokenRepository.cs|RefreshToken|Collection.UpdateOneAsync|3
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/RepositoryBase.cs|TEntity|Collection.InsertOneAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/RepositoryBase.cs|TEntity|Collection.ReplaceOneAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/RepositoryBase.cs|TEntity|Collection.UpdateOneAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/RoleAssignmentVersionRepository.cs|RoleAssignmentVersionDocument|_collection.FindOneAndUpdateAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/RolePermissionRepository.cs|RolePermission|Collection.DeleteManyAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/RolePermissionRepository.cs|RolePermission|Collection.DeleteOneAsync|2
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/UserRepository.cs|User|Collection.FindOneAndUpdateAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/UserRepository.cs|User|Collection.UpdateOneAsync|6
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/UserRoleRepository.cs|UserRole|Collection.DeleteManyAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/UserRoleRepository.cs|UserRole|Collection.UpdateOneAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Seed/DataSeeder.cs|Permission+Role|col.BulkWriteAsync|6
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Seed/DataSeeder.cs|Permission+Role|col.InsertOneAsync|2
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Seed/DataSeeder.cs|Permission+Role|col.UpdateOneAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Seed/DataSeeder.cs|RoleAssignmentVersionRepository|versionService.IncrementAsync|3
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Seed/DataSeeder.cs|RolePermission|rpCol.InsertOneAsync|16
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Seed/DataSeeder.cs|Role|roleCol.InsertOneAsync|2
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Seed/DataSeeder.cs|UserRepository|userRepository.TryWriteChangesAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Seed/DataSeeder.cs|UserRole|urCol.DeleteManyAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Seed/DataSeeder.cs|UserRole|urCol.InsertOneAsync|3
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Seed/DataSeeder.cs|User|userCol.DeleteOneAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Seed/DataSeeder.cs|User|userCol.InsertOneAsync|2
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Seed/ExportGrantBackfillRunner.cs|RoleAssignmentVersionRepository|versions.IncrementAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Seed/ExportGrantBackfillRunner.cs|RolePermission|rpCol.UpdateOneAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Seed/ExportGrantBackfillRunner.cs|T|collection.InsertOneAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/ModuleCatalog/Handlers/CommandHandlers/ActivateModuleCatalogItemCommandHandler.cs|IGlobalApplicabilityStateRepository|_state.UpsertModuleCatalogAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/ModuleCatalog/Handlers/CommandHandlers/ActivateModuleCatalogItemCommandHandler.cs|ITransactionalModuleCatalogRepository|_repository.UpdateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/ModuleCatalog/Handlers/CommandHandlers/BulkDeleteModuleCatalogItemsCommandHandler.cs|IGlobalApplicabilityStateRepository|_state.UpsertModuleCatalogAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/ModuleCatalog/Handlers/CommandHandlers/BulkDeleteModuleCatalogItemsCommandHandler.cs|ITransactionalModuleCatalogRepository|_repository.DeleteAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/ModuleCatalog/Handlers/CommandHandlers/CreateModuleCatalogItemCommandHandler.cs|IGlobalApplicabilityStateRepository|_state.UpsertModuleCatalogAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/ModuleCatalog/Handlers/CommandHandlers/CreateModuleCatalogItemCommandHandler.cs|ITransactionalModuleCatalogRepository|_repository.CreateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/ModuleCatalog/Handlers/CommandHandlers/DeactivateModuleCatalogItemCommandHandler.cs|IGlobalApplicabilityStateRepository|_state.UpsertModuleCatalogAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/ModuleCatalog/Handlers/CommandHandlers/DeactivateModuleCatalogItemCommandHandler.cs|ITransactionalModuleCatalogRepository|_repository.UpdateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/ModuleCatalog/Handlers/CommandHandlers/DeleteModuleCatalogItemCommandHandler.cs|IGlobalApplicabilityStateRepository|_state.UpsertModuleCatalogAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/ModuleCatalog/Handlers/CommandHandlers/DeleteModuleCatalogItemCommandHandler.cs|ITransactionalModuleCatalogRepository|_repository.DeleteAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/ModuleCatalog/Handlers/CommandHandlers/UpdateModuleCatalogItemCommandHandler.cs|IGlobalApplicabilityStateRepository|_state.UpsertModuleCatalogAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/ModuleCatalog/Handlers/CommandHandlers/UpdateModuleCatalogItemCommandHandler.cs|ITransactionalModuleCatalogRepository|_repository.UpdateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/ModulePages/Handlers/CommandHandlers/ActivateModulePageDescriptorCommandHandler.cs|IModulePageDescriptorRepository|_repository.UpdateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/ModulePages/Handlers/CommandHandlers/CreateModulePageActionDescriptorCommandHandler.cs|IModulePageActionDescriptorRepository|_repository.CreateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/ModulePages/Handlers/CommandHandlers/CreateModulePageDescriptorCommandHandler.cs|IModulePageDescriptorRepository|_repository.CreateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/ModulePages/Handlers/CommandHandlers/DeactivateModulePageDescriptorCommandHandler.cs|IModulePageDescriptorRepository|_repository.UpdateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/ModulePages/Handlers/CommandHandlers/DeleteModulePageActionDescriptorCommandHandler.cs|ICatalogPermissionSyncService|_permissionSync.RemovePermissionAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/ModulePages/Handlers/CommandHandlers/DeleteModulePageActionDescriptorCommandHandler.cs|IModulePageActionDescriptorRepository|_repository.DeleteAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/ModulePages/Handlers/CommandHandlers/DeleteModulePageDescriptorCommandHandler.cs|ICatalogPermissionSyncService|_permissionSync.RemovePermissionAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/ModulePages/Handlers/CommandHandlers/DeleteModulePageDescriptorCommandHandler.cs|IModulePageDescriptorRepository|_repository.DeleteAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/ModulePages/Handlers/CommandHandlers/UpdateModulePageActionDescriptorCommandHandler.cs|IModulePageActionDescriptorRepository|_repository.UpdateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/ModulePages/Handlers/CommandHandlers/UpdateModulePageDescriptorCommandHandler.cs|IModulePageDescriptorRepository|_repository.UpdateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/ModuleRegistration/RegisterModuleManifestCommandHandler.cs|IGlobalApplicabilityStateRepository|_applicabilityState.UpsertModuleCatalogAsync|3
    services/Diten.Platform/src/Diten.Platform.Application/Features/ModuleRegistration/RegisterModuleManifestCommandHandler.cs|IModuleDomainRepository|_domainRepository.CreateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/ModuleRegistration/RegisterModuleManifestCommandHandler.cs|IModulePageActionDescriptorRepository|_actionRepository.CreateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/ModuleRegistration/RegisterModuleManifestCommandHandler.cs|IModulePageActionDescriptorRepository|_actionRepository.DeleteAsync|2
    services/Diten.Platform/src/Diten.Platform.Application/Features/ModuleRegistration/RegisterModuleManifestCommandHandler.cs|IModulePageActionDescriptorRepository|_actionRepository.UpdateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/ModuleRegistration/RegisterModuleManifestCommandHandler.cs|IModulePageDescriptorRepository|_pageRepository.CreateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/ModuleRegistration/RegisterModuleManifestCommandHandler.cs|IModulePageDescriptorRepository|_pageRepository.DeleteAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/ModuleRegistration/RegisterModuleManifestCommandHandler.cs|IModulePageDescriptorRepository|_pageRepository.UpdateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/ModuleRegistration/RegisterModuleManifestCommandHandler.cs|ITransactionalModuleCatalogRepository|_catalogRepository.CreateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/ModuleRegistration/RegisterModuleManifestCommandHandler.cs|ITransactionalModuleCatalogRepository|_catalogRepository.UpdateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/PlatformAdministrators/Handlers/CommandHandlers/AssignPlatformAdministratorRolesHandler.cs|IPlatformAdministratorRepository|_repository.UpdateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/PlatformAdministrators/Handlers/CommandHandlers/AssignPlatformAdministratorRolesHandler.cs|IPlatformAdministratorRepository|_repository.UpdateGuardedAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/PlatformAdministrators/Handlers/CommandHandlers/BulkDeletePlatformAdministratorsHandler.cs|IPlatformAdministratorRepository|_repository.SoftDeleteAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/PlatformAdministrators/Handlers/CommandHandlers/BulkDeletePlatformAdministratorsHandler.cs|IPlatformAdministratorRepository|_repository.SoftDeleteGuardedAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/PlatformAdministrators/Handlers/CommandHandlers/DeletePlatformAdministratorHandler.cs|IPlatformAdministratorRepository|_repository.SoftDeleteAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/PlatformAdministrators/Handlers/CommandHandlers/DeletePlatformAdministratorHandler.cs|IPlatformAdministratorRepository|_repository.SoftDeleteGuardedAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/PlatformAdministrators/Handlers/CommandHandlers/InvitePlatformAdministratorHandler.cs|IPlatformAdministratorProvisioningService|_provisioningService.ProvisionAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/PlatformAdministrators/Handlers/CommandHandlers/InvitePlatformAdministratorHandler.cs|IPlatformAdministratorRepository|_repository.CreateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/PlatformAdministrators/Handlers/CommandHandlers/ReactivatePlatformAdministratorHandler.cs|IPlatformAdministratorRepository|_repository.UpdateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/PlatformAdministrators/Handlers/CommandHandlers/ResendPlatformAdministratorInviteHandler.cs|IPlatformAdministratorProvisioningService|_provisioningService.ProvisionAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/PlatformAdministrators/Handlers/CommandHandlers/ResendPlatformAdministratorInviteHandler.cs|IPlatformAdministratorRepository|_repository.UpdateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/PlatformAdministrators/Handlers/CommandHandlers/SuspendPlatformAdministratorHandler.cs|IPlatformAdministratorRepository|_repository.UpdateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/PlatformAdministrators/Handlers/CommandHandlers/SuspendPlatformAdministratorHandler.cs|IPlatformAdministratorRepository|_repository.UpdateGuardedAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/PlatformAdministrators/Handlers/CommandHandlers/UpdatePlatformAdministratorHandler.cs|IPlatformAdministratorRepository|_repository.UpdateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/PlatformAdministrators/Handlers/CommandHandlers/UpdatePlatformAdministratorHandler.cs|IPlatformAdministratorRepository|_repository.UpdateGuardedAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/SubscriptionPlans/Handlers/CommandHandlers/ActivateSubscriptionPlanCommandHandler.cs|IGlobalApplicabilityStateRepository|_state.UpsertSubscriptionPlanAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/SubscriptionPlans/Handlers/CommandHandlers/ActivateSubscriptionPlanCommandHandler.cs|ITransactionalSubscriptionPlanRepository|_repository.UpdateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/SubscriptionPlans/Handlers/CommandHandlers/CreateSubscriptionPlanCommandHandler.cs|IGlobalApplicabilityStateRepository|_state.UpsertSubscriptionPlanAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/SubscriptionPlans/Handlers/CommandHandlers/CreateSubscriptionPlanCommandHandler.cs|ITransactionalSubscriptionPlanRepository|_repository.CreateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/SubscriptionPlans/Handlers/CommandHandlers/DeactivateSubscriptionPlanCommandHandler.cs|IGlobalApplicabilityStateRepository|_state.UpsertSubscriptionPlanAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/SubscriptionPlans/Handlers/CommandHandlers/DeactivateSubscriptionPlanCommandHandler.cs|ITransactionalSubscriptionPlanRepository|_repository.UpdateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/SubscriptionPlans/Handlers/CommandHandlers/SeedDefaultSubscriptionPlansCommandHandler.cs|IGlobalApplicabilityStateRepository|_state.UpsertSubscriptionPlanAsync|2
    services/Diten.Platform/src/Diten.Platform.Application/Features/SubscriptionPlans/Handlers/CommandHandlers/SeedDefaultSubscriptionPlansCommandHandler.cs|ITransactionalSubscriptionPlanRepository|_repository.CreateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/SubscriptionPlans/Handlers/CommandHandlers/SeedDefaultSubscriptionPlansCommandHandler.cs|ITransactionalSubscriptionPlanRepository|_repository.UpdateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/SubscriptionPlans/Handlers/CommandHandlers/UpdateSubscriptionPlanCommandHandler.cs|IGlobalApplicabilityStateRepository|_state.UpsertSubscriptionPlanAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/SubscriptionPlans/Handlers/CommandHandlers/UpdateSubscriptionPlanCommandHandler.cs|ITransactionalSubscriptionPlanRepository|_repository.UpdateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Commercial/Entitlements/Handlers/CommandHandlers/AddTenantModuleEntitlementCommandHandler.cs|IEntitlementStateVersionRepository|_versions.IncrementPhysicalEntitlementVersionAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Commercial/Entitlements/Handlers/CommandHandlers/AddTenantModuleEntitlementCommandHandler.cs|IQuotaService|_quotaService.TryConsumeEntitlementAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Commercial/Entitlements/Handlers/CommandHandlers/AddTenantModuleEntitlementCommandHandler.cs|ITenantModuleEntitlementRepository|_repository.CreateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Commercial/Entitlements/Handlers/CommandHandlers/DisableTenantModuleEntitlementCommandHandler.cs|IEntitlementStateVersionRepository|_versions.IncrementPhysicalEntitlementVersionAsync|3
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Commercial/Entitlements/Handlers/CommandHandlers/DisableTenantModuleEntitlementCommandHandler.cs|ITenantModuleEntitlementRepository|_repository.CreateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Commercial/Entitlements/Handlers/CommandHandlers/DisableTenantModuleEntitlementCommandHandler.cs|ITenantModuleEntitlementRepository|_repository.UpdateAsync|2
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Commercial/Entitlements/Handlers/CommandHandlers/EnableTenantModuleEntitlementCommandHandler.cs|IEntitlementStateVersionRepository|_versions.IncrementPhysicalEntitlementVersionAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Commercial/Entitlements/Handlers/CommandHandlers/EnableTenantModuleEntitlementCommandHandler.cs|IQuotaService|_quotaService.TryConsumeEntitlementAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Commercial/Entitlements/Handlers/CommandHandlers/EnableTenantModuleEntitlementCommandHandler.cs|ITenantModuleEntitlementRepository|_repository.UpdateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Commercial/Entitlements/Handlers/CommandHandlers/PhysicalEntitlementAuditIntent.cs|ITransactionalAuditOutboxWriter|writer.TryEnqueueAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Commercial/Entitlements/Handlers/CommandHandlers/RemoveTenantManualModuleOverrideCommandHandler.cs|IEntitlementStateVersionRepository|_versions.IncrementPhysicalEntitlementVersionAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Commercial/Entitlements/Handlers/CommandHandlers/RemoveTenantManualModuleOverrideCommandHandler.cs|ITenantModuleEntitlementRepository|_repository.SoftDeleteAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Commercial/Entitlements/Handlers/CommandHandlers/UpdateTenantModuleEntitlementExpiryCommandHandler.cs|IEntitlementStateVersionRepository|_versions.IncrementPhysicalEntitlementVersionAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Commercial/Entitlements/Handlers/CommandHandlers/UpdateTenantModuleEntitlementExpiryCommandHandler.cs|ITenantModuleEntitlementRepository|_repository.UpdateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Commercial/Subscriptions/Handlers/CommandHandlers/ActivateTenantSubscriptionCommandHandler.cs|TenantSubscriptionTransactionWriter|_writer.UpdateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Commercial/Subscriptions/Handlers/CommandHandlers/AssignPlanToTenantCommandHandler.cs|TenantSubscriptionCommandSupport|TenantSubscriptionCommandSupport.CreateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Commercial/Subscriptions/Handlers/CommandHandlers/CancelTenantSubscriptionCommandHandler.cs|TenantSubscriptionTransactionWriter|_writer.UpdateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Commercial/Subscriptions/Handlers/CommandHandlers/CreateTenantSubscriptionCommandHandler.cs|TenantSubscriptionCommandSupport|TenantSubscriptionCommandSupport.CreateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Commercial/Subscriptions/Handlers/CommandHandlers/ExpireTenantSubscriptionCommandHandler.cs|TenantSubscriptionTransactionWriter|_writer.UpdateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Commercial/Subscriptions/Handlers/CommandHandlers/ReactivateTenantSubscriptionCommandHandler.cs|TenantSubscriptionTransactionWriter|_writer.UpdateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Commercial/Subscriptions/Handlers/CommandHandlers/RenewTenantSubscriptionCommandHandler.cs|TenantSubscriptionTransactionWriter|_writer.UpdateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Commercial/Subscriptions/Handlers/CommandHandlers/SuspendTenantSubscriptionCommandHandler.cs|TenantSubscriptionTransactionWriter|_writer.UpdateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Commercial/Subscriptions/TenantSubscriptionCommandSupport.cs|ITenantRegistryRepository|tenantRepository.UpdateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Commercial/Subscriptions/TenantSubscriptionCommandSupport.cs|TenantSubscriptionTransactionWriter|writer.CreateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Commercial/Subscriptions/TenantSubscriptionTransactionWriter.cs|IEntitlementStateVersionRepository|_versions.IncrementSubscriptionSelectionVersionAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Commercial/Subscriptions/TenantSubscriptionTransactionWriter.cs|ITenantRegistryRepository|_tenants.UpdateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Commercial/Subscriptions/TenantSubscriptionTransactionWriter.cs|ITenantSubscriptionRepository|_subscriptions.CreateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Commercial/Subscriptions/TenantSubscriptionTransactionWriter.cs|ITenantSubscriptionRepository|_subscriptions.UpdateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Commercial/Subscriptions/TenantSubscriptionTransactionWriter.cs|ITransactionalAuditOutboxWriter|_audit.TryEnqueueAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Handlers/ActivateTenantAdminUserCommandHandler.cs|ITenantRegistryRepository|_repository.UpdateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Handlers/BulkDeleteTenantsCommandHandler.cs|ITenantRegistryRepository|_repository.DeleteAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Handlers/CreateTenantAdminUserCommandHandler.cs|ITenantRegistryRepository|_repository.UpdateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Handlers/DeleteTenantAdminUserCommandHandler.cs|ITenantRegistryRepository|_repository.UpdateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Handlers/DeleteTenantCommandHandler.cs|ITenantRegistryRepository|_repository.DeleteAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Handlers/GetTenantAdminUsersQueryHandler.cs|ITenantRegistryRepository|_repository.UpdateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Handlers/GetTenantUsersSummaryQueryHandler.cs|ITenantRegistryRepository|_repository.UpdateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Handlers/InviteTenantAdminUserCommandHandler.cs|IQuotaService|_quotaService.TryConsumeAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Handlers/InviteTenantAdminUserCommandHandler.cs|ITenantRegistryRepository|_repository.RecordAdminInvitationAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Handlers/InviteTenantAdminUserCommandHandler.cs|ITenantRegistryRepository|_repository.UpdateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Handlers/ReactivateTenantCommandHandler.cs|ITenantRegistryRepository|_repository.UpdateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Handlers/RegisterTenantCommandHandler.cs|ITenantRegistryRepository|_repository.CreateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Handlers/RegisterTenantCommandHandler.cs|ITenantRegistryRepository|_repository.UpdateAsync|2
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Handlers/RegisterTenantCommandHandler.cs|TenantSubscriptionCommandSupport|TenantSubscriptionCommandSupport.CreateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Handlers/RegisterTenantCommandHandler.cs|TenantSubscriptionCommandSupport|TenantSubscriptionCommandSupport.UpdateTenantSnapshotAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Handlers/SuspendTenantCommandHandler.cs|ITenantRegistryRepository|_repository.UpdateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Handlers/UpdateTenantAdminUserCommandHandler.cs|ITenantRegistryRepository|_repository.UpdateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Handlers/UpdateTenantBrandingCommandHandler.cs|ITenantRegistryRepository|_repository.UpdateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Handlers/UpdateTenantCommandHandler.cs|ITenantRegistryRepository|_repository.UpdateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Handlers/UpdateTenantLoginSettingsCommandHandler.cs|ITenantRegistryRepository|_tenantRepository.UpdateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Handlers/UpdateTenantSettingsCommandHandler.cs|ITenantRegistryRepository|_repository.UpdateAsync|1
    """;
    // Approved P5-01 journal and P5-02 store only; separate from the immutable d5f811ad baseline.
    private const string ApprovedOperationalWriterCalls = """
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/ServiceClientOperationalProvisioningOperationRepository.cs|ServiceClientOperationalProvisioningOperation|_collection.UpdateOneAsync|2
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/ServiceClientOperationalProvisioningOperationRepository.cs|IMongoDatabase|_database.RunCommandAsync[insert:Journal]|1
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/EntitlementReconciliationOperationStore.cs|IMongoDatabase|_database.RunCommandAsync[hello:1]|1
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/EntitlementReconciliationOperationStore.cs|IMongoDatabase|_database.RunCommandAsync[insert:collection]|2
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/EntitlementReconciliationOperationStore.cs|authAuditLogs|this.InsertExistingAsync|3
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/EntitlementReconciliationOperationStore.cs|auth_role_assignment_versions|Raw.UpdateOneAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/EntitlementReconciliationOperationStore.cs|refreshTokens|Raw.UpdateManyAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/EntitlementReconciliationOperationStore.cs|rolePermissions|Raw.DeleteOneAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/EntitlementReconciliationOperationStore.cs|rolePermissions|this.InsertExistingAsync|1
    """;
    // Approved BOOKKEEPING writes — owner classification, 2026-10-01 (WP-AUTH-TAKEOVER-DECISIONS-01). A row belongs here
    // only when the document it writes is a record that a seed step RAN, and is not itself authority.
    // 2026-10-09 (WP-CT-LANE-TKO-MERGE-01) — re-measured. The DataSeeder marks.ReplaceOneAsync row (users, then 1 -> 2 with
    // the takeover's BackfillBrandsExportAsync) no longer exists: the lane moved every export key's backfill into
    // ExportGrantBackfillRunner (WP-ROLES-CLOSE-01), and the brands key is one pair of it (ExportGrantBackfill.Brands). There:
    //   the MARK of every pair — users, roles AND brands — is one write, WriteMarkAsync → InsertOnceAsync<T> (counted under the
    //     generic T|collection.InsertOneAsync row in the frozen list, because the same helper also inserts grants and audit
    //     rows: that row is classified as authority, the strictest of the three);
    //   ExportGrantBackfillRunner | PermissionReconciliationMark | markCol.UpdateOneAsync — sets an EXISTING mark's Origin to
    //     "backfilled" after a source correction (089da345b). It writes only permissionReconciliations, confers no access,
    //     and no API reads or writes it: BOOKKEEPING, exactly as the users mark was.
    // Kept as a counted row (not a NonAuthorityBindings entry) on purpose: a SECOND write to that collection, from
    // anywhere in the writer roots, changes this inventory and needs its own classification.
    private const string ApprovedBookkeepingWriterCalls = """
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Seed/ExportGrantBackfillRunner.cs|PermissionReconciliationMark|markCol.UpdateOneAsync|1
    """;
    // Approved DECLARED SERVICE CLIENT writes — MOD-0033-FU02 Section F (CONTROL TOWER decision, 2026-10-01;
    // WP-GP-1A-TRUST-CHAIN-01). The first production writers of serviceClientIdentities / serviceClientTenantGrants:
    // at normal Auth startup, a first-party service client DECLARED in ServiceClientRegistry:Clients[] is created if
    // it is absent, and so is each listed tenant's grant. Both calls are insert-only (CreateIfAbsentAsync): an
    // existing identity or grant is never updated, revoked, disabled or deleted from this file, and each create is
    // written to the Auth audit log with a SYSTEM actor.
    // The two entity types stay in NonAuthorityBindings (raw collection writes inside the repositories are not what
    // this inventory counts); what IS counted is the call site, through the repository interfaces — so a second
    // caller of either method, anywhere in the writer roots, changes this inventory and needs its own row and reason.
    private const string ApprovedDeclaredServiceClientWriterCalls = """
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/ServiceIdentityTokens/Registry/ServiceClientRegistryReconciler.cs|IServiceClientIdentityRepository|identities.CreateIfAbsentAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/ServiceIdentityTokens/Registry/ServiceClientRegistryReconciler.cs|IServiceClientTenantGrantRepository|grants.CreateIfAbsentAsync|1
    """;
    [Fact]
    public void Bootstrap_WhenOperationalPathExists_LeavesNormalPersistenceRegistrationUnchangedAndAvoidsNormalHost()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(root.FullName, "AGENTS.md"))) root = root.Parent ?? throw new InvalidOperationException("Checkout required");
        var persistence = File.ReadAllText(Path.Combine(root.FullName, "services/Diten.AuthService/src/Diten.AuthService.Persistence/DependencyInjection.cs"));
        var normal = persistence[persistence.IndexOf("    public static IServiceCollection AddPersistence(", StringComparison.Ordinal)..].Replace("\r", "").Trim();
        // owner correction 2026-10-14, main-sync: MOD-0022 kayıtları (IESignReauthTicketRepository + IESignReauthWindowRepository, added
        // to AddPersistence by main). Before: 51D35FF8…D80FC, BL-521 FIX6 14ce67b04 (see the 2026-10-09 BL-521 note above).
        Assert.Equal("0E54C78818CE17A6AE614EC27739F00F5540E9C26C527A3CBC4801EA9BE0F1AA", EntitlementReconciliationPlan.Hash(normal));
        var operational = File.ReadAllText(Path.Combine(root.FullName, "services/Diten.AuthService/src/Diten.AuthService.Api/Operational/EntitlementReconciliationOperationalMode.cs"));
        Assert.Contains("AddEntitlementReconciliationPersistence(configuration)", operational);
        foreach (var forbidden in new[] { "AddPersistence(", "EnsureIndexes", "DataSeeder", "AddHostedService", "AddMassTransit", "GrantModule", "UpsertSystemRole", "WebApplication.CreateBuilder" })
            Assert.DoesNotContain(forbidden, operational, StringComparison.Ordinal);
        var program = File.ReadAllText(Path.Combine(root.FullName, RuntimeSources[0]));
        Assert.True(program.IndexOf("EntitlementReconciliationOperationalMode.IsRequested(args)", StringComparison.Ordinal)
            < program.IndexOf("WebApplication.CreateBuilder", StringComparison.Ordinal));
    }
    [Theory]
    [InlineData("Production", "Development")]
    [InlineData("Development", "Production")]
    [InlineData("development", "Development")]
    public async Task OperationalMode_WhenEitherProcessEnvironmentIsNotExactDevelopment_RejectsBeforeConfiguration(string dotnet, string aspnet)
    {
        var oldDotnet = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        var oldAspnet = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        try
        {
            Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", dotnet);
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", aspnet);
            Assert.Equal(2, await EntitlementReconciliationOperationalMode.RunAsync(PlanArguments()));
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", oldDotnet);
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", oldAspnet);
        }
    }

    [Theory]
    [InlineData("valid", null)]
    [InlineData("omitted", "RUNTIME_SOURCE_CHAIN_INCOMPLETE")]
    [InlineData("substituted", "RUNTIME_SOURCE_CHAIN_INCOMPLETE")]
    [InlineData("alias", "SOURCE_PATH_NOT_CANONICAL")]
    [InlineData("tampered-hash", "PROVENANCE_FILE_MISMATCH")]
    [InlineData("binary-missing", "BINARY_CHAIN_INCOMPLETE")]
    [InlineData("binary-tampered", "PROVENANCE_FILE_MISMATCH")]
    [InlineData("run-plan", null)]
    [InlineData("run-existing-output", null)]
    [InlineData("run-apply-missing", null)]
    [InlineData("run-apply-hash", null)]
    [InlineData("run-apply-binding", null)]
    [InlineData("run-apply-valid", null)]
    [InlineData("run-already-running", null)] // WP-AUTH-BL570-01: the single-instance lock, on every OS
    [InlineData("run-recovered-abandoned", null)] // BL570 FIX1 M8: a crashed run's lock is taken over and SAID
    public async Task Provenance_WhenExactRuntimeSourceSetChanges_FailsClosed(string mutation, string? reason)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(root.FullName, "AGENTS.md"))) root = root.Parent ?? throw new InvalidOperationException("Checkout required");
        using var git = new Process { StartInfo = new("git") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true } };
        foreach (var arg in new[] { "--no-optional-locks", "-C", root.FullName, "rev-parse", "HEAD" }) git.StartInfo.ArgumentList.Add(arg);
        Assert.True(git.Start()); var head = (await git.StandardOutput.ReadToEndAsync()).Trim(); await git.WaitForExitAsync(); Assert.Equal(0, git.ExitCode);
        var sources = RuntimeSources.Select(relative => Path.GetFullPath(Path.Combine(root.FullName, relative))).Select(path => new ProvenanceEntry(path, HashFile(path))).ToList();
        if (mutation == "omitted") sources.RemoveAt(0);
        if (mutation == "substituted") sources[0] = new(Path.Combine(root.FullName, "AGENTS.md"), HashFile(Path.Combine(root.FullName, "AGENTS.md")));
        if (mutation == "alias") sources[0] = sources[0] with { Path = Path.Combine(root.FullName, ".", RuntimeSources[0]) };
        if (mutation == "tampered-hash") sources[0] = sources[0] with { Sha256 = new string('0', 64) };
        var binaries = new[] { "Api", "Application", "Domain", "Infrastructure", "Persistence" }
            .Select(layer => Path.Combine(AppContext.BaseDirectory, "Diten.AuthService." + layer + ".dll"))
            .Select(path => new ProvenanceEntry(path, HashFile(path))).ToArray();
        if (mutation == "binary-missing") binaries = binaries.Where(x => !x.Path.EndsWith("Diten.AuthService.Domain.dll", StringComparison.Ordinal)).ToArray();
        if (mutation == "binary-tampered") binaries[1] = binaries[1] with { Sha256 = new string('0', 64) };
        var path = Path.Combine(Path.GetTempPath(), "entitlement-provenance-test-" + Guid.NewGuid() + ".json");
        try
        {
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new { SourceHead = head, SourceFiles = sources, Binaries = binaries }));
            var options = EntitlementReconciliationCommandOptions.Parse(PlanArguments()) with
                { ProvenanceManifest = path, ProvenanceSha256 = HashFile(path), BinarySha256 = binaries.Single(x => x.Path.EndsWith("Diten.AuthService.Api.dll", StringComparison.Ordinal)).Sha256 };
            if (mutation.StartsWith("run-", StringComparison.Ordinal)) { await VerifyRunArtifactsAsync(options, head, mutation); return; }
            var method = typeof(EntitlementReconciliationOperationalRunner).GetMethod("VerifyProvenanceAsync", BindingFlags.Static | BindingFlags.NonPublic)!;
            var task = (Task)method.Invoke(null, [options, CancellationToken.None])!;
            if (reason is null) await task;
            else Assert.Equal(reason, (await Assert.ThrowsAsync<InvalidOperationException>(() => task)).Message);
        }
        finally { File.Delete(path); }
    }

    private static async Task VerifyRunArtifactsAsync(EntitlementReconciliationCommandOptions options, string head, string scenario)
    {
        var (local, authority) = EntitlementPermissionSyncServiceTests.OperationalSnapshot();
        var plan = EntitlementReconciliationPlanner.Build(local, authority, options.OperationId, options.ProvenanceSha256,
            options.BinarySha256, head, DateTimeOffset.UtcNow);
        var store = new RecordingReconciliationStore(plan) { Snapshot = local, ReturnNoReceipt = true };
        var source = new RecordingEntitlementSource(plan) { Snapshot = authority };
        var warnings = new StringWriter();
        // run-recovered-abandoned: the lock REPORTS a taken-over abandoned run (the OS signal itself is unreliable on
        // macOS — measured; the takeover is real either way). What is under test is the runner's answer to it.
        var runner = new EntitlementReconciliationOperationalRunner(store, new RecordingTokenService(plan.ActorId), source, _ => Task.FromResult("stable-process-set"))
            { Warnings = warnings };
        if (scenario == "run-recovered-abandoned")
            runner = new EntitlementReconciliationOperationalRunner(store, new RecordingTokenService(plan.ActorId), source, _ => Task.FromResult("stable-process-set"))
                { Warnings = warnings, AcquireLock = _ => OperationalSingleInstanceLock.InState(acquired: true, recovered: true) };
        var output = options.PlanOutput!;
        try
        {
            if (scenario == "run-already-running")
            {
                // Another run holds the machine-wide lock: this one is refused at once, touching nothing.
                using (var other = OperationalSingleInstanceLock.TryAcquire(EntitlementReconciliationOperationalRunner.LockName))
                {
                    Assert.True(other.Acquired);
                    Assert.Equal("OPERATION_ALREADY_RUNNING", (await Assert.ThrowsAsync<InvalidOperationException>(
                        () => Task.Run(() => runner.RunAsync(options, "test-token", CancellationToken.None)))).Message);
                    Assert.False(File.Exists(output));
                    Assert.Equal(0, store.Commits);
                }
            }
            var result = await runner.RunAsync(options, "test-token", CancellationToken.None);
            Assert.StartsWith("PLAN " + output, result);
            Assert.Equal(scenario == "run-recovered-abandoned"
                ? EntitlementReconciliationOperationalRunner.LockRecoveredWarning + Environment.NewLine : "", warnings.ToString()); Assert.Equal(0, store.Commits); Assert.Equal(0, store.Finalizations);
            var json = await File.ReadAllTextAsync(output); var saved = JsonSerializer.Deserialize<EntitlementReconciliationPlan>(json)!;
            Assert.Equal(json, saved.CanonicalJson()); Assert.EndsWith(saved.Sha256(), result);
            Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(output)!, Path.GetFileName(output) + ".*.pending"));
            if (scenario is "run-plan" or "run-already-running" or "run-recovered-abandoned") return;
            if (scenario == "run-existing-output")
            {
                await Assert.ThrowsAsync<IOException>(() => runner.RunAsync(options, "test-token", CancellationToken.None));
                Assert.Equal(json, await File.ReadAllTextAsync(output)); return;
            }
            var apply = options with { Mode = "apply", PlanOutput = null, PlanManifest = output, PlanSha256 = saved.Sha256() };
            if (scenario == "run-apply-missing") apply = apply with { PlanManifest = output + ".missing" };
            if (scenario == "run-apply-hash") apply = apply with { PlanSha256 = new string('0', 64) };
            if (scenario == "run-apply-binding") apply = apply with { OperationId = Guid.NewGuid() };
            if (scenario == "run-apply-valid")
            {
                store.AllowCommit = true;
                store.Receipt = new(saved.OperationId, saved.Sha256(), "LOCAL_COMMITTED_AUTHORITY_REVALIDATION_PENDING", null,
                    saved.LocalFingerprint, saved.ExpectedRoleAssignmentVersion + 1, false);
                Assert.Equal("SUCCESS", await runner.RunAsync(apply, "test-token", CancellationToken.None));
                Assert.Equal(saved.CanonicalJson(), store.CommittedPlan!.CanonicalJson()); Assert.Equal(1, store.Commits); return;
            }
            if (scenario == "run-apply-missing") await Assert.ThrowsAsync<FileNotFoundException>(() => runner.RunAsync(apply, "test-token", CancellationToken.None));
            else Assert.Equal(scenario == "run-apply-hash" ? "PLAN_HASH_MISMATCH" : "PLAN_BINDING_MISMATCH",
                (await Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunAsync(apply, "test-token", CancellationToken.None))).Message);
            Assert.Equal(0, store.Commits);
        }
        finally
        {
            File.Delete(output);
            foreach (var temporary in Directory.GetFiles(Path.GetDirectoryName(output)!, Path.GetFileName(output) + ".*.pending")) File.Delete(temporary);
        }
    }
    private sealed record ProvenanceEntry(string Path, string Sha256);
    private static string HashFile(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static readonly string[] RuntimeSources =
    [
        "services/Diten.AuthService/src/Diten.AuthService.Api/Program.cs",
        "services/Diten.AuthService/src/Diten.AuthService.Application/Common/Interfaces/IEntitlementPermissionSyncService.cs",
        "services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/EntitlementPermissionSyncService.cs",
        "services/Diten.AuthService/src/Diten.AuthService.Application/Common/Interfaces/ITenantEntitlementClient.cs",
        "services/Diten.AuthService/src/Diten.AuthService.Infrastructure/Services/PlatformTenantEntitlementClient.cs",
        "services/Diten.AuthService/src/Diten.AuthService.Application/Common/Interfaces/ITokenService.cs",
        "services/Diten.AuthService/src/Diten.AuthService.Infrastructure/Services/TokenService.cs",
        "services/Diten.AuthService/src/Diten.AuthService.Persistence/DependencyInjection.cs",
        "services/Diten.AuthService/src/Diten.AuthService.Application/Common/Entitlements/EntitlementReconciliationPlan.cs",
        "services/Diten.AuthService/src/Diten.AuthService.Application/Common/Interfaces/IEntitlementReconciliationOperationStore.cs",
        "services/Diten.AuthService/src/Diten.AuthService.Api/Operational/EntitlementReconciliationOperationalMode.cs",
        "services/Diten.AuthService/src/Diten.AuthService.Api/Operational/EntitlementReconciliationCommandOptions.cs",
        "services/Diten.AuthService/src/Diten.AuthService.Api/Operational/EntitlementReconciliationOperationalRunner.cs",
        "services/Diten.AuthService/src/Diten.AuthService.Api/Operational/OperationalSingleInstanceLock.cs",
        "services/Diten.AuthService/src/Diten.AuthService.Api/Operational/UnixProcessObservation.cs",
        "services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/EntitlementReconciliationOperationStore.cs"
    ];
    internal static string[] PlanArguments() => [EntitlementReconciliationCommandOptions.Selector,
        "--mode", "plan", "--tenant-id", EntitlementReconciliationPlan.TargetTenant.ToString("D"),
        "--module-code", EntitlementReconciliationPlan.Module, "--operation-id", Guid.NewGuid().ToString("D"),
        "--provenance-manifest", Path.GetFullPath(Path.Combine(Path.GetTempPath(), "provenance.json")),
        "--expected-provenance-sha256", ReconciliationTestData.Digest, "--expected-binary-sha256", ReconciliationTestData.Digest,
        "--plan-output", Path.GetFullPath(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json"))];

    [Fact]
    public void Parse_WhenExactPlanArgumentsProvided_ReturnsBoundedPlan()
    {
        var options = EntitlementReconciliationCommandOptions.Parse(PlanArguments());
        Assert.Equal("plan", options.Mode); Assert.Equal(EntitlementReconciliationPlan.TargetTenant, options.TenantId);
        Assert.Null(options.PlanManifest); Assert.NotNull(options.PlanOutput);
    }

    [Theory]
    [InlineData("--mode", "PLAN")]
    [InlineData("--tenant-id", "00000000-0000-0000-0000-000000000001")]
    [InlineData("--module-code", "Product-Item-Sku-Master")]
    [InlineData("--operation-id", "not-guid")]
    [InlineData("--expected-provenance-sha256", "short")]
    [InlineData("--plan-output", "relative.json")]
    public void Parse_WhenArgumentWidensOrBreaksContract_RejectsBeforeBootstrap(string key, string value)
    {
        var args = PlanArguments(); args[Array.IndexOf(args, key) + 1] = value;
        Assert.Throws<InvalidOperationException>(() => EntitlementReconciliationCommandOptions.Parse(args));
    }

    [Theory]
    [InlineData("--operator-token")]
    [InlineData("--Tenant-id")]
    [InlineData("--mode")]
    public void Parse_WhenUnknownCaseVariantOrDuplicateFlagProvided_Rejects(string flag)
    {
        Assert.Throws<InvalidOperationException>(() => EntitlementReconciliationCommandOptions.Parse([.. PlanArguments(), flag, "value"]));
    }

    [Theory]
    [InlineData("6", "1", true)]
    [InlineData("7", "1", false)]
    [InlineData("6", "0", false)]
    public void Parse_WhenApplyDeltaSpecified_RequiresExactSixAndOne(string adds, string removes, bool accepted)
    {
        var args = PlanArguments(); args[2] = "apply"; args[15] = "--plan-manifest";
        args = [.. args, "--expected-plan-sha256", ReconciliationTestData.Digest, "--expected-add-count", adds,
            "--expected-remove-count", removes, "--expected-remove-grant-id", EntitlementReconciliationPlan.RemovedGrant.ToString("D")];
        if (accepted) Assert.Equal("apply", EntitlementReconciliationCommandOptions.Parse(args).Mode);
        else Assert.Throws<InvalidOperationException>(() => EntitlementReconciliationCommandOptions.Parse(args));
    }

    [Fact]
    public void CanonicalPlan_WhenCopiedThenInputCollectionsMutate_RetainsImmutableRowsAndHash()
    {
        var plan = ReconciliationTestData.Plan(); var before = plan.Sha256();
        Assert.Throws<NotSupportedException>(() => ((IList<EntitlementReconciliationRow>)plan.Rows).Clear());
        Assert.Equal(before, plan.Sha256());
        Assert.NotEqual(before, (plan with { LocalFingerprint = new string('B', 64) }).Sha256());
        Assert.Throws<InvalidOperationException>(() => (plan with { TenantId = Guid.NewGuid() }).CanonicalJson());
    }
}
