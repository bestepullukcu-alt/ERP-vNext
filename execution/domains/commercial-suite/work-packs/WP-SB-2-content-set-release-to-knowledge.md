# WORK PACKAGE — WP-SB-2 · İçerik Seti yayını → Bilgi İçeriği ("birleştirilmiş sunum") + Bilgi Yolu (zincir sırasıyla)

> **CT (SoR).** Karar: `SCMM-studio-knowledge-bridge-decision.md` §2 (A+D, 2026-09-28) ve §5 yeniden analiz (2026-09-29).
> - Kullanıcı sırayı seçti: **önce SB-2** (2026-09-29).
> - SB-1 (Kapsam seçicileri) ve SB-3 (ziyaret yol adımlarını kullanır) **ayrı paketler**, bu pakette YOK.
>
> **Çalışma yeri:** worktree `C:\tmp\sb-2`, dal `wp/sb-2`, taban `test/crm-content-visit-e2e`. Commit bu dala, push YOK.

## Sorun
İçerik Stüdyosu'nda hazırlanan set onaylanıp yayınlanıyor, ama çıktısı sahaya ulaşmıyor. Yayın (`ReleaseContentSetRevisionHandler`, `ContentSetRevisionCommandHandlers.cs:375-463`):
- yalnız `revision.ReleaseState` yazıyor ve bir log denetim olayı atıyor;
- KnowledgeContent, KnowledgePath ya da domain olayı üretmiyor.

Bu yüzden yolculuk ve ziyaret, Stüdyo'da kurulan anlatıyı hiç görmüyor. Zincir editöründeki "Çıktılar" paneli de sabit "—".

## Kanıt (CT)
- **ContentSet** (`Domain/Entities/ContentSet.cs`):
  - `Template` (`ConceptChainTemplateId` + `ChainVersion`), `Scope?` (`ContentScopeId` + `ScopeVersion`);
  - `SelectedComponents[]{SelectionId, KnowledgeContentId, ContentVersion, LanguageCode, Role, Arrangement{TemplateStepId (= ConceptTypeId), BranchId (= BranchCode), Position}}`;
  - `SelectedClaims[]{ClaimId, ClaimVersion, Arrangement}`.
- **Revizyon** (`ContentSetRevision.cs`): submit anında dondurulmuş kopya; `RenderedArtifact{ContentId, Checksum, MediaType, FileName}`; `ReleaseState`. Onay → render → yayın; yayınlayan ≠ gözden geçiren (403).
- **Zincir** (`ConceptChainTemplate.cs`): `SubjectId`, `OrderedConceptTypes[]` (omurga sırası), `Branches[]`, `ForWhomAudienceProfileIds[]`.
- **Konu** (`Subject.ExternalReferences[]`): MDM Global Product bağı (6 alanlı ExternalReference, `IsPrimary`).
- **KnowledgeContent:** `ContentType` sözlüğünde `assembled-presentation` YOK (`KnowledgeContent.cs:211-224`). `ClaimRefs` + BE-6 yayın kapısı var. Dikkat: `KnowledgeContent.ContentSetId` dil varyantı grubudur, İçerik Seti değildir (isim çakışması).
- **KnowledgePath:** adım `ContentId` zorunlu; durumlar draft / review / approved / published / inactive / archived; yayında adım seti donduruluyor.
- **Ziyaret:** SB-2 ziyareti değiştirmez. Yol yayınlanınca bir yolculuk aşaması onu kullanabilir (SB-3 ile ziyarete iner).

## NE

### 1) Yayın iki kayıt üretir (aynı işlemde; atomik değilse telafi)
**A. Birleştirilmiş sunum — KnowledgeContent** (`ContentType = assembled-presentation`, yeni sözlük değeri)
- **Başlık / kod:** set adı ve kodundan türetilir. Kod benzersiz; çakışırsa sürüm soneki alır.
- **Konu:** zincirin `SubjectId`'si.
- **Ürün:** konunun birincil MDM Global Product referansı. Yoksa boş.
- **Dil:** bileşenlerin dili. Tek dil değilse → **DUR**.
- **Kitle:** zincirin `ForWhomAudienceProfileIds`'i tek ise o. Birden çoksa ya da yoksa boş; karar raporlanır.
- **Varlık:** `ContentAssetRef` = render edilen PDF'in `RenderedArtifact.ContentId`'si.
- **Kaynak:** `Source = content-studio` (yeni sözlük değeri). `ContentVersion` = revizyon numarası.
- **Durum:** `published`.
- **ClaimRefs:** setin `SelectedClaims`'inden **otomatik**.
  - Kapsamın `MarketRefs`'inde **tek bir** COUNTRY_CODES kodu varsa → o ülkenin sürümüne bağla (country ref).
  - Aksi halde çekirdek ref.
  - BE-6 kuralları aynen uygulanır (ürün, onay, dil).

