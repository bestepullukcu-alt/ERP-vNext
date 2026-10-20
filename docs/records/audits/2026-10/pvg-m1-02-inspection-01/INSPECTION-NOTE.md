# PVG-M1-02 — İnceleme notu: MOD-0230 Case Intake & Triage kod ve çalışma gerçeği

> Control Tower inceleme notudur (ct-sop7-transitions satır 3: INSPECTION_REQUIRED → INSPECTED kanıtı). Onay, paket,
> kabul ya da dağıtım izni değildir. Operasyonel runtime kapısı (`operational_runtime_gate: closed`) değişmez. Kod
> yazılmadı; tek yazma bu nottur.

## Künye

- WP ID: `PVG-M1-02` · Görev sınıfı: yönetişim / kanıt envanteri · Risk: LOW (kod yok)
- Görev kaynağı satırı (birebir): `MOD-0230 Case Intake & Triage — yapılmış ama bitmemiş (API, bellek içi depo, Gateway route, tenant UI var; operasyonel runtime kapalı): kanıt envanteri, E3/E4 eksikleri, kabul kaydı olmayan işler; kod yok`
- Kuyruk: `pvg` (`execution/domains/pharmacovigilance/work-packs/CT-QUEUE.tsv`), kayıt satırı origin/main'de yürürlükte
- Repository: `ERP-vNext` · Branch: `chore/ct/pvg-ct-01` · Ölçülen HEAD: 77738ea55 (origin/main 9d44e8a1f ile eşit ağaç + 4 yerel commit) · Worktree: `.claude/worktrees/pvg-ct-01` · `git status --porcelain` boş, `git diff --check` temiz
- Yetki: DCP-004 `pvg-urgent-w3-development-block` (`approved`, 2026-08-09) · paket MOD-0230 `ready-for-dev`, `build_gate: open`, `operational_runtime_gate: closed`, `approval_status` alanı yok; `pack_approval.py check` çıkış 0 (kural başlangıcından beri değişmedi)
- Keşif: PVG-M1-01 kaydı (`docs/records/audits/2026-10/pvg-m1-01-discovery-01/RECORD.md@5bb1cecc6`); bu notta yeniden koşulmadı
- Araçlar: dotnet SDK 8.0.416 (macOS), python3 3.9.6; alt ajan kullanılmadı (model/effort yok)
- Ölçüm zamanı: 2026-10-20T21:52Z – 2026-10-20T22:05Z (yerel 2026-10-21 00:52–01:05)

## 1. Kod gerçeği — E1 (statik, `yol:satır@77738ea55`)

