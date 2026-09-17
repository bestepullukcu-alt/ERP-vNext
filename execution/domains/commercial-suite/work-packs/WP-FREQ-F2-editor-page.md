# WORK PACKAGE — WP-FREQ-F2 · Frekans Politikası editörü → ayrı Golden Compact Create/Edit sayfası (frontend + gerekirse backend)

> **CT (SoR).** MOD-0165-FU03. Branch `feature/scmm-content-studio` (`fe16367f` üstü). FREQ-A..F1 tamam. Kullanıcı KARARI: editör **offcanvas'tan ayrı tam sayfaya** taşınacak (mockup `Ziyaret Frekans Politikalari (1).html` "yeni politika" tasarımı — **tam tasarım**). form.js'in alan mantığı KORUNUR/reuse edilir; sayfa düzeni + sağ panel (Yaşam Döngüsü / Bu politika ne yapacak / Kaydetmeden önce checklist) yeniden kurulur. Backend'de gerçek eksik çıkarsa tamamlanır (kullanıcı yetki verdi; büyük değişiklik gerekirse DUR+raporla).

## Mevcut durum (reuse kaynağı)
- **form.js** (FREQ-B): contract-driven targetType kartları, entity picker (loadEntityOptions + proxy'ler), context select'leri (BU/territory/segment/campaign/brand/product/cycle-period), frequency + "çözülen cadence" cümlesi, 5 tier band kartları (F1), source, tüm validation, create/update POST (`/CRM/VisitFrequencyPolicies/api/visit-frequency-policies`). Bu mantık KORUNUR — DOM id'leri yeni sayfaya uyarlanır.
- **_CreateEditOffcanvas.cshtml**: 8 bölümlü offcanvas markup + `vfp-editor-l10n` bridge + `.vfp-create-scope` + visit-frequency-create.css. Bu offcanvas **EMEKLİ EDİLİR** (yerini sayfa alır).
- **Backend status yüzeyi TAM:** FrequencyPolicyStatus = draft/active/inactive/archived (4'ü de var); Create+Update `Status` alır; archive ayrı endpoint. → Taslak=draft, Yayında=active, Geçici kapalı=inactive (update Status), Arşivde=archive. **Muhtemelen backend gap yok** — ama update'in status geçişlerine izin verdiğini DOĞRULA; bir geçiş bloklanıyorsa (ör. active→inactive) minimal düzelt.

## Kapsam
### 1) Controller (yeni sayfa action'ları)
`VisitFrequencyPoliciesController`: `[HttpGet] Create()` + `[HttpGet] Edit(Guid id)` → View döndür (RBAC ManageCanonical/Fallback gate; UAS-001). Mevcut proxy'ler (create/update/get/contract/picker) DEĞİŞMEZ — POST hâlâ form.js'ten api proxy'sine gider. (Edit view'ı policyId'yi JS'e verir; form.js GET ile doldurur — offcanvas'taki "edit mode" mantığı reuse.)

### 2) Sayfa view(leri) — mockup tam tasarım
`Create.cshtml` + `Edit.cshtml` (ortak gövde `_Editor.cshtml` partial'ı reuse etmek ideal). `Layout=_LayoutTenantShell`. İki kolon: SOL form + SAĞ sticky panel. `.vfp-create-scope` (tema-duyarlı, mockup paleti; Segment editör deseni referans — segment css DEĞİŞTİRME).

**SOL kolon — 5 numaralı bölüm:**
- **01 KIMLIK:** PolicyCode + PolicyName (2 kolon) · Description (tam) · Notes (tam, "Dahili not — planlama motoru okumaz" yardımcı). (Not: mevcut §8 Notes buraya taşınır.)
- **02 HEDEF — KİME UYGULANIYOR** (başlık sağı: "Listeler sistem tanımlarından gelir"): targetType radio-kart satırı (account-contact-link[rozet "en spesifik"] … segment[rozet "en geniş"], contract sırası) · "Hangi kayıt" seçili-kayıt kutusu (tip çipi + isim + dış kod + "Değiştir…"; "Listeden ara ve seç") · **collapsible "Nerede geçerli olsun?"** ("opsiyonel — boş bırakılan her alan 'tümü' demektir" + "Gizle" toggle): Organizasyon [İş birimi=BusinessUnit "Tüm iş birimleri" + kod ipucu · Saha alanı=TerritoryNodeId "Tüm saha alanları"] · Kapsam [Segment "Segmentten bağımsız" · Kampanya "Kampanyadan bağımsız"] · Ürün [Marka "Tüm markalar" · Ürün "Tüm ürünler", **marka seçilene dek disabled** "Önce marka seçin"] · Dönem [Cycle "Tüm cycle'lar" · Cycle dönemi, **cycle seçilene dek disabled** "Önce cycle seçin"] + **KAPSAM çip özeti** (seçili kısıtlar) + "Kapsamı temizle".
- **03 FREKANS — ASIL DEĞER:** FrequencyType · RequiredVisitCount · PeriodType (3 kolon) + **"ÇÖZÜLEN CADENCE: ayda 4 ziyaret"** canlı kutu (sağda "monthly · 4 · month").
- **04 GEÇERLİLİK:** EffectiveFrom · EffectiveTo ("süresiz" placeholder) + not "EffectiveTo boş bırakılırsa politika süresiz geçerlidir; aynı hedefe daha yeni EffectiveFrom çakışmada öne geçer."
- **05 ÇAKIŞMA OLURSA:** intro "Aynı hedefe başka bir kural da denk gelirse hangisinin kazanacağını bu seçim belirler." + "Bu kuralın ağırlığı" 5 band radio-kart (F1 tier'ları + **Band_{code}_Desc açıklamaları**) + "Bu kural nereden geliyor?" Source dropdown + not "Sadece raporlama ve takip için; frekansı etkilemez."

**SAĞ kolon — sticky panel (3 kart):**
- **YAŞAM DÖNGÜSÜ:** seçilebilir 4 satır — Taslak (plana girmez) / Yayında (plana giriyor) / Geçici kapalı (şimdilik durduruldu) / Arşivde (okunur, silinmez); seçili vurgulu (renkli nokta). Not: "Silme yok. Kapatma Archive ile yapılır; kayıt geçmişte okunur kalır ve planlama motoru tarafından değerlendirilmez." (Bu = status seçimi; kaydetme butonlarıyla senkron.)
- **BU POLİTİKA NE YAPACAK?:** canlı cümle "'<PolicyName>' hedeflerine <cadence> planlanacak — <kapsam ifadesi>." + anahtar-değer: Hedef / Frekans / Kapsam (kısıt yok · N kısıt) / Geçerlilik / Ağırlık / Kaydedilince. (form state'ten canlı türetilir.)
- **KAYDETMEDEN ÖNCE** (rozet: "N uyarı" / "hazır"): canlı checklist ✓/! — Hedef seçildi · Frekans geçerli · Dönem tutarlı · Kapsam (daraltıldı/kısıtı yok) · Çakışma ağırlığı · Taslak kalacak/Yayına alınır.
- **Footer buton:** "Kaydet ve aktive et" (primary → status=active) · "Taslak olarak kaydet" (outline → status=draft). Edit modunda mevcut status'e göre metin/uygun geçişler (Yayından kaldır=inactive, Arşivle=archive endpoint).

### 3) form.js uyarlama + yeni panel mantığı
form.js sayfa DOM'una uyarlanır (id'ler); contract-driven alanlar/validation/cadence/band/picker KORUNUR. YENİ: yaşam-döngüsü seçici, "bu politika ne yapacak" canlı özet, "kaydetmeden önce" checklist (form geçerlilik durumundan), iki save butonu (aktive/taslak). Brand→product ve cycle→period narrowing (mockup "önce X seçin").

### 4) index.js + Index.cshtml (offcanvas emekli)
- index.js: "Yeni Politika" butonu → `window.location = '/CRM/VisitFrequencyPolicies/Create'`; satır "Düzenle" → `/CRM/VisitFrequencyPolicies/Edit/{id}`. `openCreateEdit` offcanvas açma kaldırılır. **Liste kolonları + Save View + Detay/Çözümleme + Arşivle/Sil DEĞİŞMEZ.**
- Index.cshtml: `_CreateEditOffcanvas` partial'ı kaldır (offcanvas emekli). _DetailsQuickView + _Resolve KALIR.

### 5) L10n (7 dil, additive)
Yeni sayfa metinleri: bölüm başlıkları (mockup), "Nerede geçerli olsun/Gizle", KAPSAM/Kapsamı temizle, Organizasyon/Kapsam/Ürün/Dönem alt-başlıkları + "Tümü" placeholder'ları + "Önce marka/cycle seçin", ÇÖZÜLEN CADENCE, GEÇERLİLİK notu, ÇAKIŞMA intro/not, YAŞAM DÖNGÜSÜ 4 etiket+açıklama + archive notu, BU POLİTİKA NE YAPACAK anahtarları, KAYDETMEDEN ÖNCE checklist maddeleri + uyarı/hazır rozeti, "Kaydet ve aktive et"/"Taslak olarak kaydet". Band_{code}_Desc F1'de var. L10n köprüsü (camelCase/PascalCase) korunur.

## KORU / YAPMA
- Backend resolve/CRUD/archive/soft-delete/validation davranışı DEĞİŞMEZ (yalnız gerçek status-geçiş gap'i çıkarsa minimal düzelt; büyük backend değişiklik gerekirse DUR+raporla). Liste (index.js kolonları/Save View/FREKANS/AĞIRLIK) + Detay/Çözümleme (_DetailsQuickView/_Resolve/resolve.js) DAVRANIŞI DEĞİŞMEZ (yalnız buton/satır → sayfa yönlendirme). Segment dosyaları (segment-create.css vb.)/başka modül DOKUNMA. Vocabulary/band/source/status **hardcode YOK** (contract + L10n). create/update **payload sözleşmesi** (alan adları/GUID'ler) bozulmaz — mevcut proxy/DTO ile birebir. Golden Compact/tema/L10n köprüsü gotcha'ları korunur. Yeni scoped css visit-frequency-create.css'e eklenir (Segment css DEĞİŞTİRME).

## Acceptance
- **E2:** Diten.Web.Tests 137/0 (+ backend'e dokunulduysa CrmService.Application.Tests baseline-diff sıfır-yeni-fail). Create + Edit sayfaları açılır (mockup tam tasarım: sol 5 bölüm + sağ 3 kart panel); form.js mantığı çalışır (contract-driven kartlar, picker, cadence, 5 band, validation); "Yeni Politika"/satır-Düzenle sayfaya yönlenir; offcanvas emekli; liste/detay/çözümleme bozulmadı. git diff: yeni Create/Edit(+_Editor) view + controller action + form.js + index.js + Index.cshtml + visit-frequency-create.css + _IndexL10n/editor-l10n + resx (+ backend varsa minimal).
- **E4:** Yeni Politika → sayfa → doldur → "Kaydet ve aktive et" (active) veya "Taslak" (draft) → liste; satır Düzenle → sayfa dolu → güncelle; sağ panel canlı özet + checklist; yaşam döngüsü geçişleri.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-FREQ-F2 · Frekans editörü → ayrı Golden Compact Create/Edit sayfası (MOD-0165-FU03, frontend + gerekirse minimal backend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/scmm-content-studio · Expected HEAD: fe16367f üstü · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-FREQ-F2-editor-page.md · frontend/Diten.Web/Views/CRM/VisitFrequencyPolicies/{Index,_CreateEditOffcanvas,_DataTable,_IndexL10n}.cshtml + wwwroot/assets/js/CRM/VisitFrequencyPolicies/{index.js,form.js} + wwwroot/assets/css/visit-frequency-create.css · frontend/Diten.Web/Controllers/CRM/VisitFrequencyPoliciesController.cs · (referans ayrı-sayfa deseni) frontend/Diten.Web/Views/CRM/EligibilityPolicies/ (Create sayfası varsa) · services/.../VisitFrequencyPolicy/{Domain/Entities/VisitFrequencyPolicy.cs (FrequencyPolicyStatus), Commands, Handlers, Contract}. Editör mockup düzeni WP'de tam yazılı (sol 5 bölüm + sağ Yaşam Döngüsü/Bu politika ne yapacak/Kaydetmeden önce).

NE: Editör offcanvas'ı EMEKLİ ET, mockup'ın AYRI TAM SAYFASINA çevir (Create + Edit; ortak _Editor partial). Controller Create()/Edit(id) GET action (RBAC gate). Sol 5 bölüm (Kimlik/Hedef+scope collapsible/Frekans+cadence/Geçerlilik/Çakışma+Source) + sağ sticky panel (Yaşam Döngüsü status seçici / "Bu politika ne yapacak" canlı özet / "Kaydetmeden önce" checklist) + footer (Kaydet ve aktive et=active / Taslak olarak kaydet=draft). form.js alan mantığını REUSE et (contract-driven targetType kartları, entity picker, context select, cadence cümlesi, 5 tier band+Desc, source, validation, create/update POST) — DOM'a uyarla; YENİ: yaşam-döngüsü seçici, canlı özet, checklist, iki save butonu, brand→product & cycle→period narrowing. index.js: Yeni Politika→/Create, satır Düzenle→/Edit/{id}, offcanvas açma kaldır. Index.cshtml: _CreateEditOffcanvas partial kaldır (Detay/Çözümleme KALIR). L10n 7 dil (yeni sayfa metinleri). Backend status yüzeyi TAM (draft/active/inactive/archived; create+update Status; archive endpoint) — gerçek status-geçiş gap'i çıkarsa minimal düzelt, BÜYÜK backend değişiklik gerekirse DUR+raporla.
KORU/YAPMA: backend resolve/CRUD/archive/soft-delete/validation DAVRANIŞI DEĞİŞMEZ; liste(index.js kolon/Save View/AĞIRLIK/FREKANS)+Detay/Çözümleme(_DetailsQuickView/_Resolve/resolve.js) DEĞİŞMEZ (yalnız buton/satır yönlendirme); create/update payload sözleşmesi (alan/GUID adları) birebir korunur; Segment/başka modül DOKUNMA; hardcode vocabulary/band/source/status YOK (contract+L10n); yeni css visit-frequency-create.css'e (Segment css değiştirme); Golden Compact/tema/L10n köprüsü korunur.
DOĞRULA (E2): Diten.Web.Tests 137/0 (+backend'e dokunulduysa CrmService.Application.Tests baseline-diff sıfır-yeni-fail, Release); Create+Edit açılır (mockup tam tasarım); form.js reuse çalışır; buton/satır sayfaya yönlenir; offcanvas emekli; liste/detay/çözümleme bozulmadı; git diff kapsam içi. Ayrı commit. §22 TÜRKÇE. K13.
Durma: create/update payload sözleşmesi korunamıyorsa; büyük backend değişiklik gerekiyorsa; form.js reuse yerine yeniden yazım gerekiyorsa (raporla); liste/detay davranışı değişmek zorundaysa; kapsam VisitFrequencyPolicies dışına taşarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama → (agent sonrası, dispatch owner'da)
```text
Commit: <agent> · Agent: <PASS/FAIL> · CT: <PENDING>
```
- İzole worktree → Diten.Web.Tests (+backend varsa CrmService) baseline-diff; Create+Edit ayrı sayfa (mockup 5 bölüm + sağ panel); form.js reuse (contract-driven, payload sözleşmesi korunmuş); offcanvas emekli; index.js buton/satır yönlendirme; liste/Save View/Detay/Çözümleme bozulmadı; backend davranışı değişmedi (varsa minimal status-geçiş); hardcode yok; git diff kapsam içi.
