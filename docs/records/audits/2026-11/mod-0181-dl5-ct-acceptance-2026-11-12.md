# MOD-0181 DL-5 — CT kabulü (Evidence Pack: sayım kanıtı PDF + CSV) — 2026-11-12

- CT: MVP-5 WMS CT (oturum `ca7382cf`; ölçüm CT'nin doğrulama alt ajanı, izole worktree, detached `2c29e54d4`). Talimat: WP-MVP5-0181-DL5-01.
  Paket r8 (Ali onaylı, main'de).
- Hedef: `c76c02944` (arka uç), `9f8696dd4` (ekran), `32b934166`, `2a8759e28` (CI-3 form modeli); kapsam dışı yolda `9e223626f` (taslak sözleşme
  belgesi) ve `a55087094` (Gateway sayımı 70 → 73, XMC-001 sahip "73 yap" 2026-11-12).
- Sonuç: **KOŞULLU** — AC-44, AC-50, AC-35 MET; AC-43 KISMİ (ORTA-1, ORTA-2). Düzeltme: WP-0181-DL5-FIX-01. Sonra CT WMS-COUNT-BUNDLE 1.1.0'ı
  yalnız ekleyerek dondurur.

## Kapsam
- §5 DL-5 ile uyumlu; `ocelot.json`, `.antigravity/**`, `_Layout.cshtml` değişmedi; dondurulmuş `wms-count-bundle.openapi.yaml`
  `git diff 6270e8b26 HEAD` boş. MOD-0173 / 0178 tarafında yalnız test sayıları (Gateway 70 → 73, `CentralAuditDeclarationTests` 116 → 117,
  `SingleSystemOfRecordArchitectureTests` ayrı `Evidence` listesi), XMC-001 sahip onayıyla.
- `WmsCountBundleContractTests` DL-5 hariç tutmaları (`200c1a71a`) doğrulandı; yeni enum'lar ayrı dosyada (`CountEvidenceEnums.cs`), 1.0.0
  enum eşitliği yeşil; DL-5 cevapları `CountEvidenceDraftContract` ile taslağa (§3.11 / §4.4 / §5.3) karşı.

## Takımlar (süzgeçsiz)
| Takım | Sonuç |
|---|---|
| Inventory (kendi Mongo'su; 27017 yok) | 2123 / **0** / 5 atlandı (eski: Recall 350 CustomerRef, 4 performans) |
| Web.Tests | 4537 / 3 — bilinen 3 (CapacityPlans, SandopPlans `…no_loading_title`, ReturnDetailsBehaviorTests.DetailsJs_shows_names_not_ids…) |
| TenantArchitecture | 224 / 5 — `DocsPathGuardTests` ×5 |
| Gateway | 371 / 0 |
| vitest | koşulmadı (worktree'de `node_modules` yok) — geliştirici ölçümü: 13 kırmızı = main |
`check_pack_nav_keys.py` PASS; `verify_datatable_page.py --module CountEvidenceExports --api-profile proxy` PASS.

## Sabotajlar (her biri ayrı, bayt-eşit geri)
| # | Sabotaj | Sonuç |
|---|---|---|
| a | dışa aktarma kaydı + dosya okumasından `TenantId` | kırmızı — `AC44_another_tenant_or_company_sees_404…` |
| b | aynı okumalardan `LegalEntityId` | kırmızı — aynı |
| c | indirme ucundan `[HasPermission]` | kırmızı — `AuthorizationGuardTests…`, `AC44_each_evidence_endpoint_needs_its_own_key`, `WmsCountBundleContractTests…` |
| d1 | kayda yanlış `Sha256` | kırmızı — 5 test |
| d2 | indirmede yeniden özetleme + 500 `COUNT_EVIDENCE_INTEGRITY` reddi kaldırıldı | **YEŞİL** → ORTA-2 |
| e1 / e2 | elle COUNT_* bölümü CSV'den / PDF'ten | kırmızı — `AC43_a_Turkish_export_carries_every_section…` |
| f | `CycleCountEvidenceText_tr.resx`'ten `CountPdf.Manual` | kırmızı — anahtar ailesi + tr dışa aktarma |
| g | görünümde UAS-001 denetimi kaldırıldı | kırmızı — `CycleCountScreensTests.AC38_UAS001_without_the_page_key…` |

## AC
| AC | Durum |
|---|---|
| AC-43 | KISMİ — içerik, PDF = CSV, insert-only, `Sha256` + `X-Content-SHA256`, dil kuralı (zh/ar → İngilizce PDF + not, `RequestedLanguage`) tamam; ORTA-1, ORTA-2 açık |
| AC-44 | MET |
| AC-50 | MET |
| AC-35 (DL-5) | MET — `CountEvidenceExports/Index.cshtml:12` `Layout = "_LayoutTenantShell"` |

## Bulgular
- **ORTA-1 — Kanıt Paketi dosyasında ham kimlik.** `CountEvidenceDocumentBuilder.cs` `:83` satır SKU, `:94` karar veren, `:104` elle hareket SKU,
  `:106` elle hareket kullanıcısı, `:109` zaman çizelgesi kullanıcısı, `:110` şirket (`LegalEntityId`), `:64` yer adı bulunamazsa GUID'e düşer.
  Test bunu sabitliyor (`CycleCountEvidenceTests.cs:92`, `:100` onaylayanın GUID'ini arıyor). Denetçiye ad / kod verilmeli (FIX-01 / FIX-02
  kuralı: okunamazsa lokalize "okunamadı", kimlik değil).
- **ORTA-2 — bütünlük reddi testsiz.** `CountEvidenceQueryHandlers.cs:63-68` yeniden özetleme + 500 kaldırılınca takım yeşil (d2). Saklanan baytı
  değiştirip 500 `COUNT_EVIDENCE_INTEGRITY` bekleyen test gerekli.
- **DÜŞÜK-3 — dil seçimi taslak lafzından ayrı.** `CountEvidenceExportsController.cs:42` `Accept-Language`'i q değerine göre sıralıyor; taslak
  §3.11 "ilk etiketin birincil alt etiketi". CT kararı: kod davranışı (q sıralaması, HTTP standardı) korunur; taslak lafzı 1.1.0 dondurmasında
  koda hizalanır.
- **DÜŞÜK-4 — indirmenin denetimi servis günlüğünde** (`CountEvidenceQueryHandlers.cs:71-72`, `LogInformation`: kim, hangi dışa aktarma,
  sha256). Paket §21 ve taslak §4.4 (7) böyle tanımlıyor — pakete uygun; CT kabul eder.
- **BİLGİ-5** — 1.1.0'da hata durum deseni (`WmsCountBundleContractTests` `^(400|403|404|409|422|503)$`) 500'ü kapsamalı.

## 1.1.0 dondurmasına girecek üç operasyon (koddan ölçüldü)
Taban `/api/warehouse`, `CountEvidenceExportsController`:
- `POST /cycle-counts/{countId}/evidence-exports` — `createCountEvidenceExport`, `cycle-count.evidence.export`; `Idempotency-Key` = gövde
  `idempotencyKey`; gövde `{idempotencyKey, format: Pdf|Csv}`; 201 / aynı anahtarla 200; 400 `INVALID_REQUEST` / `IDEMPOTENCY_KEY_REUSED`, 404
  `COUNT_NOT_FOUND`, 409 `COUNT_INVALID_STATE` (yalnız Submitted / Closed).
- `GET /count-evidence-exports` — `listCountEvidenceExports`, `cycle-count.evidence.read`; `countId?`, `page`, `pageSize` 1–200; en yeni önce.
- `GET /count-evidence-exports/{exportId}/file` — `getCountEvidenceExportFile`, `cycle-count.evidence.read`; 200 bayt (`application/pdf` /
  `text/csv; charset=utf-8`, `Content-Disposition`, `X-Content-SHA256`); 404 `COUNT_EVIDENCE_NOT_FOUND`, 500 `COUNT_EVIDENCE_INTEGRITY`.
- `CountEvidenceExport`: kapalı, null yok, 13 zorunlu üye (`exportId`, `countId`, `requestedBy` uuid; `countNumber`; `format`; `language`,
  `requestedLanguage` 7 dil; `requestedAt`; `fileName`; `sha256` 64 hex; `sizeBytes` ≥ 1; `status` Ready | Failed; `contractVersion` v1).

KOŞULLU
