using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Diten.ManagementGovernanceService.Application.Modules.ProcessModeling;
using MediatR;

namespace Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest;

public readonly record struct NoContent;

public sealed record Response<T>(bool Succeeded,T? Data,int StatusCode,IReadOnlyList<string> Errors)
{
    public static Response<T> Success(T data,int status=200)=>new(true,data,status,Array.Empty<string>());
    public static Response<T> Fail(string error,int status)=>new(false,default,status,[error]);
}

public sealed record LocalTestCommandContext(Guid TenantId,Guid ActorId,string Permission,string IdempotencyKey,string RequestHash);
public sealed record ProcessModelingLocalCommand(string CommandName,Guid? RouteId,byte[] RawBody,LocalTestCommandContext Context)
    : IRequest<Response<object>>;

public interface IProcessModelingLocalCommandExecutor
{
    Task<Response<object>> ExecuteAsync(ProcessModelingLocalCommand command,CancellationToken cancellationToken);
}

public sealed class ProcessModelingLocalCommandHandler(IProcessModelingLocalCommandExecutor executor)
    : IRequestHandler<ProcessModelingLocalCommand,Response<object>>
{
    public Task<Response<object>> Handle(ProcessModelingLocalCommand request,CancellationToken cancellationToken)
        => executor.ExecuteAsync(request,cancellationToken);
}

public sealed class DefaultUnavailableProcessModelingLocalCommandExecutor : IProcessModelingLocalCommandExecutor
{
    public Task<Response<object>> ExecuteAsync(ProcessModelingLocalCommand command,CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Response<object>.Fail("process_modeling_local_test_executor_unavailable",503));
    }
}

public static class ProcessModelingLocalRequestHash
{
    private static readonly byte[] Prefix=Encoding.ASCII.GetBytes("DITEN-MOD0355-LOCAL-COMMAND-V1");
    public static string Compute(string method,string path,string commandName,Guid tenantId,Guid actorId,string permission,ReadOnlySpan<byte> rawBody)
    {
        using var stream=new MemoryStream(); stream.Write(Prefix);
        Write(stream,Encoding.UTF8.GetBytes(method)); Write(stream,Encoding.UTF8.GetBytes(path)); Write(stream,Encoding.UTF8.GetBytes(commandName));
        Write(stream,Encoding.UTF8.GetBytes(tenantId.ToString("D").ToLowerInvariant())); Write(stream,Encoding.UTF8.GetBytes(actorId.ToString("D").ToLowerInvariant()));
        Write(stream,Encoding.UTF8.GetBytes(permission)); Write(stream,rawBody);
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }
    private static void Write(Stream output,ReadOnlySpan<byte> value)
    { Span<byte> length=stackalloc byte[4]; BinaryPrimitives.WriteUInt32BigEndian(length,checked((uint)value.Length)); output.Write(length); output.Write(value); }
}

public static class ProcessModelingLocalRoutes
{
    public sealed record Route(string Command,string Method,string Template,string Permission);
    public static IReadOnlyList<Route> Exact { get; } =
    [
        R("CreateProcessArchitectureCommand","POST","/internal/local-test/v1/process-modeling/architectures"),R("UpdateProcessArchitectureCommand","PUT","/internal/local-test/v1/process-modeling/architectures/{id}"),R("ArchiveProcessArchitectureCommand","POST","/internal/local-test/v1/process-modeling/architectures/{id}/archive"),
        R("CreateProcessDomainCommand","POST","/internal/local-test/v1/process-modeling/domains"),R("UpdateProcessDomainCommand","PUT","/internal/local-test/v1/process-modeling/domains/{id}"),R("ArchiveProcessDomainCommand","POST","/internal/local-test/v1/process-modeling/domains/{id}/archive"),
        R("CreateProcessFamilyCommand","POST","/internal/local-test/v1/process-modeling/families"),R("UpdateProcessFamilyCommand","PUT","/internal/local-test/v1/process-modeling/families/{id}"),R("ArchiveProcessFamilyCommand","POST","/internal/local-test/v1/process-modeling/families/{id}/archive"),
        R("CreateProcessDefinitionCommand","POST","/internal/local-test/v1/process-modeling/definitions"),R("UpdateProcessDefinitionCommand","PUT","/internal/local-test/v1/process-modeling/definitions/{id}"),R("ArchiveProcessDefinitionCommand","POST","/internal/local-test/v1/process-modeling/definitions/{id}/archive"),
        R("CreateProcessModelCommand","POST","/internal/local-test/v1/process-modeling/models"),R("UpdateProcessModelCommand","PUT","/internal/local-test/v1/process-modeling/models/{id}"),
        R("UpdateDraftProcessModelVersionCommand","PUT","/internal/local-test/v1/process-modeling/model-versions/{id}/draft-content"),R("RequestProcessModelReviewCommand","POST","/internal/local-test/v1/process-modeling/model-versions/{id}/request-review"),R("ReturnProcessModelToDraftCommand","POST","/internal/local-test/v1/process-modeling/model-versions/{id}/return-to-draft"),R("PublishProcessModelVersionCommand","POST","/internal/local-test/v1/process-modeling/model-versions/{id}/publish"),R("RetireProcessModelVersionCommand","POST","/internal/local-test/v1/process-modeling/model-versions/{id}/retire"),R("CreateProcessModelRevisionCommand","POST","/internal/local-test/v1/process-modeling/models/{id}/revisions")
    ];
    private static Route R(string command,string method,string path)=>new(command,method,path,ProcessModelingPermissions.ExactCommandMap[command]);
}
