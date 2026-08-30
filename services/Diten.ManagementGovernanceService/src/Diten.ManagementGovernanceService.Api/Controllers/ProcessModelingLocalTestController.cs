using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Diten.ManagementGovernanceService.Api.LocalTestSecurity;
using Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest;
using Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest.Commands;
using Diten.ManagementGovernanceService.Application.Modules.ProcessModeling;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.ManagementGovernanceService.Api.Controllers;

[Route("internal/local-test/v1/process-modeling")]
[Authorize(AuthenticationSchemes=LocalTestAuthenticationHandler.SchemeName)]
public sealed class ProcessModelingLocalTestController(ISender sender,IServiceProvider services):CustomBaseController
{
    private static readonly JsonSerializerOptions StrictJson=new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive=false,
        UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow
    };

    [AllowAnonymous,HttpGet("health")]
    public IActionResult Health()=>Ok(new{status="healthy"});

    [HttpGet("models")]
    public async Task<IActionResult> Models(CancellationToken cancellationToken)
    {
        var identity=Identity();if(identity.Error is not null)return identity.Error;
        if(!HasPermission(ProcessModelingPermissions.ExactPermissions[8]))return Result(Response<NoContent>.Fail("process_model_permission_denied",403));
        var queries=services.GetService<IProcessModelingLocalQueryStore>();
        if(queries is null)return Result(Response<NoContent>.Fail("process_modeling_local_test_query_unavailable",503));
        return Result(Response<IReadOnlyList<ProcessModelListItem>>.Success(await queries.ListAsync(identity.TenantId,cancellationToken)));
    }

    [HttpGet("models/{id:guid}")]
    public async Task<IActionResult> Model(Guid id,CancellationToken cancellationToken)
    {
        var identity=Identity();if(identity.Error is not null)return identity.Error;
        var queries=services.GetService<IProcessModelingLocalQueryStore>();
        if(queries is null)return Result(Response<NoContent>.Fail("process_modeling_local_test_query_unavailable",503));
        var value=await queries.GetAsync(identity.TenantId,id,cancellationToken);
        if(value is null)return Result(Response<ProcessModelDetail>.Fail("process_model_not_found",404));
        if(!HasPermission(ProcessModelingPermissions.ExactPermissions[8]))return Result(Response<ProcessModelDetail>.Fail("process_model_permission_denied",403));
        return Result(Response<ProcessModelDetail>.Success(value));
    }

    [HttpGet("model-versions/{id:guid}")]
    public async Task<IActionResult> Version(Guid id,CancellationToken cancellationToken)
    {
        var identity=Identity();if(identity.Error is not null)return identity.Error;
        var queries=services.GetService<IProcessModelingLocalQueryStore>();
        if(queries is null)return Result(Response<NoContent>.Fail("process_modeling_local_test_query_unavailable",503));
        var value=await queries.GetVersionAsync(identity.TenantId,id,cancellationToken);
        if(value is null)return Result(Response<ProcessModelVersionView>.Fail("process_model_not_found",404));
        if(!HasPermission(ProcessModelingPermissions.ExactPermissions[8]))return Result(Response<ProcessModelVersionView>.Fail("process_model_permission_denied",403));
        return Result(Response<ProcessModelVersionView>.Success(value));
    }

    [HttpPost("architectures")]
    public Task<IActionResult> CreateArchitecture()=>Create<CreateArchitectureBody,CreateProcessArchitectureCommand>(
        nameof(CreateProcessArchitectureCommand),null,(body,execution)=>new(body.ArchitectureCode,body.Name,body.Description,body.SortOrder,execution));

    [HttpPut("architectures/{id:guid}")]
    public Task<IActionResult> UpdateArchitecture(Guid id)=>NoContent<UpdateArchitectureBody,UpdateProcessArchitectureCommand>(
        nameof(UpdateProcessArchitectureCommand),id,(body,execution)=>new(id,body.Name,body.Description,body.SortOrder,body.ExpectedVersion,execution));

    [HttpPost("architectures/{id:guid}/archive")]
    public Task<IActionResult> ArchiveArchitecture(Guid id)=>NoContent<ExpectedVersionBody,ArchiveProcessArchitectureCommand>(
        nameof(ArchiveProcessArchitectureCommand),id,(body,execution)=>new(id,body.ExpectedVersion,execution));

    [HttpPost("domains")]
    public Task<IActionResult> CreateDomain()=>Create<CreateDomainBody,CreateProcessDomainCommand>(
        nameof(CreateProcessDomainCommand),null,(body,execution)=>new(body.ProcessArchitectureId,body.DomainCode,body.Name,body.Description,body.SortOrder,execution));

    [HttpPut("domains/{id:guid}")]
    public Task<IActionResult> UpdateDomain(Guid id)=>NoContent<UpdateDomainBody,UpdateProcessDomainCommand>(
        nameof(UpdateProcessDomainCommand),id,(body,execution)=>new(id,body.Name,body.Description,body.SortOrder,body.ExpectedVersion,execution));

    [HttpPost("domains/{id:guid}/archive")]
    public Task<IActionResult> ArchiveDomain(Guid id)=>NoContent<ExpectedVersionBody,ArchiveProcessDomainCommand>(
        nameof(ArchiveProcessDomainCommand),id,(body,execution)=>new(id,body.ExpectedVersion,execution));

    [HttpPost("families")]
    public Task<IActionResult> CreateFamily()=>Create<CreateFamilyBody,CreateProcessFamilyCommand>(
        nameof(CreateProcessFamilyCommand),null,(body,execution)=>new(body.ProcessDomainId,body.FamilyCode,body.Name,body.Description,body.SortOrder,execution));

    [HttpPut("families/{id:guid}")]
    public Task<IActionResult> UpdateFamily(Guid id)=>NoContent<UpdateFamilyBody,UpdateProcessFamilyCommand>(
        nameof(UpdateProcessFamilyCommand),id,(body,execution)=>new(id,body.Name,body.Description,body.SortOrder,body.ExpectedVersion,execution));

    [HttpPost("families/{id:guid}/archive")]
    public Task<IActionResult> ArchiveFamily(Guid id)=>NoContent<ExpectedVersionBody,ArchiveProcessFamilyCommand>(
        nameof(ArchiveProcessFamilyCommand),id,(body,execution)=>new(id,body.ExpectedVersion,execution));

    [HttpPost("definitions")]
    public Task<IActionResult> CreateDefinition()=>Create<CreateDefinitionBody,CreateProcessDefinitionCommand>(
        nameof(CreateProcessDefinitionCommand),null,(body,execution)=>new(body.ProcessFamilyId,body.ProcessCode,body.Name,body.Purpose,body.Description,execution));

    [HttpPut("definitions/{id:guid}")]
    public Task<IActionResult> UpdateDefinition(Guid id)=>NoContent<UpdateDefinitionBody,UpdateProcessDefinitionCommand>(
        nameof(UpdateProcessDefinitionCommand),id,(body,execution)=>new(id,body.Name,body.Purpose,body.Description,body.ExpectedVersion,execution));

    [HttpPost("definitions/{id:guid}/archive")]
    public Task<IActionResult> ArchiveDefinition(Guid id)=>NoContent<ExpectedVersionBody,ArchiveProcessDefinitionCommand>(
        nameof(ArchiveProcessDefinitionCommand),id,(body,execution)=>new(id,body.ExpectedVersion,execution));

    [HttpPost("models")]
    public Task<IActionResult> CreateModel()=>Create<CreateModelBody,CreateProcessModelCommand>(
        nameof(CreateProcessModelCommand),null,(body,execution)=>new(body.ProcessDefinitionId,body.ModelCode,body.Name,body.Description,execution));

    [HttpPut("models/{id:guid}")]
    public Task<IActionResult> UpdateModel(Guid id)=>NoContent<UpdateModelBody,UpdateProcessModelCommand>(
        nameof(UpdateProcessModelCommand),id,(body,execution)=>new(id,body.Name,body.Description,body.ExpectedVersion,execution));

    [HttpPut("model-versions/{id:guid}/draft-content")]
    public Task<IActionResult> Draft(Guid id)=>NoContent<UpdateDraftBody,UpdateDraftProcessModelVersionCommand>(
        nameof(UpdateDraftProcessModelVersionCommand),id,(body,execution)=>new(id,body.Title,body.Description,body.Activities,body.ControlPoints,body.Relationships,body.ExpectedVersion,execution));

    [HttpPost("model-versions/{id:guid}/request-review")]
    public Task<IActionResult> RequestReview(Guid id)=>NoContent<ExpectedVersionBody,RequestProcessModelReviewCommand>(
        nameof(RequestProcessModelReviewCommand),id,(body,execution)=>new(id,body.ExpectedVersion,execution));

    [HttpPost("model-versions/{id:guid}/return-to-draft")]
    public Task<IActionResult> ReturnDraft(Guid id)=>NoContent<ExpectedVersionBody,ReturnProcessModelToDraftCommand>(
        nameof(ReturnProcessModelToDraftCommand),id,(body,execution)=>new(id,body.ExpectedVersion,execution));

    [HttpPost("model-versions/{id:guid}/publish")]
    public Task<IActionResult> Publish(Guid id)=>NoContent<ExpectedVersionBody,PublishProcessModelVersionCommand>(
        nameof(PublishProcessModelVersionCommand),id,(body,execution)=>new(id,body.ExpectedVersion,execution));

    [HttpPost("model-versions/{id:guid}/retire")]
    public Task<IActionResult> Retire(Guid id)=>NoContent<ExpectedVersionBody,RetireProcessModelVersionCommand>(
        nameof(RetireProcessModelVersionCommand),id,(body,execution)=>new(id,body.ExpectedVersion,execution));

    [HttpPost("models/{id:guid}/revisions")]
    public Task<IActionResult> Revision(Guid id)=>Create<CreateRevisionBody,CreateProcessModelRevisionCommand>(
        nameof(CreateProcessModelRevisionCommand),id,(body,execution)=>new(id,body.Title,body.Description,body.ExpectedVersion,execution));

    private async Task<IActionResult> Create<TBody,TCommand>(
        string commandName,
        Guid? routeId,
        Func<TBody,ProcessModelingLocalExecutionRequest,TCommand> commandFactory)
        where TCommand:IRequest<Response<Guid>>
    {
        var prepared=await Prepare<TBody>(commandName,routeId);
        if(prepared.Error is not null)return prepared.Error;
        return Result(await sender.Send(commandFactory(prepared.Body!,prepared.Execution!),HttpContext.RequestAborted));
    }

    private async Task<IActionResult> NoContent<TBody,TCommand>(
        string commandName,
        Guid? routeId,
        Func<TBody,ProcessModelingLocalExecutionRequest,TCommand> commandFactory)
        where TCommand:IRequest<Response<NoContent>>
    {
        var prepared=await Prepare<TBody>(commandName,routeId);
        if(prepared.Error is not null)return prepared.Error;
        return Result(await sender.Send(commandFactory(prepared.Body!,prepared.Execution!),HttpContext.RequestAborted));
    }

    private async Task<Prepared<TBody>> Prepare<TBody>(string commandName,Guid? routeId)
    {
        var raw=await ReadBody(Request,HttpContext.RequestAborted);
        if(!TryDeserializeExact(raw,out TBody? body))
            return Prepared<TBody>.Rejected(BadRequest(Response<NoContent>.Fail("process_modeling_bad_request",400)));
        if(!Guid.TryParse(User.FindFirstValue("tenant_id"),out var tenant)||!Guid.TryParse(User.FindFirstValue("sub"),out var actor)||!Guid.TryParse(User.FindFirstValue("service_principal_id"),out var servicePrincipalId))
            return Prepared<TBody>.Rejected(Unauthorized());
        if(!Request.Headers.TryGetValue("X-Tenant-Id",out var tenantHeader)||tenantHeader.Count!=1||!Guid.TryParse(tenantHeader[0],out var headerTenant)||headerTenant!=tenant)
            return Prepared<TBody>.Rejected(BadRequest(Response<NoContent>.Fail("process_modeling_bad_request",400)));
        if(!Request.Headers.TryGetValue("Idempotency-Key",out var key)||key.Count!=1||string.IsNullOrEmpty(key[0]))
            return Prepared<TBody>.Rejected(BadRequest(Response<NoContent>.Fail("idempotency_key_required",400)));
        var permission=ProcessModelingPermissions.ExactCommandMap[commandName];
        if(!User.FindAll("permission").Select(x=>x.Value).Contains(permission,StringComparer.Ordinal))
            return Prepared<TBody>.Rejected(StatusCode(403,Response<NoContent>.Fail("process_model_permission_denied",403)));
        var path=Request.Path.Value??string.Empty;
        var hash=ProcessModelingLocalRequestHash.Compute(Request.Method,path,commandName,tenant,actor,permission,raw);
        var execution=new ProcessModelingLocalExecutionRequest(commandName,routeId,raw,new(tenant,actor,permission,key[0]!,hash,servicePrincipalId));
        return Prepared<TBody>.Accepted(body!,execution);
    }

    private (Guid TenantId,IActionResult? Error) Identity()
    {
        if(!Guid.TryParse(User.FindFirstValue("tenant_id"),out var tenant)||tenant==Guid.Empty)return (Guid.Empty,Unauthorized());
        if(!Request.Headers.TryGetValue("X-Tenant-Id",out var header)||header.Count!=1||!Guid.TryParse(header[0],out var headerTenant)||headerTenant!=tenant)
            return (Guid.Empty,Result(Response<NoContent>.Fail("process_modeling_bad_request",400)));
        return (tenant,null);
    }
    private bool HasPermission(string permission)=>User.FindAll("permission").Any(x=>string.Equals(x.Value,permission,StringComparison.Ordinal));

    private static async Task<byte[]> ReadBody(HttpRequest request,CancellationToken cancellationToken)
    {
        using var stream=new MemoryStream();
        await request.Body.CopyToAsync(stream,cancellationToken);
        return stream.ToArray();
    }

    private static bool TryDeserializeExact<TBody>(byte[] raw,out TBody? body)
    {
        body=default;
        try
        {
            using var document=JsonDocument.Parse(raw);
            if(document.RootElement.ValueKind!=JsonValueKind.Object||HasDuplicateProperties(document.RootElement))return false;
            body=JsonSerializer.Deserialize<TBody>(raw,StrictJson);
            return body is not null;
        }
        catch(JsonException){return false;}
    }

    private static bool HasDuplicateProperties(JsonElement element)
    {
        if(element.ValueKind==JsonValueKind.Object)
        {
            var names=new HashSet<string>(StringComparer.Ordinal);
            foreach(var property in element.EnumerateObject())
                if(!names.Add(property.Name)||HasDuplicateProperties(property.Value))return true;
        }
        else if(element.ValueKind==JsonValueKind.Array)
        {
            foreach(var item in element.EnumerateArray())
                if(HasDuplicateProperties(item))return true;
        }
        return false;
    }

    private sealed record Prepared<TBody>(TBody? Body,ProcessModelingLocalExecutionRequest? Execution,IActionResult? Error)
    {
        internal static Prepared<TBody> Accepted(TBody body,ProcessModelingLocalExecutionRequest execution)=>new(body,execution,null);
        internal static Prepared<TBody> Rejected(IActionResult error)=>new(default,null,error);
    }

    private sealed record CreateArchitectureBody(
        [property:JsonRequired] string ArchitectureCode,
        [property:JsonRequired] string Name,
        string? Description,
        [property:JsonRequired] int SortOrder);
    private sealed record UpdateArchitectureBody([property:JsonRequired] string Name,string? Description,[property:JsonRequired] int SortOrder,[property:JsonRequired] int ExpectedVersion);
    private sealed record ExpectedVersionBody([property:JsonRequired] int ExpectedVersion);
    private sealed record CreateDomainBody([property:JsonRequired] Guid ProcessArchitectureId,[property:JsonRequired] string DomainCode,[property:JsonRequired] string Name,string? Description,[property:JsonRequired] int SortOrder);
    private sealed record UpdateDomainBody([property:JsonRequired] string Name,string? Description,[property:JsonRequired] int SortOrder,[property:JsonRequired] int ExpectedVersion);
    private sealed record CreateFamilyBody([property:JsonRequired] Guid ProcessDomainId,[property:JsonRequired] string FamilyCode,[property:JsonRequired] string Name,string? Description,[property:JsonRequired] int SortOrder);
    private sealed record UpdateFamilyBody([property:JsonRequired] string Name,string? Description,[property:JsonRequired] int SortOrder,[property:JsonRequired] int ExpectedVersion);
    private sealed record CreateDefinitionBody([property:JsonRequired] Guid ProcessFamilyId,[property:JsonRequired] string ProcessCode,[property:JsonRequired] string Name,string? Purpose,string? Description);
    private sealed record UpdateDefinitionBody([property:JsonRequired] string Name,string? Purpose,string? Description,[property:JsonRequired] int ExpectedVersion);
    private sealed record CreateModelBody([property:JsonRequired] Guid ProcessDefinitionId,[property:JsonRequired] string ModelCode,[property:JsonRequired] string Name,string? Description);
    private sealed record UpdateModelBody([property:JsonRequired] string Name,string? Description,[property:JsonRequired] int ExpectedVersion);
    private sealed record UpdateDraftBody(
        [property:JsonRequired] string Title,
        string? Description,
        [property:JsonRequired] IReadOnlyList<ProcessModelActivityInput> Activities,
        [property:JsonRequired] IReadOnlyList<ProcessModelControlPointInput> ControlPoints,
        [property:JsonRequired] IReadOnlyList<ProcessModelRelationshipInput> Relationships,
        [property:JsonRequired] int ExpectedVersion);
    private sealed record CreateRevisionBody([property:JsonRequired] string Title,string? Description,[property:JsonRequired] int ExpectedVersion);
}
