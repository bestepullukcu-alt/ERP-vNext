using System.Reflection;
using System.Text.RegularExpressions;
using Diten.Platform.API.Controllers;
using Diten.Platform.Application.Features.Tenants.Handlers;
using Diten.Platform.Application.Features.WorkingHours;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// WP-TASK-CALENDAR-ENGINE-01 (BL-451 decision note) — the two architectural promises behind the working-hours seam,
/// measured on the PRODUCTION assemblies and sources rather than on a copy of the rule.
///
/// <list type="number">
/// <item><b>One reader.</b> <c>Tenant.DefaultWorkdayStart</c>/<c>DefaultWorkdayEnd</c> are read by
/// <see cref="WorkingHoursProvider"/> and by nothing else except the tenant-settings surface that edits them (it
/// echoes them back to the administrator). A task rule or the calendar feed reading them directly is exactly the
/// regression the owner asked about: fixing hours later would then have two places to change.</item>
/// <item><b>No wall-clock constants</b> in task/calendar code — "09:00", <c>new TimeOnly(18, 0)</c>,
/// <c>TimeSpan.FromHours(9)</c>. The only default lives on the tenant entity.</item>
/// </list>
/// </summary>
public sealed class TaskCalendarGuardTests
{
    // ── 1 — only the provider reads the tenant's default hours ───────────────

    private static readonly Type[] AllowedReaders =
    [
        typeof(WorkingHoursProvider),
        // The administrator's own settings surface: it edits these two fields and shows them back.
        typeof(GetTenantSettingsQueryHandler),
        typeof(UpdateTenantSettingsCommandHandler)
    ];

    [Fact]
    public void Only_the_working_hours_provider_reads_the_tenant_default_hours()
    {
        var readers = TenantHourReaders();

        var offenders = readers.Where(r => !AllowedReaders.Contains(r)).Select(r => r.FullName).ToList();
        Assert.True(
            offenders.Count == 0,
            "These types read Tenant.DefaultWorkdayStart/End directly; ask IWorkingHoursProvider instead: "
            + string.Join(", ", offenders));
    }

    [Fact]
    public void The_scan_is_not_vacuous_it_finds_the_provider_itself()
    {
        // If the IL walk silently found nothing, the test above would pass for any code at all.
        Assert.Contains(typeof(WorkingHoursProvider), TenantHourReaders());
    }

    /// <summary>
    /// Every production type whose IL CALLS one of the two getters. Compiler-generated state machines and closures
    /// are folded into the type that wrote them (outermost declaring type), so an <c>async</c> method or a lambda
    /// cannot hide a read.
    /// </summary>
    private static IReadOnlySet<Type> TenantHourReaders()
    {
        var getters = new[]
        {
            typeof(Tenant).GetProperty(nameof(Tenant.DefaultWorkdayStart))!.GetMethod!,
            typeof(Tenant).GetProperty(nameof(Tenant.DefaultWorkdayEnd))!.GetMethod!
        };

        var assemblies = new[]
        {
            typeof(Tenant).Assembly,                       // Domain
            typeof(WorkingHoursProvider).Assembly,         // Application
            typeof(TenantRegistryRepository).Assembly,     // Infrastructure
            typeof(WorkCalendarController).Assembly        // API
        };

        var readers = new HashSet<Type>();
        foreach (var assembly in assemblies)
        {
            foreach (var type in LoadableTypes(assembly))
            {
                const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                                         | BindingFlags.Static | BindingFlags.DeclaredOnly;
                var methods = type.GetMethods(all).Cast<MethodBase>().Concat(type.GetConstructors(all));
                foreach (var method in methods)
                {
                    if (Calls(method, getters))
                    {
                        readers.Add(Outermost(type));
                    }
                }
            }
        }

        return readers;
    }

    private static bool Calls(MethodBase method, MethodInfo[] targets)
    {
        byte[]? il;
        try
        {
            il = method.GetMethodBody()?.GetILAsByteArray();
        }
        catch (Exception)
        {
            return false;
        }

        if (il is null)
        {
            return false;
        }

        // call (0x28) / callvirt (0x6F) followed by a 4-byte method token. A byte-level walk can meet an operand
        // byte that looks like an opcode; such a "token" either fails to resolve or resolves to something else,
        // and only an exact match to a target counts.
        for (var i = 0; i + 4 < il.Length; i++)
        {
            if (il[i] != 0x28 && il[i] != 0x6F)
            {
                continue;
            }

            var token = BitConverter.ToInt32(il, i + 1);
            var table = token >> 24;
            if (table != 0x0A && table != 0x06 && table != 0x2B)
            {
                continue;
            }

            try
            {
                var resolved = method.Module.ResolveMethod(
                    token,
                    method.DeclaringType?.IsGenericType == true ? method.DeclaringType.GetGenericArguments() : null,
                    method.IsGenericMethod ? method.GetGenericArguments() : null);
                if (resolved is not null && targets.Any(t => t == resolved))
                {
                    return true;
                }
            }
            catch (Exception)
            {
                // Not a method token after all.
            }
        }

        return false;
    }

