# Karar Kaydı — Studio (İçerik Seti) ↔ Knowledge (Yol / Yolculuk / Ziyaret) köprüsü + İçerik Kapsamı sözlük uyumu

> **Durum:** KARAR (kullanıcı, 2026-09-28) · **Sahip:** CT · **Uygulama:** İddialar v2 arayüz paketlerinden (CL-FE) SONRA planlanacak.
> **Bağlam:** E2E manuel test sırasında (İçerik Kapsamı sayfası) kullanıcı "bu sayfaların Knowledge / Knowledge Path ile bağlantısı yok mu?" diye sordu. CT kod analizi aşağıda.

## 1. Tespit (CT kod okuması)
| Bağlantı | Durum | Kanıt |
|---|---|---|
| İçerik Seti → Bilgi İçeriği (bileşen) | ✅ | `ContentSet.SelectedComponents[].KnowledgeContentId`, `Arrangement.TemplateStepId/BranchId` |
| İçerik Seti → Zincir Şablonu → Konu | ✅ | `ContentSetTemplateRef.ConceptChainTemplateId` |
| İçerik Seti → İçerik Kapsamı | ✅ | `ContentSetScopeRef.ContentScopeId` (sürüm sabit) |
| Bilgi İçeriği ↔ İddia | ✅ (arka uç, BE-6) | `KnowledgeContent.ClaimRefs` + yayın kapısı |
| Bilgi Yolu adımı → Bilgi İçeriği (+ ops. kavram düğümü) | ✅ | `KnowledgePathStep.ContentId`, `ConceptNodeId` |
| Yolculuk aşaması → Bilgi Yolu | ✅ | `ContentEngagementJourneyStage.RecommendedKnowledgePathId` |
| **İçerik Seti → Bilgi Yolu / Yolculuk / Ziyaret** | ❌ | Hiçbir referans yok. **Studio çıktısı sahaya ulaşmıyor.** |
| **Bilgi Yolu / Yolculuk → Zincir Şablonu** | ❌ | Zincir editöründeki "Bağlantılar → Çıktılar" paneli bu yüzden "henüz kaynak yok" gösteriyor. |
| **Ziyaret içeriği ← Bilgi Yolu adımları** | ❌ | `VisitContentSequenceResolver` aşamayı ilerletiyor, içerikleri ise yolculuk bağlamından (konu / başlık / kitle / dil) topluyor. **Yol adımlarındaki içerikleri kullanmıyor.** |
| **İçerik Kapsamı → Ürün / Kitle / Pazar kayıtları** | ❌ | `ContentScope.ProductRefs/MarketRefs/AudienceRefs` serbest metin. MDM ürünü, AudienceProfile ve COUNTRY_CODES ile bağ yok. |

## 2. Kullanıcı kararı (2026-09-28)
**A + D birlikte** (CT yorumu; kullanıcının iki seçeneği de "olsun" diye işaretlediği mesajdan).

- **A. Yayınlanan İçerik Seti → "birleştirilmiş sunum" Bilgi İçeriği.**
  - Set yayınlanınca (SCMM-17 release) `ContentType = assembled-presentation` türünde bir KnowledgeContent oluşur.
  - Konu (zincir şablonunun konusu), ürün, dil ve kitle kapsamdan alınır.
  - Setteki iddialar `ClaimRefs`'e **otomatik** yazılır, ülke sürümü kapsamın pazarından çözülür.
  - BE-6 yayın kapısı otomatik işler: yalnız o ülkede onaylı iddia.
- **D. Yayınlanan İçerik Seti → Bilgi Yolu (zincir sırasıyla).**
  - Aynı yayında bir KnowledgePath oluşur (ya da yeni sürümü açılır).
  - Adımlar **zincir şablonunun omurga/dal sırasını** izler; her adım = o zincir adımına yerleştirilmiş bileşen içeriği + adım tipinin kavram düğümü.
  - Böylece "önce ihtiyaç, sonra fayda, sonra bileşen" anlatısı korunur.
  - Yolculuk aşaması bu yolu kullanır. Zincir editöründeki "Çıktılar" paneli gerçek veriyle dolar.
