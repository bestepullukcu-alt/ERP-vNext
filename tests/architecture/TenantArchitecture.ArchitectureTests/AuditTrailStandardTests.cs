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
 *
 * ⚠ WHAT THE FIRST VERSION GOT WRONG, AND WHY THE NUMBERS BELOW ARE IN THIS FILE. "The list only shrinks" was a
 * sentence, not a measurement: a developer who wrote a new unaudited command AND added one line to the ledger got
 * eight green tests and a green CI. The ledger is a file anybody edits; this file is where a change needs a
 * reason. So every number a ledger edit could move in the wrong direction is PINNED here, exactly:
 *     debt count, exception count, K2-debt count, writing-query count — per service (PinnedCounts)
 *     the accepted trails — service, name, path, token (AcceptedTrails)
 * Shrinking debt means lowering a number here in the same change. Raising one is a Control Tower decision, and the
 * failure message says so. `scripts/run_phase1_gates.sh` adds the other half in CI: a pull request whose diff ADDS
 * a list line to a ledger is red even if somebody also raised the number.
 *
 * ⚠ WHAT THIS GUARD MEASURES AND WHAT IT DOES NOT — it is a TEXT check, like every guard in this project.
 * It reads production source and answers "does this command carry the audit marker / does its handler call the
 * audit writer". It never executes a command. So it measures that the audit is WIRED, never that a record is
 * WRITTEN, complete, immutable or readable. The rule file's §9 table says which of those nothing measures today.
 * A handler that calls the writer on a branch that never runs passes this guard.
 *
 * WHAT IS OUTSIDE THE MEASURE, by name: write paths that have no command type at all (controller → service →
 * repository, background jobs, event consumers, internal HTTP consumers).
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

    private sealed record Pinned(int Debt, int Exceptions, int K2Debt = 0, int WritingQueries = 0, int Indirect = 0);

    /// <summary>
    /// ⚠ EXACT, PER SERVICE. A number here goes DOWN when a team pays debt (same change as the ledger line). It goes
    /// UP only by a Control Tower decision — that is the whole point of keeping it outside the ledger.
    /// </summary>
    private static readonly Dictionary<string, Pinned> PinnedCounts = new(StringComparer.Ordinal)
    {
        // measured 2026-10-02 — see docs/records/audits/2026-10/
        ["Diten.AuthService"] = new(Debt: 18, Exceptions: 0, K2Debt: 8),
        ["Diten.CrmService"] = new(Debt: 188, Exceptions: 0),
        ["Diten.DevEnablementService"] = new(Debt: 8, Exceptions: 0),
        ["Diten.EnterpriseStrategyService"] = new(Debt: 36, Exceptions: 0, Indirect: 20),
        ["Diten.HcmService"] = new(Debt: 8, Exceptions: 0),
        ["Diten.HumanCapitalService"] = new(Debt: 70, Exceptions: 0),
        ["Diten.ManagementGovernanceService"] = new(Debt: 23, Exceptions: 0),
        ["Diten.MdmService"] = new(Debt: 13, Exceptions: 0, Indirect: 8),
        ["Diten.Platform"] = new(Debt: 164, Exceptions: 5, K2Debt: 192, WritingQueries: 3),
        ["Diten.PpmService"] = new(Debt: 26, Exceptions: 0, Indirect: 25),
        ["Diten.ProcurementService"] = new(Debt: 35, Exceptions: 0),
        ["Diten.PvgService"] = new(Debt: 13, Exceptions: 0),
        ["Diten.TalentEcosystemService"] = new(Debt: 106, Exceptions: 0),
    };

    /// <summary>
    /// ⚠ THE TRAILS THAT GRANT CREDIT — service · name · path · token. Changing a ledger cell from `aday` to `c`, or
    /// pointing an accepted trail at a different token, grants nothing until it is also written here.
    /// </summary>
    private static readonly (string Service, string Name, string Path, string Token)[] AcceptedTrails =
    [
        ("Diten.AuthService", "auth-kullanici-iletimi", "b", "IUserAuditRecorder.RecordAsync"),
        ("Diten.MdmService", "mdm-merkezi-iletim", "b", "IAuditableCommand+IAuditMetadataProvider"),
        ("Diten.MdmService", "mdm-kisaltma-gecmisi", "c", "IProductAbbreviationHistoryRepository.AppendIfAbsentAsync"),
        ("Diten.Platform", "platform-merkezi", "a",
            "IAuditableCommand+IAuditMetadataProvider+!IAuditExcludedRequest+!ITransactionOwnedAuditCommand"),
        ("Diten.Platform", "platform-meta-denetim", "a", "IAuditMetaAuditWriter.WriteAsync"),
        ("Diten.Platform", "gorev-etkinlik-akisi", "c", "TaskItem.Declare"),
        ("Diten.Platform", "is-akisi-gecis-gunlugu", "c",
            "IWorkflowTransitionLogRepository.CreateAsync/IWorkflowTransitionLogRepository.AppendAsync"
            + "/WorkflowTaskTransitionSupport.TransitionAsync/WorkflowTaskTransitionSupport.DelegateAsync"
            + "/WorkflowTaskTransitionSupport.RequestInfoAsync/WorkflowTaskTransitionSupport.CancelAsync")
    ];

    /// <summary>
    /// Handlers whose request is a TYPE PARAMETER, so no request name can be resolved. Each needs a reason: the
    /// commands they serve are measured through their own declarations, and have no per-command handler text.
    /// </summary>
    private static readonly Dictionary<string, string> GenericHandlerExemptions = new(StringComparer.Ordinal)
    {
        ["Diten.ManagementGovernanceService: CatalogCommandHandler"] =
            "abstract generic base `CatalogCommandHandler<TCommand>`; its concrete commands implement ICatalogCommand "
            + "(an IRequest) and are in the ledger by their own names — they are the 12 'handler bulunamayan' rows"
    };

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

    /// <summary>
    /// THE NUMBER HALF OF "ONLY SHRINKS". Without it the two tests above are satisfied by adding a line to the
    /// ledger. Exact equality in both directions: more = new debt (refused); fewer = debt was paid and the number
    /// here must be lowered in the same change, so the pin never drifts above reality and silently leaves room.
    /// </summary>
    [Fact]
    public void DebtAndExceptionCounts_ArePinned_AddingDebtNeedsAControlTowerDecision()
    {
        var failures = new List<string>();
        var measured = AuditTrailMeasurement.All.ToDictionary(s => s.Service, StringComparer.Ordinal);

        foreach (var service in PinnedCounts.Keys.Union(measured.Values.Where(s => s.Ledger is not null).Select(s => s.Service)).OrderBy(x => x, StringComparer.Ordinal))
        {
            if (!PinnedCounts.TryGetValue(service, out var pinned))
            {
                failures.Add($"{service}: defteri var ama bu testte sabit sayısı yok — PinnedCounts'a ekle");
                continue;
            }

            if (!measured.TryGetValue(service, out var found) || found.Ledger is null)
            {
                failures.Add($"{service}: sabit sayısı var ama servis ya da defteri bulunamadı — tarayıcı ya da klasör yapısı değişti");
                continue;
            }

            void Compare(string what, int pin, int actual)
            {
                if (actual > pin)
                {
                    failures.Add($"{service}: {what} {pin} → {actual} — ARTTI. Deftere satır EKLENMİŞ. Borç / istisna eklemek Control Tower kararı ister; "
                                 + "karar varsa bu testteki sayı o kararın gerekçesiyle birlikte yükseltilir.");
                }
                else if (actual < pin)
                {
                    failures.Add($"{service}: {what} {pin} → {actual} — azaldı (iyi haber). Bu testteki sabit sayıyı da {actual} yap; yoksa boşluk kalır.");
                }
            }

            Compare("bilinen borç", pinned.Debt, found.Ledger.Debt.Distinct(StringComparer.Ordinal).Count());
            Compare("istisna", pinned.Exceptions, found.Ledger.Exceptions.Count);
            Compare("K2 borcu", pinned.K2Debt, found.Ledger.K2Debt.Distinct(StringComparer.Ordinal).Count());
            Compare("yazan sorgu", pinned.WritingQueries, found.Ledger.WritingQueries.Distinct(StringComparer.Ordinal).Count());
            // A '## Dolaylı' row grants credit like a debt line withholds it, so its count is pinned the same way:
            // one added row used to audit a command with no other edit (review of 2026-10-02).
            Compare("dolaylı bildirim", pinned.Indirect, found.Ledger.Indirect.Count);
        }

        Assert.True(failures.Count == 0, Report("Defter sayıları sabitlenen sayılarla aynı değil", failures));
    }

    [Fact]
    public void AcceptedTrails_ArePinned_ALedgerCellCannotGrantCredit()
    {
        var inLedgers = AuditTrailMeasurement.All
            .Where(s => s.Ledger is not null)
            .SelectMany(s => s.Ledger!.Trails.Where(t => t.IsAccepted).Select(t => $"{s.Service} · {t.Name} · {t.Path} · {t.RawToken}"))
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
        var pinned = AcceptedTrails.Select(t => $"{t.Service} · {t.Name} · {t.Path} · {t.Token}")
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        var failures = inLedgers.Except(pinned, StringComparer.Ordinal)
            .Select(x => $"defterde kabul edilmiş ama bu testte sabitlenmemiş iz: {x} — bir izi kabul etmek (aday → a/b/c) ya da belirtecini değiştirmek Control Tower kararıdır (kural §5, §10)")
            .Concat(pinned.Except(inLedgers, StringComparer.Ordinal)
                .Select(x => $"bu testte sabitlenmiş ama defterde yok / farklı: {x}"))
            .ToList();

        Assert.True(failures.Count == 0, Report("Kabul edilmiş izler sabitlenen kümeyle aynı değil", failures));
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

                // A reason is a sentence about THIS command. Fifteen letters were satisfied by one word typed four
                // times; five different words are not proof of a good reason, but they end that shortcut.
                var distinctWords = exception.Reason
                    .Split([' ', ',', ';', '.', ':', '—', '-', '(', ')'], StringSplitOptions.RemoveEmptyEntries)
                    .Where(word => word.Count(char.IsLetter) >= 3)
                    .Select(word => word.ToLowerInvariant())
                    .Distinct(StringComparer.Ordinal)
                    .Count();
                if (exception.Reason.Count(char.IsLetter) < 15 || distinctWords < 5)
                {
                    failures.Add($"{service.Service}: {exception.Command} — GEREKÇE BOŞ ya da bir cümle değil (en az beş farklı kelime); gerekçesiz istisna olmaz");
                }

                // Rule §6: identity / role / permission changes can never be an exception. The part a name can show.
                // Only in the Auth service, where those words name the identity records themselves: elsewhere the
                // same check refused the rule's own example of a legitimate exception (a user's theme preference).
                if (service.Service == "Diten.AuthService"
                    && new[] { "User", "Role", "Permission" }.Any(part => exception.Command.Contains(part, StringComparison.Ordinal)))
                {
                    failures.Add($"{service.Service}: {exception.Command} — KİMLİK / ROL / İZİN komutu istisna olamaz (kural §6); denetle");
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
            foreach (var duplicate in service.Ledger!.Trails.GroupBy(t => t.Name, StringComparer.Ordinal).Where(g => g.Count() > 1))
            {
                failures.Add($"{service.Service}: iz adı '{duplicate.Key}' iki kez tanımlı");
            }

            // The service's own source PLUS Diten.Building.Blocks: a shared marker / forwarding behavior (rule §10 K4)
            // is declared there once, not copied into every service.
            var pool = service.TypesWithBuildingBlocks;
            var declared = pool.Select(t => t.Name).ToHashSet(StringComparer.Ordinal);

            foreach (var trail in service.Ledger.Trails)
            {
                if (trail.Kind == TrailKind.Writer)
                {
                    foreach (var token in trail.PositiveTokens)
                    {
                        if (!token.Contains('.'))
                        {
                            failures.Add($"{service.Service}: iz '{trail.Name}' — yazıcı belirteci '{token}' bir YAZMA ÜYESİ olmalı: 'Tip.Metot'. Tipi anmak kanıt değildir; yalnız okuyan handler da onu anar");
                        }
                        else if (!AuditTrailMeasurement.WriteMemberExists(token, pool))
                        {
                            failures.Add($"{service.Service}: iz '{trail.Name}' — '{token}' üretim kodunda yok (tip ya da metot bulunamadı); iz hayalet");
                        }
                    }

                    continue;
                }

                foreach (var token in trail.PositiveTokens.Concat(trail.Forbidden).Where(token => !declared.Contains(token)))
                {
                    failures.Add($"{service.Service}: iz '{trail.Name}' — belirteç '{token}' üretim kodunda tanımlı değil; iz hayalet");
                }

                // A marker interface nobody reads audits nothing. Require a MediatR pipeline behavior that names the
                // marker, and require that behavior to be named somewhere else (its registration).
                var marker = trail.RequiredGroups[0][0];
                var behaviors = pool
                    .Where(t => t.Kind == "class" && t.BaseNames.Contains("IPipelineBehavior")
                                && CSharpSourceScanner.ContainsIdentifier(t.Body, marker))
                    .ToList();

                if (behaviors.Count == 0)
                {
                    failures.Add($"{service.Service}: iz '{trail.Name}' — '{marker}' işaretini okuyan bir IPipelineBehavior yok; işaret hiçbir şey yazdırmıyor");
                    continue;
                }

                // The REGISTRATION must be in the service itself: a behavior that exists in Building.Blocks but that
                // this service never adds to its pipeline writes nothing for this service.
                var registered = behaviors.Any(behavior => service.Types.Any(other =>
                    other.Name != behavior.Name && CSharpSourceScanner.ContainsIdentifier(other.Body, behavior.Name)));

                if (!registered)
                {
                    failures.Add($"{service.Service}: iz '{trail.Name}' — {string.Join(", ", behaviors.Select(b => b.Name))} bu serviste kaydedilmiyor (DI kaydı bulunamadı)");
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

        // No "more than N commands" floor: a scan that silently found nothing leaves every pinned service without
        // commands, and DebtAndExceptionCounts… then fails on each of them by name — a floor that cannot go stale
        // when a service is retired.
        foreach (var service in measured)
        {
            if (service.Commands.Count > 0 && service.Ledger is null)
            {
                failures.Add($"{service.Service}: {service.Commands.Count} yazma komutu var, defteri yok → {AuditTrailMeasurement.LedgerDirectory}/{service.Service}.md oluştur");
            }

            if (service.Commands.Count == 0 && service.Ledger is not null)
            {
                failures.Add($"{service.Service}: defteri var ama taramada hiç yazma komutu bulunamadı — tarayıcı bu servisi göremiyor ya da servis boşaldı");
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
    /// A ledger line the parser cannot place is a line that silently stopped counting. <c>* X</c>, <c>-X</c>, an
    /// indented bullet, a <c>###</c> heading or an unknown <c>##</c> section each used to be skipped without a word.
    /// </summary>
    [Fact]
    public void LedgerFiles_AreReadableLineByLine_NothingIsSilentlySkipped()
    {
        var failures = AuditTrailMeasurement.All
            .Where(s => s.Ledger is not null)
            .SelectMany(s => s.Ledger!.Malformed)
            .ToList();

        Assert.True(failures.Count == 0, Report("Defterde okunamayan satır", failures));
    }

    /// <summary>
    /// ⚠ A TYPE NAMED <c>*Command</c> THAT PRODUCTION TREATS AS A QUERY. Platform's <c>AuditBehavior.IsQueryRequest</c>
    /// skips anything whose namespace contains <c>.Queries</c> — so a command filed there is NOT audited at runtime
    /// even when it carries <c>IAuditableCommand</c>. This measure counts it as a command (the name wins), and this
    /// test says the other half out loud: move it out of the `.Queries` namespace. Production's rule is not touched.
    /// </summary>
    [Fact]
    public void ACommandFiledUnderQueries_IsRefused_BecauseProductionSkipsItsAudit()
    {
        var failures = AuditTrailMeasurement.All
            .SelectMany(s => s.CommandsFiledAsQueries.Select(c => $"{s.Service}: {c}"))
            .ToList();

        Assert.True(failures.Count == 0, Report(
            "Adı 'Command' ile biten ama '.Queries' ad alanında duran istek. Üretimdeki sorgu kuralı (AuditBehavior.IsQueryRequest) "
            + "bunu sorgu sayar ve DENETİM KAYDINI YAZMAZ — işaret taşısa bile. Komutu '.Queries' ad alanından çıkar", failures));
    }

    /// <summary>
    /// THE SCANNER'S BLIND SPOTS, CAUGHT FROM THE OTHER SIDE. A request this measure cannot see — <c>IRequest</c>
    /// inherited from a base class, a request interface declared in another project, a <c>using</c> alias, a
    /// lower-case type name — still needs a handler, and the handler names it. So: every
    /// <c>IRequestHandler&lt;X, …&gt;</c> must name an X this measure counts as a command or a query.
    /// </summary>
    [Fact]
    public void EveryRequestHandler_HandlesARequestThisMeasureCanSee()
    {
        var failures = new List<string>();
        var seenExemptions = new HashSet<string>(StringComparer.Ordinal);

        foreach (var service in AuditTrailMeasurement.All)
        {
            foreach (var unresolved in service.UnresolvedHandlers)
            {
                var key = $"{service.Service}: {unresolved.Split(' ')[0].TrimEnd(':')}";
                if (GenericHandlerExemptions.ContainsKey(key))
                {
                    seenExemptions.Add(key);
                    continue;
                }

                failures.Add($"{service.Service}: {unresolved} — bu isteği ölçüm GÖRMÜYOR (IRequest taban sınıftan mı geliyor, başka projede mi, takma ad mı, küçük harfle mi başlıyor?). "
                             + "İsteği doğrudan IRequest uygulayan bir tür yap; jenerik bir handler ise gerekçesiyle GenericHandlerExemptions'a yaz");
            }
        }

        failures.AddRange(GenericHandlerExemptions.Keys.Where(key => !seenExemptions.Contains(key))
            .Select(key => $"{key} — muafiyet artık hiçbir handler'a karşılık gelmiyor: kaldır"));

        Assert.True(failures.Count == 0, Report("İsteği ölçülemeyen handler", failures));
    }

    /// <summary>
    /// Platform's <c>AuditBehavior</c> skips a request whose NAME contains one of a few fragments
    /// (<c>AuditBehaviorOptions.AutoExcludedRequestNameFragments</c>, read from production here) — exclusion wins
    /// over the marker. A marked command with such a name looks audited in the source and writes nothing.
    /// </summary>
    [Fact]
    public void MarkedCommands_AreNotSilentlyExcludedByName()
    {
        var fragments = AuditTrailMeasurement.PlatformAutoExcludedNameFragments();
        Assert.True(fragments.Count > 0, "AuditBehaviorOptions.AutoExcludedRequestNameFragments üretim kodundan okunamadı — dosya ya da üye adı değişti.");

        var platform = AuditTrailMeasurement.All.Single(s => s.Service == "Diten.Platform");
        var failures = platform.Commands
            .Where(c => c.Evidence.Any(t => t.IsAccepted && t.Kind == TrailKind.Marker))
            .SelectMany(c => fragments
                .Where(fragment => c.Name.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                .Select(fragment => $"Diten.Platform: {c.Name} — adı '{fragment}' içeriyor; AuditBehavior bu adı DIŞLAR ve kayıt yazılmaz. Komutun adını değiştir"))
            .ToList();

        Assert.True(failures.Count == 0, Report("İşaretli ama adı yüzünden denetimden dışlanan komut", failures));
    }

    /// <summary>
    /// K2 (owner decision 2026-10-02): for identity / authorization / tenant-state / GxP / KVKK-special writes, a
    /// record that cannot be written stops the operation. No path delivers that today, so the commands of that
    /// class that ARE audited — but best-effort — are listed per service under "## K2 borcu". Same latch as debt.
    /// </summary>
    [Fact]
    public void K2Debt_NamesAuditedCommands_AndOnlyShrinks()
    {
        var failures = new List<string>();

        foreach (var service in ServicesWithCommands.Where(s => s.Ledger is not null))
        {
            var byName = service.Commands.ToLookup(c => c.Name, StringComparer.Ordinal);

            foreach (var duplicate in service.Ledger!.K2Debt.GroupBy(x => x, StringComparer.Ordinal).Where(g => g.Count() > 1))
            {
                failures.Add($"{service.Service}: {duplicate.Key} — K2 borcunda iki kez");
            }

            foreach (var entry in service.Ledger.K2Debt.Distinct(StringComparer.Ordinal))
            {
                if (!byName.Contains(entry))
                {
                    failures.Add($"{service.Service}: {entry} — böyle bir yazma komutu yok: K2 borcundan çıkar");
                }
                else if (!byName[entry].Any(c => c.IsAudited))
                {
                    failures.Add($"{service.Service}: {entry} — denetlenmiyor; yeri '## Bilinen borç', K2 borcu değil (K2 borcu = denetlenen ama kayıt yazılamayınca durmayan komut)");
                }
            }
        }

        Assert.True(failures.Count == 0, Report("K2 borcu listesi güncel değil", failures));
    }

    /// <summary>
    /// A QUERY WHOSE HANDLER WRITES is invisible to every list above: the query rule files it away. Measured
    /// 2026-10-02 in Platform. The set is a latch — a query handler that calls a repository write method must be in
    /// "## Yazan sorgular", and an entry that no longer writes must leave.
    /// <para>⚠ The detector is narrow on purpose (a call on a `…Repository / …Store / …Collection` to a
    /// `Create|Update|Delete|Insert|Replace|Upsert|Save|Remove|Append|Add|Ensure|Set…` method). A handler that writes
    /// through a service, or through a field named `_tenants`, is NOT seen.</para>
    /// </summary>
    [Fact]
    public void QueriesThatWrite_AreListed_AndNewOnesAreRefused()
    {
        var failures = new List<string>();

        foreach (var service in AuditTrailMeasurement.All)
        {
            var listed = (service.Ledger?.WritingQueries ?? []).ToHashSet(StringComparer.Ordinal);
            var queryNames = service.Queries.Select(q => q.Name).ToHashSet(StringComparer.Ordinal);

            failures.AddRange(service.WritingQueryHandlers.Where(q => !listed.Contains(q))
                .Select(q => $"{service.Service}: {q} — SORGU İŞLEYİCİSİ DEPOYA YAZIYOR ve listede yok. Sorgu yazmaz: yazmayı bir komuta taşı (denetlenir) — '## Yazan sorgular' yeni satır kabul etmez"));
            failures.AddRange(listed.Where(q => !queryNames.Contains(q))
                .Select(q => $"{service.Service}: {q} — '## Yazan sorgular' satırı var ama böyle bir sorgu yok: kaldır"));
        }

        Assert.True(failures.Count == 0, Report("Yazan sorgu", failures));
    }

    /// <summary>
    /// A type named <c>*Command</c> that is not a MediatR request is dispatched some other way (an application
    /// service, a minimal-API endpoint). Measured 2026-10-02: exactly two services have such commands, and by CT
    /// decision they are measured BY NAME (<see cref="AuditTrailMeasurement.MeasuredByName"/>). A THIRD service
    /// growing a non-MediatR command would be invisible to every test above, so it fails here.
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
    /// Prints the inventory. It asserts nothing about the numbers (DebtAndExceptionCounts… does) — it exists so the
    /// inventory document quotes THIS measurement instead of running a second count:
    /// <c>dotnet test --logger "console;verbosity=detailed" --filter Inventory</c>.
    /// </summary>
    [Fact]
    public void Inventory_PrintsTheTableTheInventoryDocumentQuotes()
    {
        var table = new StringBuilder();
        table.AppendLine("| servis | yazma komutu | a | b | c | istisna | borç | borçta aday izi olan | K2 borcu | yazan sorgu | handler bulunamayan | MediatR dışı |");
        table.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|");

        var totals = new int[11];
        foreach (var service in AuditTrailMeasurement.All.Where(s => s.Commands.Count > 0))
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
                service.Ledger?.K2Debt.Count ?? 0,
                service.WritingQueryHandlers.Count,
                service.Commands.Count(c => !c.HasHandler),
                service.Commands.Count(c => !c.IsMediatR)
            ];

            for (var i = 0; i < row.Length; i++)
            {
                totals[i] += row[i];
            }

            table.AppendLine($"| {service.Service} | {string.Join(" | ", row)} |");
        }

        table.AppendLine($"| **TOPLAM** | {string.Join(" | ", totals)} |");
        output.WriteLine(table.ToString());

        var unresolved = AuditTrailMeasurement.All.SelectMany(s => s.UnresolvedHandlers.Select(u => $"{s.Service}: {u}")).ToList();
        output.WriteLine($"İsteği çözülemeyen handler: {unresolved.Count}");
        unresolved.ForEach(output.WriteLine);

        Assert.Equal(totals[0], totals[1] + totals[2] + totals[3] + totals[4] + totals[5]);
    }

    // ── The weak form of a writer trail (a class reached through `var`) ────────────────────────────────────────
    // Measured 2026-10-02: the token WorkflowTaskTransitionSupport.CancelAsync credited every Platform handler that
    // called `.CancelAsync(` on ANYTHING — eight commands, among them the timesheet withdrawal that only calls its
    // own approval service. A method name is proof only when one type in the service declares it.

    private static SourceType Type(string kind, string name, string body) =>
        new($"{name}.cs", kind, name, "N", false, [], body);

    [Fact]
    public void WeakWriterForm_IsRefused_WhenSeveralTypesDeclareThatMethodName()
    {
        var pool = new List<SourceType>
        {
            Type("class", "WorkflowSupport", "public Task CancelAsync(Guid id) { return Task.CompletedTask; }"),
            Type("class", "ApprovalService", "public async Task CancelAsync(Guid id) { await Task.Yield(); }"),
            Type("class", "Handler", "public async Task Handle() { await _approvals.CancelAsync(id); }")
        };

        Assert.False(AuditTrailMeasurement.CallsWriteMember(pool[2].Body, "WorkflowSupport.CancelAsync", pool));
    }

    [Fact]
    public void WeakWriterForm_IsAccepted_WhenThatClassAloneDeclaresTheMethod_AndCallsAreNotDeclarations()
    {
        var pool = new List<SourceType>
        {
            Type("class", "TaskItem", "public void Declare(TaskTransitionKind kind) { }"),
            // Three CALL shapes — none of them is a declaration, so they must not make the name look shared.
            Type("class", "HandlerA", "public async Task Handle() { var task = await _tasks.GetAsync(id); task.Declare(kind); }"),
            Type("class", "HandlerB", "public Task Handle() { return Declare(kind); }"),
            Type("class", "HandlerC", "public async Task Handle() { await Declare(kind); }")
        };

        Assert.True(AuditTrailMeasurement.CallsWriteMember(pool[1].Body, "TaskItem.Declare", pool));
        Assert.True(AuditTrailMeasurement.DeclaresMethod(pool[0], "Declare"));
        Assert.False(AuditTrailMeasurement.DeclaresMethod(pool[1], "Declare"));
        Assert.False(AuditTrailMeasurement.DeclaresMethod(pool[2], "Declare"));
        Assert.False(AuditTrailMeasurement.DeclaresMethod(pool[3], "Declare"));
    }

    [Fact]
    public void WeakWriterForm_IsRefused_WhenTheOnlyDeclarerIsAnotherType()
    {
        // The name is unique in the service, but it is ANOTHER type's method: a call to it says nothing about the
        // token's type.
        var pool = new List<SourceType>
        {
            Type("class", "TaskItem", "public void Rename(string title) { }"),
            Type("class", "Meeting", "public void Declare(string kind) { }"),
            Type("class", "Handler", "public async Task Handle() { var meeting = await _meetings.GetAsync(id); meeting.Declare(kind); }")
        };

        Assert.False(AuditTrailMeasurement.CallsWriteMember(pool[2].Body, "TaskItem.Declare", pool));
    }

    [Fact]
    public void WeakWriterForm_IsNeverAccepted_ForAnInterface_AndTheTypedFormStillIs()
    {
        var pool = new List<SourceType>
        {
            Type("interface", "IAuditRecorder", "Task RecordAsync(string what);"),
            Type("class", "Typed", "private readonly IAuditRecorder _recorder; public Task Handle() => _recorder.RecordAsync(\"x\");"),
            Type("class", "Untyped", "public Task Handle() => something.RecordAsync(\"x\");")
        };

        Assert.True(AuditTrailMeasurement.CallsWriteMember(pool[1].Body, "IAuditRecorder.RecordAsync", pool));
        Assert.False(AuditTrailMeasurement.CallsWriteMember(pool[2].Body, "IAuditRecorder.RecordAsync", pool));
    }

    private static string Report(string headline, List<string> failures) =>
        failures.Count == 0
            ? string.Empty
            : $"{headline} ({failures.Count}):\n  - {string.Join("\n  - ", failures.Take(60))}"
              + (failures.Count > 60 ? $"\n  … ve {failures.Count - 60} tane daha" : string.Empty)
              + WhatToDo;
}
