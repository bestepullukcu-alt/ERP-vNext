# Uçtan uca test — TUTUKON: içerik → strateji → ziyaret planı → ziyaret raporu

> **CT, 2026-10-07.** Kullanıcı kararı: "ben giriş yaparım, uçtan uca testi sen yaparsın".
> Yer: [yol haritası](ROADMAP-visit-planning.md) **Faz E2E** — WP-VP-FIX-2 kabulünden sonra. Faz 2b ajan işiyle paralel yürüyebilir.
> Önceki bağlam: memory `crm-content-visit-e2e-test` (2026-09-28 zincir haritası) · SB-3a / SB-3b (oyun ürün satırı → yolculuk, yolculuk ilerlemesi çözücüsü) · WP-VP-2 E4 (oyun türetmesi canlıda veri yüzünden görünmedi).

## Amaç
Bir temsilcinin (Beste) planladığı ziyarette **"ne sunacağım"** bilgisinin uçtan uca gelmesini canlıda görmek:

doktor → aktif segment → aktif oyun (STR-TUTUKON) → ürün satırı → yayımlanmış yolculuk → aşama → yol (path) adımları + içerik → planlanan ziyaret içerik kalemleri → Ziyaret Yürütme → ziyaret raporu.

## Roller
| Kim | Ne yapar |
|---|---|
| **Kullanıcı** | Fleet'i başlatır, gerekli kullanıcıyla **giriş yapar** (Beste = Admin User; onay adımlarında sema). Şifre CT'ye verilmez. |
| **CT** | Adımları ayrı sekmede **kendisi** yürütür, her adımı doğrular, kanıt (ekran / API çıktısı) toplar, bulguyu kaydeder, gerekirse düzeltme WP'si yazar. |

**Yazma kuralı:**
- Bu planda listelenen kayıt adımları kullanıcı tarafından **bu test için onaylıdır** (2026-10-07).
- CT her yazmadan önce ne yazacağını tek satırla söyler.
- Liste dışı bir yazma gerekirse önce sorar.
- Silme / arşivleme yalnız `E2E-` önekli test kayıtlarında yapılır.
- Test kayıtları **`E2E-TUT-`** önekiyle adlandırılır (temizlik için).

## Ürün kararı (2026-09-28, hatırlatma)
- TUTUKON = **sindirim konforu**; hedef uzmanlıklar **gastroenteroloji, aile hekimliği, iç hastalıkları** (üroloji değil).
- Mevcut oyun `STR-TUTUKON-URO` **arşivlenmiş** `SEG-URO-DOCTORS` segmentine bağlı → yeniden hedeflenecek.

