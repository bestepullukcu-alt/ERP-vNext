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
        var expected = (FrozenWriterCalls + "\n" + ApprovedOperationalWriterCalls).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Order(StringComparer.Ordinal).ToArray();
        Assert.DoesNotContain(actual, row => row.Contains("|UNCLASSIFIED|", StringComparison.Ordinal));
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
        foreach (Match declaration in Regex.Matches(source, @"\b(?<type>I\w+(?:Repository|Service|Writer)|IMongoDatabase|TenantSubscriptionTransactionWriter|IMongoCollection\s*<\s*\w+\s*>)\s+(?<name>\w+)"))
        {
            var type = declaration.Groups["type"].Value;
            if (type.StartsWith("IMongoCollection", StringComparison.Ordinal)) type = Regex.Match(type, @"<\s*(\w+)").Groups[1].Value;
            Bind(declaration.Groups["name"].Value, type);
        }
        foreach (Match collection in Regex.Matches(source, "(?<name>\\w+)\\s*=\\s*\\w+\\.GetCollection<(?<type>\\w+)>\\(\\s*\"(?<collection>[^\"]+)\""))
            Bind(collection.Groups["name"].Value, collection.Groups["type"].Value == "BsonDocument" ? collection.Groups["collection"].Value : collection.Groups["type"].Value);
        foreach (Match constructed in Regex.Matches(source, @"\bvar\s+(?<name>\w+)\s*=\s*new\s+(?<type>\w+(?:Repository|Service|Writer))\s*\("))
            Bind(constructed.Groups["name"].Value, constructed.Groups["type"].Value);
        var inherited = Regex.Match(source, @":\s*(?:Global)?RepositoryBase<(?<type>\w+)>");
        if (inherited.Success) Bind("Collection", inherited.Groups["type"].Value);
        Bind("TenantSubscriptionCommandSupport", "TenantSubscriptionCommandSupport");
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
        foreach (Match call in Regex.Matches(source, "(?:(?<raw>Raw)\\(\\s*\"(?<collection>[^\"]+)\"\\)|(?<receiver>\\b\\w+))\\s*\\.\\s*(?<method>(?:Create|Update|Delete|Insert|Replace|Remove|Revoke|Assign|Upsert|SoftDelete|BulkWrite|FindOneAndUpdate|Increment|Reactivate|Save|Set|Activate|Deactivate|Suspend|Provision|Grant)\\w*Async|(?:InsertOne|InsertMany|ReplaceOne|UpdateOne|UpdateMany|DeleteOne|DeleteMany|BulkWrite|FindOneAndUpdate|FindOneAndDelete|FindOneAndReplace)|RunCommand(?:Async)?)(?:\\s*<[^>]+>)?\\s*\\("))
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
                method += operation.Success ? "[" + operation.Groups["op"].Value + ":" + operation.Groups["target"].Value + "]" : "[unclassified]";
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
        "ITenantDomainRepository", "ITenantLoginSettingsRepository"
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
    private const string FrozenWriterCalls = """
    services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/InternalEventsController.cs|IUserRepository|_userRepository.CreateAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/InternalEventsController.cs|IUserRepository|_userRepository.UpdateForTenantAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/InternalEventsController.cs|IUserRoleRepository|_userRoleRepository.AssignAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/InternalPermissionsController.cs|IFullCatalogPermissionGrantService|_fullCatalogGrantService.GrantToFullCatalogRolesAsync|2
    services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/InternalPermissionsController.cs|IPermissionRepository|_permissionRepository.CreateAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/InternalPermissionsController.cs|IPermissionRepository|_permissionRepository.DeleteAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/InternalPermissionsController.cs|IPermissionRepository|_permissionRepository.ReactivateAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/InternalPermissionsController.cs|IPermissionRepository|_permissionRepository.UpdateAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/InternalPermissionsController.cs|IRolePermissionRepository|_rolePermissionRepository.RemoveByPermissionIdAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/PlatformAuthController.cs|IRefreshTokenRepository|_refreshTokenRepository.CreateAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/PlatformAuthController.cs|IRefreshTokenRepository|_refreshTokenRepository.RevokeAllByUserAsync|4
    services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/PlatformAuthController.cs|IRoleRepository|_roleRepository.UpsertSystemRoleAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/PlatformAuthController.cs|IUserRepository|_userRepository.CreateAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/PlatformAuthController.cs|IUserRepository|_userRepository.UpdateForTenantAsync|5
    services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/PlatformAuthController.cs|IUserRoleRepository|_userRoleRepository.AssignAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Api/Controllers/PlatformAuthController.cs|IUserRoleRepository|_userRoleRepository.RevokeAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/EntitlementPermissionSyncService.cs|IRolePermissionRepository|_rolePermissions.AssignAsync|2
    services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/EntitlementPermissionSyncService.cs|IRolePermissionRepository|_rolePermissions.RemoveByIdAsync|2
    services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/EntitlementPermissionSyncService.cs|IRoleRepository|_roles.UpsertSystemRoleAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/FullCatalogPermissionGrantService.cs|IRolePermissionRepository|_rolePermissionRepository.AssignAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/RoleProvisioningService.cs|IRolePermissionRepository|_rolePermissionRepository.AssignAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/RoleProvisioningService.cs|IRoleRepository|_roleRepository.UpsertSystemRoleAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Auth/Handlers/CommandHandlers/ChangePasswordCommandHandler.cs|IRefreshTokenRepository|_refreshTokenRepository.RevokeAllByUserAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Auth/Handlers/CommandHandlers/ChangePasswordCommandHandler.cs|IUserRepository|_userRepository.UpdateAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Auth/Handlers/CommandHandlers/ForcedChangeTenantPasswordCommandHandler.cs|IRefreshTokenRepository|_refreshTokenRepository.CreateAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Auth/Handlers/CommandHandlers/ForcedChangeTenantPasswordCommandHandler.cs|IRefreshTokenRepository|_refreshTokenRepository.RevokeAllByUserAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Auth/Handlers/CommandHandlers/ForcedChangeTenantPasswordCommandHandler.cs|IUserRepository|_userRepository.UpdateForTenantAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Auth/Handlers/CommandHandlers/LoginCommandHandler.cs|IRefreshTokenRepository|_refreshTokenRepository.CreateAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Auth/Handlers/CommandHandlers/LoginCommandHandler.cs|IUserRepository|_userRepository.UpdateAsync|2
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Auth/Handlers/CommandHandlers/LogoutCommandHandler.cs|IRefreshTokenRepository|_refreshTokenRepository.UpdateAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Auth/Handlers/CommandHandlers/PlatformLoginCommandHandler.cs|IRefreshTokenRepository|_refreshTokenRepository.CreateAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Auth/Handlers/CommandHandlers/PlatformLoginCommandHandler.cs|IUserRepository|_userRepository.UpdateForTenantAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Auth/Handlers/CommandHandlers/RefreshTokenCommandHandler.cs|IRefreshTokenRepository|_refreshTokenRepository.CreateAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Auth/Handlers/CommandHandlers/RefreshTokenCommandHandler.cs|IRefreshTokenRepository|_refreshTokenRepository.RevokeAllByUserAsync|2
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Auth/Handlers/CommandHandlers/RefreshTokenCommandHandler.cs|IRefreshTokenRepository|_refreshTokenRepository.UpdateAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Auth/Handlers/CommandHandlers/RegisterCommandHandler.cs|IRefreshTokenRepository|_refreshTokenRepository.CreateAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Auth/Handlers/CommandHandlers/RegisterCommandHandler.cs|IUserRepository|_userRepository.CreateAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Auth/Handlers/CommandHandlers/RegisterCommandHandler.cs|IUserRoleRepository|_userRoleRepository.AssignAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Auth/Handlers/CommandHandlers/VerifyMfaCommandHandler.cs|IRefreshTokenRepository|_refreshTokenRepository.CreateAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Auth/Handlers/CommandHandlers/VerifyMfaCommandHandler.cs|IUserRepository|_userRepository.UpdateAsync|1
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
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Users/Handlers/CommandHandlers/AdminResetPasswordCommandHandler.cs|IUserRepository|_userRepository.UpdateForTenantAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Users/Handlers/CommandHandlers/AssignRoleCommandHandler.cs|IRoleAssignmentVersionService|_versionService.IncrementAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Users/Handlers/CommandHandlers/AssignRoleCommandHandler.cs|IUserRoleRepository|_userRoleRepository.AssignAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Users/Handlers/CommandHandlers/CreateUserCommandHandler.cs|IUserRepository|_userRepository.CreateAsync|2
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Users/Handlers/CommandHandlers/DeleteUserCommandHandler.cs|IUserRepository|_userRepository.SoftDeleteAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Users/Handlers/CommandHandlers/ResendUserInvitationCommandHandler.cs|IUserRepository|_userRepository.UpdateForTenantAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Users/Handlers/CommandHandlers/RevokeRoleCommandHandler.cs|IRefreshTokenRepository|_refreshTokenRepository.RevokeAllByUserAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Users/Handlers/CommandHandlers/RevokeRoleCommandHandler.cs|IRoleAssignmentVersionService|_versionService.IncrementAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Users/Handlers/CommandHandlers/RevokeRoleCommandHandler.cs|IUserRoleRepository|_userRoleRepository.RevokeAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Users/Handlers/CommandHandlers/SetTenantPasswordCommandHandler.cs|IRefreshTokenRepository|_refreshTokenRepository.RevokeAllByUserAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Users/Handlers/CommandHandlers/SetTenantPasswordCommandHandler.cs|IUserRepository|_userRepository.UpdateForTenantAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Users/Handlers/CommandHandlers/SetUserActiveStatusCommandHandler.cs|IRefreshTokenRepository|_refreshTokenRepository.RevokeAllByUserAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Users/Handlers/CommandHandlers/SetUserActiveStatusCommandHandler.cs|IUserRepository|_userRepository.UpdateAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Application/Features/Users/Handlers/CommandHandlers/UpdateUserCommandHandler.cs|IUserRepository|_userRepository.UpdateAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/GlobalRepositoryBase.cs|TEntity|Collection.InsertOneAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/GlobalRepositoryBase.cs|TEntity|Collection.ReplaceOneAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/PermissionRepository.cs|Permission|Collection.UpdateOneAsync|2
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/RefreshTokenRepository.cs|RefreshToken|Collection.UpdateManyAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/RefreshTokenRepository.cs|RefreshToken|Collection.UpdateOneAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/RepositoryBase.cs|TEntity|Collection.InsertOneAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/RepositoryBase.cs|TEntity|Collection.ReplaceOneAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/RepositoryBase.cs|TEntity|Collection.UpdateOneAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/RoleAssignmentVersionRepository.cs|RoleAssignmentVersionDocument|_collection.FindOneAndUpdateAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/RolePermissionRepository.cs|RolePermission|Collection.DeleteManyAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/RolePermissionRepository.cs|RolePermission|Collection.DeleteOneAsync|2
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/RoleRepository.cs|Role|Collection.UpdateOneAsync|2
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/UserRepository.cs|User|Collection.ReplaceOneAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/UserRepository.cs|User|Collection.UpdateOneAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/UserRoleRepository.cs|UserRole|Collection.DeleteOneAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Seed/DataSeeder.cs|Permission+Role|col.BulkWriteAsync|3
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Seed/DataSeeder.cs|Permission+Role|col.InsertOneAsync|2
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Seed/DataSeeder.cs|RolePermission|rpCol.InsertOneAsync|11
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Seed/DataSeeder.cs|Role|roleCol.InsertOneAsync|2
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Seed/DataSeeder.cs|RoleAssignmentVersionRepository|versionService.IncrementAsync|3
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Seed/DataSeeder.cs|UserRole|urCol.DeleteManyAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Seed/DataSeeder.cs|UserRole|urCol.InsertOneAsync|3
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Seed/DataSeeder.cs|User|userCol.DeleteOneAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Seed/DataSeeder.cs|User|userCol.InsertOneAsync|2
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Seed/DataSeeder.cs|User|userCol.ReplaceOneAsync|1
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
    services/Diten.Platform/src/Diten.Platform.Application/Features/ModuleRegistration/RegisterModuleManifestCommandHandler.cs|IGlobalApplicabilityStateRepository|_applicabilityState.UpsertModuleCatalogAsync|2
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
    services/Diten.Platform/src/Diten.Platform.Application/Features/PlatformAdministrators/Handlers/CommandHandlers/BulkDeletePlatformAdministratorsHandler.cs|IPlatformAdministratorRepository|_repository.SoftDeleteAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/PlatformAdministrators/Handlers/CommandHandlers/DeletePlatformAdministratorHandler.cs|IPlatformAdministratorRepository|_repository.SoftDeleteAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/PlatformAdministrators/Handlers/CommandHandlers/InvitePlatformAdministratorHandler.cs|IPlatformAdministratorProvisioningService|_provisioningService.ProvisionAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/PlatformAdministrators/Handlers/CommandHandlers/InvitePlatformAdministratorHandler.cs|IPlatformAdministratorRepository|_repository.CreateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/PlatformAdministrators/Handlers/CommandHandlers/ReactivatePlatformAdministratorHandler.cs|IPlatformAdministratorRepository|_repository.UpdateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/PlatformAdministrators/Handlers/CommandHandlers/ResendPlatformAdministratorInviteHandler.cs|IPlatformAdministratorProvisioningService|_provisioningService.ProvisionAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/PlatformAdministrators/Handlers/CommandHandlers/ResendPlatformAdministratorInviteHandler.cs|IPlatformAdministratorRepository|_repository.UpdateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/PlatformAdministrators/Handlers/CommandHandlers/SuspendPlatformAdministratorHandler.cs|IPlatformAdministratorRepository|_repository.UpdateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/PlatformAdministrators/Handlers/CommandHandlers/UpdatePlatformAdministratorHandler.cs|IPlatformAdministratorRepository|_repository.UpdateAsync|1
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
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Commercial/Entitlements/Handlers/CommandHandlers/AddTenantModuleEntitlementCommandHandler.cs|ITenantModuleEntitlementRepository|_repository.CreateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Commercial/Entitlements/Handlers/CommandHandlers/DisableTenantModuleEntitlementCommandHandler.cs|IEntitlementStateVersionRepository|_versions.IncrementPhysicalEntitlementVersionAsync|3
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Commercial/Entitlements/Handlers/CommandHandlers/DisableTenantModuleEntitlementCommandHandler.cs|ITenantModuleEntitlementRepository|_repository.CreateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Commercial/Entitlements/Handlers/CommandHandlers/DisableTenantModuleEntitlementCommandHandler.cs|ITenantModuleEntitlementRepository|_repository.UpdateAsync|2
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Commercial/Entitlements/Handlers/CommandHandlers/EnableTenantModuleEntitlementCommandHandler.cs|IEntitlementStateVersionRepository|_versions.IncrementPhysicalEntitlementVersionAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Commercial/Entitlements/Handlers/CommandHandlers/EnableTenantModuleEntitlementCommandHandler.cs|ITenantModuleEntitlementRepository|_repository.UpdateAsync|1
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
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Commercial/Subscriptions/TenantSubscriptionTransactionWriter.cs|ITenantRegistryRepository|_tenants.UpdateAsync|2
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Commercial/Subscriptions/TenantSubscriptionTransactionWriter.cs|ITenantSubscriptionRepository|_subscriptions.CreateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Commercial/Subscriptions/TenantSubscriptionTransactionWriter.cs|ITenantSubscriptionRepository|_subscriptions.UpdateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Handlers/ActivateTenantAdminUserCommandHandler.cs|ITenantRegistryRepository|_repository.UpdateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Handlers/BulkDeleteTenantsCommandHandler.cs|ITenantRegistryRepository|_repository.DeleteAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Handlers/CreateTenantAdminUserCommandHandler.cs|ITenantRegistryRepository|_repository.UpdateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Handlers/DeleteTenantAdminUserCommandHandler.cs|ITenantRegistryRepository|_repository.UpdateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Handlers/DeleteTenantCommandHandler.cs|ITenantRegistryRepository|_repository.DeleteAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Handlers/GetTenantAdminUsersQueryHandler.cs|ITenantRegistryRepository|_repository.UpdateAsync|1
    services/Diten.Platform/src/Diten.Platform.Application/Features/Tenants/Handlers/GetTenantUsersSummaryQueryHandler.cs|ITenantRegistryRepository|_repository.UpdateAsync|1
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
    // Approved P5-02 operation store only. This is separate from the immutable d5f811ad writer baseline.
    private const string ApprovedOperationalWriterCalls = """
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/EntitlementReconciliationOperationStore.cs|IMongoDatabase|_database.RunCommandAsync[hello:1]|1
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/EntitlementReconciliationOperationStore.cs|IMongoDatabase|_database.RunCommandAsync[insert:collection]|2
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/EntitlementReconciliationOperationStore.cs|authAuditLogs|this.InsertExistingAsync|3
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/EntitlementReconciliationOperationStore.cs|auth_role_assignment_versions|Raw.UpdateOneAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/EntitlementReconciliationOperationStore.cs|refreshTokens|Raw.UpdateManyAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/EntitlementReconciliationOperationStore.cs|rolePermissions|Raw.DeleteOneAsync|1
    services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/EntitlementReconciliationOperationStore.cs|rolePermissions|this.InsertExistingAsync|1
    """;
    [Fact]
    public void Bootstrap_WhenOperationalPathExists_LeavesNormalPersistenceRegistrationUnchangedAndAvoidsNormalHost()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(root.FullName, "AGENTS.md"))) root = root.Parent ?? throw new InvalidOperationException("Checkout required");
        var persistence = File.ReadAllText(Path.Combine(root.FullName, "services/Diten.AuthService/src/Diten.AuthService.Persistence/DependencyInjection.cs"));
        var normal = persistence[persistence.IndexOf("    public static IServiceCollection AddPersistence(", StringComparison.Ordinal)..].Replace("\r", "").Trim();
        Assert.Equal("F318BCF7B220BB127BBAE2F07532C7A79686579458705C913720D8D098B9BF8F", EntitlementReconciliationPlan.Hash(normal));
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
        var runner = new EntitlementReconciliationOperationalRunner(store, new RecordingTokenService(plan.ActorId), source, _ => Task.FromResult("stable-process-set"));
        var output = options.PlanOutput!;
        try
        {
            var result = await runner.RunAsync(options, "test-token", CancellationToken.None);
            Assert.StartsWith("PLAN " + output, result); Assert.Equal(0, store.Commits); Assert.Equal(0, store.Finalizations);
            var json = await File.ReadAllTextAsync(output); var saved = JsonSerializer.Deserialize<EntitlementReconciliationPlan>(json)!;
            Assert.Equal(json, saved.CanonicalJson()); Assert.EndsWith(saved.Sha256(), result);
            Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(output)!, Path.GetFileName(output) + ".*.pending"));
            if (scenario == "run-plan") return;
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
