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
