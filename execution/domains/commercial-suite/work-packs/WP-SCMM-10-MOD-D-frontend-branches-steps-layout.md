# WORK PACKAGE — WP-SCMM-10-MOD-D · ChainTemplate Branches/Steps layout iyileştirme (frontend)

> **CT (SoR).** Module **MOD-0162** (SCMM-10). Branch `feature/scmm-content-studio`. **Yalnız frontend** (Diten.Web), **yeni CSS/style YOK** — mevcut kart + pagination class'larını reuse. Owner isteği: yan-yana kart layout + paging (scroll yok) + merkezi model korunur. **Bu WP paketli; dispatch owner'a bırakıldı (CRM ile paralel çakışmasın diye).**

## Ölçülmüş girdi (CT)
- **Create + Edit AYNI renderer:** `TemplateCreate.cshtml` + `TemplateEdit.cshtml` ikisi de `_TemplateForm.cshtml` + `wwwroot/assets/js/CRM/KnowledgeConcepts/template-form.js` kullanıyor → tek yerde düzeltince ikisi de düzelir.
- **Merkezi model yerinde (WP-A/C):** step artık refs-free `{conceptTypeId, min, max}`; Moderator/ForWhom **template-seviyesi** (Identity & Classification). Step'e Moderator/Audience GERİ EKLENMEYECEK.
- **Mevcut durum (WP-C, 1. resim):** Branches = dikey `.diten-checkitem` checklist + compose-row.
- **Hedef görsel referansı (WP-C öncesi, commit `67262f31^` template-form.js):** step'ler **yan-yana `<div class="card border shadow-none">` kartları** + aralarında `→`; ama o tasarım (i) step-seviyesi Moderator/Audience'lı (KALDIRILDI, geri ekleme) ve (ii) **yatay-scroll**'luydu (istenmiyor). Bu referanstan **yalnız kart layout'u** alınır.
- **Pager:** mevcut Bootstrap `.pagination`/`.page-item` class'ları + basit client-side sayfalama (CRM'de örnekler: Segments/form.js vb.). Yeni CSS yok.
- **Kart standardı:** `card border shadow-none` + `card-body p-3` (repo'nun mevcut kart deseni). Yeni style yazma.

## Kapsam (frontend, yalnız template-form.js + _TemplateForm.cshtml + gerekiyorsa L10n)
1. **Step'ler yan-yana kart:** her step bir kart (`card border shadow-none` reuse), içinde ConceptType etiketi + Min/Max + move (←/→) + remove; kartlar arasında `→` (chain sırası). Step refs-free kalır (Moderator/Audience YOK).
2. **Yatay scroll YOK → step paging:** bir branch'ta çok step olduğunda kartlar **satıra sığar + sayfalanır** (ör. sabit sayıda kart/sayfa, `.pagination` prev/next + sayfa göstergesi). `overflow-x`/`flex-nowrap`-scroll KULLANMA.
3. **Branch paging:** çok branch (ör. 100) → dikey sonsuz liste yerine **branch pager** (aynı `.pagination` deseni; ör. 1 branch/sayfa veya N branch/sayfa + prev/next).
4. **Compose-then-add korunur:** ConceptType seç + Min/Max + Add (step ekleme) çalışmaya devam.
5. **Publish-freeze (D-f) korunur:** yayımlı template read-only (mevcut davranış).
6. **Merkezi Moderator/ForWhom (Identity & Classification) DEĞİŞMEZ.**
7. Create + Edit ikisi de otomatik (paylaşılan renderer).

## YAPMA
- **Yeni CSS/style dosyası ya da inline style yazma** — yalnız mevcut class'lar (card/pagination/form-select-sm/btn-icon vb.).
- Step'e Moderator/Audience geri ekleme (merkezi kaldı). Backend/DTO/API (WP-A ✓). Reference set (WP-B ✓). Faz-2 position lookup. Content Set/eligibility/resolver. Başka modül. Yeni nav sayfası. Yatay scroll ile "çözme".

## Acceptance
- **E2:** `frontend/Diten.Web.Tests` baseline-diff yeşil. Step'ler yan-yana kart + `→`; çok step→**paging (yatay scroll yok)**; çok branch→paging; step refs-free; compose-then-add + publish-freeze korunur; Create+Edit aynı; **yeni CSS/style eklenmedi** (git diff'te yalnız mevcut class kullanımı). NavGuard etkilenmez (yeni nav sayfası yok).
- **E4:** A2d manuel test — CHAIN-CARN-01 çok-adımlı branch'ta scroll yok, paging çalışır; merkezi Moderator/ForWhom yerinde.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner (CRM paralel işi bitince)