| Katman | Olgu | Kaynak |
|---|---|---|
| Servis | `Diten.PvgService` — Api 4 / Application 55 / Domain 16 / Infrastructure 7 dosya; `appsettings*.json` ve `launchSettings.json` yok; yapılandırma yalnız ortam değişkeni + komut satırı | `services/Diten.PvgService/**`, `Program.cs:3-6` |
| Host | Kestrel, varsayılan `http://127.0.0.1:5011`; `/health/live`, `/health/ready`; `PvgServiceApiRuntimeOptions.ThrowIfUnsafe()` üretim-benzeri + yetkisiz ya da üretim-benzeri + üretim dışı adaptör → açılışta istisna | `Program.cs:12-14`, `PvgServiceApiHost.cs:24-25,84-97` |
| Adaptörler | Yalnız `UseNonProductionAdapters` iken kayıtlı: DenyAll FieldSecurity / WorkflowTransitionGate / EvidenceLink / PermissionGate + `InMemoryPvgIntakeDraftRepository` (kiracı anahtarlı sözlük, kalıcılık yok, Mongo yok) | `PvgServiceApiHost.cs:35-42`, `InMemoryPvgIntakeDraftRepository.cs:19-20,38,60` |
| API yüzeyi | 6 uç: POST liste-kökü, PUT `{id}`, GET liste, GET `{id}`, POST `{id}/triage`, POST `{id}/route`; rezerve segmentler (`export`, `archive`, `void`, `bulk`, `bulk-delete`, `delete`) → 404 | `PvgCaseIntakeTriageEndpoints.cs:10-31,211-213` |
| Bağlam | Kiracı, aktör ve korelasyon **HTTP başlıklarından** (`X-Diten-Tenant-Context`, `X-Diten-Actor-Id/Kind`, `X-Correlation-Id`); JWT doğrulaması yok; eksik başlık → 409 `Blocked` güvenli neden koduyla | `PvgCaseIntakeTriageEndpoints.cs:321-380` |
| Kapı zinciri (create) | doğrulama → bağlam/izin guardrail → FieldSecurity → (varsa) EvidenceLink → depo; denetim niyeti (`PvgAuditIntent`) sonuçta üretilir, bir yere yazılmaz | `PvgIntakeDraftApplicationService.cs:44-101` |
| Gateway | 4 route `/api/pv-case-intake-triage*` → `localhost:5011` `/api/v1/...`; yöntemler GET/POST, GET/PUT, POST, POST; bu route'larda `AuthenticationOptions` **yok** (dosyada 4 başka route'ta var) | `gateway/Diten.ApiGateway/ocelot.json:2785-2844` |
| Tenant UI | MVC proxy denetleyicisi `[Authorize]` ile; `[HasPermission]` **yok** (diğer kiracı denetleyicilerinde 11–17 kullanım); kiracı/aktör JWT'den sunucu tarafında çözülür ve başlık olarak iletilir; 4 görünüm + 4 partial + 4 JS; resx 7 dil × 87 anahtar | `Controllers/PharmacovigilanceCaseIntakeTriageController.cs:14-16,161-193`, `Resources/Views/Pharmacovigilance/CaseIntakeTriage/*.resx` |
| Kayıt / gezinti | Modül manifestosu, katalog kaydı ve kenar çubuğu girdisi yok (WP-0230-08 `blocked`); `pv.*` izin anahtarı depoda yok (servis, Web, AuthService) | `work-packs/WP-0230-08-module-manifest.md:8`, grep sonucu 0 |
| Port | 5011 domain-config'te (OD-7) var; `AGENTS.md` §3 port tablosunda **satırı yok**; depodaki `.claude/launch.json` 5011'i `web-verify` (Diten.Web) için kullanıyor | `domain-config.md:42`, `AGENTS.md:66-68`, `.claude/launch.json:19-29` |
| CI | 5 PVG süiti `service-tests.yml` matrisinde (Debug); `tests/known-failures.tsv`'de pvg satırı yok | `.github/workflows/service-tests.yml:84-93` |

## 2. Testler — E2

Komut (her süit için): `dotnet test <csproj> -c Debug --logger "trx;LogFileName=<süit>.trx" --results-directory <scratchpad>/pvg-m1-02/trx` — koşu 2026-10-20T21:52:17Z–21:52:35Z, HEAD 77738ea55.

| Süit | Toplam | Geçti | Kırmızı | TRX sha256 (ilk 16) |
|---|---:|---:|---:|---|
| pvg-api | 36 | 36 | 0 | 831ecd3e7acc62f8 |
| pvg-case-processing | 23 | 23 | 0 | e6c0c71568ec4904 |
| pvg-meddra-coding | 40 | 40 | 0 | dabbe50a9088f953 |
| pvg-regpv-base | 88 | 88 | 0 | bcf18f43c7b3fa6d |
| pvg-signal-management | 43 | 43 | 0 | f7ef4a806eab68f2 |

Toplam 230/230. `known_failures.py check pvg-api.trx --suite pvg-api` → çıkış 0 (kayıtlı kırmızı yok). TRX dosyaları oturumun
scratchpad'inde; depoya alınmadı (kart §5 kanıt saklama: kabul koşusunda `run_record.py` ile alınır). Ağustos planındaki
sayılar (API 35/35, RegPvBase 80–88/88) bugünkü koşuyla uyumlu; MOD-0231/0232/0234 süitleri de yeşil ama bu notun
kapsamı dışıdır.

