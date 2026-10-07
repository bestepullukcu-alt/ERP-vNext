# WORK PACKAGE — WP-VP-FIX-2 · Ziyaret planlama: Düzenle formu hedefleri silmesin (D9) + Türkçe harf araması (F-1)

> **CT (SoR), 2026-10-07.**
> - **Kaynak:** WP-VP-2 §37 (D9) ve §37 ek E4 (F-1) · [yol haritası](ROADMAP-visit-planning.md).
> - **Kullanıcı:** "D9 + arama düzeltmesi … bununla devam edelim".
> - **Kapsam:** CRM (planlama oturumu güncelleme + hesap / kişi arama deposu) + Web (Ziyaret Planlama formu, Hedefler istemci araması).
>
> **Çalışma yeri:** worktree `C:\tmp\vp-fix-2`, dal `wp/vp-fix-2`, taban `test/crm-content-visit-e2e`. Commit bu dala, push YOK.

## NE
### 1. (D9) Düzenle formu taslak planın hedeflerini siliyor
- **Kanıt:**
  - `frontend/Diten.Web/wwwroot/assets/js/CRM/VisitPlanning/form.js` `buildPayload` düzenleme modunda `selectedAccountIds: []`, `selectedPharmacyIds: []`, `selectedContacts: []` gönderiyor.
  - CRM `UpdatePlanningSessionHandler` (`PlanningSessionCommandHandlers.cs` ~147) seçimi bu dizilerle değiştiriyor.
  - Sonuç: taslakta hafta / dönem değiştirmek tüm doktor, hesap ve eczane seçimini siler.
- **Düzelt (sunucu):** güncellemede `null` dizi = **dokunma** (mevcut seçim korunur). **Boş dizi** (`[]`) = açıkça temizle (Hedefler "Temizle" + "Hedefleri kaydet" bu yolu kullanır, davranışı aynı kalır). Üç dizi bağımsız: biri `null`, diğeri dolu gelebilir.
- **Düzelt (Web):** düzenleme modunda form hedef dizilerini **hiç göndermez**. Oluşturma modunda bugünkü gibi boş gider (yeni plan hedefsiz başlar).
- Seçimle birlikte saklanan diğer alanlar (`ManualVisitOrder` vb.) da `null` dizide korunur. Hangi alanların etkilendiğini listele.
- Hedefler sekmesinin "Hedefleri kaydet" yolu **değişmez**: tam diziyi gönderir.

### 2. (F-1) Türkçe harf araması
- **Kanıt:**
  - `my-accounts` araması "HAMİDİYE" → 4, "Hamidiye" → 0.
  - Kök: `AccountRepository` (~81) ve `ContactRepository` (~46) `BsonRegularExpression(term, "i")`. Mongo'nun `i` seçeneği **İ / i / I / ı** dörtlüsünü eşlemiyor.
- **Düzelt (sunucu):** ortak bir yardımcı (ör. `TurkishInsensitivePattern.Build(term)`):
  - terimi `Regex.Escape` eder;
  - **i, ı, I, İ** karakterlerinin her birini `[iıIİ]` sınıfına çevirir;
  - `i` seçeneği diğer harfleri (ş/Ş, ğ/Ğ, ü/Ü, ö/Ö, ç/Ç) zaten eşliyor; doğrula.

  Kullanacak yerler: hesap araması (genel liste + `my-accounts` aynı depo), kişi araması. Aynı deseni taşıyan diğer CRM aramalarını **listele, dokunma** (TerritoryModel, Segment aday kaynağı) → rapor.
- **Düzelt (Web):** Ziyaret Planlama Hedefler doktor tablosu araması (istemci tarafı) Türkçe duyarsız olur.
  - `toLocaleLowerCase('tr')` + i / ı katlaması.
  - Hem "şirin" hem "ŞİRİN" hem "Şirin", "ŞİRİN"i bulur.
  - Kurum araması sunucuya gidiyor (1. maddeyle çözülür).
- Mongo dizin kullanımı bugün de regex taraması; performans değişmez. Yeni dizin YOK.

## KORU / YAPMA
- Ekran tasarımı değişmez. Yeni yazma komutu YOK (AUD-001 26 sabit).
- Mobil sözleşmesi değişmez (oturum ucu mobilde kullanılmıyor; arama davranışı yalnız genişler).
- Veri göçü YOK. Seed / grant YOK.

## Acceptance
### E2 (taban ölç, yalnız farkı raporla)
Web (697/0 birleşik), CRM Application (2270/0/5; PII flake), mimari (38/1; 26 sabit). Build 0 hata.

**Yeni testler (üretim koduyla):**
1. Güncellemede üç dizi `null` → seçim aynen kalır.
2. Yalnız biri dolu → yalnız o değişir.
3. Boş dizi → o dizi temizlenir.
4. Web formu düzenleme modunda dizileri göndermez, oluşturmada gönderir.
5. Arama deseni:
   - "Hamidiye" / "HAMİDİYE" / "hamidiye" / "HAMIDIYE" → aynı hesap;
   - "şişli" / "ŞİŞLİ" → aynı hesap;
   - regex özel karakterli terim (ör. `a.b(`) güvenli.
6. Kişi araması "şirin" → "ŞİRİN".
7. Web Hedefler istemci araması Türkçe duyarsız (kaynak / JS testi).

**Sabotaj (kırmızı kanıtla, geri al):**
1. Güncellemede `null`'ı boş say → test 1 kırmızı.
2. Desen yardımcısında i-katlamayı kaldır → test 5 kırmızı.

