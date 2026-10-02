using System.Text;
using TenantArchitecture.ArchitectureTests.AuditTrailStandard;
using Xunit.Abstractions;

namespace TenantArchitecture.ArchitectureTests;

/*
 * THE AUDIT-TRAIL DEBT LATCH (WP-AUDIT-STANDARD-01, root of BL-456).
 *
 * THE RULE (.antigravity/rules/audit-trail-standard.md): every command that changes tenant or platform data is
 * audited. Not being audited is an exception, and an exception is declared with its reason.
 *
 * WHY A LATCH AND NOT A WALL. Measured 2026-10-02: most write commands in the repo are not audited. A guard that
 * demanded the rule today would be red everywhere and would be switched off. So today's unaudited commands are
 * written down, per service, in `tests/architecture/audit-ledger/<service>.md` under "Bilinen borç" — and that
 * list can only SHRINK:
 *     a command that is neither audited, nor a declared exception, nor on the list   → red (new debt refused)
 *     a command on the list that is now audited, or no longer exists                 → red (take it off the list)
 * The second half is what keeps the list honest without anybody maintaining it.
 *
 * ⚠ WHAT THIS GUARD MEASURES AND WHAT IT DOES NOT — it is a TEXT check, like every guard in this project.
 * It reads production source and answers "does this command carry the audit marker / does its handler name the
 * audit writer". It never executes a command. So it measures that the audit is WIRED, never that a record is
 * WRITTEN, complete, immutable or readable. Those are behaviour questions; the rule file's §9 table says which of
 * them nothing measures today. A handler that names the writer and never calls it passes this guard.
 *
 * WHAT IS OUTSIDE THE MEASURE, by name: write paths that have no command type at all (controller → service →
 * repository, background jobs, event consumers). Commands that exist but bypass MediatR are measured BY NAME in
 * the two services that have them; the last guard below keeps that set exact.
 */
public sealed class AuditTrailStandardTests(ITestOutputHelper output)
{
    private const string WhatToDo =
        "\n\nKural: " + AuditTrailMeasurement.RuleFile +
        "\nÜç seçenekten biri:" +
        "\n  (1) komutu DENETLE — yol a (Platform içi IAuditableCommand), b (merkezi günlüğe iletim) ya da c (eşdeğer iz);" +
        "\n  (2) İSTİSNA olarak bildir — defterde '## İstisnalar' satırı: komut | kuraldaki sınıf (İ1…) | gerekçe;" +
        "\n  (3) hiçbiri değilse bu yeni borçtur ve defter yeni borç KABUL ETMEZ: '## Bilinen borç' yalnız küçülür." +
        "\nDefter: " + AuditTrailMeasurement.LedgerDirectory + "/<servis>.md";

    private static IEnumerable<ServiceMeasurement> ServicesWithCommands =>
        AuditTrailMeasurement.All.Where(service => service.Commands.Count > 0);

    [Fact]
    public void EveryWriteCommand_IsAudited_OrADeclaredException_OrKnownDebt()
    {
        var failures = new List<string>();

        foreach (var service in ServicesWithCommands)
        {
            var exceptions = (service.Ledger?.Exceptions.Select(e => e.Command) ?? []).ToHashSet(StringComparer.Ordinal);
            var debt = (service.Ledger?.Debt ?? []).ToHashSet(StringComparer.Ordinal);

            foreach (var command in service.Commands.Where(c => !c.IsAudited))
            {
                var listed = (exceptions.Contains(command.Name) ? 1 : 0) + (debt.Contains(command.Name) ? 1 : 0);
                if (listed == 0)
                {
                    failures.Add($"{service.Service}: {command.Name} — YENİ KOMUT DENETİMSİZ GELDİ ({command.Command.RelativePath})");
                }
                else if (listed == 2)
                {
                    failures.Add($"{service.Service}: {command.Name} — hem istisna hem borç; yalnız biri olabilir");
                }
            }
        }

        Assert.True(failures.Count == 0, Report("Denetlenmeyen ve hiçbir listede olmayan komut", failures));
    }