## 3. Çalışma gerçeği — E3 (yerel, üretim dışı)

API, worktree'deki Debug derlemesinden `ASPNETCORE_ENVIRONMENT=Development` ile 5011'de başlatıldı (PID kaydedildi,
iş sonunda aynı PID ile durduruldu; 5011 boş doğrulandı). Gateway, depodaki `launch.json` `gateway` girdisiyle 5000'de.

| # | Çağrı | Sonuç |
|---|---|---|
| a | GET `/health/live` | 200 `mode: local-dev-ci-build-test` |
| b | GET `/health/ready` | 200 `operationalRuntimeAuthorized: false`, `nonProductionAdaptersEnabled: true` |
| c | GET liste, başlıksız | 409 `PVG_TENANT_CONTEXT_REQUIRED` |
| d | GET liste, yalnız kiracı | 409 `PVG_ACTOR_CONTEXT_REQUIRED` |
| e | GET liste, kiracı+aktör+korelasyon | 409 `PVG_PERMISSION_DENIED` (DenyAll) |
| f | POST create, eksik alanlar | 400 `PVG_FIELD_VALUE_INVALID` / `PVG_REQUIRED_FIELD_MISSING` — doğrulama izin kapısından **önce** |
| f2 | POST create, gövdede `tenantId` | 400 aynı; alan kabul edilmiyor (DTO'da yok) |
| A | POST create, geçerli yük | 409 `PVG_PERMISSION_DENIED` — commit öncesi kapı; kayıt oluşmadı |
| A2 | PUT `{id}`, geçerli yük, olmayan id | 409 `PVG_PERMISSION_DENIED` |
| g | GET `/export` | 404, gövde yok |
| h | DELETE `{id}` | 405 (route yok) |
| i | `X-Correlation-Id` boşluklu | 409 `PVG_CORRELATION_CONTEXT_INVALID` |
| B1 | Üretim-benzeri açılış, varsayılanlar | çıkış 134, `PVG operational runtime is not authorized for production-like startup.` |
| B2 | Üretim-benzeri + yetkili + üretim dışı adaptör | çıkış 134, `PVG production-like startup cannot use non-production adapters.` |
| B3 | Üretim-benzeri + yetkili, adaptör yok | **host açılıyor**, `/health/ready` 200 `operationalRuntimeAuthorized: true`; GET liste (tam başlık) → **500, boş gövde** |
| G1 | Gateway GET liste, başlıksız, token yok | 409 `PVG_TENANT_CONTEXT_REQUIRED` — istek API'ye ulaştı |
| G2 | Gateway GET liste, tam başlık, token yok | 409 `PVG_PERMISSION_DENIED` |
| G3 | Gateway, korelasyon başlığı yok | 409 `PVG_PERMISSION_DENIED` — kenar kendi korelasyonunu ekliyor |
| G4 | Gateway, sahte Bearer | 409 `PVG_PERMISSION_DENIED` — token doğrulanmadı |
| G5 / G6 | Gateway `/export` / DELETE | 404 / 404 |
| G7 | Gateway POST create, geçerli yük | 409 `PVG_PERMISSION_DENIED` |
| G8 | Gateway yanıt başlığı | `X-Correlation-Id` çağıranın değeriyle geri dönüyor |

Gateway günlüğü: hata yok. UI (Diten.Web → proxy → Gateway) smoke'u **koşulmadı**: Auth + Mongo + kiracı girişi
gerektirir; Ağustos kanıtı `PVG-0230-AUTH-PROXY-01` (plan satır 91, E5 yerel) yeniden ölçülmedi.

## 4. E3 / E4 eksikleri (bulgular)

1. **Gateway kimlik doğrulaması yok (E4, kiracı/yetki):** PV route ailesinde `AuthenticationOptions` yok; anonim istek
   Gateway'den API'ye ulaşıyor (G1, G4). API JWT doğrulamıyor; kiracı/aktör başlıktan geliyor. Bugün yalnız DenyAll
   kapısı koruyor. Operasyonel runtime öncesi: route'larda Gateway auth + sunucu tarafı kiracı çözümü (Çekirdek 2, 3).
2. **`[HasPermission]` yok, izin kataloğu yok, gezinti yok (E4, UAS-001):** MVC denetleyicisi yalnız `[Authorize]`;
   `pv.*` anahtarı hiçbir yerde; sayfa yalnız URL ile erişilir; manifest/katalog kaydı WP-0230-08'de `blocked`.
3. **Adaptörsüz yetkili açılış fail-closed değil (E3):** B3 — paket §708 "gerekli sözleşme eksikse açılış başarısız ya
   da düzenlenmiş mutasyon engellenir" ister; host açılıyor ve iş ucu **500 boş gövde** veriyor (güvenli hata modeli
   değil). Eksik adaptör için açılış kontrolü yok.