- **D'nin tamamlayıcısı (kullanıcı "anlatı sırası korunsun" dedi):** Ziyaret içerik sırası **yol adımlarını** kullanmalı; bugün yolculuk bağlamından topluyor. Bu, ziyaret çözücüsünde (`VisitContentSequenceResolver`) ayrı bir değişiklik.
  - ⚠ Kapsama dahil olup olmadığını kullanıcı açıkça teyit etmedi. Paketlemeden önce sorulacak.

## 3. "Unutmayalım" listesi (sonradan düzeltilecek)
1. **Studio → saha köprüsü** (yukarıdaki A + D).
2. **İçerik Kapsamı sözlük uyumu:** ürün → MDM global ürün seçici; pazar → `COUNTRY_CODES` seçici; kitle → Knowledge AudienceProfile seçici. İddialar v2 ile aynı desen, serbest metin kalkar. Mevcut kayıtlar için geçiş: eşleşen değerler dönüştürülür, eşleşmeyenler raporlanır.
3. **Ziyaretin yol adımlarını kullanması** (D tamamlayıcısı, teyit bekliyor).

## 4. Sıralama
İddialar v2 arayüzü (CL-FE-1…7) → **SB-1** (İçerik Kapsamı seçiciler) → **SB-2** (set yayını: A birleştirilmiş içerik + D yol üretimi) → **SB-3** (ziyaret yol adımlarını kullanır, teyit sonrası) → E2E canlı (TUTUKON / ALMIBA: set → içerik + yol → yolculuk → play → ziyaret).

---

## 5. Yeniden analiz (2026-09-29, CT: kod + canlı veri, 97c5)

### 5.1 Kod — önceki tespitler geçerli, iki yeni ağır bulgu
- **Set yayını hiçbir şey üretmiyor.** `ReleaseContentSetRevisionHandler` (`ContentSetRevisionCommandHandlers.cs:375-463`) yalnız `ReleaseState` yazıyor ve bir log denetim olayı atıyor. KnowledgeContent, KnowledgePath ya da domain olayı yok. `assembled-presentation` içerik tipi yok (`KnowledgeContent.cs:211-224`). SB-1/2/3 için kod da WP de yok.
- **YENİ — ziyaret içerikleri aşamaya göre değişmiyor.** `VisitContentSequenceResolver`:
  - aşamayı `önceki StageIndex + 1` ile seçiyor (`:106`), ama içerikleri **yolculuk bağlamından** (Subject / Topic / Audience / Language) topluyor (`:215-222`).
  - Aşamanın yolunu ve yol adımlarını hiç okumuyor. Her aşamada aynı içerik havuzu çıkıyor; `Min/MaxVisitNumber` ve `Repeatable` okunmuyor.
- **YENİ — çözücü içerik listesi değil, yalnız sayı döndürüyor.** Promo / non-promo sayısı (play ürün hattı ↔ `content.ProductId`) + CycleCapacity süresi (`:189-261`). Temsilcinin göreceği içerik kimlikleri çıkmıyor.
- **Play'deki `knowledge-path` bağları ziyarette yok sayılıyor.** Yalnız ilk `content-engagement-journey` bağı kullanılıyor (`:67-73`).
- **Zincir editörü "Çıktılar" paneli sabit "—"** (`template-form.js:521-525`).
- **İçerik Kapsamı ref'leri serbest metin** (`tags: true`, doğrulayıcı yok).

### 5.2 Canlı veri (97c5)
| Kayıt | Durum |
|---|---|
| İçerik Seti | **0** |
| İçerik Kapsamı | 1 ("test", ürün serbest metin "Almiba", pazar / kitle boş) |
| Zincir şablonu | TPL-ALMIBA-01 **taslak**; TPL-TUTUKON-01 yayında (+ taslak sürüm) |
| ALMIBA Bilgi Yolları | KP-2026-2138F2 "HD Karnitin Detay Yolu" yayında, **tr** ama 4 adım içeriğinin 4'ü de **en**. KP-114 / KP-201 / KP-888 ALMIBA konusu altında demo kalıntıları (Kardiyometabolik, Titrasyon, 2024 Kampanya). |
| ALMIBA Yolculukları | CEJ-27 yayında ama API'de **0 aşama**; CEJ-40 taslak; CEJ-ALMIBA-HD taslak (tr, AUDP-001), **0 aşama** |
| ALMIBA içerikleri | 4 eski (en, yayında) + KC-2026-6CD926 (tr, yayında, CLM-ALMIBA-02 TR bağlı) |
| Play'ler | STR-TUTUKON-URO aktif, bağı **silinmiş bir yol** (KP-2026-49DE5D, durum null). "test" (taslak) KP-114/KP-201 yollarına bağlı. **Hiçbir play yolculuğa bağlı değil → her ziyaret `NoJourney`.** |

