# WORK PACKAGE — WP-CL-FE-3 · Çekirdek / yerel iddia formu + kanıt ekleme modalı + onaya hazırlık + onaya gönder / geri çek (frontend)

> **CT (SoR).** İddialar v2 Faz 3. **FE-1 birleştikten SONRA** dispatch edilir; FE-1'in Web proxy katmanını (`/CRM/Claims/api/v2/…`) kullanır.
>
> **Kaynaklar:**
> - Mockup senaryo **3** (yeni çekirdek iddia), **5** (kanıt ekleme), **6** (belge önizleme, sade)
> - Plan: CL-FE-3 + CL-FE-5'in kanıt modalı kısmı
>
> **Amaç:** Kullanıcı çekirdek (ya da yerel) iddiayı oluşturabilsin, kanıt bağlayabilsin, hazırlık kontrol listesini görsün ve **onaya gönderebilsin**. Bu paketle **canlı uçtan uca onay testi** yapılabilir hale gelir (Admin gönderir, sema WorkCenterNext'ten onaylar).
>
> **Çalışma yeri:** worktree `C:\tmp\cl-fe-3`, dal `wp/cl-fe-3`. Taban = FE-1 birleşmiş `test/crm-content-visit-e2e`. Commit bu dala, push YOK.

## Mockup
`mockups/claims-v2/` (FE-1 WP'deki dosyalar). Senaryolar: `core` (3), `evid` (5), `prev` (6).

**Ekran metinleri:**
- "Kimlik ve metin"
- "Ürüne göre önerilir. Sürümler ve ülke sürümleri arasında sabit iş anahtarıdır."
- "Durum yalnız Onaya gönder, Onayla ve Arşivle işlemleriyle değişir."
- "Metin dili: en · English"
- "Ülke sürümleri bu metinden uyarlanır; kapsamı genişletemez. Onaylanınca kilitlenir."
- "Onaya göndermek için en az 1 kanıt zorunludur…"
- "Onaya hazırlık"
- "Global onay akışı · Sıralı · Onay ayarlarından yapılandırılır"
- Kanıt modalı: "1 · Belge seçin" / "2 · Referans ve eşleme" / "… sabitlenecek" / "Alıntılanan cümle *" / "Desteklediği ifade * · … üzerinde işaretleyin"

## Kanıt (CT)
- **FE-1 proxy'leri:**
  - claims: create, update, get, submit-review, withdraw-review, evidence (list, link, remove, document-options).
  - lookups: products, audience-profiles, org-units, countries (+ languages), evidence-types, workflow-template.
- **CRM kuralları ve hata kodları** (UI'da kullanıcı diline çevrilecek):
  - `evidence_required`, `evidence_locked`, `in_review_locked`, `invalid_status`;
  - `approval_template_missing`, `approval_forbidden`, `workflow_unavailable`, `evidence_unavailable`;
  - `claim_product_mismatch`, `reference_set_missing`, `approval_via_workflow_only`.
  - Biçim `[code, message]`.
- **Kanıt modeli (MOD-0031 / BE-5):**
  - Bağ: `{documentKind (controlled|external), documentId, documentVersionId? (controlled zorunlu), evidenceTypeCode, locator{section, page, table, quote*}, supportedSpans[{languageCode, text, start?, end?}] ≤10}`.
  - Okuma alanları: `isSuperseded`, `documentState`, `reviewDueAt`, `origin`.
- **Mevcut** `Views/CRM/Claims/{Create, Edit, _Form}.cshtml` + `form.js` MVC form-post tabanlı → **JS + proxy tabanlı** sayfaya dönüşür (Zincir Şablonu editörü deseni). Route adları korunur (`/CRM/Claims/Create`, `/CRM/Claims/Edit/{id}`).

## NE
1. **Sayfa: Oluştur / Düzenle** (tam sayfa; `Create?kind=core|local`, `Edit/{id}`). **Sol ana alan / sağ yan panel** (Task Create 8/4 düzeni).
   - **Kimlik ve metin:**
     - Kod: ürüne göre öneri `CLM-{ÜRÜN}-{NN}`, düzenlenebilir, oluşturulduktan sonra kilitli.
     - Ad*, açıklama.
     - Sürüm (salt okunur 1.0 / mevcut). Durum rozeti (salt okunur).
     - **Metin** (çekirdek: dil `en` sabit; yerel: **ülke** seçimi `COUNTRY_CODES` + metin dili o ülkenin dillerinden). Karakter sayacı.
   - **Geçerlilik alanı:**
     - **Ürün*** (MDM seçici).
     - **Hedef kitle** (çoklu AudienceProfile).
     - **Sorumlu ekip** (org birimi, opsiyonel).
     - Not: "Ülke, dil ve geçerlilik tarihleri her ülke sürümünde ayrıca belirlenir."
   - **Niteleyiciler:** satır ekle / sil listesi.
   - **Kanıtlar:** liste kartları (tip, belge başlığı, id · sabit sürüm · kapsam · referans, "alıntı", "Desteklediği ifade", `isSuperseded` / süresi doluyor uyarısı) + **"Kanıt ekle"** + kaldır (gerekçe modalı). Kilitliyse (`evidence_locked`) düğmeler gizli. Kayıt henüz kaydedilmediyse "Önce taslağı kaydedin" notu (kanıt ancak kayıtlı nesneye bağlanır).
2. **Yan panel:**
   - **Onaya hazırlık** kontrol listesi, canlı hesaplanır: ad · metin · ürün · ≥1 kanıt · kaydedildi. Eksikler "eksik" rozeti.
   - **Onay akışı önizlemesi:** `workflow-template?code=CLAIM-CORE-MLR` (yerelde `CLAIM-LOCAL-MLR-{ülke}`) → sıralı adımlar (Medikal → Hukuk → Ruhsat) + aday pozisyon adları. Kişi adı varsa göster.
   - **Durum kartı:**
     - Taslak → **"Taslağı kaydet"**, **"Onaya gönder"** (onay modalı).
     - İncelemede → **"Onaydan geri çek"** + "İncelemede" bandı, form kilitli.
     - Onaylı / gözden geçirilmeli → kilit bandı + **"Yeni sürüm aç"**.
3. **Kanıt ekleme modalı** (senaryo 5):
   - **Adım 1 · Belge seç:**
     - Arama + tür (Kontrollü / Harici). `document-options`.
     - Liste satırı: başlık, id, tip, sürüm, kapsam; süresi doluyor uyarısı.
     - "Belge Yönetimi'ne yeni belge yükle" bağlantısı (yeni sekmede DocMgmt sayfası).
   - **Adım 2 · Referans ve eşleme:**
     - Seçilen belge özeti + "**{sürüm} sabitlenecek**".
     - **Kanıt tipi** (evidence-types, tek seçim).
     - Bölüm / Sayfa / Tablo.
     - **Alıntılanan cümle*** .
     - **Desteklediği ifade***: iddia metni gösterilir, kullanıcı **fareyle metin parçası seçip işaretler** → `supportedSpans{languageCode, text, start, end}`. Birden çok parça olabilir.
   - "Kanıtı ekle" → link ucu. Hata kodları kullanıcı diline çevrilir.
   - **Önizle** (sade, senaryo 6): belge Belge Yönetimi önizlemesinde **sabit sürümle** yeni sekmede açılır; sayfa varsa `#page=`. Belge içinde vurgu YOK (MVP dışı).
4. **Onaya gönder:**
   - Kaydedilmemiş değişiklik varsa önce kaydet.
   - Sonra submit-review. Başarıda "Onaya gönderildi" toast + form kilitlenir.
   - Hata → kullanıcı dilinde neden + çözüm. Örnek: `evidence_required` → "En az 1 kanıt ekleyin"; `approval_template_missing` → "Onay akışı tanımlı değil, yöneticinize başvurun".
5. **Geri çek:** withdraw-review (onay modalı). `withdraw_not_possible` → açıklama.
6. **Liste bağlantıları:** FE-1 listesindeki "Yeni çekirdek / yerel iddia" ve "Düzenle" bu sayfaya gelir. Yetkisiz → iskelet YOK (UAS-001).
7. **L10n:** **yeni resx ailesi `ClaimsForm.{7 dil}.resx`** + köprü. `ClaimsIndex` resx'ine dokunulmaz (FE-2 ile çakışmasın).

## KORU / YAPMA
- **FE-1 proxy katmanı DEĞİŞMEZ.** Eksik bir proxy gerekirse **ekleme** yapılabilir, mevcut yollar değişmez. **CRM, Platform, Auth DOKUNMA.**
- Liste sayfası (FE-1) DOKUNMA; yalnız bağlantılar.
- **Ülke sürümü formu, kapsama matrisi, detay sekmeleri bu pakette YOK** (FE-4 / FE-2 / FE-6).
- **Ülke kısıtı YOK.** Ruhsat no ve sahibi YOK. Paralel onay YOK.
- Golden Reference görsel dili; mockup renkleri tema değişkenlerine; açık ve koyu tema. Metin seçimiyle işaretleme **klavyeyle de** yapılabilir olmalı (erişilebilirlik; en azından "seçili metni ekle" düğmesiyle).
- **Tarayıcı:** oturumlu sekmeye mock yok, ayrı sekme. **Canlıda yazma yalnız kullanıcı onayıyla.** E2E canlı test ayrı pakette (CL-E4-1).
- **DUR:**
  - Belge Yönetimi'nde sabit sürüm önizlemesi için sürüm parametreli bir uç yoksa → önizlemeyi "Belge Yönetimi'nde aç" bağlantısıyla sınırla ve raporla.
  - Mevcut Create / Edit MVC post'unu kaldırmak başka bir sayfayı bozuyorsa → dur ve raporla.

## Acceptance
- **E2:**
  - Web testleri 0 kırmızı (taban FE-1 sonrası).
  - Web build 0 hata. Verifier varsa PASS.
  - Yeni testler: L10n 7 dil eşliği; proxy eklemeleri varsa izin kapıları.
- **E4 (fleet restart sonrası; yazma yalnız kullanıcı onayıyla, ayrı sekme):**
  - Çekirdek form açılıyor. Ürün, kitle ve org seçicileri doluyor.
  - Taslak kaydediliyor.
  - Kanıt modalı: belge listesi + sürüm sabitleme + işaretleme.
  - Hazırlık listesi canlı güncelleniyor. Akış önizlemesi Medikal → Hukuk → Ruhsat.
  - Kanıtsız gönderimde anlaşılır mesaj. Kilit bantları.
  - TR + bir diğer dil. Koyu tema.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner (FE-1 birleştikten SONRA)
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-CL-FE-3 · Çekirdek/yerel iddia formu + kanıt ekleme modalı + onaya hazırlık + onaya gönder/geri çek (frontend)
Repository: C:\tmp\cl-fe-3 (worktree) · Branch: wp/cl-fe-3 (taban: FE-1 birleşmiş test/crm-content-visit-e2e) · commit bu dala, push YOK

Amaç: Kullanıcı çekirdek/yerel iddiayı oluştursun, kanıt bağlasın (belge seç → sürüm sabitle → tip/konum/alıntı → iddia metninde desteklenen ifadeyi işaretle), hazırlık listesini görsün ve onaya göndersin/geri çeksin. Bu paketle canlı uçtan uca onay testi mümkün olur.

Önce oku: execution/domains/commercial-suite/work-packs/WP-CL-FE-3-core-claim-form-evidence-submit.md · execution/domains/commercial-suite/work-packs/mockups/claims-v2/ (senaryolar core/evid/prev; screens.decoded.html + logic-and-sample-data.decoded.js [EV, DOCS, MDOCS, SEGS]) · …/WP-CL-FE-1-claims-list-quickview-proxy.md (proxy yolları) · frontend/Diten.Web/Controllers/CRM/ClaimsController.cs (FE-1 proxy'leri) · Views/CRM/Claims/{Create,Edit,_Form}.cshtml + wwwroot/assets/js/CRM/Claims/form.js (dönüşecek) · Zincir Şablonu editörü (Views/CRM/KnowledgeConcepts/_TemplateForm.cshtml + template-form.js) JS+proxy+8/4 deseni için · services/Diten.CrmService/src/**/Claims/ClaimV2Support.cs (hata kodları) + ClaimEvidenceCore.cs · memory l10n-bridge-pascalcase-loader.

NE:
 1) Create?kind=core|local / Edit/{id} tam sayfa, 8/4: Kimlik ve metin (kod öneri CLM-{ÜRÜN}-{NN}, oluşturunca kilitli; ad*; açıklama; sürüm+durum salt okunur; metin: çekirdek en sabit / yerel ülke [COUNTRY_CODES] + o ülkenin dilleri; sayaç) · Geçerlilik alanı (ürün* MDM, kitle çoklu, sorumlu ekip ops.) · Niteleyiciler · Kanıtlar (kartlar: tip, başlık, id·sabit sürüm·kapsam·referans, alıntı, desteklediği ifade, isSuperseded/süresi doluyor uyarısı; Kanıt ekle; kaldır gerekçeli; kilitliyse gizli; kaydedilmemişse "Önce taslağı kaydedin").
 2) Yan panel: Onaya hazırlık (ad, metin, ürün, ≥1 kanıt, kaydedildi — canlı), onay akışı önizlemesi (workflow-template CLAIM-CORE-MLR / CLAIM-LOCAL-MLR-{ülke} adımları + aday pozisyon adları), durum kartı (taslak: Taslağı kaydet + Onaya gönder; incelemede: Onaydan geri çek + kilit; onaylı/gözden geçirilmeli: kilit bandı + Yeni sürüm aç).
 3) Kanıt modalı: Adım 1 belge seç (arama + Kontrollü/Harici, document-options, süresi doluyor, "Belge Yönetimi'ne yeni belge yükle" yeni sekme) → Adım 2 ("{sürüm} sabitlenecek", kanıt tipi, bölüm/sayfa/tablo, alıntı*, desteklediği ifade* = iddia metninden fareyle parça seç + klavyeyle "seçili metni ekle" → supportedSpans{lang,text,start,end}); Kanıtı ekle; Önizle = DocMgmt önizleme sabit sürümle yeni sekme (#page), belge içi vurgu YOK.
 4) Onaya gönder: önce kaydet → submit-review; başarı toast + kilit; hata kodlarını kullanıcı diline çevir (evidence_required, approval_template_missing, approval_forbidden, workflow_unavailable, evidence_unavailable, in_review_locked, invalid_status…).
 5) Geri çek: withdraw-review (onay modalı), withdraw_not_possible açıklaması.
 6) FE-1 listesinden Yeni/Düzenle bu sayfaya; yetkisizde iskelet YOK.
 7) Yeni resx ailesi ClaimsForm.{en,tr,fr,es,zh,ar,ru}.resx + köprü (ClaimsIndex resx'e DOKUNMA).
KORU/YAPMA: FE-1 proxy'leri DEĞİŞMEZ (yalnız ekleme); CRM/Platform/Auth DOKUNMA; liste sayfası DOKUNMA; ülke sürümü formu/matris/detay sekmeleri YOK; ülke kısıtı/ruhsat no/paralel onay YOK; Golden Reference + tema değişkenleri + açık/koyu tema; oturumlu sekmeye mock yok, ayrı sekme, canlı yazma yalnız kullanıcı onayıyla.
DOĞRULA (E2): Web testleri 0 kırmızı (taban FE-1 sonrası); Web build 0 hata; verifier varsa PASS; yeni testler L10n 7 dil eşliği + (varsa) proxy eklemeleri izin kapıları. E4 (fleet restart sonrası, ayrı sekme, yazma yalnız onayla): form + seçiciler, taslak kaydet, kanıt modalı (liste, sürüm sabitleme, işaretleme), hazırlık canlı, akış önizlemesi Medikal→Hukuk→Ruhsat, kanıtsız gönderim mesajı, kilit bantları, TR + bir dil, koyu tema. Commit ("feat(crm): WP-CL-FE-3 — core/local claim form, evidence modal, readiness, submit/withdraw review" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: DocMgmt'te sürüm parametreli önizleme ucu yoksa önizlemeyi "Belge Yönetimi'nde aç" bağlantısıyla sınırla + raporla; eski Create/Edit MVC post'unu kaldırmak başka sayfayı bozuyorsa DUR+raporla.
```
