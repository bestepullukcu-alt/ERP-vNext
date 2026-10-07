---
description: "RCS-001 — Kayıt kodu standardı: sistemdeki kayıtların kullanıcıya görünen kodu nasıl üretilir, kim değiştirebilir, neye göre benzersizdir. Yönetim karar notu (2026-09-04) + açıklarına CT önerileri (2026-10-07)."
---

# Kayıt Kodu Standardı — RCS-001

> **Kaynak.** Yönetimin *Kodlama Standardı — Karar Notu v1.0* (2026-09-04, "KESİNLEŞTİRİLMİŞ") kararları
> bu kuralın **bağlayıcı** kısmıdır (§1–§4). Notun bıraktığı açıklar için Control Tower önerileri §5'tedir.
> **Sahip 2026-10-07'de §5 önerilerinin tamamını onayladı** ("önerilerin uygun, Türkçe karaktere gerek yok"):
> artık bağlayıcıdır. "ÖNERİ" etiketleri hangi maddenin nottan, hangisinin CT'den geldiğini göstermek için durur.
> Yönetim bir maddeyi değiştirirse bu kural güncellenir.
>
> **Neden var.** 2026-10-07'de kodda ölçüldü: ortak bir kod üretici yok, hiçbir yerde `MG-` öneki yok;
> Organizasyon Birimi, Pozisyon, Tüzel Kişilik, Abonelik Planı ve PPM kodları elle giriliyor ve gerekçesiz
> değiştirilebiliyor. Notun çözdüğü sorun (kişiye göre değişen, çakışan, izlenemeyen kod) bugün sistemde duruyor.
>
> **Kapsam.** Kullanıcıya görünen *iş kodu* (Code / Business Reference). Veritabanı `Id`'si (Guid) bu kuralın
> konusu değil. Ürün kimliği ailesi (§4) ve doküman kimlikleri kendi düzenlerini korur.

---

## 1. Format (bağlayıcı)

```
{ŞİRKET}-{TÜR}-{SAYAÇ}        örnek:  MG-ORG-000042
```

| Parça | Kural |
|---|---|
| `ŞİRKET` | Kaydı açan tüzel kişiliğin kısa kodu. `MG`, `GMP` yalnız **örnektir** (yönetim notunun örneği `MG-ORG-000042`); gerçek değer her tüzel kişiliğin kaydındaki kısa kod alanından gelir (§5.4). Grup geneli kayıtta grup öneki (§5.3). |
| `TÜR` | Kayıt türünün sabit öneki (§3 tablosu). |
| `SAYAÇ` | En az **6 hane**, sıfırla doldurulmuş (`000042`). **Hiç sıfırlanmaz** (yıl, dönem, şirket değişimiyle). |

## 2. Hangi kayıt nasıl üretilir (bağlayıcı)

| Sınıf | Anlamı | Kullanıcı değiştirebilir mi |
|---|---|---|
| **OTOMATİK** | Sistem üretir. Yüksek hacimli kayıtlar. | **Hayır.** |
| **İKİSİ DE** | Sistem önerir; az sayıda, kalıcı, anlamlı kod gereken kayıtlar. | **Evet, ama gerekçe ZORUNLU** ve gerekçe denetim kaydına yazılır (AUD-001: eski kod, yeni kod, gerekçe, kişi, zaman). |
| **KALSIN** | Bugün zaten otomatik olan sistem; formatı değiştirilmez. | Kendi kuralı. |

**Mevcut kodlar değiştirilmez.** Standart yalnız bundan sonra açılan kayıtlara uygulanır (yazışma ve rapor
referansları kırılmasın). Geçmiş kayıtlar eski kodlarıyla kalır; yeni kayıt yeni formatı alır.

## 3. Kayıt türleri tablosu

Notta adı geçen türler (bağlayıcı) + yeni türler için önerilen önekler (ÖNERİ, §5.5):

| Kayıt türü | Sınıf | TÜR öneki | Bugünkü durum (2026-10-07 ölçümü) |
|---|---|---|---|
| Organizasyon Birimi | OTOMATİK | `ORG` | elle, serbest değiştirilebilir — **uyumsuz** |
| Pozisyon (kadro) | OTOMATİK | `POS` | elle, serbest değiştirilebilir — **uyumsuz** |
| Tüzel Kişilik | İKİSİ DE | (bkz. §5.4) | elle, gerekçesiz değişir — **uyumsuz** |
| Görev Türü | İKİSİ DE | `TTY` | elle, hiç değişmez — öneri yok, **uyumsuz** |
| Portföy / Program / Proje | İKİSİ DE (PPM D-10) | `PFL` / `PRG` / `PRJ` | elle — **uyumsuz** (PPM: değişmez System_ID arka planda, görünen Business_Reference önerilir + düzenlenebilir) |
| Girişim / Yatırım Dosyası | İKİSİ DE (PPM D-10) | `INI` / `INV` | elle — **uyumsuz** |
| Abonelik Planı | İKİSİ DE | (bkz. §5.3) | elle, gerekçesiz değişir — **uyumsuz** |
| Müşteri Hesabı (CRM) | KALSIN | — | `ACC-YYYY-000000`, otomatik ✅ |
| Kampanya (CRM) | KALSIN | — | `CMP-YYYY-000000`, otomatik ✅ |
| Ürün (MDM ürün kimliği ailesi) | KALSIN | — | `GP-` `GS-` `LS-` `FG-` + 12 hane, otomatik ✅ (§4) |
| Doküman (QMS) | KALSIN | — | `UID-0000001` + `GMG-QMS-SOP-0001`, otomatik ✅ (GMG-QMS-LOG-0001) |

