# Mobil notu — kurum QR kodu: başlatmada okutma (2026-10-14)

> **Kimden:** CRM (W3E-BE-a). **Kime:** Android ekibi.
>
> **Kapsam:** WP-VW-W3E W3E-BE-a. Ayrıntılı sözleşme: [WP-VW-W3E](../../WP-VW-W3E-qr-antifraud.md) §2.1–§2.4.
>
> **Kural:** mevcut uçlar ve alanlar değişmedi; başlatma ucuna iki isteğe bağlı alan eklendi.
> - Kiracı ayarı `qrMode=off` iken (varsayılan) davranış W3 ile birebir aynıdır: alanları göndermeseniz de olur, gönderseniz de yok sayılır.
> - Cihaz doğrulama alanları (`clientPlatform`, `mockLocationDetected`, …) ayrı notta: `DEVICE.md` (W3E-BE-b).

## 1. Ayarı okumak
`GET api/crm/visit-execution/contract` →
- `policy.qrMode`: `off` | `optional` | `required` — **temsilcinin etkin ayarı** (onaylı süreli istisna `qr_optional` uygulanmış hali; zorunlu → isteğe bağlı).
- `qrModes`, `qrStatuses` (aşağıdaki tablo), `maxQrPayloadLength` (200), `qrPermissions`.
- Yeni işaret kodları `flagCodes` içinde, yeni hata kodları `errorCodes` içinde.

Uygulama başlatma ekranını buna göre çizer:
- `off`: QR adımı yok.
- `optional`: "QR okut" düğmesi var, atlanabilir.
- `required`: okutmadan başlat düğmesi pasif; ama sunucu kararı esastır (aşağıdaki `not_issued` satırına bakın).
- **Uzaktan ziyaret** (`kind=remote`) hiçbir kipte QR sormaz.

## 2. Başlatma ucu: yeni alanlar
`POST api/crm/visit-execution/start` (multipart, mevcut alanlar aynen):

| alan | tür | not |
|---|---|---|
| `qrPayload` | metin, ≤ 200 | Kameranın okuduğu metin **olduğu gibi**. İstemci ayrıştırmaz, doğrulamaz, kırpmaz. |
| `qrScannedAt` | ISO-8601 anı | Okutma anı; yalnız bilgi. Sunucu saati esastır. |

**Yük biçimi** (bilgi için; istemci yorumlamaz): `DQR1.<kurum 22>.<sürüm>.<anahtar>.<imza 43>`.
- Kiracı başına türetilmiş anahtarla HMAC-SHA256 imzalı.
- Kiracı bilgisi içermez; başka kiracının kodu `invalid` sayılır.
- QR'da URL yoktur; bir tarayıcı açmaya çalışmayın.

## 3. Sonuç tablosu (sunucu karar verir)
Sıra: konum kuralından **sonra**, fotoğraftan **önce**. Kabul edilen kurum: ziyaretin konum kuralında kullandığı kurum **ya da üst kurumlarından biri** (en çok 5 düzey). Hastane kabulündeki tek kod alt birim ziyaretlerinde de geçer.

| kip | sonuç (`qr.status`) | yanıt |
|---|---|---|
| `off` / `kind=remote` | `not_applicable` | 201 |
| `required` | `ok` | 201 |
| `required` | `missing` | 400 `visit_qr_required` |
| `required` | `invalid` | 400 `visit_qr_invalid` |
| `required` | `wrong_account` | 409 `visit_qr_wrong_account` (+ satır `scanned_account_id=<id>`) |
| `required` | `revoked` | 409 `visit_qr_revoked` |
| `required` | `not_issued` | **201** + işaret `qr_not_issued` (kuruma kod basılmamış; temsilci engellenmez) |
| `required` | `unverifiable` | 503 `visit_qr_signing_unavailable` (tekrar deneyin) |
| `optional` | `ok` / `missing` / `not_issued` | 201, işaret yok |
| `optional` | `invalid` / `wrong_account` / `revoked` | 201 + işaret `qr_invalid` / `qr_wrong_account` / `qr_revoked` |
| `optional` | `unverifiable` | 201 + işaret `qr_unverifiable` (yalnız yük gönderildiyse) |

**Yanıtta** (`VisitExecutionDto`, eklemeli alan): `qr: { status, accountId, version, scannedAt, matchedVia }`.
- `matchedVia`: `self` (kurumun kendi kodu) | `parent` (üst kurumun kodu).
- Yük yanıtta **hiç** geri dönmez.