**D. Bilgi Yolu — KnowledgePath** (zincir sırasıyla)
- Kod / ad setten; konu, dil ve kitle A ile aynı.
- **Adımlar:** her bileşen bir adım; `ContentId` = bileşenin `KnowledgeContentId`'si. Sıra:
  1. omurga adımları `OrderedConceptTypes` sırasıyla;
  2. dal bileşenleri, bağlı oldukları omurga adımından hemen sonra;
  3. aynı adım içinde `Position`.
  - Sıralama, Stüdyo'nun mevcut yerleşim anlamıyla aynı olmalı (DESIGN-SCMM-14'ü oku).
- Adım tipi ve kavram bilgisi mevcut adım alanlarına uygun eşlenir: `ConceptNodeId` bileşen içeriğinin kavram düğümü, yoksa boş.
- Yol **published** yaratılır; adım seti dondurulur.

**Ortak kurallar**
- **Geri iz:** üretilen içerik ve yola kaynak referansı: `{ContentSetId, RevisionId, ConceptChainTemplateId, ChainVersion}`.
- **Revizyon bağı:** revizyona `ProducedKnowledgeContentId` + `ProducedKnowledgePathId`.
- **Idempotent:** aynı revizyon ikinci kez üretmez.
- **Sürümleme:** aynı set yeniden yayınlanırsa (yeni revizyon) yeni **sürümler** açılır, önceki üretilenler supersede / inactive olur. İçerik ve yolun mevcut sürümleme kuralları kullanılır; yeni kural icat edilmez.

### 2) Yayın ön koşulları (fail-closed)
- Mevcut koşullar aynen (render, reviewer, SoD) kalır. Yeni koşullar:
  - Setteki **her iddia** kullanılabilir olmalı (onaylı ya da gözden geçirilmeli; country ref'te dil uyumlu). Değilse 409, BE-6 kodlarıyla (`claim_not_approved` / `claim_language_mismatch`).
  - Setin **en az 1 bileşeni** olmalı. Bileşenlerin hepsi yayında olmalı (yol adımı yayında olmayan içerikle kurulamaz). Değilse 409 `component_not_published`.
- Üretim başarısızsa yayın **gerçekleşmez**: `ReleaseState` yazılmaz ya da telafiyle geri alınır.

### 3) Geri çekme
- Revizyon geri çekilince üretilen içerik → `inactive`.
- Yol → `inactive`, **ama yalnız** yayınlanmış bir yolculuk aşaması o yolu kullanmıyorsa. Kullanıyorsa yol dokunulmadan kalır ve geri çekme sonucu `path_in_use` uyarısı taşır.

### 4) Okuma ve görünürlük
- Revizyon DTO'su üretilen içerik ve yolun kimliğini ve kodunu taşır.
- **Web YOK** (CT düzeltmesi, 2026-09-29): İçerik Seti revizyon yaşam döngüsünün (gönder / karar / render / yayın / geri çek) Web arayüzü **hiç yok**. CRM `ContentSetRevisionsController` var; Web proxy ve ekran yok. Arayüz, kullanıcının mockup'ına göre ayrı pakette (**SB-UI**) yapılacak. Bu paket yalnız backend + DTO; Web testleri yalnız regresyon.
- Zincir editörü "Çıktılar" paneli **bu pakette DEĞİL** (SB-2b).

## KORU / YAPMA
- **Ziyaret çözücüsü DEĞİŞMEZ** (SB-3). İçerik Kapsamı ref'leri DEĞİŞMEZ (SB-1).
- İddia, iş akışı ve Platform DOKUNMA.
- KnowledgeContent ve KnowledgePath'in mevcut doğrulama ve sürümleme kuralları atlanmaz. Üretim, mevcut komut / servis yollarından geçer; repository'ye ham yazma YOK.
- **CRM class-map tuzağı:** yeni alanlar `RegisterClassMaps`'e (GUID → string) eklenir (memory `crm-new-aggregate-classmap-guid`, `crm-classmap-rejects-unknown-elements`).
- Standalone Mongo'da işlem yoksa telafi deseni (memory `crm-standalone-mongo-transaction-fallback`).
- Tarayıcıda ayrı sekme, canlı yazma YOK.
- **DUR:**
  - Bileşenler tek dilde değilse;
  - Stüdyo yerleşim anlamı (dal ↔ omurga bağı) koddan tek anlamlı çıkmıyorsa;
  - mevcut sürümleme kuralları yeni sürüm açmayı desteklemiyorsa.
  - Her durumda kural uydurma, raporla.

## Acceptance
- **E2:**
  - CRM testleri 0 kırmızı (bilinen sıra flake'i hariç). Web testleri 0 kırmızı (taban 333). Build'ler 0 hata.
  - Yeni testler:
    - yayın → A + D üretimi (alanlar, ClaimRefs country / çekirdek seçimi, adım sırası omurga + dal + Position);
    - idempotent ikinci yayın;
    - iddia onaysız → 409 ve hiçbir şey üretilmez;
    - yayında olmayan bileşen → 409;
    - geri çekme → inactive / `path_in_use`;
    - yeni sürüm yayını → önceki supersede;
    - class-map round-trip (GUID alanları string).
  - **Sabotaj:** adım sıralaması ve iddia ön koşulu testleri kırmızıya dönmeli.
- **E4 (CT):** TPL-ALMIBA-01 yayınla → ALMIBA seti (bileşenler + CLM-ALMIBA-02) → gönder / onayla / render / yayınla → KC (assembled-presentation, tr, TR ref) + KP (zincir sırası) oluşur → CEJ-ALMIBA-HD aşaması bu yolu seçebilir.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-architect.md] @[.antigravity/agents/frontend-ui-ux.md]
WP: WP-SB-2 · İçerik Seti yayını → Bilgi İçeriği ("birleştirilmiş sunum") + Bilgi Yolu (zincir sırasıyla)
Repository: C:\tmp\sb-2 (worktree) · Branch: wp/sb-2 · commit bu dala, push YOK

Amaç: Stüdyo çıktısı sahaya ulaşmıyor — set yayını yalnız ReleaseState + log yazıyor. Yayın; A) assembled-presentation KnowledgeContent (ClaimRefs otomatik) ve D) zincir sırasıyla published KnowledgePath üretsin. Ziyaret (SB-3) ve Kapsam seçicileri (SB-1) bu pakette YOK.