## 4. Ürün kimliği ailesi (KALSIN'ın kapsamı)

MDM'nin ürün kimliği kayıtları (Global Ürün, GSKU, LSKU, Bitmiş Ürün) kiracı genelinde **tek sayaçlı, 12 haneli,
hiç sıfırlanmayan** kendi düzenini korur (`{GP|GS|LS|FG}-{000000000000}`, `mdm_canonical_code_counters`).
Şirket öneki yoktur, çünkü bu kayıtlar grup genelidir. **Yeni bir ürün kimliği türü** (ör. Kalem / Malzeme,
MOD-0290-FU04) bu aileye katılır, RCS-001 formatına değil (ÖNERİ §5.6).

---

## 5. Notun açıkları ve CT önerileri (sahip onayı 2026-10-07 — bağlayıcı)

### 5.1 Sayaç neye göre sayar — ÖNERİ
Not "hiç sıfırlanmaz" diyor ama sayacın kapsamını söylemiyor.
**Öneri:** sayaç **(kiracı, şirket öneki, tür öneki)** başına. `MG-ORG-000001` ile `GMP-ORG-000001` birlikte var
olabilir. Kod değişmez olduğu için (5.2) şirketler arası transferde de çakışma doğmaz. Notun gerekçesi
("transferde çakışmayı önler") bu şekilde de karşılanır, numaralar şirket içinde ardışık kalır.

### 5.2 Transfer ve kodun kalıcılığı — ÖNERİ
**Öneri:** `ŞİRKET` öneki kaydı **ilk açan** şirkettir ve kod, doküman UID'i gibi **kalıcıdır**. Kayıt başka bir
şirkete geçse bile kod yeniden yazılmaz (aksi halde her yazışma referansı kırılır). Kaydın bugünkü şirketi ayrı bir
alanda durur; kod yalnız kimliktir, şu anki sahibi söylemez.

### 5.3 Şirketi olmayan kayıtlar — ÖNERİ
Not her kodun bir şirket öneki olduğunu varsayıyor, ama bazı kayıtların şirketi yok:
- **Kiracı genelinde ortak kayıt** (bir şirkete değil gruba ait, ör. Görev Türü): öneki **grubun öneki** olur
  (kiracı ayarı, ör. `GMG`). Belge kodlarındaki `OrgPrefix` ile aynı değer.
- **Platform genelinde kayıt** (ör. Abonelik Planı; bütün kiracıların ortak kataloğu): şirket öneki **yok**,
  yalnız `PLN-000001`. Ticari adı (STARTER / PRO / ENTERPRISE) İKİSİ DE kuralıyla, gerekçeli değişiklikle girilir.

### 5.4 Şirket önekinin kaynağı ve Tüzel Kişiliğin kendi kodu — ÖNERİ
**Öneri:** her Tüzel Kişilik bir **kısa kod** taşır (2–5 büyük harf, kiracıda benzersiz). `ŞİRKET` öneki budur.
Tüzel Kişiliğin kendi kodu bu kısa koddur (İKİSİ DE: sistem addan önerir, gerekçeyle değişir). Bu kısa kod bir kez
herhangi bir kaydın önekinde kullanıldıktan sonra **değiştirilemez**; aksi halde önek ile şirket arasındaki bağ kopar.

### 5.5 Tür önekleri — ÖNERİ
**Öneri:** tür öneki **3 büyük harf**, bu kuralın §3 tablosunda merkezî olarak kayıtlı. Yeni bir kayıt türü önek
alırken buraya satır eklenir; aynı önek iki türde kullanılamaz.

