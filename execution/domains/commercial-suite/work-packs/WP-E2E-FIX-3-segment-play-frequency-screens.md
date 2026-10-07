# WORK PACKAGE — WP-E2E-FIX-3 · Segment, Oyun (strateji şablonu) ve Sıklık ekranları + aday sorgusu + Türkçe arama takibi

> **CT (SoR), 2026-10-07.**
> - **Kaynak:** [E2E TUTUKON](E2E-TUTUKON-content-to-visit-plan.md) bulguları E1-B1, E1-B2, E1-B3, E5-B1, E5-B2, E5-B3, E6-B1 + WP-VP-FIX-2 §37 Türkçe arama takip listesi · [yol haritası](ROADMAP-visit-planning.md) → E2E-FIX.
> - **Kullanıcı:** "test bulgularını paketleyebilirsin" (2026-10-07); E1-B2 için "ek blokları aday sorgusuna indirme" yol haritasında.
> - **Kapsam:** CRM (segment aday kaynağı, oyun etkin sürüm seçimi, iki depo araması) + Web (Segments, StrategyTemplates, VisitFrequencyPolicies formları, kiracı kabuğunda uyarı mesajı).
>
> **Çalışma yeri:** worktree `C:\tmp\e2e-fix-3`, dal `wp/e2e-fix-3`, taban `test/crm-content-visit-e2e`. Commit bu dala, push YOK.
> **Paralel paketler:** WP-E2E-FIX-1 ve WP-E2E-FIX-2. `SharedResource.*.resx`'e dokunma (layout uyarısı mevcut anahtarlarla ya da görünüm resx'iyle).

## ⚠ Önemli bulgu (CT kod okuması): eski oyun sürümü yenisini eziyor
`VisitContentSequenceResolver.cs:162-192` açık `StrategyTemplateId` yoksa `StrategyTemplateReader.ListBySegmentAsync` (`StrategyTemplateReader.cs:42-62`) ile segmentin aktif oyunlarını alıyor; sıra `TemplateCode`, sonra **`TemplateVersion` artan** → `FirstOrDefault` = **v1**. Aktifleştirme v1'i yalnız `SupersededByTemplateId` ile işaretliyor (`ActivateStrategyTemplateHandler.cs:95,105-129`), `TemplateStatus` `active` kalıyor; `IsActive()` (`StrategyTemplate.cs:117-118`) bu işareti yok sayıyor.
E2E'de zararsızdı çünkü v1'in segmenti arşivliydi. **Aynı segmentte v2 yayımlanınca planlama v1'in ürün / yolculuğunu kullanır.** Bu paketin en öncelikli maddesi.

## Bağlam (CT kod okuması, 2026-10-07)
Kısaltmalar: **Web** = `frontend/Diten.Web`, **CRM** = `services/Diten.CrmService/src`.

