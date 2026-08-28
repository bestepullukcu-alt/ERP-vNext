using Diten.ManagementGovernanceService.Application.Modules.ProcessModeling;
using Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest.Commands;
using Diten.ManagementGovernanceService.Persistence.Modules.ProcessModeling;
using MongoDB.Bson;

namespace Diten.ManagementGovernanceService.ArchitectureTests.Modules.ProcessModeling.LocalTest;

public sealed class ProcessModelingMongoGuidPolicyArchitectureTests
{
    [Fact]
    public void Canonical_policy_is_standard_and_exact_command_dispatch_is_closed()
    {
        Assert.Equal(GuidRepresentation.Standard,ProcessModelingMongoGuidPolicy.Canonical);
        Assert.Equal(20,ProcessModelingPermissions.ExactCommandMap.Count);
        Assert.All(ProcessModelingPermissions.ExactCommandMap.Keys,command=>
        {
            Assert.Contains(ProcessModelingCommandContract.BusinessCollection(command),ProcessModelingPersistenceManifest.Collections);
            var audit=ProcessModelingCommandContract.Audit(command);
            Assert.False(string.IsNullOrWhiteSpace(audit.Entity)); Assert.False(string.IsNullOrWhiteSpace(audit.Mutation));
        });
        Assert.Throws<InvalidOperationException>(()=>ProcessModelingCommandContract.BusinessCollection("CreateProcessArchitectureCommand "));
    }

    [Fact]
    public void Raw_uuid_is_subtype_four_and_round_trips_with_one_policy()
    {
        var id=Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
        var raw=ProcessModelingMongoGuidPolicy.Write(id);
        Assert.Equal(BsonBinarySubType.UuidStandard,raw.SubType);
        Assert.Equal("00112233445566778899AABBCCDDEEFF",Convert.ToHexString(raw.Bytes));
        Assert.Equal(id,ProcessModelingMongoGuidPolicy.Read(raw));
    }

    [Fact]
    public void Local_http_surface_is_default_off_loopback_only_and_exact_twenty()
    {
        var root=ServiceRoot();
        var program=File.ReadAllText(Path.Combine(root,"src","Diten.ManagementGovernanceService.Api","Program.cs"));
        var controller=File.ReadAllText(Path.Combine(root,"src","Diten.ManagementGovernanceService.Api","Controllers","ProcessModelingLocalTestController.cs"));
        var auth=File.ReadAllText(Path.Combine(root,"src","Diten.ManagementGovernanceService.Api","LocalTestSecurity","LocalTestAuthenticationHandler.cs"));
        Assert.Contains("--local-test",program,StringComparison.Ordinal);
        Assert.Contains("http://127.0.0.1:5017",program,StringComparison.Ordinal);
        Assert.DoesNotContain("ILocalTestIdentitySource",program,StringComparison.Ordinal);
        var commandTypes=typeof(CreateProcessArchitectureCommand).Assembly.GetTypes()
            .Where(x=>x.Namespace==typeof(CreateProcessArchitectureCommand).Namespace&&x.Name.EndsWith("Command",StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(20,commandTypes.Length);
        Assert.Equal(20,ProcessModelingPermissions.ExactCommandMap.Keys.Count(command=>controller.Contains($"nameof({command})",StringComparison.Ordinal)));
        Assert.DoesNotContain("ProcessModelingLocalCommand",controller,StringComparison.Ordinal);
        Assert.Contains("X-Tenant-Id",controller,StringComparison.Ordinal); Assert.Contains("Idempotency-Key",controller,StringComparison.Ordinal);
        Assert.Contains("Diten.MOD0355.LocalTest",auth,StringComparison.Ordinal);Assert.Contains("mod0355-local-evidence-v1",auth,StringComparison.Ordinal);
        Assert.DoesNotContain("RabbitMQ",program+controller+auth,StringComparison.OrdinalIgnoreCase);
    }

    private static string ServiceRoot()
    {
        var cursor=new DirectoryInfo(AppContext.BaseDirectory);
        while(cursor is not null&&!string.Equals(cursor.Name,"Diten.ManagementGovernanceService",StringComparison.Ordinal))cursor=cursor.Parent;
        return cursor?.FullName??throw new InvalidOperationException("service_root_missing");
    }
}