### 5.6 "15–19 elle girilen varlık" listesi ve yeni modüller — ÖNERİ
Not, sorunun 15–19 varlıkla ilgili olduğunu söylüyor ama yalnız 8'ini sayıyor.
**Öneri:** kullanıcıya görünen kodu olan **her yeni kayıt türü** varsayılan olarak RCS-001'e girer. Modül paketi
türün sınıfını (OTOMATİK / İKİSİ DE) ve önekini **gerekçesiyle** yazar. Açıkça KALSIN olanlar ve ürün kimliği
ailesi (§4) istisnadır. Bugün elle girilen ama notta sayılmayan türler (Marka, MDM marka ürünü vb.) için sınıf,
geri uyum işinde (backlog) karara bağlanır.

### 5.7 Sayaç dolarsa — ÖNERİ
6 hane bir **alt sınırdır**: `999999`'dan sonra sayaç 7 haneye uzar (`1000000`), sıfıra dönmez.

### 5.8 Eşzamanlılık, boşluk ve yeniden kullanım — ÖNERİ
- Sayaç atomik ilerler (tek koşullu yazım: `findOneAndUpdate` + `$inc`). İki eşzamanlı kayıt aynı numarayı alamaz.
- Başarısız kayıtta harcanan numara **geri verilmez**; boşluk doğal ve kabul. Ardışıklık bir garanti değildir.
- Silinen ya da arşivlenen kaydın kodu **asla yeniden kullanılmaz**.
- Önerilen desen: rezerve et → kaydı yaz → tüket (MDM `CodeReservation` deseni).

### 5.9 Elle değiştirilen kodun çakışma riski — ÖNERİ
İKİSİ DE türünde kullanıcı kod yazarken sayacın ileride üreteceği bir kodu (ör. `MG-ORG-000050`) yazarsa çakışma
doğar. **Öneri:** elle yazılan kod, herhangi bir türün otomatik desenine (`{2-5 harf}-{3 harf}-{6+ hane}`) **uymayan**
bir biçimde olmalı (ör. şirketin gerçek kısaltması). Ya da sayaç üretirken mevcut kodu atlar. Birincisi daha basit;
varsayılan odur.

### 5.10 Karakter seti — ÖNERİ
Kodlar dil bağımsızdır: yalnız **ASCII `A–Z`, `0–9` ve `-`**. Türkçe harf yok. Bugünkü normalleştirici
`"Satış Bölümü 1"` → `SATI-BÖLÜMÜ-1` üretiyor (`Ö`, `Ü` kalıyor, `Ş` düşüyor). Yeni üretici bunu yapmaz; elle girişte
Türkçe harf ASCII karşılığına çevrilir (`Ş→S`, `Ö→O` …).

### 5.11 İçe aktarma ve göç — ÖNERİ
İçe aktarılan kayıt kendi eski kodunu "eski kod" (LegacyCode) alanında taşıyabilir. Yeni formatlı kod sistem
tarafından verilir. İçe aktarma, otomatik desene uyan bir kod getiriyorsa sayaç o türün en büyük numarasının üstünden
devam eder (Doküman Yönetimi'ndeki *ManualImport* deseni).

### 5.12 Ortak servis — ÖNERİ
Bütün türler **tek bir ortak kod üretme servisini** kullanır: önek ayarı, sayaç, öneri, gerekçe kaydı. Bugün dört ayrı
sayaç var (CRM hesap, CRM kampanya, MDM ürün, Doküman); yeni türler beşinci, altıncı kopyayı yazmaz.

---

## 6. Modül paketi ve geliştirme kontrol listesi

Kullanıcıya görünen kodu olan her yeni kayıt türünde paket ve kod şunları içerir:

- [ ] Sınıf (OTOMATİK / İKİSİ DE / KALSIN) ve gerekçesi, §3 tablosuna satır
- [ ] Tür öneki (§5.5), çakışmadığı ölçülmüş
- [ ] Ortak kod servisini kullanır (§5.12); kendi sayacını yazmaz
- [ ] İKİSİ DE ise: gerekçe alanı zorunlu, denetim kaydında eski / yeni kod + gerekçe (AUD-001)
- [ ] Kod oluşturulduktan sonra OTOMATİK türde değiştirilemez; API de reddeder (yalnız ekran değil)
- [ ] Benzersizlik kiracı genelinde (ya da gerekçeli kapsam); arşivli kayıtların kodu da dolu sayılır
- [ ] Testler: eşzamanlı iki oluşturma farklı kod alır; değiştirme gerekçesiz reddedilir; arşivli kodu yeniden
      kullanılamaz; sayaç 999999'dan sonra 7 haneye uzar; kod ASCII'dir

## 7. Geri uyum (mevcut modüller)

Bugün uyumsuz olan türler (§3) bu kurala **yeni kayıtlar için** taşınır; mevcut kodlar değişmez (§2).
İş, ürün backlog'unda ayrı bir kayıtla izlenir (BL-565). Organizasyon verisi girişi bu işe bağlıdır
(notun "sonraki adımlar" kısmı: yeni otomatik kodlama devreye girince başlar).
