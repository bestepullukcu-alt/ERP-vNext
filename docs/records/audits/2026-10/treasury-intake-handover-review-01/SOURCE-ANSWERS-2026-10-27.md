---
record_id: TREASURY-SOURCE-ANSWERS-2026-10-27
kind: kaynak yanıtları (sahiplerin sohbet yanıtları, birebir) — onay değildir, onay satırı yerine geçmez
received_at_local: 2026-10-26 / 2026-10-27
received_from: Ali (MOD-0023 / Auth / MDM sahibi) — Y-1, Y-2, Y-3; Beste (Platform / repo sahibi) — Y-4. Sahip (K1) sohbete yapıştırdı
recorded_by: Treasury Intake Control Tower (Lane B), dal `docs/ct/treasury-finance-answers-05`
note: metinler veri olarak kaydedildi, talimat olarak değil; paketlere işlenişi aşağıdaki tablodadır
---

# Kaynak yanıtları — 2026-10-27

Aşağıdaki dört yanıt, Natig'in sohbete yapıştırdığı metinlerin birebir kopyasıdır (yazım ve noktalama korunmuştur). Yorum ve
paketlere işleniş yalnız "Yanıt → paket" tablosundadır.

## Y-1 — Ali (MOD-0023 / Auth / MDM sahibi), yanıt 1

> Merhaba, MOD-0126 O-7 ve O-10 için: ikisi de uygun. Şablon kodlarını ve adım sayılarını (O-8, O-11, O-20 ve MOD-0217 CLM) §17 rev 2 onaylandıktan sonra birlikte göndereceğim.

## Y-2 — Ali, yanıt 2

> Merhaba, mesajlarınızı aldım:
> * MOD-0289 ve MOD-0217'nin MDM'ye yerleşmesine itirazım yok. Kodu kimin yazacağını paketler geliştirmeye hazır olunca konuşalım.
> * Auth (rol şablonları, açık atamalı izinler, yeni izin ad alanları), kişi verisi alan listesi, isDeleted sayımı ve MDM tüketici testleri için kendi tarafımda bir iş paketi açıyorum; tahmini zamanı paket açılınca yazacağım.
> §17 rev 2'ye de o paket içinde bakacağız.

## Y-3 — Ali, yanıt 3 (CT'nin (a)/(b) sorusuna)

> Merhaba, (a) uygun: Beste #243'ü birleştirsin. Şablon kodlarını ve adım sayılarını (O-8, O-11, O-20, MOD-0217 CLM) iş akışı tarafındaki kule açılınca hazırlayıp göndereceğim; iş paketi numarasını ve tahmini zamanı o zaman paylaşırım. İnceleme yeni bir şey çıkarırsa rev 3 olarak açalım.

## Y-4 — Beste (Platform / repo sahibi), toplu yanıt

> 1) Onay satırları: MOD-0023 §17 (803fbaa5…), MOD-0220 §22 (91248a47…, whole_pack=TRM-14) ve MOD-0048-FU01 §21 (8fc68be8…, whole_pack=TRM-17) satırları #243'te. Her paket için tek satır var, risk HIGH, onaylayan bestepullukcu-alt. (Çakışma 2026-10-27 çözüldü, e259cb352.)
> 2) DCP numarası: UNASSIGNED-finance-treasury-intake.md'ye DCP-011 atanmıştır. Yeniden adlandırmayı kendi dalınızda yapabilirsiniz. Onayı ready-for-dev turunda.
> 3) MOD-0048 (TRM-17): payment-method ve currency-minor-units önerisini başlangıç listesi olarak kabul ediyoruz. Finance listeyi sonradan değiştirebilir. Seed JSON ve ConsumableSets izin listesi bizde; iş başlayınca haber vereceğiz.
> 4) MOD-0149: (a) Doğru olan pack (ready-for-dev). Kayıt dosyasındaki satırı hizalıyoruz. (b) GET /api/crm/accounts/{id}/lookup-validation, dış referans çözümleme ve sözleşme belgesi önerisini ekleme olarak kabul ediyoruz; MOD-0149 pack revizyonuyla gelecek. (c) account-status kümesi: active, inactive, draft, suspended. Faturalanabilir durum yalnız active.
> 5) Auth / MOD-0018: Auth sahibi Ali. Rol şablonlarını ve ExplicitGrantOnlyPermissions eklerini (read-tenant-wide, mdm.bank-accounts.read-unmasked) ona ilettik. Teyit ve zaman ondan gelecek.
> 6) SEC-002: Ortak kaynak Diten.Platform.Common içindeki IDataScopeResolver. MDM'deki ITrustedLegalEntityScopeProvider MDM'in kendi Application portu; ortak paket değil, doğrudan kullanılmaz. Öneri: FinanceTreasuryService kendi Application katmanında aynı desende bir port tanımlar; port kiracı + kullanıcı + modül + izin anahtarı alır ve izinli LegalEntityId listesini döner. Adaptör Platform'un kapsam çözümünü çağırır. Hata, boş liste ya da belirsizlikte erişim verilmez (fail-closed). read-tenant-wide yalnız açık atamalı izin olarak Auth'ta tanımlanır.
> 7) Denetim kategorisi: Kabul, ama değer 18 olacak. 17 main'de ESignature (MOD-0022) için kullanılıyor. Yalnız ekleme olarak Finance = 18. Platform enum'u ve Diten.BuildingBlocks.CentralAudit/CentralAuditPlatformValues.cs kopyası birlikte güncellenecek. Bu değer main'e girince haber verilecek.
> MOD-0124 O-7: "v1'de kur çevrimi yok; pozisyonlar kendi para birimlerinde gösterilir" kararı teyit. Kur kaynağı ileride MOD-0048 ya da ayrı modülle.
> BL-565 (RCS-001 ortak kod servisi): Sahibi Beste (D44). Çağrı sözleşmesi ayrı notla gelecek.
> supplier 1.3.0: Main'de hâlâ 1.2.0. Cihan'a soruluyor. invoice-match 1.1.1 #247 ile main'e girdi.
> PUSH ÖNCESİ MAIN SENKRONU: Her push ve PR güncellemesinden önce origin/main dala merge edilir.

