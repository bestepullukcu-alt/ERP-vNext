using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Diten.PvgService.Application.RegPvBase;
using Diten.PvgService.Domain.RegPvBase;
using Diten.PvgService.Infrastructure.RegPvBase;
using Diten.PvgService.Persistence;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Diten.PvgService.Api.Tests.Persistence;

// WP-0230-DL10, AC-13 (a), (c) last test, (e), (f) hosted services, AC-35 (f): guards that read source text, project
// files and assembly references. None of them needs a mongod.
public sealed class PvgPersistenceBoundaryScanTests
{
    private const string PersistenceProjectFile = "Diten.PvgService.Persistence.csproj";

    [Fact]
    public void Only_the_persistence_project_references_the_Mongo_driver()
    {
        var root = PvgRepositoryRoot.Find();
        var sourceRoot = Path.Combine(root, "services", "Diten.PvgService", "src");
        var projectFiles = PvgRepositoryRoot.ProjectFiles(sourceRoot);

        // Positive control: the scan really sees the projects it is about.
        Assert.Contains(projectFiles, path => path.EndsWith("Diten.PvgService.Api.csproj", StringComparison.Ordinal));
        Assert.Contains(projectFiles, path => path.EndsWith(PersistenceProjectFile, StringComparison.Ordinal));

        var offendingProjects = projectFiles
            .Where(path => !path.EndsWith(PersistenceProjectFile, StringComparison.Ordinal))
            .Where(path => File.ReadAllText(path).Contains("MongoDB", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert.Empty(offendingProjects);

        var guardedAssemblies = new[]
        {
            typeof(SafetyCaseIntake).Assembly,
            typeof(IPvgIntakeDraftStore).Assembly,
            typeof(InMemoryPvgIntakeDraftRepository).Assembly,
            typeof(PvgServiceApiHost).Assembly
        };
        foreach (var assembly in guardedAssemblies)
        {
            var mongoReferences = assembly.GetReferencedAssemblies()
                .Select(name => name.Name)
                .Where(name => name is not null && name.StartsWith("MongoDB.", StringComparison.Ordinal))
                .ToArray();
            Assert.True(
                mongoReferences.Length == 0,
                $"{assembly.GetName().Name} references a Mongo assembly: {string.Join(", ", mongoReferences)}");
        }

        // Positive control: the persistence assembly is the one that does reference the driver.
        Assert.Contains(
            typeof(PvgMongoIntakeDraftStore).Assembly.GetReferencedAssemblies(),
            name => name.Name == "MongoDB.Driver");
    }

    [Fact]
    public void Only_the_Api_and_MOD0230_test_projects_reference_the_persistence_project()
    {
        var root = PvgRepositoryRoot.Find();
        var serviceRoot = Path.Combine(root, "services", "Diten.PvgService");
        var projects = PvgRepositoryRoot.ProjectFiles(serviceRoot)
            .Select(path => (Path: path, Text: File.ReadAllText(path)))
            .ToArray();

        // Positive control: the files read include the Api project and the persistence project.
        Assert.Contains(projects, project => project.Path.EndsWith("Diten.PvgService.Api.csproj", StringComparison.Ordinal));
        Assert.Contains(projects, project => project.Path.EndsWith(PersistenceProjectFile, StringComparison.Ordinal));
        Assert.Empty(PvgProjectReferenceCheck.FindDisallowedPersistenceReferences(projects));

        // The check itself: a non-allowed project that references the persistence project is found, and only it.
        var fixture = new[]
        {
            (Path: "services/Diten.PvgService/src/Diten.PvgService.Application/Diten.PvgService.Application.csproj",
                Text: """
                      <Project Sdk="Microsoft.NET.Sdk">
                        <ItemGroup>
                          <ProjectReference Include="..\Diten.PvgService.Persistence\Diten.PvgService.Persistence.csproj" />
                        </ItemGroup>
                      </Project>
                      """),
            (Path: "services/Diten.PvgService/src/Diten.PvgService.Api/Diten.PvgService.Api.csproj",
                Text: """
                      <Project Sdk="Microsoft.NET.Sdk.Web">
                        <ItemGroup>
                          <ProjectReference Include="..\Diten.PvgService.Persistence\Diten.PvgService.Persistence.csproj" />
                        </ItemGroup>
                      </Project>
                      """)
        };
        var findings = PvgProjectReferenceCheck.FindDisallowedPersistenceReferences(fixture);
        Assert.Equal(
            new[] { "services/Diten.PvgService/src/Diten.PvgService.Application/Diten.PvgService.Application.csproj" },
            findings);
    }

    [Fact]
    public void No_committed_configuration_selects_the_mongo_store()
    {
        var root = PvgRepositoryRoot.Find();
        var files = PvgCommittedConfiguration.Collect(root);

        // Positive control: the walk reaches the launch configuration and the workflows it is about.
        Assert.Contains(files, file => file.Path == ".claude/launch.json");
        Assert.Contains(files, file => file.Path == ".github/workflows/service-tests.yml");
        Assert.Empty(PvgStoreSelectionScan.Find(files));

        // The check itself, on a launch profile held as a string: each key spelling that selects Mongo is found once.
        const string launchProfile =
            """
            {
              "profiles": {
                "ColonSpelling": { "commandName": "Project", "commandLineArgs": "--Pvg:Persistence:Store=Mongo" },
                "EnvironmentSpelling": { "commandName": "Project", "environmentVariables": { "Pvg__Persistence__Store": "Mongo" } },
                "InMemoryProfile": { "commandName": "Project", "environmentVariables": { "Pvg__Persistence__Store": "InMemory" } }
              }
            }
            """;
        var findings = PvgStoreSelectionScan.Find([("fixture/launchSettings.json", launchProfile)]);

        Assert.Equal(
            new[]
            {
                "fixture/launchSettings.json|colon",
                "fixture/launchSettings.json|underscore"
            },
            findings.Select(finding => finding.Path + "|" + finding.Spelling).Order(StringComparer.Ordinal).ToArray());

        // The nested form (WP-0230-DL10-R1, C9): an appsettings file that selects Mongo through nested JSON objects, in
        // any letter case of the value, is found as "nested" and only so; the same shape selecting InMemory, and a file
        // that is not JSON, are not.
        var nested = PvgStoreSelectionScan.Find(
        [
            ("fixture/appsettings.Development.json", """{ "Pvg": { "Persistence": { "Store": "Mongo" } } }"""),
            ("fixture/appsettings.Lower.json", """{ "pvg": { "persistence": { "store": "mongo" } } }"""),
            ("fixture/appsettings.InMemory.json", """{ "Pvg": { "Persistence": { "Store": "InMemory" } } }"""),
            ("fixture/appsettings.Other.json", """{ "Pvg": { "Runtime": { "Store": "Mongo" } } }"""),
            ("fixture/notes.txt", """{ "Pvg": { "Persistence": { "Store": "Mongo" } } }""")
        ]);

        Assert.Equal(
            new[]
            {
                "fixture/appsettings.Development.json|nested",
                "fixture/appsettings.Lower.json|nested"
            },
            nested.Select(finding => finding.Path + "|" + finding.Spelling).Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void Store_contract_and_mongo_store_have_no_delete_bulk_delete_archive_void_or_export_member()
    {
        string[] forbidden = ["Delete", "BulkDelete", "Archive", "Void", "Export"];
        // The two stored fields the pack requires (soft delete: IsDeleted, DeletedAt); they are data, not operations.
        string[] storedFields = ["IsDeleted", "DeletedAt"];

        var types = new List<Type> { typeof(IPvgIntakeDraftStore) };
        types.AddRange(typeof(PvgMongoIntakeDraftStore).Assembly.GetTypes());
        Assert.True(types.Count > 5, "the persistence assembly should expose its types to this scan");

        const BindingFlags all = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                                 BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        var offenders = new List<string>();
        foreach (var type in types)
        {
            Check(type.FullName ?? type.Name);
            foreach (var member in type.GetMembers(all))
            {
                if (member is MethodBase { IsSpecialName: true })
                {
                    continue;
                }

                var name = member.Name.Replace("<", string.Empty).Replace(">k__BackingField", string.Empty);
                if (storedFields.Contains(name, StringComparer.Ordinal))
                {
                    continue;
                }

                Check(type.Name + "." + member.Name);
            }
        }

        Assert.True(offenders.Count == 0, "forbidden store members: " + string.Join(", ", offenders));

        void Check(string name)
        {
            if (forbidden.Any(word => name.Contains(word, StringComparison.OrdinalIgnoreCase)))
            {
                offenders.Add(name);
            }
        }
    }

    [Fact]
    public void Pvg_mongo_test_harness_refuses_port_27017_and_fails_without_mongod()
    {
        // The port: an address on 27017 is refused, and so is an address without a port (that is 27017).
        Assert.Throws<InvalidOperationException>(() => PvgMongoTestHarness.ResolveAddressPort("mongodb://127.0.0.1:27017"));
        Assert.Throws<InvalidOperationException>(() => PvgMongoTestHarness.ResolveAddressPort("127.0.0.1:27017"));
        Assert.Throws<InvalidOperationException>(() => PvgMongoTestHarness.ResolveAddressPort("mongodb://127.0.0.1/"));
        Assert.Throws<InvalidOperationException>(() => PvgMongoTestHarness.ResolvePort(27017));
        Assert.Equal(27123, PvgMongoTestHarness.ResolveAddressPort("mongodb://127.0.0.1:27123/?replicaSet=x"));
        Assert.Equal(40111, PvgMongoTestHarness.ResolvePort(40111));

        // The binary: not set, empty, relative or missing is an error - never a value that lets the test skip.
        Assert.Throws<InvalidOperationException>(() => PvgMongoTestHarness.ResolveBinary(null, _ => true));
        Assert.Throws<InvalidOperationException>(() => PvgMongoTestHarness.ResolveBinary(" ", _ => true));
        Assert.Throws<InvalidOperationException>(() => PvgMongoTestHarness.ResolveBinary("mongod", _ => true));
        Assert.Throws<InvalidOperationException>(() => PvgMongoTestHarness.ResolveBinary("/opt/none/mongod", _ => false));
        Assert.Equal("/opt/fake/mongod", PvgMongoTestHarness.ResolveBinary("/opt/fake/mongod", _ => true));
    }
}

internal static class PvgRepositoryRoot
{
    private static readonly string[] SkippedDirectories = ["bin", "obj", "node_modules", ".git", "TestResults"];

    public static string Find()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("The repository root (the directory with AGENTS.md) was not found.");
    }

    public static IReadOnlyList<string> ProjectFiles(string directory) =>
        Walk(directory, relative => false, "*.csproj").Select(file => file.FullName).Order(StringComparer.Ordinal).ToArray();

    // Files below a directory whose name matches the pattern, skipping build output, packages and nested worktrees.
    internal static IEnumerable<FileInfo> Walk(string directory, Func<string, bool> skipRelativeDirectory, string pattern)
    {
        var pending = new Stack<DirectoryInfo>();
        pending.Push(new DirectoryInfo(directory));
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            foreach (var file in current.EnumerateFiles(pattern))
            {
                yield return file;
            }

            foreach (var child in current.EnumerateDirectories())
            {
                if (SkippedDirectories.Contains(child.Name, StringComparer.OrdinalIgnoreCase) ||
                    (child.Attributes & FileAttributes.ReparsePoint) != 0 ||
                    skipRelativeDirectory(child.FullName))
                {
                    continue;
                }

                pending.Push(child);
            }
        }
    }
}

// Pure: takes project paths and project texts and returns every reference to the persistence project from a project
// other than the Api and the two MOD-0230 test projects (a reference from Domain, Application or Infrastructure would be
// a project cycle).
internal static class PvgProjectReferenceCheck
{
    private static readonly string[] AllowedReferrers =
    [
        "Diten.PvgService.Api.csproj",
        "Diten.PvgService.Api.Tests.csproj",
        "Diten.PvgService.RegPvBase.Tests.csproj"
    ];

    private static readonly Regex PersistenceReference = new(
        @"<ProjectReference\s[^>]*Include\s*=\s*""[^""]*Diten\.PvgService\.Persistence\.csproj""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static IReadOnlyList<string> FindDisallowedPersistenceReferences(
        IEnumerable<(string Path, string Text)> projects)
    {
        var findings = new List<string>();
        foreach (var (path, text) in projects)
        {
            var fileName = path.Replace('\\', '/').Split('/')[^1];
            if (PersistenceReference.IsMatch(text) && !AllowedReferrers.Contains(fileName, StringComparer.Ordinal))
            {
                findings.Add(path);
            }
        }

        return findings;
    }
}

// The configuration-like files of the repository: every appsettings*.json and launchSettings.json, the Claude launch
// configuration and the workflow files. Paths are repository-relative with "/" separators.
internal static class PvgCommittedConfiguration
{
    public static IReadOnlyList<(string Path, string Text)> Collect(string root)
    {
        var nestedWorktrees = Path.Combine(root, ".claude", "worktrees");
        var files = new List<(string Path, string Text)>();

        foreach (var file in PvgRepositoryRoot.Walk(
                     root,
                     directory => string.Equals(directory, nestedWorktrees, StringComparison.Ordinal),
                     "*"))
        {
            var relative = Path.GetRelativePath(root, file.FullName).Replace('\\', '/');
            var isSettings = file.Name.StartsWith("appsettings", StringComparison.OrdinalIgnoreCase) &&
                             file.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
            var isLaunchSettings = string.Equals(file.Name, "launchSettings.json", StringComparison.OrdinalIgnoreCase);
            var isClaudeLaunch = relative == ".claude/launch.json";
            var isWorkflow = relative.StartsWith(".github/workflows/", StringComparison.Ordinal);
            if (isSettings || isLaunchSettings || isClaudeLaunch || isWorkflow)
            {
                files.Add((relative, File.ReadAllText(file.FullName)));
            }
        }

        return files;
    }
}

// Pure: takes file paths and texts and returns every setting of Pvg:Persistence:Store / Pvg__Persistence__Store to Mongo
// (and, for JSON files, the nested form).
internal static class PvgStoreSelectionScan
{
    internal sealed record Finding(string Path, string Spelling);

    private static readonly Regex ColonSpelling = new(
        @"Pvg:Persistence:Store[""']?\s*[:=]?\s*[""']?Mongo\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex UnderscoreSpelling = new(
        @"Pvg__Persistence__Store[""']?\s*[:=]?\s*[""']?Mongo\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static IReadOnlyList<Finding> Find(IEnumerable<(string Path, string Text)> files)
    {
        var findings = new List<Finding>();
        foreach (var (path, text) in files)
        {
            if (ColonSpelling.IsMatch(text))
            {
                findings.Add(new Finding(path, "colon"));
            }

            if (UnderscoreSpelling.IsMatch(text))
            {
                findings.Add(new Finding(path, "underscore"));
            }

            if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && SelectsMongoNested(text))
            {
                findings.Add(new Finding(path, "nested"));
            }
        }

        return findings;
    }

    private static bool SelectsMongoNested(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                   Child(document.RootElement, "Pvg") is { } pvg &&
                   Child(pvg, "Persistence") is { } persistence &&
                   Child(persistence, "Store") is { ValueKind: JsonValueKind.String } store &&
                   string.Equals(store.GetString(), "Mongo", StringComparison.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static JsonElement? Child(JsonElement parent, string name)
    {
        if (parent.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var property in parent.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value;
            }
        }

        return null;
    }
}
