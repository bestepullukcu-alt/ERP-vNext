# İddialar v2 — Mockup analizi + iş planı

> **Durum:** ANALİZ + PLAN (2026-09-28) · **Sahip:** CT
> **Girdi:** `İddialar Prototipi.html`. 13 senaryo, 5 örnek rol, 3 ülke (TR/UZ/AZ), Boxicons ve Public Sans ile mevcut tema dili.
> **Bağlı belgeler:** `SCMM-claims-approval-evidence-roadmap.md` (A: Workflow ve WorkCenterNext · B: Document Management ve MOD-0031 · E: Organizasyon), `WP-ORG-01/02` (kuruldu).

## 1. Mockup'ta ne var

| # | Senaryo | Ekran | İçerik |
|---|---|---|---|
| 1 | İddia listesi | Liste | Filtre, kolon görünürlüğü, dışa aktar. Satıra tıklayınca yan panelde hızlı görünüm. Kolonlar: iddia, ürün, tür (çekirdek/yerel), kitle, **ülke durumu çipleri**, kanıt (0 = "eksik"), onaylı ülke sayısı, kullanım, işlemler |
| 2 | Kapsama matrisi | Matris | İddia × ülke. Hücre: durum rozeti + sürüm + not ("Ürün ruhsatlı değil"). **"Ülke sürümü aç"** eylemi |
| 3 | Yeni çekirdek iddia | Tam sayfa | Kimlik (kod ürüne göre önerilir, sürüm **1.0**), çekirdek metin (**en**), geçerlilik alanı (ürün, kitle, ülke), niteleyiciler, **kanıtlar**, **onaya hazırlık** kontrol listesi, **global onay akışı önizlemesi** (Medikal → Hukuk → Ruhsat, kişi adlarıyla), ülke sürümlerinin ruhsat durumu (ürün ana kaydından) |
| 4 | Ülke sürümü açma | Tam sayfa | Çekirdek salt okunur. Ülke dilleri **ülke ana kaydından** (UZ = uz, ru). **Ruhsat sahibi ve ruhsat no** (ürün ana kaydından). Dil başına metin ve niteleyici. **Uyarlama tipi + nedeni** ("daraltılabilir ya da yumuşatılabilir, genişletilemez"). Geçerlilik, kitle (yalnız daraltma), yerel kanıt (çekirdek kanıtları miras), hazırlık, **yerel onay akışı** |
| 5 | Kanıt ekleme | Modal | 1. Belge seç (Belge Yönetimi, kapsam, sürüm, "süresi doluyor" uyarısı, "yeni belge yükle" bağlantısı). 2. **Sürüm sabitlenir**, kanıt tipi (KÜB/KT, Klinik çalışma, Literatür, İç veri, Ruhsat yazısı), bölüm/sayfa/tablo, **alıntılanan cümle**, **desteklediği ifade** (iddia metni üzerinde parça işaretleme) |
| 6 | Belge önizleme | Modal | Sayfa ve bölüm, **alıntı vurgusu**, "v5'te değişti", kanıt tipi, ülke/dil, görüntülenen sürüm, belge durumu, geçerlilik, "Belge Yönetimi'nde aç" |
| 7 | Yerel incelemede sürüm | Detay → Onay | **Turlar** (Tur 1, Tur 2…). Adım başına kişi, tarih, durum, yorum. **Satır içi Onayla / Reddet** (ret gerekçesi zorunlu). UZ = **Paralel**, TR = **Sıralı** |
| 8 | Çekirdek değişti | Detay → Sürüm | Çekirdek v1.0 → v2.0 **kelime düzeyi fark** + yan yana karşılaştırma. Ülke ve çekirdek sürüm geçmişi |
| 9 | Onaylı, kilitli | Detay → Genel | Kilit bandı ve **yeni sürüm aç** |
| 10 | Kanıt belgesi güncellendi | Detay → Kanıt | "Belge güncellendi: v5", süresi doluyor uyarısı |
| 11 | Nerede kullanılıyor | Detay → Kullanım | Ülke başına: içerik, içerik seti, etkileşim yolculuğu (tür, ad, dil, sürüm, durum). "Diğer ülkeler yetki alanınız dışında" |
| 12 | Boş liste | Liste | Boş kütüphane için ilk çekirdek iddiayı oluşturma çağrısı |
| 13 | Yetkisiz | Tam sayfa 403 | İskelet çizilmez (UAS-001) |