## Y-5 — Ali, yanıt 4 (2. dalga isteğine)

Merhaba, teşekkürler. #243'ün birleştiğini ve MOD-0023 §17 ile MOD-0220 §22'nin onaylı sürümlerinin main'de olduğunu gördük.

1. Şablon kodları ve adım sayıları (MOD-0126 O-8, MOD-0119 O-11, MOD-0124 O-20, MOD-0120 AR düzeltme, MOD-0217 O-3): bizde açık.
   MOD-0023 tarafı için ayrı bir Control Tower açıyoruz; kodları ve adım sayılarını orada belirleyip size yazacağız. MOD-0124 için
   önerdiğiniz tek adım / 15 sn not edildi.
2. Auth: bugünkü cevabımız geçerli. Rol şablonları için rol → izin anahtarı listesini sizden bekliyoruz; elimize ulaşmadı, gönderebilir
   misiniz? mdm.bank-accounts.read-tenant-wide de açık atamalı listeye girmeli mi, teyidinizi bekliyoruz. mdm.contracts.*: MOD-0217
   paketi henüz taslak; anahtarlar MDM manifestinden kaydolur, Auth tarafındaki ek iş yalnız açık atama listesi olur ve MOD-0217
   onaylandıktan sonra yapılır.
3. MOD-0220 §22 onaylı ve LEGAL-ENTITY-LOOKUP 1.1.0 sözleşme taslağı mevcut; kodu aynı Control Tower'ın sırasına alınacak.

İş paketi numaralarını ve tahmini tarihleri Control Tower açıldıktan sonraki ilk ölçümle yazacağız.

## Y-6 — Ali, yanıt 5 (rol listesi alındı)

Merhaba, teşekkürler. Rol listesini ve açık atamalı yedi anahtarı aldık; ikisi de not edildi.

Auth tarafındaki işi şöyle yapacağız: rol şablonları ile açık atamalı anahtarlar için bir MOD-0018 paketi yazılacak, ardından kod.
Paketin numarasını ve tahmini tarihini paketi açınca yazacağız. mdm.contracts.* kayıtları ve sözleşme rolleri (belge deposu izinleri
dahil) MOD-0217 onaylandıktan sonra yapılır.

İş akışı şablonları: beş şablondan üçü karara bağlandı (ödeme partisi, satıcı faturası, nakit pozisyonu: üçü de tek adım). Kalan ikisi
(müşteri faturası düzeltmesi ve sözleşme onayı) bu hafta içinde bitecek; ardından şablon kodları, adım sayıları, iş paketi numaraları ve
tarihleri birlikte yazacağız.

## Y-7 — Ali, yanıt 6 (2026-10-27)

Güncel rol listesini (D47/D48) aldık: açık atamalı 8 anahtar, yeni iki rol ve finans rollerinden banka okuma çıkarılması dahil. Auth
paketini bu güncel listeye göre yazacağız. Tam matrisi (113 anahtar) treasury-intake-owner-decisions-05.md olarak ana dala koyabilir
misiniz? Elimizde yalnızca mesaj var, kayıt yok.
İş akışı şablonlarının beşi de tek adımlı olarak karara bağlandı. Şablon kodlarını, iş paketi numaralarını ve tarihleri kule son onayı
alınca birlikte yazacağız. Auth paketinin numarası ve tarihi paketi açınca gelecek.

## Y-8 — Beste, yanıt (2026-10-28)

BL-565: sahibi Beste (D44), backlog satırı düzeltilecek; servis 6 Kasım'a kadar hazır. Bekleme yerine: uygulama katmanında
`IRecordCodeAllocator.AllocateAsync(legalEntityId, recordType, idempotencyKey)` portu; testlerde sahte uygulama; üretimde kendi sayaç / yedek
kod yok; port bağlı değilse Create kapalı (fail-closed). v0 sözleşme: `POST /api/internal/record-codes/allocate`, gövde
`{legalEntityId, recordType, idempotencyKey}` (kiracı güvenilir bağlamdan), 200 `{code, sequence}` (aynı anahtar aynı kod), 422
`RECORD_TYPE_UNKNOWN`, 409 `LEGAL_ENTITY_SHORT_CODE_MISSING`, 404 başka kiracı, 503 → Create başarısız; 6 Kasım'da kesinleşir, değişiklik
yalnız ekleme. Okuma ekranları ve kodsuz dilimler beklemez.
Finance = 18: yapıldı (Platform enum + CentralAuditPlatformValues, sapma ve sabit testi); PR açılıyor, main'e girince haber verilecek.
MOD-0048 seed'leri: `payment-method` ve `currency-minor-units` seed'lerini Treasury CT ekler; Platform'da yalnız seed JSON ve
`ConsumableSets` izin listesine dokunma izni; PR'ı Treasury açar, Beste onaylar; önkoşul #243 (MOD-0048-FU01 onay satırı main'de).
MOD-0149 eki: kabul; revizyon cumartesiye kadar, sonra bağımsız inceleme ve onay; faturalanabilir yalnız `active`.
MOD-0262-FU01 servis kimliği: kabul; depo MDM'in servis kimliğiyle gelen çağrıyı dar bir izinle kabul eder, kiracı izolasyonu korunur,
denetimde aktör `Service`, izni olmayan servis reddedilir; Platform pack revizyonu pazartesi (2 Kasım); MDM iç istemcisinin tanımı
Auth tarafında (Ali). D56/2 kaydının yolu soruldu.
supplier 1.3.0: Cihan'a doğrudan sorulmalı. DCP-011: onay satırı ready-for-dev PR'ında.

