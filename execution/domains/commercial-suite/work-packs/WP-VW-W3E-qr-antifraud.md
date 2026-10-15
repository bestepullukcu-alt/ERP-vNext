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

## §37 CT kabul — W3E-BE-a E2 ACCEPTED (koşullu, 2026-10-14)
Test dalına alındı: `b997aeb77` (ajan `60f2af40d`), çakışmasız.

**CT kontrolü:**
- **İmza:** HKDF(ana[keyId], tenantId) → HMAC-SHA256; `FixedTimeEquals`; yük kiracı taşımıyor. Ajanın bulduğu base64url kanonik kodlama açığı (son karakterin kullanılmayan bitleri) ayrıştırıcıda kapatılmış ve testli.
- **Anahtar halkası:** `ISecretsProvider` (`crm-visit-qr-signing:<keyId>`, Production'da ortam değişkeni `crm-visit-qr-signing__<keyId>`). Development dışında yedek anahtar yok. `AddSecretsProvider` başlangıçta doğrulama yapmıyor; anahtar yokken CRM yine açılır, yalnız QR uçları 503 döner.
- **Doğrulayıcı:** §2.4'teki 13 satırın hepsi kodda. Üst kurum zinciri en çok 5 düzey, döngü korumalı. Uzaktan ziyarette QR sorulmuyor (`not_applicable`).
- **Testler** (worktree, CT `-o`):

  | takım | sonuç | kırmızılar |
  |---|---|---|
  | CRM | 2786/2/5 | `VisitPlanningFix2…shared_pattern` (CT `-o` yol artefaktı) + `VwW4DoctorInsights.A_manual_profile_edit…` (tabanda da var) |
  | Gateway | 181/1 | 5063–5065 bilinen port kırmızısı |
  | Web (Nav / VwW3E süzgeci) | 62/1 | `WorkCenterMenuWithoutPermissions` (CT `-o` artefaktı) |
  | Auth (seed / izin süzgeci) | 351/3 | 3'ü de CT `-o` artefaktı: `permission-scope-baseline.csv` / `DataSeeder.cs` bulunamadı. Ajan koşusunda yeşil. |

- **CT sabotajı (ajanın dokunmadığı kural):** `IVisitQrVerifier` DI kaydı silindi → QR / VisitExecution / ScaffoldSmoke süzgecinde **113/113 YEŞİL** kaldı. `.bak` + touch ile geri alındı.

**Açık (CT-1, düzeltilecek) — fail-open:** `StartVisitExecutionHandler`'da doğrulayıcı isteğe bağlı parametre (`qrVerifier = null`); null iken QR "kapalı" sayılıyor. DI kaydı kaybolursa ya da başka bir host işleyiciyi doğrulayıcısız kurarsa, `qrMode=required` canlıda **sessizce uygulanmaz** ve hiçbir test bunu yakalamaz.
- **Önerilen düzeltme (küçük):**
  1. İşleyicide `_qr is null` iken etkin `QrMode != off` ise başlatma 503 `visit_qr_signing_unavailable` döner (fail-closed); `off` iken davranış aynı kalır.
  2. `AddApplication` sonrası `IVisitQrVerifier`'ın kayıtlı olduğunu ve MediatR'ın çözdüğü başlatma işleyicisinin doğrulayıcıyı aldığını ölçen test.
- Sabotaj tekrarı (kayıt silinir) → kırmızı olmalı.

**Not / izlenecek:**
- **Anahtar halkası önbelleği:** ilk yükleme sonucunu süreç ömrü boyunca tutuyor; ilk okumada anahtar yoksa bu "yok" sonucu da önbelleğe giriyor. Canlıda anahtar sonradan eklenirse CRM yeniden başlatılmalı. Operatör notu.
- **İptal edilmiş kod + aktif kod yok:** iptal edilmiş eski bir kod okutulursa zorunlu kipte 409 `visit_qr_revoked` döner. Okutmadan devam edilirse `not_issued` → başlar + işaret. WEB-a'daki S4 metni temsilciyi "Kodu bulamıyorum" yoluna yönlendirmeli.
- **Yeniden üretme atomik değil** (eski iptal → yeni ekleme). Arada hata olursa kurum kodsuz kalır; çift aktif kod oluşamaz (kısmi tekil dizin).
- **BE-b birleşiminde beklenen çakışmalar:**
  - `VwW3AuthoringTests` etkin alan sayısı 9 → 10;
  - §2.9 ortak dosyalar.
- **Mimari:** CRM denetimsiz komut 222 → 227 (+5; AUD-001, Faz 8). Borç satırı eklenmedi.
- **Mobil not:** `mobile/2026-10-14-visit-evidence-w3e/QR.md` → CT mobil sayfasına taşıyacak (BE-b'nin `DEVICE.md`'siyle birlikte).

## §37 CT kabul — W3E-BE-b E2 ACCEPTED (2026-10-14) + BE-a ∥ BE-b birleşimi
Test dalına alındı:
- `9294c90e1` (ajan `53ee0c36b`);
- `302182712` (ajan `da8631629`, menü sırası 71);
- birleşim düzeltmesi `93700f673`.

**Çakışmalar:** 18 dosya. §2.9 ortak dosyalarında iki blok birlikte alındı.
- `VwW3AuthoringTests` etkin alan sayısı **10**.
- `ApplyPolicy` iki ayrı değişken: `b4` = `qrMode`, `b5` = `antiFraudMode`.
- Ocelot: iki dalın rotaları birlikte (319 rota).
- **CT'nin birleşim hatası (düzeltildi):**
  - başlatma komutunun parametre listesi yanlış birleşmişti;
  - 7 resx dosyasında bir `</data>` düşmüştü.
  - `9294c90e1` tek başına derlenmez; `93700f673` ile düzelir.

**CT kontrolü:**
- **Web vekili:** gövde `MultipartReader` ile yeniden kuruluyor. Beş alan düşürülüyor, `clientPlatform=web` sona ekleniyor. Fotoğraf parçası dosya adı ve türüyle aynen geçiyor.
- **Kapsam:** `.read` = ekip + kendisi, `.read-all` = kiracı. Ayrıntı ucu kapsam dışı yürütmede 404 dönüyor (`Sees`).
- **Jeton:** denetleyicide bayt sayısına indiriliyor, uygulama katmanına içerik girmiyor.
- **Birleşik ağaçta testler** (`C:\tmp\vw-w3e-merge`, CT `-o`):

  | takım | sonuç | kırmızılar |
  |---|---|---|
  | CRM | 2808/2/5 | `VisitPlanningFix2…shared_pattern` (`-o` artefaktı) + `VwW4DoctorInsights…` (tabanda da var) |
  | Web (W3E / start / Nav / L10n) | 116/1 | `WorkCenterMenuWithoutPermissions` (`-o` artefaktı) |
  | Gateway | 185/1 | 5063–5065 bilinen |
  | Auth | seed süzgeci 320/3 | 3'ü de `-o` artefaktı; `PermissionScopePreservation` + `AccountKindSeed` `-o`'suz **6/6** |
  | Platform manifest | 167/8 | 8'i Mongo test hattı yok (`DITEN_PLATFORM_TEST_MONGO_URI` / yerel mongod). Ajan hatlı koşuda 175/175. |

- **CT sabotajı (ajanın dokunmadığı kural):** ayrıntı ucunun kapsam denetimi kaldırıldı → `A_manager_sees_the_team_and_self…` **kırmızı**. `.bak` + touch ile geri alındı.

**Açık / izlenecek:**
- **CT-2 (BE-c):** işaret satırında `Qr` alanı hâlâ `object? = null`; BE-a birleşti ama doldurulmuyor.
- **CT-1 (BE-c):** fail-open, BE-a §37.
- **Mimari:** BE-b denetimsiz komut eklemedi (yalnız sorgu).
- **Mobil not:** `DEVICE.md` + `QR.md` → CT mobil sayfasına taşıyacak.
- **Kalıntı:** `C:\tmp\vw-w3e-arch-base` boş klasörü yeniden başlatmada silinebilir.

## Mockup geldi (2026-10-14) → fark belgesi
Mockup: `mockups/visit-workspace/W3E Ek Kanitlar (standalone).html` (30 ekran, brief'in tamamı).
Fark belgesi: [DESIGN-GAP-W3E-2026-10-14](mockups/visit-workspace/DESIGN-GAP-W3E-2026-10-14.md), D-1 … D-14.
- Mockup'ın istediği küçük arka uç eklemeleri ve CT-1 / CT-2 → **W3E-BE-c**.
- Ardından **W3E-WEB-a ∥ W3E-WEB-b**.

### 1b. Tur planı (güncel)
| tur | prompt | çalışma yeri / dal | bağımlılık |
|---|---|---|---|
| 2 | **W3E-BE-c** · CT-1 fail-closed + DI testi; CT-2 işaret satırında QR özeti; `qr-check` ön kontrol ucu; yazdırma `paper` / `copies`; Q5 satır alanları; Web vekili tarayıcı özeti; ayrıntıya slayt / fotoğraf saati | `C:\tmp\vw-w3e-be-c` / `wp/vw-w3e-be-c` | BE-a + BE-b test dalında ✅ |
| 3 | **W3E-WEB-a** · Q1–Q5 + S1–S8 | `C:\tmp\vw-w3e-web-a` / `wp/vw-w3e-web-a` | BE-c test dalında |
| 3 | **W3E-WEB-b** · E1–E3 + M1–M6 | `C:\tmp\vw-w3e-web-b` / `wp/vw-w3e-web-b` | BE-c test dalında; WEB-a ile paralel |
| 4 | **W3E-CFG** (CT) | — | WEB'ler test dalında |

### 2.10 QR ön kontrol ucu (BE-c) — `POST api/crm/visit-execution/qr-check`
- **İzin:** `crm.visit-execution.record`.
- **Gövde:** `{ plannedVisitId, qrPayload? }`.
- **Kurallar:**
  - Başlatmanın sahiplik ve plan kuralları aynen uygulanır: kendi planı, onaylı, bugün; aksi hâlde başlatmayla aynı hata.
  - Etkin ayar ve `VisitQrVerifier.EvaluateAsync` başlatmadakiyle aynı.
  - **Hiçbir şey yazılmaz:** yürütme yok, okutma sayacı yok, olay yok.
- **Yanıt:**
  - `qrMode`;
  - `status` (§2.4 `QrStatuses`);
  - `wouldRefuse` (bool) + `errorCode`: başlatma bu sonuçla reddedilirdi mi;
  - `flag`;
  - `accountId`, `accountName`: okunan kodun kurumu, yalnız aynı kiracı ve imza geçerliyse;
  - `matchedVia`;
  - `expectedHasActiveCode` (bool): yük boşken S6 için `not_issued` ↔ `missing` ayrımı.
- Yük günlüğe yazılmaz.
- **Web vekili:** `POST /CRM/VisitWorkspace/api/execution/qr-check` (JSON).
- **Mobil not:** `QR.md`'ye ek bölüm.

## W3E-BE-c — mockup arka uç eklemeleri + CT-1 / CT-2 (CRM + Web vekili)
1. **CT-1:** işleyicide doğrulayıcı null + `qrMode != off` → 503 `visit_qr_signing_unavailable`. `AddApplication` + sahte depolar / anahtar halkasıyla MediatR'ın çözdüğü başlatma işleyicisinin doğrulayıcıyı aldığını ölçen test (ScaffoldSmoke gibi `localhost:27017`'ye BAĞLANMADAN).
2. **CT-2:** `VisitEvidenceFlagRowDto.Qr` → `VisitEvidenceQrSummaryDto(Status, AccountId, AccountName, Version, ScannedAt, MatchedVia)`. Kurum adı satır sayfasının toplu kurum okumasına katılır (N+1 YOK). Ayrıntıya `SlideSeconds`, `SlideCount`, `PhotoCapturedAt`.
3. **§2.10** `qr-check` + Web vekili ucu + testler.
4. **D-4:** `payload` / `bulk-payload` `paper` + `copies` (doğrulama; olay ayrıntısı; `VisitQrEventDto`'ya `Paper`, `Copies`).
5. **D-5:** `VisitQrListItemDto` + `ParentAccountId`, `ParentAccountName`, `InheritedFrom` (`VisitQrInheritedDto`), `RecentScanCount`. Toplu okuma, sayfa başına sabit sorgu.
6. **D-2:** Web vekili kullanıcı ajanından kısa tarayıcı özeti → `appVersion` (≤ 40, deterministik ayrıştırıcı + test; bilinmeyen → "Web").

**Acceptance:**
- **CT-1:** doğrulayıcısız işleyici + `required` → 503; `off` → 201. DI testi kırmızı / yeşil kanıtı.
- **CT-2:** satırda QR özeti ve kurum adı; sorgu sayacı sabit.
- **`qr-check`:**
  - §2.4 tablosunun her kip / sonuç satırı için `wouldRefuse` / `errorCode` / `flag` başlatmayla **aynı**; ortak tablo testi;
  - yazma yok (depo sayaçları 0);
  - başka temsilcinin planı → başlatmayla aynı hata;
  - `expectedHasActiveCode`.
- **`paper` / `copies`:** geçersiz → 400; olayda kayıtlı.
- **Q5 alanları:** üst kurum, devralınan kod, 30 gün sayısı.
- **Tarayıcı özeti:** 6 kullanıcı ajanı örneği.

**Sabotajlar:**
- `qr-check` okutma sayacını artırsın → kırmızı.
- CT-1 kontrolü kalksın → kırmızı.
- Satır QR özeti başka kiracının kurum adını çözsün → kırmızı.

## §36.1 Agent Prompt — W3E-BE-c (paste-ready · 2. TUR)
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-VW-W3E · W3E-BE-c — mockup arka uç eklemeleri + CT-1 / CT-2: başlatmada doğrulayıcı yokken fail-closed + DI testi; yönetici işaret satırında gerçek QR özeti (kurum adıyla) + ayrıntıya slayt süresi / sayısı + fotoğraf saati; yazmayan QR ön kontrol ucu (qr-check) + Web vekili; yazdırma paper / copies olayda; kurum QR listesinde üst kurum / devralınan kod / 30 gün okutma; Web vekili tarayıcı özeti → appVersion
Repository: C:\tmp\vw-w3e-be-c (worktree, test/crm-content-visit-e2e güncel başı — BE-a + BE-b + birleşim 93700f673 içerir) · Branch: wp/vw-w3e-be-c · commit bu dala, push YOK

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-VW-W3E-qr-antifraud.md — önce oku: §0, §2.2–§2.6, §2.10, "W3E-BE-c", §37 BE-a (CT-1) ve §37 BE-b (CT-2). Fark belgesi: mockups/visit-workspace/DESIGN-GAP-W3E-2026-10-14.md (D-2, D-3, D-4, D-5, D-6, D-7) — mockup ekranları S2 / S3 / S6, Q1 geçmiş, Q5, M1 / M2 (önizleme: .claude/launch.json "mockups-workspace" 5098, dosya "W3E Ek Kanitlar (standalone).html"; AYRI sekmede aç).
MEVCUT: Features/VisitQr (VisitQrVerifier.EvaluateAsync + ChainAsync, VisitQrHandlers, VisitQrModels: VisitQrListItemDto, VisitQrInheritedDto, VisitQrEventDto) · StartVisitExecutionHandler (qrVerifier isteğe bağlı parametre — CT-1) · Features/VisitEvidenceFlags (VisitEvidenceFlagRowDto.Qr = object? null — CT-2; toplu ad okuması; Detail) · VisitExecution.Qr / SlideTimings / Photo · Web: Controllers/CRM/VisitWorkspaceController.cs (api/execution/start vekili, WebStartBodyAsync, WebStartDroppedFields).
NE: (CT-1) işleyicide doğrulayıcı null + etkin qrMode != off → 503 visit_qr_signing_unavailable; AddApplication + sahte depolar / IVisitQrKeyRing ile MediatR'ın çözdüğü başlatma işleyicisinin doğrulayıcıyı aldığını ölçen test (Mongo'ya BAĞLANMA; localhost:27017 YOK) · (CT-2) VisitEvidenceQrSummaryDto(Status, AccountId, AccountName, Version, ScannedAt, MatchedVia) satıra; kurum adı aynı toplu kurum okumasına katılır (N+1 YOK, kiracı sınırlı); ayrıntıya SlideSeconds, SlideCount, PhotoCapturedAt · (§2.10) POST api/crm/visit-execution/qr-check — başlatmanın sahiplik / plan kuralları + aynı etkin ayar + aynı VisitQrEvaluation; yanıt qrMode, status, wouldRefuse, errorCode, flag, accountId, accountName, matchedVia, expectedHasActiveCode; HİÇBİR ŞEY YAZMAZ; Web vekili POST /CRM/VisitWorkspace/api/execution/qr-check (JSON) · (D-4) payload + bulk-payload paper (a4|a5|labels4|labels8) + copies (1–50) → printed olayı; VisitQrEventDto + Paper, Copies · (D-5) VisitQrListItemDto + ParentAccountId, ParentAccountName, InheritedFrom, RecentScanCount (toplu okuma, sayfa başına sabit sorgu) · (D-2) Web vekili User-Agent → kısa tarayıcı özeti ≤40 ("Safari 18 · iPadOS"), appVersion olarak yazılır (istemcinin appVersion'ı web'de yok sayılır); bilinmeyen → "Web" · mobil not mobile/2026-10-14-visit-evidence-w3e/QR.md'ye qr-check bölümü (eski bölümleri değiştirme, ekle).
KORU/YAPMA: başlatma davranışı qrMode=off / antiFraudMode=off iken DEĞİŞMEZ; qr-check sayaç / olay / yürütme YAZMAZ; yük günlüğe yazılmaz; kiracı izolasyonu (başka kiracının kurum adı asla çözülmez); Web sayfası YOK (WEB-a / WEB-b); AUD-001 borç satırı EKLEME; seed / grant / canlı DB YOK; fleet'i durdurma.
DOĞRULA: "W3E-BE-c" acceptance + 3 sabotaj (.bak + touch ile geri al, git checkout YOK): (1) qr-check okutma sayacını artırsın → kırmızı; (2) CT-1 kontrolü kalksın → kırmızı; (3) satır QR özeti kiracı süzgeçsiz kurum adı okusun → kırmızı. CRM + Web + mimari (-o ile repo içi çıktı); taban CRM 2808/2 (birleşik), Web birleşik; denetimsiz sayı önce / sonra.
Commit: "feat(crm,web): WP-VW-W3E-BE-c — fail-closed QR start without a verifier, QR summary on evidence flag rows, qr-check pre-start endpoint, print paper/copies, QR list parent/inherited/recent scans, web browser summary as appVersion" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor TÜRKÇE (§22). K13.
```

## §36.1 Agent Prompt — W3E-WEB-a (paste-ready · 3. TUR, BE-c test dalına alındıktan SONRA)
```text
@[.antigravity/agents/frontend-ui-ux.md]
WP: WP-VW-W3E · W3E-WEB-a — kurum QR kodu ekranları (Q1 kurum ayrıntısında kart + geçmiş, Q2 yazdırma tekli + etiket sayfası, Q3 yeniden üret, Q4 iptal, Q5 toplu QR kodları sayfası) + temsilcinin başlangıç penceresinde QR okutma (S1–S8)
Repository: C:\tmp\vw-w3e-web-a (worktree, test/crm-content-visit-e2e güncel başı — BE-a/b/c içerir) · Branch: wp/vw-w3e-web-a · commit bu dala, push YOK

ŞARTNAME: mockups/visit-workspace/DESIGN-GAP-W3E-2026-10-14.md (kararlar mockup'ın önüne geçer: D-3, D-8, D-9, D-10, D-12, D-13, D-14 + §4 uç eşlemesi) + mockup "W3E Ek Kanitlar (standalone).html" ekranları Q1a, Q1a (alt birim), Q1b, Q1c, Q1d, Q1 geçmiş, Q2 tekli, Q2 etiket, Q3, Q4, Q5, Q5 sonuç, Q5 boş, S1–S8 (önizleme: .claude/launch.json "mockups-workspace" 5098; çözülmüş: mockups/visit-workspace/decoded-w3e/). Paket: WP-VW-W3E-qr-antifraud.md §0, §2.2–§2.4, §2.10, §37 notları. Görsel dil ve metinler mockup'la BİREBİR; bitince her ekranı mockup'la yan yana karşılaştır (ekran görüntüsü raporda).
MEVCUT: Controllers/CRM/AccountsController.cs (Details/{id}) + Views/CRM/Accounts/Details.cshtml + _DetailsL10n.cshtml (bölüm kartları) · Controllers/CRM/VisitWorkspaceController.cs (start + qr-check vekilleri, contract) · wwwroot/assets/js/CRM/VisitWorkspace/visit-workspace.js (E3 başlangıç penceresi ~663-826: fotoğraf, konum, "Eksik: …", Sunumu başlat pasif kuralı) · desen: W4 PotentialThresholds sayfası (standart CRM sayfa iskeleti, UAS-001, 7 dil resx + L10n köprüsü — recurring gotcha "L10n bridge").
NE: (Q1) kurum ayrıntısına "Kurum QR kodu" kartı — durumlar a/b/c/d, alt birimde üst kurum bilgi satırı (inheritedFrom), bulanık SÜS önizleme (yük çekilmez, D-8), Geçmiş bağlantısı → zaman çizelgesi (olaylar + paper/copies + gerekçe); izinler crm.visit-qr.read (kart) / .manage (düğmeler), salt okumada düğmeler gizli · (Q2) yazdırma sayfası tekli (A4/A5) + etiket sayfası (4/8, kesikli çizgi) — QR SVG Web sunucusunda üretilir (D-9: MIT .NET paketi, sürüm sabit, depo bağımlılık kuralı; uç Cache-Control no-store; yük günlüğe yazılmaz; kişisel veri basılmaz) · (Q3 / Q4) pencereler, expectedVersion, 409 sürüm çakışması mesajı, iptalde gerekçe zorunlu ≤500 · (Q5) yeni sayfa /CRM/VisitQrCodes (manifest CRM_VISIT_QR_CODES): süzgeçler bölge / tür / şehir / QR durumu (+ metin arama), sunucu tarafı sayfalama (43K kurum), satırda ↳ üst kurum + "üst kurum kodu geçer", çoklu seçim + "Seçilenlere kod üret" (bulk-issue) + "Seçilenleri yazdır" (etiket), sonuç şeridi, boş durum; UAS-001 · (S1–S8) başlangıç penceresinde QR satırı contract.policy.qrMode'a göre (off: yok; remote: yok), kamera (BarcodeDetector varsa, yoksa wwwroot'a konmuş jsQR — Apache-2.0, sürüm sabit, CDN YOK; telefonda arka kamera), okununca qr-check → S2 (kurum adı + "üst kurumun kodu"), S3, S4, S5 kamera izni, S6 "Kodu bulamıyorum" (isteğe bağlı → "QR olmadan devam et"; zorunlu + expectedHasActiveCode=false → cezasız devam; zorunlu + kod var → istisna yönlendirmesi), S7 (antiFraudMode=flag + web: bilgi satırı, engellemez), S8 "Eksik: QR kodu" + Sunumu başlat pasif; start'a qrPayload + qrScannedAt; başlatma hataları (visit_qr_*) kullanıcı dilinde · 7 DİL (en, tr, fr, es, zh, ar, ru) yankısız, Arapça RTL.
KORU/YAPMA: qrMode=off iken başlangıç penceresi DEĞİŞMEZ (mevcut W3 Web testleri yeşil); yük yalnız yazdırma ucunda ve manage izniyle; kullanıcının oturum açık sekmesine sahte / harness enjeksiyonu YOK (ayrı sekme); kalıcı veri yazma (seed / grant) YOK; CRM koduna DOKUNMA (sözleşme eksikse DUR ve raporla); WEB-b'nin dosyalarına (VisitEvidenceSettings, VisitEvidenceFlags) DOKUNMA.
DOĞRULA: Web.Tests (xUnit + RunNode; vitest bu makinede çalışmaz) — kart durumları, izin gizleme, yazdırma ucu no-store + yük günlüksüz, Q5 süzgeç / sayfalama sözleşmesi, S akışı (qr-check sonuçlarına göre durum, Eksik satırı, off'ta değişmezlik), 7 dil anahtar tamlığı + NavL10n; 2 sabotaj (.bak + touch): (1) zorunlu kipte okutmadan Sunumu başlat etkin olsun → kırmızı; (2) salt okuma kullanıcısına Yazdır görünsün → kırmızı. Web tam takım (-o repo içi), taban karşılaştırması. Mockup ↔ ekran karşılaştırma görüntüleri.
Commit: "feat(web): WP-VW-W3E-WEB-a — institution QR card, history, print (single + label sheet), regenerate / revoke dialogs, bulk QR codes page, QR scan at visit start (camera, qr-check states, missing line)" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor TÜRKÇE (§22). K13.
```

## §36.1 Agent Prompt — W3E-WEB-b (paste-ready · 3. TUR, BE-c test dalında; WEB-a ile paralel)
```text
@[.antigravity/agents/frontend-ui-ux.md]
WP: WP-VW-W3E · W3E-WEB-b — kanıt ayarlarında "Ek kanıtlar" bölümü (QR kipi, cihaz doğrulama) + istisnada "QR zorunluluğunu esnet" + etkin ayar / temsilciler / geçmişte iki yeni alan (E1–E3); yönetici "Kanıt işaretleri" sayfası (M1 liste + özet, M2 sağ çekmece, M3 temsilci özeti, M4 boş, M5 yetkisiz, M6 Arapça)
Repository: C:\tmp\vw-w3e-web-b (worktree, test/crm-content-visit-e2e güncel başı — BE-a/b/c içerir) · Branch: wp/vw-w3e-web-b · commit bu dala, push YOK

ŞARTNAME: mockups/visit-workspace/DESIGN-GAP-W3E-2026-10-14.md (D-1 katmanlı ayar ÇİZİLMEZ — bugünkü kiracı sayfasına eklenir; D-6, D-11, D-14 + §4) + mockup "W3E Ek Kanitlar (standalone).html" ekranları E1, E2, E3, M1–M6 (önizleme: .claude/launch.json "mockups-workspace" 5098; çözülmüş: mockups/visit-workspace/decoded-w3e/). Paket: WP-VW-W3E-qr-antifraud.md §0, §2.1, §2.5, §2.6, §37 notları. Görsel dil ve metinler mockup'la BİREBİR; bitince her ekranı mockup'la yan yana karşılaştır (ekran görüntüsü raporda).
MEVCUT: Controllers/CRM/VisitEvidenceSettingsController.cs + Views/CRM/VisitEvidenceSettings/Index.cshtml (".ve-soon" Yakında kartı 117-118 — KALDIRILIR, yerine bölüm) + wwwroot/assets/js/CRM/VisitEvidenceSettings/evidence-core.js (alan grupları 33-46) + visit-evidence-settings.js + Resources/Views/CRM/VisitEvidenceSettings/*.resx · istisna penceresi (W3-WEB-d) · desen: W4 PotentialThresholds sayfası (standart CRM sayfa iskeleti, UAS-001, 7 dil resx + L10n köprüsü).
NE: (E1) "Ek kanıtlar" bölümü: Kurum QR kodu (Kapalı / İsteğe bağlı / Zorunlu + bir cümlelik etki metinleri + "Kurum QR kodlarını yönet →" /CRM/VisitQrCodes, yetki yoksa bağlantı gizli), Cihaz doğrulama (Kapalı / İşaretle / Engelle PASİF "yakında"); PUT'a qrMode + antiFraudMode; (E2) istisna penceresine qr_optional seçeneği ("yeni" rozeti, açıklama); (E3) etkin ayar + Temsilciler sekmesinde iki yeni sütun / satır, istisnadan gelen değer "· istisna", değişiklik geçmişinde alan adları ve değerler kullanıcı dilinde (ham "qrMode" / "antiFraudMode" GÖRÜNMEZ) · (M) yeni sayfa /CRM/VisitEvidenceFlags (manifest CRM_VISIT_EVIDENCE_FLAGS): özet şeridi (başlatılan, işaretli + oran, işaret türüne göre yatay çubuklar, "Diğer (N tür)" gruplama), dönem (Son 7 gün varsayılan / Son 30 gün / bu ay; ≤92 gün), temsilci (ekibim), işaret türü (çoklu), kurum, "Yalnız işaretliler"; Ziyaretler / Temsilci özeti sekmeleri; tablo + sayfalama; işaret çipleri tarafsız dil + değer (Uzak konum · 820 m, Kısa ziyaret · 1 dk 40 sn, Konum hassasiyeti düşük · 240 m); "QR okutulmadı" çipi qr.status=missing iken (işaret DEĞİL, süzgece girmez); M2 çekmece: konum, QR (sonuç, kurum, sürüm, okutma saati), cihaz (platform + appVersion, doğrulama sonucu), süre + slayt, fotoğraf küçük resmi mevcut kanıt okuma yolundan KENDİ izniyle (yoksa "fotoğraf var"), istisna bağlantısı, "Ziyareti aç"; M3 temsilci satırı → liste o temsilciye süzülür; M4 boş; M5 UAS-001 (iskelet çizilmez); .read-all izni varsa temsilci süzgeci tüm kiracı · 7 DİL (en, tr, fr, es, zh, ar, ru) yankısız, Arapça RTL.
KORU/YAPMA: mevcut kanıt ayarı alanları ve W3-WEB-d testleri bozulmaz; katmanlı ayar (iş birimi / tüzel kişilik) ÇİZİLMEZ (D-1); kullanıcının oturum açık sekmesine sahte / harness enjeksiyonu YOK (ayrı sekme); kalıcı veri yazma YOK; CRM koduna DOKUNMA (sözleşme eksikse DUR ve raporla); WEB-a'nın dosyalarına (Accounts Details, VisitQrCodes, VisitWorkspace) DOKUNMA.
DOĞRULA: Web.Tests (xUnit + RunNode) — ayar PUT gövdesi iki alanla, Engelle seçilemez, istisna kuralı qr_optional, geçmiş etiketleri (ham anahtar yok), M süzgeç → sorgu parametreleri, 92 gün sınırı ekranda, QR okutulmadı çipi işaret süzgecine girmez, çekmece alanları, UAS-001, 7 dil anahtar tamlığı + NavL10n; 2 sabotaj (.bak + touch): (1) Engelle seçilebilir olsun → kırmızı; (2) geçmişte ham alan adı görünsün → kırmızı. Web tam takım (-o repo içi), taban karşılaştırması. Mockup ↔ ekran karşılaştırma görüntüleri.
Commit: "feat(web): WP-VW-W3E-WEB-b — additional evidence settings (QR mode, device verification), qr_optional exception, effective/reps/history fields, manager visit evidence flags page (summary, list, drawer, rep summary)" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor TÜRKÇE (§22). K13.
```

## §37 CT kabul — W3E-BE-c E2 ACCEPTED (2026-10-14)
Test dalına alındı: `c229ec603` (ajan `90a6b07dc`), çakışmasız.

**CT kontrolü:**
- **CT-1:** doğrulayıcı yokken etkin `qrMode != off` ise 503. DI testi MediatR'ın kurduğu işleyiciyi ölçüyor; ajan benim BE-a sabotajımı (DI kaydı silme) tekrar etti → kırmızı. **CT-1 kapandı.**
- **CT-2:** satırda QR özeti + kurum adı. Okuma sonrası kiracı süzgeci var; depo sızdırsa bile ad gösterilmiyor. **CT-2 kapandı.**
- **`qr-check` (`CheckVisitQrQuery`):** yazmıyor. Başlatmayla ortak `VisitStartGate` kullanılıyor. Yük `ToString`'de gizli; MediatR yalnız istek adını, Web vekili yalnız yöntem + yolu günlüğe yazıyor. Query adı sayesinde AUD-001 sayısı değişmedi (123 → 123).
- **Testler** (worktree, CT `-o`):

  | takım | sonuç | kırmızılar |
  |---|---|---|
  | CRM | 2840/2/5 | `VisitPlanningFix2…shared_pattern` (`-o` artefaktı) + `VwW4DoctorInsights…` (tabanda da var) |
  | Web (tam) | 1697/3 | `WorkCenterMenuWithoutPermissions` (`-o` artefaktı) + 2 `ShipmentDetailsDateNode` (tabanda da var) |

- **CT sabotajı (ajanın dokunmadığı kural):** yeni ortak başlatma kapısında "ziyaret zaten raporlanmış" kontrolü devre dışı → `Start_on_a_reported_visit_is_409_already_reported` **kırmızı**. `.bak` + touch ile geri alındı. Başlatma kurallarının taşınması testle korunuyor.

**Not / izlenecek:**
- **CT-1 uzaktan ziyareti de kapsıyor:** doğrulayıcı yoksa + kip `off` değilse 503. Doğrulayıcı varken uzaktan ziyaret `not_applicable`. Kabul edildi; yalnız yanlış kurulumda devreye girer.
- **`qr-check`'te konum kuralı sorulmuyor.** Konum hatası yine başlatmada döner (S ekranlarında ayrı satır).
- **Web vekili `appVersion`'ı da düşürüyor.** Tarayıcı özeti sunucu tarafında üretiliyor.
- **Mobil:** `QR.md` §8 → CT mobil sayfasına taşıyacak (`DEVICE.md` ile birlikte).

**Sıradaki:** W3E-WEB-a ∥ W3E-WEB-b (istemler yukarıda; BE-c artık test dalında).

## Not — PR #144 ve sayfa yardımı (UXH-001), 2026-10-14
**Kullanıcı kararı:** W3E-WEB-a / WEB-b **şimdi** çalıştırılır (istemler değişmez). PR #144 main'e girip test dalına senkronlanınca CT küçük bir düzeltme yapar:
- **"?" yardım metinleri:** `Help.CRMVISITQRCODES.*` ve `Help.CRMVISITEVIDENCEFLAGS.*` — Purpose, Audience, Step1..n + Title; 7 dil, `PageHelpResource`. `PageHelpCatalog` girdileri eklenir.
- **Enter kuralı:** yan pencerelerde birincil işlem Enter'la çalışır; geri alınamaz işlem (iptal, yeniden üret) yalnız Ctrl / Cmd + Enter.
- **Kısayollar** ortak katmana kaydedilir.

Deneme birleştirmesi (CT): test dalı ↔ PR #144 metin çakışmasız; CRM 2840/2, Web 2531/2 (bilinen).

## §37 CT kabul — W3E-WEB-b E2 ACCEPTED (2026-10-14)
Test dalına alındı: `02228ac38` (ajan `f209a8873`), çakışmasız. Yalnız Web + Web.Tests; CRM / gateway / WEB-a dosyalarına dokunulmadı.

**CT kontrolü:**
- **E1–E3:** "Ek kanıtlar" kartı + PUT `qrMode` / `antiFraudMode`; "block" hiçbir yoldan gönderilemiyor; `qr_optional` yalnız QR zorunlu temsilcide seçilebiliyor; geçmişte ham alan adı yok.
- **M sayfası (/CRM/VisitEvidenceFlags):** özet + liste + çekmece + temsilci özeti + boş + UAS-001; "QR okutulmadı" işaret değil, sorguya gitmiyor; fotoğraf mevcut kanıt yolundan, kendi izniyle.
- **Testler** (worktree, CT `-o`): Web tam **1710/3**. Kırmızılar: `WorkCenterMenuWithoutPermissions` (`-o` artefaktı) + 2 `ShipmentDetailsDateNode` (tabanda da var). Ajan: 1711/2, taban 1698/2 (+13 test).
- **CT sabotajı (ajanın dokunmadığı kural):** "Kurum QR kodlarını yönet" bağlantısının yetki koşulu kaldırıldı (`CanReadQrCodes = true`) → `B3_without_a_qr_key_the_manage_link_is_not_drawn…` **kırmızı**. `.bak` + touch ile geri alındı.

**Görsel karşılaştırma:**
- E1–E3 ve M5 canlı sayfada.
- M1–M4 / M6 ajanın kendi panelinde gerçek görünüm / JS / CSS + sahte veriyle (yetki yazmadan).
- Bulunan 8 fark düzeltildi; bazı "sonra" görüntüleri yok → **yerel E4'te CT M ekranlarını yeniden karşılaştıracak.**

**Açık / izlenecek:**
- Yerel Admin'e `crm.visit-evidence-flags.read` → W3E-CFG betiği (CT).
- Çekmecede onaylayan **adı** yok (CRM yalnız kimlik döndürüyor); yalnız onay tarihi gösteriliyor. → küçük takip (CRM ayrıntıya `approvedByName`).
- Kurum süzgeci seçenekleri yüklenen satırlardan toplanıyor (ayrı uç yok).
- Özel tarih aralığı eklendi (mockup'ta yok; 92 gün sınırı için).
- Ayar sayfasının genel düzeni (W3-WEB-d iki sütun) korundu; mockup'taki akordiyon / zaman çizelgesi kapsam dışı.
- **UXH-001** yardım metni + Enter işareti → SYNC-144 (7b).

## §37 CT kabul — W3E-WEB-a E2 ACCEPTED (2026-10-14)
Test dalına alındı: `1107ef8bf` (ajan `4a46f96ef`), WEB-b'nin (`02228ac38`) üstüne çakışmasız. Yalnız Web + Web.Tests; CRM / WEB-b dosyalarına dokunulmadı.

**CT kontrolü:**
- **Q1 kart:** read / manage ayrımı; önizleme süs, yük çekilmez.
- **Q2 yazdırma:** SVG Web sunucusunda (QRCoder 1.6.0, MIT, csproj'da tek satır); yük yalnız "Yazdır"da, `no-store`, sonra sayfadan silinir.
- **Q5:** sunucu sayfalama, toplu üret / yazdır.
- **S1–S8:** `off` / uzaktan ziyarette pencere W3 ile aynı, qr-check çağrısı yok. Kamera `BarcodeDetector` → yerel jsQR 1.4.0 (Apache-2.0, `wwwroot/assets/vendor/libs/jsqr`, LICENSE + VERSION yanında).
- **Testler** (CT `-o`):

  | ağaç | sonuç | kırmızılar |
  |---|---|---|
  | WEB-a worktree | **1720/3** | — |
  | **WEB-a + WEB-b birleşik** | **1733/3** | `WorkCenterMenuWithoutPermissions` (`-o` artefaktı) + 2 `ShipmentDetailsDateNode` (tabanda da var) |

- **CT sabotajı (ajanın dokunmadığı kural):** S6'da zorunlu kip + kurumda aktif kod **varken** de "kodsuz devam" açıldı → `W3E_Q3…(required)` + `W3E_Q2_the_box_follows…` **kırmızı**. `.bak` + touch ile geri alındı.

**Sapmalar (kabul):**
- **Ana renk `#0b1a8c`:** W3 / W4 canlı sayfalarıyla tutarlı; mockup Sneat moru `#696cff`. Önceki W3 / W4 kararıyla aynı.
- **Türkçe ekler:** ad bağımlı ek üretilemediği için "… kurumunda" biçimi.
- **Mockup'a eklenenler:** yazdırmada "Adet" alanı (D-4); Q5 sayfalama; mockup'ta olmayan iki durum (`unverifiable`, ön kontrol ağ hatası).
- **W3 testi A7 daraltıldı:** W3E QR'ı bilerek ekliyor; diğer yasaklar duruyor.
- **Hesap ayrıntısı:** QR kartı ana sütunun en üstünde.

**Açık / izlenecek:**
- **Görsel karşılaştırma** ajanın düzeneğinde (gerçek view / CSS / JS + sahte veri). Arapça ve telefon görünümü gerçek ortamda açılmadı → **yerel E4'te CT.**
- **W3E-CFG:** yerel 97c5 yetki betiği (`crm.visit-qr.read|manage`, `crm.visit-evidence-flags.read|read-all`).
- **UXH-001:** yardım + Enter → SYNC-144 (7b).
- **Mobil not:** `QR.md` + `DEVICE.md` → CT mobil sayfasına.


## K-THEME — ana renk sistemin layout renginden gelir (kullanıcı kararı, 2026-10-14)
- **Karar:** CRM ziyaret sayfalarının ana rengi **sabit yazılmaz**. Layout'taki Template Customizer'da seçilen "Primary Color" neyse o kullanılır: `--bs-primary` / `--bs-primary-rgb`. Varsayılan `#696cff` (`vendor/css/core.css`); mockup'lardaki mor da budur.
- **Önceki uygulama yanlıştı:** W2–W4 ve W3E-WEB-a lacivert `#0b1a8c`, W3E-WEB-b mor `#696cff` sabit yazmış. Layout'ta renk değişince bu sayfalar değişmiyor.
- **Kalıcı kural:** bundan sonraki her Web istemi ana rengi ve türevlerini (açık zemin, kenar, metin, çubuk) yalnız `var(--bs-primary)` / `rgba(var(--bs-primary-rgb), α)` ile yazar.
- **Anlamsal renkler** (başarı / uyarı / tehlike, hastane turkuazı gibi tür renkleri) bu kararın dışındadır.

## W3E-THEME — ziyaret sayfalarında ana rengi layout'a bağlama (Web)

**Kapsam (11 dosya, yalnız CRM ziyaret ailesi):**
- `wwwroot/assets/css/visit-planning.css`;
- `js/CRM/VisitPlanning/details.js`;
- `js/CRM/VisitWorkspace/visit-workspace.css`;
- `js/CRM/VisitPresentation/visit-presentation.css` + `visit-presentation.js` (canvas);
- `js/CRM/VisitReport/visit-report.css`;
- `js/CRM/PharmacyReport/pharmacy-report.css`;
- `js/CRM/PotentialThresholds/potential-thresholds.css`;
- `js/CRM/VisitEvidenceSettings/visit-evidence-settings.css`;
- `js/CRM/VisitQrCodes/visit-qr.css`;
- `js/CRM/VisitEvidenceFlags/visit-evidence-flags.css`.

**Kapsam dışı (XMC-001, başka modül):** `backbone-custom.css`, `pages/demand-idea-capture.css`. Raporda listelenir, dokunulmaz.

**Yapılacaklar:**
- Sayfa token'ları (`--vw-p`, `--vp-indigo*`, `--ph-p*`, `--vq-p*`, `--vf-p*` …) `--bs-primary` / `--bs-primary-rgb`'den türetilir. Açık zemin, kenar, ikincil metin ve çubuk tonları `rgba(var(--bs-primary-rgb), α)` olur; açık ve koyu tema için ayrı α değerleri.
- JS içindeki satır içi renkler (`details.js` `TYPE_TONE` / `ACTIVE_ACCOUNT_STYLE`, sunum canvas `strokeStyle`) çalışma anında `getComputedStyle(document.documentElement).getPropertyValue('--bs-primary')` ile okunur, ya da CSS sınıfına taşınır.
- QR yazdırma SVG'si siyah kalır (okunabilirlik).

**Acceptance:**
- **Guard testi:** kapsamdaki 11 dosyada eski ana renk ailesi (`#0b1a8c #e0e2f3 #c5c9e8 #4b55a8 #f3f4fb #4655d6 #2f3163 #4a4f8c #a3aaf5 #696cff #e7e7ff #5f61e6 #a5a6ff #7b7eff #35366a #a3a5ff #6d70e8`) **0**.
- **Davranış testi** (RunNode / sahte DOM): `:root`'ta `--bs-primary` değiştirilince sayfanın birincil öğesinin hesaplanan rengi değişir.
- Açık / koyu tema okunabilir. Mevcut W2–W4 / W3E Web testleri yeşil.
- **Görsel kanıt:** her sayfa ailesinden bir ekran; varsayılan renk + customizer'da başka bir renk (ör. turuncu), açık ve koyu.

**Sabotajlar:**
- Bir dosyaya `#0b1a8c` geri yazılsın → guard kırmızı.
- Sunum canvas'ı sabit renge dönsün → davranış testi kırmızı.

## §36.1 Agent Prompt — W3E-THEME (paste-ready · E4'ten önce)
```text
@[.antigravity/agents/frontend-ui-ux.md]
WP: WP-VW-W3E · W3E-THEME — CRM ziyaret sayfalarında ana rengi layout'un Template Customizer rengine bağlamak (--bs-primary / --bs-primary-rgb); sabit lacivert / mor kaldırılır
Repository: C:\tmp\vw-w3e-theme (worktree, test/crm-content-visit-e2e güncel başı) · Branch: wp/vw-w3e-theme · commit bu dala, push YOK

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-VW-W3E-qr-antifraud.md — önce oku: "K-THEME" ve "W3E-THEME" (kapsam 11 dosya, kapsam dışı 2 dosya, acceptance, sabotajlar).
MEVCUT: wwwroot/assets/vendor/css/core.css (--bs-primary #696cff, --bs-primary-rgb), vendor/js/template-customizer.js + assets/js/config.js (Primary Color seçimi), Views/Shared/_LayoutTenantShell.cshtml (customizer yüklemesi, tema anahtarı) · sayfa token blokları: visit-planning.css (--vp-indigo*), visit-workspace.css (--vw-p*), pharmacy-report.css (--ph-p*), visit-qr.css (--vq-p*), visit-evidence-flags.css (--vf-p*) ve diğerleri (açık + koyu blokları) · JS: VisitPlanning/details.js 1013-1014, VisitPresentation/visit-presentation.js 490 (canvas).
NE: sayfa token'larını --bs-primary / rgba(var(--bs-primary-rgb), α)'dan türet (açık + koyu ayrı α); JS satır içi renkleri hesaplanan --bs-primary'ye ya da CSS sınıfına taşı; anlamsal renklere (ok / warn / danger, tür renkleri) DOKUNMA; QR yazdırma SVG'si siyah kalır. Web.Tests: (1) guard — kapsamdaki 11 dosyada eski ana renk ailesi hex'leri 0; (2) davranış — RunNode / sahte DOM'da :root --bs-primary değişince birincil öğenin rengi değişir (en az bir CSS sayfası + sunum canvas). Görsel kanıt: her sayfa ailesinden bir ekran, varsayılan renk + customizer'da başka renk, açık + koyu (AYRI sekme / kendi düzeneğin; kullanıcının oturum açık sekmesine harness enjekte etme).
KORU/YAPMA: kapsam dışı backbone-custom.css ve pages/demand-idea-capture.css'e DOKUNMA (XMC-001; raporda listele); vendor/core.css ve template-customizer'a DOKUNMA; mevcut W2–W4 / W3E Web testleri yeşil kalır; CRM koduna dokunma; kalıcı veri yazma YOK.
DOĞRULA: acceptance + 2 sabotaj (.bak + touch ile geri al, git checkout YOK): (1) bir dosyaya #0b1a8c geri yaz → guard kırmızı; (2) sunum canvas'ı sabit renge döndür → davranış testi kırmızı. Web tam takım (-o repo içi), taban karşılaştırması.
Commit: "fix(web): WP-VW-W3E-THEME — CRM visit pages take the primary colour from the layout (--bs-primary), no hard-coded navy / purple" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor TÜRKÇE (§22). K13.
```

## §37 CT kabul — W3E-THEME E2 ACCEPTED (2026-10-15)
Test dalına alındı: `67366dbec` (ajan `fe4f5d107`), çakışmasız. 11 dosya + 3 test dosyası; kapsam dışı iki dosyaya ve vendor'a dokunulmadı.

**CT kontrolü:**
- Token blokları `var(--bs-primary)` / `rgba(var(--bs-primary-rgb), α)`; koyu temada metin tonu `color-mix(…, #fff)`. Anlamsal renkler ve QR baskısı değişmedi.
- **Testler** (worktree, CT `-o`): Web **1744/4**.
  - `WorkCenterMenuWithoutPermissions`: `-o` artefaktı.
  - 2 `ShipmentDetailsDateNode`: tabanda da var.
  - **`VisitEvidenceFlagsWebTests.C5`:** WEB-b'nin testi, **tarihe bağlı** (8–14 Ekim sabit, sayfa gerçek saati okuyor). 15 Ekim'de kırmızıya döndü; THEME ile ilgisiz → **CT-3**.
- **CT sabotajı (ajanın dokunmadığı yol):** `visit-qr.css` açık zemin tonu listede OLMAYAN sabit renge (`#e6e7ff`) çevrildi. Koruma testi listesi bunu görmez, ama davranış testi `T2…(visit-qr.css)` **kırmızı**. `.bak` + touch ile geri alındı.

**Açık:**
- **CT-3:** `visit-evidence-flags-smoke.js` saati sabitlemiyor → C5 her gün kayar. Düzeltme: harness `Date`'i 2026-10-14'e sabitler (yalnız test).
- **CT-4:** `visit-workspace.css` ve `visit-presentation.css`'te koyu tema bloğu yok (THEME öncesinden). Koyu temada başlangıç penceresi başlıkları soluk, fotoğraf alanı beyaz → E4 öncesi küçük Web işi.

## §36.1 Agent Prompt — W3E-DARK (paste-ready · CT-4, E4'ten önce)
```text
@[.antigravity/agents/frontend-ui-ux.md]
WP: WP-VW-W3E · W3E-DARK — ziyaret çalışma alanı ve sunum sayfalarına koyu tema renk bloğu (CT-4): koyu temada başlangıç penceresi başlıkları soluk, fotoğraf alanı beyaz
Repository: C:\tmp\vw-w3e-dark (worktree, test/crm-content-visit-e2e güncel başı — THEME 67366dbec içerir) · Branch: wp/vw-w3e-dark · commit bu dala, push YOK

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-VW-W3E-qr-antifraud.md — önce oku: "K-THEME", §37 W3E-THEME (CT-4).
MEVCUT: wwwroot/assets/js/CRM/VisitWorkspace/visit-workspace.css (#vw-root token bloğu, yalnız açık) · wwwroot/assets/js/CRM/VisitPresentation/visit-presentation.css (yalnız açık) · DESEN: pharmacy-report.css / visit-qr.css / visit-evidence-flags.css'teki `[data-bs-theme="dark"] <kök>` bloğu (zemin, kart, başlık, metin, soluk metin, kenar, yumuşak zemin, anlamsal renklerin koyu karşılıkları; ana renk K-THEME'e göre --bs-primary'den, metin tonu color-mix). Mockup: mockups/visit-workspace/decoded/00-tema.css (koyu değişkenler varsa) + "Ziyaret Calisma Alani (standalone).html" koyu tema düğmesi.
NE: iki dosyaya aynı desende `[data-bs-theme="dark"]` token bloğu; sabit açık renkler (beyaz kart, açık gri zemin, koyu başlık metni) token'a bağlanır; başlangıç penceresi (E3 + W3E QR bölümü), takvim, ayrıntı paneli, sunum ekranı ve imza alanı koyu temada okunur; ana renk yine --bs-primary (K-THEME). Web.Tests: her iki dosyada koyu blok var + başlık / zemin / kart token'ları tanımlı (dosya düzeyi) + VisitThemePrimary testleri yeşil. Görsel kanıt: çalışma alanı (takvim + başlangıç penceresi QR bölümüyle) ve sunum, açık + koyu, varsayılan renk + bir başka customizer rengi (AYRI sekme / kendi düzeneğin; kullanıcının oturum açık sekmesine harness enjekte etme).
KORU/YAPMA: açık tema görünümü DEĞİŞMEZ (mevcut Web testleri yeşil); K-THEME guard'ı (eski ana renk hex'leri 0) bozulmaz; başka sayfaların CSS'ine dokunma; CRM koduna dokunma; kalıcı veri yazma YOK.
DOĞRULA: 1 sabotaj (.bak + touch): visit-workspace.css koyu bloğu silinsin → yeni test kırmızı. Web tam takım (-o repo içi), taban karşılaştırması (C5 tarih kırmızısı tabanda da var olabilir — CT-3, ayrı).
Commit: "fix(web): WP-VW-W3E-DARK — dark theme token blocks for the visit workspace and presentation pages" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor TÜRKÇE (§22). K13.
```


## W3E yerel E4 (CT, 2026-10-15) — yetki betiği uygulandı, fleet yeniden başlatıldı, Admin ile giriş

### Doğrulananlar (gerçek veriyle)
- **M sayfası:** 6 ziyaret, özet, işaret çubukları, süzgeçler, liste; sistem rengi (mor) doğru.
- **Q1 kart:**
  - durum a ("Kod yok") → "QR kodu üret" → durum b (Aktif · Sürüm 1);
  - geçmiş zaman çizelgesi.
- **Q2 yazdırma:**
  - önizleme mockup'la uyumlu;
  - `GET …/print?paper=a4&copies=1` → 200, `no-store`, SVG var, yanıtta `DQR1` metni YOK.
- **E1–E3:**
  - "Ek kanıtlar" bölümü;
  - onay penceresi "Kurum QR kodu: Kapalı → Zorunlu";
  - geçmişte alan / değer kullanıcının dilinde, kişi adıyla.
- **S ekranları** (zorunlu kip, bugünkü ziyaret "SEZEN GENÇ · 75.YIL MECİDİYEKÖY ASM"):
  - kurumda kod yokken cezasız başlama bilgisi (S6, QR "Eksik" sayılmıyor);
  - kod varken S8 ("Kabuldeki QR kodunu okutun", "Eksik: konum, QR kodu", Sunumu başlat kapalı);
  - S5 kamera izni yok (panel kamerayı engelliyor);
  - zorunluda "Kodu bulamıyorum" → istisna yönlendirmesi.
- **Ön kontrol (qr-check) gerçek veriyle**, geliştirme anahtarıyla üretilen yüklerle:

  | yük | sonuç |
  |---|---|
  | doğru kurum | `ok` / `self` / kurum adı |
  | başka kurum | `wrong_account`, "100.YIL ECZANESİ" adıyla |
  | bozuk | `invalid` |
  | yük yok | `missing` → `visit_qr_required` |

  CT'nin hesapladığı vektör BE-a raporundaki örnekle birebir.
- **Telefon (375 px) + Arapça:** M sayfası ve QR kartı sağdan sola, okunur.

### Doğrulanamayanlar
- **S2 / S3 / S4'ün ekrandaki çizimi gerçek okutmayla:** tarayıcı paneli kamerayı engelliyor. Sunucu tarafı yukarıda doğrulandı; ekran çizimi ajan testlerinde.
- Q3 / Q4 (yeniden üret / iptal) gerçek veride denenmedi.
- Q5 toplu üretim; M2 çekmece gerçek satırda.
- **Koyu tema:** W3E-DARK bekliyor.

### Bulgular
| # | ekran | bulgu | sahip |
|---|---|---|---|
| G-1 | M (Kanıt işaretleri) | Kök kutu ve sayfalama düğmeleri aynı `.vf-page` sınıfını kullanıyor → kök 32 px'e sıkışıyor, alt bilgi (footer) tablonun üstüne biniyor (masaüstü + telefon). | Web |
| G-2 | Q5 liste | "Tür" sütunu ham kod (`Clinic`, `Pharmacy`) → BRD `account-type` etiketi kullanıcının dilinde. | Web |
| G-3 | Q5 liste, Q2 adres | Şehir ham kod (`TR-42-KONYA`). Bilinen şehir sözlüğü farkı (veri sahibi maddesi); Web'de etiket çözülemezse koddan okunur ad ("Konya") gösterilir. | Web (+ veri) |
| G-4 | Q1 kart + geçmiş | "Üreten" / olay kişisi kullanıcı kimliği (GUID) gösteriyor. Ayar geçmişi aynı kişiyi "Admin User" diye gösteriyor. → QR okuma ve geçmiş yanıtı ad çözümünü taşısın (ayar DTO'sundaki `DisplayNames` deseni). | CRM + Web |
| G-5 | E (ayar önizlemesi) | "Bu kapsamdaki temsilci şunu görecek" QR kipini yansıtmıyor; QR satırı ve kural cümlesi yok. | Web |
| G-6 | Q1 kart, Arapça | Rakamla başlayan Latin adlar sağdan sola düzende ters dönüyor ("75.YIL" → "YIL.75"). Adlar `<bdi>` / `dir="auto"` ile sarılmalı (QR kartı, liste, M sayfası). | Web |
| G-7 | M süzgeç | Kurum seçeneklerinde aynı adlı kurumlar ayırt edilemiyor (3 × "100.YIL ECZANESİ") → şehir / kod eklenmeli. | Web |
| G-8 | M çip | "Uzak konum · 198.290 m" → 1000 m üstü km ("198,3 km"). | Web |
| CT-3 | Web.Tests | `VisitEvidenceFlagsWebTests.C5` tarihe bağlı (her gün kırmızı) → smoke harness saati sabitler. | Web.Tests |

**Not:** Çalışma alanı takvimi ilk açılışta ~15 sn yüklendi (5 haftalık aralık; W2'den beri). İzlenecek.

### Yerel test verisi (temizlik kararı kullanıcıda)
- **QR kodları:**
  - `daafb809…` 100.YIL ECZANESİ v1 (+ "Yazdırıldı · A4, 1 adet" olayı);
  - `dbcc1d79…` 75.YIL MECİDİYEKÖY ASM v1.
- **Kanıt ayarı:** `qrMode` Kapalı → Zorunlu → Kapalı (v3; ayar eski haline döndü, geçmişte iki satır).

## §36.1 Agent Prompt — W3E-FIX (paste-ready · E4 bulguları G-1…G-8 + CT-3; W3E-DARK ile paralel)
```text
@[.antigravity/agents/frontend-ui-ux.md]
WP: WP-VW-W3E · W3E-FIX — yerel E4 bulguları: G-1 kanıt işaretleri sayfasında sınıf çakışması (footer tablonun üstünde), G-2 QR listesinde tür etiketi, G-3 şehir adı, G-4 QR kartı / geçmişinde kişi adı (GUID değil), G-5 ayar önizlemesinde QR satırı, G-6 RTL'de ad yönü, G-7 aynı adlı kurumları ayırt etme, G-8 uzak mesafe km biçimi, CT-3 C5 testinin saati
Repository: C:\tmp\vw-w3e-fix (worktree, test/crm-content-visit-e2e güncel başı) · Branch: wp/vw-w3e-fix · commit bu dala, push YOK

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-VW-W3E-qr-antifraud.md — önce oku: "W3E yerel E4 (CT, 2026-10-15)" bulgu tablosu + K-THEME + DESIGN-GAP-W3E (mockup metinleri).
MEVCUT: wwwroot/assets/js/CRM/VisitEvidenceFlags/visit-evidence-flags.css (.vf-page iki kez: kök 19 + sayfalama düğmesi 104) + visit-evidence-flags.js + flags-core.js · VisitQrCodes (qr-card.js, qr-codes.js, qr-print.js, VisitQrCodesController) · VisitEvidenceSettings (visit-evidence-settings.js önizleme kartı) · CRM Features/VisitQr (VisitQrService.Actor = ActorName — kimlik dönüyor; VisitQrAccountDto.IssuedBy / RevokedBy, VisitQrEventDto.By) · ad çözümü deseni: VisitEvidencePolicyDto.DisplayNames (ayar geçmişi "Admin User" gösteriyor) · BRD account-type etiketi deseni: mevcut Accounts / PharmacyReport Web okuması · Web.Tests/Js/visit-evidence-flags-smoke.js (saat sabit değil).
NE: (G-1) sayfalama düğmesi sınıfını ayır (ör. .vf-pg), kök yüksekliği içeriği sarar, footer altta · (G-2) Q5 "Tür" BRD account-type etiketi kullanıcının dilinde (yoksa kod) · (G-3) şehir: city BRD etiketi; çözülemezse `TR-NN-ADI` biçiminden okunur ad (ilk harf büyük, Türkçe İ/ı doğru) — liste + yazdırma adresi · (G-4) CRM: QR hesap okuması ve geçmiş yanıtı ad çözümünü taşır (DisplayNames ya da IssuedByName / RevokedByName / By adı, ayar deseniyle; kiracı sınırlı); Web kartta ve geçmişte adı gösterir, ad yoksa "—" (GUID asla) · (G-5) ayar önizlemesine QR satırı (kipe göre "zorunlu / isteğe bağlı", kapalıyken yok) + kural cümlesi; cihaz doğrulama "İşaretle" iken web bilgi satırı (S7 metni) · (G-6) kurum / kişi adları <bdi> ya da dir="auto" (QR kartı, Q5 liste, yazdırma, M liste + çekmece + süzgeç) · (G-7) M kurum süzgecinde aynı adlı seçeneklere şehir (ya da kurum kodu) eki · (G-8) mesafe ≥ 1000 m → "198,3 km" (kullanıcının dili ve ondalık ayırıcısı) · (CT-3) visit-evidence-flags-smoke.js Date'i 2026-10-14'e sabitler; C5 her gün yeşil.
KORU/YAPMA: K-THEME (ana renk yalnız --bs-primary; guard yeşil); W3E-DARK'ın dosyalarına (visit-workspace.css, visit-presentation.css) DOKUNMA; mevcut W3E / W4 Web testleri yeşil; QR yükü hiçbir yanıtta / günlükte yok; CRM'de yalnız G-4 (okuma DTO'su + ad çözümü), başka davranış değişmez; kalıcı veri yazma YOK; kullanıcının oturum açık sekmesine harness enjekte etme.
DOĞRULA: her G için test (Web.Tests xUnit + RunNode; G-4 için CRM testi: ad çözülüyor, başka kiracının adı asla) + 3 sabotaj (.bak + touch): (1) .vf-page çakışması geri gelsin → kırmızı; (2) kartta GUID gösterilsin → kırmızı; (3) smoke harness saati sabitlemesin → C5 kırmızı (testi bugünün tarihine göre çalıştırarak göster). CRM + Web tam takım (-o repo içi), taban karşılaştırması. Görsel kanıt: M sayfası (footer altta), Q5 tür / şehir, QR kartı adla, ayar önizlemesi QR satırıyla, Arapça QR kartı ad yönü.
Commit: "fix(web,crm): WP-VW-W3E-FIX — evidence flags page layout, QR list type/city labels, issuer names, settings preview QR row, RTL name direction, same-name institutions, km distances, clock-pinned smoke test" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor TÜRKÇE (§22). K13.
```
