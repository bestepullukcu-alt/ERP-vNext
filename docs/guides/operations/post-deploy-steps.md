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

### 3 · Marka modülünün (`brand-product-master`) planlara eklenmesi

| | |
|---|---|
| **Modül** | MOD-0290 — Marka / Ürün ana verisi (WP-MDM-BRAND-ACCEPT-01 FIX2 madde 1, FIX3 R-1 / M-4 / M-7) |
| **Ne zaman** | Marka işi birleştikten sonraki İLK deploy'da, aşağıdaki dağıtım sırasıyla |
| **Yapılmazsa** | **Kiracı Markalar menüsünü ve Ctrl+K girişini kaybeder.** BRANDS sayfası artık `brand-product-master` modülündedir ve menü plan güdümlüdür; modülü içermeyen plandaki kiracının menüsünde Markalar yoktur. Yeni kiracının Admin rolü marka anahtarlarını (`mdm.brands.*`, `mdm.products.*`, `mdm.brands.export`) plan senkronundan almaz. |
| **Kim** | Platform yöneticisi. Dev'de sahip, canlıda sahibin onayıyla. Kod plan verisini **yazmaz**. |

**Adım (kesin):** Platform → Planlar'da `product-item-sku-master` içeren her plana
`brand-product-master` eklenir; eklenmezse kiracı Markalar menüsünü ve Ctrl+K girişini kaybeder.

Plan → modül üyeliği **veridir**: seed ya da Platform kodu değişmez (CONTROL TOWER kararı,
2026-10-06). Kural: bugün `product-item-sku-master` içeren HER plan; CRM'in marka tüketen
planları ayrıca değerlendirilmez — marka, ürün ana verisiyle aynı pakettir (SAP / Oracle'da
da marka, malzeme ana verisinin parçasıdır).

**Ölçüm (dev, salt okunur, 2026-10-07):** `diten_personalization_dev` içinde 5 plan var
(FREE, STARTER, PROFESSIONAL, ENTERPRISE, DEV); **hiçbiri** `PRODUCT-ITEM-SKU-MASTER` içermiyor
(`IncludedModuleKeys`; DEV planı yalnız GOLDENSLIM, LEGAL-ENTITY, WORKFLOW). Hiçbir kiracının bu
modüle `tenant_module_entitlements` satırı da yok. Yani dev'de eklenecek plan **yok**; canlıda
aynı sorgu deploy öncesi yeniden çalıştırılır (dev ölçümü canlıyı temsil etmez):

```
db.platform_subscription_plans.find({ IncludedModuleKeys: "PRODUCT-ITEM-SKU-MASTER" }, { Code: 1, Name: 1 })
```

**Dağıtım sırası (FIX3 M-7):**

1. **Platform** (katalog) — önce. MDM, açılışta iki manifestini (`product-item-sku-master` ve
   yeni `brand-product-master`) Platform'a kaydeder; Platform yoksa kayıt yeniden denenir.
2. **Auth** — sonra. Açılışta `mdm.brands.export` kataloğa girer, `tenant-97c5`'in sabit hakkına
   eklenir ve **tek yönlü, kiracı başına bir kez** geri doldurma çalışır: `mdm.brands.read`
   tutan her role `mdm.brands.export` verilir (işaretli kiracıya bir daha bakılmaz; yöneticinin
   geri alması kalıcıdır — `BrandsExportGrantBackfill`).
3. **MDM** — en son. Kayıt Platform'da yeni modülü oluşturur, Auth eşitlemesi izinleri işler.

Geçiş sırasında beklenen, **zararsız** eşitleme hataları:
- MDM, Platform'dan önce açılırsa: modül kaydı başarısız ve yeniden deneniyor uyarısı
  (Platform gelince düzelir).
- Auth eşitlemesi `brand-product-master` için boş ya da eksik tanımlayıcı görürse: o kiracı
  atlanır (boş çekme = atla); bir sonraki eşitlemede düzelir.
- Plan adımı yapılmadan: yeni kiracılarda marka anahtarı yok; mevcut kiracıların elle ya da
  seed'le verilmiş hakları **silinmez** (model A mevcut yetkiyi kaldırmaz).

⚠ **Geçiş riski 🟡.** Ekleme yapılana kadar plan güdümlü kiracılar Markalar menüsünü görmez
(sessiz, veri kaybı yok). `tenant-97c5`'in seed'deki sabit marka hakları (`DataSeeder`
`BrandProductPermissionKeys`) dokunulmadan kalır; ekleme sonrası o kiracıda aynı anahtar
hem sabit hem plan kaynaklı görünebilir — yinelenen yetki, fazladan erişim değildir.

---

## Tamamlananlar

*(henüz yok)*