## Y-9 — Cihan, yanıt (MOD-0140 sahibi, 2026-10-28)

1.3.0, MOD-0140 kararlarından (S-1…S-6, F-3; yöneticide) yaklaşık 3 iş günü sonra main'de; önce sözleşme dosyası olarak dondurulur
(DL-1). Aktif tedarikçi listesi `GET /api/suppliers/eligible` (`listEligibleSuppliers`): çağıranın şirketiyle silinmemiş ve Active
ilişkisi olan tedarikçiler; öğe SupplierEligibility (supplierId, supplierMasterId, status, onboardingStatus, purchasingEnabled,
eligibleForTransactions); imleçli, sayfa 50; izin `procurement.suppliers.eligibility.read` (ya da `.read`). Uç gerçekten çalışana kadar
(MOD-0140 DL-3, 1.3.0'dan ~1,5–2 hafta sonra) sözleşmeye karşı sahte istemciyle kodlanır. Soru: MOD-0289 DL-3 için liste Active ilişkili
mi, yalnız işlem yapılabilir mi? → D58: Active ilişkili hepsi.

## Y-10 — Ali, yanıt 7 (2026-10-28; #268 ve istek listesi alındı)

1. Auth (MOD-0018) paketi güncel matrise göre yazılır (D47, D49 ile güncel hâli; D54, D56, D57): 25 rol, 8 açık atamalı anahtar,
   muafiyet atamasının parti bağı ve aday sorgu ucu, SEC-002 eki, iki yeni ayar anahtarı (a–f hepsi kapsamda). Sözleşme rollerine belge
   deposu izni verilmez (D56/2 ile uyumlu). `mdm.contracts.*` kaydı MOD-0217 onayından sonra. Bu iş için ayrı bir Control Tower açılıyor;
   paket numarası ve tarihi paket açılınca bildirilecek.
2. MOD-0023 şablon kodları: iş akışı kulesi önerdiği beş adı Ali'ye onaylatıyor; onaydan sonra kodlar birlikte yazılacak.
3. MOD-0220 `ApprovalStatus` alanı: ayrı bir kule açılıyor; numara ve tarih orada ölçülüp yazılacak.
4. Numaralar ve tarihler: yukarıdakiler açılınca, ölçülmüş süreyle.
5. commercial-suite: Ali'de kayıtlı sahip ataması yok (işletim kartı da "Belirsiz — sahip ataması bekliyor" diyor) → D61.

## Y-11 — Ali / Auth kulesi, ölçüm (2026-10-28)

D54, D56 ve D57 main'de değil, yalnız `chore/ct/treasury-intake-ct-05`'te (main'deki karar dosyası D50'de bitiyor, sözleşme rollerinde eski
belge deposu izinleri görünüyor); paket metni main'e dayanmalı (D53/1). Muafiyet atamasının (waive-sod) Auth'ta karşılığı yok: yeni veri
yapısı, onboarding'den büyük iş; tarih paket netleşince. → D62/1: #270 merge'ü beklenir.

## Y-12 — Ali / MOD-0023 kulesi, şablon kodları (2026-10-27/28)

Kayıt `docs/records/decisions/2026-10/mod0023-workflow-template-owner-decisions-01.md` (dal `chore/ct/pss-wf-ct-01` @ `c927047f1`, main'de
değil). Kodlar, hepsi tek aşama / tek onay adımı / atamasız; onaylayıcıyı `StartWorkflowInstanceRequest.CandidatePrincipalIds` belirler
(boş liste `WORKFLOW_ASSIGNMENT_CANDIDATES_REQUIRED`):

| Paket / madde | Şablon kodu | Onaylayıcı |
|---|---|---|
| MOD-0126 O-8 ödeme partisi | `FT-PAYMENT-BATCH-APPROVAL` | MOD-0126 aday çözümleyicisi (SoD R1–R4; eşik üstü tier2-approve) |
| MOD-0119 O-11 AP faturası | `FT-AP-INVOICE-APPROVAL` | MOD-0119 aday çözümleyicisi (SoD R1–R3) |
| MOD-0124 O-20 nakit pozisyonu | `FT-CASH-POSITION-APPROVAL` | MOD-0124 aday çözümleyicisi |
| MOD-0120 AR düzeltme (Void, WriteOff, PaymentReversal; tek şablon) | `FT-AR-ADJUSTMENT-APPROVAL` | MOD-0120 aday çözümleyicisi; eşik üstü finans kontrolörü |
| MOD-0217 O-3 sözleşme sürümü | `MDM-CONTRACT-VERSION-APPROVAL` | sözleşme onay yetkisi olanlar, hazırlayan hariç; ayrı hukuk adımı yok |

v1'de çok adımlı / kademeli onay yok → `PartiallyApproved`'a giriş önerilmez (D62/2). 15 sn HTTP zaman aşımı tüketici ayarıdır. MOD-0217
satırı MOD-0217 onayından sonra kullanılır. WP numaraları MOD-0023 kuyruğu açılınca; süre (§17 DL-1 ~1 gün, DL-2 2–3 gün) değişmedi.

## Y-13 — Ali / MOD-0220 kulesi, TRM-14 durumu (2026-10-28)

MOD-0220 §22 onaylı (sha `91248a47…`, 2026-10-26, UYGUN); kod origin/main'de yok (`LegalEntityLookupDto` 6 üye, `approvalStatus` yok).
§22.5: kod MOD-0126 DL-1 ile aynı WP'de, ayrı commit. Engel: MOD-0126 `draft`, onay satırı yok. Ölçüm notu: TRM-14 satırının yalnız
`chore/ct/treasury-intake-ct-01`'de olduğu yazıldı; Treasury CT ölçümü — `CT-QUEUE.tsv` ve satır TRM-14 origin/main'de (#268 ile; 2026-10-28
ölçüldü). Platform'daki `MdmLegalEntityReferenceValidatorTests` §22.4 AC-T4 listesinde yok; Platform sahibi getirir.

## Y-14 — Ali / Auth, genel düzenleme bildirimi (2026-10-28)

> **Düzeltildi (Y-16):** Ali böyle bir düzenleme kararı, paketi ya da tarihi olmadığını yazdı; bildirimin kaynağı doğrulanamadı. Y-14
> geçersizdir; hiçbir pakete ya da karara işlenmedi.

Auth'ta bir kerelik genel düzenleme: izinler, "yalnız açık atama" işaretleri ve rol şablonları modüllerin kendi tanımından okunacak. Bu
girince Treasury anahtarlarını ve rol şablonlarını kendi modülünde yazar; Auth koduna dokunmak ya da Ali'den ayrıca iş istemek gerekmez.
Süreli / partiye bağlı muafiyet ataması (D54) Auth'ta yeni yetenek; ayrı ele alınır, tarih kapsam netleşince. Etki (çıkarım, karar değil):
D53 Auth paket metni kapısı ve D56'daki MOD-0018 "permission onboarding" eki bu düzenlemeyle değişebilir; ayrıntı (paket / numara, tanım
biçimi, tarih) Ali'ye soruldu.