## Adımlar
| # | Adım | Kim / nasıl | Beklenen | Durum |
|---|---|---|---|---|
| E0 ☑ | **Envanter (yalnız okuma):** TUTUKON konu / başlık / içerik / yol / yolculuk / oyun / segment / sıklık politikası / iddia; Beste'nin 4 ilçesindeki gastro + aile + dahiliye doktor sayısı | CT (Mongo + API, salt okuma) | Eksikler listesi; hangi adımların gerekli olduğu kesinleşir | ☐ |
| E1 ☑ | **Segment:** `E2E-TUT-SINDIRIM` (kişi; uzmanlık ∈ {gastroenteroloji, aile hekimliği, iç hastalıkları}) oluştur → değerlendir → **etkinleştir** | CT, Segmentler sayfası (Beste) | Üye sayısı > 0; Beste'nin ilçelerindeki doktorlardan üye var | ☐ |
| E2 ☑ | **İçerik:** en az 2 KnowledgeContent (TUTUKON, tr) — promo detaylama + bir non-promo bilgi. Gerekirse iddia bağı. Yayımla (onay akışı varsa sema) | CT (Beste) + kullanıcı sema girişi (onay) | İçerikler `published`, tr | ☐ |
| E3 ☑ | **Yol (KnowledgePath):** içerikleri adım olarak içeren yol → gönder → onay → yayımla | CT + sema | Yol `released` / yayımlanmış sürüm | ☐ |
| E4 ☑ | **Yolculuk (ContentEngagementJourney):** en az 2 aşama, her aşama yola bağlı → yayımla | CT (+ sema SoD gerekirse) | Yolculuk yayımlanmış, aşamalar sıralı | ☐ |
| E5 ☑ | **Oyun:** STR-TUTUKON'un **yeni sürümü** — segment bağlaması `E2E-TUT-SINDIRIM`, ürün satırı TUTUKON → E4 yolculuğu, sıklık politikası → etkinleştir | CT (Beste) | Aktif oyun yeni segmente bağlı; eski sürüm "superseded" | ☐ |
| E6 ☑ | **Sıklık:** TUTUKON segmenti için sıklık politikası (ör. dönemde 3) aktif mi; değilse `E2E-TUT-VFP` oluştur + etkinleştir | CT | Önizlemede `frequencyStatus = resolved` | ☐ |
| E7 ☑ | **Plan:** Beste ile yeni taslak (aktif dönem, boş ilk hafta) → Hedefler'de bölge araması → uzmanlığı uygun 5–10 doktor + 1–2 eczane → Hedefleri kaydet → **Önizleme** | CT (Beste) | `contentStatus = resolved` (segment üyesi doktorlarda), içerik kalemleri (ürün, aşama, adımlar), süre tipik modelden; tatil / hafta sonu boş; bölge uyarısı yok | ☐ |
| E8 ☑ | **Uygula** (haftanın planı) | CT (Beste) | Planlanan Ziyaretler'de kayıtlar, `contentItems` dolu, adlar görünür | ☐ |
| E9 ◐ | **Ziyaret Yürütme:** bir ziyareti aç → içerik / sunum bilgisi → sonucu kaydet → rapor gönder | CT (Beste) | Rapor kaydı; yolculuk ilerlemesi bir sonraki aşamaya geçer (SB-3b) | ☐ |
| E10 ◐ | **Sonraki ziyaret:** aynı doktor için sonraki haftanın önizlemesi | CT | İçerik bir sonraki aşamadan gelir | ☐ |
| E11 ☑ | **Mobil görünüm:** `resources/me`, planlanan ziyaret liste / detay, `my-accounts` yanıtlarını API'den kaydet | CT (salt okuma) | Mobil sözleşme notu için gerçek örnek yanıtlar | ☐ |

## Çıktı
- Her adımın kanıtı ve bulgular bu dosyanın sonunda **§Sonuç** bölümüne yazılır.
- Her hata bir düzeltme WP'sine dönüşür; yol haritasına eklenir.
- Test sonunda `E2E-TUT-` kayıtlarının listesi; temizlik kullanıcı kararıyla.

## Bilinen riskler (önceki analizden)
- Oyun – yolculuk bağı (SB-3a) öncesi kayıtlar yolculuk taşımıyor → `ProductHasNoJourney`.
- Yolculuk aşamasında yol yoksa içerik gelmez (SB-3b).
- İçerik dili / kitle bağlamı uyuşmazsa içerik düşer.
- İçerik yayımı iddia (claim) onayı gerektirebilir (BE-6 yayın kapısı).
- Onay adımları için sema girişi gerekir (kullanıcı).

## §Sonuç

