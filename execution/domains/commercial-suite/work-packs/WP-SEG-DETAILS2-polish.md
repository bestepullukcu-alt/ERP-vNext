# WORK PACKAGE — WP-SEG-DETAILS2 · Details görsel/işlev rötuşları (frontend)

> **CT (SoR).** MOD-0167-FU02 Segments. Branch `feature/scmm-content-studio` (`00a6c03c` üstü). **Yalnız frontend (Diten.Web).** Owner 7 maddelik geri bildirim (SEG-DETAILS sonrası).

## Maddeler (CT teşhisiyle)
1. **Stats kartları bg yok** — `segd-stat` background/border tanımsız (düz). → kart yüzeyi ver: `background: var(--bs-card-bg)` + `border: 1px solid var(--bs-border-color)` + radius (mockup stat kartı gibi). Tema-duyarlı.
2. **Version literal bug** — `Details.cshtml:37` `<span class="badge bg-label-primary">v@segment.SegmentVersion</span>`: Razor `v@` bitişik → `@segment...`'ı e-posta sanıp **literal** basıyor. → **`v@(segment.SegmentVersion)`** (explicit expression). Ekranda gerçek versiyon (1) çıkacak.
3. **territory.node badge GUID** — criteria predicate value badge (`Details.cshtml` ~satır 25 `foreach value in node.Values`) ham GUID (`bd667afe-…`) basıyor. `segd-pred-human` (node.Label) zaten okunur cümle taşıyor. → entity-picker/GUID değerli value badge'inde: **human (node.Label) varsa value badge'ini gizle**, VEYA değeri isimle çöz. Backend criteria value-label taşımıyorsa (server-render), en temiz: GUID-benzeri value (36-char UUID) için value badge'i gizle (human satırı yeterli); isim çözümü backend gerektiriyorsa DUR+raporla. **Reference-set/enum değerler (Cardiology gibi okunur) badge'de KALIR.**
4. **Stored-note kart-içinde-kart** — `segd-stored` bölümünü app'in **`card shadow-none bg-label-primary`** deseniyle yap: `<div class="card shadow-none bg-label-primary"><div class="card-body"><h5 class="card-title text-primary">…</h5><p class="card-text">…</p></div></div>` (dış `section.card mb-4 > card-body p-4` sarmalını KALDIR — çift kart olmasın; bg-label-primary tek kart).
5. **Resolve preview + Members 0 çalışmıyor** — CT teşhisi: `details.js runResolve` fetch (`/segments/{id}/resolve`) + render **DOĞRU**; boş sonuç geçersiz `territory.node` GUID'inden (madde 3 ile aynı kök: kayıtlı değer geçerli node değil). → (a) boş-durum netleştir: 0 member'da `NoMembers` mesajı + "0 included" chip (hata değil, gerçek 0); (b) fetch **hata** (4xx/5xx) durumunda görünür hata satırı (sessiz boş kalmasın). **KALICI ÇÖZÜM veri/E4:** fleet restart (SEG-D/F/G + 431 fix canlı) + geçerli territory node ile yeniden test — bu WP kapsamı değil, not.
6. **group-head** — `segd-group-head` background `var(--bs-body-bg)` (veya tertiary-bg, tema-duyarlı); `segd-group-tag` ("Group") + `segd-group-op` (and/or) → **UPPERCASE** (CSS `text-transform: uppercase`, mockup gibi "GROUP" / "AND").
7. **Consumers kartı ekle** — mockup'ta "Used as the audience by / none yet" kartı vardı; SEG-DETAILS'te backend yok diye çıkarılmıştı. → **statik placeholder** olarak ekle (sağ ray): app-card + başlık + "none yet" boş-durum metni + "ileride bağlanacak" notu. **Uydurma sayı YOK** (gerçek consumer verisi gelene kadar hep "none yet"/boş). Backend bağlanınca doldurulur.

## KORU / YAPMA
- SEG-DETAILS davranışı DEĞİŞMEZ: 3-durum toggle, kayıtlı `/resolve`, lifecycle eylemleri, secondary label, criteria ağaç, JSON toggle, same-origin proxy, scope izolasyon (`.segment-details-scope`), tema-duyarlılık, app-card kabuk, font ayrımı. Backend/DTO/controller DOKUNMA (madde 3 backend gerektiriyorsa DUR). segment-create.css DOKUNMA. Uydurma veri (consumer sayısı) YOK. Başka modül.

