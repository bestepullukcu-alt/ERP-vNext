# Canlıya Çıkış Sonrası Operatör Adımları

> Bu belge **geliştirme kuyruğu değildir.** Buradaki maddeler kod değil, canlı ortamda
> **elle yapılacak işlerdir**: bir yetki satırının açılması, bir tanımın girilmesi, bir
> ayarın çevrilmesi. Kod deploy edildikten sonra biri bunları yapmazsa özellik oradadır
> ama çalışmaz.
>
> `docs/roadmap/backlog/product-backlog.md` yerine burada durmalarının sebebi: backlog'a
> yazılan madde "yapılacak geliştirme" diye okunur ve deploy günü kimse ona bakmaz.

## Nasıl kullanılır

1. **Deploy öncesi** bu dosyayı aç, ilgili modülün satırını oku.
2. Deploy sonrası adımı uygula.
3. Satırı "Tamamlananlar" bölümüne taşı — tarih ve kim yaptığıyla.
4. Yeni bir modül elle bir adım gerektiriyorsa, **o modülün pack'i kapanmadan önce**
   satırı buraya ekle. Pack kapanışında bu kontrol edilir.

⚠ Bir maddeyi silme. Tamamlanan madde aşağı taşınır; çünkü "bu neden böyle ayarlanmış"
sorusunun cevabı altı ay sonra yalnız burada kalır.

---

## Bekleyen adımlar

### 1 · WORK-REPORT yetki (entitlement) satırı

| | |
|---|---|
| **Modül** | MOD-0024 — İş Raporu ekranı |
| **Ne zaman** | İş Raporu canlıya çıktığı deploy'da |
| **Yapılmazsa** | Ekran vardır, **hiçbir kiracı göremez**. Menüde çıkmaz, doğrudan adres yazılsa da yetki reddi alınır. |
| **Kim** | Platform operatörü |

İş Raporu kendi modülü olarak kayıtlıdır (`work-report`) ve kiracıların bu modüle
erişimi entitlement satırıyla açılır. Kod PR #91 ile main'e girdi; satır açılmadı.

**Belirti:** kullanıcı "İş Raporu menüde yok" der, geliştirici koda bakar, kod yerindedir.
Sebep koddaki bir hata değil, açılmamış bir kapıdır.

### 2 · Organizasyon özel alan tanımları

| | |
|---|---|
| **Modül** | MOD-0288-FU02 / FU04 — organizasyon özel alanları |
| **Ne zaman** | FU04 canlıya çıktıktan **ve yönetişim onayı geldikten** sonra |
| **Yapılmazsa** | Ekran boş bir alan listesiyle açılır; birim formunda özel alan bölümü hiç görünmez. |
| **Kim** | Kiracı organizasyon yöneticisi (geliştirici değil) |

Alan tanımları **kod değil veridir** ve deploy ile taşınmaz. Test ortamında tanımlananlar
canlıya gelmez; canlıda **yeniden** tanımlanır. FU04'te içe/dışa aktarma yoktur.

Yöneticinin defterindeki (`GMG-CGV-LOG-0005`) yedi yönetişim alanı ve önerilen tipleri:

| Alan | Tip |
|---|---|
| Permanent OU ID | Metin |
| Regulatory role / independence requirement | Tek seçim |
| Accountable executive | Referans (pozisyon) |
| Approval reference | Metin |
| Charter reference | Metin |
| IT-directory mapping status | Evet / Hayır |
| Evidence ref | Metin |

⚠ **Tanım kodu sonradan değiştirilemez.** `regulatory.role` yazılıp kaydedildiyse
`regulatory-role` yapılamaz — girilmiş değerler o koda bağlıdır. Düzeltme yolu yeni alan
açıp eskisini pasife almaktır, yani canlıda ilk yazımda dikkat gerekir.

⚠ **Yönetişim kapısı açık.** Yöneticinin paketi kendi kapağında *"ERP kataloğu
hazırlanabilir, yüklenemez"* diyor: 38 doküman DRAFT, 50 birim *Proposed*, beş kapının
beşi de karşılanmamış. Kod canlıya çıkabilir; bu veri girişi o onayı bekler.

### 3 · ABB (ürün kısaltması) kararları Görev Merkezi'ne bağlanır

| | |
|---|---|
| **Modül** | MOD-0290-FU01 — ürün kısaltma kaydı (ABB) |
| **Ne zaman** | ABB'nin canlıya çıktığı deploy'da |
| **Yapılmazsa** | ABB istekleri kayıt sayfasında "onay bekliyor" görünür ama **hiçbir onaycının Görev Merkezi'ne düşmez**; kayıt sayfası kararı oraya yönlendirdiği için hiçbir istek karara bağlanamaz. |
| **Kim** | Platform operatörü |

Platform'un `WorkAggregation:RemoteProviders` listesine bir satır eklenir (geliştirmede
`services/Diten.Platform/src/Diten.Platform.API/appsettings.Development.json`'da duruyor):

| Alan | Değer |
|---|---|
| `ProviderCode` | `mdm-product-abbreviations` |
| `ContractVersion` | `1.0` |
| `BaseUrl` | canlı MDM servisinin adresi (şema + host + port, yol yok) |
| `ProjectionPath` | `api/v1/work-items/product-abbreviations/projection` |
| `ActionPathTemplate` | `api/v1/work-items/product-abbreviations/{itemId}/actions/{actionCode}` |
| `Actions` | `approve` → `mdm.product-abbreviations.approve`, `reject` → `mdm.product-abbreviations.reject`, `cancel` → `mdm.product-abbreviations.cancel` |

İki yol **açıkça yazılır**: ABB'nin rotaları sözleşmenin varsayılan yollarına uymaz, yazılmazsa
Platform yanlış adrese gider ve kaynak "kullanılamıyor" görünür. Kimlik bilgisi gerekmez —
Platform çağıranın taşıyıcı jetonunu ve kiracı başlığını iletir. Hatalı bir satır Platform'u
açılışta durdurur (`RemoteWorkItemProviderOptions.Validate`).

**Belirti:** onaycı "ABB isteği Görev Merkezi'nde yok" der; MDM'de istek `REQUESTED`'dır.

---

## Tamamlananlar

*(henüz yok)*
