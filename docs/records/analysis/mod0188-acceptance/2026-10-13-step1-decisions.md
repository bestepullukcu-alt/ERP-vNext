# MOD-0188 Adim 1 - Ali kararlari ve S1/S3 kapsam onerisi

Bu karar kaydi, `2026-10-13-evidence-baseline.md` dosyasini degistirmez. Ali'nin bu
sohbette verdigi kararlar ile MOD-0188 pack'indeki acik AC'leri ayirir. Kod,
canli entegrasyon, bagimsiz K13 veya AC kabul kaniti degildir. `ready-for-dev`
korunur; AC-D01-D29 aciktir.

**S1 durumu:** Kod ve testler tamam; gercek veriyle tarayici kaniti ertelendi
(acik madde): satir kabulu, audit izi, birim isareti ve `ar` RTL. S1 tamamlandi
sayilmaz; hicbir AC kapanmaz. S3 icin Mongo/fixture kaniti ayri olculecek;
S1/S3 tarayici kaniti daha sonra birlikte alinacak.

## Karar listesi

| Konu | Ali karari | Henuz kanitlanmayan / sonraki sahip | Ilgili AC |
|---|---|---|---|
| Kabul edilmis gecmis | S1: onayli dosya batch'inden ayri, audit'li satir kabul islemi. S3 forecast yalniz bu kabul edilmis dosya snapshot'ini tuketir. Asama 2: MOD-0183 gerceklesmis sevkiyat satirlari ancak Shipment-bundle 3.3.0 onayi sonrasi eklenir; ayni is bir kez sayilir, iki lineage saklanir, icerik catismasi karantinadir. | MOD-0188 kimlik, duzeltme, birim, karantina cozumu ve audit kapilari; MOD-0183 gmg-10 3.3.0 satir kimligi/kaynak surumu ve file/live esleme eki. Batch onayi gecmis kabulune esit degildir; AC-D05/D06 bu eke kadar yok/acik. | D03-D09, D15 |
| Stockout | Bilinmeyen stok/coverage haftasi sifir talep sayilmaz. | Tarihsel coverage/stockout ureticisi; veri yeterliligi ve uyarinin gercek veriyle sinanmasi. | D08-D09 |
| Gecici sirket secimi | Tekli secim; tenant'in MDM'deki butun aktif LegalEntity'leri Gateway `GET /api/legal-entities/lookup` ile listelenir. Her istekte MDM `lookup-validation` ayni tenant/aktif kimligi dogrular. Pozisyon bazli kullanici-sirket yetkisi simdilik aranmaz. MDM yoksa 503. | MOD-0188 tek scope reader ve dogrudan API/JWT testleri. Ekran listesi icin `mdm.legal-entities.read` tenant entitlement ile verilir; `DefaultRolePermissionTemplate` genisletilmez. Canli erisim/Gateway kaniti ve kayit/seri veri kapsami aciktir; Demand islem izni ayridir. | D01, D03-D04, D10-D28; D27 kismi |
| Hedef sirket yetkisi | Merkezi ekip butun MVP'lerden sonra Platform "yetkili sirketlerim" ucunu yapacak. Aktif pozisyon -> Organization Unit -> LegalEntity zinciri okuyucunun uygulamasini degistirecek; ek dislama/alt birim genisletmesi tanimlanmaz. | Platform iptal edilmis atama filtresi, sonuc sozlesmesi ve erisim kaybi testi. Gecici MDM kuralinin bunu karsiladigi iddia edilmez. | D27 |
| Takvim | ISO Pazartesi, 52 hafta. Is gunu/tatil Platform `working-calendars/resolve` kaynagindan; bulunamazsa is gunu uydurulmaz. Mali yil takvim yili + ISO haftasidir; MDM `FiscalYearVariant` okunmaz. Saat dilimi gecici tenant `Timezone`. | BL-629 mali yil varyanti ve BL-628 sirket saat dilimi aciktir; eski hafta sinirlari geriye donuk degismez. | D01-D02, D15 |
| SKU/UoM | Canli MDM GSKU/LSKU kimlik/durum kontrolu ve tek-birim ara duzeni korunur; PRODUCT-MASTER v1.0 `getSku` / `getSkuUom` tuketici dogrulamasi Prism taklidiyle gelistirilir. Tek-birim modu yalniz acik ayarla, canli uclar gelene kadar gecerli; `PackUomCode` temel birim sayilmaz. | MOD-0290 canli uclari; tarihli donusumun `conversionId` ve `asOf` alanlari v1.2'de (takvimi yok). Gecersiz/belirsiz donusum gecmise girmez. | D07, D09 |
| Demand v2 | Owned OpenAPI/olay semasi DRAFT kalir. Kanonik checksum tanimi alan sirasi ve kodlamayla yazildi; yeni Published eski baseline icin ayri Superseded outbox niyeti gerektirir. MOD-0189 bagli tuketicidir; MOD-0191, S&OP ve Capacity yalniz adaydir. | HTTP satir projection'inda eksik yeniden-hesaplama alanlari, kodda eksik Superseded outbox, bagli MRP okuma, olay teslimi, cursor/freeze ve canli kapsam. | D18, D23-D29 |

