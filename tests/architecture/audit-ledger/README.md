# Denetim defteri

Kural: [`.antigravity/rules/audit-trail-standard.md`](../../.antigravity/rules/audit-trail-standard.md) (AUD-001)
Ölçen test: `tests/architecture/TenantArchitecture.ArchitectureTests/AuditTrailStandardTests.cs`

Servis başına bir dosya: `Diten.<Servis>.md`. Yazma komutu ölçülen her servisin defteri vardır (test, defteri olup
komutu bulunamayan ve komutu olup defteri olmayan servisi ayrı ayrı kırmızı yapar). `CT-DECISIONS.md` defter değildir:
deftere satır EKLEYEN Control Tower kararlarının kaydıdır.

## Bu dosyalar ne DEĞİLDİR

Bir komutun denetlenip denetlenmediği **buradan okunmaz**. O, `services/<servis>/src` altındaki üretim kodundan
okunur: komut türü işareti taşıyor mu, handler'ı yazma üyesini çağırıyor mu. Defter yalnız bir insanın söylemesi
gereken şeyleri tutar — ve söylenen her şeyin **sayısı test dosyasında sabittir** (borç, istisna, K2 borcu, yazan
sorgu, dolaylı bildirim; kabul edilmiş izler tek tek): defterde satır eklemek ya da bir izin yolunu değiştirmek testi
kırar. Testin göremediği tek şey sayıyı koruyan bir değiş tokuştur (bir adı silip yerine başkasını yazmak); onu PR'da
CI adımı ve inceleme yakalar.

## Biçim — ayrıştırıcı katıdır

Okunamayan her satır `okunamayan satır: dosya:satır` diye kırmızıdır. Bilinen üç istisna, sessizce atlanır:
bir tablonun ilk satırı (her zaman başlık sayılır), `|` ile başlamayan tablo satırı ve liste bölümünde `1. X` / `– X`
biçimli madde. Bu biçimleri kullanma.

- Yalnız altı `## ` bölüm başlığı tanınır (aşağıda). `###` ve bilinmeyen `##` kırmızıdır.
- Liste bölümlerinde satır tam olarak `- TürAdı` biçimindedir: girintisiz, `- ` ile, tek ad. `* X`, `-X`, sekmeyle
  girintili madde kırmızıdır.
- Tablo bölümlerinde not yazacaksan satırı `> ` ile başlat. Madde işaretiyle başlayan satır kırmızıdır.
- Düz cümleler (madde işaretiyle başlamayan) serbesttir.

## Altı bölüm

### `## İzler` — servisin denetim mekanizmaları

| sütun | anlamı |
|---|---|
| `iz` | Kısa ad. `## Dolaylı` satırları ve test bu adı kullanır. |
| `yol` | `a` Platform içi · `b` merkezi günlüğe iletim · `c` eşdeğer iz · `aday` mekanizma var ama **kabul edilmedi** |
| `tür` | `işaret`: belirteç **komut türünün** arayüzlerinde aranır (ara arayüz üzerinden de) · `yazıcı`: belirteç komutun **handler'ında bir çağrı** olarak aranır |
| `belirteç` | `işaret` için arayüz adı. `yazıcı` için **`Tip.Metot`** — bir yazma üyesi. `A+B` = ikisi de · `A/B` = biri · `!C` = olmamalı |

Yazıcı izinde tipi anmak kanıt değildir: handler `Tip.Metot` çağrısını yapıyor olmalı (`Tip` türünde bir alan /
parametre üzerinden, ya da statik olarak). Arayüz olmayan bir sınıfın üyesi `var` üzerinden çağrılıyorsa yalnız metot
adına bakılır.

`aday` yolundaki bir ize giden komut **denetleniyor sayılmaz** ve borçta kalır. Bir izi kabul etmek (`aday` → `a`/`b`/`c`)
ya da kabul edilmiş bir izin belirtecini değiştirmek Control Tower kararıdır ve **yalnız defterde yapılamaz**: kabul
edilmiş izler `AuditTrailStandardTests.AcceptedTrails` içinde sabittir.

İşaret ve pipeline davranışı `Diten.Building.Blocks` içinde tanımlıysa da tanınır (ortak iletici, kural §5 K4);
davranışın **kaydı** servisin kendisinde aranır.

### `## İstisnalar` — bilerek denetlenmeyen komutlar

`komut | sınıf | gerekçe`. Sınıf kural dosyasının §6 tablosundan okunur (`İ1`…`İ5`). Gerekçe o komuta özgü bir cümledir
(en az beş farklı kelime). Denetlenen bir komut için istisna yazılamaz; adında `User` / `Role` / `Permission` geçen komut
istisna olamaz. İstisna sayısı testte sabittir: yeni istisna = sayıyı yükseltmek = Control Tower kararı.

### `## Dolaylı` — handler yazıcıya başka bir tür üzerinden gidiyor

