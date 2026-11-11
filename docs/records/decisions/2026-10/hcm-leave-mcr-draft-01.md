# HCM-LEAVE-03 — Blueprint düzeltme talebi (MCR) taslağı: MOD-0280 izin ve devamsızlık sayfaları

> **Taslaktır.** Blueprint'i değiştirmez, Master'a gönderilmemiştir. Master'a sahip gönderir; MCR kimliğini Master verir.
> Kod, paket ya da kapsam onayı değildir. Kaynak: Blueprint 8.8
> (`docs/reference/blueprint/System Capability & Implementation Blueprint - master 8.8.xlsx`, blob 5623c00b8),
> origin/main 3a607547e, 2026-10-27.

## Künye

- Görev kaynağı satırı: `HCM-LEAVE-03` (`execution/domains/human-capital-management/work-packs/hcm-leave/CT-QUEUE.tsv`)
- Kule: CT · İzin ve devamsızlık · dal `chore/ct/hcm-leave-ct-01`
- Sahip sözü: `mcr-taslak` (2026-10-27) — yalnız taslak hazırlama
- Biçim: Blueprint 8.8 `Module_Change_Requests` sayfasının sütunları (satır 4). O sayfada bugün yalnız MCR-001 ve MCR-002 var.

## MCR satırı (Master'a girilecek alanlar)

