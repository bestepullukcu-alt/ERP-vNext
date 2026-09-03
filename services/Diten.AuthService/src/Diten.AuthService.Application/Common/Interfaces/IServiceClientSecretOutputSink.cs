namespace Diten.AuthService.Application.Common.Interfaces;

public interface IServiceClientSecretOutputSink
{
    Task PreflightAsync(string inheritedPipeHandle, CancellationToken cancellationToken);

    Task DeliverOnceAsync(
        Guid serviceClientIdentityId,
        string clientCode,
        string rawSecret,
        CancellationToken cancellationToken);
}
