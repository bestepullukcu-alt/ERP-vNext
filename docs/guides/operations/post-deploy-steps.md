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

---

## Tamamlananlar

*(henüz yok)*
