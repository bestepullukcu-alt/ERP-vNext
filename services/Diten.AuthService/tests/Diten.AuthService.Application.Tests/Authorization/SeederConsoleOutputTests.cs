using Diten.AuthService.Application.Tests.Testing;
using Diten.AuthService.Persistence.Seed;

namespace Diten.AuthService.Application.Tests.Authorization;

/// <summary>
/// FIX4 item 5 — the seeder's own catch logs the exception type only. Moved out of ExportGrantBackfillScenarioTests
/// (2026-11-03): it redirects the process-wide Console, so it runs in the console-capture collection, never in
/// parallel with the e-sign log test or any other class.
/// </summary>
[Trait("Category", "Mongo")]
[Collection(Diten.AuthService.Application.Tests.ServiceIdentityTokens.ConsoleCaptureCollection.Name)]
public sealed class SeederConsoleOutputTests : IClassFixture<AccountKindAcceptance.AuthTestHost>
{
    private readonly AccountKindAcceptance.AuthTestHost _host;

    public SeederConsoleOutputTests(AccountKindAcceptance.AuthTestHost host) => _host = host;

    [Fact]
    public async Task The_seeders_catch_writes_the_exception_type_and_never_its_message()
    {
        var output = new StringWriter();
        var original = Console.Out;
        Console.SetOut(output);
        try
        {
            await DataSeeder.SeedAsync(_host.Database, null,
                () => throw new InvalidOperationException("SECRET-outer", new FormatException("SECRET-inner")));
        }
        finally
        {
            Console.SetOut(original);
        }

        var text = output.ToString();
        Assert.Contains("Critical Seeding Error: " + typeof(InvalidOperationException).FullName, text);
        Assert.Contains("Inner: " + typeof(FormatException).FullName, text);
        Assert.DoesNotContain("SECRET", text);
    }
}
