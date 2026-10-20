using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Diten.BuildingBlocks.Auditing.Tests;

/// <summary>
/// The forwarder sends Platform's enums as integers; Platform maps them by value. A value added on one side only would
/// file a record under the wrong category (or be refused as Unknown). Compared with Platform's SOURCE, name by name.
/// </summary>
public sealed class AuditEnumAlignmentTests
{
    private const string PlatformEnums = "services/Diten.Platform/src/Diten.Platform.Domain/Enums/AuditEnums.cs";

    public static TheoryData<string, Type> Enums() => new()
    {
        { "AuditCategory", typeof(AuditCategory) },
        { "AuditOperation", typeof(AuditOperation) },
        { "AuditOutcome", typeof(AuditOutcome) },
        { "AuditActorType", typeof(AuditActorType) }
    };

    [Theory]
    [MemberData(nameof(Enums))]
    public void The_shared_enum_matches_platform_name_by_name_and_value_by_value(string platformName, Type shared)
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), PlatformEnums));
        var block = Regex.Match(source, $@"public enum {platformName}\s*\{{(?<body>[^}}]*)\}}");
        Assert.True(block.Success, $"{platformName} not found in {PlatformEnums}");

        var platform = Regex.Matches(block.Groups["body"].Value, @"^\s*(\w+)\s*=\s*(\d+)", RegexOptions.Multiline)
            .ToDictionary(m => m.Groups[1].Value, m => int.Parse(m.Groups[2].Value));
        var ours = Enum.GetValues(shared).Cast<object>().ToDictionary(v => v.ToString()!, v => (int)v);

        Assert.Equal(platform.OrderBy(p => p.Value), ours.OrderBy(p => p.Value));
    }

    [Fact]
    public void The_default_caller_reads_the_token_and_the_correlation_header()
    {
        var tenant = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var correlation = Guid.NewGuid();
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("sub", actor.ToString()), new Claim("tenant_id", tenant.ToString()),
                new Claim("actor_type", "tenant_user"), new Claim("email", "rep@example.test"), new Claim("name", "Rep")
            ], "test"))
        };
        context.Request.Headers[HttpAuditCallContext.CorrelationHeader] = correlation.ToString();

        var caller = new HttpAuditCallContext(new HttpContextAccessor { HttpContext = context }).Resolve();

        Assert.Equal(new AuditCaller(tenant, AuditActorType.TenantUser, actor, "rep@example.test", "Rep", correlation), caller);
    }

    [Fact]
    public void Without_a_request_the_caller_is_the_system_with_no_tenant()
    {
        var caller = new HttpAuditCallContext(new HttpContextAccessor()).Resolve();

        Assert.Equal(new AuditCaller(null, AuditActorType.System, null, null, null, null), caller);
    }

    private static string RepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new InvalidOperationException("Repo root not found (no AGENTS.md above the test binary).");
    }
}