**Durum modeli (mockup):**
- Onaylı · İncelemede · Taslak · Gözden geçirilmeli · Süresi doluyor.
- Açılmadı: ya **gerekçeli kapalı** ("ruhsatlı değil") ya da henüz açılmamış.
- Kapsam dışı · Pasif / Arşiv.

**Roller (mockup):** Global ekip (çekirdek + tüm ülkeler) · Yerel ekip (yalnız kendi ülkesini düzenler) · Yerel onaylayıcı · Salt okuma (iç denetim) · Yetkisiz.

## 2. Mockup ↔ kararlarımız ↔ sistem: farklar

| # | Konu | Mockup | Kararımız / sistem gerçeği | Öneri |
|---|---|---|---|---|
| K1 | **Yerel onaylayıcılar** | Her ülkenin kendi Medikal/Hukuk/Ruhsat kişileri (UZ: Dilnoza, Aziz, Bekzod) | **Karar:** MLR yalnız TR'de, tüm ülkelere hizmet ediyor (WP-ORG-02). | Onay şablonu ülke başına ayrı olur, ama **adaylar bugün aynı TR pozisyonları**. İleride ülkede ekip kurulursa yalnız o ülkenin şablonu değişir. Mockup'taki kişi adları örnek. |
| K2 | **Paralel onay** | UZ/AZ paralel, TR sıralı | **Motor yalnız sıralı** (`WorkflowDefinitionRuntimePlan`) | **MVP: hepsi sıralı.** Paralel, MOD-0023 genişletmesi olarak ayrı iş. Tek ekip olduğumuz için bugün kazancı az. |
| K3 | **Ürünün ülke ruhsatı** (durum, ruhsat no, ruhsat sahibi) | Ürün ana kaydından otomatik | **Yok.** MOD-0290'da "MA / Registered Presentation" dilimi **açık** (`module-implementation-status.md`). LSKU yalnız `MarketCode` tutuyor. | **MVP:** Ülke sürümünde "açılmadı" nedeni **elle seçilir** (ruhsat yok / mevzuat / iş kararı); ruhsat no ve sahibi gösterilmez. MOD-0290 MA dilimi gelince otomatik olur. |
| K4 | **Ülke dilleri** | Ülke ana kaydından (UZ = uz, ru) | Ülke listesi yalnız kod ve ad dönüyor, **dil bilgisi yok** | Yeni referans seti **`country-content-languages`** (MOD-0048, tenant). Kaynağı tek bu set. |
| K5 | **Ülke bazlı yetki** (yerel ekip yalnız kendi ülkesi) | Var | RBAC'ta **veri kapsamı yok**; izinler ülke bilmiyor | Kullanıcının ülkeleri **pozisyon ataması → org birimi → tüzel kişilik → ülke** zincirinden türetilir. İkinci seçenek: iddia için ayrı "ülke yetkisi" listesi. Karar gerekiyor. |
| K6 | **Sorumlu ekip** ("Gastro İş Birimi") | Metin | Org birimi var (`OrganizationUnit`) | Org biriminden seçilir. Görünürlük sınırı değildir. |
| K7 | **"Desteklediği ifade"** (iddia metninden parça işaretleme) | Var | MOD-0031 şartnamesinde yok | Kanıt bağına `SupportedSpan` (metin + dil + başlangıç/bitiş) eklenir. İşaretleme arayüzü FE işi. |
| K8 | **Önizlemede alıntı vurgusu** | Belge içinde vurgulu cümle | DocMgmt önizlemesi güncel sürümü açıyor; sayfa/vurgu yok | **MVP:** sabit sürüm + sayfaya atlama + alıntının yanda gösterilmesi. Belge içi vurgu faz 2. |
| K9 | **Nerede kullanılıyor** | İçerik + içerik seti + yolculuk | Yalnız **İçerik Seti** iddia referansı taşıyor; Bilgi İçeriği ve Yolculuk taşımıyor | **MVP:** içerik seti (+ seti kullanan yolculuk varsa). Bilgi İçeriği ↔ iddia bağı ayrı karar. |
| K10 | **Turlar** (ret → yeniden gönder) | Tur 1 / Tur 2 | Her gönderim = yeni workflow instance | İddia sürümü **instance listesi** tutar; Onay sekmesi turları buradan çizer. |
| K11 | **Satır içi onay** | Detayda Onayla/Reddet | WorkCenterNext zaten onay görevini gösteriyor | İkisi de **aynı MOD-0023 uçlarını** kullanır. Detaydaki düğme yalnız atanan kişiye görünür. |
| K12 | **Sürüm biçimi** | 1.0 / 1.1 / 2.0 | Bugün serbest metin `ClaimVersion` | Çekirdek **ana sürüm** (1.0 → 2.0), ülke **ara sürüm** (1.0 → 1.1). Ülke sürümü bağlı olduğu çekirdek sürümünü (`cv`) tutar. |

