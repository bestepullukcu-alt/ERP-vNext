using System.Text.RegularExpressions;
using Xunit;

namespace Diten.AuthService.Application.Tests.Testing;

/// <summary>
/// CT acceptance (2026-10-01). <c>AuthTestSerializers</c> registers "ignore extra elements" for every type at test
/// assembly load, so that a class map frozen by an early test has the production shape. That makes the test process
/// MORE tolerant than a production process that forgot the convention — it could mask exactly that defect. This pins
/// the production side: each of the three hosts that talk to Mongo registers the convention itself, and does so BEFORE
/// it builds its Mongo client. Lexical on purpose (the registry is process-global and cannot be asserted per host
/// from inside this process, which has already registered its own pack).
/// </summary>
public sealed class AuthTestSerializersPinsProductionTests
{
    public static TheoryData<string, string, string> ProductionRegistrations => new()
    {
        // file (under services/Diten.AuthService/src), the method that registers, the first Mongo use it must precede
        { "Diten.AuthService.Persistence/DependencyInjection.cs", "AddPersistence", "new MongoClient(" },
        { "Diten.AuthService.Persistence/DependencyInjection.cs", "AddEntitlementReconciliationPersistence", "new MongoClient(" },
        { "Diten.AuthService.Persistence/ServiceClientOperationalPersistenceRegistration.cs", "", "new MongoClient(" }
    };

    [Theory]
    [MemberData(nameof(ProductionRegistrations))]
    public void Each_production_host_registers_ignore_extra_elements_before_it_builds_its_Mongo_client(
        string file, string method, string firstMongoUse)
    {
        var source = File.ReadAllText(Path.Combine(SourceRoot(), file.Replace('/', Path.DirectorySeparatorChar)));
        var body = method.Length == 0 ? source : MethodBody(source, method);

        var registration = Regex.Match(body, @"ConventionRegistry\s*\.\s*Register\s*\(");
        Assert.True(registration.Success, $"{file} {method}: the production host no longer registers a convention pack.");
        Assert.Matches(@"new\s+IgnoreExtraElementsConvention\s*\(\s*true\s*\)", body);

        var mongoUse = body.IndexOf(firstMongoUse, StringComparison.Ordinal);
        Assert.True(mongoUse >= 0, $"{file} {method}: '{firstMongoUse}' not found — the pin no longer sees the Mongo client.");
        Assert.True(registration.Index < mongoUse,
            $"{file} {method}: the convention is registered AFTER the Mongo client is built; a class map frozen in "
            + "between would not carry it.");
    }

    // The method's text: from its signature to the next method signature at the same indentation (or the end).
    private static string MethodBody(string source, string method)
    {
        var start = Regex.Match(source, @"public\s+static\s+IServiceCollection\s+" + Regex.Escape(method) + @"\s*\(");
        Assert.True(start.Success, $"method {method} not found");
        var next = Regex.Match(source[(start.Index + start.Length)..], @"\n    (public|private|internal)\s+static\s");
        return next.Success ? source.Substring(start.Index, start.Length + next.Index) : source[start.Index..];
    }

    private static string SourceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(directory!.FullName, "services", "Diten.AuthService", "src");
    }
}
