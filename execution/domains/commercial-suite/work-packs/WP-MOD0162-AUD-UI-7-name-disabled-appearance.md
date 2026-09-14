# WORK PACKAGE — WP-MOD0162-AUD-UI-7 · Name alanı: readonly durumda disabled görünüm

> **Control Tower kaydı (SoR).** Kullanıcı manuel-test: Name alanı readonly (dimension-türevli) iken **disabled da görünsün**; editable iken disabled görünüm **kalksın**. Module: **MOD-0162** (AudienceProfile formu). Branch: `feature/scmm-content-studio` (HEAD `d420f5b9`). **Yalnız frontend** (Diten.Web); backend DOKUNMA. **SIRA:** Subject WP'sinden önce. Compose-row layout kullanıcı onayladı ("tamam oldu").

## Ölçülmüş girdi (CT)
- **Mevcut (`updateProfileName` ~603-613):** HCP/pharmacist → `nameEl.readOnly = true` + `setValue(dimensionNameLabel())`; diğer → `nameEl.readOnly = false` + ProfileType prefill. **readOnly disabled GÖRÜNÜMÜ vermiyor** (kullanıcı disabled istiyor).
- **Submit güvenli:** `submit` `document.getElementById('taxName').value` ile okuyor (JS `.value`, ~901) — **form-serialize değil** → `disabled` input'un value'su JS ile yine okunur/set edilir → **disabled yapmak value'yu düşürmez** (kanıtlandı).
- **Not:** `setFormReadOnly` (~528) view modda tüm alanları zaten `disabled` yapar; bu WP yalnız create/edit modunda Name'in disabled durumunu ProfileType'a göre yönetir.

## Kapsam (yalnız frontend, Name alanı — SADECE disabled görünüm)
1. **`updateProfileName`:** Name'in `disabled` durumunu türev-durumla senkronla:
   - HCP/pharmacist (dimension-türevli, düzenlenemez): `nameEl.disabled = true` → **disabled görünüm** (readOnly yerine ya da yanında; değer JS `.value` ile submit'e girmeye devam eder — ~901).
   - diğer ProfileType (editable): `nameEl.disabled = false` → normal/enabled görünüm.
   - Mevcut `dimensionNameLabel()` türetme + `profileNameDirty` + ProfileType-prefill mantığı KORUNUR.
2. View mod (`setFormReadOnly`) davranışı bozulmaz (view'da zaten hepsi disabled).

## YAPMA
- Backend/API/RBAC/ocelot DOKUNMA. AUD-UI-3/4/5/6 davranış+yapısını (compose-then-add, ValueCode, Name " / ", cascade, ProfileType-koşul, small select2, compose layout) DEĞİŞTİRME — yalnız Name disabled görünümü. Name value'sunu submit'ten düşürme (JS `.value` ~901 çalışır; disabled güvenli). Subject/Topic/başka konsol. 7-dil key-echo. Başka modül.

## Acceptance
- **E2:** build temiz; Name HCP/pharmacist'te **disabled görünüm** (dimension-türevli, değer yine payload'a girer) + diğer ProfileType'ta **enabled/editable** (disabled görünüm yok); AUD-UI davranışı korunur; `Diten.Web.Tests` + Platform nav guard baseline-diff sıfır-yeni-fail.
- **E4 (kullanıcı manuel):** ProfileType=healthcare-professional → Name disabled görünür + dimension'lardan dolar + kaydedince değer gider; ProfileType=patient → Name enabled/editable.
- Kapsam: yalnız Diten.Web (taxonomy.js updateProfileName). Backend DEĞİŞMEZ.

---

## §36.1 Agent Prompt (paste-ready)

```text
@[.antigravity/agents/frontend-ui-ux.md]
WP: WP-MOD0162-AUD-UI-7 · Prompt v1.0  (Name alanı readonly→disabled görünüm — MOD-0162, frontend)

Repository: C:\Users\user\Desktop\ERP-vNext
Branch: feature/scmm-content-studio · Expected HEAD: d420f5b9 · Worktree: ana checkout

Önce oku:
1. execution/domains/commercial-suite/work-packs/WP-MOD0162-AUD-UI-7-name-disabled-appearance.md (bu WP)
2. frontend/Diten.Web/wwwroot/assets/js/CRM/Knowledge/taxonomy.js — updateProfileName (~603-613, readOnly set eden) + submit (~897-901, taxName .value okuma — disabled güvenli kanıtı) + setFormReadOnly (~528, view modu)

NE (yalnız frontend Diten.Web, Name alanı — SADECE disabled görünüm):
 updateProfileName: Name'in disabled durumunu türev-durumla senkronla —
  - HCP/pharmacist (dimension-türevli): nameEl.disabled = true (disabled GÖRÜNÜM; değer JS .value ile ~901 submit'e girmeye devam eder).
  - diğer ProfileType (editable): nameEl.disabled = false (normal görünüm).
  Mevcut dimensionNameLabel() türetme + profileNameDirty + ProfileType-prefill KORUNUR. View mod (setFormReadOnly) bozulmaz.
NASIL: mevcut readOnly satırlarını disabled ile eşle (readOnly kalabilir ya da disabled'a çevrilir — kritik olan disabled GÖRÜNÜM + değerin JS .value ile toplanması, ~901). AUD-UI-3/4/5/6 mantığı DOKUNULMADAN yalnız Name disabled görünümü.
YAPMA: backend/API/RBAC/ocelot; AUD-UI davranış/yapı değiştir; Name value'sunu submit'ten düşür; Subject/Topic/başka konsol; 7-dil key-echo; başka modül.
DOĞRULA (E2):
 - build temiz; HCP/pharmacist→Name disabled görünüm + değer payload'a girer; diğer→Name enabled/editable; AUD-UI davranışı korunur.
 - Diten.Web.Tests + Platform nav guard: yeni fail YOK (baseline-diff).
Ayrı commit. §22 raporu TÜRKÇE. Senin PASS'in kapanış değildir (K13) — CT E2 + kullanıcı E4 doğrular.

Durma koşulları: disabled Name değeri submit'ten düşürüyorsa (~901 JS .value çalışmalı; düşerse raporla) · AUD-UI davranışı bozuluyorsa · kapsam Name dışına taşarsa. DUR + raporla.
```

## Kalan (bu WP dışı)
- CT E2 + kullanıcı E4 → **AudienceProfile formu TAMAM** → **WP-MOD0162-SUBJECT-UI** → ALMIBA retest.
