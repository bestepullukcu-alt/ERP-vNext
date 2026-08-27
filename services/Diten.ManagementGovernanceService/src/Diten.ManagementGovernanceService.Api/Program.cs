using System.Text.Json.Serialization;
using Diten.ManagementGovernanceService.Api.LocalTestSecurity;
using Diten.ManagementGovernanceService.Application;
using Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest;
using Microsoft.AspNetCore.Authentication;

if (!args.Contains("--local-test",StringComparer.Ordinal)) return;

var builder=WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:5017");
builder.Services.AddManagementGovernanceApplication();
builder.Services.AddSingleton<IProcessModelingLocalCommandExecutor,DefaultUnavailableProcessModelingLocalCommandExecutor>();
builder.Services.AddAuthentication(LocalTestAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions,LocalTestAuthenticationHandler>(LocalTestAuthenticationHandler.SchemeName,_=>{});
builder.Services.AddAuthorization();
builder.Services.AddControllers().AddJsonOptions(x=>
{
    x.JsonSerializerOptions.PropertyNameCaseInsensitive=false;
    x.JsonSerializerOptions.UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow;
});
var app=builder.Build();
app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
await app.RunAsync();

public partial class Program;
