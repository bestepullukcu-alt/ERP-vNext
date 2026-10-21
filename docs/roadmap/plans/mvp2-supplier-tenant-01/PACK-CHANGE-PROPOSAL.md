# MOD-0140 paket değişikliği önerisi — tedarikçi kiracıda tek kayıt, şirket bazında ilişki

- WP: WP-MVP2-SUPPLIER-TENANT-01 · aşama P0 (ölçüm + öneri; **kod yok**) · tarih 2026-10-21
- Dal: `feature/mvp2/supplier-tenant-01` · taban `origin/main` `a00538e17`
- Ölçüm: `docs/records/audits/2026-10/mvp2-supplier-tenant-01/MEASUREMENT.md` (dosya:satır kanıtları orada)
- Durum: **ÖNERİ** — sahip onayı yok. Onaydan sonra MOD-0140 paketi değişir ve CT-APR-01'e göre yeniden onaylanır.

## 0. Neden

Yönetim kararı (2026-10-21, MVP-6 plan v10.0 sorusu 4 — kullanıcıdan aktarıldı): **bir tedarikçi kiracıda tek kayıttır;
kiracının bütün şirketleri onu paylaşır. Onay, ödeme koşulları ve satın alma ilişkisi şirket bazında tutulur.**

Bugün bunun tersi geçerli ve onaylı belgelerde yazılı: MOD-0140 §4 / §8 / §12 (`:42, :46, :53, :79, :98`),
`supplier.openapi.yaml:12`, DCP-010 AD-5 (`:80`). Kararın kaynağı (plan v10.0) repoda yok; onaydan önce bir karar kaydı
(ADR) olarak yazılması gerekir — K-9.

Emsal: kiracı genelinde ortak ana veri, şirket bazında hareket kalıbı repoda zaten var — MOD-0290 kalem ana kaydı
("kiracı geneli, şirket kapsamı yok", `MOD-0290-FU04-item-material-master.md:21`, Karar 6) ve CRM hesabı (MOD-0149).

## 1. Hedef model

### 1.1 `Supplier` — kiracıda tek kayıt (tedarikçinin kendisi)

| alan | not |
|---|---|
| `Id`, `TenantId` | `LegalEntityId` **yok** (EntityBase alanı boş kalır) |
| `SupplierId` (kod) | kiracı genelinde tekil; biçim değişmez (`SUP-xxxxxxxx`) |
| `Name`, `Country`, `TaxId` | `TaxId` normalize edilir (K-2) |
| `Contacts[]`, `Documents[]` | firmaya ait olgular |
| `Screening` {KycOutcome, SanctionsOutcome, ScreenedAt} | tarama sonucu firmaya ait (K-3) |
| `MergedIntoId?` | göçte birleşen kayıtlar için (yalnız göç yazar) |
| `Version`, soft delete alanları | |

Tekillik: `ux_tenant_supplierid` (TenantId, SupplierId); `ux_tenant_country_taxid` partial (TenantId, Country, TaxIdNormalized;
dolu + silinmemiş) — `mongo-indexing.md:30-31` örneğiyle uyumlu.

### 1.2 `SupplierCompanyRelationship` — şirket bazında ilişki (ad öneridir)

| alan | not |
|---|---|
| `Id`, `TenantId`, `LegalEntityId`, `SupplierRef` (Supplier.Id) | unique (TenantId, LegalEntityId, SupplierRef) |
| `Status` {Active, OnHold, Blocked, Inactive} | bugünkü `Supplier.Status` buraya taşınır |
| `OnboardingStatus`, `Approval` {WorkflowInstanceId, DecidedBy, DecidedAt} | onay şirket bazında |
| `ScreeningOverride?` {Outcome, Reason, By, At} | tarama sonucunu şirketin geçersiz kılması (K-3) |
| `PaymentTermsCode?` | ödeme koşulu (K-8); banka bilgisi YOK (Finance sınırı, MOD-0140 §20) |
| `PurchasingEnabled`, `PurchasingSince?` | "satın alma ilişkisi" — bu şirket bu tedarikçiden alabilir mi (K-7) |
| `SourceSystem?`, `ExternalRef?` | şirketin kendi ERP eşlemesi |
| `LegacySupplierCodes[]` | göçte bu şirketin eski kodları (K-5); tekil (TenantId, LegalEntityId, kod) |
| `Version`, soft delete alanları | |

