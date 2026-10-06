# WORK PACKAGE — WP-FREQ-F13 · "KAYDETMEDEN ÖNCE" checklist canlı + mockup ikonlar (frontend)

> **CT (SoR).** MOD-0165-FU03. Branch `feature/scmm-content-studio` (**F9…F12 landing sonrası HEAD üstü** — aynı dosyalar, SIRALI). Kullanıcı: sağ panel "KAYDETMEDEN ÖNCE" her maddede **canlı alt-açıklama** + mockup ikonları (✓ yeşil / ! amber) göstersin (mevcut sadece başlık). **Frontend only** (_Editor.cshtml sağ panel + form.js checklist render + visit-frequency-create.css + L10n). Read-only türetme; buildPayload'a dokunmaz.

## Mockup (resim-2) — her madde: başlık + canlı alt-açıklama + ikon
| Madde | İkon (durum) | Canlı alt-açıklama örneği |
|---|---|---|
| Hedef seçildi | ✓ yeşil (seçili) / ! amber (boş) | "Kardiyoloji A-Segment · SEG-00412" (picked isim·kod) |
| Frekans geçerli | ✓ / ! | "ayda 4 ziyaret" (çözülen cadence) |
| Dönem tutarlı | ✓ / ! | "weekly ↔ month" (freqType ↔ periodType) |
| Kapsam | ! amber (kısıt yok) / ✓ (N kısıt) | "Politika bu hedef tipindeki tüm kayıtlara uygulanır." / "N kısıt" |
| Ağırlık (dinamik başlık) | ✓ / ! | başlık seçili band adı ("En üst ağırlık seçildi") + açıklama Band_{code}_Desc ("Bu kural kampanya kurallarını da geçersiz kılar.") |
| Taslak kalacak / Yayına alınır | ✓ / ! | "Taslak kurallar plana girmez; hazır olduğunda yayına alırsınız." |

- **İkonlar:** satisfied = ✓ yeşil (`text-success` bx-check-circle); warning/attention = ! amber (`text-warning` bx-error-circle). Mockup'ta gri "i" info YOK — amber ! kullan.
- **"N uyarı" rozeti** = ! sayısı (0 ise "hazır" yeşil).
- Tümü **canlı** (form değişince güncellenir — mevcut checklist zaten form olaylarına bağlı; alt-açıklama + ikon ekle).

## Kapsam
- `form.js`: checklist render → her madde {icon durum, başlık (Ağırlık için dinamik band adı), canlı alt-açıklama}. Alt-açıklamalar form state'ten (picked target isim·kod, cadence, freq↔period, scope kısıt sayısı/none, band adı+Desc, draft/active not). "N uyarı"/"hazır" rozeti ! sayısından. buildPayload/validation'a DOKUNMAZ (yalnız türetilmiş gösterim).
- `_Editor.cshtml`: checklist item markup (başlık + alt-açıklama satırı) gerekiyorsa.
- `visit-frequency-create.css`: madde başlık + alt-açıklama (muted, küçük) + ikon renkleri (✓ success / ! warning). Tema-duyarlı.
- L10n: madde başlıkları + statik açıklama kalıpları (7 dil); Band_+Desc F1'de mevcut; dinamik kısımlar form state.

## KORU / YAPMA
- buildPayload/validation/id DEĞİŞMEZ (checklist salt gösterim). Backend/liste/Detay/Çözümleme/resolve.js/index.js/Segment/diğer bölümler DOKUNMA (yalnız sağ panel checklist). Hardcode band/vocabulary yok (Band_Desc L10n + contract). Tema-duyarlı. Yalnız _Editor(checklist)+form.js(checklist render)+css+resx.

## Acceptance
- **E2:** Diten.Web.Tests 137/0. git diff: _Editor.cshtml + form.js + css + resx. Backend/liste/detay/diğer bölüm diff YOK.
- **E4:** checklist canlı (form değişince güncel) — her madde başlık + alt-açıklama + ✓/! ikon; "N uyarı"/"hazır" rozeti doğru; Ağırlık maddesi seçili band adı+etkisi; tema-duyarlı.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner (F9…F12 landing SONRASI)
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-FREQ-F13 · "KAYDETMEDEN ÖNCE" checklist canlı + mockup ikonlar (MOD-0165-FU03, frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/scmm-content-studio · Expected HEAD: <F12 commit> üstü · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-FREQ-F13-checklist-live.md · frontend/Diten.Web/Views/CRM/VisitFrequencyPolicies/_Editor.cshtml (sağ panel "Kaydetmeden önce" checklist) · wwwroot/assets/js/CRM/VisitFrequencyPolicies/form.js (checklist/özet render + form olayları + band/cadence/scope helpers) · visit-frequency-create.css.

NE (frontend; buildPayload/id DEĞİŞMEZ; yalnız sağ panel checklist):
 Checklist her madde: ikon (satisfied ✓ text-success bx-check-circle / warning ! text-warning bx-error-circle; gri info YOK) + başlık (Ağırlık maddesi = seçili band adı dinamik) + CANLI alt-açıklama: Hedef=picked isim·kod (boşsa uyarı); Frekans=çözülen cadence "ayda N ziyaret"; Dönem=freqType↔periodType; Kapsam=kısıt yok "…tüm kayıtlara uygulanır"/N kısıt; Ağırlık=Band_{code}_Desc; Taslak/Yayın notu. "N uyarı" rozeti=! sayısı (0→"hazır" yeşil). Tümü form değişince canlı. form.js checklist render'ı türet; buildPayload/validation DOKUNMA.
KORU/YAPMA: buildPayload/validation/id DEĞİŞMEZ; backend/liste/Detay/Çözümleme/resolve.js/index.js/Segment/diğer bölümler DOKUNMA; hardcode band/vocabulary yok (Band_Desc L10n+contract); tema-duyarlı; yalnız _Editor(checklist)+form.js(checklist)+css+resx.
DOĞRULA (E2): Diten.Web.Tests 137/0; git diff yalnız _Editor+form.js+css+resx; backend/liste/detay/diğer bölüm diff yok. Ayrı commit. §22 TÜRKÇE. K13.
Durma: checklist buildPayload/validation'a bağımlı hale geliyorsa; kapsam checklist dışına taşarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-17) → **ACCEPTED (E2)** — F10 ile birleşik commit
```
Commit: bd1f2466 (F10+F13) · Agent: PASS · CT: ACCEPTED E2 (izole temiz build) · /c/tmp/ct-fb-verify @bd1f2466
```
- ✅ `updateChecklist` canlı: her madde ✓ (text-success bx-check-circle) / ! (text-warning bx-error-circle) + başlık (Ağırlık=seçili band adı dinamik, DOM'dan) + alt-açıklama (Hedef isim·kod / cadence / Freq↔Period / kapsam kısıt / Band_Desc / durum notu); "N uyarı"/"hazır" rozeti. Gri neutral kaldırıldı.
- ✅ **Kritik doğrulama:** silinen `targetOk` satırı `updateChecklist()` İÇİNDEydi — `validate()` (satır 694) hâlâ bağımsız `currentTargetId()`+TargetRequired kontrolü yapıyor; buildPayload currentTargetId'yi aynen kullanıyor → **validasyon/payload bozulmadı** (checklist salt türetme). Kapsam: _Editor+form.js+css+7resx; backend/detay/liste/F9=0. Diten.Web.Tests 137/0.
- ℹ️ Ajan kararı (kabul): kapsam-yok → ! (uyarı sayılır); active-olmayan durum → ! (heads-up). E4'te gözden geçirilebilir.
```