    private static Type Outermost(Type type)
    {
        while (type.DeclaringType is not null)
        {
            type = type.DeclaringType;
        }

        return type;
    }

    private static IEnumerable<Type> LoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!;
        }
    }

    // ── 2 — no wall-clock constants in task / calendar code ──────────────────

    /// <summary>Wall-clock literals: "09:00", new TimeOnly(9…), TimeOnly.Parse, new TimeSpan(9, 0, 0), FromHours(9), Hour == 18.</summary>
    private static readonly Regex WallClock = new(
        @"""[0-2]?\d:[0-5]\d|new\s+TimeOnly\s*\(\s*\d|TimeOnly\.(Parse|ParseExact)\s*\(|new\s+TimeSpan\s*\(\s*\d+\s*,\s*\d+\s*,|TimeSpan\.FromHours\s*\(\s*\d|\bHour\s*(==|<=|>=|<|>)\s*\d",
        RegexOptions.Compiled);

    private static readonly string[] GuardedFolders =
    [
        "src/Diten.Platform.Application/Features/Tasks",
        "src/Diten.Platform.Application/Features/WorkAggregation",
        "src/Diten.Platform.Application/Features/WorkingHours"
    ];

    private static readonly string[] GuardedFiles =
    [
        "src/Diten.Platform.API/Controllers/TasksController.cs",
        "src/Diten.Platform.API/Controllers/WorkCalendarController.cs",
        "src/Diten.Platform.API/Controllers/WorkItemsController.cs"
    ];

    [Fact]
    public void Task_and_calendar_code_writes_no_wall_clock_hours()
    {
        var root = PlatformRoot();
        var files = GuardedFolders
            .SelectMany(folder => Directory.EnumerateFiles(Path.Combine(root, folder), "*.cs", SearchOption.AllDirectories))
            .Concat(GuardedFiles.Select(file => Path.Combine(root, file)))
            .ToList();
        Assert.True(files.Count > 50, $"The guarded set looks wrong ({files.Count} files) — did a folder move?");

        var hits = files
            .SelectMany(file => StripComments(File.ReadAllText(file))
                .Split('\n')
                .Select((line, index) => (file, line, index))
                .Where(x => WallClock.IsMatch(x.line)))
            .Select(x => $"{Path.GetRelativePath(root, x.file)}:{x.index + 1}: {x.line.Trim()}")
            .ToList();

        Assert.True(
            hits.Count == 0,
            "Wall-clock hours written into task/calendar code — read them from IWorkingHoursProvider:\n"
            + string.Join("\n", hits));
    }

    [Theory]
    [InlineData("var start = \"09:00\";")]
    [InlineData("var end = new TimeOnly(18, 0);")]
    [InlineData("var s = TimeOnly.Parse(x);")]
    [InlineData("var d = new TimeSpan(9, 0, 0);")]
    [InlineData("var d = TimeSpan.FromHours(9);")]
    [InlineData("if (local.Hour >= 18) { }")]
    public void The_pattern_catches_each_shape_it_claims_to(string line)
        => Assert.Matches(WallClock, line);

    [Theory]
    [InlineData("local = local.AddHours(1);")]
    [InlineData("var m = TaskPlanBlockRules.StepMinutes;")]
    [InlineData("var t = TimeOnly.MinValue;")]
    public void The_pattern_leaves_ordinary_code_alone(string line)
        => Assert.DoesNotMatch(WallClock, line);

    /// <summary>Comments may say "09:00" — the rule is about code. Strings are kept (that is where "09:00" would hide).</summary>
    private static string StripComments(string source)
    {
        var noBlocks = Regex.Replace(source, @"/\*.*?\*/", m => new string('\n', m.Value.Count(c => c == '\n')), RegexOptions.Singleline);
        return Regex.Replace(noBlocks, @"(?m)^\s*///.*$|(?<![:""])//.*$", string.Empty);
    }

    private static string PlatformRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "Diten.Platform.Application")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("services/Diten.Platform not found above the test output.");
    }
}