## 4. Hata kodları ve kullanıcı metni önerisi
| kod | durum | metin önerisi |
|---|---|---|
| `visit_qr_required` | 400 | "Ziyareti başlatmak için kurumun QR kodunu okutun." |
| `visit_qr_invalid` | 400 | "Bu geçerli bir kurum QR kodu değil." |
| `visit_qr_wrong_account` | 409 | "Bu QR kodu başka bir kuruma ait." |
| `visit_qr_revoked` | 409 | "Bu QR kodu yenilenmiş ya da iptal edilmiş; kurumdaki güncel kodu okutun." |
| `visit_qr_signing_unavailable` | 503 | "QR kodu şu an doğrulanamıyor, biraz sonra tekrar deneyin." |

Ham kodu kullanıcıya göstermeyin; bilinmeyen kodda genel hata metni.

## 5. Kamera ile okutma
- Standart QR okuyucu (ZXing / ML Kit); okunan metni dokunmadan `qrPayload`'a koyun.
- Okutma başarısızsa (izin yok, kamera yok): `optional` kipte başlatabilirsiniz; `required` kipte sunucu 400 döner, kullanıcıya metni gösterin.
- Yük cihazda saklanmaz, günlüğe yazılmaz.

## 6. Kod yönetimi (mobilde ekran yok)
Kurum kodu üretme / yeniden üretme / iptal / yazdırma Web işidir (`api/crm/visit-qr/*`, `crm.visit-qr.read` | `.manage`). Mobil yalnız okutur.

## 7. Eski nota öneri (CT birleştirir; eski dosyaya dokunulmadı)
`MOBILE-NOTE-2026-10-12-visit-execution.md` §10 "Henüz yok":
- "QR / anti-fraud (W3E…)" satırı → "QR: bu not (`2026-10-14-visit-evidence-w3e/QR.md`); cihaz doğrulama: `DEVICE.md`."
- "Katmanlı kanıt ayarı … + temsilci süreli istisnası (W3-BE-c)" satırından istisna kısmı düşmeli: istisna W3-BE-c ile geldi, yeni kural `qr_optional` dahil.

## 8. Ön kontrol: `POST api/crm/visit-execution/qr-check` (W3E-BE-c, 2026-10-14)
Okutmadan hemen sonra, **Başlat'a basmadan önce** temsilciye sonucu göstermek için. Kesin karar yine başlatmada sunucudadır; bu uç hiçbir şey yazmaz (yürütme, okutma sayacı, olay yok), aynı kodu istediğiniz kadar sorabilirsiniz.

- **İzin:** `crm.visit-execution.record` (başlatmayla aynı).
- **Gövde (JSON):** `{ "plannedVisitId": "…", "qrPayload": "DQR1.…" | null, "kind": "in_person" | "remote" }` — `kind` isteğe bağlı (varsayılan `in_person`). Yük, okunduğu gibi gönderilir; istemci ayrıştırmaz.
- **Plan kuralları başlatmayla aynıdır:** kendi planı değilse 404 (ya da 403 `resource_not_caller`), onaysız / bugün değil / başlamış / raporlanmış / 48 saat dolmuş → başlatmanın aynı hata kodu. Konum kuralı bu uçta **sorulmaz**.
- **Yanıt (200):**

| alan | anlam |
|---|---|
| `qrMode` | etkin ayar (`off` / `optional` / `required`; istisna dahil) |
| `status` | §3 tablosundaki sonuç (`ok`, `missing`, `not_issued`, `invalid`, `wrong_account`, `revoked`, `unverifiable`, `not_applicable`) |
| `wouldRefuse` + `errorCode` | başlatma bu kodla **reddedilir mi** ve hangi kodla (§4) |
| `flag` | başlatma kaydedilirse konacak işaret (ör. `qr_wrong_account`) |
| `accountId`, `accountName` | **okunan** kodun kurumu — yalnız imza geçerli ve aynı kiracıysa (`ok`, `wrong_account`, `revoked`); "Bu kod Ankara Şehir Hastanesi'ne ait" metni için |
| `matchedVia` | `self` / `parent` (üst kurum kodu kabul edildi) |
| `expectedHasActiveCode` | ziyaretin kurumunda (veya üst kurumlarında) aktif kod var mı. Yük boşken: `false` → "kurumda kod yok, cezasız başlayabilirsin" (`not_issued`); `true` → "kodu okut" (`missing`) |

- **Örnek:** `{ "qrMode": "required", "status": "wrong_account", "wouldRefuse": true, "errorCode": "visit_qr_wrong_account", "flag": null, "accountId": "…", "accountName": "Ankara Şehir Hastanesi", "matchedVia": null, "expectedHasActiveCode": true }`
- Sunucuda doğrulama anahtarı yoksa: zorunlu kipte `wouldRefuse=true` + `visit_qr_signing_unavailable`; sunucu QR doğrulayıcısız kurulmuşsa ve kip `off` değilse 503 (başlatma da 503 verir).
- Ağ yoksa ön kontrolü atlayın; başlatma yine aynı kararı verir.
