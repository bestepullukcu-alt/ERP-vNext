# WORK PACKAGE — WP-FREQ-DET-I · Çözümleme tab rötuşları (Kayıt picked-box, app-field, CEVAP grey, açıklayıcı reason) (frontend)

> **CT (SoR).** MOD-0165-FU03. Branch `feature/scmm-content-studio` (`4983dc83` üstü). DET-H sonrası Çözümleme tab tasarım rötuşları. **Frontend only** (_Resolve.cshtml + resolve.js + visit-frequency-details.css + L10n). Backend DEĞİŞMEZ (/resolve verisi yeterli; açıklayıcı reason = L10n).

## İstekler
1. **Kayıt picker seçim sonrası:** editördeki "Hangi kayıt" deseni gibi — seçilince **picked-box** (tip çipi + isim + dış kod) + **"değiştir"** butonu (mockup resim-2). Şu an düz select. Boşken app select ile seç, seçilince picked-box; "değiştir" tekrar seçime döner. targetId/context resolve.js sözleşmesi korunur.
2. **Kayıt + Dönem + Tarih alanları task-create:** `form-label fw-medium` + `diten-field` (ikon) + `form-select`/`form-control` (Kayıt select2 opsiyonel; Dönem=form-select, Tarih=form-control date). Editör app-field deseni.
3. **"Kural basit" kutusu** (`.vfp-rs-simple`): background → **bg-body** (`var(--bs-body-bg)`; şu an gri).
4. **CEVAP verdict kutusu → mockup nötr-gri:** bg `var(--bs-tertiary-bg)` (mockup rgb(248,247,250)) + border `var(--bs-border-color)`, radius6, padding20; "CEVAP" label(11px uppercase muted) + 26px/600 başlık + 13px muted desc + sağda rozet. **Amber/renk-kodlama YOK** — nötr gri (kullanıcı: "grili gibi"). Rozet metni verdict'e göre (kural yok/çözüldü/çakışma/uygulanmaz), gri/muted.
5. **"DENK GELEN DİĞER KURALLAR" aday kartı SOL BORDER sıkıntısı:** aday kartlarının sol kenar/accent'i bozuk → düzelt (seçilen=yeşil sol vurgu; aday=düz border, taşma/çift-border yok).
6. **Aday reason'ları AÇIKLAYICI (mockup):** kısa "Segment bağlamı eksik" yerine mockup gibi tam cümle — reason kodu/status'e göre L10n:
   - not-effective → "Kural var ama seçtiğiniz tarihte geçerli değil."
   - specificity kaybeden (daha dar kazandı) → "Daha dar kapsamlı bir kural olduğu için bu kural uygulanmadı."
   - daha geniş → "Bölge/segment kuralı, daha dar kurallardan geniş kapsamlı."
   - last-resort/yedek → "Daha dar bir kural bulunmazsa bu kural devreye girer."
   - seçilen (specificity) → "En dar kapsamlı kural olduğu için seçildi." (priority→"Önceliğe göre seçildi.")
   - scope-mismatch (segment/campaign/cycle/business) → "Seçilen bağlamda geçerli değil (… eksik)."
   (reasonLabels genişlet; humanize fallback.)
7. **Aday tip alt-satırı daha açıklayıcı:** "Kişiye özel"→ targetType friendly + isim/bağlam nerede varsa (contact→"Kişiye özel", campaign-target→"Kampanya hedefi", territory-node→"Saha alanı", segment→"Segmentteki tüm hedefler"). Uydurma bağlam yok; isim resolve.js nameOf.

## KORU / YAPMA
- Backend DEĞİŞMEZ (/resolve verisi + L10n). Liste/editör/detay(Details.cshtml/details.js)/Segment/resolve engine DOKUNMA (yalnız _Resolve.cshtml + resolve.js + css + resx + gerekirse Index vfp-dr-l10n köprüsü). targetId/context/IncludeDiagnostics sözleşmesi korunur. Uydurma reason/bağlam YOK (gerçek /resolve + reason kodu + L10n). app-card/tema/L10n köprüsü.

