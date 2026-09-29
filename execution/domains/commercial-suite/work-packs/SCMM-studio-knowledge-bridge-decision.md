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
