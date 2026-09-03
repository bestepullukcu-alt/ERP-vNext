using Diten.AuthService.Application.Common.Interfaces;

namespace Diten.AuthService.Application.Features.ServiceIdentityTokens.Operational;

public static class ServiceClientOperationalOperations
{
    public const string ReadIdentity = "read-identity";
    public const string CreateIdentity = "create-identity";
    public const string RotateCredential = "rotate-credential";
    public const string RevokeIdentity = "revoke-identity";
    public const string ReadGrant = "read-grant";
    public const string EnableGrant = "enable-grant";
    public const string DisableGrant = "disable-grant";

    public static bool IsSupported(string value) => value is
        ReadIdentity or CreateIdentity or RotateCredential or RevokeIdentity
        or ReadGrant or EnableGrant or DisableGrant;

    public static bool IsMutation(string value) => value is
        CreateIdentity or RotateCredential or RevokeIdentity or EnableGrant or DisableGrant;

    public static bool EmitsSecret(string value) => value is CreateIdentity or RotateCredential;
}

public sealed record ServiceClientOperationalProvisioningRequest(
    string Operation,
    Guid CommandId,
    long ExpectedOperationalVersion,
    Guid? ServiceClientIdentityId,
    Guid? TenantId,
    string? ClientCode,
    string? ServiceName,
    string? Audience,
    string? InheritedPipeHandle);

public sealed record ServiceClientOperationalProvisioningResult(
    string Operation,
    Guid ServiceClientIdentityId,
    Guid? TenantId,
    string ClientCode,
    string ServiceName,
    string Audience,
    long OperationalVersion,
    string CommandFingerprint,
    bool IsReplay,
    string SecretDisposition);

public interface IServiceClientOperationalProvisioningService
{
    Task<ServiceClientOperationalProvisioningResult> ExecuteAsync(
        ServiceClientOperationalProvisioningRequest request,
        ServiceClientOperationalActor actor,
        CancellationToken cancellationToken);
}

public sealed class ServiceClientOperationalContractException(string message) : Exception(message);
public sealed class ServiceClientOperationalConflictException(string message) : Exception(message);
public sealed class ServiceClientOperationalNotFoundException(string message) : Exception(message);
public class ServiceClientOperationalRecoveryRequiredException(string message, Exception? inner = null)
    : Exception(message, inner);
public sealed class ServiceClientOperationalSecretDeliveryException(string message, Exception? inner = null)
    : ServiceClientOperationalRecoveryRequiredException(message, inner);
