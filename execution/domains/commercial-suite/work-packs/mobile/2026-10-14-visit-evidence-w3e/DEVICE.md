# Mobil notu — ziyaret başlatmada cihaz doğrulama alanları (2026-10-14)

> **Kimden:** CRM (CT). **Kime:** Android / iOS ekibi.
>
> **Kapsam:** WP-VW-W3E · W3E-BE-b. Ayrıntılı sözleşme: [WP-VW-W3E](../../WP-VW-W3E-qr-antifraud.md) §2.1, §2.5. QR alanları ayrı notta (`QR.md`, BE-a).
>
> **Kural:** mevcut başlatma alanları ve davranışı değişmedi. Aşağıdaki alanların **hepsi isteğe bağlı**; hiçbiri gönderilmezse başlatma W3'teki gibi çalışır. Cihaz alanları **hiçbir kipte başlatmayı reddettirmez** — yalnız kaydedilir ve (ayara göre) işaret konur.

## 1. Yeni start alanları (`POST api/crm/visit-execution/start`, multipart)
| alan | değerler | not |
|---|---|---|
| `clientPlatform` | `android` \| `ios` \| `web` | uygulama kendi platformunu yazar |
| `appVersion` | metin, ≤ 40 karakter | örn. `3.4.0 (412)` |
| `mockLocationDetected` | `true` \| `false` | Android: `Location.isMock()` (API 31+; öncesi `isFromMockProvider()`). iOS: `CLLocation.sourceInformation?.isSimulatedBySoftware` (iOS 15+). Bilinmiyorsa **göndermeyin** (yanlış `false` göndermeyin) |
| `integrityProvider` | `play_integrity` \| `app_attest` | jetonu üreten servis |
| `integrityVerdict` | `passed` \| `failed` \| `unavailable` | cihazın kendi yorumladığı sonuç; servis yanıt vermediyse `unavailable` |
| `integrityToken` | metin, **≤ 8 KB** (`contract.maxIntegrityTokenBytes` = 8192, UTF-8 bayt) | Play Integrity / App Attest jetonu **şimdiden gönderilebilir** |

- Bilinmeyen değer, 40 karakteri aşan sürüm, 8 KB'ı aşan jeton, `true/false` dışında bir `mockLocationDetected` → **400 `visit_capture_invalid`** (başlatma hiç yazılmaz; düzeltip tekrar gönderin).
- **Jeton saklanmaz.** Sunucu yalnız "jeton geldi" bilgisini tutar (`device.tokenProvided=true`); jeton ne veritabanına ne günlüğe yazılır. Sunucu tarafı doğrulama (Google / Apple'a sorma) sonraki pakette (W3E-ATTEST) gelecek; o zamana kadar `device.verified` her zaman `false`.
- Değerler `contract`'tan okunabilir: `clientPlatforms`, `integrityProviders`, `integrityVerdicts`, `antiFraudModes`, `maxIntegrityTokenBytes`.

## 2. Kiracı ayarı `contract.policy.antiFraudMode`
| değer | davranış |
|---|---|
| `off` (varsayılan) | cihaz beyanı **kaydedilir**, işaret **konmaz** |
| `flag` | kaydedilir + aşağıdaki işaretler konur; başlatma yine reddedilmez |

`block` bugün geçersiz bir değerdir (W3E-ATTEST ile gelecek). Uygulama bu ayara göre davranış değiştirmek zorunda değil — her zaman aynı alanları göndermesi yeterli.

## 3. İşaretler (`VisitExecutionDto.flags`, yalnız `flag` kipinde)
| işaret | ne zaman |
|---|---|
| `mock_location` | `mockLocationDetected=true` |
| `device_integrity_failed` | `integrityVerdict=failed` |
| `device_unverified` | `clientPlatform=web`, **ya da** `integrityVerdict` gönderilmedi, **ya da** `integrityVerdict=unavailable` |

Birden fazlası aynı anda konabilir (örn. sahte konum + bütünlük başarısız → iki işaret). İşaretler temsilciye ret / uyarı olarak gösterilmek zorunda değil; yöneticinin "Ziyaret Kanıt İşaretleri" ekranında görünür (`api/crm/visit-evidence-flags`, yalnız yönetici izni — mobil uygulama bu ucu çağırmaz).

## 4. Yanıtta cihaz özeti (`VisitExecutionDto.device`)
```json
"device": {
  "platform": "android",
  "appVersion": "3.4.0 (412)",
  "mockLocation": false,
  "integrityProvider": "play_integrity",
  "integrityVerdict": "passed",
  "tokenProvided": true,
  "verified": false
}
```
Hiç beyan gönderilmezse `device` alanları `null` gelir (`tokenProvided=false`, `verified=false`). W3E öncesi başlatılmış ziyaretlerde `device` `null`'dur — uygulama çökmemeli.

## 5. Web neden hep `device_unverified`?
Tarayıcı kendi cihazına kefil olamaz: sahte konum ya da cihaz bütünlüğü bilgisini tarayıcıdan güvenilir biçimde almanın yolu yok, gelen beyan da kolayca uydurulabilir. Bu yüzden Web vekili (`frontend/Diten.Web` `api/execution/start`):
- `clientPlatform`'u her zaman `web` yazar (sayfa ne gönderirse göndersin);
- `mockLocationDetected`, `integrityProvider`, `integrityVerdict`, `integrityToken` alanlarını CRM'e **iletmez**.

Sonuç: `flag` kipinde Web'den başlatılan her ziyaret `device_unverified` işaretini taşır. Güvenilir cihaz kanıtı yalnız mobil uygulamadan gelir.

## 6. Eski not için öneri (eski dosyaya dokunulmadı; CT birleştirir)
[`2026-10-12-visit-workspace-w3/MOBILE-NOTE-2026-10-12-visit-execution.md`](../2026-10-12-visit-workspace-w3/MOBILE-NOTE-2026-10-12-visit-execution.md) §10 "Henüz yok":
- "QR / anti-fraud (W3E; … ayrı not gelir)" satırı → "W3E: QR ([QR.md](../2026-10-14-visit-evidence-w3e/QR.md)) + cihaz doğrulama alanları ([DEVICE.md](../2026-10-14-visit-evidence-w3e/DEVICE.md)) hazır; sunucu tarafı jeton doğrulaması W3E-ATTEST'te."
- "Katmanlı kanıt ayarı … + temsilci süreli istisnası (W3-BE-c)" satırı listeden çıkmalı: istisna W3-BE-c ile **artık var**.
