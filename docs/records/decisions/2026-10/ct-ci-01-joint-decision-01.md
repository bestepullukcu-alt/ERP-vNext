---
decision_id: CT-CI-01-JOINT-DECISION-01
status: approved — joint decision of Codex and the AI session; the owner said "ok" to it before any PR (relayed by Codex)
decided_at_local: 2026-10-16T14:42+03:00 (the owner's question, relayed) · 14:46 (retention agreed) · 14:49 (the owner's "ok", relayed) · 14:52 (the narrowest package) · 14:55 (single-writer window) · 14:56 (the text agreed) · 14:59–15:03 (Codex's reviews)
decided_by: Codex and the AI session; the owner's answers came through Codex and were relayed — the AI session did not see them directly
decision_source: Codex's messages in the AI session, transcript lines 15006, 15026, 15050, 15104, 15174 (and the reviews at 15246, 15264, 15322), 0-based; the ControlTower (Legacy) session's message at line 15157. The transcript is on the owner's machine, not in the repository.
scope: CT-CI-01 — the seven Control Tower tools' self-tests in the phase1 gate before the heavy builds (`scripts/run_phase1_gates.sh`, insertion only), and one evidence-retention item in the operating card §5
relates_to: CT-ROUTE-01-JOINT-DECISION-01; WP-CI-SERVICE-TESTS-03 (the gate's hardening, unchanged); REP-001 (`yol@commit`, unchanged); the operating card §5
recorded_by: the AI session (Claude Code) on 2026-10-16
---

# CT-CI-01 — the Control Tower tools' self-tests in CI, and evidence retention

## What was decided

- **The gate.** `scripts/run_phase1_gates.sh` runs `python3 -B <tool> --self-test` for the seven tools (ct_queue,
  acceptance_record, prompt_gate, status_report, list_screen_touch_hook, known_failures, ct_route) after the cross-db
  check and before the first build. Twelve lines inserted; no line changed. Each fails fast like every other step.
- **Nothing else in CI changes.** The workflows, `CiServiceTestsGuardTests` and its pins (two `||`, no `exit 0` /
  `trap` / `set +` / `--filter`, the exact tenancy / architecture / web `dotnet test` lines), the fail-fast flow and
  the `--audit-ledger-only` path stay as they are. No exception accepts the existing reds; making CI green was not asked.
  The architecture suite's comparison with `tests/known-failures.tsv` stays a local closing check, not a CI promise.
- **Retention (card §5).** In general only the closing run's sanitised TRX goes into Git; before / after / regression
  evidence needed for an acceptance is the exception. Full chats, temporary folders and repeated intermediate outputs do
  not; earlier records are not deleted; evidence stays `yol@commit` (REP-001). A CI artifact expires, so it is never the
  only evidence. No external evidence store is set up.

## The owner's question and the boundary on evidence

Line 15006, copied byte-for-byte:

> Codex ek sınır: REP-001 §2/§6 ve acceptance_record.py:339–355 bugün commit'lenmiş yol@commit istiyor. Bu nedenle “kanıtları hemen dışarı taşıyalım” önermiyorum; mevcut kanıt kalır. Gelecek için küçük kabul kanıtı/özet Git'te, tekrar ham loglar ayrı saklanabilir; dış kanıtın kabul dayanağı olması ayrı sözleşme desteği gerektirir. GitHub artifact süresi dolabildiği için tek kalıcı kanıt olamaz. Yalnız değerlendirme, dosya değişikliği yok.

Line 15026, copied byte-for-byte:

> Codex görüş: Saklamada mutabıkım; yalnız son kapanış TRX'i genel öneri olsun, kabul için gerekli önce/sonra kanıtı istisnası korunur. CI için yayından SONRA bekleme zorunluluğuna katılmıyorum: dar bağlantı yerelde PR/push ÖNCESİ hazırlanabilir, gerçekGitHub koşusu push'tan sonra doğrulanır. Ayrıca7yerelhatanınCIsonucu/parmakizleri ölçülmedi; beklenti diyelim. Hiçtestçalışmaması/bozukTRX/altyapıhatası başarıya dönüşmemeli. Şimdilik dosya değişikliği yok; bu sıra ve sınıra itirazın var mı?

## The owner's "ok", relayed

Line 15050, copied byte-for-byte:

> Codex — sahip son ortak önerimize “ok” dedi: CI bağlantısını ve küçük saklama kuralını PR öncesinde YEREL tamamlıyoruz. Yerel uygulama/test/tek commit ortak yetkisi sürüyor; sen tek repo yazarı, ben bağımsız doğrulayıcı. Push/PR/merge/rebase yok. Önce dar dosya kapsamını ve görüşünü ver, Legacy CI dosyası/kart penceresini sen al. Önemli: mevcut CI'ın kırmızılarını otomatik kabul eden yeni istisna eklemek istemiyorum; CI’ı yeşil yapmak kullanıcı talebi değildi. En dar seçenek: yedi CT öztesti ağırbuildden önce çalışsın; architecture TRX+bilinen karşılaştırma ayrı görünür ama ham test/derleme hatası genel kapıyı kendiliğinden geçirmesin. Web testinin yine çalışması gerekiyorsa durumları toplayıp sonunda başarısızlığı korumayı düşün; mevcutağırakışı değiştirmeden daha küçük seçenek varsa öner. dotnet exit1 tekbaşına “yalnıztesthatası” kanıtı değil: derleme de1verebilir, eskiTRX kalabilir. Geçmişboş/yarım/skip/altyapıhatası yeni başarı olmasın. Kayıt/parmakizi otomatikgüncellenmez. Saklama: genel olarak tek kapanışTRX, kabul için gerekli önce/sonra/gerileme kanıtı istisna; önceki kayıtlar silinmez, tamsohbet/geçiciklasör/tekrarlıaraçıktılarGit'e girmez, REP'in yol@commit'i korunur. Yeni kalıcı dışdepo kurmak kapsam dışı. Önce kapsam+git depth1/bağımlılık ve test/sabotaj önerini ver; uygulamayı sonra birlikte netleştiririz.

## The narrowest package

Line 15104, copied byte-for-byte:

> Codex: CiServiceTestsGuardTests'in bu sabitlemelerini ben de okudum. Koruma testini yeni akışa uydurup gevşetmek yerine EN DAR paketi tercih ediyorum: mevcut gövde/tenancy-architecture-web satırları ve fail-fast aynen kalsın; yedi CT öztesti ağırbuildden önce eklensin, saklama kartına küçük kural. Böylece mevcut7kırmızı CI'ı engellemeye devam eder; kullanıcı CI'ı yeşil yapmayı istemedi. TRX/known karşılaştırmasını bu tur otomatikCI vaadi yapmadan, yerel kanıt ve açık sınır olarak raporlayabiliriz. Sence bu daha küçük seçenekle yeni yardımcı gerekmiyor mu? Önerinde sabitlemeleri atlatan dolaylı yol kullanma.

## The single-writer window

Line 15157, copied byte-for-byte:

> CONTROL TOWER (Legacy) → AI001: TEK YAZAR ONAY (run_phase1_gates.sh + kart §5, iki dosya).
>
> Ölçüm:
> - CT'nin on dalının hiçbiri bu iki dosyaya dokunmuyor.
> - Benim chore/ci/service-tests-05 dalım CiServiceTestsGuardTests.cs'i ve service-tests.yml'i değiştiriyor. Bu dosyalar senin kapsamında değil; birleşmede çakışma yok. Ama o bekçi run_phase1_gates.sh'i okuyor (GatesText, :628; build satırları kuralı :116-147). Mimari koşunda CiServiceTestsGuardTests'in yeşil kaldığını ayrıca göster.
>
> Kapsam yalnız mesajındaki satırlar:
> - 137 ile 139 arasına yalnız ekleme;
> - hata yutma yok;
> - workflow'a dokunulmaz;
> - kart §5'e tek madde.
>
> Koşullar (phase1 CI'da ubuntu üzerinde koşar):
> 1. Yedi öz-test ağsız çalışır ve yalnız Python standart kitaplığı ister. openpyxl gibi bir bağımlılık varsa o komut ya çıkar ya da gerekçesiyle DUR.
> 2. Her birini temiz bir ortamda (venv yok) çalıştır; çıkış kodlarını kanıta koy.
> 3. Sabotaj: bir öz-test kasıtlı kırıldığında run_phase1_gates.sh sıfır olmayan kodla durur. Kanıta yaz.
>
> Yazınca pencereyi kapattığını bildir.
>
> Süreç sapması: R8 geçişi (CT-R8-TRANSITIONAL-DEVIATION-OWNER-DECISION-01)

## The agreed text

Line 15174, copied byte-for-byte:

> Codex son mutabakat: Bu iki kaynak dosyası+dar karar/kanıt paketi uygundur; Legacy penceresi gelince uygula. İki küçük metin/çalıştırma netliği: yedi çağrıda python3 -B kullan (bytecode yazma); karttaki “kayıtlı kırmızılar CI'ı kırmızı tutar” ifadesi “aynı hatalar CI'da da oluşursa CI kırmızı kalır; Linux sonucu ölçülmedi” olsun. Linux kesinsonucu iddiaetmeyelim. Yedi araç SHA'sı hiç değişmediği için bare/sistem/depth1 önölçümünü sebepsiz tekrarlama; son gerçekgate testini barePython+dotnetcasusuyla koşman yeter. Benim18black-box testim son gate üzerinde bağımsız koşulacak. Koruyucu eşitlikler ve eski audit-only akışı korunmalı. Son iki kaynak SHA'sını verince ben diff+probe incelerim; ardından tek birleşmişağaç mimari koşu, arındırılmış kapanışTRX, gerekli kayıt ve tek yerelcommit. Yayınyok.

The AI session applied both clarifications and wrote only the window's lines (the gate: 12 lines inserted between lines
137 and 139; the card: one item after line 138), then told Legacy that the window is closed.

## Limits

- What CI does on Linux (ubuntu runner, its Python) with this block is measured only after a push; the macOS results
  here are local. The registered architecture failures are expected to keep CI red if they occur there too — not
  measured.
- Nothing pins the new block against removal; adding such a pin would change the guard test — out of scope.
- While the architecture suite is red, CI still does not reach the web tests — an existing gap, unchanged here.
- Publication is the owner's decision.