Önce oku: execution/domains/commercial-suite/work-packs/WP-SB-2-content-set-release-to-knowledge.md · …/SCMM-studio-knowledge-bridge-decision.md (§2 + §5) · execution/**/DESIGN-SCMM-14-content-set-assembly.md · …/WP-SCMM-17-release-managed-withdrawal.md · …/WP-CL-BE-6-claims-usage-content-links.md · services/Diten.CrmService/src/Diten.CrmService.Domain/Entities/{ContentSet, ContentSetRevision, ConceptChainTemplate, KnowledgeContent, KnowledgePath, Subject}.cs · Diten.CrmService.Application/Features/ContentComposition/ContentSetRevisions/ContentSetRevisionCommandHandlers.cs (Release 375-463, Withdraw 472-540) · Features/Knowledge/Content (KnowledgeContentClaimLinks, command handlers) · Features/Knowledge/KnowledgePath (create/publish/version) · Features/Knowledge/ContentEngagementJourney (stage path kullanımı — geri çekme kontrolü) · Infrastructure class-map kaydı · memory crm-new-aggregate-classmap-guid, crm-classmap-rejects-unknown-elements, crm-standalone-mongo-transaction-fallback.

NE:
 1) Yayın iki kayıt üretir. A: ContentType assembled-presentation (yeni sözlük değeri); konu = zincir SubjectId; ürün = konunun birincil MDM Global Product ExternalReference'ı; dil = bileşen dili (tek değilse DUR); kitle = zincir ForWhom tekse; ContentAssetRef = RenderedArtifact.ContentId; Source content-studio (yeni); published; ClaimRefs SelectedClaims'ten (kapsam MarketRefs'te tek COUNTRY_CODES kodu → country ref, yoksa çekirdek). D: published KnowledgePath; adımlar = bileşenler (ContentId = KnowledgeContentId); sıra omurga OrderedConceptTypes → dal bileşenleri bağlı omurga adımından hemen sonra → Position (Stüdyo yerleşim anlamıyla aynı); ConceptNodeId bileşen içeriğinden; adım seti dondurulur. Ortak: kaynak geri izi {ContentSetId, RevisionId, ConceptChainTemplateId, ChainVersion}; revizyonda ProducedKnowledgeContentId/ProducedKnowledgePathId; idempotent; yeni revizyon yayını mevcut sürümleme kurallarıyla yeni sürüm + öncekini supersede/inactive.
 2) Yayın ön koşulları (fail-closed, mevcutlar aynen): her iddia kullanılabilir (BE-6 kodları, 409); ≥1 bileşen ve hepsi yayında (409 component_not_published); üretim başarısızsa yayın gerçekleşmez (işlem ya da telafi).
 3) Geri çekme: üretilen içerik inactive; yol inactive yalnız yayınlanmış bir yolculuk aşaması kullanmıyorsa, kullanıyorsa dokunma + path_in_use uyarısı.
 4) Revizyon DTO'su üretilenleri (id + kod) taşır. Web arayüzü YOK — revizyon/yayın ekranı Web'de hiç yok, ayrı paket (SB-UI, mockup sonrası); Web'e dokunma. Zincir editörü Çıktılar paneli YOK (SB-2b).
