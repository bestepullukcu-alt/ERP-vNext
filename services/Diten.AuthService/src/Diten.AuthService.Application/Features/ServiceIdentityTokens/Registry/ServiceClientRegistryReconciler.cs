using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Diten.AuthService.Application.Features.ServiceIdentityTokens.Registry;

/// <summary>
/// MOD-0033-FU02 §F.1 — brings the DECLARED first-party service clients into existence at Auth's startup.
///
/// <para>It only ever ADDS. A missing identity is created; a missing tenant grant is created (enabled). Whatever is
/// already persisted is left exactly as it is and is reported instead: an identity whose id, hash, version, service
/// name or audience differs from the declaration, a revoked identity, a grant somebody disabled. Nothing is updated,
/// revoked, disabled or deleted here, and removing a declaration changes nothing — rotation stays the operational
/// command's job (Section E), so a restart can never silently replace a credential.</para>
///
/// <para>These are authority writes. They are the two <c>CreateIfAbsentAsync</c> calls below and nothing else; each
/// create is recorded in the Auth audit log with a SYSTEM actor (client code, service name, audience, tenant — never
/// the hash).</para>
///
/// <para>THE AUDIT ROW IS WRITTEN FIRST (write-ahead), under an id DERIVED from the event and the thing created
/// (<see cref="IdentityAuditId"/>, <see cref="GrantAuditId"/>), with create-if-absent semantics. If that write fails,
/// nothing is inserted and startup is refused; the next start tries again. So an identity or a grant this class
/// created never exists without its audit row, whatever fails and however often Auth restarts — the other order
/// (insert, then audit) left a row that the next start found "already there" and never audited. The derived id is
/// what keeps it to ONE row per creation: several Auth instances starting together all write the same id and one
/// wins, and a start that repeats a creation whose insert failed finds its row already there.</para>
///
/// <para>Accepted consequence: the row records that the registry SET OUT to create the thing. If the insert then
/// fails and the declaration is removed before the next start, the row stands for a creation that never completed.
/// Over-reporting is accepted; under-reporting is not.</para>
/// </summary>
public sealed class ServiceClientRegistryReconciler(
    IServiceClientIdentityRepository identities,
    IServiceClientTenantGrantRepository grants,
    IAuthAuditService audit,
    TimeProvider timeProvider,
    ILogger<ServiceClientRegistryReconciler> logger)
{
    public const string SystemActor = "system:service-client-registry";
    public const string IdentityRegisteredEvent = "auth.service_client.registered";
    public const string TenantGrantedEvent = "auth.service_client.tenant_granted";

    public async Task<IReadOnlyList<ServiceClientRegistryNotice>> ApplyAsync(
        IReadOnlyList<ServiceClientDeclaration> declarations, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(declarations);
        var notices = new List<ServiceClientRegistryNotice>();
        foreach (var declaration in declarations)
        {
            ct.ThrowIfCancellationRequested();
            var identity = await EnsureIdentityAsync(declaration, notices, ct);
            if (identity is null)
            {
                if (declaration.Tenants.Count != 0)
                    Report(notices, ServiceClientRegistryOutcome.GrantsSkipped, declaration.ClientCode, null, []);
                continue;
            }

            foreach (var tenantId in declaration.Tenants)
            {
                await EnsureGrantAsync(declaration, identity, tenantId, notices, ct);
            }
        }

        return notices;
    }

    /// <returns>The persisted identity when it IS the declared client and may receive grants; otherwise null.</returns>
    private async Task<ServiceClientIdentity?> EnsureIdentityAsync(
        ServiceClientDeclaration declaration, List<ServiceClientRegistryNotice> notices, CancellationToken ct)
    {
        var existing = await identities.FindByClientCodeIncludingDeletedAsync(declaration.ClientCode, ct);
        if (existing is null)
        {
            var created = new ServiceClientIdentity
            {
                Id = ServiceClientIdentityIds.FromClientCode(declaration.ClientCode),
                ClientCode = declaration.ClientCode,
                ServiceName = declaration.ServiceName,
                AllowedAudience = declaration.AllowedAudience,
                ActiveCredentialHash = declaration.CredentialHash,
                ActiveCredentialVersion = declaration.CredentialVersion,
                CreatedAt = timeProvider.GetUtcNow(),
                CreatedBy = SystemActor
            };

            // Write-ahead (see the class remarks): no audit row, no insert. The result is not needed — false means
            // the row is already there (another instance, or an earlier start whose insert failed), which is all
            // the insert below requires; this instance never writes a second one.
            await audit.WriteOnceAsync(IdentityAuditId(created.Id), IdentityRegisteredEvent, null, Guid.Empty,
                JsonSerializer.Serialize(new
                {
                    actor = SystemActor,
                    clientCode = declaration.ClientCode,
                    serviceName = declaration.ServiceName,
                    audience = declaration.AllowedAudience
                }), ct);
            if (await identities.CreateIfAbsentAsync(created, ct))
            {
                Report(notices, ServiceClientRegistryOutcome.IdentityCreated, declaration.ClientCode, null, []);
                return created;
            }

            // Another Auth instance created it between the read and the insert. Treat it as existing.
            existing = await identities.FindByClientCodeIncludingDeletedAsync(declaration.ClientCode, ct)
                ?? throw new InvalidOperationException("Service client registry: identity state is inconsistent.");
        }

        if (existing.IsDeleted)
        {
            Report(notices, ServiceClientRegistryOutcome.IdentityDeleted, declaration.ClientCode, null, []);
            return null;
        }

        var differing = new List<string>(5);
        // The declared client's id is derived from its code, and Platform's allow entry names that id (it is the
        // token's sub). A row under another id — created earlier by hand or by a fixture — would be issued tokens
        // that Platform then refuses with 403, so it is reported here and is not the declared client.
        if (existing.Id != ServiceClientIdentityIds.FromClientCode(declaration.ClientCode))
            differing.Add(nameof(ServiceClientIdentity.Id));
        if (!string.Equals(existing.ServiceName, declaration.ServiceName, StringComparison.Ordinal))
            differing.Add(nameof(ServiceClientDeclaration.ServiceName));
        if (!ServiceIdentityTokenAudiencePolicy.TryResolveIdentityAudience(existing.AllowedAudience, out var persistedAudience)
            || !string.Equals(persistedAudience, declaration.AllowedAudience, StringComparison.Ordinal))
            differing.Add(nameof(ServiceClientDeclaration.AllowedAudience));
        var purposeMatches = differing.Count == 0;
        if (!string.Equals(existing.ActiveCredentialHash, declaration.CredentialHash, StringComparison.Ordinal))
            differing.Add(nameof(ServiceClientDeclaration.CredentialHash));
        if (!string.Equals(existing.ActiveCredentialVersion, declaration.CredentialVersion, StringComparison.Ordinal))
            differing.Add(nameof(ServiceClientDeclaration.CredentialVersion));

        Report(notices,
            differing.Count == 0 ? ServiceClientRegistryOutcome.IdentityUnchanged : ServiceClientRegistryOutcome.IdentityDiffers,
            declaration.ClientCode, null, differing);
        if (existing.IsRevoked)
        {
            Report(notices, ServiceClientRegistryOutcome.IdentityRevoked, declaration.ClientCode, null, []);
            return null;
        }

        // A rotated credential (hash/version) is still the declared client. A different id, service or audience is not.
        return purposeMatches ? existing : null;
    }

    private async Task EnsureGrantAsync(
        ServiceClientDeclaration declaration,
        ServiceClientIdentity identity,
        Guid tenantId,
        List<ServiceClientRegistryNotice> notices,
        CancellationToken ct)
    {
        var existing = await grants.FindIncludingDeletedAsync(tenantId, identity.Id, declaration.AllowedAudience, ct);
        if (existing is null)
        {
            var created = new ServiceClientTenantGrant
            {
                TenantId = tenantId,
                ServiceClientIdentityId = identity.Id,
                Audience = declaration.AllowedAudience,
                IsEnabled = true,
                CreatedAt = timeProvider.GetUtcNow(),
                CreatedBy = SystemActor
            };

            // Write-ahead, exactly as for the identity: no audit row, no insert.
            await audit.WriteOnceAsync(GrantAuditId(tenantId, identity.Id, declaration.AllowedAudience),
                TenantGrantedEvent, null, tenantId, JsonSerializer.Serialize(new
                {
                    actor = SystemActor,
                    clientCode = declaration.ClientCode,
                    serviceName = declaration.ServiceName,
                    audience = declaration.AllowedAudience,
                    tenantId = tenantId.ToString("D")
                }), ct);
            if (await grants.CreateIfAbsentAsync(created, ct))
            {
                Report(notices, ServiceClientRegistryOutcome.GrantCreated, declaration.ClientCode, tenantId, []);
                return;
            }

            existing = await grants.FindIncludingDeletedAsync(tenantId, identity.Id, declaration.AllowedAudience, ct)
                ?? throw new InvalidOperationException("Service client registry: grant state is inconsistent.");
        }

        Report(notices,
            existing.IsDeleted ? ServiceClientRegistryOutcome.GrantDeleted
            : existing.IsEnabled ? ServiceClientRegistryOutcome.GrantUnchanged
            : ServiceClientRegistryOutcome.GrantDisabled,
            declaration.ClientCode, tenantId, []);
    }

    /// <summary>The audit row id of "the registry created the identity with this id". Not a secret.</summary>
    public static Guid IdentityAuditId(Guid identityId) =>
        AuditId(IdentityRegisteredEvent, identityId.ToString("D"));

    /// <summary>The audit row id of "the registry granted this identity to this tenant for this audience".</summary>
    public static Guid GrantAuditId(Guid tenantId, Guid identityId, string audience)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(audience);
        return AuditId(TenantGrantedEvent, tenantId.ToString("D"), identityId.ToString("D"), audience);
    }

    /// <summary>Same event and same thing → same id, on every instance and every start (name-based, RFC 9562 v8).</summary>
    private static Guid AuditId(string eventName, params string[] subject)
    {
        // Line-separated: an event name, a "D" Guid and an audience from the closed list never contain a line break.
        var name = "diten:auth:service-client-registry:audit\n" + eventName + "\n" + string.Join('\n', subject);
        Span<byte> digest = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(name), digest);
        Span<byte> bytes = digest[..16];
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x80);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes, bigEndian: true);
    }

    private void Report(
        List<ServiceClientRegistryNotice> notices,
        ServiceClientRegistryOutcome outcome,
        string clientCode,
        Guid? tenantId,
        IReadOnlyList<string> fields)
    {
        notices.Add(new ServiceClientRegistryNotice(outcome, clientCode, tenantId, fields));
        var differing = string.Join(",", fields);
        switch (outcome)
        {
            case ServiceClientRegistryOutcome.IdentityCreated:
                logger.LogInformation("ServiceClientRegistry: created service client {ClientCode}.", clientCode);
                break;
            case ServiceClientRegistryOutcome.GrantCreated:
                logger.LogInformation("ServiceClientRegistry: granted service client {ClientCode} for tenant {TenantId}.", clientCode, tenantId);
                break;
            case ServiceClientRegistryOutcome.IdentityDiffers:
                logger.LogWarning("ServiceClientRegistry: service client {ClientCode} already exists and differs from configuration in {DifferingFields}; it was left unchanged.", clientCode, differing);
                break;
            case ServiceClientRegistryOutcome.IdentityRevoked:
                logger.LogWarning("ServiceClientRegistry: service client {ClientCode} is revoked; it was left revoked.", clientCode);
                break;
            case ServiceClientRegistryOutcome.IdentityDeleted:
                logger.LogWarning("ServiceClientRegistry: service client {ClientCode} was deleted; it was not created again.", clientCode);
                break;
            case ServiceClientRegistryOutcome.GrantDisabled:
                logger.LogWarning("ServiceClientRegistry: the grant of service client {ClientCode} for tenant {TenantId} is disabled; it was left disabled.", clientCode, tenantId);
                break;
            case ServiceClientRegistryOutcome.GrantDeleted:
                logger.LogWarning("ServiceClientRegistry: the grant of service client {ClientCode} for tenant {TenantId} was deleted; it was not created again.", clientCode, tenantId);
                break;
            case ServiceClientRegistryOutcome.GrantsSkipped:
                logger.LogWarning("ServiceClientRegistry: no tenant grant was created for service client {ClientCode} because the persisted client is not the declared one.", clientCode);
                break;
        }
    }
}
