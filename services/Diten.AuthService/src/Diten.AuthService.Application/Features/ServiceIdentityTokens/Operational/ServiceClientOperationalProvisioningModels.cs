using Diten.AuthService.Application.Common.Interfaces;

namespace Diten.AuthService.Application.Features.ServiceIdentityTokens.Operational;

public static class ServiceClientOperationalOperations
{
    public const string ReadIdentity = "read-identity";
    public const string ReadGrant = "read-grant";
    public const string RotateCredential = "rotate-credential";
    public static bool IsSupported(string value) => value is ReadIdentity or ReadGrant or RotateCredential;
    public static bool IsMutation(string value) => value == RotateCredential;
    public static bool EmitsSecret(string value) => value == RotateCredential;
}

public sealed record ServiceClientOperationalProvisioningRequest(
    string Operation, Guid CommandId, long ExpectedOperationalVersion,
    Guid? ServiceClientIdentityId, Guid? TenantId, string? ClientCode, string? ServiceName,
    string? Audience, string? InheritedPipeHandle, string? ExpectedCredentialVersion = null);

/// <summary>OperationalVersion/CredentialVersion are the recorded mutation outcome on replay, not current state.</summary>
public sealed record ServiceClientOperationalProvisioningResult(
    string Operation, Guid ServiceClientIdentityId, Guid? TenantId, string ClientCode, string ServiceName,
    string Audience, long OperationalVersion, string CredentialVersion, string CommandFingerprint,
    bool IsReplay, string SecretDisposition, bool? GrantEnabled = null,
    long? CurrentOperationalVersion = null, string? CurrentCredentialVersion = null);

public interface IServiceClientOperationalProvisioningService
{
    Task<ServiceClientOperationalProvisioningResult> ExecuteAsync(
        ServiceClientOperationalProvisioningRequest request, ServiceClientOperationalActor actor,
        CancellationToken cancellationToken);
}

public sealed class ServiceClientOperationalContractException(string message) : Exception(message);
public sealed class ServiceClientOperationalConflictException(string message) : Exception(message);
public sealed class ServiceClientOperationalNotFoundException(string message) : Exception(message);
public class ServiceClientOperationalRecoveryRequiredException(string message, Exception? inner = null)
    : Exception(message, inner);
public sealed class ServiceClientOperationalSecretDeliveryException(string message, Exception? inner = null)
    : ServiceClientOperationalRecoveryRequiredException(message, inner);