## Y-15 — Ali, yanıt 8 (2026-10-28)

Sahip kararı (D9 / XMC-001 "(a) bu işte düzelt"; D29/1): MOD-0018 Auth eki (anahtarlar, açık atama listesi, rol şablonları) ve MOD-0220 §22
alanı (`approvalStatus` + LEGAL-ENTITY-LOOKUP 1.1.0) Treasury tarafından yazılır; sahip inceler ve teyit eder. Auth paket metni main'deki
karar dosyasına atıf yapar (D62/1). MOD-0023 §17 kodunu sahibi yazar (D25/1, D32/1); numara ve tarih kuleden. Sorular: (1) waive-sod Auth
veri yapısı ve SEC-002 Platform dosyaları için XMC-001 onayı kimden — Ali vermez, alan sahibi verir → D63; (2) D62 kaynağı main'de ve ct-05
uzak dalında yok — doğru, yalnız yerel commit `d101b3e55` idi → D63/1 ara PR; (3) paket yazılınca MOD-0018 sahibine inceleme isteği gider.

## Y-16 — Ali, Y-14 düzeltmesi ve dört cevap (2026-10-28)

Auth izinlerini modül tanımına taşıyan bir düzenleme kararı verilmedi; paket ya da tarih yok (Y-14 geçersiz). Sahip kararı: Auth tarafı
yalnız Treasury için yazılır. Cevaplar: (1) düzenleme paketi / numara / tarih yok; (2) modül tanımı biçimi değişmedi — mevcut MOD-0018
onboarding kalıbı (FU20 / FU22 örnek); (3) D53 paket metni kapısı ve D56 (SEC-002) eki aynen geçerli; (4) anahtarlar mevcut yolla
kaydedilir, DL-1 kabulü D53/2'ye göre "Auth kayıt kodu main'de" şartını bekler. Auth paketini ve MOD-0220 alanını Treasury yazar; yazınca
inceleme isteği gider. D54 için tarih kapsam netleşince.

## Y-17 — Ali, D54 Auth yapısı (2026-10-28)

D54 Auth veri yapısını da Treasury yazar (D9 / XMC-001 "(a)"): atama kaydı, yazma ucu, parti için geçerli atamalar sorgusu, denetim;
sahip inceler ve teyit eder; D63/3 düzeltilsin. Pakette açık olacaklar: kiracı izolasyonu (TenantId, kiracılar arası 404), hedef parti
yok / okunamıyor / eşleşmiyor için 403, en çok 24 saat, onaylayan ve gerekçe kodu, denetim kaydı. Platform dosyaları için XMC-001 Beste'den.