## 3. Veri modeli önerisi (CRM, CAND-CAP-0011)

```
Claim (çekirdek ya da yalnız yerel)
  ClaimCode · ClaimName · Kind (core | local) · ProductRef (MDM) · ResponsibleOrgUnitId
  CoreVersion (1.0, 2.0 …) · CoreText (en) · Qualifiers[en] · Audiences[] · Status · Evidence → MOD-0031
  ReviewRounds[] { WorkflowInstanceId, SubmittedAt/By, Outcome, ClosedAt }
  (onaylı = kilitli; değişiklik = yeni çekirdek sürüm; eski sürüm tarihçede)

ClaimCountryVersion
  ClaimId · CountryCode · Version (1.0, 1.1 …) · BoundCoreVersion
  Texts[{lang, text}] · Qualifiers[{lang, text}]      (diller = country-content-languages)
  Adaptation { Type: verbatim | narrowed | softened, Reason }
  Audiences ⊆ Claim.Audiences · ValidFrom/To
  Status: draft | in-review | approved | review-required | expiring | not-opened(reason) | archived
  Evidence (yerel) → MOD-0031 · ReviewRounds[] (ülke şablonu)
```

**Kurallar (backend, fail-closed):**
- Ülke sürümü, çekirdeği onaylı değilse gönderilemez (yalnız yerel iddia hariç).
- Onaya göndermek için en az 1 kanıt gerekir (çekirdek + yerel toplamı).
- Kitle yalnız çekirdeğin kitlelerinden daraltılabilir.
- Çekirdeğin yeni sürümü onaylanınca bağlı ülke sürümleri **review-required** olur.
- Sabitlenen kanıt belgesinin yeni sürümü yürürlüğe girince ya da belge askıya alınınca ilgili sürüm **review-required** olur.
- Geçerlilik bitişi yaklaşınca **expiring** olur.
- İçerik seçiciler yalnız **o ülkede onaylı** sürümü sunar.

## 4. İş planı (WP'ler)

