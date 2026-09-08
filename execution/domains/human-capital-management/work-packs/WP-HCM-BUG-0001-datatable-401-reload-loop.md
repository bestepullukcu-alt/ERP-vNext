WORK PACKAGE — BUG (blocking)

WP ID:            WP-HCM-BUG-0001
Prompt ID:        P-HCM-BUG-0001
Prompt Version:   v1.0
Task Class:       debug + fix (frontend/auth, cross-origin) — BLOCKS all HR page testing
Golden-Flow Profile: A (state-affecting: auth)
Risk Class:       HIGH (auth path; shared dt-defaults across all pages)
State:            READY
Target Agent / Entry Point: debugger  (paste-block leads with @module-pack-author per standing pref)

Repository: /Users/cihan/Desktop/ERP-vNext · Branch: hr-future · HEAD c2e54336 · Worktree: /Users/cihan/Desktop/ERP-vNext
Runtime UP: frontend http://localhost:5001, gateway 5080, HCM 5059. Login: http://localhost:5001/account/login?tenantId=00000000-0000-0000-0000-000000000001 (admin@diten.com / Admin123!). USE localhost (NOT 127.0.0.1).

CT diagnosis (measured — start here, verify in browser):
- Symptom: DataTable pages (GoldenReferenceSlim + HR modules) reload continuously; console shows repeated
  "Error loading SVG: Failed to fetch" (template-customizer) and "DataTables Ajax error (HTTP 0)".