## Y-18 — Ali, Auth eki inceleme bulguları (2026-10-28; onay değil, kararlar sonra)

Auth CT ile inceleme başladı. Üç bulgu: (1) K-W3 / K-A1 — Auth'taki bütün iç uçlar bugün ortak sırla (`X-Internal-Api-Key`) çalışıyor;
servis kimliği token'ı doğrulayan şema yok; taslağın yolu yeni doğrulama katmanı ister ve pakette dosyası yok; ortak sır kiracıya bağlı
değil, kiracı token'dan alınmalı. (2) K-W1 — iki yeni Auth anahtarı da açık atama olduğundan koddaki sayı 18 değil 20; paket 8 sayıyor.
(3) Muafiyeti atayacak kişi: pakette "Hazine yöneticisi ya da K1" yazıyor ama rolde izin yok. → D74.

## Y-19 — Ali, Auth eki ilk kararlar ve şartlı teyit (2026-10-29; onay satırı / ready-for-dev onayı değildir)

(a) İlk üç karar: K-1 **FU27** (kayıt satırı paketle birlikte yazılır); K-7 sabit rol kodu, görünen ad çevirisi kaynak dosyada, kiracı yöneticisi
7 dilde görür; aday sorgusu **DL-6 ayrı dilim** kalır. Kalan kararlar (K-W5, K-W3, K-W1, K-A2/A3/A4) bulgulara göre revizyondan sonra konuşulur;
bağımsız inceleme revizyondan sonra, onay satırından önce.
(b) 04:25 mesajı: Treasury Auth ekini (main 4c08fc75a, D69–D78) **şartla teyit** ediyor; Auth CT kiracı yalıtımını kontrol etti, kapanıyor.
Şartlar: K-W5 denetim izi için defter kararı ve rol atama adımlarının (rola izin bağlama, kişiye rol verme) denetim borcu **DL-5 kodundan önce**
kapanmalı; **DL-1'i bunlar bekletmez**. Henüz vermediği: K-W5, K-A1, K-A2, K-A3 ve "önerileri kabul" listesi (K-1b, K-2, K-3, K-5, K-W2,
K-W4, K-W6); K-W7 ve K-A4b teyidi ayrıca. → D80.

## Y-20 — Beste, MOD-0048 XMC-001 izni (2026-10-29; K1 aktardı)

K1'in aktarımı (2026-10-29): "48 izin alındı" — Beste, MOD-0048 seed PR'ı için B1 (a) yolunu kabul etti: `PlatformLookupProvider.BuildCurrencyOptions`
yalnız geçerli üç harfli ISO kodlarını döndürür (`¤¤` gibi ICU yer tutucuları elenir) + test; Treasury yazar, PR'ı Beste onaylar. Yazılı metin
bu kayda alınmadı (sahip aktarımı). `module: BusinessReferenceData` sapması ve O-14 için ayrı cevap gelmedi.

## Y-21 — Ali, Auth eki açık maddeleri (2026-10-29; K1 aktardı; ready-for-dev ya da kod onayı değildir)

