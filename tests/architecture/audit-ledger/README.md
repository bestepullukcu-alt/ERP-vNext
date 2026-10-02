# Denetim defteri

Kural: [`.antigravity/rules/audit-trail-standard.md`](../../.antigravity/rules/audit-trail-standard.md) (AUD-001)
Ölçen test: `tests/architecture/TenantArchitecture.ArchitectureTests/AuditTrailStandardTests.cs`

Servis başına bir dosya: `Diten.<Servis>.md`. Yazma komutu olan her servisin defteri vardır; olmayanın yoktur
(test ikisini de ölçer).

## Bu dosyalar ne DEĞİLDİR

Bir komutun denetlenip denetlenmediği **buradan okunmaz**. O, `services/<servis>/src` altındaki üretim kodundan
okunur: komut türü işareti taşıyor mu, handler'ı yazıcıyı anıyor mu. Defter yalnız bir insanın söylemesi gereken
dört şeyi tutar.

## Dört bölüm

### `## İzler` — servisin denetim mekanizmaları

| sütun | anlamı |
|---|---|
| `iz` | Kısa ad. İnsanlar ve `## Dolaylı` satırları bu adı kullanır. |
| `yol` | `a` Platform içi · `b` merkezi günlüğe iletim · `c` eşdeğer iz · `aday` mekanizma var ama **kabul edilmedi** |
| `tür` | `işaret`: belirteç **komut türünün** taban listesinde aranır · `yazıcı`: belirteç komutun **handler'ında** aranır |
| `belirteç` | Üretim kodundaki tür adı. `A+B` = ikisi de olmalı · `!C` = olmamalı |

`aday` yolundaki bir ize giden komut **denetleniyor sayılmaz** ve borçta kalır. Yolu `aday`'dan `a`/`b`/`c`'ye
çevirmek bir karardır (kural §5 ve §10); çevrildiği anda test o ize giden her komutun borç listesinden
çıkarılmasını ister.

Test her iz için şunu doğrular: belirteç serviste gerçekten tanımlı; `işaret` ise onu okuyan bir
`IPipelineBehavior` var ve o davranış bir yerde kaydediliyor.

### `## İstisnalar` — bilerek denetlenmeyen komutlar

`komut | sınıf | gerekçe`. Sınıf kural dosyasının §6 tablosundan okunur (`İ1`…`İ5`); gerekçe o komuta özgü bir
cümledir. Denetlenen bir komut için istisna satırı yazılamaz.

### `## Dolaylı` — handler yazıcıya başka bir tür üzerinden gidiyor

`komut | iz | üzerinden`. Handler yazıcıyı kendisi anmıyor, bir uygulama servisine devrediyorsa buraya yazılır.
Test iki ucu kanıtlar: handler `üzerinden` türünü anıyor **ve** o tür (ya da onu uygulayanlar) izin belirtecini
anıyor.

⚠ **Dar tür yaz.** Test, aradaki türün *o komut için* yazıcıyı çağırdığını göremez. `üzerinden` sütununa bir depo
ya da her şeyi yapan genel bir servis yazılırsa, o türe dokunan her komut sahte kredi alır. Yalnız işi o olan
tür yazılır (ör. `ProductAbbreviationWorkflow`), `ITaskItemRepository` yazılmaz.

### `## Bilinen borç` — 2026-10-02'de denetimsiz bulunan komutlar

`- KomutAdı`, satır başına bir tane.

**Bu liste yalnız küçülür.** Satır eklemek yasaktır. Bir komutu denetlediğinde ya da sildiğinde satırını çıkar —
çıkarmazsan test kırmızıdır. Yeni yazdığın bir komut denetlenmiyorsa buraya yazamazsın: ya denetle, ya gerekçeli
istisna bildir.

## Test kırmızıysa

| mesaj | ne yapılır |
|---|---|
| `YENİ KOMUT DENETİMSİZ GELDİ` | Komutu denetle (yol a/b/c) ya da `## İstisnalar`'a sınıf + gerekçeyle yaz. Borç listesine ekleme. |
| `ARTIK DENETLENİYOR … listeden çıkar` | Satırı `## Bilinen borç`'tan sil. İyi haber. |
| `BÖYLE BİR YAZMA KOMUTU YOK` | Komut silinmiş ya da adı değişmiş: satırı sil (adı değiştiyse yeni ad borca **yazılmaz** — yeni komut gibi ele alınır). |
| `GEREKÇE BOŞ` | İstisnanın gerekçesini yaz ya da istisnayı kaldır. |
| `iz hayalet` / `IPipelineBehavior yok` | `## İzler` satırı üretim kodunda karşılık bulmuyor: belirteç silinmiş ya da davranış kaydı kaldırılmış. |
| `MediatR'a uğramayan '*Command' türlerinin sayısı değişti` | MediatR dışı bir yazma yolu eklendi ya da kaldırıldı. Kural §10 K5'i oku; sayıyı sessizce artırma. |

## Sayım

Envanter tablosunu test üretir — ikinci bir sayım yoktur:

    dotnet test tests/architecture/TenantArchitecture.ArchitectureTests \
      --filter "FullyQualifiedName~Inventory_PrintsTheTable" --logger "console;verbosity=detailed"