KORU/YAPMA: ziyaret çözücüsü ve İçerik Kapsamı ref'leri DEĞİŞMEZ; iddia/iş akışı/Platform DOKUNMA; içerik/yol doğrulama ve sürümleme kuralları atlanmaz, mevcut komut/servis yolları kullanılır (ham repository yazması YOK); yeni alanlar class-map'e (GUID string); işlem yoksa telafi; tarayıcıda ayrı sekme, canlı yazma YOK.
DOĞRULA (E2): cd C:\tmp\sb-2; dotnet test services/Diten.CrmService/tests/Diten.CrmService.Application.Tests -c Release --nologo → 0 kırmızı (bilinen sıra flake'i hariç); dotnet test frontend/Diten.Web.Tests → 0 kırmızı (taban 333); build'ler 0 hata. Yeni testler: A+D üretimi (alanlar, ClaimRefs country/çekirdek, adım sırası omurga+dal+Position), idempotent, onaysız iddia 409 + hiçbir şey üretilmez, yayında olmayan bileşen 409, geri çekme inactive/path_in_use, yeni sürüm supersede, class-map round-trip. Sabotaj: adım sıralaması + iddia ön koşulu testleri kırmızıya dönmeli. Commit ("feat(crm): WP-SB-2 — content set release produces assembled-presentation content + chain-ordered knowledge path" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: bileşenler tek dilde değilse, Stüdyo dal↔omurga yerleşim anlamı tek anlamlı değilse, mevcut sürümleme yeni sürüm açmayı desteklemiyorsa DUR + raporla (kural uydurma).
```

---

## Kullanıcı kararları — DUR yanıtları (2026-09-29)
Ajan DUR raporu:
- v2 zincirde "omurga adımına bağlı dal" kavramı yok. Bileşenler dala yerleşiyor (`ContentSetArrangement.ValidateSlot`); omurga dallardan türetiliyor.
- Set bileşenlerinde dil kısıtı yok.

**Kararlar:**
1. **Yol adım sırası = dal-öncelikli** (Stüdyo çalışma alanıyla birebir, `workspace.js:67-80`): dal `SortOrder` → dal içindeki adım sırası (`Steps` liste sırası) → `Position`. Dalsız eski şablonda: `OrderedConceptTypes` sırası → `Position`. Bu karar NE §1-D'deki "omurga → dal bağlı adımdan sonra" ifadesinin yerine geçer.
2. **Karışık dil → yayın engellenir:** 409 `component_language_mixed` (hangi bileşenlerin hangi dilde olduğu mesajda). Hiçbir şey üretilmez; dil tahmin edilmez. Tek dilse A ve D o dili kullanır.
3. **Sürümleme (ajan bulgusu, kabul):**
   - Yol: `CreateKnowledgePathVersionHandler` ile yeni sürüm. Eski yayındaki sürüm, çakışma (409) olmaması için `inactive` yapılır ya da `EffectiveTo` ile kapatılır; hangisinin mevcut kurallara uygun olduğunu ajan seçer ve raporlar.
   - İçerik: sürüm komutu yok → soneki olan yeni kodla yeni kayıt + eskisi Update ile `inactive`.

**Devam prompt'u (aynı worktree, aynı dal):**
```text
WP-SB-2 devam — DUR yanıtları WP dosyasının sonunda ("Kullanıcı kararları"). 1) Yol sırası dal-öncelikli: dal SortOrder → dal Steps sırası → Position (dalsız eski şablon: OrderedConceptTypes → Position). 2) Karışık dil: 409 component_language_mixed, hiçbir şey üretilmez. 3) Sürümleme bulgularını uygula (yol: CreateKnowledgePathVersion + eskiyi inactive/EffectiveTo, seçimini raporla; içerik: sonekli yeni kod + eskisi inactive). Paketin geri kalanı aynen. Sabotaj testlerine dal-öncelikli sıra + karışık dil 409 eklensin. Aynı DOĞRULA/commit kuralları.
```