### Faz 0 — Karar sonuçları (kullanıcı, 2026-09-28)
- **D1 (K5) → Ülke kısıtı YOK (şimdilik).** Yetkisi olan herkes tüm ülkeleri düzenler. İK tarafı oturunca pozisyondan türetilecek. → **CL-BE-7 ertelendi.** Mockup'taki "yetki alanınız dışında" maskelemesi MVP'de yok.
- **D2 (K2) → MVP'de sıralı** (motor sınırı, tek ekip). CT varsayımı; kullanıcı itiraz etmedi.
- **D3 (K3) → elle, KARARLAŞTI.**
  - **Ülke ekseni:** Global referans seti **`COUNTRY_CODES`**. Bugün 6 değer var: TR, BY, UZ, TM, GE, AZ.
    - Liste koda gömülmez. Set genişledikçe matris ve seçenekler kendiliğinden büyür.
    - ⚠ Di10 tüzel kişiliklerinde bu setin dışında ülkeler de var: KZ, KG, MD, PL, RO, ES, UA, TJ, AL, XK, CH. Bu ülkeler iddia matrisine ancak `COUNTRY_CODES` genişletilince girer. Genişletme kullanıcı kararıdır.
  - **Hücre durumları:** Her iddia × ülke hücresi varsayılan olarak **"Açılmadı"** (gerekçesiz) başlar. Kullanıcı iki şeyden birini yapar:
    - **ülke sürümü açar**, ya da
    - **"Açılmayacak" olarak işaretler.** Bu durumda nedeni **tek seçimli select2** ile seçmek zorunludur: *Ruhsat yok* · *Mevzuat izin vermiyor* · *İş kararı*.
    - Neden değerleri yeni referans setinden gelir: `claim-country-closure-reason` (MOD-0048, 7 dil etiketi). Koda gömülmez.
  - **Geri alınabilir:** Kapalı bir hücre sonradan açılabilir (ör. ruhsat alındı). Kapatma ve açma kim/ne zaman/neden bilgisiyle tarihçeye yazılır.
  - **Matris gösterimi:** Kapalı hücre "Açılmadı · {neden}" olarak görünür.
  - Ruhsat no ve ruhsat sahibi MVP'de gösterilmez. Bunlar MOD-0290 MA dilimi gelince eklenir.
- **D4 (K9) → Bilgi İçeriği ↔ iddia bağı KAPSAMDA.** CL-BE-6 büyür: Bilgi İçeriği onaylı iddia referansı taşır, Kullanım sekmesi İçerik + İçerik Seti + Yolculuk gösterir. İleride "yalnız o ülkede onaylı iddia içeren içerik ziyarette gösterilir" kuralının zemini.
- **D5 → MOD-0031 ortak kanıt servisi.**

### Faz 0 — Kararlar (ilk soru listesi)
- **D1 (K5):** Ülke yetkisi pozisyondan türetilsin mi, yoksa ayrı liste mi tutulsun?
- **D2 (K2):** MVP'de her ülke sıralı onayla başlayabilir mi?
- **D3 (K3):** Ruhsat bilgisi MVP'de elle ("açılmadı nedeni") girilsin mi?
- **D4 (K9):** Bilgi İçeriği ↔ iddia bağı bu kapsamda mı, sonraya mı?
- **D5:** Kanıt bağının sahibi MOD-0031 mi? (önceki sorudan açık)

### Faz 1 — Temeller (paralel)
| WP | Kapsam | Servis |
|---|---|---|
| **CL-BE-1** | İddia modeli v2: çekirdek + ülke sürümü, durumlar, sürümleme, çekirdek değişince ülke sürümlerini "gözden geçirilmeli" yapma, "açılmadı" nedeni, kitle daraltma kuralı, mevcut iddialara geçiş (test verisi) | CRM |
| **CL-BE-2 = B1** | MOD-0031 en küçük hali: kanıt bağı (sabit sürüm, tip, konum bilgisi, alıntı, `SupportedSpan`), belge üzerinden ters sorgu, bağ olayları | Platform |
| **CL-BE-3 = A1** | Workflow'un başka servislerce kullanılması: tamamlanma olayı, görüntü bağlamı (WorkCenterNext başlığı ve derin link), iptal edilmiş atama ve pozisyon durumu düzeltmesi, toplu durum okuma, yetki anahtarlarının seed'i | Platform |
| **CL-REF-1** | Referans setleri: `country-content-languages` (COUNTRY_CODES → diller), `claim-country-closure-reason` (ruhsat yok / mevzuat / iş kararı), `claim-adaptation-type`, `evidence-type`. Ülke ekseni `COUNTRY_CODES` (Global) | MOD-0048 (veri) |