### 5.3 Kullanıcıya sorulan kararlar
- SB-3 kapsamı: ziyaret içeriği = aşamanın yol adımları; çıktı içerik listesi mi olacak?
- Play'deki yol bağı ziyarette tek aşamalı yolculuk gibi mi sayılacak?
- Sıra: SB-3 → SB-2 → SB-1 mi?

---

## 6. İçerik Setleri — açık işler listesi (2026-09-30, kullanıcı: "unutma, not al")
| # | Açık | Durum / paket |
|---|---|---|
| 1 | Set yayını sahaya inmiyor (içerik + yol üretimi) | **SB-2** sürüyor. Kararlar: dal-öncelikli sıra, karışık dil 409. |
| 2 | Revizyon yaşam döngüsünün (gönder / karar / render / yayın / geri çek) **Web ekranı yok** | **SB-UI**, mockup sonrası (brief: `mockups/content-studio-v2/BRIEF-content-studio-v2.md`) |
| 3 | `crm.content-set.release` / `withdraw` yetkileri rollere verilmedi | SB-UI ile birlikte grant script |
| 4 | Set onayı tek kişilik; MLR (Medikal → Hukuk → Ruhsat) + Görev Merkezi olmalı | Stüdyo v2 fazı (iddialardaki MOD-0023 deseni) |
| 5 | Render = özet PDF (döküm), sunum değil | **SB-4**: HTML saha sunumu + aynı kaynaktan arşiv PDF'i |
| 6 | Sayfa tasarımcısı (sürükle-bırak), marka kiti (ürün), ülke yasal blokları, onaylı görsel kütüphanesi, kilitli iddia blokları, uyum kontrolü | Stüdyo v2 (mockup sonrası fazlara bölünecek) |
| 7 | Sahadan sayfa gösterim takibi | Stüdyo v2 son faz + mobil |
| 8 | Zincir editörü "Çıktılar" paneli boş | **SB-2b** (SB-2'nin geri izini okur) |
| 9 | İçerik Kapsamı serbest metin | **SB-1R: kapsam KALDIRILIYOR, bağlam setten türetilir (§7)** · **CLN-1 ile kaldırıldı** (2026-10-01): kapsam / set / revizyon kodu CRM'den silindi; 97c5'te 1 kapsam belgesi kaldı (okuyucusu yok, CLN-2) |
| 10 | Uygunluk kontrolü fiilen hep "Belirsiz": iddialarda `Applicability.EligibilityPolicyId` İddialar v2 arayüzünde girilemiyor; kapsam değerleri serbest metin | SB-1R ile birlikte; Uygunluk Politikaları ayrıca gözden geçirilecek |
| 11 | Karar bekleyen: play'deki doğrudan Bilgi Yolu bağı ziyarette tek aşamalı yolculuk sayılsın mı? | SB-3 öncesi kullanıcıdan |
| 11b | **SB-2 yeniden yayın riski:** set yeniden yayınlanınca eski yol `inactive` olur. Sürüme sabitlenmiş (`PathVersionPinPolicy`) bir yolculuk aşaması pasif yola bakar; yanıtta `previous_path_in_use` uyarısı var. **Öneri: aşamalar varsayılan olarak "yolun en son yayındaki sürümünü izle" çalışsın.** | **SB-3'te ele alınacak.** Kullanıcı (2026-09-30): ziyaret tarafına gelince yeniden değerlendirilecek. |
| 12 | Veri: ALMIBA zinciri taslak; ALMIBA yolu dil karışık; yolculuklar aşamasız; KP-114 / 201 / 888 demo kalıntısı | E2E sırasında |

---

## 7. KARAR — İçerik Kapsamı kaldırılıyor; set bağlamı türetilir (kullanıcı, 2026-09-30)
**Gerekçe:**
- Ürün, kitle ve dil zaten yapılandırılmış olarak Bilgi Bankası'nda (konu → MDM ürünü, hedef kitle profili, dil), zincir şablonunda ("Kimin için") ve iddialarda var. İçerik Kapsamı aynı bilgiyi serbest metinle tekrar ediyordu.
- "Bir kez tanımla, çok yerde kullan" faydası küçük. Değişmezliği revizyon dondurması zaten sağlıyor.
- Göç maliyeti yok: canlıda 1 "test" kapsamı, 0 set.
- **SCMM-14 tasarımından (D14-a, kapsam = ayrı kayıt) bilinçli sapma.**

**Yeni model — set bağlamı:**
| Alan | Kaynak |
|---|---|
| Ürün | zincir şablonunun konusu → MDM Global Product (salt okunur, otomatik) |
| Kitle | zincir şablonunun "Kimin için" hedef kitle profili; bileşen kitleleri uyumlu olmalı |
| Dil | bileşenlerin dili (set tek dil; SB-2 kuralı `component_language_mixed`) |
| **Ülke** | **setin üstünde tek seçimli alan (COUNTRY_CODES).** Dil o ülkenin `country-content-languages` dillerinden biri olmalı. İddia ülke sürümü, yasal altbilgi (Stüdyo v2) ve kullanım raporu buradan okur. |
| Kanal / dönem | şimdilik yok (gerekirse setin üstünde) |

**Paket: SB-1R** (eski "SB-1 seçiciler" yerine), **SB-2 birleştikten sonra**:
- `ContentSet.Scope` → `CountryCode` (+ türetilmiş bağlam okuması);
- uygunluk kontrolü bağlamı setten kurar;
- iddia kullanım raporu ülkeyi setten okur;
- SB-2'nin "kapsam MarketRefs" okuması tek noktada setin ülkesine döner;
- İçerik Kapsamları sayfası, menüsü ve Web proxy'si kaldırılır; CRM tarafı önce salt okunur, sonra silinir;
- 7 dil L10n temizliği;
- canlıdaki "test" kapsamı arşivlenir.

**Açık:** Uygunluk Politikaları sayfası ayrıca gözden geçirilecek (iddialara politika girilemediği için kontrol fiilen hep "Belirsiz").

> **Durum (2026-09-30):** SB-1R yapıldı (`9df79043`). §8 kararıyla setin kendisi de emekliye ayrılıyor; SB-1R'nin bağlam mantığı yola taşınır.

---

## 8. KARAR — İçerik Seti kaldırılıyor; kurgu + iddia + MLR onayı Bilgi Yolu'na taşınıyor (kullanıcı, 2026-09-30)
**Bağlam:**
- Kullanıcı "set ile yol arasındaki fark"ı sordu. İkisinde de sıralama var; setten üretilen yol, setin sırasının kopyası. İki yerde düzenlenebilir sıra → kopma riski.
- CT üç seçenek sundu: (1) setten gelen yol kilitli, (2) birleştir, (3) olduğu gibi. CT (1)'i önerdi. **Kullanıcı (2)'yi seçti.**

**Kararlar:**
1. **İçerik Seti kavramı kalkar.** Bilgi Yolu tek kayıt olur: zincir şablonu + adımlara yerleşen içerikler + **iddialar** + bağlam (ülke, dil; ürün ve kitle zincirden) + revizyon + çıktı (PDF; sonra HTML) + yayın / geri çekme.
2. **Tüm yollar MLR'li (yol türü AYRIMI YOK).** Her yol bir zincir şablonuna bağlanır ve Medikal → Hukuk → Ruhsat onayından geçer (MOD-0023 + Görev Merkezi, iddialardaki desen). Eğitim yolları dahil.
3. **SB-2 emekliye ayrılır; mantığı yolun yayınına taşınır:**
   - dal-öncelikli sıra;
   - tek dil (`component_language_mixed` / `component_language_mismatch`);
   - bileşenler yayında;
   - iddia kullanılabilir (BE-6 kodları);
   - ülke sürümü seçimi;
   - "birleştirilmiş sunum" içeriği → yolun çıktısı.
   - Setten üretim kodu kaldırılır.
4. **SB-1R'nin bağlam mantığı** (ülke / dil sette, ürün / kitle zincirden, `ContentSetContextResolver`, `context_locked`) yola taşınır.
5. **SCMM-15/16/17 (set revizyonu, render, yayın)** yol revizyonuna bağlanır. Tek kişilik inceleme MLR iş akışıyla değişir (açık işler #4 kapanır).
6. **İçerik Setleri sayfası, menüsü ve yazma uçları kalkar** (kapsamdaki gibi: önce salt okunur, sonra silinir). Canlıda 0 set, göç yok.
7. **Mockup brief'i** (`mockups/content-studio-v2/BRIEF-content-studio-v2.md`) "Bilgi Yolu" sayfası için güncellenir.

**Sonuçlar:**
- **Ziyaret sadeleşir (SB-3):** aşama → onaylı yol → adımlar. Ara üretim adımı yok.
- **Mevcut yollar** (KP-114, KP-201, KP-2026-2138F2 yayında; KP-888 taslak) zincirsiz ve MLR'siz. Göç kuralı paketlemede kullanıcıya sorulacak. Öneri: okunur kalsın, "onaysız eski yol" işareti taşısın, ziyarette kullanılmadan önce MLR'den geçsin.
- **Journey stage** yayındaki (= MLR onaylı) yolu gösterir. 11b (sürüm sabitleme riski) SB-3'te aynen değerlendirilecek.

**Sıradaki adım:** Tasarım brief'i `DESIGN-KP-STUDIO` (DESIGN-SCMM-14 formatında):
- model;
- revizyon ve onay;
- yayın kuralları;
- göç;
- emeklilik sırası;
- paket bölünmesi.

Kullanıcı onayıyla paketlere dönüşecek.

---

## 9. KARAR — Ziyaret içeriği (SB-3 ön kararları; kullanıcı, 2026-10-01)
1. **Q2 / 11b — sürüm politikası:** yolculuk aşamaları varsayılan olarak **"yolun en son yayındaki sürümünü izle"**. Yeni sürüm yayınlanınca saha otomatik güncellenir. Belirli sürüme sabitleme istisnai seçenek olarak kalır.
2. **Q1 — doğrudan yol bağı: HAYIR.** Akış şöyle:
   - zincir şablonu konuları sıralar ("önce ihtiyaç, sonra etki / fayda…");
   - Bilgi Yolu bu zincire içerik + iddia + sayfa tasarımı ile kurgulanır (ürün × kitle × ülke / dil);
   - bu yollar **İçerik Etkileşim Yolculukları**'nda (her ürün ve kitle için) ziyaret sırasına dizilir;
   - ziyarete **yolculuk sırasıyla** gelir.
   - **Strateji şablonu içeriği yolculuk üzerinden bağlar.** Şablonda doğrudan Bilgi Yolu bağı ziyarette kullanılmaz; yeni bağ olarak önerilmemeli / kaldırılmalı.
3. **YENİ — çok ürünlü ziyaret:** bir ziyaret tek ürün üzerinden gitmez.
   - **En fazla 3 promo + en fazla 3 non-promo ürün** planlanır ve anlatılır.
   - Her ürün için o ürünün (ve doktorun kitlesinin) yolculuğundaki **sıradaki aşama** → yolu → içerikler.
   - **Aşama ilerlemesi doktor × ürün (yolculuk) bazında tutulmalı.** Bugün doktor başına tek `StageIndex` var.

**Sonuç:**
- SB-3 bugünkü tek yolculuk / tek aşama çözücüsünden büyük.
- Paketlemeden önce **DESIGN-SB-3** analizi gerekiyor:
  - ziyaretin promo / non-promo ürünleri nereden geliyor (strateji şablonu ürün hatları / SKU %, CycleCapacity, kampanya, mikro hedef);
  - ürün → yolculuk eşlemesi (konu ↔ ürün + kitle);
  - doktor × yolculuk aşama takibi (PlannedVisit / VisitReport);
  - 3 + 3 sınırının yeri;
  - eski sistemdeki (DitenCRM) karşılığı.
