# CONTROL TOWER — Daily Operating Card v1.7

> **Purpose:** günlük Control Tower turu için hızlı gate. Yeni authority yaratmaz. Ayrıntı için [CONTROL TOWER SOP](./control-tower-sop.md) (v2.5 r3).

## 0. Yeni Control Tower açılışı

> Sahip kararı 2026-10-15 (`CT-STEP2-STARTER-KIT-OWNER-DECISION-01`). Yalnız bu karardan sonra açılan Control Tower'lar
> için. Eski Control Tower'lar ve onaylı işleri geriye dönük değişmez, durdurulmaz.

- [ ] **Araç bağımlılığı:** `python3 -m pip install -r scripts/requirements-tools.txt`. Kütüphane yoksa keşif
  "tamamlanamadı" der (çıkış 2).
- [ ] **Görev kaynağı:** [`ct-queue-template.tsv`](../../../execution/delivery/ct-queue-template.tsv)'den başlar. Kayıt
  satırı ([`ct-queue-registry.tsv`](../../../execution/delivery/ct-queue-registry.tsv)) `vocabulary` = `sop7`,
  `strict` = `yes` ile, sahip onayıyla `main`'e girer. `python3 scripts/ct_queue.py validate --queue <kuyruk>` 0 hata.
- [ ] **Durumlar:** sözlük `sop7` = SOP §7'nin 13 ana + 6 istisna durumu. İzinli geçişler ve her geçişin kanıtı
  [`ct-sop7-transitions.tsv`](../../../execution/delivery/ct-sop7-transitions.tsv)'de; `öneri` işaretli satırlar SOP'de
  açıkça yazmayan, sahibin onayladığı geçişlerdir. Son durumlar (çıkış yok): ACCEPTED, REWORK, FAILED, SUPERSEDED,
  CANCELLED. Dağıtılabilir durum: yalnız READY.
- [ ] **`module` sütunu:** `MOD-nnnn`, `MOD-nnnn-FUn` ya da `ortak:<MOD listesi | kapsam yolu>`. Kayıt dosyasında
  olmayan kimlik ve kullanımdan kalkmış takma ad reddedilir.
- [ ] **Kalite kapısı (CT-PQG-01, SOP §20.2):** her uygulama promptu dağıtımdan önce
  `python3 scripts/prompt_gate.py check <prompt> --queue <kuyruk>` ile çıkış 0 alır; HIGH için `--review <kayıt>@<commit>`.
  Kapı ve kuyruk kaydı `main`'de değilken yalnız `--prepare` çalışır ve "DAĞITIM İZNİ DEĞİL" der. Elle denetim
  kapının yerine geçmez.

### 0.1 Keşif — her yeni iş için (DISCOVERED → INSPECTION_REQUIRED)

```text
python3 .antigravity/scripts/verify_module_id.py --discover "<ad>" [--resource <gateway kaynağı>] [--sor "<SoR nesnesi>"] > <kanıt yolu>
python3 .antigravity/scripts/verify_module_id.py --check-report <kanıt yolu>
```

