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

### 3 · Global Ürün / GSKU / LSKU denetim işlemleri: Platform ÖNCE (BL-552, BL-566)

| | |
|---|---|
| **Modül** | MOD-0290 — Global Ürün düzeltme, emeklilik isteği ve kimlik onayı (BL-552); GSKU / LSKU (LSKU FIX1); Platform MOD-0021 merkezi denetim |
| **Ne zaman** | BL-552'yi (akış 2–3, `23fb2d85f` ve sonrası) ve LSKU FIX1'i taşıyan deploy'da — **Platform, MDM'den ÖNCE** |
| **Yapılmazsa** | MDM'nin yeni denetim işlemleri (79 `GlobalProductIdentityDecisionNotApplicable`, 80 `GlobalProductCorrectionEnded`, 81 `GlobalProductRetirementEnded`, 82 `GskuChildCreationAdmissionReleased`, 83 `LskuDraftCancelled`, 84 `LskuIdentityDecisionNotApplicable`, 85 `LskuRetirementManualReconciliationRequired`, 86 `LskuIdentitySubmitAbandoned`) Platform'da tanınmaz. Platform 409 `AUDIT_SOURCE_INTENT_MAPPING_UNSUPPORTED` döner. Canlı Platform'da bugün hiçbir MDM ürün işlemi eşlenmiş değil (harita `origin/main`'de yok), bu yüzden bu ilk sürümde MDM'nin ürettiği **bütün** ürün işlemleri için (üretilmeyen 43 / 47 hariç; kural ve taban tek yerde: `CentralMappingRollout`) en çok 2880 deneme (varsayılan 30 sn ile yaklaşık bir gün) bekler; süre geçerse **niyetler ölü mektuba düşer ve merkeze ulaşmaz**. Ölü mektuptan yeniden gönderme aracı yoktur. |
| **Kim** | Deploy operatörü |

**Kontrol:** MDM deploy edildikten sonra son teslim hatası `AUDIT_SOURCE_INTENT_MAPPING_UNSUPPORTED` olan niyet kalmamalı.
Kalıyorsa Platform bu işlemleri eşleyen sürümde değildir. Teslim işçisi o niyetleri yeniden deneyen her döngüde bir
Warning yazar: "Audit delivery: N intent(s) of … wait for Platform to map the operation". N toplam birikim değildir:
döngü kiracı başına en çok `BatchSize` (varsayılan 25) niyet dener, N o döngüde denenip bekletilenlerin sayısıdır.
Platform 409'un gövdesini zamanında vermezse (500 ms) bekleyebilen işlemler (bu sürümde üretilen bütün ürün işlemleri)
`AUDIT_SOURCE_INTENT_TIMEOUT` ile yeniden denenir; yalnız üretilmeyen 43 / 47 için 409 kalıcıdır.

### 4 · GP kimlik onayı ayarları artık başlatmayı durdurur (BL-552 akış 3)

| | |
|---|---|
| **Modül** | MOD-0290 — Global Ürün kimlik onayı işçisi (`ProductIdentityWorkflowWorker`) |
| **Ne zaman** | `1204887c3` ve sonrasını taşıyan deploy'dan ÖNCE yapılandırmaya bakılır |
| **Yapılmazsa** | Aralık dışı değer artık sessizce düzeltilmez; MDM **açılmaz** (`PRODUCT_IDENTITY_WORKFLOW_WORKER_CONFIGURATION_INVALID`). İşçi kapalı olsa da geçerlidir, çünkü etkileşimli gönderim de bu ayarları okur. |
| **Kim** | Deploy operatörü |

Sınırlar: `StartSettleSeconds` 1–600 (varsayılan 120; üç GP işçisinde de — işletim kuralı: Platform `WorkflowStartRecovery:AbandonAfterMinutes` × 60 ≥ MDM `StartSettleSeconds` + 300; Platform tabanı 15 dk olduğu için 600 her Platform ayarında güvenli; 600'ü aşan değer artık açılışı durdurur); `QuarantineAfterFailedAttempts` 0–1000 (varsayılan 30, 0 = hiç);
`StuckGaugeIntervalSeconds` 15–3600 (varsayılan 60). Aynı `StuckGaugeIntervalSeconds` sınırı GP düzeltme ve emeklilik
isteği işçilerinde de vardır; o iki işçi de artık aralık dışı ayarla **kapalıyken bile** açılmayı durdurur
(`GLOBAL_PRODUCT_CORRECTION_WORKER_CONFIGURATION_INVALID`, `GLOBAL_PRODUCT_RETIREMENT_WORKER_CONFIGURATION_INVALID`).

**GSKU / LSKU işçileri de (WP-MDM-SETTLE-CAP-01):** `GskuCorrectionWorkflowWorker`, `GskuRetirementRequestWorkflowWorker`,
`LskuIdentityWorkflowWorker`, `LskuRetirementRequestWorkflowWorker` bölümlerinde `StartSettleSeconds` artık 1–600
(dün 1–3600). Aşan değerde MDM **açılmaz**: `GSKU_CORRECTION_WORKER_CONFIGURATION_INVALID`,
`GSKU_RETIREMENT_REQUEST_WORKER_CONFIGURATION_INVALID`, `LSKU_IDENTITY_WORKFLOW_WORKER_CONFIGURATION_INVALID`,
`LSKU_RETIREMENT_WORKER_CONFIGURATION_INVALID`. İzlenen yapılandırmada değer yok (varsayılan 120); deploy operatörü canlı
ortam değişkenlerine bakar.

### 5 · Marka modülünün (`brand-product-master`) planlara eklenmesi

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

**Dağıtım sırası (FIX3 M-7, FIX4 F4): Platform → Auth → MDM + Web (MDM ile Web BİRLİKTE).**

1. **Platform** (katalog) — önce. MDM, açılışta iki manifestini (`product-item-sku-master` ve
   yeni `brand-product-master`) Platform'a kaydeder; Platform yoksa kayıt yeniden denenir.
2. **Auth** — sonra. Açılışta `mdm.brands.export` kataloğa girer, `tenant-97c5`'in sabit hakkına
   eklenir ve **tek yönlü, kiracı başına bir kez** geri doldurma çalışır: `mdm.brands.read`
   tutan her role `mdm.brands.export` verilir (işaretli kiracıya bir daha bakılmaz; yöneticinin
   geri alması kalıcıdır — `BrandsExportGrantBackfill`).
3. **MDM ve Web — aynı pencerede, birlikte.** Kayıt Platform'da yeni modülü oluşturur, Auth
   eşitlemesi izinleri işler. İkisi ayrı çıkarsa:
   - **yeni MDM + eski Web:** her ürün düzenlemesi 400 `expected_version_required` (eski form
     sürüm göndermez);
   - **yeni Web + eski MDM:** ürün Oluştur / Düzenle'de marka seçicisi boş (eski MDM
     `isLinkable` göndermez; yeni Web yalnız MDM'in bağlanabilir dediğini sunar).
   İkisi de veri kaybetmez ama ekranı kullanılamaz yapar: MDM ve Web aynı deploy'da çıkar.

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