### Faz 2 — Entegrasyon
| WP | Kapsam |
|---|---|
| **CL-BE-4** | İddia ↔ workflow: onaya gönder/geri çek, tur listesi, sonuç olayı tüketicisi (CRM'e ilk olay tüketicisi), uzlaştırma süpürgesi, ülke şablonu seçimi, onay geçmişi okuması |
| **CL-BE-5** | İddia ↔ kanıt: MOD-0031 proxy, "en az 1 kanıt" kuralı, belge sürümü/askı olayı ile "gözden geçirilmeli", süresi doluyor taraması |
| **CL-BE-6** | Kullanım okuma modeli: İçerik Seti iddia referansları (+ D4 kararına göre Bilgi İçeriği ve Yolculuk). Ülke yetkisine göre maskeleme |
| **CL-BE-7** | Ülke yetkisi (D1): kullanıcının ülke kümesi, yazma kapısı, listede maskeleme |

### Faz 3 — Arayüz (mockup ekranları)
| WP | Ekran |
|---|---|
| **CL-FE-1** | Liste: filtreler (ürün, ülke, dil, durum, kitle, tür, "kanıt eksik", "gözden geçirilmeli", "süresi doluyor"), ülke çipleri, hızlı görünüm paneli, dışa aktar |
| **CL-FE-2** | Kapsama matrisi + "Ülke sürümü aç" |
| **CL-FE-3** | Çekirdek oluştur/düzenle: kimlik, metin, geçerlilik alanı, niteleyiciler, kanıt listesi, **onaya hazırlık**, akış önizlemesi, ülke ruhsat özeti |
| **CL-FE-4** | Ülke sürümü oluştur/düzenle: salt okunur çekirdek, dil sekmeleri, uyarlama tipi ve nedeni, kitle daraltma, miras + yerel kanıt, yerel akış |
| **CL-FE-5** | Kanıt modalı (belge seç → sürüm sabitle → konum bilgisi, alıntı, desteklediği ifade işaretleme) + **Kanıt Paneli** (MOD-0031 gömülebilir) + önizleme modalı |
| **CL-FE-6** | Detay sekmeleri: Genel (kilit bandı, yeni sürüm) · Kanıt · **Onay (turlar, satır içi Onayla/Reddet)** · Sürüm (kelime farkı + yan yana) · Kullanım |
| **CL-FE-7** | Yetki durumları (403 iskeletsiz, salt okuma, ülke dışı maskeleme), boş durumlar, **7 dil** |

### Faz 4 — Konfigürasyon + canlı
- **A2:** Workflow şablonları `CLAIM-CORE-MLR` ve `CLAIM-LOCAL-MLR-{TR, UZ, AZ…}`. Adaylar WP-ORG-02 pozisyonları; SLA ve eskalasyon. Kullanıcı yapar, CT hazırlar.
- **Yetki tanımları:** "İddia Yazarı", "İddia İnceleyici", "İddia Okuyucu" rolleri + DocMgmt kanıt klasörü erişimi.
- **Canlı E4:** mockup'ın 13 senaryosu, TUTUKON ve ALMIBA ile. sema pullukcu onaylar, Admin gönderir.

**Bağımlılık:** Faz 0 → (CL-BE-1 ∥ CL-BE-2 ∥ CL-BE-3 ∥ CL-REF-1) → (CL-BE-4 ∥ CL-BE-5 ∥ CL-BE-6 ∥ CL-BE-7) → FE-1…7 (FE-1/2/3 BE-1'den sonra başlayabilir) → Faz 4.

## 5. MVP dışı (sonraya)
- Paralel onay.
- Belge içinde alıntı vurgusu.
- Ruhsat ve ruhsat sahibinin ürün ana kaydından otomatik gelmesi (MOD-0290 MA dilimine bağlı).
- E-imza.
- Bilgi İçeriği ↔ iddia bağı (D4'e göre).
- Global çekirdekten çoklu ülkeye toplu sürüm açma.