### 1.3 Kiracı izolasyonu ve görünürlük

1. Her sorgu `TenantId` ile sınırlı (AGENTS.md çekirdek 2). `Supplier` sorgusu `TenantId` + `!IsDeleted`; ilişki sorgusu
   `TenantId` + `LegalEntityId` + `!IsDeleted`.
2. **Normal okuma ilişki üzerinden gider:** şirket A kullanıcısı yalnız A ile ilişkisi olan tedarikçileri görür (liste,
   detay, `/validate`). İlişki yoksa 404 — bugünkü davranışın aynısı. Böylece SEC-002 için paket istisnası gerekmez.
3. Şirket A kullanıcısı B'nin ilişki verisini (onay, ödeme koşulu, durum, harici eşleme) **hiçbir uçta** görmez.
   Ortak alanlar (ad, vergi no, iletişim, belgeler) her ilişkili şirkette aynıdır.
4. Kiracı genelinde arama (ilişki kurmak için "bu vergi no'lu firma zaten var mı?") ayrı izin:
   `procurement.suppliers.read-tenant-wide` (SEC-002 kalıbı, `data-scope-enforcement.md:70-71`; açık verilir). Döndürdüğü
   en az alan: `supplierId`, `name`, `country` — ilişki verisi dönmez.
5. **Ortak alanları kim değiştirir:** ayrı izin `procurement.suppliers.master.update` (K-1). İlişki alanları bugünkü
   `procurement.suppliers.update` / `onboard` izinleriyle ve yalnız kendi şirketinde.

### 1.4 Tüketicilerin uygunluk sorusu

Tek soru: "(kiracı, şirket, tedarikçi kodu) için **aktif ilişki** var mı?" — MVP-6 DC-02'nin istediği "olumlu Tenant+LE
uygunluk sonucu" tam olarak budur (`OWNER-DECISION-PACK.md:36-37`). Kod çözümü: önce `Supplier.SupplierId`, yoksa ilişkinin
`LegacySupplierCodes`'u — eski PO / fatura / sözleşmelerdeki kodlar çözülmeye devam eder.

## 2. Göç planı

### 2.1 Birleştirme kuralı

1. **Grup anahtarı:** (TenantId, Country, TaxIdNormalized). `TaxIdNormalized` = boşluk, nokta, tire atılmış, büyük harf
   (K-2). Vergi no'su boş kayıtlar **birleşmez**: her biri kendi kiracı kaydı + tek ilişki olur; sonradan elle birleştirme
   ayrı iş.
2. **Hayatta kalan:** grupta en eski `CreatedAt` (eşitlikte en küçük `Id`). Onun `Id`'si ve `SupplierId` kodu kiracı kaydı
   olur.
3. **Çakışan ortak alanlar:**
   - Ad ve ülke: hayatta kalanınki. Farklılıklar göç raporuna sayı olarak yazılır, veri otomatik değiştirilmez.
   - İletişimler: (Type, Email) ile tekilleştirilmiş birleşim.
   - Belgeler: (Type, EvidenceRef) ile birleşim.
   - Tarama: en yeni değerlendirme, `ScreenedAt` = o kaydın `UpdatedAt`'ı. Şirketlerin farklı sonuçları ilişkiye
     `ScreeningOverride` olarak geçmez; rapora yazılır.
4. **İlişki kaydı:** her eski kayıttan bir ilişki üretilir: şirket, `Status`, `OnboardingStatus`, `Approval`,
   `SourceSystem` / `ExternalRef`, `CreatedAt`. Eski kod hayatta kalanınkinden farklıysa `LegacySupplierCodes`'a girer.
5. **Kod çakışması:** iki **farklı** firmanın iki şirkette aynı `SUP-` koduna sahip olması mümkün (bugün tekillik şirket
   bazında). Göç bunu sayar. Çakışan ikinci firmaya yeni kod verilir, eski kodu o şirketin `LegacySupplierCodes`'una girer.
6. **Eski kayıtlar:** silinmez. Hayatta kalmayanlar `MergedIntoId` + soft delete (`IsDeleted`, `DeletedAt`) alır.
   Hayatta kalanın `LegalEntityId`'si boşaltılmaz; yeni koleksiyon kullanılır (2.3).

### 2.2 Eski kimliklere başvuran kayıtlar

