using Diten.Platform.API.Middleware;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Application.Tests.Tenants;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.Platform.Application.Tests.Middleware;

/// <summary>
/// BL-521 FIX3 G2 — the 503 answers FIX2 added (an unreadable administrator record, the job dashboard's record read) are
/// 503 and not 500 only because the production exception handler runs BEFORE authentication, authorization and the job
/// dashboard: an exception thrown inside those reaches <c>GlobalExceptionHandler</c> only if it sits above them. No test
/// runs the production pipeline itself (BL-535), so this file pins the two halves that can be pinned without it: the ORDER
/// in Program.cs (text, comments ignored), and that the HTTP test host the 503 mappings are measured on maps exceptions
/// with the PRODUCTION handler — not a copy of its own.
/// </summary>
[Collection(DisposableMongoReplicaSetCollection.Name)]
[Trait("Category", "Mongo")]
public sealed class PlatformExceptionPipelineOrderTests
{
    private const string ExceptionHandler = "app.UseExceptionHandler();";

    // Merge WP-CT-LANE-BL521-MERGE-01 — the lane's BL-512 FIX1 moved UseAuthentication / UseAuthorization out of Program.cs into
    // ONE call, app.UsePlatformAccessPipeline() (Security/PlatformAccessPipeline.cs). The order this pins is the same: the
    // exception handler sits above that call; the test below pins that authentication and authorization are inside it.
    [Theory]
    [InlineData("app.UsePlatformAccessPipeline();")]
    [InlineData("app.UsePlatformHangfireDashboard(")]
    public void Program_maps_exceptions_before_authentication_authorization_and_the_job_dashboard(string later)
    {
        var program = ProgramCode();

        var handler = IndexOfOnly(program, ExceptionHandler);
        var laterIndex = IndexOfOnly(program, later);

        Assert.True(handler < laterIndex, $"Program.cs calls '{later}' before '{ExceptionHandler}': an exception thrown there becomes a 500.");
    }

    [Fact]
    public void Program_registers_the_production_exception_handler_and_problem_details()
    {
        var program = ProgramCode();

        IndexOfOnly(program, "builder.Services.AddExceptionHandler<Diten.Platform.API.Middleware.GlobalExceptionHandler>();");
        IndexOfOnly(program, "builder.Services.AddProblemDetails();");
    }

    [Fact]
    public async Task The_http_test_host_of_the_503_mappings_uses_the_production_exception_handler()
    {
        await using var host = await PartnerAdminScopeHttpMongoTests.Host.StartAsync();

        var handlers = host.Services.GetServices<IExceptionHandler>().ToList();

        Assert.IsType<GlobalExceptionHandler>(Assert.Single(handlers));
    }

    /// <summary>Merge WP-CT-LANE-BL521-MERGE-01 — what the order above stands for: authentication and authorization are run by
    /// the access pipeline Program.cs calls after the exception handler, and Program.cs does not call them anywhere else.</summary>
    [Fact]
    public void Authentication_and_authorization_run_inside_the_access_pipeline_and_nowhere_else_in_Program()
    {
        var pipelinePath = Path.Combine(RepositoryRoot(), "services", "Diten.Platform", "src", "Diten.Platform.API", "Security", "PlatformAccessPipeline.cs");
        var pipeline = string.Join('\n', File.ReadAllLines(pipelinePath).Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));
        var authentication = pipeline.IndexOf("app.UseAuthentication();", StringComparison.Ordinal);
        var authorization = pipeline.IndexOf("app.UseAuthorization();", StringComparison.Ordinal);

        Assert.True(authentication >= 0 && authorization > authentication, "PlatformAccessPipeline no longer runs authentication, then authorization.");
        var program = ProgramCode();
        Assert.DoesNotContain("app.UseAuthentication();", program, StringComparison.Ordinal);
        Assert.DoesNotContain("app.UseAuthorization();", program, StringComparison.Ordinal);
    }

    /// <summary>Program.cs without its comment lines — a commented-out call is not a call.</summary>
    private static string ProgramCode()
    {
        var path = Path.Combine(RepositoryRoot(), "services", "Diten.Platform", "src", "Diten.Platform.API", "Program.cs");
        return string.Join('\n', File.ReadAllLines(path).Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));
    }

    private static int IndexOfOnly(string text, string fragment)
    {
        var first = text.IndexOf(fragment, StringComparison.Ordinal);
        Assert.True(first >= 0, $"Program.cs no longer contains '{fragment}'.");
        Assert.True(text.IndexOf(fragment, first + fragment.Length, StringComparison.Ordinal) < 0, $"Program.cs contains '{fragment}' more than once.");
        return first;
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("repository_root_not_found");
    }
}
