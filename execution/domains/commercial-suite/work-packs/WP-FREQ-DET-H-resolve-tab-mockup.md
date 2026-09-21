# WORK PACKAGE — WP-FREQ-DET-H · Çözümleme (Frekans Kontrolü) tab mockup birebir (frontend)

> **CT (SoR).** MOD-0165-FU03. Branch `feature/scmm-content-studio` (**DET-G landing sonrası HEAD üstü** — visit-frequency-details.css paylaşımlı olabilir, SIRALI). Kullanıcı: Çözümleme tab'ını mockup "Frekans Kontrolü"ne getir; 3 ana kart task-create standardında, iç tasarım mockup. **Frontend only** (`_Resolve.cshtml` + resolve.js + css + L10n). **Backend DEĞİŞMEZ** — `/resolve` (VisitFrequencyResolveResult) `CandidatePolicies` = tam `FrequencyCandidatePolicy` (TargetType/RequiredVisitCount/PeriodType/Specificity/Reason/Selected HEPSİ var); IncludeDiagnostics=true ile eligible+eliminated tüm kurallar gelir.

## Kullanıcı kararları
- **"Kim / ne":** mockup **senaryo dropdown'u** (dostane): her senaryo bir targetType + gerekli bağlama eşlenir; kayıt picker + Dönem/Tarih ona göre.
- **"NE YAPMALIYIM?":** verdict'ten **türetilir** (canlı). **"Başka bir sonuç örneği göster" ATLA** (mockup demo chrome, gerçekte yok).

## Mockup 3 kart
### 1) Sol — "Kimi soruyorsunuz?" (input, app-card)
- Başlık + desc. **"Kim / ne" senaryo dropdown** — dostane etiketler → targetType+context eşlemesi:
  - "Bir doktor — belirli hastanede" → contact (+ territory-node/saha bağlamı; contact location context) · "Bir kurum/hesap" → account · "Kişi-kurum bağı" → account-contact-link · "Segmentteki tüm hedefler" → segment · "Kampanya hedefi" → campaign-target · "Saha alanı" → territory-node · "Konsept düğümü" → concept-node · "Hedef kitle profili" → audience-profile.
- **Kayıt** picker (seçilen hedef + "değiştir") — targetType'a göre entity picker (resolve.js READERS/nameOf reuse).
- **Dönem** (cycle-period selector) + **Tarih** (effectiveAt date) — resolve context.
- **"Frekansı göster"** butonu (primary) → GET /resolve (IncludeDiagnostics=true).
- **"Kural basit"** not kutusu (statik açıklama — mockup copy: "en dar kapsamlı kazanır…").

