# WORK PACKAGE — WP-KP-UI-2 · Bilgi Yolu Stüdyosu — uyum · MLR + inceleyici görünümü · revizyonlar + fark · çıktı ve yayın · önizleme · kullanım · eski yol sihirbazı (frontend)

> **CT (SoR).**
> - **Tasarım:** `DESIGN-KP-STUDIO-knowledge-path-studio.md` §3.2–§3.7, §4 (D-KP-4 / 6 / 8), §6.
> - **Mockup v2:** `mockups/kp-studio/` (`kp-studio-screens-v2.decoded.html`, `kp-studio-screen-texts.txt`); sekmeler Uyum, MLR onayı, Çıktı ve yayın, Revizyonlar, Saha önizleme, Kullanım + inceleyici görünümü + modallar.
> - **Bağımlılıklar** (birleşti): KP-1 (`95c2444d`), KP-UI-1 (`702d76d5`), KP-2 (`ccf3d8fe`), KP-3 (`892e7953`).
> - **Kapsam:** yalnız `frontend/Diten.Web`.
>
> **Çalışma yeri:** worktree `C:\tmp\kp-ui-2`, dal `wp/kp-ui-2`, taban `test/crm-content-visit-e2e`. Commit bu dala, push YOK.

## Kapsam dışı
- Sayfa tasarımcısı ve sayfa bazlı (tuval) notlar → KP-UI-3.
- HTML önizleme → SB-4.
- Ülkeye uyarla, AI yerleşim, saha etkisi uyarısı.

**Sayfalar henüz yok.** Bu yüzden inceleyici görünümü ve notlar **kurgu üzerinden** çalışır: dondurulmuş revizyonun adım kartları + iddia blokları; not `StepRef` / `BlockRef` ile. Sayfa tuvali KP-UI-3'te gelir.

## Kanıt (CT) — CRM uçları (KP-2 / KP-3)
`api/crm/knowledge/paths/{pathId}/…`:

| Uç | Ne |
|---|---|
| `POST submit-review` / `POST withdraw-review` | onaya gönder / geri al |
| `GET revisions` · `GET revisions/{revId}` | revizyon listesi; detay = anlık görüntü + `ReviewRound` + `Notes` + `ChangeSummary` |
| `POST revisions/{revId}/decision` `{approve \| reject, comment}` | **tek kanal karar** (K1) |
| `POST revisions/{revId}/notes` · `…/notes/{noteId}/resolve` | notlar |
| `GET review-history` | Platform geçmişi: adım adı + karar + kişi + tarih + **yorum** |
| `POST revisions/{revId}/render` · `GET revisions/{revId}/artifact?kind=pdf` | çıktı |
| `POST revisions/{revId}/release` · `POST revisions/{revId}/withdraw` `{reason}` | yayın / geri çekme |
| `GET usage` | yolculuk aşamaları (pin politikası / sürüm) + strateji şablonları |
| `POST bind-chain`, adım güncelleme (`arrangement`), `POST claims` | eski yol sihirbazı (KP-1) |

**Hata kodları** (kullanıcı dilinde gösterilecek):
- **KP-2:** `chain_template_required`, `review_round_open`, `chain_conformance_failed`, `component_not_published`, `component_language_mismatch`, `claim_no_country_version`, `approval_template_missing`, `workflow_unavailable`, `comment_required`, `sod_submitter_cannot_decide`, `approval_via_workflow_only`.
- **KP-3:** `revision_not_approved`, `artifact_missing`, `revision_superseded`, `revision_not_released`, `sod_submitter_cannot_release`, `reason_required`, `path_in_use`, `previous_path_in_use` (uyarı), `artifact_store_unavailable`, `claim_not_approved`, `claim_language_mismatch`.

**İzinler:**
- gönder / geri al: `crm.knowledge.path.manage`;
- karar / not: `read` (kim karar verebilir → MOD-0023 adayları + SoD);
- render / yayın / geri çekme: `crm.knowledge.path.publish`.

