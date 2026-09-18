# WORK PACKAGE — WP-FREQ-DET-M · Çözümleme CEVAP kutusu verdict-renkli (mockup birebir) (frontend)

> **CT (SoR).** MOD-0165-FU03. Branch `feature/scmm-content-studio` (DET-L landing sonrası HEAD üstü — resolve.js SIRALI). **E4 isteği:** CEVAP kutusu mockup'taki gibi verdict'e göre renk-kodlu olsun (5 durum). **DET-I kararını (nötr-gri) BİLİNÇLİ TERSİNE ÇEVİRİR** — kullanıcı mockup'ın tam halini (durum başına doğru renk: yeşil/amber/kırmızı/gri) gördü ve onu istiyor. **Frontend only** (`resolve.js` renderResolveResult verdict + `visit-frequency-details.css` `.vfp-rs-answer--*` + L10n 7 dil). Backend DEĞİŞMEZ.

## Mockup — 5 verdict durumu (kullanıcı resimleri)
| verdict | kutu | rozet (metin/ton) | başlık | açıklama |
|---|---|---|---|---|
| **resolved** (resim-2) | yeşil tint (`--bs-success-bg-subtle` + `--bs-success-border-subtle`) | "net sonuç" / success | "Ayda N ziyaret" | mevcut resolved açıklaması korunur |
| **conflict** (resim-5) | amber tint (`--bs-warning-bg-subtle` + `--bs-warning-border-subtle`) | "çakışma çözüldü" / warning | "Ayda N ziyaret" | "Aynı kapsamda iki kural vardı; daha yüksek öncelikli olan uygulandı. Diğerini gözden geçirmek isteyebilirsiniz." |
| **date-out** (resim-4, TÜREV) | kırmızı/pembe tint (`--bs-danger-bg-subtle` + `--bs-danger-border-subtle`) | "tarih dışında" / danger | "Bu tarihte geçerli kural yok" | "Hedefe uyan kurallar var ama seçtiğiniz tarih hiçbirinin geçerlilik aralığına girmiyor." |
| **unknown** (resim-3) | nötr gri (mevcut `--bs-tertiary-bg`) | "Bilinmiyor" / secondary | "Tanımlı frekans yok" | "Bu hedefe hiçbir politika denk gelmiyor; planlayıcı sistem varsayılanını kullanır." |
| **not_applicable** | nötr gri | mevcut / secondary | mevcut | mevcut |

