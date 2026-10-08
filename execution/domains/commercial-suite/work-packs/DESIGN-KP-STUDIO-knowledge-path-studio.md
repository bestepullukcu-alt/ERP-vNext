# TASARIM BELGESİ — DESIGN-KP-STUDIO · Bilgi Yolu Stüdyosu (kurgu + iddia + MLR + yayın tek kayıtta)

> **CT (SoR), 2026-09-30.**
> - **Kararlar:** `SCMM-studio-knowledge-bridge-decision.md` §7 (kapsam kalktı) + §8 (set kalktı, her yol MLR'li).
> - **Mockup:** `mockups/kp-studio/` (analiz: `KP-STUDIO-mockup-analysis.md`; kullanıcı dört öneriyi kabul etti: K1–K4).
> - **Brief:** `mockups/content-studio-v2/BRIEF-content-studio-v2.md`.
> - **Modül:** CAND-CAP-0011 + MOD-0162 (Bilgi Yolu), CrmService. Çalışma dalı `test/crm-content-visit-e2e`.
> - Bu belge DESIGN-SCMM-14'ün **yerine geçer** (İçerik Seti tasarımı emekli).

## 1. Ne
**Bilgi Yolu**, ilaç şirketinin saha anlatısının tek kaydı olur. İçinde:
- zincir şablonuna göre dal ve adımlara yerleşen **içerikler** ve **iddialar**;
- yolun **bağlamı**: ülke + dil + ürün + kitle;
- **sayfalar** (tasarım);
- **revizyonlar** (dondurulmuş gönderimler) ve her revizyonun **MLR onayı** (Medikal → Hukuk → Ruhsat, MOD-0023 + Görev Merkezi);
- **çıktılar** (HTML saha sunumu + arşiv PDF'i);
- **yayın** ve **geri çekme**.

Yayındaki yol, yolculuk aşamasından **ziyarete** gider.

- **Tek kayıt, tek sıra:** Sıra yalnız yolda düzenlenir; set ↔ yol kopması yok.
- **Her yol MLR'li:** yol türü ayrımı yok (eğitim yolları dahil). Kural: **onaysız yol sahada kullanılmaz.**

## 2. Model

### 2.1 `KnowledgePath` (mevcut aggregate, genişler)
**Mevcut alanlar:**
- `PathCode`, `PathName`, `PathVersion`, `PathStatus` (draft / review / approved / published / inactive / archived);
- `SubjectId`, `TopicId?`, `AudienceProfileId?`, `LanguageCode?`;
- `EffectiveFrom/To`, `Steps[]`, `StepSetFrozenAt`, `PublishedAt/By`, `SupersedesPathId`.

**Yeni ve değişen alanlar:**

| Alan | Anlam | Kural |
|---|---|---|
| `ChainTemplate` `{ConceptChainTemplateId, ChainVersion}` | yolun iskeleti, sabitlenmiş | yeni yolda **zorunlu**; yoksa yol "onaysız eski yol" (§6) |
| `CountryCode` | yolun ülkesi (COUNTRY_CODES) | **K3:** yolun kimliği; oluşturulunca ve sürümler arasında **değişmez** |
| `LanguageCode` | yolun dili (ülkenin `country-content-languages` dillerinden) | **K3:** kimlik, değişmez; tek dil kuralı |
| Ürün, kitle | zincirden **türetilir** (konu → birincil `global-product`; ForWhom) | salt okunur; `SubjectId` = zincirin konusu (tutarlılık doğrulaması) |
| `Claims[]` `{ClaimId, ClaimCode, CountryVersionId?, Arrangement}` | adımlara yerleşen iddialar | ülke sürümü yolun ülkesinden çözülür (SB-2 kuralı) |
| `Pages[]` | sayfa tasarımı (§2.4) | KP-UI-3 fazında dolar; o zamana kadar boş |
| `Revisions` | ayrı aggregate (§2.3) | |

- **Bağlam çözümü:** SB-1R'nin `ContentSetContextResolver`'ı **`KnowledgePathContextResolver`** olarak taşınır.
- **Değişmezlik:** ülke ve dil sürümler arasında değişmez. Başka ülke için "Ülkeye uyarla" yeni bir `PathCode` üretir (sonraki faz).

### 2.2 Adım yerleşimi
- `KnowledgePathStep`'e `Arrangement {ChainStepId (= ConceptTypeId), BranchCode, Position}` eklenir.
- **Sıra hesaplanır:** `StepOrder` = SB-2'nin dal-öncelikli sırası (dal `SortOrder` → dalın adım listesi → `Position`). `ContentSetPathOrder` → `KnowledgePathOrder`.
- **Zincir uyumu (mockup düzeltmesi #2):**
  - Kurgu zincirin iskeletini **değiştirmez**. Dal ve adım eklenmez, çıkarılmaz, dallar arası taşınmaz.
  - Yalnız zincirin adımlarına içerik ve iddia yerleştirilir, sıralanır, adım ayarı verilir (zorunlu, süre, ön koşul).
  - Adımın en az / en çok öğe kuralı zincirden gelir.
- Mevcut adım alanları kalır: tip, zorunlu, ön koşul, kavram düğümü, süre, dal koşulları.

### 2.3 `KnowledgePathRevision` (yeni aggregate; SCMM-15/16/17 revizyon mantığı buraya taşınır)
- **Kimlik:** `RevisionNumber` (yol içinde artar), `PathId`, `PathVersion`.
- **Dondurulmuş anlık görüntü:** zincir ref, bağlam (ülke, dil, ürün, kitle), adımlar (+ içerik sürümleri), iddialar (+ ülke sürümü + sürüm), sayfalar, uyum sonucu.
- **İnceleme:**
  - `ReviewRounds[]`: MOD-0023 örnek kimliği, şablon kodu, adımlar, sonuç. `ClaimReviewRound` deseni.
  - `ReviewNotes[]`: sayfa / blok, konum, metin, yazar, **çözüldü**, **sonraki revizyona taşındı** (Öneri 3).
- **Çıktılar:** `RenderedArtifacts[] {kind: html | pdf, ContentId, Checksum, ByteSize, RenderedAt}`, aynı kaynaktan.
- **Yayın:** `ReleaseState {Released | Withdrawn, zaman, kişi, gerekçe}`.
- **Sürüm ↔ revizyon ilişkisi** (mockup'taki açıklama korunur):
  - bir **sürüm** (v1.0, v2.0) sahaya çıkan hattır;
  - bir sürümün taslağında birden çok **revizyon** gönderilebilir (Rev 1 reddedildi, Rev 2 onaylandı);
  - onaylı revizyon yayınlanınca o sürüm "yayında" olur.

### 2.4 Sayfalar (KP-UI-3 fazının iskeleti — bu belgede yalnız sözleşme)
- `PathPage {PageId, StepRef (ChainStepId + BranchCode), Order, LayoutTemplateId?, Blocks[]}`.
- `Block {BlockId, Kind (heading / text / image / chart / claim / references / safety / legal-footer / interaction), Frame, Props}`.
- **Kilitli bloklar:**
  - `claim`: metin iddianın ülke sürümünden, düzenlenemez;
  - `safety` (**K2:** ürün × ülke × dil onaylı güvenlilik metni);
  - `legal-footer` (ülke profili);
  - sayfa başına onay kodu.
- **Marka kiti** (ürün bazında), **ülke yasal profili**, **güvenlilik metni** ve **onaylı görsel kütüphanesi** ayrı ana veriler (KP-5).

## 3. Davranışlar

### 3.1 Kurgu (taslak)
- **Oluştur:** zincir + ülke + dil. Ürün ve kitle zincirden gelir. Kod `KP-…` (mockup düzeltmesi #5, `BY-` değil).
- **İçerik ekle:**
  - yayında olmalı (mevcut kural);
  - **yolun dilinde** olmalı (409 `component_language_mismatch`, SB-1R'den).
- **İddia ekle:**
  - ürün uyumu;
  - yolun ülkesinde sürümü olmalı. Yoksa eklenir ama **engelleyici** uyarı taşır (mockup'taki gibi).
  - Seçici "İddia arama" (Öneri 1): ülke ve dil filtreli.
- **Uygunluk kontrolü:** mevcut Eligibility portu; bağlam yoldan.

### 3.2 Onaya gönder (revizyon + MLR)
- **Ön kontrol listesi** (engelleyici varsa gönderim kapalı):
  - zincir uyumu;
  - tek dil;
  - içerikler yayında;
  - iddiaların ülke sürümü var;
  - uyum kontrolünde engelleyici yok.
- **Gönderim:**
  - revizyon dondurulur;
  - MOD-0023 örneği başlar: şablon `KP-MLR-{ÜLKE}`, Medikal → Hukuk → Ruhsat sıralı, pozisyon adayları. CFG script'i, CL-CFG-1 deseni.
  - Başlatma **kullanıcı token'ıyla** yapılır ve DisplayContext taşır: başlık `Bilgi yolu onayı · KP-… v2.0 · Rev 3`, bağlantı **inceleyici görünümü**.
- **Taslak kilitlenmez:** yeni revizyon için düzenleme sürer. Açık bir tur varken ikinci gönderim 409.
- **Gönderimde değişiklik özeti** (Öneri 6): önceki revizyonla fark otomatik not olarak eklenir.

### 3.3 İnceleme — **K1: tek kanal = iş akışı**
- **İnceleyici görünümü** (yol içinde): sayfalar + iğneli notlar + iddiaya tıklayınca kanıt paneli (MOD-0031, sabitlenmiş sürüm, alıntı).
- **Onayla / Reddet düğmeleri** aynı **MOD-0023 approve / reject** çağrısını yapar:
  - kullanıcı token'ıyla, **yorum ile**;
  - ret gerekçesi zorunlu.
- **Görev Merkezi** iş öğesi bu görünümü açar. İki kanal da tek iş akışına yazar.
- **Adım adı** CRM tarafında görünümde yazar. Bu, REQ-WCN-01 W-1 / W-2 eksiklerini yol için çözer; WCN talebi diğer modüller için geçerli kalır.
- **SoD kişi bazında** (mockup düzeltmesi #6): gönderen hiçbir MLR adımını onaylayamaz (MOD-0023 SoD + CRM ön kontrolü).
- **Sonuç:** tamamlanma olayı → CRM tüketicisi (inbox) → `ApplyReviewOutcome`. 120 sn sonra okumada uzlaşma yapılır (BE-4 deseni).
- **Doğrudan yayın ya da onay** → 409 `approval_via_workflow_only`.

### 3.4 Çıktı
- Onaylı revizyondan **render** yapılır.
- **İlk faz:** mevcut MigraDoc PDF (döküm) yola uyarlanır.
- **SB-4:** HTML paket + aynı HTML'den PDF.
- Aynı parmak izi gösterilir. Idempotent: yeniden render aynı ContentId. Depolama FU01 `ContentMessagingArtifacts`.

### 3.5 Yayın (SB-2 kurallarının yeni yeri)
**Ön koşullar** (fail-closed; 409 kodları SB-2 / BE-6'dan):
- revizyon **onaylı**;
- **çıktı hazır**;
- tüm içerikler yayında (`component_not_published`);
- tek dil (`component_language_mixed`);
- tüm iddialar kullanılabilir (`claim_not_approved`, `claim_language_mismatch`);
- **uyum temiz**;
- **yayınlayan ≠ gönderen** ve yayınlayan revizyonun MLR onaycılarından biri değil (SCMM-17 SoD'nin güçlendirilmiş hali).

**Etki:**
- yol sürümü `published`, adım seti dondurulur;
- aynı `PathCode`'un önceki yayındaki sürümü `inactive` olur (yerini aldı; SB-2'nin seçimi).
- Önceki sürümü sürüme sabitlenmiş bir yolculuk aşaması kullanıyorsa uyarı `previous_path_in_use` (11b → SB-3).
- **"Birleştirilmiş sunum" ayrı bir Bilgi İçeriği olarak ÜRETİLMEZ.** Sunum, yolun yayındaki revizyonunun çıktısıdır. SB-2'nin A çıktısı emekli.

### 3.6 Geri çekme
- Gerekçe zorunlu. Yol `inactive` olur.
- Yayınlanmış bir yolculuk aşaması kullanıyorsa yola dokunulmaz ve `path_in_use` döner (SB-2 kuralı).

### 3.7 Yeni sürüm
- Yalnız yayındaki sürümden açılır (mevcut V-P14).
- Ülke ve dil kopyalanır, **değişmez** (K3). Zincir yeni sürüme yükseltilebilir (adımlar yeniden eşlenir, uyumsuzluk raporlanır).

## 4. Kararlar (kilit)
| # | Karar | Kaynak |
|---|---|---|
| D-KP-1 | İçerik Seti yok; kurgu + iddia + MLR + yayın Bilgi Yolu'nda | §8 |
| D-KP-2 | Her yol zincire bağlı ve MLR'li; yol türü yok | §8 |
| D-KP-3 | Ülke + dil yolun kimliği, sürümler arası değişmez; başka ülke = uyarlama ile yeni yol | K3 |
| D-KP-4 | Onay tek kanal: iş akışı. Yol içindeki inceleyici görünümü ve Görev Merkezi aynı MOD-0023 çağrısı; yorum + ret gerekçesi | K1 |
| D-KP-5 | Güvenlilik bilgisi iddia değil; ürün × ülke × dil onaylı güvenlilik metni, kilitli blok | K2 |
| D-KP-6 | Eski yol sihirbazındaki otomatik iddia eşleştirmesi yalnız öneri; her biri kullanıcı onayıyla | K4 |
| D-KP-7 | Kurgu zincirin iskeletini değiştirmez | mockup düzeltmesi #2 |
| D-KP-8 | SoD kişi bazında; yayınlayan ≠ gönderen ve ≠ MLR onaycısı | #6 + SCMM-17 |
| D-KP-9 | Birleştirilmiş sunum = yolun çıktısı (ayrı Bilgi İçeriği yok) | §8 madde 3 |
| D-KP-10 | Tenant sahipliği, ürün bazında marka kiti, ülke bazında yasal blok; legal entity yalnız veri | brief §8 |

## 5. Ziyarete etki (SB-3 ile)
- Aşama → yayındaki (= MLR onaylı) yol → **adımlar sırasıyla** → içerik listesi.
- Her içeriğin iddiaları ve çıktının sayfa aralığı da ziyarete taşınır.
- Promo / non-promo sayısı ve süre bu listeden hesaplanır (CycleCapacity).
- **Onaysız eski yol ziyarette kullanılmaz.**
- Açık kararlar SB-3'te: 11b (aşama varsayılanı "en son yayındaki sürümü izle"), Q2 (play'deki doğrudan yol bağı).
- Sahada sayfa gösterim takibi sonraki faz (mobil + VisitReport).

## 6. Göç
**Mevcut yollar** (KP-114, KP-201, KP-2026-2138F2 yayında; KP-888 taslak):
- `ChainTemplate` yok → **"onaysız eski yol"** olarak türetilir;
- listede görünür, okunur;
- ziyarette kullanılmaz;
- düzenlenmek istenirse **eski yol sihirbazı** çalışır:
  1. zincir seç;
  2. adımları zincir adımlarına eşle;
  3. iddia önerilerini tek tek onayla;
  4. ülke / dil seç, onaya gönder.
- Veri yazılmaz: alanlar boş kalır, türetilmiş bir işaret kullanılır.

**Canlı içerikler:**
- CEJ-27 yayında ama aşamasız; CEJ-ALMIBA-HD taslak. Göç engeli yok.
- ALMIBA zinciri (TPL-ALMIBA-01) taslak; yol kurmadan önce yayınlanmalı.
- İçerik Seti **0**, kapsam 1 ("test").

## 7. Emeklilik (sıra)
1. KP-1…KP-3 yeşil ve birleşmiş olmadan hiçbir şey kaldırılmaz.
2. **KP-4:**
   - SB-2 üreticisi (`ContentSetReleaseProducer`, `ContentSetPathOrder`) kaldırılır. Mantık zaten KP-1 / KP-3'e taşınmış olur.
   - İçerik Seti yazma uçları + komutlar kaldırılır.
   - `ContentSetRevision` uçları salt okunur.
   - Web İçerik Setleri sayfaları, JS, resx, proxy ve manifest `CONTENT_SETS` sayfası kaldırılır.
   - Kapsam okuma uçları + repository kaldırılır (SB-1R'den kalan).
   - Auth anahtarları `crm.content-set.*` ve `crm.content-scope.*` silinmez, "deprecated" yapılır.
3. Sonraki temizlik: salt okunur set / kapsam repository'leri ve class-map `LegacyScope` (veri 0 olduğu doğrulanınca).
   - **CLN-1 ile kaldırıldı** (2026-10-01): set 0 / revizyon 0 / kapsam 1 doğrulandı; iki controller, iki feature, üç entity + `ContentSetScopeRef`, üç repository, DI, class-map'ler (`LegacyScope` dahil) ve index oluşturma kodu silindi. `ChainContextErrors` ayrı dosyaya taşındı; `KnowledgeStudioOrigin` ve `KnowledgeContent.ContentSetId` korundu. Koleksiyon / index / veri dokunulmadı (CLN-2).

## 8. Paketler ve bağımlılıklar
| Paket | Kapsam | Katman | Bağımlı |
|---|---|---|---|
| **KP-1 Model** | zincir ref, ülke / dil (kimlik), türetilmiş bağlam (resolver taşınır), adım yerleşimi + dal-öncelikli sıra (taşınır), yolda iddialar, tek dil, eski yol işareti, zincir uyumu; API | CRM | — |
| **KP-2 Revizyon + MLR** | `KnowledgePathRevision`, gönder / geri al, MOD-0023 (`KP-MLR-{CC}`), inceleyici karar ucu (yorumlu, K1), notlar, sonuç tüketicisi + uzlaşma, doğrudan yayın 409, SoD; **CFG:** şablonlar + grant script | CRM + CFG | KP-1 |
| **KP-3 Çıktı + yayın** | render (PDF uyarlaması), yayın ön koşulları (SB-2 kuralları), sürüm supersede, geri çekme, `path_in_use` | CRM | KP-2 |
| **KP-4 Emeklilik** | set, kapsam ve SB-2 kaldırma (§7) | CRM + Web + Platform manifest + Auth açıklama | KP-3 |
| **KP-UI-1** | liste (eski yol işareti, filtreler), çalışma alanı kabuğu (tek durum çizgisi, 8 sekme), kurgu (dal sütunları, adıma ekle + gerekçeli "Eklenemez", iddia arama), bağlam | Web | KP-1 |
| **KP-UI-2** | uyum (şimdilik kurgu kuralları), MLR sekmesi + inceleyici görünümü (iğneli not, kanıt paneli, karar), revizyonlar + fark, çıktı / yayın, önizleme (PDF), kullanım, eski yol sihirbazı (K4) | Web | KP-2, KP-3 |
| **SB-3 Ziyaret** | aşama → yol adımları → içerik listesi; 11b + Q2 kararları | CRM | KP-3 |
| **KP-5 Ana veri** | marka kiti (ürün), ülke yasal profili, güvenlilik metni (ürün × ülke × dil, K2), onaylı görsel kütüphanesi (Belge Yönetimi) | CRM + Web | KP-1 |
| **KP-UI-3 Sayfa tasarımcısı** | sayfalar / bloklar, şablonlar, kilitli bloklar, uyum kontrolünün tasarım kuralları, erişilebilirlik uyarısı | Web + CRM | KP-5, KP-UI-1 |
| **SB-4 HTML çıktı** | HTML paket + aynı kaynaktan PDF (sunucu tarafı tarayıcı motoru kararı) | CRM / Infra | KP-UI-3 |
| Sonraki | ülkeye uyarla, AI yerleşim, saha etkisi uyarısı, gösterim takibi | — | — |

**Önerilen dalga:**
1. KP-1;
2. KP-2 ∥ KP-UI-1;
3. KP-3 ∥ KP-5;
4. KP-UI-2 ∥ SB-3 ∥ KP-4;
5. KP-UI-3;
6. SB-4.

## 9. Açık sorular (paketleme sırasında)
1. **Güvenlilik metninin onayı:** Ruhsat tek adım mı, MLR mı? Hangi kayıtta tutulur (ayrı ana veri mi, iddia altyapısının bir türü mü)? K2 "iddia değil" dedi; onay akışı KP-5'te sorulacak.
2. **Yayın SoD'si:** yayınlayanın MLR onaycılarından biri olamaması canlı ekip yapısında (tek kişide üç pozisyon: sema) sorun yaratır mı? Gerekirse "yayınlayan ≠ gönderen" ile sınırlanır.
3. **Zincir sürüm yükseltmesi** (yeni sürümde): eşleşmeyen adımlar ne olur?
4. **Eğitim yolları:** hepsi MLR'li olunca eğitim ekibinin onay yükü artar. Canlıda şu an yok; gerekirse sonra değerlendirilir.

> **Kullanıcı cevabı (2026-10-01) — §9.1:** güvenlilik metnini **yalnız Regülasyon (Ruhsat)** onaylar; tam MLR (Medikal → Hukuk → Ruhsat) değil. Kayıt yeri ve onay akışı (tek adımlı MOD-0023 şablonu mu, KP-5 içinde tek onaycı mı) KP-5 paketlenirken CT önerisiyle netleşir.