**Kanıt paneli:** İddialar v2 Web proxy'si
- `GET /CRM/Claims/api/v2/claims/{id}/evidence`;
- `GET /CRM/Claims/api/v2/claims/country-versions/{id}/evidence`;
- belge önizleme bağlantısı (FE-3 / FIX-1 kartları).

Kanıt paneli **salt okunur**; yeniden kullan, kopyalama (`claim-evidence.js` kart çizimi).

**KP-UI-1 altyapısı:**
- `KnowledgePathsController.Studio.cs` (proxy allowlist, lookup);
- `Workspace.cshtml`, `workspace.js`, `studio-common.js` (API sarmalayıcı, hata kodu → metin, L10n);
- resx `KnowledgePathStudio.*`;
- ortak `ClaimCoverageOptions`, `ReferenceValueSet`.

## NE
1. **Proxy:** yukarıdaki KP-2 / KP-3 uçları için izinli proxy (allowlist; mevcut desen).
   - `artifact` PDF akışı: bodiless / 404 guard (memory `proxy-forward-204-content-length-crash`).
2. **Çalışma alanı sekmeleri** (KP-UI-1 kabuğuna eklenir): Kurgu (var) · **Uyum** · **MLR onayı** · **Çıktı ve yayın** · **Revizyonlar** · **Saha önizleme** · **Kullanım**.
   - Sekmeler role / izne göre görünür; boş sekme yok.
   - **Tek durum çizgisi:** taslak / incelemede (adım) / onaylı / yayında + "Sahada vX".
   - **"Sıradaki adım" bandı** duruma göre: engeli düzelt → onaya gönder → onay bekleniyor → çıktı oluştur → yayınla.
