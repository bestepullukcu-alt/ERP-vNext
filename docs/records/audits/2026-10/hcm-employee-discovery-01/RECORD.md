# HCM-EMP-01 — Keşif kararları (iş paketi kaydı)

> Control Tower kaydıdır; onay, paket ya da dağıtım izni değildir. MOD-0251 paketinin onaylı dilimi (P2 taslak / referans
> doğrulama) değişmez.

## Künye

- WP ID: `HCM-EMP-01` · Görev sınıfı: yönetişim · Risk: LOW
- Görev kaynağı satırı (birebir): `Keşif kaydı: MOD-0251 Core HR / Employee Master için verify_module_id.py --discover yeniden koşulur, rapor kanıt yoluna kaydedilip commit edilir (2026-10-27 ilk koşu çıkış 3, kaydedilmedi); her bulgunun kararı yazılır: (a) aynı kimlikle origin/hr-future'da "HRIS External SoR" paketi, (b) Diten.HumanCapitalService'teki dış HRIS çalışan izdüşümü, (c) module-id-registry.md'de MOD-0251 satırı yok; kod yok`
- Kuyruk: `hcm-employee` (`execution/domains/human-capital-management/work-packs/hcm-employee/CT-QUEUE.tsv`, taban 457ce41467fa4d586a28cab7750915825d3f69ba; kayıt satırı `main`'de değil — sahip kararı `dalga-sonu` 2026-10-27 ve sahip sözü 2026-10-27: kayıt satırı PR'ı ilk işçi başlatılırken iki kule için (hcm-employee + hcm-leave) tek PR olarak açılır)
- Repository: `ERP-vNext` · Branch: `chore/ct/hcm-employee-ct-01` · Ölçülen HEAD: 1fc586de16f8e5a31b86b6be3c7ed1a93a63ad72 · Worktree: `.claude/worktrees/hcm-employee-ct-01`
- Yetki: MOD-0251 paketi `status: approved`, `approval_status: conditionally-approved-for-draft-reference-validation-slice` (yalnız P2 dilimi); bu kayıt kod yetkisi vermez
- Araç: `.antigravity/scripts/verify_module_id.py` sha256 `72d27f77dcf4aa6c40cf6bd6c44808fe79b15c9911f4cae835da637e0763368f`

## Keşif satırı (kart §0.1)

Keşif: inceleme gerekli · rapor docs/records/audits/2026-10/hcm-employee-discovery-01/MOD-0251-core-hr-employee-master.txt@5e910d52441cfc21cdf65722e8ce8b48571548d8 · sha256 0ec5853fae5aec3a5fc60818d1e35e179d3a3ed700d5a08d326f9f2fba6f8fd4

Sorgu: `--discover "Core HR / Employee Master" --resource employees --sor "Employee"`. Koşu: 2026-10-27, origin/main 826150ef4, son fetch 2026-10-27T14:10. `--check-report` çıkış 0 (RAPOR GEÇERLİ); satır sonu LF (CR 0, 433 satır); raporda e-posta adresi yok (maskeleme gerekmedi).

Önceki koşu: 2026-10-27 sabah, sorgu `"Core HR Employee Master"`, origin/main 6704ac9e3, çıkış 3; ilk çağrı salt okunur olduğu için kaydedilmedi. Bu kayıt onun yerine geçer.

## Keşif kararları — her BULGU için (kart §0.1 "inceleme gerekli")

