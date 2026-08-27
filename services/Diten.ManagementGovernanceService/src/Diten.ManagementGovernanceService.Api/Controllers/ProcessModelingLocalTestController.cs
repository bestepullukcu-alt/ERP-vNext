using System.Security.Claims;
using System.Text.Json;
using Diten.ManagementGovernanceService.Api.LocalTestSecurity;
using Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest;
using Diten.ManagementGovernanceService.Application.Modules.ProcessModeling;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.ManagementGovernanceService.Api.Controllers;

[Route("internal/local-test/v1/process-modeling")]
[Authorize(AuthenticationSchemes=LocalTestAuthenticationHandler.SchemeName)]
public sealed class ProcessModelingLocalTestController(ISender sender):CustomBaseController
{
    [AllowAnonymous,HttpGet("health")] public IActionResult Health()=>Ok(new{status="healthy"});
    [HttpPost("architectures")] public Task<IActionResult> CreateArchitecture()=>Run("CreateProcessArchitectureCommand",null,true,["architectureCode","name","description?","sortOrder"]);
    [HttpPut("architectures/{id:guid}")] public Task<IActionResult> UpdateArchitecture(Guid id)=>Run("UpdateProcessArchitectureCommand",id,false,["name","description?","sortOrder","expectedVersion"]);
    [HttpPost("architectures/{id:guid}/archive")] public Task<IActionResult> ArchiveArchitecture(Guid id)=>Run("ArchiveProcessArchitectureCommand",id,false,["expectedVersion"]);
    [HttpPost("domains")] public Task<IActionResult> CreateDomain()=>Run("CreateProcessDomainCommand",null,true,["processArchitectureId","domainCode","name","description?","sortOrder"]);
    [HttpPut("domains/{id:guid}")] public Task<IActionResult> UpdateDomain(Guid id)=>Run("UpdateProcessDomainCommand",id,false,["name","description?","sortOrder","expectedVersion"]);
    [HttpPost("domains/{id:guid}/archive")] public Task<IActionResult> ArchiveDomain(Guid id)=>Run("ArchiveProcessDomainCommand",id,false,["expectedVersion"]);
    [HttpPost("families")] public Task<IActionResult> CreateFamily()=>Run("CreateProcessFamilyCommand",null,true,["processDomainId","familyCode","name","description?","sortOrder"]);
    [HttpPut("families/{id:guid}")] public Task<IActionResult> UpdateFamily(Guid id)=>Run("UpdateProcessFamilyCommand",id,false,["name","description?","sortOrder","expectedVersion"]);
    [HttpPost("families/{id:guid}/archive")] public Task<IActionResult> ArchiveFamily(Guid id)=>Run("ArchiveProcessFamilyCommand",id,false,["expectedVersion"]);
    [HttpPost("definitions")] public Task<IActionResult> CreateDefinition()=>Run("CreateProcessDefinitionCommand",null,true,["processFamilyId","processCode","name","purpose?","description?"]);
    [HttpPut("definitions/{id:guid}")] public Task<IActionResult> UpdateDefinition(Guid id)=>Run("UpdateProcessDefinitionCommand",id,false,["name","purpose?","description?","expectedVersion"]);
    [HttpPost("definitions/{id:guid}/archive")] public Task<IActionResult> ArchiveDefinition(Guid id)=>Run("ArchiveProcessDefinitionCommand",id,false,["expectedVersion"]);
    [HttpPost("models")] public Task<IActionResult> CreateModel()=>Run("CreateProcessModelCommand",null,true,["processDefinitionId","modelCode","name","description?"]);
    [HttpPut("models/{id:guid}")] public Task<IActionResult> UpdateModel(Guid id)=>Run("UpdateProcessModelCommand",id,false,["name","description?","expectedVersion"]);
    [HttpPut("model-versions/{id:guid}/draft-content")] public Task<IActionResult> Draft(Guid id)=>Run("UpdateDraftProcessModelVersionCommand",id,false,["title","description?","activities","controlPoints","relationships","expectedVersion"]);
    [HttpPost("model-versions/{id:guid}/request-review")] public Task<IActionResult> RequestReview(Guid id)=>Run("RequestProcessModelReviewCommand",id,false,["expectedVersion"]);
    [HttpPost("model-versions/{id:guid}/return-to-draft")] public Task<IActionResult> ReturnDraft(Guid id)=>Run("ReturnProcessModelToDraftCommand",id,false,["expectedVersion"]);
    [HttpPost("model-versions/{id:guid}/publish")] public Task<IActionResult> Publish(Guid id)=>Run("PublishProcessModelVersionCommand",id,false,["expectedVersion"]);
    [HttpPost("model-versions/{id:guid}/retire")] public Task<IActionResult> Retire(Guid id)=>Run("RetireProcessModelVersionCommand",id,false,["expectedVersion"]);
    [HttpPost("models/{id:guid}/revisions")] public Task<IActionResult> Revision(Guid id)=>Run("CreateProcessModelRevisionCommand",id,true,["title","description?","expectedVersion"]);

    private async Task<IActionResult> Run(string command,Guid? routeId,bool create,string[] fields)
    {
        var raw=await ReadBody(Request,HttpContext.RequestAborted); if(!ExactJson(raw,fields))return BadRequest(Response<object>.Fail("process_modeling_bad_request",400));
        if(!Guid.TryParse(User.FindFirstValue("tenant_id"),out var tenant)||!Guid.TryParse(User.FindFirstValue("sub"),out var actor))return Unauthorized();
        if(!Request.Headers.TryGetValue("X-Tenant-Id",out var tenantHeader)||tenantHeader.Count!=1||!Guid.TryParse(tenantHeader[0],out var headerTenant)||headerTenant!=tenant)return NotFound();
        if(!Request.Headers.TryGetValue("Idempotency-Key",out var key)||key.Count!=1||string.IsNullOrEmpty(key[0]))return BadRequest(Response<object>.Fail("idempotency_key_required",400));
        var permission=ProcessModelingPermissions.ExactCommandMap[command]; if(!User.FindAll("permission").Select(x=>x.Value).Contains(permission,StringComparer.Ordinal))return StatusCode(403,Response<object>.Fail("process_model_permission_denied",403));
        var path=Request.Path.Value??string.Empty;var hash=ProcessModelingLocalRequestHash.Compute(Request.Method,path,command,tenant,actor,permission,raw);
        var result=await sender.Send(new ProcessModelingLocalCommand(command,routeId,raw,new(tenant,actor,permission,key[0]!,hash)),HttpContext.RequestAborted);
        return Result(result,create);
    }
    private static async Task<byte[]> ReadBody(HttpRequest request,CancellationToken ct){using var ms=new MemoryStream();await request.Body.CopyToAsync(ms,ct);return ms.ToArray();}
    private static bool ExactJson(byte[] raw,string[] declared)
    {
        try
        {
            using var document=JsonDocument.Parse(raw);if(document.RootElement.ValueKind!=JsonValueKind.Object)return false;
            var allowed=declared.Select(x=>x.TrimEnd('?')).ToHashSet(StringComparer.Ordinal);var required=declared.Where(x=>!x.EndsWith('?')).ToHashSet(StringComparer.Ordinal);var seen=new HashSet<string>(StringComparer.Ordinal);
            foreach(var p in document.RootElement.EnumerateObject())if(!allowed.Contains(p.Name)||!seen.Add(p.Name)||p.Name is "tenantId" or "actorId" or "permission" or "requestHash" or "commandName")return false;
            return required.IsSubsetOf(seen);
        }
        catch(JsonException){return false;}
    }
}