    [Fact]
    public void KnownDebt_OnlyShrinks_AnEntryThatIsAuditedOrGone_MustBeRemoved()
    {
        var failures = new List<string>();

        foreach (var service in ServicesWithCommands.Where(s => s.Ledger is not null))
        {
            var byName = service.Commands.ToLookup(c => c.Name, StringComparer.Ordinal);

            foreach (var duplicate in service.Ledger!.Debt.GroupBy(x => x, StringComparer.Ordinal).Where(g => g.Count() > 1))
            {
                failures.Add($"{service.Service}: {duplicate.Key} — borç listesinde iki kez");
            }

            foreach (var entry in service.Ledger.Debt.Distinct(StringComparer.Ordinal))
            {
                if (!byName.Contains(entry))
                {
                    failures.Add($"{service.Service}: {entry} — BÖYLE BİR YAZMA KOMUTU YOK (silinmiş, adı değişmiş ya da yanlış yazılmış): listeden çıkar");
                }
                else if (byName[entry].Any(c => c.IsAudited))
                {
                    var trail = byName[entry].SelectMany(c => c.Evidence).First(t => t.IsAccepted);
                    failures.Add($"{service.Service}: {entry} — ARTIK DENETLENİYOR (iz: {trail.Name}, yol {trail.Path}): listeden çıkar");
                }
            }
        }

        Assert.True(failures.Count == 0, Report("Borç listesi güncel değil — liste yalnız küçülür", failures));
    }

    [Fact]
    public void Exceptions_NameARealUnauditedCommand_AClassFromTheRule_AndAReason()
    {
        var classes = AuditTrailMeasurement.ExceptionClassesFromTheRule();
        Assert.True(classes.Count > 0, $"İstisna sınıfları kural dosyasından okunamadı: {AuditTrailMeasurement.RuleFile} içinde '| İ1 | …' satırları bekleniyor.");

        var failures = new List<string>();

        foreach (var service in ServicesWithCommands.Where(s => s.Ledger is not null))
        {
            var byName = service.Commands.ToLookup(c => c.Name, StringComparer.Ordinal);

            foreach (var duplicate in service.Ledger!.Exceptions.GroupBy(e => e.Command, StringComparer.Ordinal).Where(g => g.Count() > 1))
            {
                failures.Add($"{service.Service}: {duplicate.Key} — istisna tablosunda iki kez");
            }

            foreach (var exception in service.Ledger.Exceptions)
            {
                if (!byName.Contains(exception.Command))
                {
                    failures.Add($"{service.Service}: {exception.Command} — böyle bir yazma komutu yok: istisna satırını kaldır");
                    continue;
                }

                if (byName[exception.Command].Any(c => c.IsAudited))
                {
                    failures.Add($"{service.Service}: {exception.Command} — denetleniyor; istisna satırı gereksiz ve yanıltıcı: kaldır");
                }

                if (!classes.Contains(exception.Class))
                {
                    failures.Add($"{service.Service}: {exception.Command} — istisna sınıfı '{exception.Class}' kuralda yok (geçerli: {string.Join(", ", classes)})");
                }

                if (exception.Reason.Count(char.IsLetter) < 15)
                {
                    failures.Add($"{service.Service}: {exception.Command} — GEREKÇE BOŞ ya da bir cümle değil; gerekçesiz istisna olmaz");
                }
            }
        }

        Assert.True(failures.Count == 0, Report("Geçersiz istisna bildirimi", failures));
    }