## Acceptance
- **E2:** Diten.Web.Tests 137/0. git diff: _Resolve.cshtml + resolve.js + css + resx (+ Index köprü). Backend/liste/editör/detay/Segment diff YOK.
- **E4:** Kayıt seçilince picked-box+değiştir; Kayıt/Dönem/Tarih app-field; Kural basit bg-body; CEVAP nötr-gri kutu; aday kart sol-border düzgün; reason açıklayıcı cümleler; tip alt-satırı açıklayıcı.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-FREQ-DET-I · Çözümleme tab rötuşları (MOD-0165-FU03, frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/scmm-content-studio · Expected HEAD: 4983dc83 üstü · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-FREQ-DET-I-resolve-tab-polish.md · frontend/Diten.Web/Views/CRM/VisitFrequencyPolicies/{_Resolve.cshtml, Index.cshtml (vfp-dr-l10n)} + wwwroot/assets/js/CRM/VisitFrequencyPolicies/resolve.js (senaryo/picker/verdict/candidate render + reasonLabels) + wwwroot/assets/css/visit-frequency-details.css (.vfp-rs-*) · (referans "Hangi kayıt" picked-box + app-field) form.js/_Editor.cshtml.

NE (frontend; backend DEĞİŞMEZ):
 1) Kayıt picker: seçilince "Hangi kayıt" deseni picked-box (tip çipi+isim+dış kod)+"değiştir" (editör deseni); boşken app select. targetId/context resolve.js sözleşmesi korunur.
 2) Kayıt/Dönem/Tarih → app-field (form-label fw-medium + diten-field + form-select/form-control date).
 3) .vfp-rs-simple ("Kural basit") background → var(--bs-body-bg) (bg-body).
 4) CEVAP verdict kutusu → nötr gri (bg var(--bs-tertiary-bg) + border, radius6 padding20): "CEVAP" label + 26px/600 başlık + 13px muted desc + sağ rozet (muted, verdict metni). Amber/renk-kodlama KALDIR.
 5) Aday kartı SOL BORDER düzelt (seçilen yeşil sol vurgu; aday düz border, taşma/çift yok).
 6) Aday reason AÇIKLAYICI (reasonLabels genişlet, L10n 7 dil): not-effective→"Kural var ama seçtiğiniz tarihte geçerli değil."; specificity-loser→"Daha dar kapsamlı bir kural olduğu için bu kural uygulanmadı."; daha-geniş→"…daha geniş kapsamlı."; last-resort→"Daha dar bir kural bulunmazsa bu kural devreye girer."; selected-specificity→"En dar kapsamlı kural olduğu için seçildi."; selected-priority→"Önceliğe göre seçildi."; scope-mismatch→"Seçilen bağlamda geçerli değil (… eksik)." humanize fallback.
 7) Aday tip alt-satırı açıklayıcı (targetType friendly + isim/bağlam nerede varsa; uydurma yok, nameOf).
KORU/YAPMA: backend DEĞİŞMEZ; liste/editör/detay(Details.cshtml/details.js)/Segment/resolve engine DOKUNMA (yalnız _Resolve.cshtml+resolve.js+css+resx+gerekirse Index köprü); targetId/context/IncludeDiagnostics sözleşmesi korunur; uydurma reason/bağlam YOK; app-card/tema/L10n köprüsü.
DOĞRULA (E2): Diten.Web.Tests 137/0; git diff _Resolve+resolve.js+css+resx(+Index); backend/liste/editör/detay/Segment diff yok. Ayrı commit. §22 TÜRKÇE. K13.
Durma: picked-box targetId sözleşmesini bozuyorsa; kapsam _Resolve/resolve.js dışına taşarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-18) → **ACCEPTED (E2)**
```
Commit: d8377ac6 · Agent: PASS · CT: ACCEPTED E2 (izole temiz build) · /c/tmp/ct-deti-verify @d8377ac6
```
- ✅ Kapsam: _Resolve.cshtml + Index.cshtml(vfp-dr-l10n köprü) + resolve.js + visit-frequency-details.css + 7 resx. **KORU=0** (backend/liste/editör/details.js/Details.cshtml/Segment/resolve engine dokunulmadı; Reason_* değişmedi → detay reason kolonu korundu).
- ✅ Kayıt picked-box+değiştir (targetId/data-role/targetIdValue korundu) + Kayıt/Dönem/Tarih app-field; Kural basit bg-body; CEVAP nötr-gri (amber renk-kodlama kalktı); aday sol-border görünür (3px, seçilen yeşil); açıklayıcı reason (candWhy, gerçek alanlardan) + tip alt-satırı.
- ✅ **Koordinatör eki (mesajla, salt-css):** stat-strip gri panel→transparent+hairline; .vfp-det-cand sol-border görünür. Detay markup dokunulmadı.
- ✅ Build+test (CT izole, Release): Diten.Web.Tests **137/0**.
- ⚠️ **E4 bulgusu → DET-K:** Çözümleme "segment" senaryosu resolve query'sinde segmentId göndermiyor; segment-hedefli politikalar SegmentId context=targetId taşıdığı için hepsi "SegmentContextMissing/devrede değil" eleniyor. Fix DET-K (senaryo hedefin kendi boyutunu context gönderir).

**DET-I KOMPLE. Sıra: DET-K (Çözümleme senaryo context bug) + DET-J (WorkCenter tab).**
```
