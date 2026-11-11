---
status: proposal
kind: owner-decision-request
date: 2026-10-29
author: Treasury Intake Control Tower (K1 / gmg-10)
decision_owner: Ali — MOD-0018 sahibi
related: KARAR D87, D80/2 (a); FU27 DL-5
---

> **Öneri — karar değil.** Bu dosya karar sahibinin onayını ister; merge edilmesi kararı vermez. Cevap bu PR'da yorum ya da
> "Approve" ile verilir; Treasury CT cevabı kaynak cevap (Y-n) ve karar (D-n) olarak kaydeder, sonra pakete işler.
> Seçim: 1–6 maddeleri tek tek; özellikle 4 (ekran)

# Treasury CT → Ali: K-W5 — muafiyet atamalarının denetim izi için okuma yolu (FU27 DL-5'e ek öneri, onayına)

Kaynak: KARAR D87 (D86 geri alındı), `.antigravity/rules/audit-trail-standard.md` §5.c (yol `c` beş koşul) ve §7 (denetim kaydı
okumasının meta-denetimi); ölçüm ct-05 `584c10ed3` (main birleşik). Karar senin (MOD-0018); yalnız DL-5 kodunu bekletir.

## Ölçüm (kod okuması)

- `authAuditLogs`'u okuyan hiçbir uç ya da ekran yok (Auth denetleyicileri; Web'deki denetim ekranları yalnız Platform merkezi günlüğü).
  Defter de böyle diyor (`tests/architecture/audit-ledger/Diten.AuthService.md:19-20`) → `auth-gunlugu` 3. koşulu sağlamıyor.
- `AuthAuditLog`'da ayrı nesne alanı yok: nesne yalnız `Metadata` metninde (`Domain/Entities/AuthAuditLog.cs:3-21`) → 1. koşul ("ne +
  hangi nesne") zayıf.
- `WriteOnceAsync` main'de var (`IAuthAuditService.cs:18`, `AuthAuditService.cs:29-50`); kuralın §4.4'teki "bu dalda henüz yok" cümlesi eskidi.
- **Tuzak:** defterdeki `auth-gunlugu` belirteci `WriteAsync / WriteEmptyRoleLoginAsync` — `WriteOnceAsync`'i içermez. Tüm izi `aday`→`c`
  yapmak iki yeni komutu kredilendirmez; ayrıca `WriteAsync` kullanan kimlik komutlarını (Login, Register, ChangePassword, VerifyMfa,
  ESign yeniden doğrulama) "denetlenen" yapar → `## Bilinen borç` ve `PinnedCounts` değişir, bunların bir kısmı K2 sınıfıdır (§4.4).
- Kabul edilmiş yol `c` örnekleri: BOM geçmişi (`GET /api/bom/version/{id}/history`, kaydın kendi okuma izni, Ayrıntı ekranında
  "Geçmiş" bölümü); MDM kısaltma kanıtı (`…/evidence`, ayrı `mdm.product-abbreviations.audit` izni).

## Öneri — dar, ayrı bir iz (tüm `auth-gunlugu`'yu değil)

1. **Yeni iz** `auth-sod-muafiyet-gecmisi`, belirteç `IAuthAuditService.WriteOnceAsync` + iki komut adı; mevcut `auth-gunlugu` `aday` kalır
   (kimlik komutlarının borcu değişmez). Not: `IssueServiceIdentityTokenHandler` da `WriteOnceAsync` kullanıyor; belirteç komut adlarıyla
   daraltılmazsa onun İ5 istisna satırı "artık denetleniyor" olur — belirteç biçimi senin kararın.
2. **Nesne alanı:** `AuthAuditLog`'a `EntityType` / `EntityId` (yalnız ekleme; eski kayıtlar boş) — 1. koşul.
3. **Okuma ucu:** `GET /api/roles/sod-waiver-assignments/{id}/history`; `[HasPermission("auth.payment-sod-waivers.read")]` (kaydı gören
   izini de görür — kural 3. koşul, BOM deseni; ayrı `.audit` anahtarı istemezsen açık atama listesi 20 kalır); `TenantId` süzgeci,
   başka kiracı 404; depo arayüzüne yalnız okuma üyesi (güncelleme / silme yok — 2. koşul).
4. **Ekran (3. koşul):** FU27'de ekran yok. En küçük yol: Roller ekranında (`Views/Governance/Roles`, rol hızlı görünümü) salt-okunur
   muafiyet atamaları listesi + "Geçmiş" bölümü, `Controllers/RolesController.cs` vekili izin denetimiyle. Bu FU27'nin "ekran yok"
   maddesini değiştirir ve 7 dil metin işini getirir — **senin kararın**; istemezsen alternatif: atama ekranı ayrı pakete giderken iz
   de o pakete kadar `aday` kalır ve DL-5 kodu bekler.
5. **Meta-denetim (§7):** geçmiş okuması merkezi günlüğe okuma satırı yazar. Auth'un `PlatformAuditEvent`'inde `IsMetaAudit` yok;
   Platform'un iç ucu kabul ediyor (`AuditAppendRequest.IsMetaAudit`). Auth iletici alanı eklenir (Platform dosyası değişmez).
6. **Defter ve testler (DL-5 değişikliğiyle):** `## İzler` yeni satır + beş koşul kanıt notu; `AcceptedTrails` sabitlemesi
   (`AuditTrailStandardTests.cs:99`); `CT-DECISIONS.md` satırı; HTTP testleri (kiracı 404, izinsiz 403, meta-denetim satırı, ekranda
   geçmiş), gerçek Mongo'da güncelleme / silme yolunun yokluğu.

## Kapsam dışı

D80/2 (b) — rol atama adımlarının denetim borcu (`auth-rbac-gunlugu`) bu önerinin kapsamında değil; ayrıca konuşulur.

Hangi seçenekleri onaylıyorsun (1–6; özellikle 4. ekran)? Onaylarsan FU27 DL-5'e işleyip CT-APR-02'den geçiririz.
