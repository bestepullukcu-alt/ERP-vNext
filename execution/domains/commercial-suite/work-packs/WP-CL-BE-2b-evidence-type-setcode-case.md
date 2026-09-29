# WORK PACKAGE — WP-CL-BE-2b · Kanıt tipi doğrulaması: set kodu büyük harfe çevriliyor → "evidence-type" bulunamıyor (Platform, küçük düzeltme)

> **CT (SoR).** **CL-E4-1 canlı testinde bulunan engel** (2026-09-29). Admin User, CLM-ALMIBA-02'ye "ALMIBA — Etki Mekanizması" belgesini kanıt olarak bağlamaya çalıştı.
> - `POST /CRM/Claims/api/v2/claims/{id}/evidence` → **400** `["reference_set_missing","Reference set 'evidence-type' is not available (reference_data_set_not_found)."]`
> - Set **yayında**: Web lookup `/CRM/Claims/api/v2/lookups/evidence-types` 200 ve 5 değer dönüyor. Mongo'da `evidence-type` global, Published.
>
> **Çalışma yeri:** worktree `C:\tmp\cl-be-2b`, dal `wp/cl-be-2b`. Commit bu dala, push YOK.

## Kök neden (CT)
- `Platform.Application/Features/BusinessReferenceData/Services/BusinessReferenceDataActiveMembershipService.cs`:
  - `Normalize()` (satır 154) set kodunu **`ToUpperInvariant()`** yapıyor.
  - Bu değer `GetPublishedValuesAsync(normalizedSet…)` çağrısına (satır 77 / 127) gidiyor.
  - Repository set kodunu **birebir** eşliyor (`BusinessReferenceDataStewardshipRepository` `Filter.Eq(x => x.SetCode, normalized)`).
  - Sonuç: küçük harfli (kebab-case) her set bulunamıyor. Yalnız `COUNTRY_CODES` gibi büyük harfli setler çalışır.
- **Tek tüketici** `EvidenceLinkingHandlers.cs:252` (`EvidenceReferenceSets.EvidenceType = "evidence-type"`). Başka kullanan yok (grep: yalnız DI + EvidenceLinking).
- BE-2 testleri sahte membership servisiyle koştuğu için yakalanmadı.

## NE
1. `BusinessReferenceDataActiveMembershipService`: **set kodu büyük harfe çevrilmez**, yalnız `Trim()`.
   - **Değer kodu** karşılaştırması büyük/küçük harf duyarsız kalır (mevcut `OrdinalIgnoreCase`).
   - Sonuç `SetCode` alanı ve mesajdaki set kodu da trim'li, özgün haliyle döner.
2. **Regresyon testleri** (gerçek servis + sahte `IBusinessReferenceDataConsumerQueryService`, birebir set kodu eşleyen):
   - `evidence-type` (küçük harf) bulunuyor, aktif değer kabul;
   - `COUNTRY_CODES` (büyük harf) bulunmaya devam ediyor;
   - aktif olmayan değer → `reference_value_not_active`;
   - olmayan set → set-bulunamadı nedeni.
3. **EvidenceLinking entegrasyon testi:** gerçek membership servisiyle bir `POST links` başarılı olmalı. Sahte membership ile değil.

## KORU / YAPMA
- Membership servisinin diğer davranışları (değer normalizasyonu, hata nedenleri, istisna yakalama) DEĞİŞMEZ.
- EvidenceLinking kuralları, CRM, Web DOKUNMA.
- **DUR:** repository set kodunu zaten büyük/küçük harf duyarsız arıyorsa (kök neden başka bir yerdeyse) → dur, gerçek nedeni raporla.

## Acceptance
- **E2:**
  - Platform Application testleri → yeni kalıcı kırmızı yok (taban 173 ortam kırmızısı; TRX karşılaştırması).
  - Yeni testler yeşil.
  - **Sabotaj kanıtı:** `ToUpperInvariant` geri eklenince regresyon testi kırmızıya dönmeli.
  - Platform build 0 hata.
- **E4 (CT, birleştirme + fleet restart sonrası):** CLM-ALMIBA-02'ye kanıt bağlama **201**.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-specialist.md]
WP: WP-CL-BE-2b · Kanıt tipi doğrulaması: set kodu büyük harfe çevriliyor → "evidence-type" bulunamıyor (Platform, küçük düzeltme)
Repository: C:\tmp\cl-be-2b (worktree) · Branch: wp/cl-be-2b · commit bu dala, push YOK

Amaç: Canlı E2E'de kanıt bağlama 400 reference_set_missing ("evidence-type" not found) veriyor; set yayında. Kök neden: BusinessReferenceDataActiveMembershipService.Normalize() set kodunu ToUpperInvariant yapıyor, repository birebir eşliyor → küçük harfli her set bulunamıyor. Tek tüketici EvidenceLinking.

Önce oku: execution/domains/commercial-suite/work-packs/WP-CL-BE-2b-evidence-type-setcode-case.md · services/Diten.Platform/src/Diten.Platform.Application/Features/BusinessReferenceData/Services/BusinessReferenceDataActiveMembershipService.cs (Normalize satır 154, GetPublishedValuesAsync 77/127) · Diten.Platform.Infrastructure/Persistence/Repositories/BusinessReferenceDataStewardshipRepository.cs (Filter.Eq SetCode) · Features/EvidenceLinking/EvidenceLinkingHandlers.cs:252.

NE:
 1) Membership servisi set kodunu büyük harfe ÇEVİRMESİN (yalnız Trim); değer kodu karşılaştırması büyük/küçük harf duyarsız kalsın; sonuç SetCode/mesaj trim'li özgün kod.
 2) Regresyon testleri (gerçek servis + birebir eşleyen sahte consumer query): evidence-type bulunur + aktif değer kabul; COUNTRY_CODES bulunmaya devam; pasif değer reference_value_not_active; olmayan set → set-bulunamadı.
 3) EvidenceLinking entegrasyon testi: gerçek membership servisiyle POST links başarılı.
KORU/YAPMA: membership servisinin diğer davranışları DEĞİŞMEZ; EvidenceLinking kuralları/CRM/Web DOKUNMA.
DOĞRULA (E2): cd C:\tmp\cl-be-2b; dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests -c Release --nologo → yeni kalıcı kırmızı yok (taban 173 ortam kırmızısı, TRX karşılaştır); yeni testler yeşil; sabotaj: ToUpperInvariant geri → regresyon testi kırmızı; Platform build 0 hata; git diff yalnız services/Diten.Platform/**. Commit ("fix(platform): WP-CL-BE-2b — BRD membership keeps set code case (evidence-type lookup)" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: repository set kodunu zaten büyük/küçük harf duyarsız arıyorsa (kök neden başka yerde) DUR + gerçek nedeni raporla.
```