### 2) Sağ — "CEVAP" (result, app-card)
- **verdict:** büyük başlık — resolved→"Ayda N ziyaret" (+ seçilen kural adı) / unknown→"Tanımlı frekans yok" / conflict→"Çakışma" / not_applicable→"Uygulanmaz"; sağ rozet (kural yok/çözüldü/çakışma). Alt açıklama (verdict'e göre).
- **"BU HEDEFE DENK GELEN DİĞER KURALLAR" (dardan genişe sıralı):** CandidatePolicies[] (eligible+eliminated) — her satır: ad + tip alt-satır (TargetType etiketi + specificity ipucu) + frekans "N / yerel-dönem" + **status rozeti** (tarih dışı=PolicyNotEffective / devrede değil=specificity/scope elendi / yedek=last-resort / seçildi=Selected) + reason satırı (reasonLabels[reason] insan-dili). Seçilen vurgulu.

### 3) Alt — "NE YAPMALIYIM?" (recommendations, app-card)
- verdict'ten türet: unknown→"Bu hedef kapsama girmiyor / Segment veya bölge seviyesinde yedek kural önerilir" + "Hızlı çözüm: segment için tek politika tüm hedefleri kapsar" · conflict→çakışma uyarısı (aynı bantta iki kural) · resolved→"kapsanıyor" (ok). İkon (! uyarı / i bilgi). **Örnek-buton YOK.**

## KORU / YAPMA
- Backend DEĞİŞMEZ (/resolve tam DTO verir; IncludeDiagnostics=true iste). Liste/editör/detay(Details.cshtml/details.js)/Segment/resolve engine DOKUNMA (yalnız _Resolve.cshtml + resolve.js + css + L10n). Vocabulary/verdict/reason hardcode YOK (contract + reason kodları + L10n). Uydurma sonuç/öneri YOK (gerçek /resolve + verdict-türev). app-card kabuk (3 kart task-create standardı); tema/L10n köprüsü. Senaryo→targetType eşlemesi contract targetTypes'a dayanır.

## Acceptance
- **E2:** Diten.Web.Tests 137/0. git diff: _Resolve.cshtml + resolve.js + css + resx. Backend/liste/editör/detay/Segment diff YOK.
- **E4:** Çözümleme tab 3 kart (input senaryo dropdown / CEVAP verdict+kurallar+status rozet / NE YAPMALIYIM türev); gerçek /resolve; mockup tasarım; tema-duyarlı.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner (DET-G landing SONRASI)
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-FREQ-DET-H · Çözümleme (Frekans Kontrolü) tab mockup birebir (MOD-0165-FU03, frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/scmm-content-studio · Expected HEAD: <DET-G commit> üstü · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-FREQ-DET-H-resolve-tab-mockup.md · frontend/Diten.Web/Views/CRM/VisitFrequencyPolicies/{_Resolve.cshtml, Index.cshtml (Çözümleme tab + vfp-dr-l10n köprüsü)} + wwwroot/assets/js/CRM/VisitFrequencyPolicies/resolve.js (READERS/nameOf/verdict/reason label + GET /resolve) + wwwroot/assets/css/visit-frequency-details.css (.vfp-dv-* resolve stilleri) · services/.../Resolve/VisitFrequencyResolveContracts.cs (VisitFrequencyResolveResult + FrequencyCandidatePolicy: TargetType/RequiredVisitCount/PeriodType/Specificity/Reason/Selected) · frontend/.../index.js (periodLabels referans). Mockup: Downloads/Ziyaret Frekans Politikalari (1).html "Frekans Kontrolü" tab.

NE (frontend; backend DEĞİŞMEZ):
 3 app-card layout. SOL "Kimi soruyorsunuz?": "Kim/ne" senaryo dropdown (dostane→targetType+context: doktor-hastanede→contact+territory, kurum→account, segment→segment, kampanya→campaign-target, saha→territory-node, konsept→concept-node, kitle→audience-profile, kişi-kurum→account-contact-link) + Kayıt picker (READERS/nameOf reuse) + Dönem(cycle-period)+Tarih(effectiveAt) + "Frekansı göster"→GET /resolve?IncludeDiagnostics=true + "Kural basit" statik not.
 SAĞ "CEVAP": verdict büyük başlık (resolved→"Ayda N ziyaret"+kural / unknown→"Tanımlı frekans yok" / conflict / not_applicable) + rozet + açıklama. "BU HEDEFE DENK GELEN DİĞER KURALLAR" (CandidatePolicies eligible+eliminated): ad + tip alt-satır (TargetType etiketi+specificity) + frekans "N / yerel-dönem" + status rozeti (tarih dışı=PolicyNotEffective/devrede değil=specificity-scope elendi/yedek=last-resort/seçildi=Selected) + reason (reasonLabels). Seçilen vurgulu.
 ALT "NE YAPMALIYIM?": verdict'ten türet (unknown→yedek kural öner+hızlı çözüm segment; conflict→çakışma uyarısı; resolved→kapsanıyor) ikonlu. "Başka bir sonuç örneği göster" YOK.
 L10n 7 dil (senaryo etiketleri, status rozetleri, NE YAPMALIYIM copy, Kural basit).
KORU/YAPMA: backend DEĞİŞMEZ (IncludeDiagnostics=true iste); liste/editör/detay(Details.cshtml/details.js)/Segment/resolve engine DOKUNMA (yalnız _Resolve.cshtml+resolve.js+css+resx); hardcode vocabulary/verdict/reason YOK (contract+kod+L10n); uydurma sonuç/öneri YOK (gerçek /resolve+verdict-türev); app-card/tema/L10n köprüsü; senaryo→targetType contract targetTypes'a dayanır.
DOĞRULA (E2): Diten.Web.Tests 137/0; git diff _Resolve+resolve.js+css+resx; backend/liste/editör/detay/Segment diff yok. Ayrı commit. §22 TÜRKÇE. K13.
Durma: /resolve DTO beklenen alanları vermiyorsa (beklenmiyor, hepsi var); senaryo→targetType eşlemesi context olmadan resolve'u bozuyorsa; kapsam _Resolve/resolve.js dışına taşarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-18) → **ACCEPTED (E2)**
```
Commit: 79cbb0f6 · Agent: PASS · CT: ACCEPTED E2 (izole temiz build) · /c/tmp/ct-deth-verify @79cbb0f6
```
- ✅ **Kapsam:** _Resolve.cshtml + resolve.js + visit-frequency-details.css + Index.cshtml (vfp-dr-l10n köprü) + 7 resx. **KORU=0** (backend/liste/_DataTable/index.js/editör/Details.cshtml/details.js/Segment/resolve engine dokunulmadı).
- ✅ **3-kart mockup:** SOL senaryo dropdown (dostane→targetType+context, contract targetTypes'a dayalı; doktor-hastanede→contact+territoryNodeId) + Kayıt picker (READERS/nameOf reuse) + Dönem/Tarih + "Frekansı göster"→GET /resolve **IncludeDiagnostics=true**. SAĞ CEVAP (verdict başlık+rozet+seçilen kural) + "DENK GELEN DİĞER KURALLAR" (CandidatePolicies dar→geniş: ad+tip+"N/yerel-dönem"+status rozeti[tarih dışı/devrede değil/yedek/seçildi]+reason). ALT NE YAPMALIYIM (verdict türev, örnek-buton yok).
- ✅ **Build+test (CT izole, Release):** Diten.Web.Tests **137/0**.
- ℹ️ **Not (kabul):** /resolve query'sinde cycle-period boyutu YOK (yalnız effectiveAt + context id'ler); Dönem seçici UX için gösterilir ama sorguya gönderilmez (yanıltıcı no-op'tan kaçınıldı); gerçek bağlam effectiveAt + territoryNodeId. İleride resolve'a cycle-period eklenirse bağlanır.
- ⏳ E4: Çözümleme 3-kart + senaryolar + status rozetleri.

**DET-H KOMPLE → MOD-0165-FU03 UI TAM: liste + editör + detay + çözümleme, hepsi mockup + CT-E2.**
```