## Acceptance
- **E2:** Diten.Web.Tests baseline-diff yeşil (137/0). (1) stats kartı bg+border (tema-duyarlı); (2) versiyon gerçek sayı (literal yok); (3) criteria value badge GUID göstermez (human yeterli / reference-set okunur değer kalır); (4) stored-note `bg-label-primary` tek kart (çift kart yok); (5) resolve 0-durumu NoMembers + hata görünür; (6) group-head bg + GROUP/AND uppercase; (7) consumers "none yet" placeholder kart. Scope izole + tema-duyarlı + davranış korundu.
- **E4:** fleet restart + geçerli territory node → resolve gerçek sayı (madde 5 kök).

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-SEG-DETAILS2 · Details görsel/işlev rötuşları (MOD-0167-FU02, frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/scmm-content-studio · Expected HEAD: <dispatch anındaki HEAD (00a6c03c üstü)> · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-SEG-DETAILS2-polish.md · frontend/Diten.Web/Views/CRM/Segments/Details.cshtml (satır 37 version, ~178 group-head, ~180 group-op, ~25 predicate value, stored-note, stats) + wwwroot/assets/css/segment-details.css (segd-stat/segd-group-head/segd-stored) + wwwroot/assets/js/CRM/Segments/details.js (runResolve/loadMembers).

NE (yalnız Details.cshtml + segment-details.css + details.js + gerekirse L10n):
 1) segd-stat: background var(--bs-card-bg) + border 1px var(--bs-border-color) + radius (tema-duyarlı kart yüzeyi).
 2) Details.cshtml:37 v@segment.SegmentVersion → v@(segment.SegmentVersion) (Razor e-posta tuzağı; gerçek versiyon çıksın).
 3) criteria predicate value badge: 36-char GUID/UUID value için badge'i GİZLE (segd-pred-human/node.Label okunur cümle yeterli); reference-set/enum okunur değer (Cardiology) badge'de KALIR. Backend criteria value-label gerekiyorsa DUR+raporla.
 4) stored-note: segd-stored bölümünü <div class="card shadow-none bg-label-primary"><div class="card-body"><h5 class="card-title text-primary">başlık</h5><p class="card-text">gövde</p></div></div> yap; dış section.card sarmalını KALDIR (çift kart olmasın).
 5) details.js resolve: 0 member'da NoMembers + "0 included" chip (hata değil); fetch 4xx/5xx'te görünür hata satırı (sessiz boş kalmasın). Fetch/render mantığını BOZMA (kayıtlı /resolve).
 6) segd-group-head background var(--bs-body-bg); segd-group-tag + segd-group-op text-transform:uppercase (GROUP/AND).
 7) sağ raya "Used as the audience by" placeholder kartı: app-card + başlık + "none yet" boş-durum + ileride-bağlanır notu. Uydurma sayı YOK.
KORU/YAPMA: 3-durum toggle/kayıtlı /resolve/lifecycle/secondary label/criteria ağaç/JSON toggle/proxy/scope/tema/app-card/font ayrımı DEĞİŞMEZ; backend/DTO/controller DOKUNMA (madde 3 backend→DUR); segment-create.css DOKUNMA; uydurma consumer sayısı YOK; başka modül.
DOĞRULA (E2): Diten.Web.Tests baseline-diff yeşil (137/0); 7 madde uygulandı; scope izole + tema-duyarlı + davranış korundu; git diff yalnız Details.cshtml/segment-details.css/details.js(/resx). Ayrı commit. §22 TÜRKÇE. K13.
Durma: madde 3 backend value-label gerektiriyorsa DUR+raporla; resolve mantığı değişmek zorundaysa DUR; kapsam Details dışına taşarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-17) → **ACCEPTED (E2)**
```text
Commit: 1900568b · Agent: PASS · CT: ACCEPTED E2 (gerçek build) · izole worktree /c/tmp/ct-det2-verify @1900568b
```
- ✅ **Scope:** Details.cshtml + segment-details.css + details.js + 7 resx. Backend/segment-create.css sızıntı=0.
- ✅ **7 madde:** (1) segd-stat bg+border zaten mevcut (tema-duyarlı, no-op teyit); (2) `v@segment.SegmentVersion` literal=0, `v@(segment.SegmentVersion)` ×2 (başlık+stat); (3) criteria value badge `value?.Length==36 && Guid.TryParse` → GUID gizli, okunur değer (Cardiology) kalır; (4) stored-note `card shadow-none bg-label-primary` tek kart (dış section kalktı, dead CSS temizlendi); (5) resolve 0→NoMembers + hata satırı korundu (kayıtlı /resolve mantığı bozulmadı); (6) segd-group-head bg `--bs-body-bg` + group-tag/op `text-transform:uppercase`; (7) "Used as audience by" placeholder kart (segd-empty "none yet", uydurma sayı yok) + L10n 3 anahtar × 7 dil.
- ✅ **Davranış korundu:** 3-durum toggle, kayıtlı /resolve, lifecycle, secondary label, criteria ağaç, JSON toggle, scope izolasyon, tema-duyarlılık, app-card, font ayrımı.
- ✅ **Build+test (CT izole, Release, GERÇEK build):** Diten.Web.Tests **137/0**.
- ⏳ **E4 (madde 5 kök):** fleet restart + **geçerli territory node** ile canlı resolve → gerçek sayı (kayıtlı `bd667afe` geçersiz GUID olduğu için 0 dönüyordu; kod doğru).