## S1 onerisi - dosyadan kabul edilmis gecmise ince dikey dilim

1. Tek okuyucu arkasinda MDM sirket listesini ve her istekte ayni-tenant/aktif
   secim dogrulamasini kur; Demand izinleri ve mevcut Tenant/kayit/seri sinirini
   gevsetme. Yetkili SKU x Warehouse kapsam kaynagi yoksa ilgili islem kapali
   kalir. MDM hatasinda 503/sifir etki. Gateway route degisikligi bu lane'in
   disindadir; mevcut route ve izin erisimi on kontrolde dogrulanir.
2. Mevcut import/batch incelemesinden **ayri** bir satir kabul islemi tasarla:
   kaynak kimligi/icerik, duzeltme, temel birim, karantina cozumu, actor/izin,
   zaman ve audit kaniti. Atomik etki, tekrar istek ve yarista tek sayim
   olustur. Batch onayinda gecmis kaydi olusmadigini koru.
3. Kabul edilmis dosya snapshot'ini Tenant x LegalEntity x SKU x Warehouse x
   as-of kapsaminda okunabilir yap; gercek sifir, Missing ve Unknown ayrimini
   koru. MOD-0183 canli satirlari S1'e girmez.
4. Yalniz degisen guard'lar icin odakli HTTP/JWT ve gecici Mongo testleri;
   dogrudan API, MDM arizasi, karantina, audit arizasi ve tekrar/yaris etkisini
   olc. Bu oneri kodlama yetkisi veya D03-D08 kabul karari degildir.

## S3 onerisi - kabul edilmis dosya gecmisinden tahmin, Draft, yayin

S1 snapshot'i ve surumlu yontem politikasi dogrulanmadan S3 otomatik forecast
acilmaz. As-of sonrasi sizma yasak; seri bazli uygunluk/rolling-origin,
secili yontemin acik gerekcesi, 52 haftalik sonuc, Draft'a kaynak/parametre
kokeni, bagimsiz inceleme ve tek Published baseline ayni zincirde izlenir.
Yetersiz guvenilir gecmiste yalniz gerekceli tamamen manuel Draft korunur.
Unknown/stockout sifira donusmez. Yontemle uretilmis haftanin yayin ve kayipsiz
rollback kokeni henuz teknik aciktir. S3'te MOD-0183 file/live birlestirmesi,
MOD-0189 netting ve Demand v2 freeze/tuketici kabulunu bitmis varsayma.

## Demand v2 - Ali icin kisa onay ozeti

- **Uclar:** Owned DRAFT OpenAPI'de Draft olusturma/okuma/manual week/submit,
  approve/reject/reopen/exclude, current Published by period, revision
  status/manifest/sayfa, ayri Invalidated audit okumasi, rollback Draft,
  publish ve invalidate vardir. Cycle, import ve forecast producer yuzeylerinin
  tamami bu DRAFT'ta modellenmis degildir. Demand v1 FROZEN kalir.
- **Olaylar:** DRAFT sema Published, ayri Superseded ve Invalidated durumlarini
  eventId, revision, kapsam, stateVersion ve butunluk ozetiyle satirsiz tasir.
  Outbox'a yazilmasi teslim edildigi anlamina gelmez.
- **Tuketiciler:** MOD-0189 MRP yetkili status ve degismez snapshot'in birincil
  bagli tuketicisidir; MOD-0191 Safety Stock, S&OP ve Capacity planli
  tuketici adaylaridir. Bu ucunun kesin kabul/versiyon ihtiyaci ayrica
  teyit edilmelidir; MOD-0189 draft pack'i de canli tuketici kaniti degildir.
- **Acik sorular:** Ayri Superseded outbox niyetinin kod ve teslim kaniti;
  bagli MOD-0189 run kaniti; wire veride eksik kanonik alanlar;
  cursor/stateVersion gecisi; olay tasiyici/retry/replay; canli sirket/seri
  kapsami, izin katalogu, saklama ve Gateway yayin yolu.
- **CT onerisi:** Kanonik checksum'u bagimsiz tuketici test vektoru ve tam wire
  alanlariyla dogrula; cursor davranisini kesinlestir. Superseded icin ayri olay
  niyetini ve yetkili status + uzlastirmayi birlikte test et. MOD-0189 sahibi
  ve CT kabulunu almadan sozlesmeyi dondurma.
  Canli Platform kapisinin gecici MDM kuralindan farkini D27 acik maddesiyle
  birlikte yayin notuna yaz. Olay teslimi ve Superseded icerik okumasini
  varmis gibi sunma.

Kaynaklar: `execution/domains/supply-chain-execution/module-packs/MOD-0188-demand-planning.md`,
`services/Diten.PlanningService/contracts/drafts/demand-v2.openapi.yaml`,
`services/Diten.PlanningService/contracts/drafts/demand-v2.events.schema.json`,
`docs/analysis/contracts/product-master-bundle.openapi.yaml` ve ilk kanit
tablosu. Bu belge karar/oneriyi kaydeder; kaynak dosyalari degistirmez.