PO, fatura, teklif / RFx, sözleşme ve karantina kararlarındaki `SupplierId` **yeniden yazılmaz** (K-5).
- Bu belgeler kendi şirketini zaten taşıyor.
- Kod (kiracı, şirket, kod) üzerinden ilişkiye çözülür.
- Geçmiş kayıtlar değişmez; başka modüllere yazılmaz (XMC-001); geri alma kolaylaşır.
- Göç sonrası doğrulama bir sayımdır: bu beş koleksiyondaki her (kiracı, şirket, kod) üçlüsü bir ilişkiye çözülmeli; çözülemeyen = 0.

### 2.3 Çalıştırma ve geri alma

1. **Yeni koleksiyonlar:** `supplier_masters`, `supplier_company_relationships`. Eski `suppliers` koleksiyonu göç
   boyunca salt okunur kaynak; içeriği değişmez (2.1-6 hariç, o da yalnız onaydan sonraki son adımda).
2. **Göç işi idempotent** ve üç kipli:
   - `dry-run`: yalnız sayılar ve rapor.
   - `apply`: yeni koleksiyonlara yazar; aynı girdiyle ikinci koşu bir şey değiştirmez.
   - `verify`: 2.2 sayımı ve kayıt sayıları dengesi.
3. **Okuma anahtarı:** `Suppliers:Model = PerLegalEntity | TenantShared` (varsayılan PerLegalEntity). Göç + verify geçince
   kiracı bazında açılır.
4. **Geri alma:** anahtar `PerLegalEntity`'ye döner, eski koleksiyon dokunulmamış olduğu için anında eski davranış.
   Yeni koleksiyonlar silinmez, saklanır; soft delete'ler (2.1-6) `MergedIntoId` ile geri açılabilir.
5. **Yazma dondurma:** `apply` sırasında tedarikçi yazma uçları kısa süre 503 döner (bakım penceresi), ya da `Version`
   kontrolüyle değişen kayıt yeniden alınır. Sahip seçer; öneri bakım penceresi.
6. Göç işi bir şema / taşıma değişikliğidir: CT-APR-01'e göre yüksek risk (`module-pack-standard.md:510-517`).

### 2.3a Mevcut veri sayımı (salt okunur; sahibin ortamında koşulur)

```js
// mongosh — yalnız sayı döner; vergi no, ad, kiracı adı DÖNMEZ.
db.suppliers.aggregate([
  { $match: { IsDeleted: false, TaxId: { $type: "string", $ne: "" } } },
  { $project: { TenantId: 1, LegalEntityId: 1,
      k: { $concat: [ { $ifNull: ["$Country", ""] }, "|",
           { $toUpper: { $replaceAll: { input: { $replaceAll: { input: { $replaceAll: { input: "$TaxId", find: " ", replacement: "" } }, find: ".", replacement: "" } }, find: "-", replacement: "" } } } ] } } },
  { $group: { _id: { t: "$TenantId", k: "$k" }, les: { $addToSet: "$LegalEntityId" }, n: { $sum: 1 } } },
  { $group: { _id: "$_id.t", firms: { $sum: 1 },
      firmsInManyLes: { $sum: { $cond: [ { $gt: [ { $size: "$les" }, 1 ] }, 1, 0 ] } },
      recordsToMerge: { $sum: { $cond: [ { $gt: ["$n", 1] }, { $subtract: ["$n", 1] }, 0 ] } } } },
  { $group: { _id: null, tenants: { $sum: 1 }, firms: { $sum: "$firms" },
      firmsInManyLes: { $sum: "$firmsInManyLes" }, recordsToMerge: { $sum: "$recordsToMerge" } } }
]);
// Ek sayımlar: vergi no'suz aktif kayıt sayısı; aynı SUP- kodunun birden çok şirkette farklı firmalara ait olduğu durum sayısı.
```

## 3. Sözleşme ve tüketici etkisi

