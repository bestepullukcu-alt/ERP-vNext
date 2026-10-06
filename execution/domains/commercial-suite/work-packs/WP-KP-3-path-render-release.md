# WORK PACKAGE — WP-KP-3 · Bilgi Yolu çıktısı + yayın + geri çekme + kullanım okuması

> **CT (SoR).**
> - **Tasarım:** `DESIGN-KP-STUDIO-knowledge-path-studio.md` §3.4–§3.7, §4 (D-KP-8, D-KP-9), §9 Q2.
> - **Mockup v2:** "Çıktı ve yayın", "Kullanım" sekmeleri.
> - **Bağımlılık:** KP-1 (`95c2444d`) + KP-2 (`ccf3d8fe`) birleşti.
> - **Kapsam:** yalnız CrmService. Web KP-UI-2'de.
>
> **Çalışma yeri:** worktree `C:\tmp\kp-3`, dal `wp/kp-3`, taban `test/crm-content-visit-e2e`. Commit bu dala, push YOK.

## Yayın SoD kararı (§9 Q2 — CT varsayılanı, kullanıcıya bildirildi)
**Kural: Yayınlayan ≠ revizyonu gönderen (kişi bazında).**

Tasarımdaki daha güçlü kural ("yayınlayan MLR onaycılarından biri olamaz") canlı ekipte kimseye yayın bırakmıyor:
- 97c5'te 2 kullanıcı var;
- Admin gönderen;
- sema üç MLR pozisyonunun hepsinde.

Kullanıcı güçlü kuralı isterse tek bir koşul eklenir; kodda tek noktada tutulmalı (`KnowledgePathReleaseRules.CanRelease`).

## Kanıt (CT)
- **Revizyon** (KP-2): `Domain/Entities/KnowledgePathRevision.cs`.
  - `RenderedArtifacts` (`KnowledgePathRenderedArtifact`) ve `ReleaseState` (`KnowledgePathReleaseState`) **yer olarak ayrıldı**, yazılmıyor.
  - `ReviewRound` (outcome), `CreatedBy` = gönderen, anlık görüntü.