| Sütun | Değer |
|---|---|
| MCR ID | *(Master verir)* |
| Date Raised | 2026-10-27 |
| Raised By | Sahip (CT · İzin ve devamsızlık üzerinden) |
| Needed Capability | MOD-0280 Time, Attendance & Leave Management için yerel izin ve devamsızlık sayfaları; MOD-0280'in Module Pages kaydının dahili modüle göre düzeltilmesi |
| Why Needed (feature / dependency / module needing it) | (1) Module Pages satır 3144–3147, MOD-0280'e hâlâ eski dış sağlayıcı sayfalarını veriyor (Integration Overview, Contracts & Events, Monitoring & SLAs, Access & Audit; Domain "7) External Systems & Providers"). Oysa Blueprint_Data MOD-0280'i "4) Enterprise Application Ecosystem / Human Capital Management Foundation" altında dahili modül olarak tanımlıyor (Soft Pages: Time Entry; Attendance Calendar; Leave Request; Absence Record; Schedule / Roster) ve CHANGE_LOG satır 53 (UPD-04, 2026-06-15) bunu doğruluyor. (2) Prioritized_Gap_Register satır 156 (GAP-155) "no native HR workflow pages needed" diyor; bu, UPD-04'ten önceki dış sağlayıcı modeline ait. (3) MOD-0280'in SoR nesnesi "Leave Balance" izin türü ve hak tanımı olmadan hesaplanamıyor; bu iki yönetim sayfası Soft Pages'te yok. (4) `TIME-LEAVE-BUNDLE` Contract Bundle Dictionary satır 300'de "Definition pending owner". |
| Existing Modules Checked (closest MOD-IDs) | MOD-0280 (üst modül; Blueprint_Data, registry `reserved / planned`) · MOD-0280-FU01 Time Entry & Weekly Timesheet (zaman girişi; izin kapsam dışı) · MOD-0315 Time, Attendance & Leave Workspace (İK görünümleri; MOD-0280 kayıtlarının tüketicisi, SoR_Map satır 281) · MOD-0362 Working Calendar & Public Holidays (takvim ve tatiller; izin işlemleri değil, SoR_Map satır 326) · CAND-CAP-0036 (yalnız hazırlık kaydı, `origin/hr-future`) · MDM-079 (eski UKG/Kronos dış sağlayıcı modeli). Hiçbiri izin talebi, izin bakiyesi ya da devamsızlık kaydının sahibi değil. |
| Proposed Domain | 4) Enterprise Application Ecosystem *(değişiklik yok; Module Pages'in buna uyması isteniyor)* |
| Proposed Suite / Capability Group | Human Capital Management Foundation / Payroll, Time & Compensation |
| Proposed Wave | W-3 *(değişiklik yok)* |
| Proposed SoR Object(s) | Leave Balance *(mevcut MOD-0280 SoR'u)*; izin ve devamsızlık kaydı (Leave / Absence Entry) — Enterprise Object Catalog satır 46 "Time, Attendance & Leave Record" altında |
| Status | Taslak — sahip gönderecek |
| Decision / Decided By / Decision Date / Resulting MOD-ID / Master Version Applied | *(Master doldurur)* |

## İstenen değişiklikler (ayrıntı)

1. **Module Pages — MOD-0280:** satır 3144–3147'deki dört dış sağlayıcı sayfası kaldırılsın ya da "superseded by UPD-04" diye işaretlensin. Yerine Blueprint_Data'daki Soft Pages, Domain "4) Enterprise Application Ecosystem" ile girilsin: Time Entry · Attendance Calendar · Leave Request · Absence Record · Schedule / Roster.
2. **Module Pages — yeni iki sayfa (MOD-0280):**
   - *Leave Types* — Primary Tasks: izin türlerini tanımla (bakiyeden düşer mi, belge gerekir mi, onay gerekir mi). Key Objects: Leave Type.
   - *Leave Entitlements* — Primary Tasks: kişi başı yıllık hak ve devir sınırı gir (ilk sürümde elle; yasal hesap sonraya). Key Objects: Leave Entitlement, Leave Balance.
3. **GAP-155:** UPD-04 sonrası geçersiz; "Superseded" olarak kapatılsın ya da MOD-0280'in yerel sayfalarıyla yeniden yazılsın. (GAP-154 MOD-0279 için ayrıca bakılmalı; bu talebin kapsamı dışında.)
4. **Alt modül:** MOD-0280'in izin ve devamsızlık parçası depoda `MOD-0280-FU02 Leave & Absence Management` olarak ayrıldı (`execution/registries/module-id-registry.md`, sahip kararı `fu02-evet`, 2026-10-27; `--check-id … --parent MOD-0280` çıkış 0). Master alt modülleri izliyorsa bu kayıt Master'a da işlensin; izlemiyorsa bilgi notudur. Emsal: MOD-0280-FU01 aynı yolla ayrıldı.
5. **Sözleşme paketi:** `TIME-LEAVE-BUNDLE` için sahip ve en az içerik tanımlansın. Öneri (onay değil): izin türü, izin hakkı, izin/devamsızlık kaydı, bakiye okuma, "izin onaylandı / iptal edildi" olayları, AuditEvent v1, Correlation-ID.
6. **Kimlik çakışması notu:** `origin/hr-future` dalında `MOD-0280` kimliği eski anlamıyla ("Time & Attendance (UKG/Kronos) [External Provider]", platform-shared-services) bir pakette kullanılıyor. O dal `main`'de değil; Master'ın Legacy_ID_Migration_Map ya da benzeri bir kayıtta bu eski anlamı not etmesi önerilir.
7. **Kimlik anahtarı (bağımsız denetim 01, F-01):** Enterprise Object Catalog satır 46 ("Time, Attendance & Leave Record") anahtar hücresi "ExternalVendorKey + Canonical Employee ID" diyor; aynı satırın notu ise kaydı yerel ("Native internal time, attendance, schedule, and leave records") tanımlıyor. "ExternalVendorKey" UPD-04 öncesi dış sağlayıcı modelinden kalma. Anahtar yerel kimlik sözleşmesiyle uzlaştırılsın: öneri (onay değil) "Canonical Employee ID" (MOD-0251, CT · Çalışan kaydı'nın HCM-CORE-BUNDLE sözleşmesi) ve kiracı kimliği; dış sağlayıcı anahtarı kaldırılsın ya da yalnız içe aktarma için isteğe bağlı alan olsun.

## Bu talebin kapsamı dışında

- MOD-0315'in sayfaları (Leave Balance View, Absence Record View, Timesheet / Roster Interface) değişmez.
- MOD-0280'in Release Candidate durumu ("N", Release_Candidates satır 281) ve Dev_Readiness satırı bu talebin konusu değil.
- Servis yeri (ADR-005: şimdilik Diten.Platform) Blueprint yerleşimini ("Domain App (HCM Time & Leave)") değiştirmez; sapma depoda kayıtlı.

## Satır numaraları hakkında

Blueprint satır numaraları, Excel sayfasının kendi satır sırasından (başlık 1. satır) `openpyxl` ile okundu (2026-10-27).