`komut | iz | üzerinden`. Test iki ucu kanıtlar: handler `üzerinden` türünü anıyor **ve** o tür (ya da onu uygulayanlar)
izin yazma üyesini çağırıyor.

⚠ **Dar tür yaz.** Test, aradaki türün *o komut için* yazıcıyı çağırdığını göremez. `üzerinden` sütununa bir depo ya da
her şeyi yapan genel bir servis yazılırsa, o türe dokunan her komut sahte kredi alır.

### `## Bilinen borç` — denetlenmeyen komutlar

**Yalnız küçülür.** Bir komutu denetlediğinde ya da sildiğinde satırını çıkar **ve** testteki sabit sayıyı düşür.
Yeni yazdığın bir komut denetlenmiyorsa buraya yazamazsın: ya denetle, ya gerekçeli istisna bildir.

### `## K2 borcu` — denetlenen ama kayıt yazılamayınca durmayan komutlar

Sahip kararı (kural §4.3–§4.4): kimlik · yetki · kiracı durumu · GxP · KVKK-özel sınıfında kayıt yazılamazsa işlem
durur. Bugün denetlenen ama en iyi çabayla yazan o sınıftaki komutlar burada durur. **Yalnız küçülür.** Buradaki bir
komut denetlenmiyorsa yeri `## Bilinen borç`'tur (test söyler).

### `## Yazan sorgular` — işleyicisi depoya yazan sorgular

Sorgu kuralı bunları her komut listesinden gizler ve hiçbir kayıtları olmaz. **Yalnız küçülür**: yazma bir komuta
taşınınca satır çıkar. Yeni bir yazan sorgu kabul edilmez.

## Deftere satır eklemek

Üç şey birlikte gerekir; biri eksikse CI ya da test kırmızıdır:

1. Control Tower kararı — [`CT-DECISIONS.md`](CT-DECISIONS.md) dosyasına ad + gerekçe.
2. `AuditTrailStandardTests.PinnedCounts` içindeki sayının yükseltilmesi.
3. Satırın kendisi.

## Test kırmızıysa

| mesajda geçen | ne yapılır |
|---|---|
| `YENİ KOMUT DENETİMSİZ GELDİ` | Komutu denetle (yol a/b/c) ya da `## İstisnalar`'a sınıf + gerekçeyle yaz. Borç listesine ekleme. |
| `ARTIK DENETLENİYOR … listeden çıkar` | Satırı `## Bilinen borç`'tan sil ve sabit sayıyı düşür. İyi haber. |
| `BÖYLE BİR YAZMA KOMUTU YOK` | Komut silinmiş ya da adı değişmiş: satırı sil (adı değiştiyse yeni ad borca **yazılmaz**). |
| `… ARTTI. Deftere satır EKLENMİŞ` | Eklediğin satırı geri al. Karar varsa: yukarıdaki üç adım. |
| `… azaldı (iyi haber)` | Testteki sabit sayıyı da düşür. |
| `kabul edilmiş ama bu testte sabitlenmemiş iz` | Bir izi defterde kabul ettin ya da belirtecini değiştirdin. Control Tower kararı ve `AcceptedTrails` satırı gerekir. |
| `GEREKÇE BOŞ` / `KİMLİK / ROL / İZİN komutu istisna olamaz` | Gerekçeyi yaz ya da istisnayı kaldır; kimlik komutunu denetle. |
| `yazıcı belirteci … bir YAZMA ÜYESİ olmalı` / `iz hayalet` / `IPipelineBehavior yok` | `## İzler` satırı üretim kodunda karşılık bulmuyor: `Tip.Metot` yaz; tip / metot silinmiş ya da davranış kaydı kaldırılmış olabilir. |
| `'.Queries' ad alanında duran istek` | Adı `Command` ile biten türü `.Queries` ad alanından çıkar — üretim onu sorgu sayıp kaydını yazmıyor. |
| `SORGU İŞLEYİCİSİ DEPOYA YAZIYOR` | Yazmayı bir komuta taşı. |
| `bu isteği ölçüm GÖRMÜYOR` | İsteği doğrudan `IRequest` uygulayan bir tür yap (taban sınıftan miras, takma ad, küçük harfle başlayan ad ölçülemez). |
| `AuditBehavior bu adı DIŞLAR` | Komutun adını değiştir; işaret taşısa da kaydı yazılmıyor. |
| `okunamayan satır` | Yukarıdaki "Biçim" bölümü. |
| `MediatR isteği olmayan '*Command' türü, adla ölçülmeyen bir serviste` | Komutu MediatR isteği yap; değilse kural §2 ad kuralına Control Tower kararıyla eklenir. |

## Sayım

Envanter tablosunu test üretir — ikinci bir sayım yoktur:

    dotnet test tests/architecture/TenantArchitecture.ArchitectureTests \
      --filter "FullyQualifiedName~Inventory_PrintsTheTable" --logger "console;verbosity=detailed"