| sözleşme | değişiklik | tür |
|---|---|---|
| `supplier.openapi.yaml` 1.2.0 → **1.3.0** | Açıklama (:12) yeniden yazılır: kimlik kiracıda tek, görünürlük çağıranın şirketiyle ilişki üzerinden. Mevcut işlemler **aynı şekil ve aynı şirket bazlı davranış**ta kalır; `status` = çağıran şirketin ilişki durumu (bugün de öyle). Yeni: ilişki uçları (oku / kur / durum / onay / ödeme koşulu, yalnız kendi şirketi), kiracı genelinde vergi no ile arama (ayrı izin), 409 `DUPLICATE_SUPPLIER` gövdesine mevcut `supplierId` | **eklemeli** (K16 ile uyumlu) — tek davranış farkı aşağıda |
| `requisition-po`, `contracting`, `sourcing`, `invoice-match` 1.1.0 | şekil değişmez; `supplierId` opak kod olarak kalır | değişmez |
| `supplier-performance` 1.0.0 (MOD-0147/0148, MVP-6) | şekil değişmez; DC-02 "olumlu Tenant+LE uygunluğu" = aktif ilişki olarak tanımlanır | değişmez (tanım netleşir) |
| `quarantine` 1.2.0 | `supplierId: uuid` zaten `SUP-` koduyla uyumsuz — bugünkü kusur, bu değişiklikten bağımsız | **yalnız rapor** (Inventory sahibi; XMC-001) |

**Tek davranış farkı (üretici yüzeyi):** şirket B'de, kiracıda zaten var olan bir vergi no ile tedarikçi oluşturmak bugün
kabul ediliyor; yeni modelde 409 + mevcut `supplierId` döner, ekran "mevcut tedarikçiyi bu şirkete bağla"yı önerir (K-6).
Bunu çağıran yalnız Diten.Web tedarikçi ekranı; MVP-6 tüketicileri tedarikçi oluşturmuyor. Yıkıcı değil; yine de geçiş
penceresi önerisi: 1.3.0 yayımından sonra bir sürüm boyunca eski davranış `Suppliers:Model=PerLegalEntity` ile korunur.

**Etkilenen tüketiciler** (yalnız rapor, dokunulmadı):
- MOD-0141 / 0143 / 0144 / 0145: beş doğrulama noktası (`CreatePurchaseOrderHandler.cs:72`, `CaptureInvoiceHandler.cs:78`,
  `CreateRfxEventHandler.cs:73`, `SubmitBidHandler.cs:81`, `CreateContractHandler.cs:61`). Repository arayüzü aynı kalırsa
  kodları değişmez; Blocked / OnHold reddi eklenecekse (K-7) davranış değişir → sahiplerinin onayı (XMC-001).
- MOD-0148 / 0147 (MVP-6): kod yok; DC-02 bu modelle karşılanır. Paketleri ve MVP-6 seam belgeleri ("tenant-scoped")
  sahiplerince güncellenir.
- Inventory karantina: değişmez; tip kusuru ayrıca raporlanır.

## 4. Dilimler

| dilim | içerik | testler | sabotaj (üretim kodunda) |
|---|---|---|---|
| **S1** model + göç | iki varlık, indeksler, göç işi (dry-run / apply / verify), `Suppliers:Model` anahtarı | gerçek replica set (27017–27021 ve ≥49152 dışında geçici Mongo): birleştirme kuralı, vergi no normalizasyonu, kod çakışması, idempotent ikinci koşu, verify sayımı, geri alma | birleştirme anahtarından Country'yi çıkar → farklı ülke aynı no testi kırmızı; idempotency kontrolünü kaldır → ikinci koşu testi kırmızı |
| **S2** komutlar / sorgular | repository ikiye ayrılır (Supplier: kiracı; ilişki: kiracı + şirket); liste / detay / validate ilişki üzerinden; oluşturma = bağla-ya-da-oluştur; ortak alan güncelleme (ayrı izin); ilişki komutları (durum, onay, ödeme koşulu); uygunluk sorusu (§1.4) | şirket A, B'nin ilişkisini göremez / değiştiremez (404); ilişkisi olmayan şirkette 404; kiracı genelinde vergi no tekrarı 409 + mevcut kod; eski kod çözülür; izin olmadan kiracı geneli arama 403 | ilişki sorgusundan `LegalEntityId`'yi kaldır → izolasyon testi kırmızı; legacy kod çözümünü kaldır → eski PO kodu testi kırmızı |
| **S3** ekranlar (7 dil) | liste = şirketin tedarikçileri; "mevcut tedarikçi bulundu → bağla" akışı; ilişki paneli (durum, onay, ödeme koşulu); ortak alanlar yalnız yetkiliye düzenlenebilir; UAS-001 | RESX eşitliği (7 dil), görünüm anahtarları çevrili, ekran tarayıcıda (E3) | bir dilden anahtar sil → RESX testi kırmızı |
| **S4** tüketiciler | Procurement içindeki beş doğrulama ilişki uygunluğuna bağlanır (gerekirse Blocked / OnHold reddi); MVP-6 DC-02 tanımı; karantina tip kusuru sahibine | her tüketici için: ilişkisi olmayan şirkette 404; Blocked'da red (K-7 kararıyla) | uygunluk kontrolünü atla → tüketici testleri kırmızı |

