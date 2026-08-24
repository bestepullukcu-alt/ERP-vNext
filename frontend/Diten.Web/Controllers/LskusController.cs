using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Diten.Web.Models.Lskus;
using Diten.Web.Security;
using Diten.Web.Views.MasterDataManagement.Lskus;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

[Authorize]
[Route("MasterDataManagement/Lskus")]
public sealed class LskusController : Controller
{
    private const string ReadPermission = "mdm.lskus.read";
    private const string CreatePermission = "mdm.lskus.create";
    private readonly HttpClient _http; private readonly string _gateway; private readonly ITimeLimitedDataProtector _protector; private readonly IStringLocalizer<LskusIndex> _l10n;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
    public LskusController(HttpClient http, IConfiguration config, IDataProtectionProvider protection, IStringLocalizer<LskusIndex> l10n) { _http=http; _gateway=(config["GatewayUrl"] ?? throw new InvalidOperationException("GatewayUrl configuration is required.")).TrimEnd('/'); _protector=protection.CreateProtector("Diten.Web","MOD-0290","LskuFormAttempt","v1").ToTimeLimitedDataProtector(); _l10n=l10n; }
    [HttpGet("")] public IActionResult Index() { if (!Has(ReadPermission)) return Forbid(); var create=Has(CreatePermission); ViewData["CanCreateLsku"]=create; if(create) ViewData["LskuFormAttemptToken"]=NewToken(); return View("~/Views/MasterDataManagement/Lskus/Index.cshtml"); }
    [HttpGet("api")] public Task<IActionResult> List(CancellationToken ct) => Has(ReadPermission) ? Proxy(HttpMethod.Get,$"{_gateway}/api/lskus{Request.QueryString}",ct) : Task.FromResult<IActionResult>(Forbid());
    [HttpGet("api/{id:guid}")] public Task<IActionResult> Detail(Guid id,CancellationToken ct) => Has(ReadPermission) ? Proxy(HttpMethod.Get,$"{_gateway}/api/lskus/{id:D}",ct) : Task.FromResult<IActionResult>(Forbid());
    [HttpGet("api/create-options")] public Task<IActionResult> CreateOptions(CancellationToken ct) => Has(CreatePermission) ? Proxy(HttpMethod.Get,$"{_gateway}/api/lskus/create-options",ct) : Task.FromResult<IActionResult>(Forbid());
    [HttpPost("api")][ValidateAntiForgeryToken] public async Task<IActionResult> Create([FromForm] CreateLskuViewModel model,[FromForm] string? formAttemptToken,CancellationToken ct) { if(!Has(CreatePermission)) return Forbid(); if(!TryToken(formAttemptToken,out var key)) return BadRequest(new { success=false, errors=new[]{_l10n["ErrorInvalidFormAttempt"].Value} }); if(!ModelState.IsValid || model.GskuId==Guid.Empty || string.IsNullOrWhiteSpace(model.MarketCode)) return BadRequest(new {success=false,errors=new[]{_l10n["ErrorValidation"].Value}}); if(!RequestMessage(HttpMethod.Post,$"{_gateway}/api/lskus/drafts",JsonContent.Create(new {gskuId=model.GskuId,marketCode=model.MarketCode.Trim()},options:_json),out var req)) return Unauthorized(); req.Headers.TryAddWithoutValidation("Idempotency-Key",key); using(req) using(var res=await _http.SendAsync(req,ct)) { if(res.StatusCode==HttpStatusCode.Accepted) return StatusCode(202,new {success=false,errors=new[]{_l10n["CreateReconciliationPending"].Value},formAttemptToken}); if(res.StatusCode==HttpStatusCode.Created) { var e=await res.Content.ReadFromJsonAsync<LskuGatewayResponse<LskuDraftViewModel>>(_json,ct); if(e?.IsSuccessful==true && e.Data is not null) return StatusCode(201,new {success=true,data=e.Data,formAttemptToken=NewToken()}); } return Failure(res.StatusCode); } }
    private async Task<IActionResult> Proxy(HttpMethod method,string url,CancellationToken ct) { if(!RequestMessage(method,url,null,out var req)) return Unauthorized(); using(req) using(var res=await _http.SendAsync(req,ct)) { if(!res.IsSuccessStatusCode) return Failure(res.StatusCode); return Content(await res.Content.ReadAsStringAsync(ct),res.Content.Headers.ContentType?.ToString() ?? "application/json"); } }
    private bool RequestMessage(HttpMethod method,string url,HttpContent? content,out HttpRequestMessage request) { request=new(method,url){Content=content}; var token=Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request); if(!string.IsNullOrWhiteSpace(token)) request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token); var tenant=User.Claims.FirstOrDefault(x=>x.Type is "tenantId" or "tenant_id" || x.Type.EndsWith("/tenantId",StringComparison.OrdinalIgnoreCase))?.Value; if(!Guid.TryParse(tenant,out var id)){request.Dispose();request=null!;return false;} request.Headers.Add("X-Tenant-Id",id.ToString("D"));return true; }
    private IActionResult Failure(HttpStatusCode code) { var status=(int)code; var key=status switch {404=>"ErrorNotFound",409=>"ErrorConflict",503=>"ErrorProviderUnavailable",504=>"ErrorProviderTimeout",403=>"ErrorForbidden",_=>"ErrorGateway"}; return StatusCode(status,new {success=false,errors=new[]{_l10n[key].Value}}); }
    private string NewToken(){var p=new Attempt(Subject(),Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));return _protector.Protect(JsonSerializer.Serialize(p,_json),TimeSpan.FromMinutes(30));}
    private bool TryToken(string? token,out string key){key="";try { if(string.IsNullOrWhiteSpace(token))return false; var p=JsonSerializer.Deserialize<Attempt>(_protector.Unprotect(token,out _),_json); if(p is null || p.Subject!=Subject() || string.IsNullOrWhiteSpace(p.Key))return false;key=p.Key;return true;}catch(CryptographicException){return false;}catch(JsonException){return false;}}
    private bool Has(string key)=>PermissionClaims.HasPermission(User,key); private string Subject()=>User.FindFirstValue(ClaimTypes.NameIdentifier)??User.FindFirstValue("sub")??User.Identity?.Name??""; private sealed record Attempt(string Subject,string Key);
}