- Root: dt-defaults.js — DataTable ajax gets 401 → handleUnauthorized (line 749) → refreshTokenAndReload() (line 37)
  → window.location.reload() (line 55) → ajax 401 again → infinite loop. The aborted in-flight fetches (SVGs exist on
  disk at wwwroot/assets/img/customizer/*.svg) throw "Failed to fetch" — collateral, not the cause.
- Auth path (already correct in code): cookie name access_token matches; gateway bridges cookie→Bearer
  (gateway/Program.cs:134-137); CORS allows localhost+127.0.0.1:5001 with AllowCredentials; ajax uses
  credentials:'include'/withCredentials:true; cookie SameSite=Lax, Secure=IsHttps (sent over http localhost).
- Open question (needs BROWSER Network tab): why does the authenticated DataTable request to :5080 still return 401?
  Candidates: access_token cookie not actually sent to :5080; token expired (very short TTL → the repeated
  /account/refresh 200s in frontend log); audience/issuer/tenant claim mismatch for tenant-context token; or the
  specific endpoint (e.g., /api/golden-reference-slim) requires a permission the user lacks and returns 401 not 403.

Objective:
1. Reproduce (login, open a looping page) and read the browser Network tab: capture the exact failing request to
   :5080 — its Cookie/Authorization headers and the gateway's 401 body/reason. Pin the ONE root cause with evidence.
2. Fix that root cause (examples by cause — pick the one the evidence shows, do not shotgun):
   - cookie not reaching gateway → fix cookie scope/SameSite/attributes or the ajax credentials;
   - token expired too fast → fix access-token TTL / the refresh not actually rotating the access token;
   - endpoint returns 401 for missing permission → correct status to 403 AND/OR seed/grant the needed permission;
   - tenant/audience mismatch → align token validation.
3. Harden the loop guard in dt-defaults.js: a repeated/consecutive 401 must NOT reload infinitely — after one failed
   refresh+retry, redirect to login (redirectToLogin already exists) instead of reloading again. No infinite reload.

Scope:
- Allowed (WRITE, per root cause): frontend/Diten.Web/wwwroot/assets/js/dt-defaults.js (loop guard) + the specific
  auth/cookie/gateway file the evidence implicates. Minimal, targeted.
- Protected: unrelated modules, module packs, ocelot routes unrelated to auth, business logic.

Acceptance Criteria (measurable, E3):
- Browser Network evidence of the 401 cause captured and stated.
- After fix: opening GoldenReferenceSlim and an HR page (e.g., Competency & Skills) logged-in → DataTable loads
  (list/empty) with NO reload loop, NO "HTTP 0", NO repeated /account/refresh storm.
- dt-defaults.js no longer reloads infinitely on persistent 401 (redirects to login after one failed retry).
- Fix-absent→RED reasoning stated (K3): the loop reproduced before, gone after.

Failure Protocol: if the 401 is a missing permission for a non-HCM endpoint (e.g., devenablement golden-reference),
report it separately — do not widen scope. Do not disable auth. Do not touch backend business logic.

Output: §22 report + Network evidence + changed files + before/after runtime. PASS ≠ CT ACCEPTED (K13).

Next (after this is accepted): WP-HCM-FE-0001 (golden-compact CRUD, Competency reference) → roll out + per-module
runtime test across all 12 HR modules.

---

## Agent Prompt (paste-ready)

@module-pack-author
WP: WP-HCM-BUG-0001 · Prompt P-HCM-BUG-0001 v1.0

Repository: /Users/cihan/Desktop/ERP-vNext · Branch: hr-future · Expected HEAD: c2e54336 · Worktree: /Users/cihan/Desktop/ERP-vNext
Runtime UP: frontend http://localhost:5001 · gateway 5080 · HCM 5059. Login: http://localhost:5001/account/login?tenantId=00000000-0000-0000-0000-000000000001 (admin@diten.com / Admin123!). localhost KULLAN, 127.0.0.1 DEĞİL.

CT teşhisi (ölçülmüş — buradan başla, tarayıcıda doğrula):
- Belirti: DataTable sayfaları (GoldenReferenceSlim + HR modülleri) sürekli reload oluyor; konsolda tekrar eden
  "Error loading SVG: Failed to fetch" ve "DataTables Ajax error (HTTP 0)".
- Kök: dt-defaults.js — DataTable ajax 401 alınca handleUnauthorized (satır 749) → refreshTokenAndReload() (satır 37)
  → window.location.reload() (satır 55) → ajax yine 401 → sonsuz döngü. SVG'ler diskte VAR
  (wwwroot/assets/img/customizer/*.svg); "Failed to fetch" reload'ın iptal ettiği fetch'lerden — yan hasar.
- Auth kodu zaten doğru görünüyor: cookie adı access_token eşleşiyor; gateway cookie→Bearer köprüsü var
  (gateway/Program.cs:134-137); CORS localhost+127.0.0.1:5001 + AllowCredentials; ajax credentials:'include';
  cookie SameSite=Lax, Secure=IsHttps (http localhost'ta gönderilir).
- AÇIK SORU (tarayıcı Network sekmesi şart): authenticated DataTable isteği :5080'e giderken neden hâlâ 401?
  Adaylar: access_token cookie :5080'e gitmiyor; token çok kısa ömürlü/expired (frontend log'da tekrarlı
  /account/refresh 200); tenant-context token audience/issuer/tenant claim uyuşmazlığı; ya da endpoint
  (ör. /api/golden-reference-slim) eksik izinde 403 yerine 401 dönüyor.

NE:      (1) Reprodüksiyon (login → döngüdeki sayfayı aç) + tarayıcı Network sekmesinde :5080'e giden BAŞARISIZ isteğin
         Cookie/Authorization header'larını ve gateway'in 401 sebebini yakala. Tek kök nedeni KANITLA.
         (2) O kök nedeni düzelt (kanıtın gösterdiği TEK sebebe göre; rastgele deneme yok):
             cookie ulaşmıyorsa cookie/SameSite/credentials; token hızlı expire ise access-token TTL / refresh rotasyonu;
             endpoint izin eksikliğinde 401 dönüyorsa status'u 403 yap ve/veya gerekli izni seed/grant; audience/tenant
             uyuşmazlığı ise token doğrulamasını hizala.
         (3) dt-defaults.js döngü korumasını sertleştir: tekrarlı 401'de SONSUZ reload YOK — bir başarısız refresh+retry
             sonrası redirectToLogin() ile login'e yönlendir (fonksiyon mevcut).
NEDEN:   Bu döngü tüm HR sayfalarını test edilemez yapıyor; her şeyi blokluyor.
NASIL:   Minimum, hedefli değişiklik: dt-defaults.js (döngü koruması) + kanıtın işaret ettiği tek auth/cookie/gateway
         dosyası. Auth'u DEVRE DIŞI BIRAKMA. Backend iş mantığına dokunma.
YAPMA:   İlgisiz modüller/pack/ocelot rotaları/iş mantığı. Non-HCM endpoint izin eksikliği ise ayrı raporla, scope genişletme.
DOĞRULA: 401 sebebinin Network kanıtı; fix sonrası GoldenReferenceSlim + bir HR sayfası (Competency) login'liyken DataTable
         yükleniyor (liste/boş), reload döngüsü YOK, "HTTP 0" YOK, /account/refresh fırtınası YOK; dt-defaults kalıcı
         401'de sonsuz reload yapmıyor. fix-absent→RED mantığını yaz (K3).

Durma koşulları: non-HCM izin eksikliği · auth'u kapatma ihtiyacı · scope dışı. Dur ve raporla.

Rapor: §22 report + Network kanıtı + değişen dosyalar + öncesi/sonrası runtime.
Senin PASS'in kapanış değildir (K13); CT bağımsız doğrular. Kabul sonrası: golden-compact CRUD (WP-HCM-FE-0001) +
12 modülün tek tek runtime testi.