    [Fact]
    public void DeclaredTrails_ExistInProductionCode_AndMarkersAreWiredIntoThePipeline()
    {
        var failures = new List<string>();

        foreach (var service in AuditTrailMeasurement.All.Where(s => s.Ledger is not null))
        {
            foreach (var malformed in service.Ledger!.Malformed)
            {
                failures.Add($"{service.Ledger.RelativePath}: {malformed}");
            }

            foreach (var duplicate in service.Ledger.Trails.GroupBy(t => t.Name, StringComparer.Ordinal).Where(g => g.Count() > 1))
            {
                failures.Add($"{service.Service}: iz adı '{duplicate.Key}' iki kez tanımlı");
            }

            var declared = service.Types.Select(t => t.Name).ToHashSet(StringComparer.Ordinal);

            foreach (var trail in service.Ledger.Trails)
            {
                foreach (var token in trail.Required.Concat(trail.Forbidden).Where(token => !declared.Contains(token)))
                {
                    failures.Add($"{service.Service}: iz '{trail.Name}' — belirteç '{token}' servisin üretim kodunda tanımlı değil; iz hayalet");
                }

                if (trail.Kind != TrailKind.Marker)
                {
                    continue;
                }

                // A marker interface nobody reads audits nothing. Require a MediatR pipeline behavior that names the
                // marker, and require that behavior to be named somewhere else in the service (its registration).
                var behaviors = service.Types
                    .Where(t => t.Kind == "class" && t.BaseNames.Contains("IPipelineBehavior")
                                && CSharpSourceScanner.ContainsIdentifier(t.Body, trail.Required[0]))
                    .ToList();

                if (behaviors.Count == 0)
                {
                    failures.Add($"{service.Service}: iz '{trail.Name}' — '{trail.Required[0]}' işaretini okuyan bir IPipelineBehavior yok; işaret hiçbir şey yazdırmıyor");
                    continue;
                }

                var registered = behaviors.Any(behavior => service.Types.Any(other =>
                    other.Name != behavior.Name && CSharpSourceScanner.ContainsIdentifier(other.Body, behavior.Name)));

                if (!registered)
                {
                    failures.Add($"{service.Service}: iz '{trail.Name}' — {string.Join(", ", behaviors.Select(b => b.Name))} hiçbir yerde kaydedilmiyor (DI kaydı bulunamadı)");
                }
            }
        }

        Assert.True(failures.Count == 0, Report("Defterde tanımlı iz üretim kodunda karşılık bulmuyor", failures));
    }

    [Fact]
    public void IndirectDeclarations_AreProvenByTheHandlerAndTheTypeItGoesThrough()
    {
        var failures = new List<string>();

        foreach (var service in AuditTrailMeasurement.All.Where(s => s.Ledger is not null))
        {
            var names = service.Commands.Select(c => c.Name).ToHashSet(StringComparer.Ordinal);

            failures.AddRange(service.UnprovenIndirect.Select(line => $"{service.Service}: {line}"));
            failures.AddRange(service.Ledger!.Indirect
                .Where(d => !names.Contains(d.Command))
                .Select(d => $"{service.Service}: {d.Command} — '## Dolaylı' satırı var ama böyle bir yazma komutu yok: kaldır"));
        }

        Assert.True(failures.Count == 0, Report("'## Dolaylı' bildirimi üretim kodunda kanıtlanamadı", failures));
    }

    [Fact]
    public void EveryServiceWithWriteCommands_HasALedger_AndCommandNamesAreUnambiguous()
    {
        var failures = new List<string>();
        var measured = AuditTrailMeasurement.All;

        // Never "> 0" alone: a scan that silently found nothing would pass every other test by having nothing to check.
        Assert.True(measured.Sum(s => s.Commands.Count) > 1000, "Tarama beklenenin çok altında komut buldu — tarayıcı ya da klasör yapısı değişti; ölçüm güvenilir değil.");

        foreach (var service in measured)
        {
            if (service.Commands.Count > 0 && service.Ledger is null)
            {
                failures.Add($"{service.Service}: {service.Commands.Count} yazma komutu var, defteri yok → {AuditTrailMeasurement.LedgerDirectory}/{service.Service}.md oluştur");
            }

            failures.AddRange(service.DuplicateCommandNames.Select(d => $"{service.Service}: aynı adda iki komut — defter adı ayırt edemez: {d}"));
        }

        var known = measured.Select(s => s.Service).ToHashSet(StringComparer.Ordinal);
        failures.AddRange(AuditTrailMeasurement.LedgerFilesOnDisk()
            .Where(file => !known.Contains(file))
            .Select(file => $"{AuditTrailMeasurement.LedgerDirectory}/{file}.md — bu adda bir servis yok (services/{file}/src)"));

        Assert.True(failures.Count == 0, Report("Defter kapsamı eksik", failures));
    }