### E4 (CT, fleet; kullanıcı girişi; ayrı sekme)
- Bir taslak planda Düzenle → haftayı değiştir → kaydet → Hedefler'de seçim **duruyor**. Bu bir kayıt işlemidir; test taslağında, kullanıcı onayıyla.
- Hedefler kurum araması "Hamidiye" → sonuç var.
- Doktor araması "şirin" → "ŞİRİN".

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-architect.md] @[.antigravity/agents/frontend-ui-ux.md]
WP: WP-VP-FIX-2 · Ziyaret planlama: Düzenle formu hedefleri silmesin (D9) + Türkçe harf araması (F-1)
Repository: C:\tmp\vp-fix-2 (worktree) · Branch: wp/vp-fix-2 · commit bu dala, push YOK

Paket belgesi + tam komut: execution/domains/commercial-suite/work-packs/WP-VP-FIX-2-edit-keeps-targets-turkish-search.md — önce tamamını oku. Ayrıca: …/WP-VP-2-rep-scope-names-territory-derivation.md (§37 + §37 ek) · services/Diten.CrmService/src/**/Features/VisitPlanning/Handlers/CommandHandlers/PlanningSessionCommandHandlers.cs · services/Diten.CrmService/src/**/Persistence/Repositories/{AccountRepository,ContactRepository}.cs · services/Diten.CrmService/src/**/Features/VisitPlanning/MyAccounts/GetMyAccountsQuery.cs · frontend/Diten.Web/wwwroot/assets/js/CRM/VisitPlanning/{form,details}.js · .antigravity/rules/audit-trail-standard.md.

NE:
(1) D9 — CRM: oturum güncellemede null dizi = dokunma (seçim + bağlı alanlar korunur), boş dizi = açıkça temizle, üç dizi bağımsız. Web: form.js düzenleme modunda hedef dizilerini HİÇ göndermez (oluşturmada bugünkü gibi). "Hedefleri kaydet" yolu değişmez.
(2) F-1 — CRM: ortak Türkçe-duyarsız arama deseni yardımcısı (Regex.Escape + i/ı/I/İ → [iıIİ]; diğer TR harfleri i seçeneğiyle doğrula); AccountRepository (genel liste + my-accounts) ve ContactRepository aramasına uygula; aynı desenli diğer CRM aramalarını listele (dokunma). Web: Hedefler doktor tablosu istemci araması Türkçe-duyarsız (toLocaleLowerCase('tr') + i/ı katlama).
KORU/YAPMA: tasarım değişmez; YENİ YAZMA KOMUTU YOK (AUD-001 26 sabit); mobil sözleşmesi değişmez; göç/seed/grant/yeni dizin YOK.
DOĞRULA (E2): tabanı ölç, yalnız farkı raporla — Web (697/0) · CRM Application (2270/0/5, PII flake) · mimari (38/1, 26 SABİT); build 0 hata; fleet açıkken Web bin kilitliyse -o frontend/Diten.Web.Tests/bin/Debug/<ad>. Yeni testler WP Acceptance 1–7. Sabotaj 1–2 (kırmızı kanıtla, geri al).
Commit: "fix(crm,web): WP-VP-FIX-2 — planning edit keeps targets (null = unchanged) + Turkish-insensitive search" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor: madde başına ne yapıldı + kanıt (dosya:satır, test adı), null'da korunan alanlar listesi, aynı desenli dokunulmayan aramalar listesi. §22 TÜRKÇE. K13.
```

---

## §37 CT kabul — E2 ACCEPTED (2026-10-07)
**Commit:** `23e0a9b98` (ajan `23e0a9b9`, ff). Push: test dalı.

**CT K13:**
| Paket | Taban | Sonuç |
|---|---|---|
| Web | 697/0 | **699/0** |
| CRM | 2270/0/5 | **2283 + 1 PII flake (bilinen) / 5** |
| Mimari | 38/1 (26) | **38/1 (26 sabit)** |

**Kod okuması:** `MergeSelection` (null = koru, [] = temizle, liste = yaz; üçü bağımsız), API isteği gönderilmeyen doktor listesini artık null bırakıyor, `TurkishInsensitivePattern` (`Regex.Escape` + `[iıIİ]`) hesap + kişi deposunda.

**CT sabotajı (ajanınkinden ayrı):**
1. Doktor listesinde null'ı yok say → 2 D9 testi **kırmızı**.
2. `Regex.Escape`'i kaldır → `A_regex_character_in_the_term_is_plain_text` **kırmızı**.

İkisi de geri alındı.

**Ajanın canlı Mongo okuması (salt okuma):** "Hamidiye" 0 → 15, "şişli" 0 → 37, "şirin" 0 → 106.

**Dokunulmayan aynı desenli aramalar:**
- `TerritoryModelRepository:43-44` (kaçışsız + i) → küçük takip;
- `TerritoryModelRepository:89-91` ve `SegmentCandidateSource:331/342` (kaçışlı, i katlaması yok) → takip listesi.

**E4 (CT, bekliyor; fleet yeniden başlatma — CRM + Web değişti):**
- taslakta Düzenle → hafta değiştir → seçim duruyor (kayıt, test taslağında);
- "Hamidiye" kurum araması;
- "şirin" doktor araması.
### §37 ek — E4 ACCEPTED (2026-10-07, CT)
- `my-accounts` araması "Hamidiye" / "HAMİDİYE" / "hamidiye" / "HAMIDIYE" → 4; "şişli" → 36; genel liste "Hamidiye" 15; `a.b(` güvenli (0) ✓.
- Taslak `a238bdc5` (kullanıcı onayıyla): 3 doktor hedefle → Düzenle → hafta 42 → Kaydet → `/Details/a238bdc5…?week=2026-10-12` (404 yok, E7-B4) ve 3 hedef korundu ✓ → hafta 41'e geri, hedefler korundu ✓.