1. blueprint `MOD-0251 'Core HR / Employee Master'` (Core HR Master; Employee, Employment Record, Job Assignment) — **parent (mevcut modül)**: iş bu kimliğin kendisidir; yeni kimlik açılmaz. `verify_module_id.py --check-id MOD-0251 --name "Core HR / Employee Master"` çıkış 0.
2. sor `'HR employee profile projections; employment record snapshots; HR profile enrichments'` sahibi MOD-0298 (Employee Profile Workspace) — **ayrı modül, engel değil**: Blueprint 8.8 SoR_Map notu "Workspace over MOD-0251 internal employee master; does not create a competing master". MOD-0298 MOD-0251'i tüketir, onu tekrar etmez. `Diten.HumanCapitalService`'teki `EmployeeProfileProjection` (dış HRIS izdüşümü: `HrisSourceProfileId`, `ExternalEmployeeReference`) bu ailenin kodu gibi görünür; hangi modüle ait olduğu ve MOD-0251 ile ilişkisi **HCM-EMP-02'de ölçülür** (bulgu (b)). MOD-0298 sahip kararı `dagilim-evet` ile hiçbir kuleye atanmadı.
3. sor `'Employee relations cases; grievance cases; …'` sahibi MOD-0317 (Employee Relations & HR Case Management) — **yanlış pozitif, ilgisiz**: "Employee" sözcük benzerliği; ileride MOD-0251'in tüketicisi olabilir, nesne ortaklığı yok.
4. sor `'Self-service request drafts; channel preferences; …'` sahibi MOD-0319 (Employee / Manager Self-Service Channel) — **yanlış pozitif, ilgisiz**: sözcük benzerliği; ileride tüketici.
5. paket `human-capital-management/module-packs/MOD-0251-core-hr-employee-master.md` (342 ref) — **parent**: mevcut paket kullanılır. Künyede `owner: HCM / HR data owner TBD` → açık karar (aşağıda).
6. paket `platform-shared-services/module-packs/MOD-0251-hris-external-sor.md` (yalnız `refs/remotes/origin/hr-future`, 1 ref) — **kimlik çakışması, `main`'i engellemez**: aynı numara, farklı anlam ("HRIS (Workday/SuccessFactors/Oracle HCM) [External SoR]", `Diten.Platform`). Blueprint 8.8'de MOD-0251 iç çalışan kaydıdır; o paket eski anlamı taşır ve `main`'de yoktur. Bu kule o paketi kullanmaz ve o dala dokunmaz (başka sahibin dalı; son commit d2ad629d8, 2026-09-14). `hr-future` `main`'e birleşirse çakışma gerçek olur → uzlaştırma kararı açık (aşağıda).
7. görev `HCM-EMP-01` — **kendi satırı** (bu kayıt); mükerrer değil.
8. görev `HCM-EMP-02`, `HCM-EMP-03` — **kendi kuyruğunun alt işleri**; bu kaydın (2) ve (6) bulgularını tüketir.
9. görev `HCM-EMP-04` — **kendi kuyruğunun sahip kararı satırı**; mükerrer değil.
10. görev `HCM-EMP-05` — **kendi kuyruğunun paket taslağı satırı**; mükerrer değil. (`HCM-EMP-06` keşifte eşleşmedi; sözcük farkı, bulgu değil.)
11. registry — **bulgu 0, ama eksik var (bulgu (c))**: `execution/registries/module-id-registry.md`'de MOD-0251 satırı yok (origin/main'de `grep -c` 0, 2026-10-27). Kimlik Blueprint'ten kanıtlanıyor, satırın yokluğu kimliği geçersiz kılmaz; satır eklenmesi açık karar (aşağıda).
12. gateway — **bulgu 0, inceleme kısmi**: `--resource employees` hiçbir route ile eşleşmedi; oysa `ocelot.json`'da `/api/v1/hcm/employees` ve `/api/v1/hcm/employees/drafts` route'ları var (satır 2788–2847@6704ac9e3, okuma 2026-10-27). Bu route'lar MOD-0251 P2 diliminin kendi route'larıdır; mükerrer iş değil. Eşleşmemenin nedeni (kaynak adı biçimi) ölçülmedi.

Sonuç: yeni iş yoktur; iş, var olan MOD-0251'in bitirilmesidir. Hiçbir bulgu bu kuyruğun kod yazmayan işlerini engellemez. Dağıtım yine de her satırın kendi DoR'una (kart §1) ve kayıt satırının `main`'e girmesine bağlıdır.

## Açık kararlar (sahip; onay değildir)

- Paket sahibi: künyede `HCM / HR data owner TBD`. Bu kuleyi sahibin `dagilim-evet` kararı atadı; paket sahibi (rol etiketi) ayrı karardır. Künye değişikliği `pack_approval.py` onay satırı ister.
- `origin/hr-future`'daki MOD-0251 "HRIS External SoR" paketinin uzlaştırması: o dalın sahibine sorulur (XMC-001); bu kule dala yazmaz.
- `module-id-registry.md`'ye MOD-0251 satırı: ortak dosya; içerik bu kulede hazırlanabilir, `main`'e girişi iki kulenin ortak kayıt PR'ında (ilk işçi başlatılırken; §7b).