| Bulgu | Kök (dosya:satır) |
|---|---|
| E1-B1 referans değerleri | `Web/wwwroot/assets/js/CRM/Segments/form.js:150-165` `loadReferenceValues` → `GET {endpoint}/reference-values/{set}`; `:157-159` yalnız `x.value \|\| x.valueCode` olanları tutuyor, etiket `x.text \|\| x.displayName`. Gerçek yanıt (`CrmReferenceSetReader` → Platform `published-values`): `data.items[{ code, label, description, isActive, sortOrder, attributes }]` (`BusinessReferenceDataStewardshipModels.cs:159-171`) → hepsi eleniyor. CRM sunucu okuyucusu dört adı da kabul ediyor (`GatewayReferenceDataValidator.cs:411`). |
| E1-B3 global-products 400 | `Segments/form.js:173` `getJson('/global-products?pageSize=200')`; vekil (`SegmentsController.cs:252-255`) aynen `/api/global-products/selector`'a; MDM `PageSize 1..100` (`GetGlobalProductSelectorValidator.cs:11`). Emsal: `StrategyTemplates/form.js:247` `loadAll` 100'lük sayfalarla. |
| E1-B2 10K | `SegmentLimits.MaxCandidateSet = 10_000` (`Domain/Entities/SegmentVocabulary.cs:246`); 422 `PreviewSegmentReachHandler.cs:95`, `ResolveSegmentMembershipHandler.cs:64`. `SegmentCandidateSource.cs:38-77` tenant + `IsDeleted` + `BuildPushdown` + `Limit(cap+1)`. İndirilen: yalnız konu belgesindeki alanlar (`MapField` `:345-369`; kişi uzmanlığı `:359` dahil), yalnız eq / in / contains (`:324-336`). Bellekte: olumsuzlama (`:247,258,333`), tarih (`:312`), `account.attribute` (`:367`), `contact.account-role / is-primary / account-type`, `territory.*`, `consent.*`, `concept.affinity`. VEYA grubunda / "any" modunda tek bir indirilemeyen dal **tüm grubu düşürüyor** (`Combine` `:279-286`) → aday kümesi tüm kiracı. |
| Türkçe arama | `TerritoryModelRepository.cs:43-44` kaçışsız `new BsonRegularExpression(term, "i")` (Name, ModelCode); `:89-91` kaçışlı, i katlaması yok; `SegmentCandidateSource.cs:331` (contains) ve `:342` (eq `^…$`) kaçışlı, i katlaması yok. Ortak yardımcı: `CRM/Diten.CrmService.Application/Common/TurkishInsensitivePattern.cs`. |
| E5-B1 kaydet ve aktifleştir | `StrategyTemplates/form.js:1333-1364` gizli `#activateAfterSave`; `StrategyTemplatesController.cs:154-155` yalnız `crm.strategy-template.activate` kontrolü (`:176`, `:218-220`'deki manage yedeği yok); `ActivateAfterSaveAsync` (`:420-433`) hata olunca `TempData["WarningMessage"]` — kabuk bunu çizmiyor (`_LayoutTenantShell.cshtml:661-662` yalnız Success / Error). Arka uç aktif olmayan segment bağında 409 (`ActivateStrategyTemplateHandler.cs:71-78`). |
| E5-B2 iki aktif sürüm | Yukarıdaki ⚠. Durumlar yalnız `draft / active / archived` (`StrategyTemplateVocabulary.cs:16-22`); `IsSuperseded()` = `SupersededByTemplateId != null` (`StrategyTemplate.cs:122`). |
| E5-B3 GUID | `StrategyTemplates/form.js:201` `segments?includeArchived=false` + `:209` `o.archived` eleniyor; `renderSegments` (`:387-388`) bulunamazsa ham kimlik. `details.js:150` `includeArchived=true` (ayrıntıda ad görünüyor). |
| E6-B1 vurgu yok | Alan `vfpPriority` bant kartları (`VisitFrequencyPolicies/form.js:179-189,228-231`; `_Editor.cshtml:431-433`); doğrulama `form.js:781` yalnız 12px metin; `submit` (`:951`) üstte genel mesaj; kartlarda hata sınıfı, kaydırma / odak yok. En yakın desen: `CRM/RegulatoryTexts/form.js:42-55` (`[data-error-for]` + `is-invalid`). |

## NE
### 1. (E5-B2) Oyunda tek etkin sürüm — öncelikli
- Okuma kuralı: segmentten / koddan oyun seçerken **yerini başka sürüme bırakmış** (`IsSuperseded()`) oyun etkin sayılmaz; birden fazla aday kalırsa **en yüksek `TemplateVersion`** kazanır. Kural tek yerde (okuyucu); çözücü kendi sıralamasını tekrarlamaz, okuyucuyu kullanır.
- Aynı kuralı oyunu segmentten seçen **tüm** okuyucular uygular (planlama önizlemesi, uygula, planlanan ziyaret içerik çözümü; listele, dokunmadıklarını raporla).
- `TemplateStatus` değer kümesi değişmez (yeni durum YOK, göç YOK). Liste / ayrıntı ekranında yerini bırakmış sürüm "Yerini v{n} aldı" bilgisiyle görünür (rozet; 7 dil).
- Açık `StrategyTemplateId` verilmişse bugünkü gibi o kullanılır.

### 2. (E5-B1, E5-B3) Oyun formu
- **E5-B1:** "Kaydet ve aktifleştir" izin kontrolü `:176` / `:218-220` ile aynı (activate **ya da** manage yedeği). Aktifleştirme başarısızsa neden kullanıcıya görünür: kiracı kabuğu `TempData["WarningMessage"]`'ı da çizer (mevcut Success / Error deseniyle, uyarı rengi). Arka uç 409 nedeni (ör. arşivli segment) yerelleştirilmiş metinle.
- **E5-B3:** formda bağlı segmentlerin adı arşivli olsalar da görünür ("(arşivli)" etiketiyle); seçim listesi yeni bağ için yine yalnız aktif segmentleri sunar.

### 3. (E1-B1, E1-B3) Segment formu
- **E1-B1:** `loadReferenceValues` `code` / `label` (ve geriye uyum için `value` / `valueCode` / `text` / `displayName`) okur; pasif değerler (`isActive=false`) gösterilmez ya da işaretlenir (mevcut desen hangisiyse); sıralama `sortOrder`.
- **E1-B3:** ürün listesi 100'lük sayfalarla (`StrategyTemplates` `loadAll` deseni) ya da aramalı seçici; 400 kalkar.

### 4. (E1-B2) Aday sorgusuna indirme
- **Önce ölç (salt okuma, canlı `DitenERP_Dev` 97c5):** gastroenteroloji, aile hekimliği, dahiliye kişi sayıları tek tek ve birlikte; E2E'deki 422'nin hangi kural şeklinden geldiği (uzmanlık zaten indiriliyor → küme mi büyük, VEYA / "any" / olumsuzlama mı?). Raporla.
- **İndir:**
  - `territory.node` / `territory.model`: önce kapsanan hesap / kişi kimlikleri (mevcut kapsam çözücüsüyle), sonra `Id IN`.
  - `contact.account-type` / `account-role` / `is-primary`: `account_contact_links` (+ `accounts.AccountType`) ön sorgusuyla `ContactId IN`.
  - VEYA grubu: tüm dalları indirilebiliyorsa `$or` olarak indir; biri indirilemiyorsa bugünkü gibi düşür (doğruluk korunur).
  - Bellekteki değerlendirme **aynen kalır** (indirme yalnız adayı daraltır; son karar yine bellekte) → sonuç değişmez, yalnız aday kümesi küçülür.
- **Önizleme sayısı:** kural tamamen indirilebiliyorsa önizleme sayısı Mongo `CountDocuments` ile; 10K sınırı önizlemede bu durumda uygulanmaz. Üyelik çözümündeki sınır **değişmez** (karar CT'de; ölçümü raporla).
- Ön sorgu kimlik kümeleri de büyükse (ör. > 50K) indirmeyi atla ve bugünkü yola dön; eşik sabit + raporla.

### 5. Türkçe arama takibi
- `TerritoryModelRepository:43-44` ve `:89-91`, `SegmentCandidateSource:331` ve `:342` → `TurkishInsensitivePattern.Build` (kaçış + `[iıIİ]`; eşittir aramasında `^…$` korunur).

### 6. (E6-B1) Sıklık formu alan vurgusu
- Eksik bant: kart grubuna hata sınıfı (kırmızı çerçeve) + hata metni grubun hemen üstünde; gönderimde ilk hatalı alana kaydır + odak. Aynı davranış formun diğer doğrulanan alanlarına da (tek ortak işlev bu formun içinde).

## KORU / YAPMA
- **Yeni yazma komutu YOK** (AUD-001 26 sabit). Oyun durum kümesi değişmez, göç YOK.
- Segment değerlendirme sonuçları değişmez (indirme yalnız aday daraltır) — eşdeğerlik testiyle kanıtla.
- Ekran tasarımı değişmez. Yeni metinler **7 dil**; TR diakritikli; `SharedResource`'a dokunma.
- Seed / grant / dizin YOK (yeni dizin gerekirse **ekleme**, raporla). `E2E-TUT-` kayıtları silinmez; TUTUKON v1 kaydına dokunma.

## Acceptance
### E2 (taban ölç, yalnız farkı raporla)
Web (699/0), CRM Application (2303/0/5; PII flake), mimari (38/1; **26 sabit**). Build 0 hata.

**Yeni testler (üretim koduyla):**
1. Aynı segmentte v1 (aktif, yerini v2'ye bırakmış) + v2 (aktif) → çözücü / okuyucu **v2**'yi seçer; v1 yalnızken v1.
2. Yerini bırakmış ama halefi aktif olmayan (taslak) sürüm → v1 etkin kalır (halef yalnız aktifleşince devralır) — kuralı netleştir ve test et.
3. Kaydet ve aktifleştir: manage yedeğiyle aktifleşir; aktifleştirme 409 → uyarı mesajı kabukta görünür (Web testi).
4. Oyun formu arşivli bağlı segmentin adını gösterir.
5. Segment `loadReferenceValues` `{code,label}` öğelerini listeler (JS / kaynak testi).
6. Segment ürün listesi 100'ü aşmayan sayfalarla ister; 176 ürünün hepsi gelir.
7. İndirme eşdeğerliği: bölge / bağlantı / VEYA bloklu kurallarda indirmeli ve indirmesiz yol **aynı üyeleri** verir; aday sayısı küçülür.
8. Tamamen indirilebilen kuralda 10K'yı aşan önizleme 422 yerine sayı döner; kısmen indirilebilende bugünkü 422.
9. Türkçe arama: bölge modeli "İstanbul" / "istanbul" / "ISTANBUL"; segment contains / eq aynı.
10. Sıklık formu eksik bantta kart grubunda hata sınıfı + odak (kaynak / JS testi).

**Sabotaj (kırmızı kanıtla, geri al):**
1. Okuyucuda `IsSuperseded` dışlamasını kaldır → test 1 kırmızı.
2. Bellekteki son değerlendirmeyi atla (yalnız indirmeye güven) → test 7 kırmızı.

### E4 (CT, fleet; ayrı sekme)
- Segment düzenleyicide uzmanlık çipleri dolu; ürün listesinde TUTUKON var (kaydetmeden).
- Oyun formunda arşivli segment adı; TUTUKON planı önizlemesi v2'nin ürün satırını kullanıyor (salt okuma).
- Gastro + aile + dahiliye segment önizlemesi: sayı ya da raporlanan nedenle 422 (kaydetmeden).
- Sıklık formunda bant seçmeden kaydet → bant grubu kırmızı + odak.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-architect.md] @[.antigravity/agents/frontend-ui-ux.md]
WP: WP-E2E-FIX-3 · Segment, Oyun ve Sıklık ekranları + aday sorgusu + Türkçe arama takibi
Repository: C:\tmp\e2e-fix-3 (worktree) · Branch: wp/e2e-fix-3 · commit bu dala, push YOK

Paket belgesi + tam komut: execution/domains/commercial-suite/work-packs/WP-E2E-FIX-3-segment-play-frequency-screens.md — önce tamamını oku (⚠ bölümü + Bağlam tablosu CT okumasıdır, doğrula). Ayrıca: …/E2E-TUTUKON-content-to-visit-plan.md (§E1, E5, E6) · …/WP-VP-FIX-2-edit-keeps-targets-turkish-search.md (§37 takip listesi) · services/Diten.CrmService/src/**/{Features/StrategyTemplate*/**,Features/VisitContentSequence/**,Features/Segment*/**} · services/Diten.CrmService/src/**/Persistence/Repositories/{SegmentCandidateSource,TerritoryModelRepository}.cs · frontend/Diten.Web/wwwroot/assets/js/CRM/{Segments,StrategyTemplates,VisitFrequencyPolicies}/** · frontend/Diten.Web/Controllers/CRM/{SegmentsController,StrategyTemplatesController}.cs · frontend/Diten.Web/Views/Shared/_LayoutTenantShell.cshtml · .antigravity/rules/audit-trail-standard.md.

NE:
(1) E5-B2 ÖNCELİKLİ — oyun seçiminde IsSuperseded() olan sürüm etkin sayılmaz, birden çok aday → en yüksek TemplateVersion; kural tek yerde (okuyucu), segmentten oyun seçen tüm yollar kullanır; durum kümesi/göç değişmez; ekranda "Yerini v{n} aldı" rozeti.
(2) E5-B1 kaydet+aktifleştir izin kontrolü manage yedeğiyle; kabuk WarningMessage'ı çizer; 409 nedeni yerelleştirilmiş. E5-B3 formda arşivli bağlı segment adı "(arşivli)".
(3) E1-B1 loadReferenceValues code/label (+geriye uyum); E1-B3 ürün listesi 100'lük sayfalarla.
(4) E1-B2 — önce canlı salt-okuma ölçüm (gastro/aile/dahiliye sayıları, 422'nin kural şekli); territory.* ve contact link bloklarını kimlik ön sorgusuyla indir; tüm dalları indirilebilen VEYA grubunu $or ile indir; bellekte son değerlendirme AYNEN; tamamen indirilebilen kuralda önizleme sayısı CountDocuments (10K yok); üyelik çözümü sınırı değişmez; büyük ön-sorgu kümesinde (eşik) eski yola dön.
(5) Türkçe arama — TerritoryModelRepository:43-44, :89-91, SegmentCandidateSource:331, :342 → TurkishInsensitivePattern.Build.
(6) E6-B1 — sıklık formu bant grubunda hata sınıfı + ilk hataya kaydır/odak.
KORU/YAPMA: YENİ YAZMA KOMUTU YOK (AUD-001 26 sabit); oyun durum kümesi/göç YOK; segment sonuçları değişmez (eşdeğerlik testi); tasarım değişmez; yeni metinler 7 dil (TR diakritik), SharedResource'a dokunma; seed/grant/dizin YOK; E2E-TUT ve TUTUKON v1 kayıtlarına dokunma.
DOĞRULA (E2): tabanı ölç, yalnız farkı raporla — Web (699/0) · CRM Application (2303/0/5, PII flake) · mimari (38/1, 26 SABİT); build 0 hata; fleet açıkken Web bin kilitliyse -o frontend/Diten.Web.Tests/bin/Debug/<ad>. Yeni testler WP Acceptance 1–10. Sabotaj 1–2 (kırmızı kanıtla, geri al).
Commit: "fix(crm,web): WP-E2E-FIX-3 — single effective play version, segment reference values/products, candidate pushdown, Turkish search, play/frequency form fixes" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor: madde başına ne yapıldı + kanıt (dosya:satır, test adı), oyunu segmentten seçen okuyucular listesi, E1-B2 canlı ölçüm tablosu + hangi blokların artık indirildiği + eşik, yeni dizin gerekip gerekmediği. §22 TÜRKÇE. K13.
```

---

## §37 CT kabul — E2 ACCEPTED (2026-10-07)
**Commit:** `aaa174ba0` (ajan `299753a80`, FIX-1 + belge commit'leri üzerine rebase, ff). Push: test dalı.

**CT K13 (FIX-1 dahil taban üzerinde):**
| Paket | Taban | Sonuç |
|---|---|---|
| CRM Application | 2314/0/5 (FIX-1 sonrası) | **2342/0/5** (+28) |
| Web | 711/0 | **721/0** (+10) |
| Mimari | 38/1 (26) | **38/1 (26 sabit)** |

**Kod okuması:**
- **E5-B2:** kural tek yerde `StrategyTemplateReader` — `IsReplacedAt` (yerini bırakmış **ve** halefi aktif + yürürlükte) dışlanır; `InPreferenceOrder` = sürüm azalan → kod → kimlik. `VisitContentSequenceResolver` ve `VisitProvenanceDeriver` kendi sıralamasını bıraktı. VP-2'nin "v2 v3'ü yener" testi yeni kurala çevrildi (bilinçli; hata sabitleniyordu).
- **E1-B2:** `SegmentCandidatePrefilter` yalnız **üst küme** üretiyor (bölge: mevcut kapsam çözücüsü; bağlantı: toplu ön-sorgu); bellekteki değerlendirme aynen. Eşik 50.000. Tam yerel kuralda önizleme sayısı `CountDocuments` (`CountedByStore`, örnek boş → ekran "—" gösteriyor, sayı görünüyor). Üyelik çözümü 10K sınırı değişmedi.
- **Kabuk uyarısı:** `WarningMessage` artık sarı toast; yalın resx anahtarı (yalnız harf/rakam) sessiz kalır.

**CT sabotajı (ajanınkinden ayrı):** halefin `IsActive()` kontrolü kaldırıldı + bağlantı ön-sorgusu boş küme → **3 kırmızı** (`A_successor_that_is_not_live…(draft|archived)`, `Link_blocks_role_and_primary_are_pushed_and_the_members_are_identical`). Geri alındı.

**Canlı ölçüm (ajan, salt okuma):** gastro 910 · aile 44.568 · dahiliye 10.574 · üçü 56.052 · Beste'nin 4 ilçesiyle kesişim **1.850**. 422'nin nedeni kümenin kendisinin büyük olması (kişide ilçe alanı yok; bölge yalnız bağlantı → hesap zincirinden).

**Takipler:**
- Üyelik çözümü (kampanya hedefleme) 10K sınırı: 56K'lık segment önizlemede sayılır ama çözülemez. Planlamadaki oyun türetme tek kişi değerlendirmesi kullandığı için etkilenmez. Karar gerekirse Faz 3'te.
- 8 denetleyicinin `WarningMessage`'a yazdığı ham resx anahtarları (bugüne kadar hiç görünmüyordu) → yerelleştirip göster (küçük iş).
- İsteğe bağlı dizin adayları: `contacts {TenantId, Specialty}`, `account_territory_assignments {TenantId, TerritoryNodeId, AssignmentStatus}` (eklenmedi).

**E4 (CT, bekliyor):** segment düzenleyicide uzmanlık çipleri + ürün listesinde TUTUKON · oyun formunda arşivli segment adı · TUTUKON önizlemesi v2'nin satırı · gastro+aile+dahiliye önizlemesi sayı · sıklık formunda bant grubu kırmızı + odak.

### §37 ek — E4 ACCEPTED (2026-10-07, CT, salt okuma)
- Segment düzenleyici (`E2E-TUT-SINDIRIM`): uzmanlık çipleri dolu ✓ (etiketler İngilizce → veri işi 0.5). Ürün listesi 100'lük sayfalarla (2. sayfa 77, TUTUKON var) ✓.
- Önizleme: gastroenteroloji + aile hekimliği + dahiliye → **56.052** (`countedByStore`, ~1,5 sn; eskiden 422) ✓; yalnız gastroenteroloji 910 + 50 örnek ✓.
- Oyun listesi: v1'de "Yerini v2 aldı" ✓; v1 düzenle: "Üroloji Hekimleri (arşivli)" (GUID yok) ✓.
- Sıklık formu boş kaydet: bant grubu dahil 7 alan işaretli, odak ilk hatada, istek gitmiyor ✓.