3. **Uyum sekmesi** (KP-UI-3'e kadar kurgu kuralları):
   - zincir uyumu (eksik / fazla);
   - yayında olmayan içerik;
   - dil;
   - iddia kullanılabilirliği (onaysız / ülke sürümü yok / dil).
   - Her satırda önem (engelleyici / uyarı), kural, yer (dal › adım › öğe), **kullanıcı dilinde** açıklama + nasıl düzeltilir + **"Kurguya git"** (ilgili öğeye kaydırır ve vurgular).
   - Ham hata kodu yok (mockup v2 notu; gerekirse ipucunda).
4. **Onaya gönder** (modal):
   - kontrol listesi (uyum sonucu): engelleyici varsa "Gönder" kapalı;
   - **değişiklik özeti önizlemesi** (son revizyona göre);
   - açık tur varsa "Rev N incelemede (Hukuk adımı)…" bilgisi ve gönderim kapalı.
   - **Geri al** (gönderen ya da manage).
5. **MLR onayı sekmesi:**
   - **zaman çizelgesi:** gönderim → Medikal → Hukuk → Ruhsat; her adımda durum, kişi, **tarih**, **yorum** (`review-history`);
   - bekleyen adım ve aday pozisyonlar;
   - **"İnceleyici görünümü"** düğmesi.
   - Not: "Kararınız Görev Merkezi'ndeki onay iş akışına yazılır; Görev Merkezi'nden verilen karar da buraya yansır."
6. **İnceleyici görünümü:** route `/CRM/KnowledgePaths/{pathId}/Review/{revisionId}`. KP-2'nin DisplayContext bağlantısı bu adrese işaret ediyor; **bu sayfa yoksa WCN bağlantısı kırılır**.
   - **İzin:** read; yetkisize düz 403.
   - **Sol:** revizyonun dondurulmuş kurgusu (dal / adım; içerik ve iddia blokları); not sayıları.
   - **Orta:** seçili adım. İddia bloğu metni (yolun dilinde ülke sürümü) + niteleyici. **İddiaya tıklayınca sağda kanıt paneli** (salt okunur; belge kodu, sürüm, durum rozeti, alıntı, önizleme bağlantısı).
   - **Notlar:**
     - adım ya da bloğa not ekle (`StepRef` / `BlockRef`);
     - not akışı: yazar, tarih, çözüldü işareti;
     - önceki revizyondan taşınan notlar işaretli;
     - çöz (yazar ya da manage).
   - **Karar paneli:**
     - adım adı ("Karar · Hukuk inceleme");
     - Onayla (not isteğe bağlı) / Reddet (**yorum zorunlu**, istemcide de doğrula).
     - **Gönderen kişiye karar alanı gösterilmez** (kişi bazında; oturumdaki kullanıcı kimliği = revizyonu gönderen → "Kendi gönderdiğiniz revizyon için karar veremezsiniz"). Sunucu da 403 döner.
     - Aday değilse sunucu 403 → anlaşılır mesaj.
7. **Revizyonlar sekmesi:**
   - **üçlü durum kartı:** Sahada / Onayda / Üzerinde çalışılan; sürüm ↔ revizyon farkı açıklaması;
   - revizyon zaman çizelgesi (numara, tarih, gönderen, sonuç, çıktı, yayın);
   - **"Farkı gör":** iki revizyon seçici + adım / iddia / içerik sürümü değişiklikleri. `ChangeSummary` + iki anlık görüntünün istemci tarafı karşılaştırması.
8. **Çıktı ve yayın sekmesi** (publish izni):
   - **Çıktı oluştur:** onaylı revizyon için. Dosya adı, boyut, tarih, **parmak izi**; PDF indir / görüntüle.
   - **Yayın ön koşulları listesi:** onaylı, çıktı hazır, içerikler yayında, tek dil, iddialar kullanılabilir, uyum, "yayınlayan ≠ gönderen". Eksikte düğme kapalı + neden.
   - **Yayınla:**
     - onay penceresi ("yayınlanınca sahadaki vX yerini alır");
     - `previous_path_in_use` uyarısını aşama adlarıyla göster.
   - **Geri çek:**
     - gerekçe zorunlu;
     - `path_in_use` 409'da kullanan aşamaları listele, "Önce bu aşamaları başka sürüme bağlayın".
9. **Saha önizleme sekmesi:**
   - yayındaki ya da onaylı revizyonun **PDF'i** (gömülü görüntüleyici) + revizyon no + parmak izi;
   - onaysız taslak için "Taslak — onaydan sonra çıktı oluşur" bilgisi.
   - HTML önizleme YOK (SB-4).
10. **Kullanım sekmesi:**
    - yolculuk aşamaları (yolculuk adı, aşama, durum, pin politikası / sürüm) + strateji şablonları; bağlantılar;
    - sayfa gösterim verisi YOK (sonraki faz; boş durum metni).
11. **Eski yol sihirbazı** (K4; KP-UI-1'in basit bağlama modalının yerini alır):
    1. zincir + ülke + dil (`bind-chain`);
    2. eski adımları zincir adımlarına **eşle** (dal / adım seçimi; adım güncellemesi `arrangement`; eşlenmeyen adım uyarısı);
    3. **iddia önerileri:** adım içeriklerinin Bilgi İçeriği `ClaimRefs`'inden gelen iddialar; **her biri için Kabul / Reddet**; karar verilmeden "İleri" kapalı; kabul edilenler `POST claims` (otomatik bağlama YOK);
    4. özet + "Onaya gönder" (isteğe bağlı).
12. **L10n ve erişilebilirlik:**
    - `KnowledgePathStudio` resx 7 dil (yeni anahtarlar) + köprü;
    - ar RTL (mantıksal CSS);
    - açık / koyu tema (tema değişkenleri);
    - klavye + `aria`.

## KORU / YAPMA
- CRM / Platform / Auth DOKUNMA.
- İddialar ekranları ve kanıt modalı **davranışı DEĞİŞMEZ** (yalnız salt okunur kart yeniden kullanımı).
- KP-UI-1 kurgu davranışı DEĞİŞMEZ (sekme eklemek dışında).
- Kanıt **ekleme / kaldırma** inceleyici görünümünde YOK (salt okunur).
- Tarayıcıda ayrı sekme, canlı yazma YOK.
- **DUR:**
  - oturumdaki kullanıcı kimliği (kişi bazlı SoD gizlemesi için) Web'de güvenilir şekilde okunamıyorsa → dur, raporla. Sunucu 403'ü yine korur; yalnız UI gizlemesi etkilenir.
  - KP-2 / KP-3 DTO'larında ekran için gerekli bir alan yoksa → CRM'e dokunmadan dur, raporla.

## Acceptance
- **E2:**
  - Web testleri 0 kırmızı (taban 368). Build 0 hata. `node --check` temiz.
  - **Yeni testler:**
    - proxy allowlist (yeni uçlar; artifact bodiless guard);
    - inceleyici görünümü route + izin (düz 403);
    - kişi bazlı karar gizleme (gönderen ↔ aday);
    - ret yorumu istemci doğrulaması;
    - hata kodları → metin (KP-2 + KP-3 kodları);
    - sihirbaz adım kapısı (karar verilmeden ileri yok);
    - L10n 7 dil + JS ↔ resx eşliği;
    - ham kod görünmemesi.
  - **Sabotaj:** inceleyici görünümü izin kapısı ve gönderen karar gizlemesi testleri kırmızıya dönmeli.
- **E4 (CT):** fleet restart + KP-2-CFG şablonları canlıda + TPL-ALMIBA-01 yayında →
  1. ALMIBA TR yolu kur;
  2. onaya gönder;
  3. sema WCN'den ya da inceleyici görünümünden yorumla onaylar;
  4. çıktı;
  5. Admin dışında bir yayınlayan → yayın.
  - Yayınlayan kuralı: gönderen yayınlayamaz. Canlıda sema yayınlayabilir.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-ui-ux.md]
WP: WP-KP-UI-2 · Bilgi Yolu Stüdyosu — uyum · MLR + inceleyici görünümü · revizyonlar + fark · çıktı ve yayın · önizleme · kullanım · eski yol sihirbazı (frontend)
Repository: C:\tmp\kp-ui-2 (worktree) · Branch: wp/kp-ui-2 · commit bu dala, push YOK · yalnız frontend/Diten.Web

Amaç: Mockup v2'nin kalan sekmeleri ve inceleyici görünümü, KP-2/KP-3 uçları üzerine. Sayfa tasarımcısı yok (KP-UI-3) → inceleyici görünümü ve notlar dondurulmuş KURGU üzerinden (StepRef/BlockRef). HTML önizleme yok (SB-4). /CRM/KnowledgePaths/{pathId}/Review/{revisionId} rotası ZORUNLU (WCN bağlantısı buraya gidiyor).

Önce oku: execution/domains/commercial-suite/work-packs/WP-KP-UI-2-path-mlr-release-screens.md · …/DESIGN-KP-STUDIO-knowledge-path-studio.md · …/mockups/kp-studio/{KP-STUDIO-mockup-analysis.md, kp-studio-screens-v2.decoded.html, kp-studio-screen-texts.txt} · …/WP-KP-2-path-revision-mlr.md + WP-KP-3-path-render-release.md (§37, uçlar, hata kodları) · …/WP-KP-UI-1-path-list-workspace-kurgu.md (§37) · frontend/Diten.Web/Controllers/CRM/{KnowledgePathsController.cs, KnowledgePathsController.Studio.cs, ClaimsController.V2.cs (evidence proxy)} · Views/CRM/KnowledgePaths/** · wwwroot/assets/js/CRM/KnowledgePaths/{workspace, studio-common, index}.js · wwwroot/assets/js/CRM/Claims/claim-evidence.js (kart çizimi, salt okunur yeniden kullan) · services/Diten.CrmService/src/**/Knowledge/Path/{Review, Release}/*Contracts*.cs (DTO sözleşmesi, okumak için) · memory proxy-forward-204-content-length-crash, l10n-bridge-pascalcase-loader.

NE:
 1) Proxy allowlist: submit-review, withdraw-review, revisions (liste/detay), decision, notes (+resolve), review-history, render, artifact (PDF akışı, bodiless/404 guard), release, withdraw, usage.
 2) Sekmeler (KP-UI-1 kabuğuna): Kurgu · Uyum · MLR onayı · Çıktı ve yayın · Revizyonlar · Saha önizleme · Kullanım (izne göre; boş sekme yok); tek durum çizgisi (taslak/incelemede(adım)/onaylı/yayında + "Sahada vX"); duruma göre "sıradaki adım" bandı.
 3) Uyum: zincir uyumu, yayında olmayan içerik, dil, iddia kullanılabilirliği; satırda önem/kural/yer/kullanıcı dilinde açıklama + düzeltme + "Kurguya git" (kaydır+vurgula); ham kod yok.
 4) Onaya gönder modalı: kontrol listesi (engelleyici varsa kapalı), değişiklik özeti önizlemesi, açık turda bilgi + kapalı; Geri al.
 5) MLR sekmesi: zaman çizelgesi (gönderim → Medikal → Hukuk → Ruhsat; durum/kişi/TARİH/YORUM review-history'den), bekleyen adım + aday pozisyonlar, "İnceleyici görünümü" düğmesi, tek kanal notu.
 6) İnceleyici görünümü /CRM/KnowledgePaths/{pathId}/Review/{revisionId} (read; yetkisize düz 403): sol dondurulmuş kurgu + not sayıları; orta seçili adım, iddia bloğu metni+niteleyici, iddiaya tıklayınca sağda salt okunur kanıt paneli (Claims v2 evidence proxy; belge kodu/sürüm/durum/alıntı/önizleme); notlar (adım/bloğa ekle, akış, çözüldü, taşınan işaretli, çöz); karar paneli (adım adı; Onayla not isteğe bağlı / Reddet yorum ZORUNLU istemcide de; GÖNDEREN KİŞİYE karar alanı gösterilmez — kişi bazında; aday değil 403 → anlaşılır mesaj).
 7) Revizyonlar: üçlü durum kartı (Sahada/Onayda/Çalışılan) + sürüm↔revizyon açıklaması; zaman çizelgesi; "Farkı gör" (iki revizyon seçici; ChangeSummary + iki anlık görüntünün istemci karşılaştırması).
 8) Çıktı ve yayın (publish izni): Çıktı oluştur (onaylı revizyon; dosya adı/boyut/tarih/parmak izi; PDF indir/görüntüle); ön koşul listesi (eksikte düğme kapalı + neden, "yayınlayan ≠ gönderen" dahil); Yayınla (onay penceresi; previous_path_in_use uyarısını aşama adlarıyla); Geri çek (gerekçe zorunlu; path_in_use 409'da aşamaları listele).
 9) Saha önizleme: yayındaki/onaylı revizyon PDF'i gömülü + revizyon no + parmak izi; taslakta bilgi metni.
 10) Kullanım: yolculuk aşamaları (ad, aşama, durum, pin politikası/sürüm) + strateji şablonları + bağlantılar; gösterim verisi yok (boş durum).
 11) Eski yol sihirbazı (KP-UI-1 basit modalının yerine; K4): 1 zincir+ülke+dil (bind-chain) → 2 eski adımları zincir adımlarına eşle (arrangement; eşlenmeyen uyarısı) → 3 iddia önerileri (adım içeriklerinin ClaimRefs'i; her biri Kabul/Reddet; karar verilmeden İleri kapalı; kabul → POST claims; otomatik bağlama YOK) → 4 özet + isteğe bağlı Onaya gönder.
 12) L10n/erişilebilirlik: KnowledgePathStudio resx 7 dil (yeni anahtarlar) + köprü; ar RTL (mantıksal CSS); tema değişkenleri; klavye + aria. Tüm KP-2/KP-3 hata kodları kullanıcı dilinde.
KORU/YAPMA: CRM/Platform/Auth DOKUNMA; İddialar ekranları + kanıt modalı davranışı DEĞİŞMEZ (yalnız salt okunur kart yeniden kullanımı); KP-UI-1 kurgu davranışı DEĞİŞMEZ; inceleyici görünümünde kanıt ekleme/kaldırma YOK; tarayıcıda ayrı sekme, canlı yazma YOK.
DOĞRULA (E2): cd C:\tmp\kp-ui-2; dotnet test frontend/Diten.Web.Tests -c Release --nologo → 0 kırmızı (taban 368); Web build 0 hata; node --check temiz. Yeni testler: proxy allowlist (yeni uçlar + artifact bodiless guard), inceleyici görünümü route + düz 403, kişi bazlı karar gizleme (gönderen↔aday), ret yorumu istemci doğrulaması, KP-2+KP-3 hata kodları → metin, sihirbaz adım kapısı, L10n 7 dil + JS↔resx, ham kod görünmemesi. Sabotaj: inceleyici izin kapısı + gönderen karar gizlemesi testleri kırmızıya dönmeli. Commit ("feat(crm): WP-KP-UI-2 — knowledge path studio compliance, MLR + reviewer view, revisions, output & release, preview, usage, legacy wizard" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: oturumdaki kullanıcı kimliği Web'de güvenilir okunamıyorsa ya da KP-2/KP-3 DTO'larında ekran için gerekli alan yoksa (CRM'e dokunmadan) DUR + raporla.
```

---

## §37 — CT bağımsız doğrulama (2026-10-01) — **ACCEPTED (E2)**
- **Commit:** `01168702` (`wp/kp-ui-2`) → `test/crm-content-visit-e2e` fast-forward. 26 dosya, yalnız `frontend/`. Ana checkout temiz: ajanın önizleme denemesi iz bırakmamış.
- **Diff (K13 okuma):**
  - **Yeni dosyalar:** `KnowledgePathsController.Review.cs` + `KnowledgePathStudioReview.cs` (saf kurallar);
  - **İnceleyici rotası:** `/CRM/KnowledgePaths/{pathId}/Review/{revisionId}`, `RequirePage(Read)`;
  - **Ekran parçaları:** `_ReviewTabs`, `_SubmitModal`, `_LegacyWizard`;
  - **Betikler:** `workspace-review.js`, `review.js`, `legacy-wizard.js` (HTML üretimi `esc()` ile);
  - `_BindChainModal` kaldırıldı (sihirbaz yerini aldı).
- **CT testleri:** Web **399/0** (368 + 31). `node --check` 6 dosya temiz.
- **CT sabotajı:** inceleyici görünümü izin kapısı kaldırıldı → `KnowledgePathReview` 1 kırmızı. Kod geri alındı. Ajan: gönderen karar gizlemesi (2 kırmızı).
- **DUR yok:**
  - kişi kimliği Web'de CRM ile aynı sırayla okunuyor (`sub` → NameIdentifier → e-posta → ad);
  - bekleyen adım / adaylar Platform şablonundan okunuyor (İddialar V2 deseni);
  - kişi adları geçmişin görünen adından geliyor, kimlik numarası gösterilmiyor.
- **Ajan notları (kabul):**
  - canlı tarayıcı doğrulaması yapılamadı (önizleme aracı yalnız ana checkout'u çalıştırıyor) → E4 CT;
  - kanıt paneli `crm.claim.read` ister (yoksa "yetkiniz yok");
  - sihirbazın 1. adımı zinciri hemen bağlar, ülke / dil o anda kilitlenir;
  - liste ekranı yeni sapma eklemedi (12 bilinen sapma aynen).
- **E4:** fleet restart + KP-2-CFG şablonları + TPL-ALMIBA-01 yayında → uçtan uca (kurgu → gönder → sema yorumla onaylar → çıktı → sema yayınlar).