- **Render altyapısı (SCMM-16B):**
  - `ContentSetRevisions/Rendering/{IContentSetRevisionRenderer, IContentArtifactStore}.cs`;
  - `Infrastructure/ContentComposition/Rendering/{PdfSharpContentSetRevisionRenderer, HttpContentArtifactStore}.cs` (FU01 `ContentMessagingArtifacts`, kullanıcı token'ı ile yükleme, idempotent);
  - artifact okuma: `GET …/artifact` (`ContentSetRevisionQueryHandlers`, revizyon id'sinden çözülür; istemciden contentId alınmaz).
- **Yayın kuralları** (SB-2 üreticisi; buraya taşınır, kopyalanmaz):
  - `ContentSetReleaseProducer.cs`: içerik yayında, tek dil, iddia kullanılabilir, ülke sürümü seçimi, eski yol sürümünü `inactive` yapma, yolculuk aşaması kullanımı (`:518` `RecommendedKnowledgePathId`) → `path_in_use` / `previous_path_in_use`.
  - KP-1'in `KnowledgePathStudioReader` iddia okuması zaten `usable` / `reason` veriyor.
- **Yol yayını** (mevcut): `PublishKnowledgePathHandler` (V-P10 çakışma, `StepSetFrozenAt`, `PublishedAt / By`). KP-2: zincirli yolda 409 `approval_via_workflow_only`.
- **Kullanım bağları:**
  - yolculuk aşaması `RecommendedKnowledgePathId` (+ `PathVersionPinPolicy`);
  - strateji şablonu `ContentBindings` (`knowledge-path`).
  - İddia kullanım okuyucusu (`ClaimUsageQueryHandlers.cs:192`) benzer sorguyu yapıyor.
- **Yetki:** `crm.knowledge.path.publish` (mevcut anahtar) render + yayın + geri çekme için.

## NE

### 1. Render (çıktı)
- **Uç:** `POST paths/{id}/revisions/{revId}/render` (publish izni).
  - Yalnız **onaylı** revizyon (409 `revision_not_approved`).
  - **Idempotent:** çıktı varsa yeniden yüklemez, aynı ContentId.
- **Renderer** (`IKnowledgePathRevisionRenderer` + PdfSharp uygulaması; mevcut MigraDoc yardımcılarını ortaklaştır):
  - başlık + **onay kodu** `{PathCode}-v{PathVersion}-R{n}`;
  - bağlam (zincir, ülke adı, dil adı, ürün, kitle);
  - **dal-öncelikli sırayla** adımlar (içerik başlığı + sürüm);
  - adımlara yerleşen iddialar (kod + yolun dilindeki ülke metni + niteleyici);
  - uyum özeti;
  - **MLR turu** (adım adı, karar, kişi, tarih, yorum);
  - üretim zamanı.
- **Saklama:** mevcut `IContentArtifactStore` → `RenderedArtifacts += {Kind: pdf, ContentId, Checksum, ByteSize, MediaType, FileName, RenderedAt, RenderedBy}`.
- **HTML ÇIKTI YOK** (SB-4). `Kind` alanı ileride `html` alacak şekilde.
- **Okuma:** `GET paths/{id}/revisions/{revId}/artifact?kind=pdf`. Akış revizyondan çözülür, istemciden contentId alınmaz; başka tenant'ta 404.

### 2. Yayın
- **Uç:** `POST paths/{id}/revisions/{revId}/release` (publish izni).
- **Ön koşullar** (fail-closed, 409 + kod):
  - revizyon onaylı (`revision_not_approved`);
  - **çıktı hazır** (`artifact_missing`);
  - revizyon yolun **son onaylı** revizyonu ve yol `approved` durumda (`revision_superseded`);
  - anlık görüntüdeki içerikler hâlâ yayında (`component_not_published`);
  - tek dil (`component_language_mismatch`);
  - **tüm iddialar kullanılabilir**: onaylı ya da gözden geçirilmeli, ülke sürümü var, yolun dilinde metin var (`claim_not_approved`, `claim_no_country_version`, `claim_language_mismatch`);
  - uyum (`chain_conformance_failed`);
  - **SoD:** yayınlayan ≠ gönderen (403 `sod_submitter_cannot_release`).
- **Etki** (tek yazım ya da telafi):
  - revizyon `ReleaseState{Released, zaman, kişi}`;
  - yol `published`, `StepSetFrozenAt`, `PublishedAt / By` (mevcut alanlar);
  - aynı `PathCode`'un önceki yayındaki sürümü `inactive`.
  - Bu eski sürümü **sürüme sabitlenmiş** yayınlanmış bir yolculuk aşaması kullanıyorsa yanıtta `previous_path_in_use` uyarısı + aşama adları (11b → SB-3).
- **Idempotent:** yayınlanmış revizyon için 200 + mevcut durum.

### 3. Geri çekme
- **Uç:** `POST paths/{id}/revisions/{revId}/withdraw` `{reason}` (zorunlu, 400 `reason_required`; publish izni).
- **Koşul:** yalnız yayınlanmış revizyon.
- **Kullanımda yol:** yayınlanmış bir yolculuk aşaması bu yolu kullanıyorsa **409 `path_in_use`** + aşama adları; hiçbir şey değişmez (mockup v2).
- **Kullanılmıyorsa:** revizyon `Withdrawn`, yol `inactive`.

### 4. Kullanım okuması
- **Uç:** `GET paths/{id}/usage` →
  - `journeys[] {journeyId, code, name, status, stageCode, stageName, pinPolicy, pinnedVersion?}`;
  - `strategyTemplates[] {templateId, code, name, status}` (`knowledge-path` bağları).
- Hem geri çekme kontrolünde hem KP-UI-2 "Kullanım" sekmesinde kullanılır. Sayfa gösterim verisi YOK (sonraki faz).

### 5. Ortaklaştırma
- **SB-2'nin yayın kuralları** (bileşen yayında, dil, iddia kullanılabilirliği, ülke sürümü, yolculuk kullanım kontrolü) `Features/Knowledge/Chain/` ya da `Path/Release/` altında **tek yerde** toplanır.
- `ContentSetReleaseProducer` KP-4'e kadar aynı kuralları buradan çağırır. Davranışı değişmez, testleri yeşil kalır.

### 6. Eski yol
- Zincirsiz eski yolun mevcut `publish` ucu bugünkü gibi kalır (KP-4 kapatacak). Ona dokunma.

## KORU / YAPMA
- **Web / Platform / Auth DOKUNMA.** HTML render YOK (SB-4).
- İçerik Seti / SB-2 davranışı DEĞİŞMEZ (yalnız ortak kural çağrısı). KP-4 ayrı.
- Ziyaret çözücüsü DEĞİŞMEZ (SB-3).
- Ham repository yazması yok. Yeni alanlar class-map'e. İşlem yoksa telafi.
- **DUR:**
  - yolun eski sürümünü `inactive` yapmak mevcut V-P10 / sürüm kurallarıyla çelişiyorsa;
  - FU01 deposu `ContentMessagingArtifacts` kapsamında Bilgi Yolu revizyonunu sahip öğe olarak kabul etmiyorsa: raporla.

## Acceptance
- **E2:**
  - CRM testleri 0 kırmızı (taban 2167/0/5; bilinen sıra flake'i hariç). **Web testleri 0 kırmızı** (taban 368). Build 0 hata.
  - **Yeni testler:**
    - render (onaysız 409, idempotent, onay kodu, dal-öncelikli sıra + iddia metni + MLR turu içerikte, geçerli %PDF);
    - artifact okuma (revizyondan çözüm, başka tenant 404);
    - yayın ön koşulları (her kod);
    - SoD yayınlayan = gönderen 403;
    - yayın etkisi (published, donma, önceki sürüm inactive, `previous_path_in_use` + aşama adları);
    - idempotent yayın;
    - geri çekme (`reason_required`, `path_in_use` 409 + aşama adları, inactive);
    - kullanım okuması;
    - eski yolun publish ucu korunur;
    - SB-2 testleri ortak kurallarla yeşil.
  - **Sabotaj:** SoD yayın kontrolü, iddia kullanılabilirlik ön koşulu ve `path_in_use` testleri kırmızıya dönmeli.
- **E4:** KP-2-CFG + KP-UI-2 sonrası CT.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-KP-3 · Bilgi Yolu çıktısı + yayın + geri çekme + kullanım okuması
Repository: C:\tmp\kp-3 (worktree) · Branch: wp/kp-3 · commit bu dala, push YOK · yalnız CrmService

Amaç: DESIGN-KP-STUDIO §3.4–§3.7: onaylı revizyondan PDF çıktı (onay kodu, dal-öncelikli adımlar, iddia metinleri, MLR turu), yayın (SB-2 kuralları burada, fail-closed; yayınlayan ≠ gönderen), geri çekme (kullanımdaysa 409 path_in_use), kullanım okuması. HTML çıktı YOK (SB-4).

Önce oku: execution/domains/commercial-suite/work-packs/WP-KP-3-path-render-release.md · …/DESIGN-KP-STUDIO-knowledge-path-studio.md · …/WP-SB-2-content-set-release-to-knowledge.md (§37, kurallar) · …/WP-KP-2-path-revision-mlr.md (§37) · …/WP-SCMM-16B-render-content-set-revision.md + WP-SCMM-17-release-managed-withdrawal.md · services/Diten.CrmService/src/Diten.CrmService.Domain/Entities/{KnowledgePath, KnowledgePathRevision, ContentEngagementJourney, StrategyTemplate}.cs · Application/Features/Knowledge/Path/** (Review/*, KnowledgePathStudio*, publish handler) · Application/Features/ContentComposition/ContentSetRevisions/{ContentSetReleaseProducer, ContentSetRevisionQueryHandlers}.cs + Rendering/* · Infrastructure/ContentComposition/Rendering/{PdfSharpContentSetRevisionRenderer, HttpContentArtifactStore}.cs · Application/Features/ContentComposition/Claims/ClaimUsageQueryHandlers.cs (kullanım sorgu deseni) · Persistence/DependencyInjection.cs · memory crm-new-aggregate-classmap-guid, crm-standalone-mongo-transaction-fallback.

NE:
 1) Render: POST paths/{id}/revisions/{revId}/render (publish izni; onaysız 409 revision_not_approved; idempotent aynı ContentId). IKnowledgePathRevisionRenderer + PdfSharp (MigraDoc yardımcılarını ortaklaştır): onay kodu {PathCode}-v{PathVersion}-R{n}, bağlam (zincir, ülke adı, dil adı, ürün, kitle), dal-öncelikli adımlar (içerik başlığı+sürüm), adım iddiaları (kod + yol dilindeki ülke metni + niteleyici), uyum özeti, MLR turu (adım adı/karar/kişi/tarih/yorum), zaman. IContentArtifactStore → RenderedArtifacts{Kind pdf, ContentId, Checksum, ByteSize, MediaType, FileName, RenderedAt/By}. GET …/revisions/{revId}/artifact?kind=pdf (revizyondan çözülür; başka tenant 404).
 2) Yayın: POST paths/{id}/revisions/{revId}/release (publish izni). Ön koşullar 409: revision_not_approved, artifact_missing, revision_superseded (son onaylı + yol approved), component_not_published, component_language_mismatch, claim_not_approved/claim_no_country_version/claim_language_mismatch, chain_conformance_failed; SoD yayınlayan ≠ gönderen 403 sod_submitter_cannot_release (KnowledgePathReleaseRules.CanRelease tek nokta). Etki: ReleaseState Released; yol published + StepSetFrozenAt + PublishedAt/By; aynı PathCode önceki yayındaki sürüm inactive; eski sürümü sürüme sabitlenmiş yayınlanmış yolculuk aşaması kullanıyorsa previous_path_in_use uyarısı + aşama adları. Idempotent. Tek yazım ya da telafi.
 3) Geri çekme: POST …/revisions/{revId}/withdraw {reason} (400 reason_required; publish izni; yalnız yayınlanmış). Yayınlanmış yolculuk aşaması kullanıyorsa 409 path_in_use + aşama adları (hiçbir şey değişmez); değilse revizyon Withdrawn + yol inactive.
 4) Kullanım: GET paths/{id}/usage → journeys[]{journeyId, code, name, status, stageCode, stageName, pinPolicy, pinnedVersion?} + strategyTemplates[]{templateId, code, name, status} (knowledge-path bağları).
 5) Ortaklaştır: SB-2 yayın kuralları (bileşen yayında, dil, iddia kullanılabilirliği, ülke sürümü, yolculuk kullanım kontrolü) tek yerde (Features/Knowledge/Chain ya da Path/Release); ContentSetReleaseProducer bunları çağırsın, davranışı değişmesin.
 6) Zincirsiz eski yolun mevcut publish ucu DOKUNMA.
KORU/YAPMA: Web/Platform/Auth DOKUNMA; HTML render YOK; İçerik Seti/SB-2 davranışı DEĞİŞMEZ (yalnız ortak kural); ziyaret çözücüsü DEĞİŞMEZ; ham repository yazması yok; yeni alanlar class-map'e; işlem yoksa telafi.
DOĞRULA (E2): cd C:\tmp\kp-3; dotnet test services/Diten.CrmService/tests/Diten.CrmService.Application.Tests -c Release --nologo → 0 kırmızı (taban 2167/0/5; bilinen sıra flake'i hariç); dotnet test frontend/Diten.Web.Tests -c Release --nologo → 0 kırmızı (taban 368); build 0 hata. Yeni testler: render (onaysız 409, idempotent, onay kodu, içerik: dal-öncelikli + iddia metni + MLR turu, %PDF), artifact okuma (revizyondan, başka tenant 404), yayın ön koşulları (her kod), SoD yayınlayan=gönderen 403, yayın etkisi (published, donma, önceki inactive, previous_path_in_use + aşama adları), idempotent yayın, geri çekme (reason_required, path_in_use 409 + adlar, inactive), kullanım okuması, eski yol publish korunur, SB-2 testleri yeşil. Sabotaj: SoD yayın + iddia kullanılabilirlik ön koşulu + path_in_use testleri kırmızıya dönmeli. Commit ("feat(crm): WP-KP-3 — knowledge path render, release, withdrawal, usage" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: eski sürümü inactive yapmak V-P10/sürüm kurallarıyla çelişiyorsa ya da FU01 ContentMessagingArtifacts Bilgi Yolu revizyonunu sahip öğe olarak kabul etmiyorsa DUR + raporla.
```

---

## §37 — CT bağımsız doğrulama (2026-10-01) — **ACCEPTED (E2)**
- **Commit:** ajan `ca7bfb12` → `test/crm-content-visit-e2e` üzerine rebase (REQ-WCN-01 doküman commit'inden sonra) → fast-forward. 14 dosya, yalnız CrmService.
- **Diff (K13 okuma):**
  - **Yeni `Path/Release/`:** kurallar, handler'lar, sözleşmeler, renderer arayüzü.
  - **`KnowledgePathReleaseRules.CanRelease`:** yayınlayan ≠ gönderen (`CreatedBy` ve `ReviewRound.SubmittedBy`, kişi bazında) — tek nokta.
  - **`MigraDocPdf` ortak yardımcı:** set renderer'ı ona geçti, set çıktısı değişmedi.
  - **SB-2 üreticisi** ortak kuralları çağırıyor (+22 satır).
  - **Class-map'ler** KP-2'den mevcut.
- **CT testleri:** CRM **2186/0/5** (2167 + 19), Web **368/0**.
- **CT sabotajı:** `CanRelease` her zaman true → `KnowledgePathRelease` 1 kırmızı. Kod geri alındı. Ajan: SoD + iddia kapısı (2) + `path_in_use`.
- **DUR yok:**
  - FU01 sahip tipini doğrulamıyor (revizyon id kabul);
  - eski sürümü `inactive` yapmak V-P10 ile çelişmiyor.
- **Ajanın bulup düzelttiği hata:** `previous_path_in_use` yalnız **sürüme sabitlenmiş** aşamaları saymalı. "En son yayın" politikalı aşamalar da uyarı üretiyordu; düzeltildi. Geri çekme ve SB-2 davranışı aynı.
- **E4:** KP-2-CFG + KP-UI-2 sonrası CT.