4. **Doğrulama izin kapısından önce (küçük):** f — yetkisiz çağıran zorunlu alan kodlarını öğreniyor; yalnız güvenli
   kodlar sızıyor.
5. **Kalıcılık yok (E4):** depo bellek içi; yeniden başlatmada veri kaybı; WP-0230-03 başlığı "Mongo persistence" ama kod
   bellek içi. E4 kalıcılık/denetim/RBAC kanıtı gerçek adaptörler olmadan alınamaz (tasarım gereği).
6. **Port kaydı sapması (Çekirdek 5):** 5011 `AGENTS.md` §3'te yok; `launch.json` 5011'i Diten.Web `web-verify` için
   kullanıyor (çakışma). Paket satır 667 bu düzeltmeyi kapı olarak sayıyor.
7. **Kişisel e-posta:** paket (5) ve DCP-004 (3) — HIGH onay satırı, ayrı iş (sahip notu 2026-10-21).

## 5. Kabul kaydı olmayan işler

- `WP-0230-01…07`: dosyalarda `status: ready` (kapanmamış); `WP-0230-08` `blocked`.
- Ağustos planı (`docs/roadmap/plans/pvg-control-tower-development-plan-2026-08-13.md`) 39 "Completed WP" satırı: kanıt
  commit'leri (de4fca61, 6ee2c29f, 816d5359, bfa7099c, a44c324b, 695a3b18, 80069ab4, 603601cc, 083c9db3, 2fc2ead6,
  af2e21b9, 972e7ad9) origin/main'de **var**, ama kanıt plan anlatısı + plan içi test sayısı (E0–E2); CT-OPS-03 kabul
  kaydı (`acceptance_record.py`) ve depoda TRX/`run_record` **yok**.
- `pvg-operational-approval-packet-draft-2026-08-24.md`: "Build-test 100% PASS / Operational 0% NO-GO" — taslak, onay
  değil.

## 6. Sonuç ve öneri (öneri — onay değil)

- Ulaşılan düzey: E1 ✔, E2 ✔ (230/230), E3 ✔ API doğrudan + Gateway (DenyAll altında), UI E3 koşulmadı, E4 alınamaz.
- Queue satırı `PVG-M1-02`'nin görev tanımı ("kanıt envanteri, E3/E4 eksikleri, kabul kaydı olmayan işler") bu notla
  büyük ölçüde karşılanmıştır. Geçiş: INSPECTION_REQUIRED → INSPECTED (kanıt: bu not `yol@commit`); sonra PLANNED
  (kapsam: bu notun §4–§5'ini resmî kabul kaydı taslağına çevirmek; kod yok; izinli yol
  `docs/records/audits/2026-10/pvg-m1-02-*`, korunan: `services/**`, `gateway/**`, `frontend/**`, `execution/**`).
- Sahip kararı gerektirenler: §4-1 Gateway auth ve §4-3 açılış kontrolü **kod** işidir → ayrı WP (paket kapsamında,
  `build_gate: open`; runtime kapısına dokunmaz); §4-6 port tablosu `AGENTS.md`/`.antigravity` korunan yol → sahip
  onayı; §4-2 `pv.*` kataloğu MOD-0018 sahibinin (XMC-001).