```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-SCMM-10-MOD-D · Prompt v1.0  (ChainTemplate Branches/Steps yan-yana kart + paging — MOD-0162, frontend)

Repository: C:\Users\user\Desktop\ERP-vNext
Branch: feature/scmm-content-studio · Expected HEAD: <dispatch anındaki HEAD> · Worktree: ana checkout
(CRM paralel işi varsa: bu WP yalnız KnowledgeConcepts template-form.js + _TemplateForm.cshtml + o sayfanın L10n'ine dokunur; CRM işiyle dosya çakışmasını kontrol et, çakışırsa sıralı git.)

Önce oku:
1. execution/domains/commercial-suite/work-packs/WP-SCMM-10-MOD-D-frontend-branches-steps-layout.md (bu WP)
2. frontend/Diten.Web/wwwroot/assets/js/CRM/KnowledgeConcepts/template-form.js (MEVCUT: dikey .diten-checkitem checklist — bunu değiştireceksin)
3. GÖRSEL REFERANS (yalnız kart layout için): `git show 67262f31^:frontend/Diten.Web/wwwroot/assets/js/CRM/KnowledgeConcepts/template-form.js` — yan-yana `card border shadow-none` step kartları + `→`. AMA o tasarımın step-seviyesi Moderator/Audience'ını ALMA (merkezi kaldı) ve yatay-scroll'unu ALMA (paging yapacaksın).
4. Pager deseni: mevcut Bootstrap `.pagination`/`.page-item` + bir CRM client-side pager örneği (ör. wwwroot/assets/js/CRM/Segments/form.js). Yeni CSS YOK.
5. _TemplateForm.cshtml (Branches container markup) + _TemplateFormL10n.cshtml (gerekiyorsa yeni etiket: prev/next/page).

NE (frontend):
 1) Step'leri yan-yana KART yap (card border shadow-none reuse): ConceptType etiketi + Min/Max + move(←/→) + remove; kartlar arası `→`. Step model refs-free {conceptTypeId,min,max} KALIR.
 2) Yatay scroll KALDIR → step PAGING: kartlar satıra sığar, sabit sayıda kart/sayfa, .pagination prev/next + sayfa göstergesi. overflow-x/flex-nowrap-scroll kullanma.
 3) Branch PAGING: çok branch için pager (aynı .pagination deseni).
 4) Compose-then-add (ConceptType+Min/Max+Add) ve publish-freeze read-only korunur.
 5) Merkezi Moderator/ForWhom (Identity & Classification) DOKUNMA.
NASIL: yalnız MEVCUT class'lar (card/pagination/form-select-sm/btn-icon/badge). Görsel referans 67262f31^ ama refs-free + paging. Create+Edit paylaşılan renderer → ikisi de otomatik.
YAPMA: yeni CSS/style/inline-style; step'e Moderator/Audience geri; backend/DTO/API; reference set; Faz-2 position; Content Set/eligibility; başka modül; yeni nav sayfası; yatay scroll ile çözme.
DOĞRULA (E2): frontend/Diten.Web.Tests baseline-diff yeşil; step yan-yana kart+paging (scroll yok); branch paging; refs-free; compose+freeze korunur; git diff'te YENİ CSS/style YOK. Ayrı commit. §22 TÜRKÇE. K13 — CT bağımsız doğrular.

Durma koşulları: paging mevcut class'larla yapılamıyorsa (yeni CSS gerekiyorsa DUR+raporla, yazma) · backend sözleşmesi beklenenden farklıysa · kapsam frontend/KnowledgeConcepts dışına taşarsa · CRM paralel işiyle dosya çakışması → DUR + raporla.
```

## §37 CT bağımsız doğrulama → (agent sonrası, dispatch owner'da)
```text
Commit: <agent> · Agent: <PASS/FAIL> · CT: <PENDING>
```
- İzole worktree → dotnet test frontend/Diten.Web.Tests baseline-diff; template-form.js logic-read (yan-yana kart + step/branch paging + refs-free + compose/freeze); **git diff'te yeni CSS/style yok** doğrula; yalnız KnowledgeConcepts frontend değişti.

## Kalan (bu WP dışı)
- A2d manuel test (nihai model + yeni layout) → A3→A8 → sync/PR. · Faz-2 ModeratorRoleType=position → MOD-0288 positions lookup.