    /// <summary>
    /// A type named <c>*Command</c> that is not a MediatR request is dispatched some other way (an application
    /// service, a minimal-API endpoint). Measured 2026-10-02: exactly two services have such commands, and by CT
    /// decision they are measured BY NAME (<see cref="AuditTrailMeasurement.MeasuredByName"/>) — each of those
    /// commands is in the ledger like any other. This test keeps that set exact: a THIRD service growing a
    /// non-MediatR command would be invisible to every test above, so it fails here and has to be added to the
    /// name rule (and its commands to a ledger) — or turned into a MediatR request.
    ///
    /// <para>⚠ Still outside the measure: write paths with no <c>*Command</c> type at all — a controller calling a
    /// service or repository directly, background jobs, event consumers (rule file §9).</para>
    /// </summary>
    [Fact]
    public void CommandsThatBypassMediatR_ExistOnlyInTheServicesMeasuredByName()
    {
        var outside = AuditTrailMeasurement.All
            .Where(s => s.NonMediatRCommandNames.Count > 0 && !AuditTrailMeasurement.MeasuredByName.Contains(s.Service))
            .Select(s => $"{s.Service}: {string.Join(", ", s.NonMediatRCommandNames)}")
            .ToList();

        Assert.True(outside.Count == 0, Report(
            "MediatR isteği olmayan '*Command' türü, adla ölçülmeyen bir serviste. Bu komutu hiçbir denetim ölçümü görmüyor", outside));

        var vacuous = AuditTrailMeasurement.MeasuredByName
            .Where(name => AuditTrailMeasurement.All.FirstOrDefault(s => s.Service == name)?.NonMediatRCommandNames.Count is null or 0)
            .Select(name => $"{name}: adla ölçülen hiçbir komutu kalmadı — MeasuredByName listesinden çıkar")
            .ToList();

        Assert.True(vacuous.Count == 0, Report("Ad kuralı listesi güncel değil", vacuous));
    }

    /// <summary>
    /// Prints the inventory. It asserts nothing about the numbers — it exists so the inventory document quotes
    /// THIS measurement instead of running a second count: <c>dotnet test --logger "console;verbosity=detailed"
    /// --filter Inventory</c>.
    /// </summary>
    [Fact]
    public void Inventory_PrintsTheTableTheInventoryDocumentQuotes()
    {
        var table = new StringBuilder();
        table.AppendLine("| servis | yazma komutu | a | b | c | istisna | borç | borcun içinde aday izi olan | handler bulunamayan | MediatR isteği olmayan *Command türü |");
        table.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|");

        var totals = new int[9];
        foreach (var service in AuditTrailMeasurement.All.Where(s => s.Commands.Count > 0 || s.NonMediatRCommandNames.Count > 0))
        {
            int Path(string path) => service.Commands.Count(c => c.Evidence.FirstOrDefault(t => t.IsAccepted)?.Path == path);
            var exceptions = (service.Ledger?.Exceptions.Select(e => e.Command) ?? []).ToHashSet(StringComparer.Ordinal);
            var unaudited = service.Commands.Where(c => !c.IsAudited).ToList();

            int[] row =
            [
                service.Commands.Count,
                Path("a"),
                Path("b"),
                Path("c"),
                unaudited.Count(c => exceptions.Contains(c.Name)),
                unaudited.Count(c => !exceptions.Contains(c.Name)),
                unaudited.Count(c => !exceptions.Contains(c.Name) && c.HasCandidateOnly),
                service.Commands.Count(c => !c.HasHandler),
                service.NonMediatRCommandNames.Count
            ];

            for (var i = 0; i < row.Length; i++)
            {
                totals[i] += row[i];
            }

            table.AppendLine($"| {service.Service} | {string.Join(" | ", row)} |");
        }

        table.AppendLine($"| **TOPLAM** | {string.Join(" | ", totals)} |");
        output.WriteLine(table.ToString());

        Assert.Equal(totals[0], totals[1] + totals[2] + totals[3] + totals[4] + totals[5]);
    }

    private static string Report(string headline, List<string> failures) =>
        failures.Count == 0
            ? string.Empty
            : $"{headline} ({failures.Count}):\n  - {string.Join("\n  - ", failures.Take(60))}"
              + (failures.Count > 60 ? $"\n  … ve {failures.Count - 60} tane daha" : string.Empty)
              + WhatToDo;
}