- Araç raporu yalnız ekrana yazar; Control Tower onu tek yönlendirmeyle iş paketinin kanıt yoluna kaydeder.
- Rapor: aracın kimliği (commit + betiğin SHA-256'sı), sorgu, `origin/main` SHA'sı ve son fetch zamanı, incelenen
  kaynakların kimlikleri, eksikler, sonuç ve çıkış kodu.
- Raporun son satırı `rapor-sha256: <hex>`. Değer, bu satır **hariç** önceki bütün satırların `\n` ile birleştirilip
  sonuna bir `\n` eklenmiş UTF-8 içeriğinin SHA-256'sıdır. `--check-report` aynı kuralla yeniden hesaplar: 0 = geçerli,
  2 = bozuk ya da değiştirilmiş.
- Sonuç: 0 = "incelenen kaynaklarda eşleşme bulunamadı" (işin yeni olduğunu kanıtlamaz; rapordaki eksikler geçerlidir),
  3 = "inceleme gerekli", 2 = "tamamlanamadı".
- İş paketi kaydındaki keşif satırı, sabit biçim:

  ```text
  Keşif: <sonuç> · rapor <yol>@<commit> · sha256 <rapor-sha256>
  ```

- **"İnceleme gerekli":** dağıtımdan önce iş paketi kaydına her bulgu için (1) karar — parent, FU, child ya da gerçekten
  yeni; (2) çakışmanın neden yeni işi engellemediği ya da mevcut işin nasıl kullanılacağı yazılır. Çözülmemiş tek bir
  bulgu varken dağıtım yok.
- Keşif çalıştırılmamış ya da "tamamlanamadı" → CT-PQG-01'de engelleyici. Yalnız yeni Control Tower'lar için.

### 0.2 Yeniden işleme

- **Y1. Yeni satır, eski durum korunur.** REWORK, FAILED ve kabulden sonra bulunan hata eski bir duruma dönmez. Yeni
  `<kimlik>-R<n>` satırı READY'de açılır; `depends_on` asıl kimliği içerir, iş metni kök nedeni yazar. Asıl satır ve
  raporu üzerine yazılmaz; kabul edilmiş satır ACCEPTED kalır (SOP §19.2).
- **Y2. Esaslı değişiklik yeni iş paketidir.** Yeni bir kimlik verilir. Asıl satır **sonlanmamışsa** SUPERSEDED olur;
  ACCEPTED ya da başka bir son durumdaysa olduğu gibi kalır ve yeni iş `depends_on` ile ona bağlanır (SOP §19.1, §19.3).
- **Y3. İki tur sınırı.** Aynı kök neden iki ardışık turda kapanmazsa `docs/records/decisions/` altında bir
  ARCHITECTURE_REASSESSMENT_REQUIRED karar/engel kaydı açılır; bu bir kuyruk durumu değildir (SOP §28.2). Kayıt
  çözülmeden yeni `-R` satırı açılmaz ve dağıtılmaz. `-R3` ve sonrası, `record` sütununda bu kaydı gösterir.
- **Y4. `-R` satırı da kapılardan geçer:** dağıtımdan önce DoR (§1) ve CT-PQG-01.

`ct_queue.py validate` katı kuyrukta mekanik olarak denetler: durum adlarının sözlükte olması, son durumdan çıkış
(kuyruk dosyasının git geçmişindeki ardışık sürümler arasında), `-R<n>` satırının var olan asıl kimliğe bağlı olması ve
`depends_on`'da onu göstermesi, `-R3` ve sonrası için karar kaydı, `module` sütunu. Atlanmış adım anlık görüntülerden
çıkarılamaz; kanıtların varlığını denetlemek CT-OPS-03'ün işidir.

## 1. START / DoR — dispatch etmeden önce

- [ ] **Identity + owner:** canonical module ID/name ve owner/SoR açık.
- [ ] **Pack authority:** gerekli Module Pack `approved/ready-for-dev`; cross-module ise DCP boundary açık.
- [ ] **Contract:** required API/DTO/event/permission/evidence contract mevcut; invention yok.
- [ ] **Dependencies:** `SATISFIED` veya kayıtlı `WAIVED`.
- [ ] **Scope:** allowed paths + protected paths açık.
- [ ] **Git isolation:** repo / branch / expected HEAD / worktree / dirty baseline ölçüldü.
- [ ] **Execution:** Profile A/B/C + Agent Lane ID/Type + risk class açık.
- [ ] **Target agent:** entry point §6'dan seçildi; o ajanın §17.4 zorunlu giriş alanları dolu; paste-ready prompt (§36.1) hazır.
- [ ] **Acceptance:** golden/contract/inspection flow + measurable AC + required evidence level açık.
- [ ] **Görev kaynağı satırı (R8, r3):** WP'nin satırı, dağıtan Control Tower'ın kayıtlı görev kaynağında dağıtımdan önce var (kayıtlı = `execution/delivery/ct-queue-registry.tsv`'de `main`'de geçerli bir satır; SCE / MVP6: `docs/roadmap/plans/mvp6-process-pilot-01/CT-QUEUE.tsv`). Kayıtlı kaynak yoksa: 30 günlük geçiş süresinde yalnız `CT-R8-TRANSITIONAL-DEVIATION-OWNER-DECISION-01` kapsamında (kararı aldığını doğrulamış, onaylı WP / Module Pack, ayrı geçiş referansı); süre bitince yalnız bitişten önce onaylanmış WP'ler, sonra onaylananlar için kayıtlı kaynak ya da sahibin süreli istisnası. Değilse yeni dağıtım yok; çalışan iş durdurulmaz (SOP §0.1 r3, §17.1, §20.1).
- [ ] **Kuyruk kontrolü (yardımcı; zorunlu kapı değil):** `python3 scripts/ct_queue.py require <WP-ID> --queue <kuyruk>` — çıkış 1 = satır yok, aynı kimlikle birden çok satır ya da durum dağıtılabilir değil. Kuyruklar [`ct-queue-registry.tsv`](../../../execution/delivery/ct-queue-registry.tsv)'de; kayıtlı kuyruğu olmayan Control Tower için dağıtım kendiliğinden serbest değildir. Kuyruğa yazmadan önce `guard` (kilit değildir); dallar arası kimlik çakışması için `writers`.

**Bir kritik madde FAIL → implementation dispatch yok.**

> **R8 (SOP r3; kararlar `CT-R8-REGISTERED-TASK-SOURCE-OWNER-DECISION-01`, `CT-R8-TRANSITION-PERIOD-OWNER-DECISION-01`):** her Control Tower kendi kayıtlı
> görev kaynağını kullanır; SCE / MVP6 kuyruğuna başkası yazmaz; §36.2 T1–T4 SCE / MVP6'nındır, diğerleri §36.1'i
> kullanır. Yürürlük: r3 `main`'e girdiğinde; 30 günlük geçiş süresi o gün başlar. O zamana kadar v2.5 r2 ve geçiş
> kararı (yalnız onu aldığını doğrulayan Control Tower'lar için).

## 2. PARALLEL / Agent Lane gate

- [ ] Shared contract/migration/permission/global route/shared registration için **single writer**.
- [ ] Parallel Agent Lane'ler ayrı worktree + disjoint scope kullanıyor.
- [ ] `Parallel-safe with` **ölçüldü**, varsayılmadı.
- [ ] Integration order açık.
- [ ] Güvenli paralellik varsa mümkünse **2+ executable WP** hazır.

## 3. DISPATCH PREFLIGHT

```text
WP / Prompt ID + version
PQG: python3 scripts/prompt_gate.py check <prompt> --queue <kuyruk> → çıkış 0 (HIGH: --review; SOP §20.2)
Agent Lane ID / Type
Target agent / entry point
Repository / Branch / HEAD / Worktree
Depends on / Parallel-safe with
Allowed paths / Protected paths
```

Aşağıdakilerde STOP: wrong branch/HEAD, overlapping dirty files, draft pack, missing contract, ownership conflict, unplanned migration, **agent/scope mismatch**.

## 4. EVIDENCE LEVEL

| Level | Evidence | Meaning |
|---|---|---|
| E0 | Agent narrative | claim only |
| E1 | Static code/config inspection | readiness |
| E2 | Build/unit/contract tests | implementation confidence |
| E3 | Runtime API/UI smoke | runtime behavior |
| E4 | + persistence/RBAC/tenant/audit/failure paths | state-changing acceptance |
| E5 | + cross-module integrated runtime / restart-compatibility where required | capability/integration acceptance |

**Required level yoksa Agent PASS → CT ACCEPTED değildir.**

## 5. VERIFY

- [ ] Runtime freshness: process start > binary timestamp.
- [ ] Critical fix: mümkünse `fix absent → RED`, `fix present → GREEN`.
- [ ] Producer + consumer birlikte ölçüldü.
- [ ] State-changing ise UI/API + persisted state + audit/observability.
- [ ] RBAC + tenant + concurrency + idempotency applicable kontrolleri.
- [ ] Cross-module ise Integration Gate.
- [ ] Bilinen kırmızılar: koşu TRX ile alınır (`dotnet test … --logger "trx;LogFileName=<dosya>"`) ve `python3 scripts/known_failures.py check <trx> --suite <ad>` ile `tests/known-failures.tsv`'ye karşı okunur. Çıkış 0 = bu karşılaştırmada yeni regresyon yok (testler geçti ya da kabul edildi demek değildir): kayıtlı kırmızılar aynı, ya da KISMEN-DÜZELDİ — kayıtlı öğelerin bir kısmı gitti, yeni öğe yok; kayıt (`tests/known-failures.tsv` + `tests/known-failures-items.json`) elle güncellenir; 1 = YENİ ya da YENİ-AYRINTI → regresyon, STOP; 2 = koşu ya da kayıt güvenilir değil (boş, bozuk, eksik, iptal, hiç test koşmamış, TSV ile JSON uyuşmuyor) → STOP; 3 = konsol çıktısı, ayrıntı doğrulanmadı → kabul için yetmez. Yerel tekrar tanıdan önce `should-rerun --suite <ad>`: ATLA yalnız adı verilen kayıtlı testler içindir — süitin geri kalanı, CI, zorunlu kabul, güvenlik testleri ve sahibin açıkça istediği testler atlanmaz.

## 6. CLOSE / REPLAN

- [ ] Agent Verdict ayrı, Verification Verdict ayrı, CT Status ayrı.
- [ ] Backlog / Module Pack / Seam / DCP / Delivery Board applicable kayıtları güncellendi.
- [ ] Decision / waiver / intentionally-not-done kaydedildi.
- [ ] Dependency gates yeniden hesaplandı.
- [ ] Newly-unblocked work belirlendi.
- [ ] Güvenli paralellik varsa sonraki **2+ WP** üretildi; yoksa neden yazıldı.

```text
Commit ≠ Acceptance
Isolated PASS ≠ Integrated PASS
Agent PASS ≠ CT ACCEPTED
Chat ≠ System of Record
Replanning = Closure
```

## 7. Kalıcı onaylar ve açık onay sınırları

> Sahip kararı 2026-10-15 (CT-OPS-01). "Kalıcı" yalnız önceden yetkilendirilmiş kapsam içindir. Bu tablo GIT-002, XMC-001,
> `AGENTS.md` ve SOP'yi değiştirmez; çelişkide onlar kazanır.

| İşlem | Onay | Kaynak |
|---|---|---|
| Salt-okunur inceleme ve ölçüm | Kalıcı — tekrar sorulmaz | Sahip kararı 2026-10-15 |
| REP-001'e uygun rapor üretme | Kalıcı — tekrar sorulmaz | [REP-001](../../../.antigravity/rules/status-reporting-and-evidence.md); sahip kararı |
| Ortak test kilidine uyarak, onaylı kapsamda test çalıştırma | Kalıcı — tekrar sorulmaz | Sahip kararı 2026-10-15 |
| Kendi geçici çalışma dosyalarını kullanma | Kalıcı — tekrar sorulmaz | Sahip kararı 2026-10-15 |
| Açıkça onaylanmış iş paketi için dal ve worktree oluşturma | Kalıcı — tekrar sorulmaz | [GIT-002](../../../.antigravity/rules/git-safety.md) §1; sahip kararı |
| Commit, push, PR ve merge | **Her seferinde açık onay** | [GIT-002](../../../.antigravity/rules/git-safety.md) §4, §5 |
| Yıkıcı Git işlemleri | **Her seferinde açık onay** | [GIT-002](../../../.antigravity/rules/git-safety.md) §6 |
| `.antigravity/**` değişiklikleri | **Her seferinde açık onay** | [AGENTS.md](../../../AGENTS.md) §4 |
| Başka modüle müdahale | **Her seferinde açık onay** | [XMC-001](../../../.antigravity/rules/cross-module-change-consent.md) |
| Mimari veya kapsam değişikliği | **Her seferinde açık onay** | SOP §4.1, §19.4 |
| Canlı ortam müdahalesi | **Her seferinde açık onay** | Sahip kararı 2026-10-15 |
| Ortak dosyada tek-yazar anlaşması | **Her seferinde**, etkin Control Tower ile | SOP §16.4, K15 |

## 8. Domain → Control Tower sahipliği (yalnız kanıttan)

> Kanıt = depo yolu@commit. Kanıt yoksa satır "Belirsiz — sahip ataması bekliyor" yazar; sahip kendiliğinden atanmaz.
> Ölçüm: 11 `execution/domains/*/domain-config.md` dosyasının hiçbiri Control Tower sahibi tanımlamıyor (2026-10-15, `6b8a62d5a`).

| Domain | Control Tower | Kanıt |
|---|---|---|
| supply-chain-execution | MVP6 Control Tower (CT-QUEUE) — **kısmi**: MOD-0147, 0148, 0183–0187, 0190, 0192; domainin diğer modülleri Belirsiz | `docs/roadmap/plans/mvp6-process-pilot-01/CT-QUEUE.tsv@6b8a62d5a` (bu modüllerin satırları; MOD-0188 yalnız başka bir sahibe atıf olarak geçer, 0189 ve 0191 hiç geçmez) · `docs/guides/operations/mvp6-development-process-v1.0.md@6b8a62d5a` · güncel: PR #146 (`3912d568b`) `execution/domains/supply-chain-execution/work-packs/CT-QUEUE.tsv` dosyasını ekledi (MOD-0188, 3 satır) — kayıtlı görev kaynağı değildir; sahipliği doğrulanmadı |
| pharmacovigilance | PVG Control Tower — planda; güncelliği doğrulanmadı | `docs/roadmap/plans/pvg-control-tower-development-plan-2026-08-13.md@6b8a62d5a` |
| platform-shared-services | Backlog'daki "CT" — **kısmi**: Auth, Platform, MOD-0023 onay motoru, MOD-0024 Görev Merkezi, MOD-0027 bildirimler. Etiket yalnız "CT" der, hangi Control Tower olduğunu söylemez; bugünkü sahip, sahip kararı bekliyor | `docs/roadmap/backlog/product-backlog.md@6b8a62d5a` "SAHİP: CT (…)" etiketleri (Auth 13, MOD-0024/Görev Merkezi/WorkCenter 12, MOD-0023 ve onay motoru 10, Platform ve bildirimler 6); paketler `execution/domains/platform-shared-services/module-packs/@6b8a62d5a` |
| master-data-management | Backlog'daki "CT" — **kısmi**: MDM devralma, MOD-0220. Bugünkü sahip, sahip kararı bekliyor | `docs/roadmap/backlog/product-backlog.md@6b8a62d5a` "SAHİP: CT (…)" etiketleri (MDM devralma 10, MDM 4, MOD-0220 2) |
| procurement | Belirsiz — sahip ataması bekliyor | CT-QUEUE'da yalnız MOD-0140 satırı var; domain sahipliği sayılmadı |
| commercial-suite | Belirsiz — sahip ataması bekliyor | — |
| developer-enablement | Belirsiz — sahip ataması bekliyor | — |
| enterprise-strategy-business-performance | Belirsiz — sahip ataması bekliyor | `domain-config.md`'deki "Control Tower Gate 2" bir kapıdır, sahiplik değildir |
| human-capital-management | Belirsiz — sahip ataması bekliyor | — |
| management-governance | Belirsiz — sahip ataması bekliyor | `domain-config.md`'deki "Control Tower Gate 2" bir kapıdır, sahiplik değildir |
| portfolio-delivery | Belirsiz — sahip ataması bekliyor | — |
| *(domain dışı)* altyapı, CI, test altyapısı, ortak ön yüz | Backlog'daki "CT"; bugünkü sahip, sahip kararı bekliyor | `docs/roadmap/backlog/product-backlog.md@6b8a62d5a` "SAHİP: CT (…)" etiketleri (altyapı 12, ortak ön yüz 5, test altyapısı 2) |