## Türev verdict mantığı (frontend — backend DEĞİŞMEZ)
Backend `frequencyStatus`: resolved / conflict / unknown / not_applicable. Mockup'ın "date-out" durumu ayrı bir kod değil — **türet**:
```
displayVerdict(r, cands):
  v = norm(r.frequencyStatus); hasWinner = !!norm(r.selectedFrequencyPolicyId)
  if v === 'conflict' → 'conflict'
  if v === 'resolved' → 'resolved'
  if v === 'not_applicable' → 'not_applicable'
  // v === 'unknown' (kazanan yok):
  if cands.length > 0 && cands.every(c => norm(c.reason) === 'policy_not_effective') → 'date_out'
  else → 'unknown'
```
(resim-3 = tüm adaylar tarih-dışı → mockup resim-4'e "date_out" olarak dönüşür. Karışık reason → 'unknown'.)

## NE (frontend)
1. **CSS** (`visit-frequency-details.css`, `.vfp-rs-answer` ~544): tone modifier'lar ekle — `.vfp-rs-answer--success { background: var(--bs-success-bg-subtle); border-color: var(--bs-success-border-subtle); }`, `--warning` (warning-subtle), `--danger` (danger-subtle). Nötr (unknown/not_applicable) = mevcut sınıf (tertiary-bg), modifier yok. Tema-duyarlı (subtle token'lar dark'ta da çalışır). Başlık/label/explain renkleri mevcut (`--vfp-heading`/`--vfp-muted`) kalır — okunur kontrast korunur.
2. **resolve.js renderResolveResult** (~463-477):
   - `displayVerdict(r, cands)` türev fonksiyonu ekle (yukarıdaki mantık; cands zaten hesaplanıyor ama answer'dan SONRA — **cands hesabını answer'dan ÖNCEye al** ya da displayVerdict'i ladder sıralamasından sonra uygula; sıra: cands → displayVerdict → answer).
   - `.vfp-rs-answer` element'ine `vfp-rs-answer--${toneClass}` ekle (success/warning/danger; unknown/not_applicable→modifier yok).
   - Rozet: `badge(badgeText, badgeTone)` — display-verdict'e göre metin+ton (resolved→"net sonuç"/success, conflict→"çakışma çözüldü"/warning, date_out→"tarih dışında"/danger, unknown→"Bilinmiyor"/secondary, not_applicable→mevcut/secondary). L10n key'ler.
   - **Başlık (`answerHeadline`):** koşulu `v === 'resolved'` yerine **`hasWinner && r.requiredVisitCount != null`** yap → hem resolved hem conflict "Ayda N ziyaret" gösterir (mockup resim-5 conflict de "Ayda 2 ziyaret"). date_out→"Bu tarihte geçerli kural yok" (yeni key), unknown→"Tanımlı frekans yok" (mevcut).
   - **Açıklama:** display-verdict'e göre — resolved/conflict/unknown mevcut EXPLAIN_KEY; date_out→yeni ResolveExplainDateOut.
3. **L10n (7 dil):** `ResolveVerdictBadge_resolved`="net sonuç", `_conflict`="çakışma çözüldü", `_dateout`="tarih dışında", `_unknown`="Bilinmiyor"; `ResolveHeadlineDateOut`="Bu tarihte geçerli kural yok"; `ResolveExplainDateOut`="Hedefe uyan kurallar var ama seçtiğiniz tarih hiçbirinin geçerlilik aralığına girmiyor."; `ResolveExplainConflict` mockup metnine güncelle ("Aynı kapsamda iki kural vardı; daha yüksek öncelikli olan uygulandı. Diğerini gözden geçirmek isteyebilirsiniz."). Değerler ekli/güncelleme (mevcut key'leri bozmadan).
4. **L10n plumbing (DET-M DUR bulgusu — ZORUNLU wiring):** resolve.js L10n'i `Index.cshtml`'deki `resolveL10n` sözlüğünden (`#vfp-dr-l10n` köprüsü) okur; resx tek başına JS'e ulaşmaz. Yeni 6 anahtar **Index.cshtml `resolveL10n` sözlüğüne de** eklenmeli (mekanik, davranışsız): `["ResolveVerdictBadge_resolved"] = Localizer["ResolveVerdictBadge_resolved"].Value` (ve _conflict/_dateout/_unknown + ResolveHeadlineDateOut + ResolveExplainDateOut). Mevcut `Resolve*` girişlerinin yanına eklenir. `ResolveExplainConflict` zaten sözlükte varsa dokunma (yalnız resx değeri güncellenir). **Bu wiring KORU'daki "Liste DOKUNMA" istisnasıdır** — yalnız `resolveL10n` sözlüğüne additive giriş; tab markup / DataTable / başka Index mantığı DEĞİŞMEZ.

## KORU / YAPMA
- Backend DEĞİŞMEZ (frequencyStatus/candidatePolicies yeterli; date-out türev). index.js/editör/form.js/detay(Details.cshtml/details.js)/_Resolve.cshtml markup/Segment/resolve engine DOKUNMA (yalnız resolve.js + visit-frequency-details.css + resx + **Index.cshtml `resolveL10n` sözlüğü additive wiring**). Index.cshtml'de YALNIZ resolveL10n sözlüğüne 6 anahtar eklenir — tab markup/DataTable/başka Index mantığı DOKUNMA. DET-K query kurgusu + DET-L select2 picker + `vfp-rs-winner`/`vfp-rs-cand` (kazanan kural + aday merdiveni) + `candTag`/`candWhy`/`freqSentence` DEĞİŞMEZ. NE YAPMALIYIM kartı DEĞİŞMEZ. Uydurma verdict/metin YOK (gerçek frequencyStatus + türev kural + L10n). Tema-duyarlı (subtle token). "BU FREKANSI VEREN KURAL" bölümü (winner) mevcut mantıkla korunur.

## Acceptance
- **E2:** Diten.Web.Tests 137/0. git diff: resolve.js + visit-frequency-details.css + resx + Index.cshtml (yalnız resolveL10n wiring). Backend/index.js/form.js/detay/_Resolve/Segment diff YOK.
- **E4:** resolved→yeşil kutu+"net sonuç"; conflict→amber+"çakışma çözüldü"+"Ayda N ziyaret"; tüm adaylar tarih-dışı→kırmızı+"tarih dışında"+"Bu tarihte geçerli kural yok"; hiç uygun kural yok→gri+"Bilinmiyor"+"Tanımlı frekans yok". Tema-duyarlı. **Razor değişmedi → wwwroot JS/CSS Ctrl+F5 yeter (restart gerekmez).**

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner (DET-L landing SONRASI)
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-FREQ-DET-M · Çözümleme CEVAP kutusu verdict-renkli (mockup birebir) (MOD-0165-FU03, frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/scmm-content-studio · Expected HEAD: <DET-L commit> üstü · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-FREQ-DET-M-resolve-verdict-tone.md · frontend/Diten.Web/wwwroot/assets/js/CRM/VisitFrequencyPolicies/resolve.js (verdictTone ~52, answerHeadline ~433, renderResolveResult ~463-477 answer bloğu, cands ~496) · wwwroot/assets/css/visit-frequency-details.css (.vfp-rs-answer ~544) · frontend/.../Resources l10n resx (7 dil: en,tr,fr,es,zh,ar,ru).

NE (frontend; backend DEĞİŞMEZ; DET-I nötr-gri kararını mockup'a göre TERSİNE çevirir):
 1) CSS .vfp-rs-answer tone modifier: --success (background var(--bs-success-bg-subtle); border-color var(--bs-success-border-subtle)), --warning (warning-subtle), --danger (danger-subtle). Nötr=mevcut (tertiary-bg, modifier yok). Tema-duyarlı.
 2) resolve.js renderResolveResult: cands hesabını answer'dan ÖNCE al; displayVerdict(r,cands) türet: conflict→'conflict'; resolved→'resolved'; not_applicable→'not_applicable'; unknown+cands.length>0 && cands.every(c=>norm(c.reason)==='policy_not_effective')→'date_out'; değilse 'unknown'. .vfp-rs-answer'a vfp-rs-answer--{success|warning|danger} ekle (unknown/not_applicable modifier yok). Rozet metin+ton: resolved→"net sonuç"/success, conflict→"çakışma çözüldü"/warning, date_out→"tarih dışında"/danger, unknown→"Bilinmiyor"/secondary, not_applicable→mevcut/secondary (L10n). answerHeadline koşulu v==='resolved' → (hasWinner && r.requiredVisitCount!=null) [resolved+conflict "Ayda N ziyaret"]; date_out→ResolveHeadlineDateOut; unknown→mevcut. Açıklama: date_out→ResolveExplainDateOut; diğerleri mevcut EXPLAIN_KEY.
 3) L10n 7 dil (resx): ResolveVerdictBadge_resolved="net sonuç", _conflict="çakışma çözüldü", _dateout="tarih dışında", _unknown="Bilinmiyor"; ResolveHeadlineDateOut="Bu tarihte geçerli kural yok"; ResolveExplainDateOut="Hedefe uyan kurallar var ama seçtiğiniz tarih hiçbirinin geçerlilik aralığına girmiyor."; ResolveExplainConflict güncelle="Aynı kapsamda iki kural vardı; daha yüksek öncelikli olan uygulandı. Diğerini gözden geçirmek isteyebilirsiniz." (diğer dillere de çevir).
 4) L10n PLUMBING (DET-M DUR bulgusu — ZORUNLU): resolve.js L10n'i Index.cshtml resolveL10n sözlüğünden (#vfp-dr-l10n) okur; resx tek başına yetmez. Index.cshtml resolveL10n sözlüğüne (Resolve* girişlerinin yanına) 6 yeni anahtarı ADDITIVE ekle: ["ResolveVerdictBadge_resolved"]=Localizer["ResolveVerdictBadge_resolved"].Value (+_conflict/_dateout/_unknown, ResolveHeadlineDateOut, ResolveExplainDateOut). ResolveExplainConflict zaten sözlükte varsa dokunma (yalnız resx değeri güncellenir). YALNIZ resolveL10n sözlüğü — tab markup/DataTable/başka Index mantığı DOKUNMA.
KORU/YAPMA: backend DEĞİŞMEZ; index.js/editör/form.js/detay/_Resolve.cshtml markup/Segment/resolve engine DOKUNMA (yalnız resolve.js+css+resx+Index.cshtml resolveL10n wiring); Index'te yalnız resolveL10n additive; DET-K query + DET-L select2 + vfp-rs-winner/vfp-rs-cand/candTag/candWhy/freqSentence + NE YAPMALIYIM DEĞİŞMEZ; uydurma verdict/metin YOK (gerçek frequencyStatus+türev+L10n); tema-duyarlı subtle token.
DOĞRULA (E2): cd C:\Users\user\Desktop\ERP-vNext; dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo → 137/0; git diff resolve.js+css+resx+Index.cshtml(yalnız resolveL10n); backend/index.js/form.js/detay/_Resolve/Segment diff yok. Ayrı commit ("feat(freq): WP-FREQ-DET-M — Çözümleme CEVAP kutusu verdict-renkli (yeşil/amber/kırmızı/gri) + date-out türevi (MOD-0165-FU03)" + son satır Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: date-out türevi başka reason'ları yanlış kırmızıya boyuyorsa; değişiklik resolve.js/css/resx dışına taşarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-18) → **ACCEPTED (E2)**
```
Commit: 9935ab4e · Agent: PASS · CT: ACCEPTED E2 (izole temiz build) · /c/tmp/ct-detmon-verify @5003e3ae
```
- ✅ **Kapsam (10 dosya):** resolve.js (+65/-24) + visit-frequency-details.css (+4, yalnız `.vfp-rs-answer--{success|warning|danger}`) + Index.cshtml (+7, YALNIZ resolveL10n additive 6 anahtar) + 7 resx (+8/-1 her biri). **KORU=0** (backend/index.js/form.js/detay/_Resolve.cshtml markup/Segment/resolve engine dokunulmadı; tab markup/DataTable dokunulmadı; DET-K query + DET-L select2 + vfp-rs-winner/vfp-rs-cand/candTag/candWhy + NE YAPMALIYIM korundu).
- ✅ **displayVerdict türevi:** conflict/resolved/not_applicable geçer; unknown + `cands.every(reason==='policy_not_effective')` → date_out; karışık→unknown. cands answer'dan ÖNCE hesaplanıyor. `.vfp-rs-answer--{success|warning|danger}` (theme-aware subtle token); rozet+başlık+açıklama verdict'e göre; conflict "Ayda N ziyaret" (hasWinner && requiredVisitCount!=null).
- ✅ **L10n plumbing:** 6 yeni anahtar HEM 7 resx HEM Index resolveL10n'e; ResolveExplainConflict resx'te mockup metnine güncellendi. Türkçe fallback var ama gerçek değer L10n'den (7 dil).
- ✅ **Build+test (CT izole, Release):** Diten.Web.Tests **137/0**.
- ⏳ E4: 5 verdict tonu. **Index.cshtml (Razor) değişti → FLEET RESTART.**

**DET-M KOMPLE.**
```