#293'ü gördü. D79/D80 kayıtlarındaki açık maddeleri 29 Ekim'de verdi, hepsi paketin önerisiyle: K-1b paket başına kod · K-2 hiçbiri ·
K-3 bugünkü davranış · K-W2 (a) · K-W4 `/api/roles/…` · K-W6 sürümlü dosya · K-A1 `/internal/permission-candidates` + sürümlü
sözleşme · K-A2 (b) tüketici süzer · K-A3 hizmet hesabı aday değil, geçici kilitli aday · K-W5 `auth-gunlugu` + önce-yaz, CT defter
kararı (DL-5 kodundan önce kapanmalı) · K-W7 ve K-A4b teyit · K-5'te yalnız yöntem kabul: banka / amaç anahtarlarını MOD-0289 /
MOD-0217 sahibi söyler, Platform dosyaları Beste'den (XMC-001). 7 dilli rol adı tablosu (§14.2) kontrol ediliyor, sonucu ayrıca
yazacak. "MOD-0220 yeniden onayı açık, §22 alanınız o netleşene kadar ready-for-dev'e geçemez" (hangi paketin §22'si olduğu ve
D82/2 dilim kapısıyla ilişkisi Ali'ye soruldu — K1 kararı 2026-10-29). → D84.

## Y-22 — Beste, Treasury CT'ye cevap (2026-10-29; K1 aktardı)

(1) #304: sırada #299'dan başlayan PR'lar var; sonra inceleyip merge edecek; bu arada dal güncellenmeyecek (onay düşer).
(2) DCP-011 `approved`: kabul. Bugünkü sürümün (`4cb9e99a`) UYGUN incelemesi main'de, onay satırı yok. Durum `draft` → `approved`;
yalnız durum değiştiği için yeni sürüme kısa doğrulama incelemesi yeterli. Durum değişikliği + inceleme satırı + onay satırı (yazan
gmg-10, onaylayan bestepullukcu-alt) tek PR'da; PR'ı Treasury açar, Beste onaylayıp merge eder.
(3) K-6: kabul, cevabı "hayır". FinanceTreasuryService Platform kapsam ucunu doğrudan kullanmayacak; dört finans paketi kendi kapsam
yolunu yazar (O-19 / O-22 / O-14 / O-23). Soranın kimliği yalnız MDM (`DITENMDMSERVICE`); FU27 §5.2 koşullu Platform dosyaları gerekmez.
(4) SEC-002 eki (FU27 DL-4), Platform tarafı XMC-001: yalnız `TrustedLegalEntityScopeCredentialAuthenticator.cs` (K-5'teki
`mdm.bank-accounts.*` ve `mdm.contracts.*` anahtarları; `MaxAllowedPairs` eklenen çift kadar; `AllowsPair` ve `IsValidConfiguration`'da
tek modül kodu → ürün + MOD-0289 + MOD-0217 kümesi; her izin kendi modül koduyla eşleşir; `DITENMDMSERVICE` kontrolü değişmez),
`TrustedLegalEntityScopeCredentialAuthenticatorTests.cs` (yeni tavan ve çiftler, yanlış modül kodlu çiftin reddi, bilinmeyen çiftin 403'ü;
sabotaj kanıtı), `docs/guides/operations/post-deploy-steps.md` (ortam ayarı `TrustedLegalEntityScopeCredential:Mdm:AllowedPairs`: sıra,
geri alma, kontrol). Başka Platform dosyasına dokunulmaz; kapalı-başarısız korunur. K-5 listesini kontrol edecek. Kod FU27 onaylandıktan
sonra. MDM tarafı (`PlatformTrustedLegalEntityScopeProviderClient.cs` ve testleri) MDM sahibinin alanı — XMC-001 ve sıra Ali ile.
Sıra (FU27 §8): Auth izinleri → Platform kodu → Platform ortam ayarı → MDM istemcisi.
(5) BL-565 (RCS-001): v1 = allocate ucu, atomik sayaç, tür önekleri, şirket kısa kodları. Kesin sözleşme 3 Kasım'da Treasury'de;
servis v1 hedefi 6 Kasım main. "İKİSİ DE" türleri ve mevcut modüllerin taşınması sonraki dilim. Port + sahte istemci yolu (D59/1)
uyumlu. Excel ve kısa kod listesi en geç 2 Kasım'da Beste'de olmalı.
(6) MOD-0262-FU01 servis kimliği: Platform pack revizyonu 2 Kasım'da hazır; Auth tarafı (MDM için iç istemci ve izin) Ali'nin
alanı — kapsam ve tarih Ali ile; revizyon onaylanınca Platform kodunun tarihi bildirilecek. → D88.

**Özgün metin (K1'in 2026-10-29'da Treasury CT oturumuna yapıştırdığı haliyle, değiştirilmeden):**

> Beste → Treasury CT (gmg-10)
> Merhaba,
>
> * #304: sırada #299’dan başlayan PR’lar var. Onlar alındıktan sonra #304’ü inceleyip merge edeceğim. Lütfen bu arada dalı güncellemeyin, yoksa onay düşüyor.
> * DCP-011 approved: kabul.
> * Bugünkü sürümün (`4cb9e99a…`) UYGUN incelemesi main’de, ama onay satırı yok.
> * Durumu `draft` → `approved` yapın. Yalnız durum değiştiği için yeni sürüme kısa bir doğrulama incelemesi yeterli.
> * Durum değişikliğini, inceleme satırını ve onay satırını (yazan gmg-10, onaylayan bestepullukcu-alt) tek PR’da hazırlayın. PR’ı siz açın, ben onaylayıp merge ederim.
> * K-6: kabul, cevabım “hayır”.
> * FinanceTreasuryService Platform kapsam ucunu doğrudan kullanmayacak. Dört finans paketi kendi kapsam yolunu yazar (O-19 / O-22 / O-14 / O-23).
> * Soranın kimliği yalnız MDM (`DITENMDMSERVICE`) olarak kalır. FU27 §5.2'deki koşullu Platform dosyaları gerekmiyor.
> * SEC-002 eki (FU27 DL-4):
> * Platform tarafı, XMC-001 izni veriyorum. Kapsamı yalnız şu dosyalar:
> * services/Diten.Platform/src/Diten.Platform.API/Security/TrustedLegalEntityScopeCredentialAuthenticator.cs:
> * izin listesine K-5'teki `mdm.bank-accounts.*` ve `mdm.contracts.*` anahtarları;
> * `MaxAllowedPairs` tavanının eklenen çift sayısı kadar artırılması;
> * `AllowsPair` ve `IsValidConfiguration` içindeki tek modül kodunun ürün, MOD-0289 ve MOD-0217 kodlarından oluşan bir kümeye çevrilmesi.
>
> Her izin kendi modül koduyla eşleşmeli: bir banka izni ürün modülü adına sorulamaz. `DITENMDMSERVICE` kimlik kontrolü değişmez.
>
> * services/Diten.Platform/tests/Diten.Platform.Application.Tests/Authorization/TrustedLegalEntityScopeCredentialAuthenticatorTests.cs:
> * yeni tavan ve yeni çiftler;
> * yanlış modül koduyla gelen çiftin reddi;
> * bilinmeyen çiftin 403 alması.
>
> Sabotaj kanıtıyla.
>
> * docs/guides/operations/post-deploy-steps.md: ortam ayarı `TrustedLegalEntityScopeCredential:Mdm:AllowedPairs` için yeni adım; sıra, geri alma ve kontrol.
>
> Başka Platform dosyasına dokunulmaz. Kapalı-başarısız davranış korunur: hata, boş liste ya da belirsizlik “erişim yok” demek. K-5 anahtar listesini ilettiğinizde kontrol edeceğim. Kod FU27 onaylandıktan sonra yazılır.
>
> * MDM tarafı: `PlatformTrustedLegalEntityScopeProviderClient.cs` ve testleri MDM sahibinin alanı. Lütfen XMC-001 iznini ve sırayı Ali ile netleştirin.
> * Sıra (FU27 §8): Auth izinleri → Platform kodu → Platform ortam ayarı → MDM istemcisi.
> * BL-565 (RCS-001 ortak kod servisi):
> * v1 kapsamı: ayırma (allocate) endpoint’i, atomik sayaç, tür önekleri ve şirket kısa kodları.
> * Kesin sözleşme 3 Kasım’da sizde olacak, servis v1'in 6 Kasım’da main’de olmasını hedefliyoruz.
> * “İKİSİ DE” türleri ve mevcut modüllerin taşınması sonraki dilimde.
> * Port ve sahte istemci yolunuz (D59/1) bu planla uyumlu.
> * Excel’in ve kısa kod listesinin en geç 2 Kasım’da elimde olması gerekiyor.
> * MOD-0262-FU01 servis kimliği:
> * Platform tarafındaki pack revizyonu 2 Kasım’da hazır olacak.
> * Auth tarafı (MDM için iç istemci ve izin) Ali’nin alanı. Lütfen kapsamı ve tarihi doğrudan Ali ile netleştirin.
> * Revizyon onaylanınca Platform kodunun tarihini bildireceğim.

## Yanıt → etkilenen paket → işlendiği yer

Paket yolları `execution/domains/finance-treasury/module-packs/` altındadır. Her pakette ayrıca en üstte
"Revizyon (2026-10-27, kaynak yanıtları Y-1…Y-4)" notu vardır. "İşlenmedi" satırları bu dalın kapsamı dışında kalan yanıtlardır.

| yanıt | etkilenen paket | işlendiği yer |
|---|---|---|
| Y-1 (O-7, O-10 uygun) | MOD-0126 | Açık kararlar O-7 ve O-10 satırı: D42'nin yanına Ali teyidi; revizyon notu (1) |
| Y-1 / Y-3 (şablon kodu, adım sayısı: O-8, O-11, O-20, MOD-0217) | MOD-0126 (O-8); MOD-0119 O-11, MOD-0124 O-20, MOD-0217 | MOD-0126 O-8 satırı **açık** kalır, Y-3 notu eklendi. MOD-0119 O-11, MOD-0124 O-20 ve MOD-0217 işlenmedi (değer gelmedi; MOD-0217 başka domain) |
| Y-2 (MDM yerleşimi; Ali'nin Auth iş paketi) | MOD-0289, MOD-0217 (MDM); dört Treasury paketinin Auth satırları | işlenmedi — MDM paketleri başka domain; Auth iş paketi numarası ve zamanı henüz yok |
| Y-3 ((a) #243 birleştirilsin) | — (onay PR'ı) | işlenmedi — PR sürecidir, paket metni değil |
| Y-4/1 (onay satırları #243'te) | MOD-0023 §17, MOD-0220 §22, MOD-0048-FU01 §21 | işlenmedi — onay satırları `execution/approvals/` ve #243 kapsamıdır; Treasury paketlerindeki ilgili checklist maddeleri satırlar main'e girince güncellenir |
| Y-4/2 (DCP-011) | MOD-0119, MOD-0120, MOD-0124, MOD-0126 | her paketin revizyon notu: "DCP numarası DCP-011 atandı (Y-4/2); dosya adı değişikliği ayrı adım". DCP yolu ve checklist maddeleri değişmedi (DCP `approved` değil) |
| Y-4/3 (`payment-method`, `currency-minor-units` başlangıç listesi) | MOD-0126 (O-11, O-13); MOD-0119 (O-10); MOD-0120 ve MOD-0124 (O-8; ortak satır) | Dependencies satırları, kapanış tablosu, Açık kararlar satırları, checklist maddeleri (seed önkoşulu açık kalır) |
| Y-4/4a (MOD-0149 pack doğru; kayıt satırı hizalanıyor) | MOD-0120 | Dependencies MOD-0149 satırı (TRM-09); revizyon notu (3) |
| Y-4/4b (MOD-0149 `lookup-validation` + dış referans + sözleşme eki kabul) | MOD-0120 | Dependencies MOD-0149 satırı, kapanış tablosu, Açık kararlar O-3, checklist MOD-0149 eki (MOD-0149 pack revizyonuna kadar önkoşul) |
| Y-4/4c (`account-status` kümesi; faturalanabilir yalnız `active`) | MOD-0120 | Açık kararlar O-4, Validation Rules `CustomerId` satırı |
| Y-4/5 (Auth sahibi Ali; `ExplicitGrantOnlyPermissions` ekleri) | dört Treasury paketi | SEC-002 checklist maddelerinde "Ali teyidi açık" olarak anıldı; ayrı karar satırı açılmadı |
| Y-4/6 (SEC-002 tüketim yolu) | MOD-0126 (O-19), MOD-0119 (O-22), MOD-0120 (O-14, T-4), MOD-0124 (O-23) | Runtime Constraints kapsam cümlesi, Dependencies SEC-002 satırı, kapanış tablosu, Açık kararlar satırı ("Platform sahibi yanıtı Y-4/6"), checklist maddesi (kod + kanıt testi açık) |
| Y-4/7 (`AuditCategory` `Finance` = 18) | dört Treasury paketi | Audited Events `Category` metni, "kapsam dışı bağımlı değişiklikler" listesi, Açık kararlar satırı (MOD-0126 O-6, MOD-0119 O-18), checklist "değer main'de" maddesi (açık kalır) |
| Y-4 (MOD-0124 O-7 teyidi) | MOD-0124 | Açık kararlar O-7: D43'ün yanına Beste yeniden teyidi; checklist O-7 maddesi |
| Y-4 (BL-565 sahibi Beste) | MOD-0126 | işlenmedi — D44 ile zaten kayıtlı; çağrı sözleşmesi gelmedi |
| Y-4 (supplier 1.3.0 main'de yok; invoice-match 1.1.1 #247) | MOD-0119 | işlenmedi — MOD-0140 1.3.0 checklist maddesi açık kalır; durum değişmedi |
| Y-4 (push öncesi main senkronu) | — (süreç kuralı) | işlenmedi — paket metni değil; push/PR adımında uygulanır |
| Y-5/1, Y-6 (şablonlar: ödeme, satıcı faturası, nakit pozisyonu tek adım; diğer ikisi bu hafta) | MOD-0126, MOD-0119, MOD-0124 (O-8 / O-11 / O-20); MOD-0120, MOD-0217 | işlenmedi — kodlar gelince "ready-for-dev" revizyonunda; MOD-0124'ün tek adım önerisi teyit edildi |
| Y-5/2 (rol listesi istendi; `mdm.bank-accounts.read-tenant-wide` sorusu) | altı paket | D46 / D46a / D47 (`treasury-intake-owner-decisions-05.md`); soru MOD-0289 D23/1 ile cevaplandı |
| Y-5/3 (MOD-0220 §22 onaylı, LEGAL-ENTITY-LOOKUP 1.1.0 taslağı; kod MOD-0023 kulesinin sırasında) | dört Treasury paketi (TRM-14) | işlenmedi — TRM-14 checklist maddesi kod gelene kadar açık |
| Y-6 (Auth: MOD-0018 paketi + kod; `mdm.contracts.*` MOD-0217 onayından sonra) | altı paket | işlenmedi — Auth checklist maddeleri açık kalır; Ali'ye D47 güncellemesi ayrıca gönderilir |
| Y-10/1–4 (Auth kapsamı a–f teyit; şablon kodları ve MOD-0220 ayrı kulelerde; numara/tarih sonra) | altı paket | işlenmedi — Auth, TRM-14 ve şablon kodu checklist maddeleri açık kalır; numara gelince ready-for-dev revizyonunda |
| Y-10/5 (commercial-suite sahibi kayıtlı değil) | MOD-0057 | D61 (`treasury-intake-owner-decisions-05.md`) |
| Y-11 (kararlar main'de değil; waive-sod Auth karşılığı yok) | altı paket | D62/1 — #270 merge'ü beklenir; waive-sod tarihi Auth paketinde |
| Y-12 (beş şablon kodu, tek adım) | MOD-0126, MOD-0119, MOD-0124, MOD-0120, MOD-0217 | işlenmedi — ready-for-dev revizyonunda O-8 / O-11 / O-20 / MOD-0120 / MOD-0217 O-3 satırlarına; D62/2 |
| Y-13 (TRM-14; MOD-0126 onayı bekleniyor) | MOD-0126 (TRM-14) | işlenmedi — MOD-0126 onay satırı ready-for-dev PR'ında |
| Y-14 (geçersiz — Y-16) | — | işlenmedi |
| Y-15 (Auth eki ve MOD-0220 §22 Treasury yazar; XMC-001 sorusu) | altı paket; MOD-0126 (TRM-14) | D63 |
| Y-16 (Y-14 düzeltmesi; D53 / D56 aynen; MOD-0018 FU20 / FU22 kalıbı) | altı paket | işlenmedi — paket metni değişmez; Auth eki FU20 / FU22 kalıbıyla yazılır |
| Y-17 (D54 Auth yapısını Treasury yazar; beş kabul şartı) | MOD-0018 Treasury Auth eki; MOD-0126 | D64 |
| Y-18 (iç uç kimliği, açık atama sayısı, muafiyet atayan) | MOD-0018 Treasury Auth eki | D74 |
| Y-19 (FU27, K-7, DL-6; şartlı teyit — K-W5 + rol atama denetimi DL-5 kodundan önce) | MOD-0018 Treasury Auth eki; altı paket | D80 |
| Y-20 (MOD-0048 B1 izni: currencies lookup alpha-3 filtresi) | seed dalı `feature/pss/mod0048-fu01-treasury-seeds`; MOD-0126 / 0119 / 0120 / 0124 seed önkoşulu | seed PR'ında |
| Y-21 (Auth eki açık maddeleri paket önerisiyle; K-W5 yöntem, K-5 yalnız yöntem; MOD-0220 / §22 notu) | MOD-0018 Treasury Auth eki; ready-for-dev tutuşu | D84 |
| Y-22 (#304 sırası; DCP-011 onay yolu; K-6 "hayır"; Platform XMC-001 dosya kapsamı; BL-565 ve MOD-0262-FU01 tarihleri) | DCP-011; FU27 DL-4; dört finans paketi SEC-002; RCS-001 | D88 |