Her dilim: ayrı commit, ayrı PR; push öncesi `origin/main` merge + ölçüm yeniden (main'e göre yeni kırmızı yok).

## 5. Sahip kararları (A / B; öneri tek)

| # | soru | A | B | öneri |
|---|---|---|---|---|
| K-1 | Ortak alanları kim değiştirir? | ilişkisi olan her şirket (`update` izniyle) | ayrı kiracı geneli izin `procurement.suppliers.master.update` | **B** — bir şirketin değişikliği hepsini etkiler; ana veri sahipliği ayrı olmalı (MOD-0290 kalıbı) |
| K-2 | Vergi no tekillik anahtarı | (Kiracı, TaxId) | (Kiracı, Ülke, normalize TaxId) | **B** — vergi no ulusal; farklı ülkede aynı no farklı firma |
| K-3 | KYC / yaptırım taraması | firmada tek sonuç | her şirkette ayrı | **A + şirket override'ı** — tarama firmanın olgusu; "Override" şirket kararı |
| K-4 | Yeni yazma komutlarının denetimi (AUD-001 K4: Procurement'ta yeni yazma komutu BL-501'e kadar engelli; BL-501 MVP'lerden sonra) | BL-501'i bekle (S2 ilişki komutları MVP'lerden sonra) | CT, Procurement için kapsamlı istisna verir (ilişki geçmişi aynı işlemde, ekranda — yol c) | **B** — MVP-6 DC-02 bu komutlara bağlı; karar yalnız CT'nin (kural "kendi günlüğümüz" seçeneğini açıkça yasaklıyor) |
| K-5 | Birleşen kayıtların eski kodları | PO / fatura vb.'de kodları yeniden yaz | eski kodu ilişkide takma ad olarak tut, geçmişi yazma | **B** — geçmiş değişmez, başka modüle yazılmaz, geri alınabilir |
| K-6 | Başka şirkette aynı vergi no ile oluşturma | 409 + mevcut kod, kullanıcı bağlar | sessizce otomatik bağla | **A** — bağlamak onay gerektiren bir şirket kararı |
| K-7 | Tüketiciler Blocked / OnHold'u reddetsin mi? | S4'te evet | şimdilik hayır | **A** — "durum şirket bazında" kararının anlamı bu; bugün hiç bakılmıyor (MEASUREMENT §3.2) |
| K-8 | Ödeme koşulu | ilişkide `PaymentTermsCode` (kod; ana tablo sonra) | Finance'a bırak (MOD-0140 §20) | **A** — yönetim kararı ödeme koşulunu şirket ilişkisine koyuyor; banka bilgisi Finance'ta kalır |
| K-9 | Kararın kaydı | yalnız bu öneri | ADR: `docs/records/decisions/2026-10/` | **B** — plan v10.0 repoda yok; onaylı paket değişikliği kayıtlı karar ister |
| K-10 | Paket onayı | yazan kişi onaylar | CT-APR-01 yüksek risk: `high: evet`, paketi yazmamış onaylayıcı | **B** — kural zorunlu kılıyor (kiracı + şema / taşıma) |

Ayrıca bilinmesi gerekenler:
- MOD-0140'ta §21 "Audited Events" yok. İlk yazma komutu değişikliğinde zorunlu olur (`module-pack-standard.md:282`).
- `approvals.tsv`'de MOD-0140 satırı yok.

## 6. Bu P0'da yapılmayanlar

Kod, test, sözleşme ve paket dosyası değişmedi. Bunlar: MOD-0140 paketi, `supplier.openapi.yaml`, DCP-010 ve tüketici
paketleri. Onaydan sonra S1'den önce yazılacaklar:
- MOD-0140 paket değişikliği: §4, §8, §12, yeni §21 ve `## Teslim dilimleri`.
- Sözleşme 1.3.0 taslağı.
- ADR (K-9).
