# WORK PACKAGE — WP-VW-W3E · Ek kanıtlar: kurum QR kodu + cihaz doğrulama işaretleri (alıcı uç) + yönetici kanıt işaretleri — 2026-10-14

> **Kaynaklar:**
> - Yol haritası [ROADMAP-visit-workspace](ROADMAP-visit-workspace.md), W3E satırı.
> - Mockup brief'i [BRIEF-W3E-qr-antifraud](mockups/visit-workspace/BRIEF-W3E-qr-antifraud.md). Mockup gelince fark belgesi `DESIGN-GAP-W3E-…` yazılır; WEB istemleri ondan sonra.
> - W3 yürütme [WP-VW-W3-visit-execution](WP-VW-W3-visit-execution.md): kanıt ayarı, istisna, başlatma, işaret sözlüğü.
> - W4 [WP-VW-W4-report-v2](WP-VW-W4-report-v2.md) §2.4: ekip kapsamı, `TeamScopeResolver`.
> - Mobil not: `mobile/2026-10-12-visit-workspace-w3/MOBILE-NOTE-2026-10-12-visit-execution.md` §3, §5, §6, §10.
>
> **Taban:** test dalı `test/crm-content-visit-e2e` = main `92f60aacd` (PR #143). CRM 2742/2, Web 1681/2, mimari denetimsiz 118.

## 0. Kararlar

### Kullanıcı (2026-10-14)
- **K-W3E-1 · QR kiracı ayarıdır:** Kapalı / İsteğe bağlı / Zorunlu; kod **kurum başına**.
  - Hastanedeki doktor ziyareti hastanenin kodunu okutur: başlatmanın konum kuralında kullandığı kurumun kodu.
- **K-W3E-2 · Kod iptal edilene kadar geçerlidir:** imzalı, sürümlü yük.
  - "Yeniden üret" sürümü artırır, eski kodlar anında geçersiz olur.
  - "Yeniden yazdır" aynı kodu basar.
- **K-W3E-3 · Anti-fraud önce kaydeder + işaretler, engellemez.**
  - Google Play Integrity / Apple App Attest jetonunun sunucuda doğrulanması ve "Engelle" kipi **ayrı paket (W3E-ATTEST)**, canlıya çıkış kararlarıyla birlikte.
- **K-W3E-4 · Web tarayıcıdan başlatma serbest** ama ayar açıksa "cihaz doğrulanamadı" diye işaretlenir.

### CT teknik seçimleri (itiraz edilmezse geçerli)
- **K-W3E-5 · Üst kurum zinciri:**
  - Konum kuralının kurumu (`VisitExecutionRules.AccountOf`) **ya da onun üst kurumlarından birinin** (`ParentAccountId` zinciri, en çok 5 düzey) aktif kodu geçer.
  - Böylece hastane kabulündeki tek kod, alt birim ziyaretlerinde de çalışır.
- **K-W3E-6 · Zorunlu kipte kuruma hiç kod basılmamışsa** temsilci engellenmez: başlar + `qr_not_issued` işareti. (Konum için `allow_flagged` ile aynı mantık; yöneticinin eksiği temsilciyi durdurmaz.)
- **K-W3E-7 · İsteğe bağlı kipte okutmamak işaret değildir.** Yalnız yanlış kurum / iptal / geçersiz kod okutulursa işaret konur.
- **K-W3E-8 · Uzaktan ziyaret** (`kind=remote`) QR sormaz (`not_applicable`).
- **K-W3E-9 · İstisna:** yeni esnetme kuralı `qr_optional` (Zorunlu → İsteğe bağlı), `photo_optional` ile aynı desen.
- **K-W3E-10 · Web vekili istemci beyanına güvenmez:** başlatmayı iletirken `clientPlatform=web` yazar, cihaz doğrulama alanlarını siler. Böylece tarayıcı "doğrulandı" diyemez.
  - Mobil beyanı ise W3E-ATTEST'e kadar **doğrulanmamış beyan** olarak saklanır (`verified=false`).
- **K-W3E-11 · Yönetici kanıt işaretleri sayfası ilk aşamada salt okumadır.** "İncelendi" işaretleme yok.

**AUD-001:** yeni CRM yazma komutları denetimsiz sayıyı artırır. Faz 8'de toplu çözülür; borç satırı EKLENMEZ.

## 1. Tur planı
| tur | prompt | çalışma yeri / dal | bağımlılık |
|---|---|---|---|
| 1 | **W3E-BE-a** · CRM: kurum QR kodu (kayıt, imza anahtarı, üret / yeniden üret / iptal / yazdır / toplu, geçmiş), ayar `qrMode`, istisna `qr_optional`, başlatmada QR doğrulaması, izinler, menü, gateway, mobil not | `C:\tmp\vw-w3e-be-a` / `wp/vw-w3e-be-a` | — |
| 1 | **W3E-BE-b** · CRM: cihaz doğrulama alanları + `antiFraudMode` + işaretler; Web vekili `clientPlatform=web`; yönetici kanıt işaretleri okuması (liste + özet, ekip kapsamı), izinler, menü, gateway, mobil not | `C:\tmp\vw-w3e-be-b` / `wp/vw-w3e-be-b` | — (BE-a ile paralel; ortak dosyalar §2.9) |
| 2 | **W3E-WEB-a** · kurum ayrıntısında QR kartı + yazdırma + toplu QR sayfası; başlatma penceresinde QR okutma (S1–S8) | `C:\tmp\vw-w3e-web-a` / `wp/vw-w3e-web-a` | **mockup** + BE-a + BE-b |
| 2 | **W3E-WEB-b** · kanıt ayarlarında "Ek kanıtlar" bölümü + istisna seçeneği; yönetici kanıt işaretleri sayfası | `C:\tmp\vw-w3e-web-b` / `wp/vw-w3e-web-b` | **mockup** + BE-a + BE-b |
| 3 | **W3E-CFG** · yerel yetki betiği (dry-run varsayılan) + canlı yetki listesi (canlıya kullanıcı uygular) | CT | WEB'ler test dalında |

## 2. Sabit sözleşme

### 2.1 Kanıt ayarına iki alan (`VisitEvidencePolicy`)
| alan | değerler | varsayılan | paket |
|---|---|---|---|
| `QrMode` | `off` · `optional` · `required` (`VisitQrModes`) | `off` | BE-a |
| `AntiFraudMode` | `off` · `flag` (`VisitAntiFraudModes`; `block` W3E-ATTEST'te gelir, şimdi geçersiz değer) | `off` | BE-b |

- **Uygulanacak yerler:**
  - `VisitEvidencePolicyDefaults`;
  - `VisitExecutionRules.ApplyPolicy` (bozuk değer → `visit_evidence_policy_invalid`);
  - `VisitEvidencePolicyInput` / `VisitEvidencePolicyDto`;
  - `VisitEvidencePolicyResolver.Values` (geçmiş satırı alan adı `qrMode` / `antiFraudMode` otomatik);
  - yürütme sözleşmesi (`contract.policy`).
- **Eski belgeler:** alan yoksa okumada varsayılan; geri yazma / göç YOK.
- **İstisna sözlüğüne `qr_optional`** (BE-a): `VisitEvidenceExceptionRules` + `Combine`'da `QrMode=required → optional`.

### 2.2 Kurum QR kodu (BE-a) — yeni toplam `InstitutionQrCode`, `Account`'a alan EKLENMEZ
**Alanlar:**
- `Id, TenantId, AccountId`;
- `Version` (int, kurum başına 1'den artar);
- `Status` `active|revoked`;
- `KeyId`;
- `IssuedAt / IssuedBy`;
- `RevokedAt / RevokedBy / RevokeReason`;
- `LastScannedAt`, `ScanCount`.

**Saklama:**
- Koleksiyon `crm_institution_qr_codes`.
- Kısmi tekil dizin `(TenantId, AccountId)` yalnız `Status=active` (`$eq` ile — **partial-index `$ne` tuzağı**).
- Class map `RegisterClassMaps`'e (**GUID tuzağı**).
- Olaylar ekleme-yalnız `crm_institution_qr_events`: `issued`, `regenerated`, `revoked`, `printed`, `bulk_issued` (kim, ne zaman, sürüm, gerekçe).

**Yük (payload) biçimi:**
- `DQR1.<accountId base64url 22>.<version>.<keyId>.<hmac base64url>`, ≤ 200 karakter.
- **İmza:** HMAC-SHA256.
  - Anahtar **kiracı başına türetilir**: HKDF(ana anahtar[keyId], tenantId).
  - Başka kiracının kodu imzada düşer → `invalid`; kiracı bilgisi sızmaz.
- **Karşılaştırma** `CryptographicOperations.FixedTimeEquals`. Desen: `services/Diten.AuthService/.../ESignReauthTicketService.cs` 107 / 120; ortak kütüphane YOK.

**Anahtarlar:**
- `ISecretsProvider` (`Diten.BuildingBlocks.Security.Secrets`, CRM.Api zaten başvuruyor); önek `crm-visit-qr-signing/`, `keyId → base64 ≥ 32 bayt`.
- Ayar `Crm:VisitQr:ActiveKeyId`.
- Döndürme: eski `keyId`'ler doğrulamada kalır, yeni üretim aktif anahtarla.
- **Development:** anahtar yoksa ve `IsDevelopment()` ise belirlenimci dev anahtarı + uyarı günlüğü.
- **Development dışında anahtar yoksa:**
  - üretim / yazdırma uçları → 503 `visit_qr_signing_unavailable`;
  - başlatma: Zorunlu kipte 503 aynı kod; İsteğe bağlı kipte başlar + `qr_unverifiable` işareti.
- Gizli değer koda, appsettings'e ya da teste GİRMEZ.

**Doğrulama sonucu (`QrStatus`):**
- `ok`;
- `missing`;
- `invalid` (bozuk biçim / imza / bilinmeyen anahtar / başka kiracı);
- `wrong_account`;
- `revoked` (sürüm aktif sürüm değil ya da kayıt iptal);
- `not_issued` (beklenen kurum zincirinde aktif kod yok);
- `not_applicable` (uzaktan ya da `off`);
- `unverifiable` (anahtar yok).

### 2.3 QR uçları (BE-a) — `api/crm/visit-qr`
| uç | izin | not |
|---|---|---|
| `GET accounts/{accountId}` | `crm.visit-qr.read` | durum, sürüm, üreten, iptal bilgisi, son okutma, son 30 gün okutma, `inheritedFrom` (üst kurumun aktif kodu); **yük YOK** |
| `GET accounts/{accountId}/history` | `.read` | olaylar, yeniden eskiye |
| `POST accounts/{accountId}/issue` | `.manage` | gövde `{ expectedVersion?, reason? }`: kod yoksa / iptalse sürüm+1 aktif; aktifken = **yeniden üret** (`expectedVersion` zorunlu, uymazsa 409 `visit_execution_concurrency_conflict`) |
| `POST accounts/{accountId}/revoke` | `.manage` | `{ expectedVersion, reason }`, gerekçe zorunlu ≤ 500 |
| `GET accounts/{accountId}/payload` | `.manage` | yazdırma için yük; `printed` olayı yazılır; `Cache-Control: no-store` |
| `POST bulk-issue` | `.manage` | `{ accountIds[≤200] }`: aktif kodu olan atlanır; kurum başına sonuç `issued|skipped_active|not_found` |
| `POST bulk-payload` | `.manage` | `{ accountIds[≤200] }` → yalnız aktif kodlar; her biri için `printed` olayı |
| `GET accounts?status=&accountType=&city=&territoryId=&search=&page=&pageSize≤100` | `.read` | toplu sayfa listesi: kurum + QR durumu (kodsuz kurum da listelenir, `status=none`); kurum süzgeci mevcut Accounts sorgusunu kullanır (43K, sunucu tarafı sayfalama) |

**Kurallar:**
- Kurum kiracıda yoksa 404 `visit_qr_account_not_found`.
- Hata kodları `VisitExecutionErrorCodes`'a: `visit_qr_*`.

### 2.4 Başlatmada QR (BE-a) — `POST api/crm/visit-execution/start`
**Yeni çok parçalı alanlar:** `qrPayload` (≤ 200), `qrScannedAt` (istemci anı; yalnız bilgi, sunucu saati esas).

**Sıra:** konum kuralından SONRA, fotoğraftan ÖNCE. Etkin ayar `effective.Policy.QrMode` (istisna `qr_optional` uygulanmış hali).

| kip | sonuç | davranış |
|---|---|---|
| `off` ya da `kind=remote` | — | `not_applicable`, yük gelse de yok sayılır |
| `required` | `ok` | başlar |
| `required` | `missing` | 400 `visit_qr_required` |
| `required` | `invalid` | 400 `visit_qr_invalid` |
| `required` | `wrong_account` | 409 `visit_qr_wrong_account` (+ hata satırı `scanned_account_id=…`, yalnız aynı kiracı) |
| `required` | `revoked` | 409 `visit_qr_revoked` |
| `required` | `not_issued` | başlar + işaret `qr_not_issued` (K-W3E-6) |
| `required` | `unverifiable` | 503 `visit_qr_signing_unavailable` |
| `optional` | `ok` / `missing` | başlar, işaret yok |
| `optional` | `invalid` / `wrong_account` / `revoked` | başlar + işaret `qr_invalid` / `qr_wrong_account` / `qr_revoked` |
| `optional` | `not_issued` | başlar, işaret yok |
| `optional` | `unverifiable` | başlar + `qr_unverifiable` |

**Beklenen kurum:** `AccountOf(plan)` + `ParentAccountId` zinciri (≤ 5, döngü korumalı). Hiçbirinde aktif kod yoksa `not_issued`. Okunan kod zincirden birine aitse `ok`.

**`VisitExecution.Qr`** (yeni gömülü nesne): `Status, AccountId?, Version?, ScannedAt?, MatchedVia (self|parent)?`.
- `ok` ise `InstitutionQrCode.LastScannedAt / ScanCount` artar: atomik `$inc`; başlatmayı bloklamaz, hata günlüğe.

**Sözleşmeye (`contract`) eklenenler:**
- `QrModes`, `QrStatuses`, `MaxQrPayloadLength`;
- yeni hata kodları + işaretler;
- izinler.

**İşaret sözlüğüne** (`VisitExecutionFlags`): `qr_not_issued, qr_invalid, qr_wrong_account, qr_revoked, qr_unverifiable`.

### 2.5 Cihaz doğrulama alanları (BE-b) — aynı başlatma ucu
**Yeni çok parçalı alanlar (hepsi isteğe bağlı):**
- `clientPlatform` `web|android|ios`;
- `appVersion` ≤ 40;
- `mockLocationDetected` bool;
- `integrityProvider` `play_integrity|app_attest`;
- `integrityVerdict` `passed|failed|unavailable`;
- `integrityToken` ≤ 8 KB: **SAKLANMAZ**; yalnız `tokenProvided=true`. W3E-ATTEST doğrulayacak.

**`VisitExecution.Device`** (yeni gömülü nesne): `Platform, AppVersion, MockLocation, IntegrityProvider, IntegrityVerdict, TokenProvided, Verified=false`.
- Ayar `off` iken de kaydedilir. İşaretler yalnız `AntiFraudMode=flag` iken.

**İşaret kuralları** (`flag` kipi):
- `mockLocationDetected=true` → `mock_location`;
- `integrityVerdict=failed` → `device_integrity_failed`;
- `clientPlatform=web` ya da beyan yok ya da `integrityVerdict=unavailable` → `device_unverified`.

Hiçbiri başlatmayı reddetmez.

**Web vekili** (`frontend/Diten.Web/Controllers/CRM/VisitWorkspaceController.cs` `api/execution/start`):
- `clientPlatform`'u `web` yazar (gelen değeri ezer);
- `mockLocationDetected`, `integrityProvider`, `integrityVerdict`, `integrityToken` alanlarını iletmez (K-W3E-10).

Sözleşmeye `AntiFraudModes, ClientPlatforms, IntegrityVerdicts, IntegrityProviders, MaxIntegrityTokenBytes`.

### 2.6 Yönetici kanıt işaretleri (BE-b) — `api/crm/visit-evidence-flags`
| uç | izin | not |
|---|---|---|
| `GET ?from=&to=&resourceId=&flags=a,b&accountId=&onlyFlagged=true&page=&pageSize≤100` | `crm.visit-evidence-flags.read` (ekip) · `.read-all` (kiracı) | satır: yürütme kimliği, plan kimliği, `startedAt`, temsilci (kimlik + ad), hedef (tür, ad, kurum adı), `kind`, işaretler, konum özeti (mesafe, hassasiyet, yarıçap, durum), QR özeti, cihaz özeti, süre (sn), istisna kimliği, fotoğraf var mı |
| `GET summary?from=&to=&resourceId=` | aynı | toplam başlatılan, işaretli, oran, işaret türüne göre sayı, temsilci başına (başlatılan / işaretli / en sık işaret) |
| `GET {executionId}` | aynı | çekmece ayrıntısı (+ istisna kuralı, tarih aralığı, onaylayan) |

**Kapsam:**
- `.read` → `TeamScopeResolver` ile ekibin temsilcileri (+ kendisi).
- `.read-all` → kiracı.
- Kapsam dışı temsilci / yürütme → **404** (sızdırmaz).
- Tarih aralığı en çok 92 gün (`visit_evidence_flags_range_invalid` 400).

**Diğer kurallar:**
- Dizin `(TenantId, StartedAt)` + `(TenantId, ResourceId, StartedAt)`.
- Ad / hedef çözümü toplu okuma (N+1 YOK).
- Fotoğraf **içeriği** bu uçtan gelmez. Küçük resim WEB-b'de mevcut kanıt okuma yolundan, kendi izniyle.

### 2.7 İzinler, menü, rotalar
**İzinler** (Auth DataSeeder, açıklama İNGİLİZCE; CRM manifest izin satırları):
- `crm.visit-qr.read`, `crm.visit-qr.manage` (BE-a);
- `crm.visit-evidence-flags.read`, `crm.visit-evidence-flags.read-all` (BE-b).

| sayfa kodu | rota | paket | Nav.Page |
|---|---|---|---|
| `CRM_VISIT_QR_CODES` | `/CRM/VisitQrCodes` | BE-a | 7 dil |
| `CRM_VISIT_EVIDENCE_FLAGS` | `/CRM/VisitEvidenceFlags` | BE-b | 7 dil |

**Gateway** (ocelot + rota testleri):
- `/api/crm/visit-qr` + `/{everything}` (BE-a);
- `/api/crm/visit-evidence-flags` + `/{everything}` (BE-b).

### 2.8 Mobil not (her BE kendi dosyası, çakışmasın)
Klasör `execution/domains/commercial-suite/work-packs/mobile/2026-10-14-visit-evidence-w3e/`:
- **BE-a → `QR.md`:**
  - yeni start alanları;
  - durum tablosu §2.4;
  - hata kodları;
  - kamera ile okutma: yük metni olduğu gibi gönderilir, istemci ayrıştırmaz / doğrulamaz;
  - `contract.policy.qrMode`.
- **BE-b → `DEVICE.md`:**
  - yeni start alanları;
  - Play Integrity / App Attest jetonunu şimdiden gönderebilir (saklanmaz);
  - `mockLocationDetected` (Android `Location.isMock` / iOS `sourceInformation.isSimulatedBySoftware`);
  - işaretler;
  - Web'in neden hep `device_unverified` olduğu.
- **İkisi de:** eski not §10 "Henüz yok" satırının güncellenmesi önerisi (istisna W3-BE-c artık var). Eski dosyaya DOKUNMA; CT birleştirir.

### 2.9 Paralel turda ortak dosyalar (BE-a ∥ BE-b)
Aşağıdaki dosyalara iki paket de **ayrı satır / ayrı blok** ekler; CT cherry-pick'te birleşimle çözer:
- `VisitEvidencePolicy.cs` + `Defaults`;
- `ApplyPolicy`;
- `VisitEvidencePolicyInput/Dto`;
- `Resolver.Values`;
- `VisitExecutionFlags`;
- `VisitExecutionErrorCodes`;
- `VisitExecutionContractDto`;
- `StartVisitExecutionCommand` + denetleyici form okuması;
- `VisitExecution` entity;
- `VisitExecutionDto`;
- CRM manifest;
- Auth DataSeeder;
- ocelot.

**Kural:**
- Mevcut satırları yeniden sıralama / biçimlendirme YOK.
- Kendi alanını en sona ekle.
- Başlatma işleyicisinde BE-a QR bloğunu konumdan sonra, BE-b cihaz bloğunu fotoğraftan hemen önce ekler; ikisi de tek yardımcı çağrısı olsun (`VisitQrVerifier.EvaluateAsync`, `DeviceEvidenceRules.Evaluate`).

## W3E-BE-a — kurum QR kodu (CRM)
1. §2.1 `QrMode` + istisna `qr_optional`.
2. §2.2:
   - `InstitutionQrCode` + olaylar + depolar + dizinler + class map;
   - `VisitQrSigner` (HKDF + HMAC, `keyId`, sabit zamanlı karşılaştırma);
   - `ISecretsProvider` kaydı;
   - Development anahtarı.
3. §2.3 uçlar + CQRS + `VisitQrController`.
4. §2.4 `VisitQrVerifier` + başlatma bloğu + `VisitExecution.Qr` + DTO + sözleşme + işaretler + hata kodları.
5. §2.7 izinler, manifest sayfası + Nav L10n 7 dil, ocelot + rota testi.
6. §2.8 `QR.md`.

**Acceptance:**
- **İmza:**
  - doğru yük `ok`;
  - tek karakter değişince `invalid`;
  - başka kiracının yükü `invalid` (kiracı sızmaz);
  - bilinmeyen `keyId` → `invalid`;
  - eski `keyId` döndürmeden sonra doğrular.
- **Sürüm:** yeniden üret → eski yük `revoked`, yeni `ok`; iptal → `revoked`; yeniden yazdır aynı yükü verir.
- **§2.4 tablosunun her satırı ayrı test:** 13 satır, `remote` dahil.
- **Üst kurum zinciri:**
  - alt birim ziyaretinde üst kodu `ok` + `MatchedVia=parent`;
  - döngülü `ParentAccountId` sonsuz döngüye girmez;
  - 6. düzey üst kabul edilmez.
- **İstisna:** `qr_optional` aktifken Zorunlu → İsteğe bağlı davranışı; etkin ayar okumasında kaynak `exception`.
- **Eşzamanlılık:** aynı kuruma eşzamanlı iki "üret" → tek aktif kod; dizin + 409.
- **Toplu:** 200 sınırı (201 → 400); aktif olan atlanır; sonuç sayıları.
- **Yetki:** `.read` yük göremez (payload 403); kiracı izolasyonu (başka kiracının kurumu 404).
- **Anahtar yok:** Development dışı → üretim 503, Zorunlu başlatma 503, İsteğe bağlı başlatma + `qr_unverifiable`.
- **Geriye uyum:**
  - `qrMode=off` iken mevcut başlatma testleri değişmeden yeşil;
  - eski ayar belgesi okunur;
  - geçmiş satırı `qrMode`.

**Sabotajlar:**
- `FixedTimeEquals` yerine imza karşılaştırması atlansın (her imza geçsin) → kırmızı.
- Sürüm denetimi kalksın (eski yük geçsin) → kırmızı.
- `not_issued` Zorunlu kipte reddetsin → kırmızı.

## W3E-BE-b — cihaz doğrulama işaretleri + yönetici kanıt işaretleri (CRM + Web vekili)
1. §2.1 `AntiFraudMode`.
2. §2.5 alanlar + `DeviceEvidenceRules` + `VisitExecution.Device` + DTO + sözleşme + işaretler.
3. Web vekili `clientPlatform=web` + doğrulama alanlarını silme (Web.Tests).
4. §2.6 uçlar + sorgular + dizinler + `TeamScopeResolver` kapsamı.
5. §2.7 izinler, manifest sayfası + Nav L10n 7 dil, ocelot + rota testi.
6. §2.8 `DEVICE.md`.

**Acceptance:**
- **İşaretler:**
  - `flag` kipinde üç işaret kuralı;
  - `off` kipinde işaret yok ama `Device` kaydı var;
  - hiçbir durumda başlatma reddedilmez;
  - `integrityToken` veritabanında yok (belge taraması), yalnız `tokenProvided`;
  - 8 KB üstü jeton → 400 `visit_capture_invalid`.
- **Web vekili:**
  - istemci `clientPlatform=android` + `integrityVerdict=passed` gönderse bile CRM'e `web` gider, doğrulama alanları gitmez;
  - fotoğraf / konum alanları bozulmaz.
- **İşaret listesi:**
  - ekip kapsamı: kendi / ekip / yabancı → 404 / `read-all`;
  - tarih sınırı 92 / 93 gün;
  - `onlyFlagged`;
  - işaret türü süzgeci;
  - sayfalama;
  - toplu ad çözümü (sorgu sayısı sabit, N+1 yok — testte sayaç).
- **Özet:** sayılar listeyle tutarlı; oran 0 bölme korumalı.
- **Geriye uyum:** `antiFraudMode=off` iken mevcut başlatma testleri yeşil; kiracı izolasyonu.

**Sabotajlar:**
- Web vekili istemcinin `integrityVerdict`'ini iletsin → kırmızı.
- Ekip kapsamı tüm kiracı olsun → kırmızı.
- `flag` kipinde `device_unverified` web için konmasın → kırmızı.

## W3E-WEB-a / W3E-WEB-b
Mockup gelince fark belgesiyle yazılacak. Ön kapsam:
- **WEB-a:**
  - Q1 kurum kartı;
  - Q2 yazdırma: QR görseli **sunucu tarafında** üretilir. Lisansı MIT bir .NET paketi, sürüm sabit, depo bağımlılık kuralına uygun. Ya da istemcide yerel dosyadan JS kütüphanesi; CDN YOK.
  - Q3 / Q4;
  - Q5 toplu sayfa;
  - S1–S8 okutma: `BarcodeDetector` varsa o, yoksa yerel dosyadan jsQR.
- **WEB-b:** E bölümü + istisna seçeneği + etkin ayar / temsilciler satırları; M sayfası (liste, çekmece, temsilci özeti); 7 dil, Arapça RTL; UAS-001.

## W3E-CFG
CT:
- **Yerel 97c5:** dört yeni izin için betik (`scripts/rbac/grant_w3e_*.py`, varsayılan dry-run).
- **Canlı (`be823006`):** yalnız izin listesi. Uygulamayı kullanıcı yapar ya da açık izin verir.
- **Canlıda imza anahtarı:** canlıya çıkış kararlarına bağlı (K-INFRA-3 ile aynı sahiplik sorusu). Anahtar yoksa canlıda `qrMode` açılmamalı.

---

## §36.1 Agent Prompt — W3E-BE-a (paste-ready · 1. TUR)
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-VW-W3E · W3E-BE-a — CRM kurum QR kodu: InstitutionQrCode (+ olaylar), kiracı başına türetilmiş HMAC imzalı sürümlü yük, üret / yeniden üret / iptal / yazdır / toplu uçları, ayar qrMode (off|optional|required), istisna qr_optional, başlatmada QR doğrulaması (üst kurum zinciri), izinler, menü sayfası, gateway, mobil not
Repository: C:\tmp\vw-w3e-be-a (worktree, test/crm-content-visit-e2e güncel başı) · Branch: wp/vw-w3e-be-a · commit bu dala, push YOK

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-VW-W3E-qr-antifraud.md — önce oku: §0 (K-W3E-1..11), §2.1, §2.2, §2.3, §2.4, §2.7, §2.8, §2.9, "W3E-BE-a". Brief: mockups/visit-workspace/BRIEF-W3E-qr-antifraud.md (Q ve S bölümleri — ekran yok, yalnız davranış).
MEVCUT: Domain/Entities/VisitEvidencePolicy.cs (VisitEvidencePolicyDefaults, VisitLocationModes deseni) · Features/VisitExecution/VisitExecutionRules.cs (ApplyPolicy 308-343, AccountOf 23, EvaluateLocation 40-103) · VisitExecutionCommandHandlers.cs StartVisitExecutionHandler 70-266 (resolver 167, konum, fotoğraf) · VisitExecutionModels.cs (StartVisitExecutionCommand 140-148, VisitEvidencePolicyInput 126-135, ContractDto 85-101, VisitExecutionDto 10-33) · VisitExecutionContract.cs (ErrorCodes 22-58) · Domain/Entities/VisitExecution.cs (Flags 52, VisitExecutionFlags 190-205) · Domain/Entities/VisitEvidenceException.cs (VisitEvidenceExceptionRules 76-88) · Features/VisitEvidence/VisitEvidencePolicyResolution.cs (Combine 87-122, Values 125-135) · Api/Controllers/CRM/VisitExecutionController.cs start 47-72 (çok parçalı form okuma). Account.cs'ye ALAN EKLEME (ParentAccountId zaten var). İmza deseni: services/Diten.AuthService/**/ESignReauthTicketService.cs 107/120 (HMACSHA256.HashData + FixedTimeEquals) — ortak kütüphane YOK, CRM'de küçük VisitQrSigner. Gizli değer: Diten.BuildingBlocks.Security.Secrets ISecretsProvider (başka serviste nasıl kayıtlı olduğuna bak; CRM.Api başvuruyor ama bugün kullanmıyor).
NE: §2.1 QrMode + qr_optional · §2.2 toplam + olaylar + depolar + kısmi tekil dizin ($eq, $ne DEĞİL) + class map (RegisterClassMaps — GUID tuzağı) + VisitQrSigner (HKDF(ana[keyId], tenantId) → HMAC-SHA256; yük DQR1.<accountId b64url>.<version>.<keyId>.<sig>; ≤200) + anahtar okuma (önek crm-visit-qr-signing/, Crm:VisitQr:ActiveKeyId; Development'ta yoksa belirlenimci dev anahtarı + uyarı; dışında 503 visit_qr_signing_unavailable) · §2.3 uçlar api/crm/visit-qr/* (payload uçları Cache-Control: no-store, printed olayı; bulk ≤200; liste sunucu tarafı sayfalı, kodsuz kurum status=none) · §2.4 VisitQrVerifier.EvaluateAsync (beklenen kurum AccountOf + ParentAccountId zinciri ≤5, döngü korumalı) + başlatma bloğu (konumdan sonra, fotoğraftan önce) + tablo 13 satır + VisitExecution.Qr + LastScannedAt/ScanCount atomik $inc (başlatmayı bloklamaz) + DTO + contract (QrModes, QrStatuses, MaxQrPayloadLength, hata kodları, işaretler, izinler) · §2.7 crm.visit-qr.read|manage (Auth DataSeeder, açıklama İNGİLİZCE) + CRM manifest sayfası CRM_VISIT_QR_CODES (/CRM/VisitQrCodes) + Nav.Page 7 dil + ocelot /api/crm/visit-qr (+/{everything}) + rota testi · §2.8 mobile/2026-10-14-visit-evidence-w3e/QR.md.
KORU/YAPMA: qrMode=off iken mevcut başlatma davranışı ve W3 testleri DEĞİŞMEZ; Account toplamına alan yok; gizli anahtar koda / appsettings'e / teste girmez (test kendi anahtarını enjekte eder); yük günlüğe yazılmaz; BE-b ile paralel — §2.9 ortak dosyalara yalnız kendi satırını en sona ekle, yeniden biçimlendirme YOK; cihaz doğrulama / işaret listesi YOK (BE-b); Web sayfası YOK (WEB-a); AUD-001 borç satırı EKLEME; seed / grant / canlı DB yazımı YOK; fleet'i durdurma.
DOĞRULA: "W3E-BE-a" acceptance (imza 5, sürüm 3, §2.4 tablosu 13 satır, üst zincir 3, istisna, eşzamanlılık, toplu, yetki, anahtar yok, geriye uyum) + 3 sabotaj (.bak + touch ile geri al, git checkout YOK); CRM + Gateway + Web (NavL10n) + Auth (DataSeeder) + mimari (-o ile repo içi çıktı); taban CRM 2742/2; denetimsiz sayı önce / sonra raporda.
Commit: "feat(crm): WP-VW-W3E-BE-a — institution QR codes (signed versioned payload, per-tenant derived key, issue/regenerate/revoke/print/bulk, history), qrMode policy + qr_optional exception, QR verification at visit start with parent-institution chain" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor: uçlar, yük örneği (dev anahtarıyla), test sayıları önce / sonra, sabotaj kanıtı, sapmalar. §22 TÜRKÇE. K13.
```

## §36.1 Agent Prompt — W3E-BE-b (paste-ready · 1. TUR, BE-a ile paralel)
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-VW-W3E · W3E-BE-b — CRM cihaz doğrulama alanları (kaydet + işaretle, engelleme YOK), ayar antiFraudMode (off|flag), Web vekili clientPlatform=web zorlaması, yönetici kanıt işaretleri okuması (liste + özet + ayrıntı, ekip / kiracı kapsamı), izinler, menü sayfası, gateway, mobil not
Repository: C:\tmp\vw-w3e-be-b (worktree, test/crm-content-visit-e2e güncel başı) · Branch: wp/vw-w3e-be-b · commit bu dala, push YOK

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-VW-W3E-qr-antifraud.md — önce oku: §0 (özellikle K-W3E-3, -4, -10, -11), §2.1, §2.5, §2.6, §2.7, §2.8, §2.9, "W3E-BE-b". Brief: mockups/visit-workspace/BRIEF-W3E-qr-antifraud.md (S7 ve M bölümleri — ekran yok, yalnız veri ihtiyacı).
MEVCUT: Domain/Entities/VisitEvidencePolicy.cs · Features/VisitExecution/VisitExecutionRules.cs (ApplyPolicy 308-343, ApplyFinish 284-295 short_visit) · VisitExecutionCommandHandlers.cs StartVisitExecutionHandler 70-266 · VisitExecutionModels.cs (StartVisitExecutionCommand 140-148, ContractDto 85-101, VisitExecutionDto 10-33) · Domain/Entities/VisitExecution.cs (Flags 52, VisitExecutionFlags 190-205, Location, ExceptionId, RelaxedRules) · Features/VisitEvidence/VisitEvidencePolicyResolution.cs (Values 125-135) · Features/DoctorInsights/DoctorInsightsVisibility.cs TeamScopeResolver (ekip kapsamı deseni, yabancı → 404) · VisitEvidenceRepRoster (temsilci adları) · Web: frontend/Diten.Web/Controllers/CRM/VisitWorkspaceController.cs api/execution/start 183-211 (çok parçalı tampon + iletme).
NE: §2.1 AntiFraudMode (off|flag; block şimdi geçersiz değer) · §2.5 start alanları (clientPlatform, appVersion, mockLocationDetected, integrityProvider, integrityVerdict, integrityToken ≤8KB — SAKLANMAZ, yalnız tokenProvided) + DeviceEvidenceRules.Evaluate (fotoğraftan hemen önce tek çağrı) + VisitExecution.Device (Verified=false) + işaretler mock_location / device_integrity_failed / device_unverified (yalnız flag kipinde; off kipinde Device yine kaydedilir; hiçbir durumda reddetmez) + DTO + contract (AntiFraudModes, ClientPlatforms, IntegrityVerdicts, IntegrityProviders, MaxIntegrityTokenBytes) · Web vekili: clientPlatform=web yazar (geleni ezer), mockLocationDetected / integrityProvider / integrityVerdict / integrityToken iletmez (Web.Tests) · §2.6 api/crm/visit-evidence-flags (liste, summary, {executionId}) — .read = TeamScopeResolver ekibi + kendisi, .read-all = kiracı, kapsam dışı 404, aralık ≤92 gün, dizinler (TenantId,StartedAt) + (TenantId,ResourceId,StartedAt), hedef / temsilci adları toplu okuma (N+1 YOK), fotoğraf içeriği YOK (yalnız hasPhoto) · §2.7 crm.visit-evidence-flags.read|read-all (Auth DataSeeder, açıklama İNGİLİZCE) + CRM manifest sayfası CRM_VISIT_EVIDENCE_FLAGS (/CRM/VisitEvidenceFlags) + Nav.Page 7 dil + ocelot /api/crm/visit-evidence-flags (+/{everything}) + rota testi · §2.8 mobile/2026-10-14-visit-evidence-w3e/DEVICE.md.
KORU/YAPMA: antiFraudMode=off iken mevcut başlatma davranışı ve W3 testleri DEĞİŞMEZ; Web vekilinde fotoğraf / konum iletimi bozulmaz; jeton veritabanına / günlüğe yazılmaz; Play Integrity / App Attest sunucu doğrulaması YOK (W3E-ATTEST); BE-a ile paralel — §2.9 ortak dosyalara yalnız kendi satırını en sona ekle, yeniden biçimlendirme YOK; QR YOK (BE-a); Web sayfası YOK (WEB-b; yalnız vekil değişikliği); AUD-001 borç satırı EKLEME; seed / grant / canlı DB yazımı YOK; fleet'i durdurma.
DOĞRULA: "W3E-BE-b" acceptance (işaret kuralları, jeton saklanmıyor, Web vekili, kapsam kendi / ekip / yabancı / read-all, 92 / 93 gün, süzgeçler, sayfalama, N+1 sayaç, özet tutarlılığı, geriye uyum, kiracı izolasyonu) + 3 sabotaj (.bak + touch ile geri al, git checkout YOK); CRM + Web + Gateway + Auth (DataSeeder) + mimari (-o ile repo içi çıktı); taban CRM 2742/2, Web 1681/2; denetimsiz sayı önce / sonra raporda.
Commit: "feat(crm,web): WP-VW-W3E-BE-b — device evidence at visit start (record + flag: mock location, integrity failed, unverified), antiFraudMode policy, web proxy forces clientPlatform=web, manager visit evidence flags list + summary (team / tenant scope)" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor: uçlar, örnek yanıtlar, test sayıları önce / sonra, sabotaj kanıtı, sapmalar. §22 TÜRKÇE. K13.
```