### E0 — Envanter (CT, 2026-10-07; yalnız okuma: Mongo `DitenERP_Dev` + API) ☑
| Parça | TUTUKON durumu | Not |
|---|---|---|
| Konu | ✓ `SUBJ-002` "GP-000000000063 — TUTUKON" (aktif, MDM global ürün `b1ebcf4d…`) | — |
| Başlıklar | ✓ `TOPIC-001` Bitkisel Formülasyon · `TOPIC-002` Sindirim Konforu Desteği · `TOPIC-003` Kullanım ve Dozaj (aktif) | — |
| Kitle profili | ⚠ yalnız `AUDP-001` Nephrology / Doctor, `AUDP-002` General Surgery / Family Medicine | Gastro / dahiliye için kitle yok → gerekirse E2'de `AUDP` eklenir |
| Kavram / zincir | ✓ `TPL-TUTUKON-01` yayımlanmış (+ bir taslak) | Bilgi Yolu stüdyosu bu zinciri kullanabilir |
| **İçerik** | ✗ **0** (tüm kiracıda 5 içerik, hepsi ALMIBA) | E2 gerekli |
| **Yol** | ✗ yalnız `KP-2026-2C045D` "test" (taslak, 0 adım) | E3 gerekli |
| **Yolculuk** | ✗ **0** TUTUKON yolculuğu | E4 gerekli |
| **Oyun** | ⚠ `STR-TUTUKON-URO` aktif ama: (a) arşivli `SEG-URO-DOCTORS`'a bağlı; (b) ürün satırında **yolculuk yok** (SB-3a öncesi kayıt → `ProductHasNoJourney`); (c) içerik bağı **artık var olmayan** yola (`KP-2026-49DE5D`) | E5'te yeni sürüm şart |
| Segment | ✗ uygun aktif segment yok (aktif: ALMIBA nefroloji, KOL kardiyoloji) | E1 gerekli |
| Sıklık | ⚠ `VFP-URO-TUTUKON` (arşivli segmente bağlı, ayda 2) | E6'da yeni segmente politika |
| İddia | yalnız `CLM-ALMIBA-02` (onaylı) | TUTUKON içeriği iddiasız yayımlanabilir mi → E2'de görülecek |
| Güvenlilik metni | 2 kayıt (SAF-TR-0001…) | — |
| Hedef hekim (Beste'nin 4 ilçesi) | ✓ aile hekimliği 1.105 · dahiliye 679 · gastro 72 (Şişli 414 / 242 / 29 · Fatih 327 / 379 / 41 · Kağıthane 189 / 19 / 1 · Beyoğlu 175 / 39 / 1) | Yeterli |
| Diğer | planlanan ziyaret 177 (176 aktif), ziyaret raporu 2 (taslak), `journey_progress` 0, kampanya 1 (hedef 0) | — |

**E0 bulguları:**
- **R-1 (veri riski):** Aktif ALMIBA segmenti `contact.specialty in ["Nephrology"]` (büyük harf) diyor. Kişilerde kod küçük harf (`nephrology`). Değerlendirici büyük / küçük harfe duyarlıysa üye 0 olur → E1'de yeni segment **küçük harf kodlarla** kurulur, ALMIBA ayrıca kontrol edilir.
- **R-2:** Oyunun eski yol bağı silinmiş bir kayda işaret ediyor (veri silme sonrası artık bağ) → yeni sürümde kaldırılır.
- **VP-FIX-2 E4 (kısmi):**
  - ✓ `my-accounts` "Hamidiye" / "hamidiye" / "HAMİDİYE" → 4;
  - ✓ "şişli" → 36;
  - ✓ kişi "şirin" / "ŞİRİN" → 106;
  - Düzenle-hedefler-korunur kontrolü **E7'de** (test taslağında).

### E1–E11 için gerekli yazmalar (CT önerisi, sırayla)
1. **E1 segment** `E2E-TUT-SINDIRIM`:
   - kişi; `contact.specialty in [gastroenterology, family-medicine, internal-medicine]`;
   - değerlendir → etkinleştir.
2. **E2 içerik:** 2–3 KnowledgeContent (SUBJ-002; TOPIC-002 / 003; tr):
   - "TUTUKON — Sindirim konforu (detaylama)" (promo sunum);
   - "TUTUKON — Kullanım ve dozaj" (bilgi).

   Gerekirse kitle `E2E-TUT-AUDP` (Gastroenteroloji / Aile / Dahiliye). Yayımla (onay akışı varsa sema).
3. **E3 yol:** Bilgi Yolu `E2E-TUT-PATH` (TPL-TUTUKON-01 zinciri, adımlar = E2 içerikleri) → inceleme / onay → yayımla.
4. **E4 yolculuk:** `E2E-TUT-CEJ`, 2 aşama (aşama 1 = E3 yolu, aşama 2 = aynı ya da ikinci yol) → yayımla.
5. **E5 oyun:** STR-TUTUKON yeni sürüm:
   - segment `E2E-TUT-SINDIRIM`;
   - ürün satırı TUTUKON → `E2E-TUT-CEJ`;
   - eski yol bağı kaldırılır;
   - sıklık → E6 politikası;
   - etkinleştir.
6. **E6 sıklık:** `E2E-TUT-VFP` (segment `E2E-TUT-SINDIRIM`, ayda 2) → etkinleştir.
7. **E7–E10:** Beste ile plan / önizleme / uygula / ziyaret raporu (planlanan ziyaret + rapor kayıtları).
### E1 — Segment (CT, 2026-10-07; Beste girişi) ☑ (kapsam daraltıldı)
**Yazmalar:**
- segment oluşturuldu `seg-2026-d7b132` "E2E-TUT-SINDIRIM — Gastroenteroloji hekimleri (TUTUKON test)" (id `aaa53ad4-20ba-4998-8d94-e4cffbbdb093`), kural `contact.specialty in [gastroenterology]`;
- **etkinleştirildi** (v1, donduruldu). Üye **910** (ülke geneli); Beste'nin ilçelerinde 72 gastroenterolog.

**Kapsam değişikliği (CT kararı, test için):** plandaki "gastro + aile + dahiliye" kurulamadı (E1-B2) → yalnız gastroenteroloji.

**Bulgular:**
- **E1-B1 (hata):** Segment düzenleyicide referans kümesi değerleri (uzmanlık vb.) **hiç listelenmiyor**.
  - Kök: `wwwroot/assets/js/CRM/Segments/form.js` `loadReferenceOptions` `x.value || x.valueCode` / `x.text` arıyor. Uç (`api/reference-values/{set}`) artık `code` / `label` dönüyor (MOD-0048 consumable-sets biçimi, WP-BRD sonrası).
  - Sonuç: kullanıcı değeri elle yazmak zorunda. ALMIBA segmentindeki "Nephrology" muhtemelen böyle girildi.
  - Tüm referans nitelikleri etkilenir → küçük düzeltme.
- **E1-B2 (sınır / ürün):** Dinamik segment önizlemesi aday kümesi **10.000**'i aşınca 422 `segment_candidate_set_too_large`.
  - Aday kümesi yalnız ilk koşuldan (uzmanlık) kuruluyor; bölge gibi ek bloklar aday sorgusuna inmiyor.
  - Ülke genelinde aile hekimliği 44.568, dahiliye 10.574 → bu uzmanlıklarla segment **kurulamıyor**, bölgeyle daraltmak da işe yaramıyor.
  - Karar / iş gerekir: ek blokları aday sorgusuna indirmek ya da sınır stratejisi.
- **E1-B3:** Segment oluşturma sayfasında `GET /CRM/Segments/api/global-products?pageSize=200` → **400** (ürün seçicisi; incelenecek).
- **R-1 kapandı:** değerlendirici büyük / küçük harf duyarsız. ALMIBA "Nephrology" kuralı 611 üye veriyor.
### E2 — İçerik (CT, 2026-10-07; Beste girişi) ☑
**Yazmalar (Bilgi Bankası → İçerik Oluştur, sonra Düzenle → durum):**
| Kod | Başlık | Tür | Başlık (topic) | Dil | Gövde ref. | Durum |
|---|---|---|---|---|---|---|
| `KC-2026-E70D11` (`2b4d5763…`) | E2E-TUT — TUTUKON: Sindirim konforu (detaylama) | presentation | TOPIC-002 Sindirim Konforu Desteği | tr | `e2e-tut/sindirim-konforu-detaylama-v1` | **published** |
| `KC-2026-2EC045` (`ad5ed530…`) | E2E-TUT — TUTUKON: Kullanım ve dozaj | brochure | TOPIC-003 Kullanım ve Dozaj | tr | `e2e-tut/kullanim-ve-dozaj-v1` | **published** |

Konu SUBJ-002 (TUTUKON). Kitle profili boş (alan zorunlu değil). Ürün bağlanamadı (E2-B1). Mongo'da doğrulandı.

**Bulgular:**
- **E2-B1 (hata):** İçerik formundaki **Ürün** seçicisi yalnız ilk 100 ürünü listeliyor (kiracıda 176). `GP-000000000063 — TUTUKON` listede yok → içerik ürüne bağlanamıyor.
- **E2-B2 (ürün / uyum kararı):** İçerik durumu formdan doğrudan `published` seçilerek yayımlanıyor; inceleme / onay (MLR) adımı yok. Yayın kapısı yalnız bağlı iddia varsa devreye giriyor. Tanıtım içeriği için onay akışı gerekip gerekmediği karar gerektirir.
- **E2-B3 (bilinen):** İçerik türü / durum / kaynak / dil açılırları ham kod gösteriyor (presentation, draft, manual, tr).
- Not: düzenlemeden sonra kayıttaki `Version` 0 kaldı (iyimser eşzamanlılık sayacı artmıyor olabilir) → E2E-FIX'te kontrol.
### E3 — Bilgi Yolu (CT, 2026-10-07; Beste girişi) ◐ onay bekliyor
**Yazmalar:**
- Yol oluşturuldu `KP-2026-269A07` "E2E-TUT — TUTUKON sindirim konforu yolu" (id `29eff507-85fb-4dd8-bbe0-043c67ee4e3d`).
  - Zincir: Tutukon Sindirim Konforu Zinciri vv1, TR / tr.
  - Ürün TUTUKON ve kitle "General Surgery / Family Medicine" zincirden geldi.
- 5 adım dolduruldu:
  - Ana akış: Bileşen / Etki ← detaylama · Fayda ← detaylama · İhtiyaç ← kullanım ve dozaj.
  - Kısa akış: Fayda ← detaylama · İhtiyaç ← kullanım ve dozaj.
- **Onaya gönderildi** (Rev 1) → MLR akışı Medikal → Hukuk → Ruhsat; iş öğesi Görev Merkezi'nde. Onaylar için **sema girişi** gerekiyor.

**Bulgular:**
- **E3-B1 (küçük):** Adım kartları boşken "İsteğe bağlı" etiketi taşıyor ama "Eksik" engeli sayılıyor (5 engel). Doldurunca "Zorunlu" oluyor → etiket tutarsız.
- **E3-B2 (gözlem):** Kitle zincirden "General Surgery / Family Medicine" geliyor; hedef gastroenteroloji. Kitle yol ekranında değiştirilemiyor. Ziyaret çözücüsü kitleyi kullanıyorsa içerik eşleşmesi etkilenebilir → E7'de görülecek.
### E3 (devam) — MLR onayı + yayın (CT, 2026-10-07; sema girişi) ☑
**Yazmalar:** İnceleyici görünümünden Medikal → Hukuk → Ruhsat **onaylandı** (not: "E2E test — … onay."). Sonra **Çıktı oluştur**: arşiv PDF `KP-2026-269A07-v1.0-R1.pdf`, 87,5 KB, parmak izi `d739…51ae`. Ardından **Yayınla** → "Yayınlandı; yol sahada" (v1.0 **Yayında**).

Yayın kontrol listesi tam: MLR onaylı, çıktı hazır, tüm içerikler yayında, tek dil, iddialar kullanılabilir, zincir uyumu temiz, yayınlayan ≠ gönderen.

**Bulgular:**
- **E3-B3 (küçük):** Son (Ruhsat) onayında da "Sıradaki adım Görev Merkezi'nde açılır" mesajı çıkıyor, oysa sıradaki adım yok.
- **E3-B4 (incelenecek):** Yol sayfasında sema (gönderen değil) için "Onaydan geri al" düğmesi görünüyor. Kim geri alabilir → kural kontrolü.

### E4 — Yolculuk (CT, 2026-10-07; sema girişi) ☑
**Yazmalar:** yolculuk `CEJ-2026-8AD806` "E2E-TUT — TUTUKON sindirim konforu yolculuğu" (id `97a1b154-0c0e-4480-86f7-fed1912577b5`).
- SUBJ-002 / TOPIC-002 / AUDP-002 / tr.
- 2 aşama:
  - #1 `E2E-TUT-S1` Farkındalık (awareness);
  - #2 `E2E-TUT-S2` Pekiştirme (reinforcement);
  - ikisi de → `KP-2026-269A07` latest-published, ilerletme `visit-completed`, zorunlu, tekrarlanamaz.
- **Yayımlandı** (Mongo: `JourneyStatus = published`).

**Bulgular:**
- **E4-B1:** Aşama listesindeki "repeated" rozeti, aşama tekrarlanamaz olarak kaydedildikten sonra da görünüyor → rozet yanlış.
- **E4-B2:** Aşama formu yeni aşamada "zorunlu"yu kapalı başlatıyor. Yayın "en az bir zorunlu aşama" istiyor (V-J11); hata mesajı **İngilizce** ("A journey can only be published…"). Onay penceresi "Emin misiniz? Devam etmek istediğinize emin misiniz?" diye iki kez soruyor.
- **E4-B3:** Aşamanın "Önerilen Bilgi Yolu" listesi konudan bağımsız tüm yolları gösteriyor (TUTUKON yolculuğunda ALMIBA yolları).
- **E4-B4 (kural / karar):** Yolculuğu oluşturan kişi (sema) kendisi yayımlayabildi. Yolda "yayınlayan ≠ gönderen" var, yolculukta yok → tutarlılık kararı.
### E6 — Sıklık (CT, 2026-10-07; sema girişi) ☑
**Yazma:** politika `vfp-2026-mi82xi` "E2E-TUT-VFP — TUTUKON gastroenteroloji, ayda 2" (id `290934ac…`).
- Hedef: segment `E2E-TUT-SINDIRIM`; monthly · 2 / ay · ağırlık 500 · kaynak manual.
- **Kaydet ve aktive et** → aktif.

**Bulgu:**
- **E6-B1 (küçük):** Çakışma ağırlığı seçilmeden kaydedince yalnız "Lütfen işaretli alanları düzeltin." çıkıyor; hiçbir alan işaretlenmiyor (eksik yalnız sağdaki kontrol listesinde yazıyor).

### E5 — Oyun yeni sürüm (CT, 2026-10-07; sema girişi) ☑
**Yazmalar:** `STR-TUTUKON-URO` **v2** (id `5e6227f1-1ed9-4484-956f-0b52ab166486`).
- Ad "TUTUKON Sindirim Konforu Play (E2E-TUT)".
- Segment `E2E-TUT-SINDIRIM` (birincil); eski `SEG-URO-DOCTORS` kaldırıldı.
- Sıklık → `vfp-2026-mi82xi`.
- Ürün satırı TUTUKON · %100 · promo · yolculuk `CEJ-2026-8AD806`.
- Eski yol bağı (`KP-2026-49DE5D`) kaldırıldı.
- **Aktifleştirildi.** v1 → `SupersededByTemplateId = v2`.

**Bulgular:**
- **E5-B1:** "Kaydet ve aktifleştir" kaydetti ama **aktifleştirmedi** (v2 taslak kaldı; ayrıntıdan ayrıca "Aktifleştir" gerekti).
- **E5-B2:** v1 `TemplateStatus = active` kalıyor (yalnız `SupersededByTemplateId` doldu) → aynı soyda iki "aktif" sürüm. Çözücü segmentten bulduğu için bugün zararsız (v1'in segmenti arşivli), ama kural netleşmeli.
- **E5-B3:** Düzenleme ekranında arşivli segment bağı ad yerine ham GUID (`63deb51c…`) gösteriyor.
### E7 — Plan + önizleme (CT, 2026-10-07; Beste girişi) ☑
**Yazmalar:**
- Taslak plan `a42373cb-0439-41d8-9513-b1571d93fe40` (TR, Q4 dönemi, 42. hafta).
- Hedefler (bölge araması ile): MEMORİAL ŞİŞLİ, AMERİKAN HASTANESİ, ŞİŞLİ HAMİDİYE ETFAL → uzmanlık filtresi Gastroenterology → **16 gastroenterolog** + 1 bağlı eczane → **Hedefleri kaydet**.
- VP-FIX-2 E4 için Düzenle → hafta 43 → kaydet.

**Önizleme (kaydetmeden):**
- 33 ziyaret, 0 sığmayan.
- `contentStatus`: **resolved 32** (tüm doktorlar) + not-applicable 1 (eczane).
- Takvim `resolved`.
- Örnek (İLKER ŞEN, Ş. Hamidiye Etfal):
  - 12 Eki 09:00–09:06 → ürün **TUTUKON** (promo) · yolculuk `CEJ-2026-8AD806` · aşama **#0 Farkındalık** · yol `KP-2026-269A07` 1.0 · adımlar KC-2026-E70D11 / KC-2026-2EC045;
  - **2 Kas → aşama #1 Pekiştirme** (ilerleme planda öngörülüyor).

✅ **Zincir uçtan uca çalışıyor:** doktor → aktif segment → aktif oyun v2 → ürün satırı → yolculuk → aşama → yol adımları → içerik.

**VP-FIX-2 E4 ✓:** Düzenle ile hafta 42 → 43 kaydedildi; seçim **korundu** (16 kişi / 3 hesap / 1 eczane).

**Bulgular:**
- **E7-B1 (çözücü):** Ziyaret içerik adımları yolun **iki dalını birden** düzleştiriyor (Ana akış 3 + Kısa akış 2 = 5 adım; detaylama 3 kez). Dal seçimi (ör. ana akış, kısa ziyarette kısa akış) kuralı yok.
- **E7-B2:** Adımların süresi (`minutes`) null; ziyaret süresi 6 dk (1 promo ürün × tipik süre). İçerik / adım süresi süreye girmiyor.
- **E7-B3 (bilinen, B-4 / B-5):** 17 ziyaretin hepsi tek güne (Pazartesi) düşüyor. "Ayda 2" sıklıkta ikinci ziyaret 3 hafta sonra (2 Kas) geliyor, 2 haftalık aralık değil.
- **E7-B4 (hata):** Düzenle → Kaydet sonrası yönlendirme `/CRM/VisitPlanning/Details/true` → **404**. Kök: `form.js` güncellemede `r.body.data` (`true`) değerini kimlik sanıyor.
- **E7-B5 (bilinen, Faz 4):** Yeni plan formunda geçmiş hafta (40) seçilebiliyor.
- Önizleme öğesinde `frequencyStatus` alanı yok (planlanan ziyarette var).
### E8 — Uygula (CT, 2026-10-07; Beste girişi) ☑
**Yazma:** plan `a42373cb` → "Bu haftanın planı olarak kaydet" → oturum **committed**, **33 planlanan ziyaret** (`VP-a42373cb-0001…0033`).

**Doğrulama (API, salt okuma):**
- Ziyaretlerin hepsi `planned`, **33 / 33 hedef adı dolu**, 32 / 32 doktor ziyaretinde içerik kalemi var (eczane yok).
- Örnek `VP-a42373cb-0018` HALİL ÖZARI · Ş. Hamidiye Etfal · 9 Kas 09:00–09:06 (6 dk):
  - sıklık `resolved` `vfp-2026-mi82xi` 2 / ay;
  - içerik `strategy` · yolculuk "E2E-TUT — TUTUKON sindirim konforu yolculuğu" · aşama **Pekiştirme** · kalem TUTUKON promo, 5 adım;
  - köken: segment `E2E-TUT-SINDIRIM` + oyun v2, **sunucuda türetilmiş** (`selectionMode = recommended`).
- Tarihler: 19 Eki (17) + 9 Kas (16).

**Bulgular:**
- **E8-B1:** "Bu haftanın planı olarak kaydet" **onay sormadan** uyguluyor ve **başarı mesajı göstermiyor**. Sayfa yenilenene kadar plan düzenlenebilir görünüyor (salt okunur bandı yok).
- **E8-B2 (= E7-B3):** "Ayda 2" sıklıkta ikinci ziyaret 3 hafta sonra.
### E9 — Ziyaret Yürütme + rapor (CT, 2026-10-07; Beste girişi) ◐ (rapor ✓, ilerleme ✗)
**Yazmalar:**
- `VP-a42373cb-0001` (HALİL ÖZARI, 19 Eki) → **Tamamlandı**.
- Rapor:
  - gerçek aşama `E2E-TUT-S1` / #0, planla eşleşti;
  - geri bildirim metni;
  - sonuç kodu `detaylama-tamamlandi`;
  - → **gönderildi** (rapor `c229bb38…`, `submitted`).

**Bulgular:**
- **E9-B1 (önemli):** Rapor yolculuk ilerlemesini **beslemiyor**.
  - Ekran rapora `JourneyId` göndermiyor (`ActualContent.JourneyId = null`). Takvim öğesinde `plannedJourneyId` var ama kullanılmıyor.
  - `journey_progress` koleksiyonu **0**.
  - Sonraki ziyaretin aşaması bugün yalnız plandaki sıradan (öngörü) geliyor, gerçekleşenden değil → döngü kapanmıyor (SB-3c'nin işi; ekranın journey kimliğini göndermesi ön koşul).
- **E9-B2:** Rapor formunda "Planlanan içerik (FU04)" **"—"**, ziyarette içerik kalemi varken. Takvim kartında ürün / içerik yok (yalnız "#0") → temsilci "ne sunacağım"ı göremiyor.
- **E9-B3:** "Sunulan gerçek aşama" **serbest metin kod** + sayı. Temsilci aşama kodu bilmez (K-3 ruhuna aykırı); seçim olmalı ve varsayılan plandan gelmeli.
- **E9-B4:** Sonuç kodu zorunlu ama ekran ret nedenini göstermiyor ("İşlem başarısız").
  - Sunucu yalnız "boş değil" kontrol ediyor; ekrandaki "Sonuç kodları referans verilerinden gelir" notuna rağmen **referans doğrulaması yok**.
  - Alan serbest metin.
- **E9-B5:** 12 gün **ileri tarihli** ziyarete bugünden "Tamamlandı" ve rapor girilebiliyor (tarih kısıtı yok).
- **E9-B6:** "Tamamlandı" onay sormadan işleniyor. Kartta sonuç ham kodla görünüyor ("completed"). Sayfa başlığı "Ziyaret Raporu", menü "Ziyaret Yürütme".
- ✓ Takvim kartında hedef adı görünüyor (B-8).

### E10 — Sonraki aşama ◐
- ✓ **Planda öngörü:** aynı doktorun ikinci ziyareti (9 Kas) aşama **#1 Pekiştirme** ile planlandı.
- ✗ **Gerçekleşenle ilerleme yok** (E9-B1): rapor sonrası `journey_progress` yazılmadı. Yeni bir planlamada ilerleme yalnız mevcut planlı ziyaretlerden sayılır.

### E11 — Mobil örnek yanıtlar ☑
`mobile/2026-10-06-visit-planning/E2E-SAMPLE-RESPONSES-2026-10-07.md`: `resources/me`, planlanan ziyaret liste öğesi (adlar + içerik kalemleri), `my-accounts`, ziyaret takvimi öğesi. CRM rotaları koddan doğrulandı.

## E2E özeti (2026-10-07)
- **Çalışan zincir:** segment → oyun v2 → ürün satırı → yolculuk → aşama → yol (MLR onaylı, yayında) → içerik → plan önizleme (32 / 32 çözüldü) → uygula (33 ziyaret, adlar + içerik + köken) → takvim → rapor.
- **Kopuk halka:** rapor → yolculuk ilerlemesi (E9-B1). Temsilci ekranlarında "ne sunacağım" görünmüyor (E9-B2).
- **Bulgu toplamı:** E1-B1..B3, E2-B1..B3, E3-B1..B4, E4-B1..B4, E5-B1..B3, E6-B1, E7-B1..B5, E8-B1..B2, E9-B1..B6 → **E2E-FIX** paketi(leri) + **K-7** kararı.
- **Test kayıtları (`E2E-TUT-`):**
  - segment `seg-2026-d7b132`;
  - içerik `KC-2026-E70D11`, `KC-2026-2EC045`;
  - yol `KP-2026-269A07`;
  - yolculuk `CEJ-2026-8AD806`;
  - sıklık `vfp-2026-mi82xi`;
  - oyun v2 `5e6227f1`;
  - plan `a42373cb` + 33 planlanan ziyaret;
  - rapor `c229bb38`.

  Temizlik kullanıcı kararıyla.