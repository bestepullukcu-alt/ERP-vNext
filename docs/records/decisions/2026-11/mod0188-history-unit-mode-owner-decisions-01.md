# MOD-0188 — Talep geçmişi birim modu: sahip kararları 01 (D1–D6)

- **Modül:** MOD-0188 Demand Planning (MVP-4)
- **Pack:** `execution/domains/supply-chain-execution/module-packs/MOD-0188-demand-planning.md` — AC-D07 revizyonu PR #256
- **Karar veren:** Ali (GMG-ERP; MOD-0290 PRODUCT-MASTER sahibi), yazılı teyit 2026-11-17
- **Kaydeden:** MVP-5 WMS / MVP-1 Control Tower oturumu (Beste adına), 2026-11-17
- **Pack onaylayıcısı:** Beste (`bestepullukcu-alt`); pack yazarı ElikPenco (MVP-4) — yazan ≠ onaylayan

## Kaynak ve tarih

#256 bu kararları "Ali, 2026-11-11" diye anıyordu; main'de o tarihli kayıt yoktu (yalnız MVP-4 devir notu,
`docs/records/audits/2026-11/mvp4-developer-handoff-01/README.md`). Ali'nin notu: *11 Kasım tarihli ayrı bir kararım kayıtlı değil;
teyidin tarihi bugün yazılsın.* Bu yüzden aşağıdaki kararların **tek tarihi 2026-11-17**'dir. Ali'ye beş madde soruldu; cevaplar
olduğu gibi aşağıdadır.

## D1 — Birim modu açma sınırı

**Karar (Ali: "Evet"):** `DemandPlanning:HistoryUnitMode` üretim varsayılanı `SingleUnitInterim`dir. `ProductMasterV1` kodunun ya da
Prism testinin varlığı modu açmaz. Paylaşılan ya da kiracıya hizmet veren ortamda `ProductMasterV1` yalnız şu üçü **birlikte**
varsa açılır:

1. PRODUCT-MASTER v1.1 dondurma kaydı;
2. canlı tüketici kanıtı;
3. ayrı, kayıtlı sahip açma kararı.

## D2 — Eski satırda görünür uyarı

**Karar (Ali: "Evet"):** Ara düzende kabul edilmiş bir satırın birimi güncel temel birimden farklıysa satır silinmez ve otomatik
yeniden incelemeye alınmaz; yalnız görünür uyarı verilir. Uyarı, güncel temel birim eşleşse bile, v1.2 tarihli dönüşüm kanıtı
(`conversionId`/`asOf`, BL-584) ve sahibin onayladığı uzlaştırma kararı gelene kadar kalkmaz. Bu, önceki onaylı pack'teki
"satır yeniden incelemeye gider" davranışını değiştirir.

**Bilinen yan etki (Ali'nin isteğiyle açıkça yazılır):** Uzlaştırma kuralı gelene kadar, eski satırla birimi çakışan seride
farklı birimli **yeni satır kabul edilemez**; `UnitConflictsWithAcceptedHistory` ile karantinaya gider. Pack'in açık maddelerine
bu yan etki ayrıca yazılmalıdır.

## D3 — Uzlaştırma kuralı (TBD)

**Karar (Ali: "TBD"):** Çakışan serinin uzlaştırma kuralı şimdi verilmez. v1.2 tarihli dönüşüm kanıtı (BL-584) gelince
kararlaştırılır; **sahibi Ali**. Açık madde olarak **BL-667** ile backlog'a yazıldı.

## D4 — Tarihsiz v1.1 çevrimi tarihsel otorite değildir

**Karar (Ali: "Evet"):** PRODUCT-MASTER v1.1'in tarihsiz çevrimi yalnız güncel birim projeksiyonuna yardım eder; geçmiş tarihli
kabul için dönüşüm kanıtı sayılmaz.

## D5 — `contractVersion` yalnız şema kapısı

**Karar (Ali: "(a)"):** #227 tüketicisinin `getSku`/`getSkuUom` yanıtlarında `contractVersion` alanını tam `v1.1` olarak denetlemesi
**yalnız fail-closed şema kapısı** olarak kalır; iş kararı ya da çevrim seçimi bu alana göre dallanmaz. Bu sabitleme pack'te
**bilinçli kural** olarak yazılır ve **testle gösterilir** (beklenmeyen/eksik sürüm reddi). PRODUCT-MASTER v1.1 dondurulurken
yeniden gözden geçirilir.

## D6 — `ProductMasterV1` dönüşüm kuralları bu revizyona girmez

Bağımsız inceleme (2026-11-17, #256 @ `29166cd20`, UYGUN DEĞİL) pack'te D1–D5'te bulunmayan dönüşüm kuralları buldu. Ali'ye
beş madde soruldu:

- a) yeni satırın tarihsiz v1.1 çevrimiyle temel birime dönüştürülüp kabulü;
- b) birebir ya da en çok 4 adımlı yol; farklı çarpanlı / 5+ adımlı / taşan yolda karantina;
- c) 4 basamak `AwayFromZero` yuvarlama, `ConversionBelowPrecision` karantinası;
- d) aynı temel birime çözülen farklı özgün birimlerin aynı batch'te kabulü;
- e) import ile kabul arasında çevrim değişirse 409, sıfır kabul.

**Karar (Ali, 2026-11-17):** a–e şimdi pakete **girmez**; PRODUCT-MASTER v1.1 dondurulunca ayrı bir pack revizyonuyla gelir.
AC-D07 (b) yalnız açma kapısını (D1) tanımlar. #227'deki dönüşüm kodu D1 açma kapısının arkasında kapalı kalır; o revizyon
onaylanana kadar bu kod onaylı davranış sayılmaz ve canlı kabul ilan edilmez. a–e pack'in açık maddelerine **sahibi Ali** olarak
yazılır.

## Pack'e düşen işler (#256)

- "Ali, 2026-11-11" atıfları bu dosyaya ve **2026-11-17** tarihine çevrilir (D1, D2).
- D2 yan etkisi açık maddelere yazılır.
- D3 açık maddesi BL-667 ile anılır.
- D5 bilinçli kural + test beklentisi olarak yazılır; "çözülmeden canlı açma kararı verilmez" ifadesi bu kararla güncellenir.
- D6: dönüşüm kuralları (a–e) pack metninden çıkarılır; AC-D07 (b) yalnız açma kapısı; a–e açık madde, sahip Ali.

## Sınır

Bu kayıt #256'yı onaylamaz; pack onayı bağımsız inceleme (CT-APR-02) ve Beste'nin onay satırıyla (CT-APR-01) verilir. Kod, ortam
açma ya da `ProductMasterV1` etkinleştirme izni değildir.
