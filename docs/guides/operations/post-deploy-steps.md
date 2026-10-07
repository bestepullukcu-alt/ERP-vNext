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

### 3 · Global Ürün denetim işlemleri: Platform ÖNCE (BL-552)

| | |
|---|---|
| **Modül** | MOD-0290 — Global Ürün düzeltme, emeklilik isteği ve kimlik onayı (BL-552); Platform MOD-0021 merkezi denetim |
| **Ne zaman** | BL-552'yi (akış 2–3, `23fb2d85f` ve sonrası) taşıyan deploy'da — **Platform, MDM'den ÖNCE** |
| **Yapılmazsa** | MDM'nin yeni denetim işlemleri (79 `GlobalProductIdentityDecisionNotApplicable`, 80 `GlobalProductCorrectionEnded`, 81 `GlobalProductRetirementEnded`) Platform'da tanınmaz. Platform 409 `AUDIT_SOURCE_INTENT_MAPPING_UNSUPPORTED` döner. MDM bu üç işlem için en çok 2880 deneme (varsayılan 30 sn ile yaklaşık bir gün) bekler; süre geçerse **niyetler ölü mektuba düşer ve merkeze ulaşmaz**. Ölü mektuptan yeniden gönderme aracı yoktur. |
| **Kim** | Deploy operatörü |

**Kontrol:** MDM deploy edildikten sonra son teslim hatası `AUDIT_SOURCE_INTENT_MAPPING_UNSUPPORTED` olan niyet kalmamalı.
Kalıyorsa Platform bu işlemleri eşleyen sürümde değildir. Teslim işçisi o niyetleri yeniden deneyen her döngüde bir
Warning yazar: "Audit delivery: N intent(s) of … wait for Platform to map the operation". N toplam birikim değildir:
döngü kiracı başına en çok `BatchSize` (varsayılan 25) niyet dener, N o döngüde denenip bekletilenlerin sayısıdır.
Platform 409'un gövdesini zamanında vermezse (500 ms) yalnız bu üç işlem `AUDIT_SOURCE_INTENT_TIMEOUT` ile yeniden
denenir; diğer işlemler için 409 kalıcıdır.

### 4 · GP kimlik onayı ayarları artık başlatmayı durdurur (BL-552 akış 3)

| | |
|---|---|
| **Modül** | MOD-0290 — Global Ürün kimlik onayı işçisi (`ProductIdentityWorkflowWorker`) |
| **Ne zaman** | `1204887c3` ve sonrasını taşıyan deploy'dan ÖNCE yapılandırmaya bakılır |
| **Yapılmazsa** | Aralık dışı değer artık sessizce düzeltilmez; MDM **açılmaz** (`PRODUCT_IDENTITY_WORKFLOW_WORKER_CONFIGURATION_INVALID`). İşçi kapalı olsa da geçerlidir, çünkü etkileşimli gönderim de bu ayarları okur. |
| **Kim** | Deploy operatörü |

Sınırlar: `StartSettleSeconds` 1–1500 (varsayılan 120; üç işçide de — Platform süpürme yaşı (WorkflowStartRecovery AbandonAfterMinutes) MDM yerleşme penceresinden en az 5 dk uzun olmalı; 1500'ü aşan değer artık açılışı durdurur); `QuarantineAfterFailedAttempts` 0–1000 (varsayılan 30, 0 = hiç);
`StuckGaugeIntervalSeconds` 15–3600 (varsayılan 60). Aynı `StuckGaugeIntervalSeconds` sınırı GP düzeltme ve emeklilik
isteği işçilerinde de vardır; o iki işçi de artık aralık dışı ayarla **kapalıyken bile** açılmayı durdurur
(`GLOBAL_PRODUCT_CORRECTION_WORKER_CONFIGURATION_INVALID`, `GLOBAL_PRODUCT_RETIREMENT_WORKER_CONFIGURATION_INVALID`).

---

## Tamamlananlar

*(henüz yok)*
