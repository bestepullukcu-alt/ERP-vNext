---
id: MOD-0126
name: Payments & Approvals
domain: finance-treasury
service: Diten.FinanceTreasuryService
service_port: 5071
shell: tenant
golden_reference: compact
data_mode: server
entity_base: EntityBase
status: draft
owner: depo sahibi (K1) / Treasury Intake Control Tower
branch: feature/fin/mod-0126-payments-approvals
started: 2026-10-24
target: 2026-12-15
form_field_count: 16
slice: "TRM-S1 — ödeme partisi + doğrulama + MOD-0023 onay akışı + SoD + onay belgesi/kanıt + serbest bırakma (banka dağıtımı KAPALI)"
---

# MOD-0126 — Payments & Approvals

> **Durum: `draft` (planlama belgesi).** Bu paket kod, servis iskeleti, port kaydı, gateway rotası ya da kaynak
> projeden kod alma izni vermez. Kod ancak paket `approved` / `ready-for-dev` olduktan, bağımsız inceleme (CT-APR-02)
> ve sürüme bağlı onay satırı (CT-APR-01) kaydedildikten sonra yazılır; MOD-0126 için kod alma izni **verildi** (KARAR
> D27/5; D1 yöntemi). Engeller: "Ready-for-dev Checklist" (işaretsiz maddeler paket kapısı; "— DL-n için" maddeleri o
> dilimin önkoşulu — D51/1) ve "Açık kararlar".
>
> **Revizyon (2026-10-28, KARAR D78 — R4 açık, tier-2 olmayan partide muafiyet; CT-APR-02 Codex fark incelemesi F-1):**
> `status: draft` değişmedi. Kaynak: `docs/records/reviews/MOD-0126/2026-10-28-58976767.md` F-1 ve
> `docs/records/decisions/2026-10/treasury-intake-owner-decisions-05.md` D78 (2026-10-28 ölçümünde karar dosyasında yazılı,
> commit'lenmemiş; main'de değil). **D78:** sabitlenmiş `R4Enabled = true` ama `IsTier2 = false` partide (R4 bu partiye
> uygulanmaz) `ApprovePaymentBatchWithSodWaiverCommand` 403 `PAYMENT_SOD_WAIVER_FORBIDDEN`; `Waived` kaydı oluşmaz, karar,
> niyet, makbuz yazılmaz, MOD-0023 çağrılmaz (D77/1 ile aynı mantık; D77'nin kapsamı genişletilmedi, ayrı karar). R4 satırı,
> Failure Path; **AC-43 genişletildi** — (j) ve sabotajı, DL-2 Bitiş'e eklendi. Yeni AC yok; yeni hata kodu yok; AC numaraları
> ve dilim tablosu değişmedi. Bu revizyon sha256'yı değiştirir; MOD-0119 / 0120 / 0124 yeniden sabitlendi.
>
> **Revizyon (2026-10-28, KARAR D77 — R4 kapalı partide muafiyet ve R11 hata kodu):** `status: draft` değişmedi. Kaynak:
> `docs/records/decisions/2026-10/treasury-intake-owner-decisions-05.md` D77 (2026-10-28 ölçümünde karar dosyasında yazılı,
> commit'lenmemiş; main'de değil). **D77/1:** sabitlenmiş `R4Enabled = false` partide `ApprovePaymentBatchWithSodWaiverCommand`
> 403 `PAYMENT_SOD_WAIVER_FORBIDDEN`; karar, niyet, makbuz yazılmaz, MOD-0023 çağrılmaz (R4 satırı, Failure Path; **AC-43
> genişletildi** — (i) ve sabotajı, DL-2 Bitiş'e eklendi). **D77/2:** Release'de R11 tier-2 şartı tutmazsa 403
> `PAYMENT_TIER2_RELEASE_FORBIDDEN`; parti `Approved` kalır, `PaymentDispatchAttempt` yazılmaz (R11 satırı, Failure Path, hata
> sözlüğü "Yeni"; **AC-19 genişletildi** — "Release reddedilir" bu koda çevrildi, R11 tier-2 reddi senaryosu ve sabotajı — DL-3).
> **Yeni hata kodu:** `PAYMENT_TIER2_RELEASE_FORBIDDEN` (403). Yeni AC yok; AC numaraları ve dilim tablosu değişmedi. Bu revizyon
> sha256'yı değiştirir; MOD-0119 / 0120 / 0124 yeniden sabitlendi.
>
> **Revizyon (2026-10-28, CT-APR-02 Codex b91d61325 KOŞULLU bulguları):** `status: draft` değişmedi. Kaynak:
> `docs/records/reviews/MOD-0126/2026-10-28-674c73e9.md`. **F-1 (ORTA) — R4 kapalıyken aday seçimi ve Release:** R4'ün
> uygulanma koşulu `IsTier2` ∧ partiye sabitlenen `R4Enabled = true` olarak R4 satırına yazıldı ve aday tabanı ("Aday tabanı"),
> Approve kontrolü (R4, Failure Path, AC-6), muafiyet adayı kuralı ve Release R11 aynı koşula hizalandı: sabitlenmiş
> `R4Enabled = false` partide `IsTier2` sabitlenir ama aday tabanı normal partininkidir, `tier2-approve` sorgulanmaz, muafiyet
> adayı eklenmez, Approve `tier2-approve` istemez ve R11'in tier-2 şartı aranmaz (KARAR D56/4, D57/4 ile uyumlu — R4 ayarlanabilir;
> R1–R3 değişmedi). **AC-44 (d) genişletildi** (R4 kapalı, yalnız `approve` + iş akışı izni olan, R1–R3 dışı adayla Submit →
> Approve; R4 açık eş parti; koşulun kaldırılma / ters çevrilme sabotajı — DL-2), **AC-19 genişletildi** (aynı senaryonun
> Release kısmı ve sabotajı — DL-3), **AC-6 netleştirildi** (R4 uygulanan parti). **F-2 (DÜŞÜK):** AC-41'in kapanış cümlesi
> "DL-1'de kısmi; (d) Approve DL-2, Release DL-3 — tam kapanış DL-3" olarak dilim bitiş listeleriyle hizalandı. Yeni AC yok; yeni
> hata kodu yok; AC numaraları ve dilim tablosu değişmedi. Bu revizyon sha256'yı değiştirir; MOD-0119 / 0120 / 0124 yeniden
> sabitlendi.
>
> **Revizyon (2026-10-28, KARAR D76/3 — kullanıcının başlattığı Reconcile):** `status: draft` değişmedi. Kaynak: `docs/records/decisions/2026-10/treasury-intake-owner-decisions-05.md` D76 (2026-10-28 ölçümünde karar
> dosyasında yazılı, commit'lenmemiş; main'de değil; D75 yerel commit `da2aba808`, main'de değil).
> **D76/3:** kullanıcının başlattığı Reconcile da yeniden denemedir — aday listesi Auth'tan yeniden hesaplanmaz,
> niyetteki ilk liste kullanılır; rol değişikliği gerekiyorsa yeni gönderim yapılır. Metin D75 revizyonunda zaten böyleydi;
> yalnız karar atfı eklendi (Runtime Constraints 3a (ii), Validation Rules "Aday tabanı", AC-34 (d) eki başlığı). Davranış değişmedi; AC genişletilmedi; yeni AC yok;
> yeni hata kodu yok; AC numaraları ve dilim tablosu değişmedi.
>
> **Revizyon (2026-10-28, KARAR D75 — iç uç kimliği atıfları ve yeniden denemede ilk aday listesi):** `status: draft`
> değişmedi. Kaynak: `docs/records/decisions/2026-10/treasury-intake-owner-decisions-05.md` D74/1 (yerel commit `5d2655c72`,
> main'de değil) ve D75 (2026-10-28 ölçümünde karar dosyasında yazılı, commit'lenmemiş; main'de değil). (1) **D74/1 hizalaması:**
> Auth aday sorgusu (MOD-0018 Treasury Auth eki DL-6) ve muafiyet atama iç ucu (DL-5) Auth'un ortak sır desenini
> (`X-Internal-Api-Key`) kullanır; kiracı çağrıyı başlatan kullanıcının iletilen token'ından alınır; **servis kimliği
> kullanılmaz** — eski "servis kimliğiyle" / "servis kimliği FU K-A1" atıfları düzeltildi (o paketin K-A1'inin kimlik kısmı
> KAPANDI — KARAR D74/1; uç adı ve sözleşme dosyası CT önerisi olarak açık). (2) **D75/2 (b):** aday listesi yalnız kullanıcının
> başlattığı gönderimde (Submit) hesaplanır; Reconcile'ın aynı anahtarla yeniden başlatması niyetteki ilk listeyi
> (`PaymentWorkflowStartIntent.CandidatePrincipalIds`) aynen gönderir, Auth aday sorgusunu yeniden çağırmaz. Ölçüm: önceki metin
> Runtime Constraints 3a (ii) ve Validation Rules "Aday tabanı"nda Reconcile'da çözümleyiciyi **yeniden** çalıştırıyordu —
> D75/2 (b)'ye aykırıydı, düzeltildi. (3) D75/1 (Auth tarafı: atayan izin + Muafiyet yetkilisi rol üyeliği) "Muafiyet kuralları"
> "Onaylayan" satırına işlendi; servis davranışı değişmedi. Değişen yerler: Repo Scope (ApproverCandidates bağdaştırıcısı),
> Dependencies (MOD-0018 RBAC ve aday tabanı satırları),
> Runtime Constraints 3a (ii), Validation Rules "Aday tabanı", Authorization Convention "Muafiyet kuralları", D66 revizyon notu
> (köşeli parantez); **AC-34 genişletildi**. Yeni hata kodu yok; mevcut AC numaraları değişmedi; yeni AC yok. D75/4 ölçümü:
> bu paket v1'de BANK-ACCOUNT-LOOKUP'ı tüketmez (Dependencies MOD-0289 satırı: yalnız boş olmayan opak Guid) — kural yalnız
> Follow-up maddesine yazıldı; AC ve hata kodu eklenmedi.
>
> **Revizyon (2026-10-28, KARAR D74/3 — muafiyet atayanı):** `status: draft` değişmedi. Kaynak:
> `docs/records/decisions/2026-10/treasury-intake-owner-decisions-05.md` D74 (2026-10-28 ölçümünde karar dosyasında yazılı,
> commit'lenmemiş; main'de değil). `waive-sod` atamasını onaylayan artık **Muafiyet yetkilisi** rolüdür — MOD-0018 Treasury Auth
> ekinin 27. rolü (`PaymentSodWaiverAuthority`, ad önerisi), yalnız `auth.payment-sod-waivers.assign` / `.read`, açık atamayla;
> D49/4'ün "Hazine yöneticisi ya da K1" kısmı kalktı. Ödeme sürecinde rolü olan kişi kendine muafiyet veremez (Auth'ta kendine
> atama 403). Servis davranışı değişmedi: servis rol adını denetlemez; atamayı onaylayanın kimliğini okur ve R1–R3 kişileri ve
> çağıranla karşılaştırır (AC-43 (c), (d)). Değişen yer: Authorization Convention "Muafiyet kuralları" tablosunun "Onaylayan"
> satırı (CT04b revizyon notuna D74/3 işareti). Mevcut AC numaraları değişmedi; yeni AC yok.
>
> **Revizyon (2026-10-28, KARAR D71 — elle atamada iş akışı izni kesişimi):** `status: draft` değişmedi. Kaynak:
> `docs/records/decisions/2026-10/treasury-intake-owner-decisions-05.md` D71 (2026-10-28 ölçümünde main'de değil — yerel
> dalda, main'de değil). Onaylayıcı aday tabanı iş akışı izniyle de kesişir: normal partide
> `approve ∩ platform.workflow.tasks.approve`, `IsTier2` partide `approve ∩ tier2-approve ∩ platform.workflow.tasks.approve`;
> servis DL-6 aday sorgusunu her anahtar için ayrı çağırır (tier-2 kesişimiyle aynı desen) — karar veremeyecek kişi aday olmaz.
> Çağrılardan biri okunamazsa gönderme kapalı-başarısızdır (niyet yazılmaz, parti değişmez, MOD-0023 çağrılmaz); kesişim (R1–R4,
> şirket kapsamı ve muafiyet adayı kuralından sonra) boşsa mevcut 422 `PAYMENT_WORKFLOW_RESOLVER_NO_ELIGIBLE_APPROVER` dalı (D68).
> MOD-0018 eki K-A4b **KAPANDI — KARAR D71**; D70/1 (iş akışı izni rol şablonuyla gelir) değişmedi; MOD-0018 sahibinin teyidi Auth
> ekiyle birlikte. Muafiyet adayı biçimi (`waive-sod` + parti, R4 / D13) bu revizyonda değişmedi. Validation Rules "Aday tabanı",
> Runtime Constraints 3a, Dependencies kapanış tablosu (aday tabanı satırı), Failure Path, Authorization Convention, Test
> Expectations, Ready-for-dev Checklist aday tabanı maddesi; **AC-11 genişletildi**. Yeni hata kodu yok; mevcut AC numaraları ve
> dilim tablosu değişmedi. Bu revizyon sha256'yı değiştirir; MOD-0119 / 0120 / 0124 yeniden sabitlendi.
>
> **Revizyon (2026-10-28, KARAR D70 + D67 atıfları):** `status: draft` değişmedi. Kaynak:
> `docs/records/decisions/2026-10/treasury-intake-owner-decisions-05.md` D67, D70 (D67 PR #281 ile main'de; D70 2026-10-28
> ölçümünde main'de değil — yerel dal `chore/ct/treasury-intake-ct-05`, main'de değil). (1) **KARAR D70/1:** onaylayıcı rollere
> `platform.workflow.tasks.approve` / `.reject` rol şablonuyla gelir (MOD-0018 eki K-4 (b)); iş akışı izni aday şartı değildir —
> MOD-0018 eki K-A4 kapandı (Validation Rules "Aday tabanı", Authorization Convention); rol şablonu yoluyla tutarlı, elle atamada
> açık nokta — K-A4b (onay anahtarı rol dışı elle verilmiş, iş akışı izni olmayan kişi; MOD-0018 eki Açık kararlar) — **KARAR D71 ile kapandı** (yukarıdaki D71 notu). (2) **KARAR D70/3:** Auth paket metni
> kapısı (D53/1) = metin main'de + MOD-0018 sahibinin teyidi (Ready-for-dev Checklist ENGELLER; açık) [D80 revizyonu: şartlı teyit — Y-19 / KARAR D80/2, DL-1'i bekletmez]. (3) **D67 atıfları:**
> `ApproverCandidates` servis ortak aday kaynağının tek yazarı bu paketin DL-2'si — **D67/3** ile teyit edildi (Repo Scope;
> "sahip teyidi açık" kalktı); aday sorgusu FU DL-6'dır, kodu DL-2 kabul kapısı (**D67/1**); muafiyet adayı DL-6'nın `waive-sod` +
> parti biçimi (**D67/2**). (4) Ölçümle: D64 (PR #276) ve D66 (PR #281) main'de — DL-2 dilim kapısı maddelerine yazıldı. D70/2
> bu pakete yeni bir şey getirmez (D68 zaten uygulanmıştı). Yeni hata kodu yok; mevcut AC numaraları ve dilim tablosu değişmedi.
> Bu revizyon sha256'yı değiştirir; MOD-0119 / 0120 / 0124 yeniden sabitlendi.
>
> **Revizyon (2026-10-28, sahip denetimi 61466e504 KOŞULLU — tier-2 aday kesişimi):** `status: draft` değişmedi. Bulgu (P2):
> tier-2 partide aday sorgusu yalnız `finance-treasury.payment-batches.tier2-approve`'u soruyordu; Approve ucu temel
> `finance-treasury.payment-batches.approve`'u da istediği için yalnız tier-2 izni olan kişi aday olup kararı uygulayamıyor,
> görev takılıyordu. Düzeltme: tier-2 aday tabanı **`approve ∩ tier2-approve`** — servis DL-6 aday sorgusunu iki anahtar için
> ayrı ayrı çağırır, kesişimi alır, R1–R4'ü ve şirket kapsamını kesişime uygular; iki çağrıdan biri okunamazsa gönderme
> kapalı-başarısızdır. Normal parti (yalnız `approve`) ve muafiyet adayı biçimi (`waive-sod` + parti) değişmedi; bağımlı
> `platform.workflow.tasks.approve` şartı bu kesişime katılmaz (MOD-0018 eki K-A4 — **D70/1 ile kapandı**, iş akışı izni rol şablonuyla gelir; **KARAR D71** ile iş akışı izni ayrı bir kesişim olarak eklendi — yukarıdaki D71 notu). Validation Rules R4 + "Aday tabanı",
> Runtime Constraints 3a (Reconcile yeniden başlatma), Failure Path, Authorization Convention, Test Expectations, O-10;
> **AC-11 genişletildi**. Yeni hata kodu yok; mevcut AC numaraları ve dilim tablosu değişmedi. Bu revizyon sha256'yı
> değiştirir; MOD-0119 / 0120 / 0124 yeniden sabitlendi.
>
> **Revizyon (2026-10-28, ready-for-dev 1. tur eki: D66):** `status: draft` değişmedi. Kaynak:
> `docs/records/decisions/2026-10/treasury-intake-owner-decisions-05.md` D66 (yazıldığı anda main'de değildi; ölçüm 2026-10-28: PR #281 ile main'de). (1) **KARAR
> D66/1 — aday tabanı:** onaylayıcı adayları Auth aday sorgusundan gelir — MOD-0018 Treasury Auth ekinin **DL-6**'sı
> (`execution/domains/platform-shared-services/module-packs/MOD-0018-FU27-treasury-permission-onboarding.md` Bölüm 8.6; "bu izne sahip kullanıcılar", iç uç, servis kimliğiyle [D75 revizyonu: kimlik KARAR D74/1 — `X-Internal-Api-Key` ortak sırrı + iletilen kullanıcı token'ı; servis kimliği kullanılmaz]); servis R1–R4'ü uygular ve sonucu
> `CandidatePrincipalIds` olarak gönderir; ayrı dizin istemcisi taşınmaz. Muafiyet aday listesi (D56/3) aynı sorgunun
> `waive-sod` + parti biçimidir (tek aday sorgusu). O-10 ve 1. turdaki "O-10 gerilimi" notu **kapandı**. Owned Objects portları,
> hata sözlüğü, Repo Scope (servis ortak aday kaynağı — D57/5 deseni, CT önerisi), Dependencies MOD-0018 satırı, kapanış
> tablosuna yeni satır, Validation Rules R4 + "Aday tabanı", Failure Path, Authorization Convention, Test Expectations,
> Ready-for-dev Checklist, O-10, DL-2 Yollar / Önkoşul; **AC-11 ve AC-42 genişletildi**. (2) **KARAR D66/2:** "son adım değil"
> yanıtı v1'de beklenmez; gelirse parti `PendingApproval`'da kalır, onaylanmış sayılmaz — 1. turdaki D62/2 metni (durum tablosu,
> Runtime Constraints 4, AC-46) bu karara bağlandı. (3) **KARAR D66/3 (D56/3'teki "DL-5 kodu main'de = DL-2 kabul kapısı"
> kuralının yerine):** DL-2 muafiyeti beklemez — DL-5 kodu main'e girene kadar muafiyet yolu 403 ile kapalıdır ve bunu
> kanıtlayan test DL-2 kabulü için yeterlidir; muafiyet DL-5 gelince açılır ve **ayrıca kabul edilir** (muafiyet açılış
> kabulü; dilim değildir). Dependencies / kapanış tablosu DL-5 satırları, Authorization Convention, Ready-for-dev Checklist,
> DL-2 Önkoşul / Bitiş; **AC-43 genişletildi** ((h) kapalı yol kanıtı). D66/4 bu pakete dokunmaz (MOD-0119). Mevcut AC
> numaraları ve dilim tablosu değişmedi. Bu revizyon sha256'yı değiştirir; MOD-0119 / 0120 / 0124 yeniden sabitlendi.
>
> **Revizyon (2026-10-28, ready-for-dev 1. tur: Y-12, D62–D65):** Ali / Beste'yi beklemeyen maddeler; `status: draft` değişmedi.
> Kaynak: `docs/records/decisions/2026-10/treasury-intake-owner-decisions-05.md` (D62–D65) ve
> `docs/records/audits/2026-10/treasury-intake-handover-review-01/SOURCE-ANSWERS-2026-10-27.md` (Y-12, Y-15, Y-16, Y-17).
> (1) **Y-12 — MOD-0023 şablon kodu (O-8):** `FT-PAYMENT-BATCH-APPROVAL` (MOD-0023 sahip kararları W-1, W-6 —
> `docs/records/decisions/2026-10/mod0023-workflow-template-owner-decisions-01.md`, dal `chore/ct/pss-wf-ct-01` @ `c927047f1`;
> ölçüm 2026-10-28: kayıt origin/main @ `28c68e7a6`'da (#277, içerik dal sürümüyle aynı), bu dalda yok). Tek aşama, tek onay
> adımı, atama yok; onaylayıcıları başlatma isteğinin `StartWorkflowInstanceRequest.CandidatePrincipalIds` alanı belirler (bu
> paketin R1–R4 aday çözümleyicisi; eşik üstü partide `tier2-approve`); boş liste MOD-0023'te 400
> `WORKFLOW_ASSIGNMENT_CANDIDATES_REQUIRED`. 15 sn HTTP zaman aşımı tüketici ayarıdır. Dependencies MOD-0023 satırı, kapanış
> tablosu, Runtime Constraints 4–5, Repo Scope bağımlı değişiklikler, O-8 ("kod geldi (Y-12)"), Ready-for-dev Checklist şablon
> maddesi (şablonu kuran MOD-0023 kodu main'de değil, WP numarası ve tarih gelmedi — madde **açık**); **AC-11 genişletildi**
> (şablon kodu + aday listesi). (2) **D62/2 — `PartiallyApproved` v1'de kapalı:** durum (kod 9 ve ad) enum'da saklanır, v1'de
> giriş yolu yoktur; MOD-0023'ün "son adım değil" yanıtında parti `PendingApproval`'da kalır (MOD-0119 / MOD-0124 deseni; **KARAR D66/2** ile teyitli) —
> durum tablosu, Runtime Constraints 4; **yeni AC-46** (DL-2). (3) **D64 (D63/3'ün yerine; Y-14 geçersiz, kullanılmadı):**
> `waive-sod` atama kaydı, yazma ucu, "bu parti için geçerli atamalar" sorgusu ve denetimi MOD-0018 Treasury Auth ekinin
> **DL-5**'idir (Treasury yazar, MOD-0018 sahibi teyit eder); DL-5 main'e girene kadar muafiyet yolu 403
> `PAYMENT_SOD_WAIVER_FORBIDDEN` ile kapalıdır (D54 değişmedi) — Dependencies MOD-0018 satırı, kapanış tablosuna yeni satır,
> Authorization Convention, DL-2 dilim kapısı maddeleri ve DL-2 Önkoşul. (4) **D53 / D56 Y-16 ile aynen geçerli:** Auth paket
> metni `execution/domains/platform-shared-services/module-packs/MOD-0018-FU27-treasury-permission-onboarding.md`'dir (FU
> numarası açık, K-1 [D80 revizyonu: FU27, eski geçici ad FU-TBD — K-1 kapandı, D80/1]; Treasury yazar, MOD-0018 sahibi teyit eder — Y-15, Y-16); Auth / SEC-002 satırları ve "Ali'nin iş
> paketi" ifadeleri buna çekildi; paket kapısı (D53/1) ve DL-1 kabul kapısı (D53/2) değişmedi. (5) **O-10 gerilimi (karar
> değil):** şablon atamasız olduğundan O-10'un "şablonun atama kuralı" aday tabanı kalmadı — O-10'a açık not, DL-2 Önkoşul'a
> O-10 (**D66/1 ile kapandı** — yukarıdaki D66 notu). D65 bu pakete dokunmaz
> (MOD-0057, MOD-0289). Mevcut AC numaraları ve dilim tablosu değişmedi. Bu revizyon sha256'yı değiştirir; MOD-0119 / 0120 /
> 0124 yeniden sabitlendi.
>
> **Revizyon (2026-10-28, D58/D59):** **KARAR D59/1** (kaynak Y-8, Beste) — RCS-001 / BL-565 servisi paket kapısından
> **DL-1 kabul kapısına** taşındı: ENGELLER metni, Ready-for-dev Checklist maddesi ("— DL-1 için (kabul kapısı)"), Dependencies
> ve kapanış tablosu RCS-001 satırları (v0 çağrı sözleşmesi `POST /api/internal/record-codes/allocate`, 200 / 422 / 409 / 404 /
> 503, 6 Kasım'da kesinleşir), Runtime Constraints ve Failure Path (üretimde port bağlı değilse Create 503
> `PAYMENT_RECORD_CODE_UNAVAILABLE`, kodsuz kayıt yok, kendi sayaç / yedek kod yok), Repo Scope (servis ortak port
> `Application/Common/RecordCodes/**` + bağdaştırıcı `Infrastructure/RecordCodes/**`, tek yazarı bu paketin DL-1'i) ve DL-1
> Önkoşul / Bitiş. Port adı uzlaştırıldı: `IRecordCodePort` → **`IRecordCodeAllocator`** (`AllocateAsync(legalEntityId,
> recordType, idempotencyKey) → code`; dört Treasury paketinde aynı). Tür öneki `PAY` değişmedi. **Yeni AC-45** (sahte
> allocator + sabotaj, DL-1). D58 bu pakete dokunmaz (MOD-0289). Dilim tablosu değişmedi. Bu revizyon MOD-0126 sha256'sını
> değiştirir; MOD-0119 / 0120 / 0124 yeniden sabitlendi. `status: draft`.
>
> **Revizyon (2026-10-28, D57):** (1) **KARAR D57/4** (D56/4 ayrıntıları): SoD ayarı izinleri
> `finance-treasury.payment-sod-settings.read` (T1) / `.update` (T3) — Hazine yöneticisi (D47 rol matrisine eklendi); ayar ekranı
> MOD-0126 ekran diliminde (DL-4: Repo Scope, Layout & Shell, Frontend File Contract "SoD ayar ekranı", Page Help, DL-4 Yollar /
> Önkoşul / Bitiş); R1–R3 kapatılamaz — `PaymentSodSettings` yalnız eşik ve `R4Enabled` taşır, istekte R1–R3 anahtarı 400; kayıt
> yoksa R1–R4 açık ve varsayılan eşik (D11/1: her onay tier-2), okuma ucu ve ekran varsayılanı gösterir. Owned Objects, Entity
> Fields, Runtime Constraints, Validation Rules, Failure Path, Authorization Convention (iki satır + Tier-3 listesi), Audited
> Events, AC-44 ((a)–(e) DL-2, (f) ekran DL-4 — AC-44 DL-4'te kapanır), Ready-for-dev Checklist (anahtar kararı kapandı; Auth
> paket metni maddesi açık), O-4 (kalan yok). (2) **KARAR D57/5:** MOD-0023 bağdaştırıcısı servis ortak yapı taşı, tek yazarı
> DL-2; MOD-0119 DL-2, MOD-0120 DL-3, MOD-0124 DL-3 onu bekler — Repo Scope ve DL-2 Yollar'da adıyla; tüketiciler
> `Infrastructure/Workflow/**`'a yazmaz. Yeni AC yok; AC numaraları ve dilim tablosu değişmedi. `status: draft`.
>
> **Revizyon (2026-10-28, D56 + DL-2 hazırlık bulguları):** (1) **KARAR D56/3:** `waive-sod` Auth atama alanları (hedef parti,
> başlangıç / bitiş, onaylayan, gerekçe kodu) "— DL-2 için" dilim kapısına girdi (Auth paket metni tanımlar; kodu DL-2
> kabulünde); muafiyet adayı kaynağı Auth'un "bu partiye geçerli `waive-sod` ataması olanlar" sorgusu (Ali, MOD-0018) —
> R4, AC-42 (üç test + sabotaj), AC-43, Authorization Convention, Dependencies MOD-0018 satırı, Failure Path, O-10; okuma portu
> `IPaymentSodWaiverAssignmentSource` ve `Infrastructure/SodWaivers/**` Repo Scope'a ve DL-2 Yollar'a yazıldı (DL-2 kaydı §5
> bulgu 1, 3). (2) **KARAR D56/4:** SoD ayarı FinanceTreasuryService'te kiracı başına tek kayıt (`PaymentSodSettings`: tek eşik +
> R1–R4 açık / kapalı), denetlenir, DL-2'de yazılır — Follow-up'tan çıktı; Owned Objects, Entity Fields (+ `ux_payment_sod_settings_tenant`),
> hata sözlüğü (`PAYMENT_SOD_SETTINGS_VERSION_CONFLICT`), Runtime Constraints, Validation Rules, Failure Path, Audited Events
> (`finance-treasury.payment-sod-settings.updated`), Authorization Convention ("SoD ayarı izinleri" — anahtar adları karar
> bekliyor), Ready-for-dev Checklist, O-4, DL-2 Yollar / Önkoşul / Bitiş; **yeni AC-44** (DL-2). (3) DL-2 kaydı §5 mekanik
> bulgular: MOD-0023 bağdaştırıcısı servis ortak yapı taşı olarak yazıldı — tek sahibi bu paketin DL-2'si, MOD-0119 / 0120 / 0124
> tüketir (bulgu 4); DL-2 Bitiş'e "Validation Rules R11 hariç" notu ve `payment-batch.openapi.yaml` DL-2 sürümü 1.1.0 (bulgu 6).
> AC-W1…W17 listesi değişmedi (diğer üç paket buna hizalandı). Mevcut AC numaraları ve dilimler değişmedi. `status: draft`.
>
> **Revizyon (2026-10-27, D54 — Codex F-1):** `waive-sod` açık atamasının hedef ödeme partisini taşıması zorunlu oldu (KARAR
> D54, `treasury-intake-owner-decisions-05.md`; bulgu `docs/records/reviews/MOD-0126/2026-10-27-699b0708.md` F-1). Atamada hedef
> parti kimliği yoksa ya da okunamıyorsa, ya da rotadaki partiyle eşleşmiyorsa muafiyetli onay 403 `PAYMENT_SOD_WAIVER_FORBIDDEN`
> ile reddedilir (kapalı-başarısız; yazım ve MOD-0023 çağrısı yok). "Hedef parti yoksa yalnız süreç denetimi" yolu kaldırıldı.
> Authorization Convention (muafiyet kuralları ve Auth alanları), Validation Rules, Failure Path, Dependencies (MOD-0018 satırı),
> Audited Events ve AC-43 buna çekildi; AC-43'e sabotaj dalı eklendi (aynı atama A sonra B partisi; hedef partisiz atama). Yeni AC
> kimliği yok; dilimler değişmedi. Auth bağımlılığı: MOD-0018 ataması hedef parti kimliğini taşır (Ali). `status: draft`.
>
> **Revizyon (2026-10-27, D53):** Auth kapısı ikiye ayrıldı — paket metni paket kapısında, kayıt kodu DL-1 kabul kapısında (D53).
>
> **Revizyon (2026-10-27, D52):** metin temizliği (D52/4, anlam değişmedi). MOD-0023 §17 rev 2 onay maddeleri `[x]`
> (`approvals.tsv:16`, #243); kod / test maddeleri DL-2 kabul kapısında kalır. Dependencies sözleşme tablosunda küsurat kaynağı
> MOD-0048-FU01 §21 olarak adlandırıldı. AC numaraları ve dilimler değişmedi. `status: draft`.
>
> **Revizyon (2026-10-27, D51 denetimi):** sahip denetimi, dayanak D51 (yeni karar yok). (1) Paket kapısına işaretsiz madde eklendi:
> Auth izin / rol kaydı — MOD-0018 paketi ve kodu (Ali, Y-6; rol matrisi D47/D49/D50) — D51/1 "Auth kaydı"nı paket kapısında
> sayar; ENGELLER metni ve DL-1 Önkoşul metni buna çekildi. (2) AC-32 kısmi kapanan AC oldu (son listelendiği dilimde kapanır):
> DL-1 uçları kısmı DL-1'de, karar uçları kısmı DL-2'de, Release ve kalan okumalar kısmı DL-3'te (tam kapanış); AC metni
> değişmedi; checklist SEC-002 maddesi buna çekildi. (3) DL-3 Önkoşul'daki "O-6" atfı (MOD-0269 sözleşmesi) doğrudan atıfla
> değişti — "MOD-0269 BANK-DISPATCH 0.1.0 dondu (D27/7, TRM-06)"; O-6 yalnız `AuditCategory` / K2 içindir. AC numaraları
> değişmedi. `status: draft`.
>
> **Revizyon (2026-10-27, D51):** (1) Ready-for-dev Checklist ENGELLER ikiye ayrıldı (D51/1): **paket kapısı** (işaretsiz —
> CT-APR-02/01, DCP-011 onayı, RCS-001 / BL-565 servisi, Platform `AuditCategory` `Finance` main'de, SEC-002 tasarımı + Auth'ta
> `read-tenant-wide` tanımı ve DL-1'in kendi önkoşulları TRM-14, O-11, O-13) ve **dilim kapıları** ("— DL-n için" — O-4, MOD-0023
> şablonu ve §17 rev 2 maddeleri DL-2; MOD-0269 sözleşmesi ve MOD-0031 dedupe ölçümü DL-3). Madde düşürülmedi, yalnız
> sınıflandırıldı; "Teslim dilimleri" Önkoşul metinleri ve Runtime Constraints'teki "Ready-for-dev engeli, DL-2" ifadesi buna
> çekildi. (2) SEC-002 "kapsamı soran kod + kanıt testi" ready-for-dev maddesinden DL-1 kabul kapısına taşındı; AC-32 DL-1
> "Bitiş"te zaten vardı (D51/2); Dependencies SEC-002 satırı da buna çekildi. (3) DL-1 WP'si kendi dalında (`feature/fin/wp-0126-dl1`); `/add-module` servis iskeleti DL-1
> WP'sinin ilk adımı (D51/3); künyedeki `branch` alanı değişmedi. AC numaraları değişmedi. `status: draft`.
>
> **Revizyon (2026-10-27, AC biçimi):** module-pack-standard §7 biçimine uyum — AC-6a kimliği tam sayı olmadığı için AC-42 oldu (madde aynı yerde, DL-2 bitiş listesi güncellendi). Kapsam ve anlam değişmedi.
>
> **Revizyon (2026-10-27, D46):** rol şablonlarının içeriği sahip kararıyla verildi (D46, `treasury-intake-owner-decisions-05.md`);
> Auth'ta kayıt Ali'nin iş paketinde. DCP dosya adı `DCP-011-finance-treasury-intake.md`. `status: draft`.
>
> **Revizyon (2026-10-27, CT04a — sahip denetimi; KARAR D47/1):** hazine yöneticisi yalnız yapılandırma ve operasyonel destek
> (onay, serbest bırakma, tier-2 ve muafiyet yok); `waive-sod` hiçbir rol şablonunda değil — yalnız açık atama, süreli, gerekçe
> kodlu, bağımsız onaylı, kendi kendine muafiyet yasak (Authorization Convention); muafiyet olayına bitiş, etkilenen parti ve
> onaylayan aktör alanları eklendi (Audited Events). Rol şablonu göstergeleri D46 + D47 (rol matrisi `treasury-intake-owner-decisions-05.md`); Module Summary
> hedef kullanıcıları ve DCP checklist maddesi güncel duruma çekildi. AC numaraları değişmedi. `status: draft`.
>
> **Revizyon (2026-10-27, CT04b — KARAR D49/1, D49/4):** (1) `waive-sod` açık atamasının kuralları yazıldı — atamayı onaylayan
> muafiyetten yararlanmayan Hazine yöneticisi ya da K1 (D74/3 ile Muafiyet yetkilisi rolü oldu); süre en çok 24 saat, bitişte kendiliğinden düşer; gerekçe kodları
> `ACIL-ODEME`, `TEK-YETKILI`, `DENETIM-ISTEGI`; kapsam tek ödeme partisi; her kullanım 5 iş günü içinde K1 incelemesi; onaylayan
> yararlanamaz. Authorization Convention'da hangisinin servis çalışma anında, hangisinin Auth (MOD-0018) / süreç denetimi olduğu
> ayrıldı; Auth'un taşıması gereken atama alanları bağımlılık olarak yazıldı. Validation Rules (gerekçe kodu kapalı liste),
> Failure Path, Audited Events (atama başlangıcı / onaylayanı / gerekçe kodu, K1 incelemesi girdisi) güncellendi; AC-9'a
> listede olmayan gerekçe dalı, yeni AC-43 (DL-2). (2) Hedef kullanıcılar D49/1 rol adlarıyla (Ödeme görüntüleyici; Hazine
> konsolide görüntüleyici yalnız gerekçeyle). Mevcut AC numaraları değişmedi. `status: draft`.
>
> **Revizyon (2026-10-27, kaynak yanıtları Y-1…Y-4):** kaynak `docs/records/audits/2026-10/treasury-intake-handover-review-01/SOURCE-ANSWERS-2026-10-27.md` —
> (1) O-7 / O-10: D42'nin yanına MOD-0023 sahibinin (Ali) teyidi eklendi ("ikisi de uygun", Y-1); O-8 açık kalır (şablon kodu
> ve adım sayısı iş akışı tarafındaki kule açılınca gelir, Y-3); (2) Platform `AuditCategory` `Finance` değeri 18 (Y-4/7; 17 main'de
> ESignature / MOD-0022 için kullanılıyor); "değer main'de" maddesi açık; (3) SEC-002 tüketim yolu Platform sahibi yanıtı Y-4/6 ile
> yazıldı (O-19): servis içi Application portu, Platform `IDataScopeResolver` bağdaştırıcısı, fail-closed, MDM sağlayıcısı doğrudan
> kullanılmaz, `read-tenant-wide` Auth'ta yalnız açık atamalı; (4) MOD-0048 `payment-method` ve `currency-minor-units` önerisi
> başlangıç listesi olarak kabul (Y-4/3; O-11, O-13) — seed JSON ve `ConsumableSets` izin listesi Platform'da, önkoşul açık;
> (5) DCP numarası DCP-011 atandı (Y-4/2); dosya adı değişikliği ayrı adım — paketteki `UNASSIGNED-finance-treasury-intake.md`
> yolu bu revizyonda değişmez.
> Bu revizyon pack'i `ready-for-dev` yapmaz; künye `draft` kalır.
>
> **Revizyon (2026-10-26, D42):** O-7 (onay yüzeyi ve başlatma kimliği) ve O-10 (onaylayıcı adayları) için D29/1'in "MOD-0023
> sahibi teyidi açık" koşulu sahip teyidiyle kapandı — `docs/records/decisions/2026-10/treasury-intake-owner-decisions-04.md` D42;
> O-8 (şablon kodu, adım sayısı) MOD-0023 sahibinde açık. RCS-001 ortak kod servisinin (BL-565) sahibi Beste (D44). Bu revizyon
> pack'i `ready-for-dev` yapmaz.
>
> **Revizyon (2026-10-26):** paket, yazıldıktan sonra verilen kararlara ve DL-1 hazırlık kaydına
> (`execution/domains/finance-treasury/work-packs/MOD-0126-DL1-hazirlik.md`, MOD-0126-DL1-HAZIRLIK-01) hizalandı —
> (1) MOD-0048 `payment-method` kümesi için `IPaymentMethodReferencePort` + MOD-0048 bağdaştırıcısı (`Infrastructure/Currency/`)
> "Portlar", Repo Scope ve DL-1 yollarına yazıldı; MOD-0126'ya özgüdür, servis ortak yapı taşı değildir; (2) `ux_payment_batch_code`
> tekilliği (kısmi filtre yok, silinen dahil; RCS kodu DB tekilliğini kaldırmaz) AC-23'e test + sabotajla bağlandı;
> (3) denetim defteri CT kararı verildi — `treasury-intake-owner-decisions-02.md` D35 (PR #235): iz
> `finance-treasury-merkezi-iletim | b | işaret | ICentralAuditDeclaration`, borç / istisna 0; Platform `AuditCategory`
> `Finance` (D35/3) DL-1 önkoşulu; (4) onay satırları `ready-for-dev` sürüme yazılır, DCP önce numara alır (S-49, D34 — PR #235;
> D34/2, D32/8'in numarasız onayının yerini alır); (5) MOD-0023 §17 rev 2 (sha256
> `803fbaa5272373846d6c60507525cc9259220a64fab02cf8889f362136a05fb2`, PR #235), kapanış AC-W1…W17, yazan ≠ merge eden (D32/1);
> (6) D30 korunan yol yamaları (AGENTS §2/§3/§9, `ports.md` 5071, RCS-001 §3 `PAY`) 2026-10-25'te uygulandı — bekleyen gibi
> yazılmış cümleler düzeltildi. Bu revizyon pack'i `ready-for-dev` yapmaz; künye `draft` kalır.
>
> **D27 (2026-10-25, sahip) — kapı kararları verildi:** karar 0.3 → **KARAR D27/1** süreli T32 sapması: Treasury
> sözleşmeleri `docs/analysis/contracts/` altında dondurulmuş dosya olarak tutulur ve MOD-0003 gelince oraya kaydedilir;
> **bitiş: MOD-0003 çıkana kadar, en geç Treasury v1 sonu (hangisi önce gelirse)**. Karar 0.5 → **KARAR D27/2** (AR kısıtlı
> yerel MVP, sapma S-2). Karar 0.6 → **KARAR D27/3** (v1'de ERP-FIN-BUNDLE yok; her modül kendi sözleşmesini yayımlar;
> ortak paket GL gelince ayrı karar). Karar 0.7 → **KARAR D27/4** (GL / MOD-0118 kapsam dışı; fiş / yevmiye yok,
> `VoucherReference` yok). D1 kod alma → **KARAR D27/5** (MOD-0126, MOD-0119, MOD-0120, MOD-0124 için kaynak S `da4abb20`
> doğrulanıp uyarlanarak alınabilir; kod yine yalnız pack `ready-for-dev` olunca ve dilim sırasıyla yazılır). Kayıt:
> `docs/records/decisions/2026-10/treasury-intake-owner-decisions-02.md` D27 (bu revizyonla aynı commit'te). Bu revizyon pack'i `ready-for-dev` yapmaz.
>
> **BANK-DISPATCH dondu (D27/7, D27/8):** `docs/analysis/contracts/bank-dispatch.md` FROZEN 0.1.0 (`rail` alanı çıkarıldı;
> bankacılık sahibi 0.1 için K1) — dosya çalışma ağacında, bu oturumda okundu (`bank-dispatch.md:5`). Bu paketteki bütün
> MOD-0269 atıfları dondurulmuş dosyaya işaret eder; bölüm numaraları (§5, §5.1, §6) taslak rev 3 ile aynıdır (ölçüldü:
> `bank-dispatch.md:167, 181, 195`).
>
> **Bağlanan kararlar (yeniden karar verilmez):** yerleşim TREASURY-MOD0126-PLACEMENT-01
> (`docs/records/decisions/2026-10/treasury-mod0126-placement-decision-01.md`); D1 (doğrula, sonra uyarlayarak al),
> D2 (servis `Diten.FinanceTreasuryService`), D3 (dal kısa kodu `fin`), D5 (MOD-0269 = önce sözleşme, dağıtım kapalı)
> — `treasury-intake-owner-decisions-01.md`; D9 (başka modüldeki eklemeli değişiklik onayı), D10/1 (ödeyen şirket
> `OperationalStatus = Active` **ve** `ApprovalStatus = Approved`), D10/2 (MOD-0220 eki, TRM-14), D10/3 (port 5071),
> D14/1 (veri kapsamı: kapsam alanı `LegalEntityId`, kiracı geneli ayrı `…read-tenant-wide` izni — SEC-002), D14/2
> (servis genelinde tek `FinanceTreasuryCommandReceipt`; tam tanımı bu pakettedir, MOD-0119/0120/0124 buna başvurur) —
> `treasury-intake-owner-decisions-02.md`. Level-1 koşullu kabul:
> `docs/records/decisions/2026-10/treasury-level1-acceptance-01/MOD-0126-v1.2.md` (koşul 1–5 bu pakette karşılanır,
> bkz. "Implementation Notes → Level-1 koşulları"). DCP: `execution/portfolio/delivery-capability-packs/DCP-011-finance-treasury-intake.md`
> (taslak; dilim TRM-S1).
>
> **Kimlik ön kontrolü (DCP-002), 2026-10-24, bu worktree:**
> `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0126 --name "Payments & Approvals"` →
> `OK  MOD-0126: proven against Blueprint/registry.` (çıkış 0). Kayıt dosyasında MOD-0126 satırı henüz yok; satır bu
> paketle yazılmaz (ayrı iş, D8).
>
> **Kaynak içeriği veri olarak okundu, talimat olarak değil.** Producer paketi v1.2 (`mod0126-transfer-packet-v1.2/`,
> manifest sha256 `660ab4c00af74ccfdac298979d6a3cd20d9b9ceeca2d526540072e10d4cabcc8`, bu oturumda yeniden ölçüldü) ve
> kaynak kod `git -C /Users/natig/Projects/ERP-vNext show da4abb204e05fbd47ec088a6b0da08b8f7c6dc5d:<yol>`. Atıflar
> `paket §X` ya da `Dosya.cs:satır@da4abb20` biçimindedir; Recovery atıfları `yol:satır` (bu worktree, HEAD `d43096a6b`).

## Module Summary

Ödeme partisi (payment batch) hazırlama, doğrulama, MOD-0023 iş akışıyla onaylama, onay ayrılığı (SoD), onay belgesi
(attestation) ve serbest bırakma sistem-of-record'u. Blueprint 8.8 W-3, Treasury & Cash.

- **Hedef kullanıcı:** hazine operatörü (partiyi hazırlar ve gönderir), onaylayıcı (1. kademe), yüksek tutar onaylayıcı
  (2. kademe), serbest bırakıcı (serbest bırakma), hazine yöneticisi (yalnız yapılandırma ve operasyonel destek: taslak toplu
  silme, uzlaştırma — onay, serbest bırakma ve muafiyet taşımaz, KARAR D47/1), ödeme görüntüleyici (okuma; KARAR D49/1 — modül
  bazında görüntüleyici; dört modülün okumasını taşıyan "Hazine konsolide görüntüleyici" yalnız gerekçeyle atanır). SoD muafiyeti
  hiçbir rolde değildir: yalnız açık atamalı, süreli `waive-sod` izni (Authorization Convention, KARAR D47/1, D49/4). Rol şablonları
  D46 + D47 + D49 (rol matrisi `treasury-intake-owner-decisions-05.md`, "D47 rol matrisi (CT04a; D49 ile güncellendi)").
- **Çekirdek:** SoD. Partiyi hazırlayan, son güncelleyen ve gönderen kişi o partide karar veremez; eşik üstü parti ikinci
  kademe izni ister; eşik **tek**, **para birimli** ve **politika sürümlü**dür; muafiyet izin + gerekçe + kanıt ister.
- **Kapsam ilkesi:** kaynaktaki 11 HTTP işlemi (PP-01…PP-11, paket §C) Recovery standartlarına uyarlanır; kaynak
  kusurları taşınmaz ("Implementation Notes → Taşınmayacaklar").
- **Bankaya ödeme gitmez.** Serbest bırakma MOD-0269 arayüz sözleşmesinden geçer; bağdaştırıcı "yapılandırılmamış" döner
  ve deneme kaydedilir (D5).

## Ownership and Boundaries

- **In-scope:** `PaymentBatch` + `PaymentInstruction` (elle hazine kaynağı, `MANUAL_TREASURY`); doğrulama; MOD-0023'e
  gönderme (yerel niyet kaydı + idempotency anahtarı + belirsiz sonuç + uzlaştırma); karar (onay, ret, kanıt isteme,
  yükseltme) ve SoD; SoD muafiyeti; kanıt bağlantısı (MOD-0031 referansı); onay belgesi; serbest bırakma denemesi
  (dağıtım kapalı); istisna kaydı ve iz (trace) okuma; kiracı ekranı (liste + oluştur + düzenle + detay).
- **Out-of-scope:** gerçek banka dağıtımı, banka geri çağrıları, ekstre (MOD-0269, D5 — dört kapalı akış varsayılmaz);
  banka ana verisi ve hesap çözümleme (MOD-0289, MDM — bu modül yalnız opak referans taşır); AP kaynaklı parti
  (`ApLinked`, MOD-0119 — kaynak enum'da var, `PaymentProposalEnums.cs:3-7@da4abb20`; bu dilimde reddedilir); nakit
  tahmini besleme (MOD-0124/0125); GL kaydı (MOD-0118 — KARAR D27/4, kapsam dışı); MOD-0146 Payment Run Support (dışlama korunur);
  tedarikçi, müşteri, şirket ana kayıtları (MOD-0140, MOD-0149, MOD-0220); Görev Merkezi köprüsü (WC-D1; follow-up).
- **Sahip olunan sözleşme:** `PAYMENT-BATCH` (yeni, `docs/analysis/contracts/payment-batch.openapi.yaml`, x-owner
  MOD-0126) — kaynakta resmi sözleşme yok (paket §B "ABSENT"; level-1 koşul 5). Tek yazıcı MOD-0126 (K15).
- **Tüketilen sözleşmeler:** MOD-0220 `lookup-validation` (+ TRM-14 `ApprovalStatus`), MOD-0023 iş akışı başlatma ve
  görev geçişleri, MOD-0031 kanıt bağlantısı, MOD-0048 para birimi, MOD-0269 dağıtım arayüzü (BANK-DISPATCH 0.1.0 FROZEN, D27/7), Platform veri
  kapsamı (`IDataScopeResolver`, SEC-002 — D14/1).

## Owned Objects

### Varlıklar (kaynak 8 sınıf → Recovery karşılığı)

| # | Kaynak sınıf (`PaymentProposalEntities.cs@da4abb20`) | Recovery | Gerekçe |
|---|---|---|---|
| 1 | `PaymentBatch` (:5-31) | **taşınır** — `PaymentBatch : EntityBase` | çekirdek |
| 2 | `PaymentInstruction` (:33-53) | **taşınır** — `PaymentInstruction : EntityBase` | satır |
| 3 | `PaymentApprovalDecision` (:68-97) | **taşınır + `State`** (niyet/sonuç) | karar niyeti uzak çağrıdan önce yazılır |
| 4 | `PaymentException` (:99-114) | **taşınır** | istisna kuyruğu |
| 5 | `PaymentAttestation` (:116-130) | **taşınır**, yalnız `payment.attestation.v2` / manifest v2 | Recovery'de tarihsel v1 kayıt yok (paket §B, v0.8) |
| 6 | `PaymentEvidenceLinkReference` (:132-149) | **taşınır** (değer nesnesi) | MOD-0031 referansı |
| 7 | `PaymentOutboxMessage` (:55-66) | **taşınmaz** | kaynakta yalnız MOD-0021'e denetim aktarımıdır (paket §F "audit relay to MOD-0021 only"); Recovery'de `CentralAudit` outbox'ı (`central_audit_outbox`) yerini alır (AUD-001 K4) |
| 8 | `PaymentOutboxAttemptLog` (:151-163) | **taşınmaz** | aynı neden; teslim denemelerini `CentralAuditDispatcher` tutar |
| + | — | **yeni** `PaymentWorkflowStartIntent` | uzak başlatmadan önce yerel niyet (level-1 koşul 2) |
| + | — | **yeni** `PaymentDispatchAttempt` | serbest bırakma denemesi; MOD-0269 "yapılandırılmamış" sonucu kaydı (D5) |
| + | — | **yeni** `PaymentSodSettings` | kiracı başına **tek** SoD ayar kaydı (tek eşik + R4 açık / kapalı; R1–R3 kapatılamaz — **KARAR D57/4**), değişikliği denetlenir — **KARAR D56/4**; DL-2 yazar (Entity Fields), ekranı DL-4 (D57/4) |
| + | — | **yeni** `FinanceTreasuryCommandReceipt` | **servis ortak** istemci idempotency makbuzu (D12/2, D14/2); `Pending`/`Completed` yaşam döngüsü — Entity Fields. Tek tanım bu pakettedir; MOD-0119, MOD-0120, MOD-0124 aynı tipi kullanır |

### Kaynak işlemleri (PP-01…PP-11, paket §C) → Recovery

| Kaynak | Kaynak rota (`api/v{v}/mod0126/…`) | Recovery komut / sorgu | Recovery uç (Gateway `/api/…`) | Karar |
|---|---|---|---|---|
| PP-01 | POST payment-batches/manual | `CreatePaymentBatchCommand` | `POST /api/payment-batches` | taşınır |
| PP-02 | GET payment-batches | `GetPaymentBatchListQuery` | `GET /api/payment-batches` | taşınır (DataTable server) |
| PP-03 | GET payment-batches/{id} | `GetPaymentBatchByIdQuery` | `GET /api/payment-batches/{id}` | taşınır |
| PP-04 | POST …/validate | `ValidatePaymentBatchCommand` | `POST /api/payment-batches/{id}/validate` | taşınır |
| PP-05 | POST …/admin-direct-decision | — | — | **düşürüldü — sahip kararı D11/2** (O-1): kaynak kendini "Phase-1 golden-flow proxy only. Not final MOD-0023 SoD" diye tanımlıyor (`PaymentLifecycleService.cs:13-15@da4abb20`), SoD servisini çağırmıyor ve `PassedByAdminProxy` yazıyor (paket §H.2 (4)) |
| PP-06 | POST …/attestation | `GeneratePaymentAttestationCommand` | `POST /api/payment-batches/{id}/attestations` | taşınır |
| PP-07 | GET …/trace | `GetPaymentBatchTraceQuery` | `GET /api/payment-batches/{id}/trace` | taşınır |
| PP-08 | POST …/submit-workflow | `SubmitPaymentBatchCommand` | `POST /api/payment-batches/{id}/submit` | **uyarlanır**: yerel niyet önce, idempotency anahtarı, belirsiz sonuç |
| PP-09 | POST …/workflow-decision | `ApprovePaymentBatchCommand`, `RejectPaymentBatchCommand`, `RequestPaymentBatchEvidenceCommand`, `ApprovePaymentBatchWithSodWaiverCommand` | `POST /api/payment-batches/{id}/{approve\|reject\|request-evidence\|approve-with-sod-waiver}` | **bölünür**: karar türü başına bir komut (her biri kendi izni ve denetim olayıyla); muafiyet ayrı komut. **Escalate v1 dışı (D22/1)**: MOD-0023'te görev düzeyinde karşılığı yok; yükseltme MOD-0023 SLA kurallarıyla olur |
| PP-10 | GET payment-exceptions | `GetPaymentExceptionListQuery` | `GET /api/payment-exceptions` | taşınır |
| PP-11 | POST outbox/dispatch-once | — | — | **düşürülür** (O-2): elle "bir kez gönder" ucu kaynak denetim outbox'ına aittir; Recovery'de iletim `CentralAuditDispatcher`'ındır |

### Recovery ekleri (kaynakta yok)

| Komut | Uç | Neden |
|---|---|---|
| `UpdatePaymentBatchCommand` | `PUT /api/payment-batches/{id}` | Compact `Edit` sayfası; yalnız `Draft`/`Validated`, gönderilmemiş parti (O-3) |
| `DeletePaymentBatchCommand` | `DELETE /api/payment-batches/{id}` | Golden Reference silme seti; yalnız hiç gönderilmemiş parti, soft delete (O-3) |
| `BulkDeletePaymentBatchCommand` | `DELETE /api/payment-batches/bulk` | Golden Reference zorunlu toplu silme; aynı kural (O-3) |
| `ReconcilePaymentBatchWorkflowCommand` | `POST /api/payment-batches/{id}/workflow/reconcile` | belirsiz başlatma / karar sonucunu MOD-0023'ten okuyup kapatır |
| `LinkPaymentBatchEvidenceCommand` | `POST /api/payment-batches/{id}/evidence-links` | kaynakta kanıt bağlama oluşturmanın içinde ve yerel kayıttan sonra, işlem dışında (`PaymentProposalService.cs:235-298@da4abb20`); Recovery'de ayrı, idempotent komut |
| `ReleasePaymentBatchCommand` | `POST /api/payment-batches/{id}/release` | serbest bırakma denemesi → MOD-0269 portu → "yapılandırılmamış" + kayıt (D5) |
| `UpdatePaymentSodSettingsCommand`, `GetPaymentSodSettingsQuery` | uç yolu DL-2 prompt'undan önce `payment-batch.openapi.yaml`'a yazılır; izinler `finance-treasury.payment-sod-settings.update` / `.read` (**KARAR D57/4**, Authorization Convention → "SoD ayarı izinleri") | kiracının SoD ayar kaydını okur / değiştirir (**KARAR D56/4**; DL-2); kayıt yokken okuma varsayılanı döndürür (R1–R4 açık, varsayılan eşik — D57/4) |

- **Sorgular:** `GetPaymentBatchListQuery`, `GetPaymentBatchByIdQuery`, `GetPaymentBatchTraceQuery`,
  `GetPaymentExceptionListQuery`.
- **DTO'lar:** `PaymentBatchModels.cs` tek dosyada (Golden Reference): `PaymentBatchListItemDto`, `PaymentBatchDetailDto`,
  `PaymentInstructionDto`, `PaymentBatchWriteRequest`, `PaymentInstructionWriteRequest`, `PaymentDecisionRequest`,
  `PaymentSodWaiverRequest`, `PaymentWorkflowStateDto`, `PaymentDecisionDto`, `PaymentExceptionDto`,
  `PaymentAttestationDto`, `PaymentDispatchAttemptDto`, `PaymentBatchTraceDto`. DTO ve isteklerde `TenantId` ve
  `LegalEntityId` **yoktur**.
- **Portlar (Application):** `ILegalEntityEligibilityPort` (MOD-0220), `IPaymentWorkflowPort` (MOD-0023),
  `IPaymentEvidencePort` (MOD-0031), `ICurrencyReferencePort` (MOD-0048), `IBankDispatchPort` (MOD-0269),
  `IRecordCodeAllocator` (RCS-001 ortak kod servisi; `AllocateAsync(legalEntityId, recordType, idempotencyKey) → code`, KARAR D59/1), `IPaymentSodPolicyProvider` (tek eşik + R4 anahtarı, R1–R3 her zaman açık — D57/4; kaynağı
  `PaymentSodSettings`, KARAR D56/4), `IPaymentSodWaiverAssignmentSource` (Auth `waive-sod` ataması alanları, kullanım anı —
  KARAR D54, D56/3; ad öneri), `IFinanceTreasuryApproverCandidateSource` (Auth aday sorgusu — onaylayıcı ve muafiyet adayları,
  KARAR D66/1; **servis ortak**, tek yazarı bu paketin DL-2'si; ad öneri),
  `IFinanceTreasuryDataScopeSource` (SEC-002 veri kapsamı, D14/1 — **servis ortak** tek çözüm noktası; MOD-0119/0120/0124
  aynı arayüzü kullanır), `IPaymentMethodReferencePort` (MOD-0048 `payment-method` kümesi, D28 / O-13 — **MOD-0126'ya
  özgü**, servis ortak yapı taşı değildir).
- **Ödeme yöntemi kaynağı (`IPaymentMethodReferencePort`, Application; bağdaştırıcısı `Infrastructure/Currency/`, MOD-0048
  bağdaştırıcılarıyla aynı yer):** MOD-0048 `GET /api/lookups/reference-data/consumable-sets/payment-method/published-values`
  ucunu zaman aşımı yapılandırılmış `HttpClient` ile okur. Kabul: kümede **yayımlanmış** ve `IsActive = true` olan `Code`;
  karşılaştırma büyük-küçük harfe duyarlı, `StringComparison.Ordinal` (code-style S-56). Kümede yok ya da pasif → 422
  `PAYMENT_METHOD_NOT_ALLOWED`, kayıt değişmez; küme okunamıyor (5xx, zaman aşımı, ayrıştırılamayan gövde, küme yok / izin
  listesinde değil) → 503 `PAYMENT_METHOD_SOURCE_UNAVAILABLE`, yazma yok. Serbest metin ya da gömülü liste yok. Kurallar:
  Validation Rules, Failure Path to Verify, AC-41.
- **Frontend rotası:** `/FinanceTreasury/PaymentBatches` (+ `/Create`, `/Edit/{id}`, `/Details/{id}`).
- **İzinler:** "Authorization Convention" tablosu.

### Durum makinesi (`PaymentBatchStatus`, kaynakla aynı 10 değer — `PaymentProposalEnums.cs:9-21@da4abb20`)

| Durum | Değer | Recovery anlamı | Giriş | Çıkış |
|---|---|---|---|---|
| Draft | 0 | taslak | oluşturma, güncelleme, kesin başlatma reddi (önceki durum Draft ise) | Validate, Update, Submit, Delete |
| Validated | 1 | doğrulandı | Validate başarılı | Update (→ Draft), Submit, Delete |
| Submitted | 2 | **yerel başlatma niyeti yazıldı; MOD-0023 sonucu henüz kesin değil** (Pending ya da Unknown) | Submit tx1 | kesin başarı → PendingApproval; kesin ret → önceki durum; Unknown → Reconcile |
| PendingApproval | 8 | MOD-0023 örneği başladı, karar bekleniyor | kesin başlatma başarısı / Reconcile | kararlar (son adım olmayan onay → `PendingApproval` kalır, onaylanmış sayılmaz — KARAR D66/2) |
| PartiallyApproved | 9 | son adım olmayan onay — **v1'de girişi yok** (KARAR D62/2: şablon `FT-PAYMENT-BATCH-APPROVAL` tek adımlı, Y-12; değer ve ad enum'da korunur, çok adımlı onay gelince açılır; AC-46) | — | — |
| EvidenceRequested | 6 | kanıt istendi | RequestEvidence | kararlar |
| Escalated | 7 | yükseltildi — **v1'de girişi yok** (D22/1; değer enum'da korunur, kaynakla aynı 10 değer) | — | — |
| Approved | 3 | son onay (terminal karar) | Approve (son adım; v1 şablonu tek adımlı — D62/2) | Release denemesi (durum değişmez) |
| Rejected | 4 | ret (terminal) | Reject | — |
| Cancelled | 5 | MOD-0023 örneği iptal / kapatıldı (terminal) | yalnız Reconcile, MOD-0023 örneği iptal ya da terk edilmiş olarak kapandı dediğinde | — |

- Kaynakta `Submitted` hiçbir yerde atanmıyor (`git grep PaymentBatchStatus.Submitted` @da4abb20 → 0 atama; gönderme
  doğrudan `PendingApproval` yazıyor, `PaymentWorkflowService.cs:190@da4abb20`). Recovery bu değeri "niyet yazıldı, sonuç
  bekleniyor" anlamında kullanır; yeni durum eklenmez.
- **Niyet ve karar durumları — tek geçiş matrisi (gap 2/G5):** başlatma niyeti ve karar niyeti aynı sonuç sözlüğünü
  kullanır. Açık durumlar: `Pending` (başlatma) / `Requested` (karar) ve `Unknown`. Kapalı durumlar: `Started` (başlatma) /
  `Recorded` (karar), `Refused`, `Cancelled`.

| Niyet | Açık → | tx2 (kesin sonuç) | belirsiz | Reconcile (yalnız `ReconcilePaymentBatchWorkflowCommand`) | Sonuç olayı |
|---|---|---|---|---|---|
| `PaymentWorkflowStartIntent.Status` | `Pending` | `Started` · `Refused` | `Unknown` | `Unknown`/`Pending` → `Started` · `Refused` · `Cancelled` | `…submitted` · `…workflow-start-refused` · `…workflow-start-cancelled` |
| `PaymentApprovalDecision.State` | `Requested` | `Recorded` · `Refused` | `Unknown` | `Unknown`/`Requested` → `Recorded` · `Refused` · `Cancelled` | `…approved`/`…approved-with-sod-waiver`/`…rejected`/`…evidence-requested` · `…decision-refused` · `…decision-cancelled` |

  `Refused` = MOD-0023 kesin reddetti; `Cancelled` = MOD-0023 örneği ya da görevi iptal edilmiş / kapanmış bulundu
  (yalnız Reconcile atar, yalnız uzak tarafın **kesin** iptal/kapanış kaydıyla — o örnek için MOD-0023 iptal geçiş
  kaydı ya da `CloseAbandonedWorkflowStart` kapanışı; bulunamadı / 404 / zaman aşımı / belirsiz okuma `Cancelled`
  sayılmaz, niyet `Unknown` kalır — Runtime Constraints 3a). Kaynaktaki `RefusedByWorkflow`
  adı kullanılmaz.
- **Servis geneli durum sözlüğü (sahip kararı D14/2, ortak niyet altyapısı):** bu matris `Diten.FinanceTreasuryService`'in
  **tek** niyet durum sözlüğüdür. Niyet tipleri modüle özgü kalır (MOD-0119, MOD-0120, MOD-0124'ün başlatma, karar ve
  dışa aktarma niyetleri) ama durum adları buradakilerdir: açık `Pending` · `Requested` · `Unknown`; kapalı `Started` ·
  `Recorded` · `Refused` · `Cancelled`. Yeni durum adı eklenmez; `Cancelled` yalnız Reconcile'da atanır.
- `PaymentSodResult` (kaynak 10 değer, `PaymentProposalEnums.cs:31-43@da4abb20`) Recovery'de: `Passed`,
  `FailedSelfApproval`, `FailedSubmitterApproval`, `FailedUpdaterApproval` (yeni), `FailedRoleConflict`,
  `FailedAmountTier`, `Waived`. **Taşınmaz:** `NotEvaluatedByWorkflowProxy`, `PassedByAdminProxy` (PP-05 ile birlikte),
  `NotApplicableForManualTreasury`, `DeferredExternalSource` (kaynakta karşılığı olan akış bu dilimde yok).

## Entity Fields

Taban: servisin kendi `EntityBase`'i (`entity-base-template.md`): `Id`, `TenantId`, `IsDeleted`, `DeletedAt`,
`CreatedAt`, `UpdatedAt`, `Version` — tekrar tanımlanmaz. İş modülü olduğu için `CreatedBy` / `UpdatedBy` (`Guid?`,
token `sub`'ından; DTO'dan değil) **zorunlu**. Kaynak tabanı `TenantEntity` (`D/Common/TenantEntity.cs:3-15@da4abb20`)
`DateTime` + `long RowVersion` + `string CreatedBy = "system"` taşır; Recovery'de `DateTimeOffset` (UTC), `int Version`
(iyimser eşzamanlılık) ve `Guid` aktör kimliği kullanılır. Not: kaynak tabanda `DeletedAt` alanı **vardır** (`:8`) ama
hiçbir yol `IsDeleted = true` yazmıyor ve silme ucu yok (paket §F). Recovery'de soft delete `IsDeleted` + `DeletedAt`
(UTC) birlikte yazılır.

**Kapsam alanları (her varlıkta):** `TenantId` (`ITenantContext`) ve `LegalEntityId` (isteğin `X-Legal-Entity-Id`
başlığı, MOD-0220'den doğrulanmış; Recovery R-2 deseni, örn. `services/Diten.SupplyChainService/src/Diten.SupplyChainService.Api/Features/Loads/LoadContextMiddleware.cs:61-81`)
sunucuda yazılır; gövde ya da sorgu parametresiyle gelen `tenantId` / `legalEntityId` 400 ile reddedilir. Alt kayıtlar
(talimat, karar, istisna, niyet, deneme, onay belgesi) partinin `LegalEntityId`'sini taşır ki her sorgu Tenant + LE ile
süzülsün.

**Para (SOP §18.8, domain-config "Para / decimal"):** tüm tutarlar `decimal` (Mongo `Decimal128`); `float`/`double`
yasak. Para birimi **parti başına tek**: ISO 4217, 3 büyük harf; her talimat partinin para birimini taşır ve farklıysa
reddedilir (`PaymentProposalService.cs:77-85@da4abb20`). Hassasiyet: para biriminin küsurat hanesi MOD-0048'den;
fazla haneli tutar **yuvarlanmaz, reddedilir** (`:94-99@da4abb20`). `TotalAmount` ve `PaymentCount` sunucu hesaplar,
istekten alınmaz. Kur çevrimi yok (FX tarihi uygulanmaz). Saat dilimi: zaman damgaları UTC; `RequestedValueDate`
yalnız tarihtir (`DateOnly`), saat dilimi çevrimi yapılmaz.

### PaymentBatch (`payment_batches`)

| Alan | Tip | Kural | Kaynak |
|---|---|---|---|
| BatchCode | string | RCS-001 OTOMATİK `{ŞİRKET}-{TÜR}-{SAYAÇ}`; tür öneki önerisi `PAY` (O-9); kullanıcı giremez/değiştiremez; silinen partinin kodu yeniden kullanılmaz | kaynak `BatchNumber` istemciden de alınıyor (`PaymentProposalService.cs:126-128@da4abb20`) ve sayaç `CountAsync()+1` (`:471-475`) — taşınmaz |
| SourceKind | enum `ManualTreasury`=1 | sunucu sabitler; `ApLinked` reddedilir | `:369-377@da4abb20` |
| DefaultSourceBankAccountRef | Guid | zorunlu, boş olmayan; **opak** MOD-0289 referansı | `:384-387@da4abb20` |
| PaymentMethod | string | zorunlu, trim, ≤ 32; değer MOD-0048 `payment-method` kümesinin **yayımlanmış ve aktif** bir `Code`'u (KARAR D28 / O-13; kural Validation Rules) | `:389-398@da4abb20` |
| CurrencyCode | string(3) | zorunlu, ISO 4217, upper | `:400-405@da4abb20` |
| TotalAmount | decimal | sunucu: talimatların toplamı; > 0 | `:130@da4abb20` |
| PaymentCount | int | sunucu: talimat sayısı | — |
| RequestedValueDate | DateOnly | zorunlu; geçmiş tarih kuralı TBD (O-12) | `:407-410@da4abb20` |
| Status | `PaymentBatchStatus` | durum makinesi | — |
| RiskRating | enum Low/Medium/High | varsayılan Low | `:145@da4abb20` |
| EvidenceRequired | bool | true ise gönderme/son onay öncesi en az bir kanıt bağlantısı ister | — |
| Notes | string? | trim, ≤ 2000 (kaynakta sınır yok; öneri) | — |
| SubmittedBy / SubmittedAt | Guid? / DateTimeOffset? | sunucu (tx1) | — |
| UpdatedByBeforeSubmit | Guid? | gönderme anındaki son güncelleyen (SoD R3) | yeni |
| WorkflowInstanceId / WorkflowStatus / WorkflowCorrelationId | Guid? / string? / string? | yalnız kesin başlatma sonucundan | `:185-189@da4abb20` |
| SodPolicyVersion / Tier2ThresholdAmount / Tier2ThresholdCurrency / IsTier2 | string / decimal / string(3) / bool | gönderme anında sabitlenir (pinned); karar aynı değeri kullanır | yeni (koşul 1) |
| EvidenceLinks | `PaymentEvidenceLinkReference[]` | MOD-0031 referansı; ikili veri yok | :24 |
| ReleaseSequence | int | 0'dan başlar; her **yeni** Release denemesi aynı işlemde +1 (sürüme koşullu, CAS); makbuz tekrarı artırmaz; üst sınır 999 999 999 — ulaşılmışsa 409 `PAYMENT_RELEASE_SEQUENCE_EXHAUSTED` (artırma ve port çağrısı yok; gap 8/P2-b); MOD-0269 `dispatchKey = mod0126:{batchId:N}:release:{ReleaseSequence}` (gap 7/P1-b) | yeni |
| ReleaseBlockedForReview | bool | varsayılan false; bağdaştırıcı `NotConfigured` dışı sonuç döndürürse true olur ve sonraki Release'i kapatır (D17/2) | yeni |
| CreatedBy / UpdatedBy | Guid? | token `sub` | — |

### PaymentInstruction (`payment_instructions`)

| Alan | Tip | Kural |
|---|---|---|
| PaymentBatchId, LineNumber | Guid, int | sunucu; 1..500 satır (`PaymentProposalService.cs:15,418-427@da4abb20`) |
| ManualOutflowReference | string | zorunlu, trim, ≤ 64 (öneri; kaynakta yalnız boş olmama, `:437-441`) |
| PayeeType | enum LegalEntityCounterparty=1, Supplier=2, Employee=3, Other=99 | tanımlı değer (`:455-459`) |
| PayeeReferenceId | Guid? | `PayeeType ≠ Other` ise zorunlu (öneri); varlık doğrulaması TBD (O-14) |
| DestinationReferenceId | Guid? | alacaklı banka hesabının **opak** MOD-0289 referansı |
| SourceBankAccountRef | Guid | zorunlu, boş olmayan, opak (`:443-447`) |
| Amount | decimal | > 0; küsurat ≤ para birimi hanesi (`:87-99`) |
| CurrencyCode | string(3) | sunucu partiden yazar; istekte farklıysa ret |
| DocumentDirection | enum Payment | bu dilimde yalnız `Payment` (`:171`) |
| RemittanceReference | string? | trim, ≤ 140 (öneri) |
| SourceDocumentRef | string? | trim, ≤ 128 (öneri) |
| ManualOverride | bool | varsayılan false |
| ValidationStatus / ValidationMessages | enum / string[] | Validate yazar; mesajlar resx anahtarıdır, serbest İngilizce metin değil |

> **`form_field_count: 16` → `golden_reference: compact`.** Oluştur/düzenle formunda kullanıcının doldurduğu alanlar
> (kaynak istek modeli `CreateManualPaymentBatchRequest` / `CreateManualPaymentInstructionRequest`,
> `PaymentProposalContracts.cs:5-34@da4abb20`; ekran indeksi paket §I): **başlık 7** — DefaultSourceBankAccountRef,
> PaymentMethod, CurrencyCode, RequestedValueDate, RiskRating, EvidenceRequired, Notes; **talimat satırı 9** —
> ManualOutflowReference, PayeeType, PayeeReferenceId, DestinationReferenceId, SourceBankAccountRef, Amount,
> RemittanceReference, SourceDocumentRef, ManualOverride. **Sayılmayanlar:** BatchCode (RCS-001 OTOMATİK), SourceKind
> (sunucu sabit), LegalEntityId (oturum şirketi, `X-Legal-Entity-Id`), satır CurrencyCode (partiden türetilir),
> Idempotency-Key (başlık; yalnız `FinanceTreasuryCommandReceipt`'te saklanır) / CorrelationId (başlık), TotalAmount / PaymentCount (sunucu), kaynaktaki `ApSource` / `Gateway` /
> `ForecastFeed` / `EvidenceLinks` (bu dilimde istek modelinde yok). 16 > 8 → Compact (ayrı Create / Edit / Details).

**Banka verisi yasağı:** hiçbir varlık IBAN, hesap numarası, BIC/SWIFT, banka adı ya da sağlayıcı sırrı tutmaz; yalnız
opak Guid referansı (domain-config "Banka verisi bu domain'de saklanmaz"). Çözümleme MOD-0289'dan ve yalnız dağıtım
anında yapılır (dağıtım bu dilimde kapalı).

### PaymentWorkflowStartIntent (`payment_workflow_start_intents`, yeni)

| Alan | Tip | Kural |
|---|---|---|
| PaymentBatchId | Guid | — |
| IdempotencyKey | string | deterministik: `mod0126:{batchId:N}:start:{SubmissionSequence}`; aynı niyet için hiç değişmez |
| SubmissionSequence | int | partinin kaçıncı gönderimi (kesin ret sonrası yeniden gönderme yeni sıra alır) |
| Status | enum Pending/Started/Refused/Cancelled/Unknown | tek geçiş matrisi (`Cancelled` yalnız Reconcile; D14/2 hizalaması) |
| PreviousBatchStatus | `PaymentBatchStatus` | kesin retteki geri dönüş (Draft/Validated) |
| CandidatePrincipalIds / ExcludedPrincipalIds | Guid[] | çözümleyici sonucu (SoD R1–R4) |
| SodPolicyVersion, Tier2ThresholdAmount, Tier2ThresholdCurrency, IsTier2 | — | sabitlenen politika |
| WorkflowInstanceId | Guid? | yalnız kesin sonuçtan |
| LastOutcomeCode / LastAttemptAt / AttemptCount | string? / DateTimeOffset? / int | `PAYMENT_WORKFLOW_START_*` kodları |
| CorrelationId | string | — |

### PaymentApprovalDecision (`payment_approval_decisions`)

Kaynak alanlar (`PaymentProposalEntities.cs:68-97@da4abb20`) korunur: `Decision`, `ReasonCode`, `Comment`,
`EvidenceRefs`, `EvidenceLinks`, `SodResult`, `SodCheckDetail` (resx anahtarı + parametre), `RemoteIdempotencyKey`
(kaynakta `IdempotencyKey`; Recovery'de MOD-0023 görev geçişine giden **uzak** anahtar, deterministik
`mod0126:{decisionId:N}:transition` — istemci anahtarı değildir),
`DecidedBy` (**Guid**, kaynakta string), `DecidedAt`, `RequestFingerprintVersion`, `RequestFingerprint`, `WorkflowInstanceId`,
`WorkflowTaskId`, `WorkflowTransitionId`, `WorkflowIsFinalStep`, `SodPolicyVersion`, `WaiverRequested`,
`WaiverReasonCode`. **Çıkarılan:** `DecisionPath` (tek yol kalır: iş akışı), `AuditOutboxId`, `AuditEventId` (CentralAudit
taşır). **Eklenen:** `State` (Requested/Recorded/Refused/Cancelled/Unknown — tek geçiş matrisi yukarıda),
`IsWaiverCandidate` (bool — onaylayıcı muafiyet adayı olarak atandıysa, D13).

### PaymentSodSettings (`payment_sod_settings`, yeni — KARAR D56/4)

Kiracı başına **tek** SoD ayar kaydı (FinanceTreasuryService içinde): tek eşik + R4 açık / kapalı. **KARAR D57/4:** R1–R3
kapatılamaz — kayıtta anahtarları yoktur, her sürümde açıktır; yalnız eşik ve R4 ayarlanır. Kiracı düzeyi ayar
olduğu için `LegalEntityId` taşımaz (yukarıdaki "Kapsam alanları" kuralının tek istisnası — D56/4 "kiracı başına"). Silinmez
(soft delete yok); kayıt yoksa R1–R4 açık ve varsayılan eşik geçerlidir — D11/1 güvenli varsayılanı: her onay tier-2, kodda
tutar yok (KARAR D57/4; Runtime Constraints). Değişikliği denetlenir (Audited Events
`finance-treasury.payment-sod-settings.updated`, AC-44). DL-2 yazar; ekranı DL-4'tedir (KARAR D57/4, Frontend File Contract).

| Alan | Tip | Kural |
|---|---|---|
| TenantId | Guid | sunucu; `ux_payment_sod_settings_tenant` ile kiracı başına tek kayıt |
| PolicyVersion | int | sunucu yazar; her değişiklikte +1; gönderme anında partiye ve niyete sabitlenir (R6) |
| Tier2ThresholdAmount | decimal | zorunlu, > 0; para biriminin küsurat hanesini aşamaz (MOD-0048; yuvarlanmaz) |
| Tier2ThresholdCurrencyCode | string(3) | zorunlu; ISO 4217, MOD-0048 para birimi listesinde |
| R4Enabled | bool | zorunlu; R4 açık / kapalı (D56/4); değer `PolicyVersion` ile partiye sabitlenir. R1–R3 için alan **yoktur** — kapatılamaz, her zaman açık (KARAR D57/4) |
| Version, CreatedAt / UpdatedAt, CreatedBy / UpdatedBy | `EntityBase` | `If-Match` + `Version` CAS |

### FinanceTreasuryCommandReceipt (`finance_treasury_command_receipts`) — servis ortak idempotency makbuzu (gap 2/G2–G3, D14/2)

**Tek ortak tip (sahip kararı D14/2):** `Diten.FinanceTreasuryService`'in bütün modülleri (MOD-0126, MOD-0119, MOD-0120,
MOD-0124) **bu tek tipi ve tek koleksiyonu** kullanır; modül önekli makbuz tipi açılmaz. Tip bu paketin DL-1'iyle (servisi
açan dilim) servis ortak yapı taşı olarak gelir; **tek sahibi ve yazarı MOD-0126 DL-1'dir** (Repo Scope, gap 4/G5); diğer
paketler burayı başvuru gösterir, yeniden tanımlamaz ve önce açmaz.
Tipin ya da kurallarının değişmesi dört paketin birlikte değişikliğini ister.

| Alan | Tip | Kural |
|---|---|---|
| TenantId, LegalEntityId, ActorId, Operation, TargetId, Key | Guid, Guid, Guid, string, Guid?, string | **makbuz kapsamı** (D12/2) — altı alan; `TargetId` Create için `null`, diğerlerinde komutun hedef kaydının kimliği (bu modülde parti) |
| Operation | string | **modül nitelikli işlem adı:** `{modül}:{Komut}` — ör. `mod0126:SubmitPaymentBatch`, `mod0119:ApproveApInvoice`, `mod0120:RequestReceivableInvoiceAdjustment`, `mod0124:PublishCashPosition`. Aynı anahtar başka modülün işleminde ayrı makbuzdur; modül öneki `LinkedIntentId`'nin hangi niyet koleksiyonuna ait olduğunu da söyler |
| RequestFingerprint / FingerprintVersion | string / int | sunucuda hesaplanır |
| Status | `Pending` \| `Completed` | tek aşamalı komut: tx1'de doğrudan `Completed`; iki aşamalı komut (bu modülde Submit ve kararlar): tx1'de `Pending`, tx2'de ya da — tx2 hiç çalışmadıysa — Reconcile'da `Completed` |
| ResponseStatusCode / ResponseBody | int? / string? | **yalnız `Completed`'da** dolu; o anda istemciye verilen (ya da verilecek) HTTP yanıtı — kesin sonuç **ya da** 202 belirsiz |
| LinkedIntentId | Guid? | iki aşamalı komutta başlatma ya da karar niyetinin kimliği (niyet tipi modüle özgüdür) |
| CreatedAt / CompletedAt | DateTimeOffset / DateTimeOffset? | — |

**Değişmezlik (gap 3/G2):** `Completed` makbuz **bir daha değişmez**. Belirsiz sonuçta kaydedilen 202 yanıtı (gövdesinde
`intentId` ve durum sorgusu bağlantısı) sonsuza kadar aynı döner; işlemin **gerçek sonucu** makbuzda değil niyette
(`PaymentWorkflowStartIntent` / `PaymentApprovalDecision`) izlenir ve `GET /api/payment-batches/{id}` ile `…/trace`
üzerinden okunur. Aynı anahtarla tekrar (replay) **asla kurtarma yapmaz**; Reconcile yalnız **`Pending`** makbuzu (tx1
sonrası çökme — istemci hiç yanıt almadı) `Completed` yapar; zaten `Completed` olan makbuza dokunmaz. Bu kurallar dört
modülde aynıdır.

**İstemci anahtarı yalnız makbuzdadır** (gap 3/G1): `PaymentBatch`, `PaymentApprovalDecision`, `PaymentAttestation`,
`PaymentDispatchAttempt` istemci anahtarı alanı taşımaz. MOD-0023'e giden anahtarlar ayrı ve **uzak** anahtardır:
`PaymentWorkflowStartIntent.IdempotencyKey` (`mod0126:{batchId:N}:start:{SubmissionSequence}`) ve
`PaymentApprovalDecision.RemoteIdempotencyKey`; deterministiktir, istemciden bağımsızdır.

### PaymentException (`payment_exceptions`), PaymentAttestation (`payment_attestations`), PaymentDispatchAttempt (`payment_dispatch_attempts`)

- `PaymentException`: kaynak alanları (`:99-114@da4abb20`); `OwnerUserId` Guid?. Bir SoD reddi **tek** istisna satırı
  yazar (kaynak iki satır yazıyor, `PaymentWorkflowService.cs:390-412@da4abb20` — taşınmaz).
- `PaymentAttestation`: kaynak alanları (`:116-130@da4abb20`); `SchemaVersion = payment.attestation.v2`,
  `ContentHash` (SHA-256), `SourceSnapshotJson` (yerel anlık görüntü; banka verisi içermez), `GeneratedBy` Guid.
- `PaymentDispatchAttempt` (yeni): `PaymentBatchId`, `ReceiptId` (Release makbuzu), `ReleaseSequence`, `DispatchKey`,
  `RequestedBy` (Guid), `RequestedAt`, `Outcome` (bağdaştırıcının döndürdüğü değer olduğu gibi: `NotConfigured` ya da
  beklenmeyen `Accepted` / `Rejected` / `Unknown` — D17/2), `OutcomeCode`, `ProviderReference?`,
  `ProviderContractVersion` (MOD-0269 sözleşme sürümü). Ekleme-yalnız; güncelleme/silme yok. Talimat başına kaynak
  hesap porta olduğu gibi gider (D17/1); deneme kaydı banka verisi taşımaz.

### Mongo indeksleri (kaynak 18 ad, 4 tekil — `PaymentProposalsMongoIndexes.cs:8-28@da4abb20`)

| Recovery indeksi | Anahtar | Tekil | Kaynak karşılığı |
|---|---|---|---|
| `ux_payment_batch_code` | TenantId + BatchCode | **evet, kısmi filtre YOK** (silinen partinin kodu da dolu sayılır, RCS-001 §6). Kodun RCS-001 ortak servisinden gelmesi veritabanı tekilliğini **kaldırmaz**; ikisi birlikte. Test + sabotaj: AC-23 (`PaymentBatchCodeUniquenessTests`) | `ux_mod0126_payment_batch_number` (kısmi `IsDeleted=false`, `:47-52` — taşınmaz) |
| `ux_finance_treasury_command_receipt_scope` | TenantId + LegalEntityId + ActorId + Operation + TargetId + Key (`finance_treasury_command_receipts`) | evet — **tek istemci idempotency kuralı** (D12/2); **servis ortak, bu paketin DL-1'i sahibidir** (D14/2); MOD-0119/0120/0124 yeniden bildirmez | kaynağın dört `ux_mod0126_*_idempotency` indeksinin (TenantId + IdempotencyKey) **yerine geçer**; o dört indeks taşınmaz |
| `ux_payment_start_intent_remote_key` | TenantId + IdempotencyKey (uzak, deterministik anahtar) | evet — MOD-0023'e aynı anahtarla tek başlatma | yeni |
| `ux_payment_start_intent_open` | TenantId + PaymentBatchId, kısmi `Status ∈ {Pending, Unknown}` | evet — partide aynı anda tek açık başlatma niyeti | yeni |
| `ux_payment_decision_open` | TenantId + PaymentBatchId, kısmi `State ∈ {Requested, Unknown}` | evet — partide aynı anda tek açık karar niyeti | yeni |
| `ix_finance_treasury_command_receipt_pending` | TenantId + Status + CreatedAt, kısmi `Status = Pending` (`finance_treasury_command_receipts`) | — | yeni (Reconcile listesi); servis ortak, sahibi bu paketin DL-1'i (D14/2) |
| `ux_payment_dispatch_attempt_key` | TenantId + DispatchKey (`payment_dispatch_attempts`) | evet — bir `dispatchKey` tek denemeye bağlanır (gap 7/P1-c; anahtar/yük çatışması yapısal olarak engellenir) | yeni |
| `ux_payment_sod_settings_tenant` | TenantId (`payment_sod_settings`) | evet — kiracı başına tek SoD ayar kaydı (KARAR D56/4; AC-44 sabotajı) | yeni |
| `ix_payment_batch_list` | TenantId + LegalEntityId + IsDeleted + Status + CreatedAt(desc) | — | `ix_mod0126_payment_batch_status` / `_created` |
| `ix_payment_batch_workflow_instance` | TenantId + WorkflowInstanceId (kısmi, var olan) | — | aynı |
| `ix_payment_instruction_batch` | TenantId + PaymentBatchId + LineNumber | — | aynı |
| `ix_payment_decision_batch` | TenantId + PaymentBatchId + DecidedAt | — | aynı |
| `ix_payment_exception_status` | TenantId + LegalEntityId + Status + Severity | — | aynı |
| `ix_payment_attestation_batch` | TenantId + PaymentBatchId + GeneratedAt(desc) | — | aynı |

İndeks kurulumu servis iskeletinin indeks barındırılan hizmetiyle yapılır (`mongo-indexing`); taşıma/geri doldurma yok
(kaynak veri taşınmaz — paket §L.13).

### Hata sözlüğü (kaynak 52 `PAYMENT_*`, `PaymentProposalErrorCodes.cs:5-56@da4abb20`; HTTP eşlemesi paket §E)

Taşınan kodlar korunur (`PAYMENT_VALIDATION_FAILED` 400, `PAYMENT_PERMISSION_DENIED` 403, `PAYMENT_IDEMPOTENCY_CONFLICT`
409, `PAYMENT_BATCH_NOT_FOUND` 404, `PAYMENT_BATCH_VERSION_CONFLICT` 409, `PAYMENT_DECISION_REASON_REQUIRED` 400,
`PAYMENT_DECISION_STATUS_INVALID` 409, `PAYMENT_BATCH_ALREADY_TERMINAL` 409, `PAYMENT_BATCH_ALREADY_SUBMITTED` 409,
`PAYMENT_WORKFLOW_TRANSITION_FAILED` 502, `PAYMENT_WORKFLOW_INSTANCE_MISMATCH` 409, `PAYMENT_WORKFLOW_TASK_INVALID` 400,
`PAYMENT_SOD_DENIED` 403, `PAYMENT_SOD_WAIVER_REQUIRED` 422, `PAYMENT_SOD_WAIVER_FORBIDDEN` 403,
`PAYMENT_SOD_WAIVER_REASON_REQUIRED` 400, `PAYMENT_TIER2_APPROVER_REQUIRED` 403,
`PAYMENT_WORKFLOW_RESOLVER_NO_ELIGIBLE_APPROVER` 422, `PAYMENT_CURRENCY_PRECISION_UNAVAILABLE` 503,
`PAYMENT_CURRENCY_SOURCE_UNAVAILABLE` 503 (para birimi listesi okunamıyor — bağımlılık hatası, girdi hatası değil),
`PAYMENT_CURRENCY_CODE_INVALID` 400 (biçimi geçerli ama MOD-0048 para birimi listesinde olmayan kod — girdi alanı hatası; dört
Treasury paketinde aynı 400), `PAYMENT_EVIDENCE_*`).

- **Değişen:** `PAYMENT_TENANT_FORBIDDEN` kullanılmaz — kiracılar/şirketler arası erişim **404** `PAYMENT_BATCH_NOT_FOUND`
  (multi-tenancy). `PAYMENT_WORKFLOW_START_FAILED` (kaynakta her başlatma hatası 502, `CustomBaseController.cs:57@da4abb20`)
  yalnız **kesin ret** için kullanılır.
- **Yeni:** `PAYMENT_WORKFLOW_START_OUTCOME_UNKNOWN` (202 — ret değildir), `PAYMENT_WORKFLOW_START_PENDING` (409 — açık
  niyet varken yeniden gönderme), `PAYMENT_DECISION_OUTCOME_UNKNOWN` (202), `PAYMENT_LEGAL_ENTITY_NOT_ELIGIBLE` (422 —
  aktif değil ya da onaylı değil), `PAYMENT_LEGAL_ENTITY_UNAVAILABLE` (503 — MOD-0220 yanıt vermedi; geçiş yok),
  `PAYMENT_SOD_POLICY_UNAVAILABLE` (503 — ayar deposu erişilemiyor; ayarın yokluğu hata değildir),
  `PAYMENT_SOD_SETTINGS_VERSION_CONFLICT` (409 — SoD ayarında bayat `If-Match`, KARAR D56/4), `PAYMENT_BANK_DISPATCH_NOT_CONFIGURED`
  (sonuç kodu, serbest bırakma yanıtında), `PAYMENT_RECORD_CODE_UNAVAILABLE` (503), `PAYMENT_IDEMPOTENCY_KEY_REQUIRED`
  (400), `PAYMENT_DECISION_PENDING` (409 — açık karar niyeti), `PAYMENT_SOD_WAIVER_NOT_ALLOWED` (403 — R1–R3 muaf tutulamaz),
  `PAYMENT_REQUEST_IN_PROGRESS` (202 — aynı anahtarlı istek, makbuz `Pending`; kurtarma yapılmaz),
  `PAYMENT_RECONCILE_RESTART_ACTOR_MISMATCH` (409 — Reconcile'ı çağıran asıl komutun aktörü değil; uzak yazma/başlatma
  çağrısı yapılmaz, niyet `Unknown` kalır), `PAYMENT_BANK_DISPATCH_CONTRACT_VIOLATION` (500 — porttan
  `BankDispatchContractException` ya da sözleşme dışı istisna; işlem geri alınır), `PAYMENT_BANK_DISPATCH_OUTCOME_UNSUPPORTED`
  (502 — `NotConfigured` dışı sonuç; deneme kaydedilir, parti kilitlenir, D17/2), `PAYMENT_RELEASE_BLOCKED_FOR_REVIEW` (409 —
  kilitli partide Release; port çağrılmaz), `PAYMENT_METHOD_NOT_ALLOWED` (422 — ödeme yöntemi MOD-0048 kümesinde yok ya da pasif), `PAYMENT_METHOD_SOURCE_UNAVAILABLE`
  (503 — küme okunamıyor), `PAYMENT_RELEASE_SEQUENCE_EXHAUSTED` (409 — `ReleaseSequence` üst sınırda;
  artırma ve port çağrısı yok), `PAYMENT_TIER2_RELEASE_FORBIDDEN` (403 — Release'de R11 tier-2 şartı tutmuyor; parti
  `Approved` kalır, `PaymentDispatchAttempt` yazılmaz — KARAR D77/2). MOD-0269 eşlemesi: `docs/analysis/contracts/bank-dispatch.md` (FROZEN 0.1.0) §5.
- **Taşınmaz:** `PAYMENT_AUDIT_DISPATCH_*`, `PAYMENT_AUDIT_CONTRACT_UNAVAILABLE`, `PAYMENT_AUDIT_RESPONSE_INVALID`,
  `PAYMENT_OUTBOX_*` (CentralAudit), `PAYMENT_UNSUPPORTED_P0_OPERATION` (istek modelinde o alanlar yok),
  `PAYMENT_APPROVER_DIRECTORY_*` (O-10: aday tabanı Auth aday sorgusu — KARAR D66/1; dizin istemcisi taşınmaz).
- Kullanıcıya giden ileti resx anahtarıdır; teknik metin (`403`, `Forbidden`) ekrana çıkmaz (UAS-001 §5).

## Repo Scope

Hepsi kod alma izninden ve `ready-for-dev`'den sonra; dilim sütunu "Teslim dilimleri" ile eşleşir.

- `services/Diten.FinanceTreasuryService/**` — **yeni servis**, `@orchestrator /add-module` iskeleti (5 katman:
  Api / Application / Domain / Persistence / Infrastructure + `tests/`), port 5071, JWT, `TenantId` izolasyonu, 4 zorunlu
  pipeline davranışı + `AddCentralAudit<…>(RequireEnlistment = true)`, modül manifest kaydı
  (`module-self-registration-standard`). (DL-1)
  - `src/Diten.FinanceTreasuryService.Application/Features/PaymentBatch/**` (Golden Reference Compact düzeni)
  - `src/Diten.FinanceTreasuryService.Domain/PaymentBatches/**`
  - `src/Diten.FinanceTreasuryService.Persistence/PaymentBatches/**` (depolar, indeks hizmeti; her işlem geri çağrısı
    `CentralAuditTransaction.EnlistAsync` çağırır)
  - `src/Diten.FinanceTreasuryService.Infrastructure/{LegalEntities,Workflow,Evidence,Currency,BankDispatch,RecordCodes,DataScope,SodWaivers,ApproverCandidates}/**`
    (port bağdaştırıcıları; `DataScope` = SEC-002 tüketim bağdaştırıcısı, servis ortak; `SodWaivers` = Auth `waive-sod` ataması
    alanları bağdaştırıcısı (kullanım anı), port `IPaymentSodWaiverAssignmentSource` — KARAR D54, D56/3, DL-2;
    `ApproverCandidates` = Auth aday sorgusu bağdaştırıcısı, servis ortak (aşağıda) — KARAR D66/1, DL-2;
    `Workflow` = servis ortak MOD-0023 bağdaştırıcısı, aşağıda)
  - **Ödeme yöntemi kaynağı (MOD-0126'ya özgü, servis ortak değil; DL-1):** `IPaymentMethodReferencePort`
    `src/Diten.FinanceTreasuryService.Application/Features/PaymentBatch/**` içinde, MOD-0048 `payment-method` bağdaştırıcısı
    `src/Diten.FinanceTreasuryService.Infrastructure/Currency/**` içinde (MOD-0048 bağdaştırıcılarıyla aynı yer; zaman aşımlı
    `HttpClient`). MOD-0119/0120/0124 bu portu kullanmak isterse ayrı karar gerekir.
  - **Servis ortak yapı taşları (D14/1, D14/2) — tek sahibi ve tek yazarı bu paketin DL-1'idir (gap 4/G5):**
    servis iskeleti; `src/Diten.FinanceTreasuryService.Application/Common/{CommandReceipts,DataScope}/**`
    (`FinanceTreasuryCommandReceipt`, `IFinanceTreasuryDataScopeSource` — okuma ve yazma kapsamı, `FinanceTreasuryDataScope`)
    ve `src/Diten.FinanceTreasuryService.Persistence/Common/CommandReceipts/**` (koleksiyon + iki makbuz indeksi
    `ux_finance_treasury_command_receipt_scope`, `ix_finance_treasury_command_receipt_pending`); servis ortak portlar ve
    bağdaştırıcıları `ILegalEntityEligibilityPort` (`Infrastructure/LegalEntities`), `ICurrencyReferencePort`
    (`Infrastructure/Currency`), `IRecordCodeAllocator` (port `src/Diten.FinanceTreasuryService.Application/Common/RecordCodes/**`,
    v0 HTTP bağdaştırıcısı `src/Diten.FinanceTreasuryService.Infrastructure/RecordCodes/**` — KARAR D59/1; üretim kompozisyonunda
    bağdaştırıcı kayıtlı değilse port "bağlı değil" sonucu verir ve Create kapanır; sahte allocator yalnız test projesinde),
    `DataScope` bağdaştırıcısı; CentralAudit
    kaydı (`AddCentralAudit<FinanceTreasuryCentralAuditScope>(RequireEnlistment = true)`, `FinanceTreasuryCentralAuditScope`,
    servis defteri). MOD-0119, MOD-0120 ve MOD-0124 bunlara **bağımlıdır** (MOD-0126 DL-1, onların DL-1'inin önkoşuludur)
    ve bunları **hiçbir zaman yeniden tanımlamaz ya da açmaz** — başka bir paket önce `ready-for-dev` olsa bile MOD-0126
    DL-1'i bekler. Değişiklik dört paketin birlikte onayını ister.
  - **MOD-0023 iş akışı bağdaştırıcısı servis ortak yapı taşıdır (KARAR D57/5; DL-2 hazırlık bulgusu, MOD-0120 cümlesiyle hizalandı):**
    `src/Diten.FinanceTreasuryService.Infrastructure/Workflow/**` — MOD-0023 HTTP istemcisi (zaman aşımı
    `FinanceTreasury:Workflow:TimeoutSeconds`, başlatma / geçiş / `by-objects` / `history` okuması, yanıt sınıflandırması) —
    tek sahibi ve tek yazarı **bu paketin DL-2'sidir**. MOD-0119, MOD-0120 ve MOD-0124 onu **tüketir**; modüle özgü portlarını
    (ör. MOD-0119 `IApWorkflowPort`) bu ortak istemci üzerinde, kendi klasörlerinde uygular (`Infrastructure/Workflow/**`'a yazmaz)
    ve ikinci bir MOD-0023 istemcisi açmaz; MOD-0023 kullanan dilimleri — **MOD-0119 DL-2, MOD-0120 DL-3, MOD-0124 DL-3** —
    MOD-0126 DL-2'yi bekler (KARAR D57/5). Değişiklik dört paketin birlikte onayını ister (D14/2 deseni).
  - **Onaylayıcı aday kaynağı servis ortak yapı taşıdır (KARAR D66/1; **KARAR D67/3** — servis ortak port, tek yazarı MOD-0126 DL-2, sahip teyit etti; D57/5 deseni):** port
    `src/Diten.FinanceTreasuryService.Application/Common/ApproverCandidates/**` (`IFinanceTreasuryApproverCandidateSource`;
    girdi izin anahtarı + isteğe bağlı parti, çıktı kullanıcı kimlikleri), bağdaştırıcı
    `src/Diten.FinanceTreasuryService.Infrastructure/ApproverCandidates/**` — MOD-0018 Treasury Auth eki DL-6 iç ucu
    (`execution/domains/platform-shared-services/module-packs/MOD-0018-FU27-treasury-permission-onboarding.md` Bölüm 8.6; kimlik **KARAR D74/1** — `X-Internal-Api-Key` ortak sırrı + çağrıyı başlatan kullanıcının token'ı `Authorization` ile iletilir, kiracı Auth'ta token'dan okunur, servis kimliği kullanılmaz (o paketin Bölüm 8.7'si); zaman aşımlı `HttpClient`; her hata "okunamaz"; yalnız kullanıcının başlattığı Submit'te çağrılır — **KARAR D75/2 (b)**). Tek sahibi ve tek yazarı
    **bu paketin DL-2'sidir**; MOD-0119 DL-2, MOD-0120 DL-3 ve MOD-0124 DL-3 onu **tüketir** (kendi kurallarını kendi gönderme
    çözümleyicisinde uygular) ve ikinci bir Auth aday istemcisi açmaz. Değişiklik dört paketin birlikte onayını ister.
  - `src/Diten.FinanceTreasuryService.Api/Controllers/PaymentBatchesController.cs`, `PaymentExceptionsController.cs`
  - `src/Diten.FinanceTreasuryService.Api/CentralAudit/FinanceTreasuryCentralAuditScope.cs`
- `tests/architecture/audit-ledger/Diten.FinanceTreasuryService.md` — yeni defter (`## İzler` satırı
  `finance-treasury-merkezi-iletim | b | işaret | ICentralAuditDeclaration`, borç 0, istisna 0) + aynı değişiklikte
  `tests/architecture/audit-ledger/CT-DECISIONS.md` satırı ve `AuditTrailStandardTests` `AcceptedTrails` /
  `PinnedCounts` (SupplyChain E-2 deseni, CT-DECISIONS 2026-10-21) — **CT kararı verildi:**
  `treasury-intake-owner-decisions-02.md` D35 (PR #235). D35/1–2: iz `finance-treasury-merkezi-iletim | b | işaret |
  ICentralAuditDeclaration`; `## Bilinen borç`, `## K2 borcu`, `## İstisnalar`, `## Dolaylı`, `## Yazan sorgular` boş,
  `PinnedCounts` sıfır (sonradan satır eklemek ayrı CT kararıdır). D35/4: defter dosyası, `CT-DECISIONS.md` satırı ("kararı
  veren": Natig, 2026-10-26 — Treasury CT önerisi, D35) ve `AuditTrailStandardTests` DL-1 değişikliğinde **birlikte** yazılır. (DL-1)
- `docs/analysis/contracts/payment-batch.openapi.yaml` — yeni üretici sözleşmesi (x-owner MOD-0126). (DL-1)
- `frontend/Diten.Web/Views/FinanceTreasury/PaymentBatches/**`,
  `frontend/Diten.Web/wwwroot/assets/js/FinanceTreasury/PaymentBatches/**`,
  `frontend/Diten.Web/Resources/Views/FinanceTreasury/PaymentBatches/PaymentBatchesIndex.{en,tr,fr,es,zh,ar,ru}.resx`,
  `frontend/Diten.Web/Controllers/FinanceTreasury/PaymentBatchesController.cs` (same-origin MVC proxy),
  `PageHelpResource.{7 dil}.resx` içinde `Help.PAYMENTBATCHES.*` ve `Services/Help/PageHelpCatalog.cs` satırı,
  menü/nav anahtarları `Nav.Domain.FINANCETREASURY`, `Nav.Module.PAYMENTSAPPROVALS`, `Nav.Page.PAYMENTBATCHES`. (DL-4)
- SoD ayar ekranı (**KARAR D57/4**): `frontend/Diten.Web/Views/FinanceTreasury/PaymentSodSettings/**`,
  `frontend/Diten.Web/wwwroot/assets/js/FinanceTreasury/PaymentSodSettings/**`,
  `frontend/Diten.Web/Resources/Views/FinanceTreasury/PaymentSodSettings/PaymentSodSettingsIndex.{en,tr,fr,es,zh,ar,ru}.resx`,
  `frontend/Diten.Web/Controllers/FinanceTreasury/PaymentSodSettingsController.cs` (same-origin MVC proxy),
  `Help.PAYMENTSODSETTINGS.*` ve `PageHelpCatalog.cs` satırı, `Nav.Page.PAYMENTSODSETTINGS`. (DL-4)

**Bu paketin kapsamında OLMAYAN ama bağımlı değişiklikler** (her biri kendi sahibinin onayıyla, D9; burada yazılmaz):
MOD-0220 TRM-14; MOD-0023 iş akışı şablonu `FT-PAYMENT-BATCH-APPROVAL` (Y-12; kurulumu MOD-0023 sahibinin WP'si); `ocelot.json` rotaları (integration-agent); Platform `AuditCategory`
eki `Finance` (değer 18; Y-4/7) (O-6, KARAR D35/3 — Platform sahibinin işi, DL-1 önkoşulu); FinanceTreasury servisinden SEC-002 veri kapsamı
çözücüsünün tüketim yolu (platform-shared-services, O-19; yol Y-4/6 ile yanıtlandı); MOD-0018 Treasury Auth eki
(`execution/domains/platform-shared-services/module-packs/MOD-0018-FU27-treasury-permission-onboarding.md`, FU27 —
K-1 kapandı, Y-19 / D80/1; Treasury yazar, MOD-0018 sahibi teyit eder — Y-15, Y-16): anahtarlar, açık atama listesi ve rol şablonları o
paketin DL-1'i, `waive-sod` atama kaydı / yazma ucu / "bu parti için geçerli atamalar" sorgusu ve denetimi DL-5'i (KARAR
D54, D56/3, D64), onaylayıcı aday sorgusu DL-6'sı (KARAR D66/1). AGENTS.md §2/§3/§9, `.antigravity/rules/ports.md` (5071) ve
`record-coding-standard.md` §3 `PAY` satırı **uygulandı** (D30, 2026-10-25) — bekleyen iş değildir. Bkz. "Dependencies".

## Protected Paths

- `.antigravity/**` (port bandı, RCS-001 §3 satırı ve kural değişiklikleri ayrı onay)
- `gateway/Diten.ApiGateway/**/ocelot.json` — yalnız `integration-agent`
- `services/Diten.Platform/**` (MOD-0023, MOD-0021, MOD-0048, MOD-0031, `IDataScopeResolver` — platform-shared-services), `services/Diten.MdmService/**`
  (MOD-0220, MOD-0289 — master-data-management), `services/Diten.ProcurementService/**` (MOD-0140; tek yazar MVP-2, K15),
  `services/Diten.AuthService/**`, `services/Diten.Building.Blocks/**` (CentralAudit yalnız tüketilir) ve diğer domain servisleri
- `frontend/Diten.Web/Views/Archive/**`, `Controllers/Archive/**`, `Views/Shared/_Layout.cshtml`, `_LayoutBackbone` (kaynak
  layout'u; Recovery'de kullanılmaz)
- `docs/analysis/contracts/supplier.openapi.yaml` ve diğer üretici sözleşmeleri (tüketilir, yeniden tanımlanmaz)
- `execution/registries/**`, `execution/domains/finance-treasury/domain-config.md`, CT kuyruğu — bu paketle değişmez
- Kaynak depo `/Users/natig/Projects/ERP-vNext` ve handoff klasörü — yalnız okunur; kaynak düzeltmesi yapılmaz

## Dependencies

| Kapı / bağımlılık | Rol | Recovery durumu (ölçüm Recovery @4e01e986269065cba6f962e0c13b9b2ae4519f6c) | Bu pakete etkisi |
|---|---|---|---|
| MOD-0023 Workflow Designer | başlatma + görev geçişleri + başlatanın onayını reddetme | **var.** `POST /api/v1/workflow/instances` (`WorkflowDefinitionsController.cs:87-93`, izin `platform.workflow.instances.start`, `WorkflowModels.cs:14`); istekte `IdempotencyKey` var (`WorkflowModels.cs:286-299`) ve aynı anahtar yeniden oynatılır (`StartWorkflowInstanceHandler.cs:76-85`); görev onay/ret uçları `tasks/{taskId}/approve|reject` (`WorkflowDefinitionsController.cs:196-243`); başlatanın onayı `SodViolation` ile reddedilir, kimlik karşılaştırması Guid (`WorkflowTaskTransitionSupport.cs:964-977`); uzlaştırma okuması `GET instances/by-objects` (`:121-132`) | v1 başlatma **kullanıcı token'ıyla genel uçtan** (O-7). **Şablon kodu `FT-PAYMENT-BATCH-APPROVAL`** (Y-12; MOD-0023 sahip kararları W-1, W-6 — `docs/records/decisions/2026-10/mod0023-workflow-template-owner-decisions-01.md`, origin/main @ `28c68e7a6`, #277): tek aşama, tek onay adımı, atama yok; onaylayıcıları başlatma isteğinin `CandidatePrincipalIds` alanı belirler (`StartWorkflowInstanceRequest`, `WorkflowModels.cs:286-299@8a737a8e9`) — bu paketin R1–R4 aday çözümleyicisi; boş liste 400 `WORKFLOW_ASSIGNMENT_CANDIDATES_REQUIRED`, hiçbir şey yazılmaz (`StartWorkflowInstanceHandler.cs:154-158@8a737a8e9`). Şablonun MOD-0023'te kurulması (Platform şablon kataloğu + `--ensure-workflow-templates`) MOD-0023 sahibinin ayrı WP'sidir — kod main'de **yok** (`git grep FT-PAYMENT-BATCH-APPROVAL origin/main -- services` → 0, 2026-10-28), WP numarası ve tarih gelmedi (D9) |
| Güvenilir iş akışı tüketicisi (`api/internal/v1/workflow/trusted-consumer/*`) | servis kimliğiyle başlatma, `start-result` | **var ama yalnız `Diten.MDM`'ye kilitli** — `TrustedServiceTokenValidationExtensions.cs:18`, `ConfiguredTrustedWorkflowStartAuthorizationPolicy.cs:79`, `ServiceIdentityTokenAudiencePolicy.cs:5,16` (Paket A taslağı, `docs/roadmap/plans/erp-finance-legal-module-intake-01/PKG-A-MOD-0033-FU02-TREASURY-WORKFLOW-IDENTITY-DRAFT-v0.1.md`) | v1'de kullanılmaz; arka plan uzlaştırma süpürücüsü bu kimlik gelene kadar yok (follow-up) |
| MOD-0021 Audit / CentralAudit (BL-501, K4) | yol `b` | **var** — `services/Diten.Building.Blocks/src/Diten.BuildingBlocks.CentralAudit/` (`CentralAuditBehavior.cs`, işlem içi `CentralAuditTransaction.EnlistAsync`, `CentralAuditRecorder.cs:6-31`); kullananlar Inventory, SupplyChain | yeni serviste `AddCentralAudit` + defter + CT kararı (verildi — D35, PR #235; Repo Scope). Servis-başı MDM kopyası **yasak** (AUD-001 §5) |
| MOD-0220 LE `lookup-validation` | ödeyen şirket uygunluğu (D10/1) | **var** — `GET /api/legal-entities/{id}/lookup-validation`, izin `mdm.legal-entities.read` (`LegalEntitiesController.cs:48-54`); kayıtta `OperationalStatus` ve `ApprovalStatus` var (`LegalEntity.cs:49-50`); yanıtta `ApprovalStatus` **henüz yok** | **TRM-14** (D10/2): MOD-0220 pack değişikliği + eklemeli `ApprovalStatus` alanı + MOD-0220 sözleşme belgesi — MOD-0126 kapsamı değildir, eşlik eden değişikliktir; o gelmeden "onaylı" kontrolü yapılamaz → ödeme kapanır (fail-closed) |
| MOD-0269 Banks/SWIFT/Payments Gateway | serbest bırakma portu | **yok** (0 dosya; karar 0.1 = A, D5) — sözleşme **dondu**: `docs/analysis/contracts/bank-dispatch.md` (BANK-DISPATCH FROZEN 0.1.0, `rail` yok — KARAR D27/7; TRM-06 kapandı) | `IBankDispatchPort` dondurulmuş sözleşmeye göre yazılır; bağdaştırıcı yalnız `NotConfigured` döner |
| MOD-0289 Bank Master & Bank Account Registry | banka referansı çözümleme | kod yok; yerleşim MDM (D20/1), TRM-S3; okuma sözleşmesi **taslak**: `docs/analysis/contracts/drafts/bank-account-lookup-validation-v1.0-proposal.md` (BANK-ACCOUNT-LOOKUP 1.0.0 DRAFT, fikstürler F-E1…F-E9, F-503) | bu dilimde yalnız boş olmayan opak Guid; çözümleme dağıtım açılınca, sözleşme dondurulunca (follow-up) |
| MOD-0048 Reference Data (para birimi) | kod listesi + küsurat hanesi | **kısmen** — `GET /api/lookups/reference/currencies` (`TenantReferenceLookupsController.cs:18,37-41`), `LookupOptionDto` küsurat hanesi taşımıyor (`LookupModels.cs:3-9`; liste `CultureInfo`'dan, `PlatformLookupProvider.cs:358`) | küsurat hanesi kaynağı MOD-0048-FU01 §21 `currency-minor-units` — başlangıç listesi kabul (Y-4/3), seed henüz yok (O-11); yoksa oluşturma `PAYMENT_CURRENCY_PRECISION_UNAVAILABLE` ile durur — sabit tablo uydurulmaz |
| MOD-0048 Reference Data (`payment-method` kümesi, D28 / O-13) | ödeme yöntemi kod listesi | **uç var, küme yok** — `GET /api/lookups/reference-data/consumable-sets/{setCode}/published-values` (`services/Diten.Platform/src/Diten.Platform.API/Controllers/ConsumableReferenceDataController.cs:25, 43-44@b9df4e281`; öğe `Code`, `Label`, `IsActive`, `BusinessReferenceDataStewardshipModels.cs:159-165`); izin listesi `BusinessReferenceData:ConsumableSets` (`services/Diten.Platform/src/Diten.Platform.API/appsettings.json:104`) `payment-method` içermiyor (ölçüldü @b9df4e281) | MOD-0048 sahibi kümeyi tanımlar, yayımlar ve izin listesine ekler (**açık üretici bağımlılığı**); gelene kadar Create 503 `PAYMENT_METHOD_SOURCE_UNAVAILABLE` — sabit liste uydurulmaz. Tüketici: `IPaymentMethodReferencePort` + `Infrastructure/Currency/` bağdaştırıcısı (Owned Objects → Portlar) **Y-4/3 (2026-10-27, Platform):** `payment-method` önerisi başlangıç listesi olarak kabul (Finance sonradan değiştirebilir); seed JSON ve `ConsumableSets` izin listesi Platform'da, iş başlayınca haber verilecek — seed main'e girene kadar önkoşul açık |
| MOD-0031 Evidence Linking | kanıt bağlantısı | **var** — `api/v1/evidence` (`EvidenceLinksController.cs:19`) | DL-3; bağlama sözleşmesinin idempotency (dedupe) davranışı DL-3 başında ölçülür; ölçülene kadar hiçbir kurtarma yolu MOD-0031'i yeniden çağırmaz (gap 5/G2) |
| MOD-0018 RBAC | `[HasPermission]`, izin tohumu | var | **Auth paket metni: MOD-0018 Treasury Auth eki** — `execution/domains/platform-shared-services/module-packs/MOD-0018-FU27-treasury-permission-onboarding.md` (`draft`; FU27 — K-1 kapandı, Y-19 / D80/1; Treasury yazar, MOD-0018 sahibi inceler ve teyit eder — Y-15, Y-16; 2026-10-28 ölçümünde main'de değil). O paketin **DL-1**'i: yeni `finance-treasury.*` anahtarları (O-5), rol şablonları, `read-tenant-wide` ve `waive-sod` yalnız açık atama (`ExplicitGrantOnlyPermissions`), SoD ayarı anahtarları (D56/4, D57/4). Paket kapısı (D53/1: metin main'de) ve DL-1 kabul kapısı (D53/2: kodu main'de) değişmedi. **`waive-sod` parti bağı — o paketin DL-5'i (KARAR D64; D63/3'ün yerine):** atama kaydı (hedef parti, başlangıç / bitiş, onaylayan, gerekçe kodu — KARAR D54, D56/3), yazma ucu, "bu parti için geçerli atamalar" sorgu ucu (muafiyet aday listesi D66/1 ile DL-6 aday sorgusunun `waive-sod` + parti biçimi — D56/3) ve denetim; DL-5'in paket kapısı D64'ün main'e girmesini bekler (D62/1). **DL-5 kodu main'e girene kadar muafiyet yolu kapalıdır:** her muafiyetli onay 403 `PAYMENT_SOD_WAIVER_FORBIDDEN` (kapalı-başarısız; yazım ve MOD-0023 çağrısı yok — D54 değişmedi); aday sorgusu okunamadığı için muafiyet adayı eklenmez, tier-2 adayı kalmayan partinin gönderimi kapalı-başarısızdır (AC-42); DL-2 bunu beklemez (**KARAR D66/3**). **Onaylayıcı aday tabanı — o paketin DL-6'sı (KARAR D66/1):** "bu izne sahip kullanıcılar" iç ucu (`X-Internal-Api-Key` + iletilen kullanıcı token'ı, servis kimliği yok — **KARAR D74/1**; kiracı token'dan, yalnız etkin kullanıcı, açık atama anahtarında yalnız açık atama; şirket kapsamı o paketin K-A2'si); servis R1–R4'ü uygular, sonucu `CandidatePrincipalIds` olarak gönderir; DL-6 paket metni DL-2 önkoşulu, kodu DL-2 kabul kapısı; MOD-0018 sahibinin teyidi gerekir (D66/1) |
| **SEC-002 veri kapsamı** (`IDataScopeResolver`, MOD-0018-FU15) | kullanıcının şirket yetkisi (D14/1) | çözücü yalnız Platform'da kayıtlı (`Diten.Platform.Application/DependencyInjection.cs` → `OrgDataScopeResolver`); ortak kütüphanedeki `NoOpDataScopeResolver` boş liste döner (`.antigravity/rules/data-scope-enforcement.md` §6); FinanceTreasury'den tüketim yolu **yok** | **ENGEL (O-19)**: SEC-002 §4 kapısı — kapsam cümlesi + tüketim yolu tasarımı + Auth'ta `read-tenant-wide` tanımı olmadan `ready-for-dev` yok; kapsamı soran kod + kanıt testi (AC-32) DL-1 kabul kapısıdır (D51/2). Tüketim yolu servis ortaktır (`IFinanceTreasuryDataScopeSource`); dört Treasury paketi için tek karar (MOD-0120 O-14 ile aynı). `NoOp` kaydı yasak (kapsamı kapatır) — **Yol yanıtlandı (Platform sahibi yanıtı Y-4/6, 2026-10-27):** servis içi Application portu + Platform `IDataScopeResolver` bağdaştırıcısı, fail-closed; MDM `ITrustedLegalEntityScopeProvider` doğrudan kullanılmaz; kapsamı soran kod + kanıt testi (AC-32) açık — DL-1 kabul kapısında (D51/2). **Auth tarafı (Y-16; D53 / D56 aynen):** `read-tenant-wide`'ın yalnız açık atamalı izin tanımı MOD-0018 Treasury Auth ekinin DL-1'indedir (`…/MOD-0018-FU27-treasury-permission-onboarding.md` Bölüm 14.1; FU27 — K-1 kapandı, Y-19 / D80/1). O ekin SEC-002 dilimi (DL-4, D56/1) yalnız MDM istemcisini kapsar; FinanceTreasury'nin Platform kapsam ucuna bağlanmasının o eke girip girmediği ekin açık kararı **K-6**'dır (CT önerisi: girmez, O-19'un işi) |
| RCS-001 ortak kod servisi (§5.12, BL-565) | parti kodu | **yok** (Building.Blocks'ta yok; MDM'de yalnız kendi `CodeReservation`'ı, `CodeReservation.cs:5`); şirket kısa kodu (§5.4) LE kaydında ayrı alan değil (`LegalEntity.cs:11` yalnız `Code`) | **DL-1 kabul kapısı (KARAR D59/1; paket kapısı değil):** modül kendi sayacını ya da yedek kodu yazamaz (RCS-001 §6). DL-1 kodu `IRecordCodeAllocator.AllocateAsync(legalEntityId, recordType, idempotencyKey)` portuna karşı sahte uygulamayla yazılır; servis Beste'den 6 Kasım'a kadar (Y-8); gerçek servis DL-1 kabulünden önce main'de olmalıdır. Üretimde port bağlı değilse Create 503, kodsuz kayıt yok (O-9) |
| MOD-0140 Supplier (PayeeType = Supplier) | alacaklı doğrulaması | var — `POST /api/suppliers/validate` (MOD-0140 pack §3) | varlık doğrulaması O-14 (bu dilimde yalnız biçim) |
| Port 5071 | servis portu | sahip kararı D10/3; **AGENTS.md §3 ve `.antigravity/rules/ports.md` güncellendi** (D30: AGENTS §2/§3/§9 ve `ports.md` 5071 uygulandı, 2026-10-25) | iskelet port kaydından sonra |
| DCP (DCP-011-finance-treasury-intake) | dilim kapısı | `draft` | DCP `approved` olmadan pack `ready-for-dev` olmaz (DCP §15) |

### Bağımlılık sözleşmeleri — kapanış tablosu (gap P2, 2026-10-24)

Her satır kapanmadan ilgili dilim başlamaz. **Sahte (mock) istemciyle geçen test, canlı entegrasyon kanıtı sayılmaz:**
"sözleşme testi" sütunu sahte istemcili testi, "canlı kanıt" sütunu gerçek servisle dev ortamında koşulmuş kaydı
(`scripts/run_record.py`) ister.

| Bağımlılık | Sözleşme dosyası / sürüm | Hash | Sahibi | Hata davranışı (MOD-0126) | Kapanış kanıtı (sözleşme testi · canlı kanıt) | Dilim |
|---|---|---|---|---|---|---|
| PAYMENT-BATCH (bu modül) | `docs/analysis/contracts/payment-batch.openapi.yaml` v1.0.0 — **yazılacak** | TBD (dondurulunca) | finance-treasury | — | sözleşme testi AC-29 · — | DL-1 |
| MOD-0220 LE uygunluğu + `ApprovalStatus` | MOD-0220 sözleşme belgesi — **yazılacak** (TRM-14) | TBD | master-data-management (D10/2) | 422 uygun değil · 503 erişilemez · geçiş yok | sahte LE istemcisi · dev ortamında Active+Approved / Approved-değil iki kayıt | DL-1 |
| MOD-0023 iş akışı başlatma + görev geçişi | Platform `WorkflowModels.cs` (sözleşme dosyası yok) + şablon kodu **`FT-PAYMENT-BATCH-APPROVAL`** (Y-12, O-8 — tek aşama, tek onay adımı, atamasız; adaylar `CandidatePrincipalIds` ile, boş liste 400 `WORKFLOW_ASSIGNMENT_CANDIDATES_REQUIRED` — Treasury boş liste göndermez, D68; şablonu kuran kod main'de değil); anahtarla idempotentlik ölçüldü (tekil indeksler + yeniden oynatma, Runtime Constraints 3a) | Platform commit SHA'sı DL-2 başında yazılır | platform-shared-services | belirsiz → 202 Unknown · kesin ret → 502 · zaman aşımı yapılandırılır · yinelenen anahtar hatası → Unknown (ret değil) | sahte MOD-0023 (anahtar başına tek örnek / geçiş) · dev ortamında gerçek başlatma + onay + ret + **aynı anahtarlı yarış yanıtı ölçümü** (var olan örneği / geçişi döndürdüğü doğrulanmadan yinelenen anahtar hatası `Unknown`) | DL-2 |
| MOD-0023 saklama + silmeme politikası (örnek ve geçiş kaydı; gap 6/P1-a) | **KARAR D21/1–2** — MOD-0023 pack §17 (TRM-15) önerisi §17.3/1: `WorkflowInstance` ve `WorkflowTransitionLog` hiç silinmez, TTL yok; silinmiş kayıtlar da okunur — `isDeleted` + `includeDeleted=true` (D25/2, D21/2'yi genişletir); silinmiş görülürse yeniden yazma yok; tek silme işareti `IsDeleted`, miras silme üç repository'de kapatılır (§17.8, D32/2; AC-W15) | §17 rev 2 sha256 `803fbaa5…` (PR #235); onay bu sürüme | platform-shared-services (politika: sahip K1, D21) | §17 rev 2 onaylanıp AC-W1 ve AC-W15 geçene kadar 404 / boş okumada anahtarlı yeniden yazma **kapalı**; niyet `Unknown` | ölçüm (@ef4f3a8bc): silme yolu ve TTL yok — ölçüm politika değildir · MOD-0023 AC-W1 (mimari test, sabotajlı) + bu pack'in silinmiş kayıt testi | DL-2 |
| MOD-0023 okuma eşleme alanları (gap 6/P1-b) | **KARAR D21/3** — MOD-0023 pack §17 (TRM-15) önerisi §17.3/3: `by-objects` ve `instances/{id}` → `IdempotencyKey`; `history` → `ApprovalTaskId` + `IdempotencyKey` (yalnız ekleme) | §17 rev 2 sha256 `803fbaa5…` (PR #235); onay bu sürüme | platform-shared-services | alan kodda yoksa okuma niyeti kapatmaz → `Unknown` | MOD-0023 AC-W2/W3 + bu pack'in eşleme testi | DL-2 |
| MOD-0269 banka arayüzü | `docs/analysis/contracts/bank-dispatch.md` — BANK-DISPATCH **FROZEN 0.1.0** (KARAR D27/7; TRM-06 kapandı; D5) | dosya özeti DL-3 başında `shasum` ile yazılır (bu revizyonda ölçülmedi) | bankacılık sahibi 0.1 için K1 (KARAR D27/8); gerçek sağlayıcı seçilince ayrı sahip | her çağrı `NotConfigured`, ağ çağrısı yok | bağdaştırıcı birim testi · ağ çağrısı olmadığını gösteren test | DL-3 |
| MOD-0048 para birimi + küsurat | `GET /api/lookups/reference/currencies` + küsurat alanı — **yok**; `currency-minor-units` (MOD-0048-FU01 §21) başlangıç listesi kabul (Y-4/3), seed main'de bekleniyor (O-11) | TBD | platform-shared-services | hane yok → 503 `PAYMENT_CURRENCY_PRECISION_UNAVAILABLE` | sahte istemci · dev ortamında TRY/EUR/JPY | DL-1 |
| RCS-001 ortak kod servisi | **v0 çağrı sözleşmesi (KARAR D59/1; Beste, Y-8 — 6 Kasım'da kesinleşir, değişiklik yalnız ekleme):** `POST /api/internal/record-codes/allocate`, gövde `{legalEntityId, recordType, idempotencyKey}` (kiracı güvenilir bağlamdan), 200 `{code, sequence}` (aynı anahtar aynı kod), 422 `RECORD_TYPE_UNKNOWN`, 409 `LEGAL_ENTITY_SHORT_CODE_MISSING`, 404 başka kiracı, 503 servis yok; servis kodu **yok** (BL-565, O-9) | TBD (6 Kasım'da kesinleşince) | servis sahibi **Beste — KARAR D44** (2026-10-26; `treasury-intake-owner-decisions-04.md`); önek CT + `.antigravity` onayı (D29/2, D30) | 200 dışı her yanıt, zaman aşımı ya da port bağlı değil → Create 503 `PAYMENT_RECORD_CODE_UNAVAILABLE`, parti yazılmaz; yedek kod yok | sahte allocator (AC-45, sabotajlı) · dev ortamında gerçek servisle `PAY` kodu | DL-1 kabul kapısı (D59/1) |
| SEC-002 veri kapsamı (D14/1) | Platform `IDataScopeResolver` tüketim yolu — kodda **yok**; yol Platform sahibi yanıtı Y-4/6 (servis içi port + Platform çözücü bağdaştırıcısı, fail-closed) (O-19; dört paket için tek karar) | TBD | platform-shared-services | çözücü hatası → boş kapsam, boş liste, detay / yazma 404 | kapsam testi AC-32 (sabotajlı) · dev ortamında iki şirket iki kullanıcı | DL-1 |
| MOD-0018 Treasury Auth eki — `waive-sod` parti bağlı atama (KARAR D54, D56/3, **D64**) | `execution/domains/platform-shared-services/module-packs/MOD-0018-FU27-treasury-permission-onboarding.md` **DL-5** (`draft`; FU27 — K-1 kapandı, Y-19 / D80/1): atama kaydı, yazma ucu, "bu parti için geçerli atamalar" sorgusu (yanıt alanları o paketin Bölüm 8.5'i), denetim; sorgu yanıtının sürümlü sözleşme dosyası o paketin K-W6 kararı — **yazılacak** | TBD (dondurulunca) | Treasury yazar, MOD-0018 sahibi (Ali) inceler ve teyit eder (D64; Y-17); DL-5 paket kapısı D64'ün main'e girmesi (D62/1; D64 main'de, PR #276) | DL-5 kodu main'de değil, ya da yanıt yok / okunamıyor / alan eksik / hedef parti yok ya da eşleşmiyor → muafiyetli onay 403 `PAYMENT_SOD_WAIVER_FORBIDDEN`, yazım ve MOD-0023 çağrısı yok (D54); aday sorgusu okunamıyor → aday eklenmez, gönderme kapalı-başarısız (AC-42) | sahte Auth istemcisi (AC-42, AC-43, sabotajlı) · dev ortamında gerçek DL-5 ucuyla A partisine bağlı geçerli atama (A'da geçer, B'de 403) | DL-2 (paket metni); kodu DL-2 kabulünü beklemez — muafiyet açılış kabulü (**KARAR D66/3**; D56/3'teki kabul kapısı kuralının yerine) |
| Onaylayıcı aday tabanı — Auth aday sorgusu (**KARAR D66/1**) | `execution/domains/platform-shared-services/module-packs/MOD-0018-FU27-treasury-permission-onboarding.md` **DL-6** (Bölüm 8.6; `draft`; FU27 — K-1 kapandı, Y-19 / D80/1): iç uç `GET /internal/permission-candidates` (öneri, K-A1), `X-Internal-Api-Key` ortak sırrı + iletilen kullanıcı token'ı, kiracı token'dan, servis kimliği kullanılmaz (**KARAR D74/1**); yalnız kullanıcının başlattığı gönderimde çağrılır (**KARAR D75/2 (b)**); sürümlü sözleşme dosyası **yazılacak** (K-A1); anahtar başına ayrı çağrı — `approve`, tier-2 partide `tier2-approve`, her partide `platform.workflow.tasks.approve` (**KARAR D71**), servis kesişimi alır | TBD (dondurulunca) | Treasury yazar, MOD-0018 sahibi teyit eder (D66/1) | herhangi bir anahtar çağrısında yanıt yok / okunamıyor / zaman aşımı / 401 / 403 / bozuk gövde → gönderme kapalı-başarısız: niyet yazılmaz, MOD-0023 çağrılmaz; kesişim (iş akışı izni dahil, D71), R1–R4 ve şirket kapsamı sonrası boş liste yerelde 422 `PAYMENT_WORKFLOW_RESOLVER_NO_ELIGIBLE_APPROVER` — MOD-0023 çağrılmaz, niyet yazılmaz, parti değişmez (**KARAR D68**; MOD-0023'ün 400'ü yalnız savunma hattı → 502); Auth'un hiçbir hatası "aday yok → kural atla" anlamına gelmez | sahte Auth aday istemcisi (AC-11, sabotajlı) · dev ortamında gerçek DL-6 ucuyla iki kullanıcı (izinli etkin / izinli pasif) | DL-2 (kodu DL-2 kabul kapısı) |
| MOD-0031 kanıt bağlama | `api/v1/evidence` (sözleşme dosyası ölçülecek) | DL-3 başında | platform-shared-services | 503 → bağlama yok, parti değişmez · belirsiz → yeniden çağrı yok (yalnız okuma) | sahte · dev ortamı · **MOD-0031 dedupe ölçümü** (aynı uzak anahtarın tekrarı tek bağlantı mı) — ölçülmeden kurtarmada yeniden çağrı açılmaz | DL-3 |
| CentralAudit | `Diten.BuildingBlocks.CentralAudit` (kod) | Building.Blocks commit SHA'sı DL-1 başında | BL-501 | yazılamazsa komut başarısız (K2) | sabotaj testi AC-17 · AUD-001 §8 dev kanıtı AC-30 | DL-1 |

## Runtime Constraints

- Servis portu **5071** (D10/3; AGENTS §3 ve `ports.md` kaydı yapıldı — D30). Tarayıcı ve `Diten.Web` yalnız Gateway (5000) üzerinden
  çağırır; tarayıcı JS 5071'e gitmez.
- **Kiracı + şirket izolasyonu her sorguda:** `TenantId` `ITenantContext`'ten, `LegalEntityId` `X-Legal-Entity-Id`
  başlığından (MOD-0220'de doğrulanmış); başka kiracı ya da başka şirket kaydı **404**. Üretimde sabit/varsayılan kiracı
  yok.
- **Veri kapsamı (SEC-002, sahip kararı D14/1) — kapsam cümlesi:** kapsamı taşıyan alan **`LegalEntityId`** (parti ve
  bütün alt kayıtları); çevrilen kapsam türü yalnız **`LegalEntity`** (`OrgUnit`, `Position`, `ManagerChain` çevrilmez →
  daraltır, sızdırmaz; birim→şirket birleştirmesi uydurulmaz); `IsInclude = false` olan `LegalEntity` kapsamı o şirketi
  **kapatır**. Kullanıcı yalnız yetkili olduğu şirketlerin kayıtlarını görür: istek başlığındaki şirket kullanıcının
  kapsamında değilse parti ve istisna listeleri **boş** (200, 0 satır), detay / iz / her yazma **404**
  `PAYMENT_BATCH_NOT_FOUND`; çözücü hatası, kiracı ya da kullanıcı yokluğu → boş kapsam (süzgeçsiz sorgu değil). Kiracıdaki
  tüm şirketleri görmek ayrı izindir: `finance-treasury.payment-batches.read-tenant-wide` (yalnız açık atama —
  `ExplicitGrantOnlyPermissions`; tek üretici `FinanceTreasuryDataScope.TenantWideScope()`). Kapsam servis genelinde
  **tek yerde** (`IFinanceTreasuryDataScopeSource`, MOD-0119/0120/0124 ile ortak; modül yalnız kendi `read-tenant-wide`
  anahtarını verir) çözülür; okumalar (liste, detay, iz, istisna listesi) **okuma kapsamını**, her yazma **yazma
  kapsamını** kullanır (aşağıda); sayfalama ve toplamlar daraltılmış kümeden hesaplanır. Referans uygulama:
  `WorkReportScope` / `WorkReportScopeSource` (SEC-002 §5). Tüketim yolu (Platform sahibi yanıtı Y-4/6): `IFinanceTreasuryDataScopeSource` servisin kendi Application portudur; bağdaştırıcısı Platform `IDataScopeResolver`'ı çağırır; hata, boş liste ya da belirsizlik → erişim yok (fail-closed); MDM'nin `ITrustedLegalEntityScopeProvider`'ı doğrudan kullanılmaz; `read-tenant-wide` Auth'ta yalnız açık atamalı izindir.
- **Okuma kapsamı ile yazma kapsamı ayrıdır (gap 4/G1; D14/1 ile tutarlı — `…read-tenant-wide` şirketleri *görme*
  iznidir, yazma izni değildir):** `IFinanceTreasuryDataScopeSource` iki ayrı sonuç verir: **okuma kapsamı** = kullanıcının
  kendi şirket yetkileri ya da `read-tenant-wide` varsa kiracıdaki tüm şirketler (`TenantWideScope()`); **yazma kapsamı**
  = yalnız kullanıcının kendi şirket yetkileri — `TenantWideScope()` yazma kapsamına **hiçbir zaman** girmez (tek üretici
  okuma tarafında kalır). Her yazma komutu (Create, Update, Delete, BulkDelete, Validate, Submit, Approve,
  ApproveWithSodWaiver, Reject, RequestEvidence, Reconcile, kanıt bağlama, onay belgesi, Release) hedef kaydın
  `LegalEntityId`'si için **katman 1**'i ister: `X-Legal-Entity-Id` hedef partinin şirketine eşit **ve** kullanıcının o
  şirkette kendi yetkisi var. Yalnız `read-tenant-wide` sayesinde görünen bir partiye yazma → **404**
  `PAYMENT_BATCH_NOT_FOUND` (kapsam dışı kuralıyla aynı; varlık sızdırılmaz, başka adım çalışmaz). Bu ayrım servis ortak
  yapı taşının parçasıdır (MOD-0126 DL-1; dört paket aynı kuralı uygular).
- **Ödeyen şirket uygunluğu güncel durumdur, kayıt anındaki kopya değildir (TP-001 T14):** Create, Submit, her karar ve
  Release'te MOD-0220 `lookup-validation` çağrılır; `OperationalStatus = Active` **ve** `ApprovalStatus = Approved` değilse
  `PAYMENT_LEGAL_ENTITY_NOT_ELIGIBLE` (422); MOD-0220 yanıt vermezse `PAYMENT_LEGAL_ENTITY_UNAVAILABLE` (503) — geçiş yok.
  Sözleşme: LEGAL-ENTITY-LOOKUP 1.1.0 (`docs/analysis/contracts/drafts/legal-entity-lookup-validation-v1.1-proposal.md`,
  MOD-0220 pack §22, TRM-14; sınıflandırma tablosu sözleşme §2). Tutarlı 200 + `approvalStatus = APPROVED` → uygun;
  tutarlı 200 + `DRAFT` / `SUBMITTED` / `REJECTED` ya da 404 → 422 `PAYMENT_LEGAL_ENTITY_NOT_ELIGIBLE`; geçersiz ya da
  çelişkili 200 (`approvalStatus` yok / `null` / boş / bilinmeyen; `legalEntityId` yok ya da farklı; `referenceable = false`;
  `lifecycleState ≠ ACTIVE`; ayrıştırılamayan gövde), 401/403, 5xx, zaman aşımı → 503 `PAYMENT_LEGAL_ENTITY_UNAVAILABLE`
  (kapalı-güvenli: belirsiz yanıt asla "onaylı" sayılmaz). Test: her satır için ayrı vaka (AC-39).
  **Reconcile bu kuraldan muaftır** (sahip kararı D15/2): yeni işlem başlatmaz, daha önce kontrol edilip başlatılmış
  işlemin uzak sonucunu kaydeder; şirket arada pasifleşse de niyet kapanabilmelidir. **Muafiyetin sınırı (gap 4/G2, D15/2
  metniyle tutarlı — muafiyet "yeni işlem başlatmayan" kayda dayanır):** muafiyet yalnız Reconcile'ın uzakta **var olan**
  bir örneğin / geçişin sonucunu kaydettiği yol içindir. Okuma örnek/geçiş göstermezse ve Reconcile uzak yazmayı aynı
  anahtarla yeniden yapacaksa (yalnız anahtarla idempotent olduğu ölçülmüş MOD-0023 başlatma / geçiş) bu **yeni bir
  uzak işlemdir**: önce şirket uygunluğu (D10/1, D14/4), SoD + izin ve asıl aktör yeniden denetlenir (Runtime
  Constraints 3a).
- Soft delete (`IsDeleted` + `DeletedAt` UTC, filtrede Id + TenantId). Okumalar `IsDeleted = false`.
- **Idempotency (sahip kararı D12/2 — her yazma komutunda zorunlu):** Create, Update, Delete, BulkDelete, Validate,
  Submit, Reconcile, her karar, kanıt bağlama, onay belgesi ve Release `Idempotency-Key` başlığı **ister**; başlık yoksa
  400 `PAYMENT_IDEMPOTENCY_KEY_REQUIRED`. Sunucu anahtar **üretmez**.
  - **Makbuz kapsamı:** `(TenantId, LegalEntityId, ActorId, Operation, TargetId, Key)` — `TargetId` Create için boş,
    diğerlerinde parti kimliği. Tekil indeks bu altılıdadır; aynı anahtar başka aktör/işlem/hedef için ayrı makbuzdur.
  - **Makbuz (`FinanceTreasuryCommandReceipt`, servis ortak — Entity Fields, D14/2):** istek parmak izi (sunucuda hesaplanır, muafiyet girdilerini içerir —
    `PaymentWorkflowService.cs:744-746@da4abb20`) ve **yaşam döngüsü**: tek aşamalı komutlarda tx1'de `Completed` + yanıt;
    iki aşamalı komutlarda (Submit ve kararlar — Release bu dilimde tek aşamalıdır) tx1'de `Pending` (yanıt yok, `LinkedIntentId` dolu), yanıt **tx2'de**
    (kesin sonuç ya da 202 belirsiz) ya da — makbuz hâlâ `Pending` ise — **Reconcile'da** yazılır ve makbuz `Completed`
    olur. `Completed` makbuz değişmez (Entity Fields, değişmezlik kuralı).
  - **Kontrol sırası (her komutta aynı):** (1) kimlik + `[HasPermission]` → (2) veri kapsamı (SEC-002, D14/1; okumada
    okuma kapsamı, **yazmada yazma kapsamı** — hedef partinin şirketi kullanıcının kendi şirket yetkisinde değilse, parti
    `read-tenant-wide` ile görünse bile → 404 `PAYMENT_BATCH_NOT_FOUND`, başka adım yok) → (3) makbuz araması: aynı kapsam + farklı parmak
    izi → 409 `PAYMENT_IDEMPOTENCY_CONFLICT`; aynı kapsam + aynı parmak izi + `Completed` → **kayıtlı yanıt aynen döner**;
    aynı kapsam + aynı parmak izi + `Pending` → 202 `PAYMENT_REQUEST_IN_PROGRESS` (bağlı niyetin o anki durumuyla). İki
    durumda da **başka hiçbir adım çalışmaz** — uzak çağrı yok, Reconcile yok. → (4) açık niyet kontrolü (Submit ve
    kararlar; aşağıda 2. ve 4. madde) → (5) durum ve `Version` CAS → (6) iş kuralları ve SoD → (7) tx1 (iş verisi + makbuz
    + denetim, tek işlem).
  - **Kurtarma yalnız `ReconcilePaymentBatchWorkflowCommand` ile yapılır** (kendi `Idempotency-Key`'i ve makbuzuyla); aynı
    anahtarla tekrar asla kurtarma tetiklemez (replay ve recovery ayrıdır).
  - **Çökme:** tx1 sonrası çökmede makbuz `Pending` kalır; aynı anahtarla tekrar 202 `PAYMENT_REQUEST_IN_PROGRESS` alır;
    Reconcile niyeti kapattığında bağlı makbuz **yalnız `Pending` ise** `Completed` yapılır (kesin yanıtla); `Completed`
    makbuza dokunulmaz. tx1 commit olmadıysa hiçbir şey
    yazılmamıştır; aynı anahtarla tekrar baştan çalışır.
- **Kayıt kodu (RCS-001, KARAR D59/1):** `BatchCode` yalnız `IRecordCodeAllocator.AllocateAsync(legalEntityId,
  recordType = PAY, idempotencyKey)` portundan alınır; anahtar Create'in idempotency anahtarından (D12/2) türetilir, aynı
  anahtarla tekrar aynı kodu alır. Bağdaştırıcı v0 sözleşmesini (`POST /api/internal/record-codes/allocate`) çağırır;
  200 dışı yanıt (422 `RECORD_TYPE_UNKNOWN`, 409 `LEGAL_ENTITY_SHORT_CODE_MISSING`, 404, 503) ya da zaman aşımı Create'i
  503 `PAYMENT_RECORD_CODE_UNAVAILABLE` ile başarısız kılar. **Üretimde fail-closed:** port bağlı değilse (gerçek
  bağdaştırıcı kayıtlı değil) Create 503, kodsuz kayıt açılmaz; servis içinde kendi sayaç, yedek kod ya da yerel kod
  üretimi yasak (RCS-001 §6). Sahte allocator yalnız test projesinde kayıtlıdır. Port servis ortaktır; MOD-0119 / 0120 /
  0124 aynı kuralı kendi tür önekleriyle kullanır.
- **Eşzamanlılık:** Update / kararlar `If-Match` (Version) ve durum CAS; bayat istek 409.
- **Yerel kayıt önce, uzak çağrı sonra (domain-config; TP-001 T16):**
  1. *Submit:* tx1 — parti `Submitted`, `PaymentWorkflowStartIntent(Pending)`, merkezi denetim satırı (işleme yazılır).
     Sonra MOD-0023 başlatma, aynı `IdempotencyKey` ile. Sonra tx2: kesin başarı → niyet `Started`, parti
     `PendingApproval` + `WorkflowInstanceId`; kesin ret (400/403/409 — 404 **hariç**: 404 belirsizdir → `Unknown`, D21/7; MOD-0023'ün "başlatma tamamlanmadı" 409'u, 409 `WORKFLOW_TRANSITION_CONFLICT` ve 503 `WORKFLOW_TRANSITION_CONFLICT` / `WORKFLOW_TRANSACTION_UNAVAILABLE` hariç — bunlar belirsizdir, D21/7, D25) → niyet `Refused`, parti `PreviousBatchStatus`, tek istisna satırı; **belirsiz** (zaman aşımı, bağlantı kopması,
     5xx, boş ya da okunamayan 2xx gövde) → niyet `Unknown`, parti `Submitted` kalır, yanıt **202**
     `PAYMENT_WORKFLOW_START_OUTCOME_UNKNOWN`. Belirsiz sonuç **ret olarak kaydedilmez** (kaynak kusuru, paket §H.3).
  2. *Açık niyet varken* (`Pending` / `Unknown`): **aynı** anahtarla tekrar → makbuz kuralı (kayıtlı yanıt ya da 202
     `PAYMENT_REQUEST_IN_PROGRESS`); **farklı** anahtarla Submit → 409 `PAYMENT_WORKFLOW_START_PENDING`, uzak çağrı yok. Parti başına en fazla bir açık
     başlatma niyeti: tekil kısmi indeks `(TenantId, BatchId)` where `Status ∈ {Pending, Unknown}`.
  3. *Reconcile:* sonuç `Started` / `Refused` / `Cancelled`'a yalnız uzak tarafın **kesin** durumuyla bağlanır; belirsiz
     kalırsa durum değişmez ve deneme sayılır. Sıra 3a'dadır.
  3a. *Reconcile önce okur; uzak yazmayı yalnız anahtarla idempotent olduğu ölçülmüş yerde yeniden yapar (gap 4/G2,
     gap 5/G2; her iki aşamalı komut — Submit ve beş karar komutu):*
     - **Önce okuma:** Reconcile uzak tarafı **okur** — başlatmada `instances/by-objects` (parti kimliği) ve niyetin
       deterministik uzak anahtarı; kararda örneğin görev geçmişi ve `RemoteIdempotencyKey`. Okuma yazma değildir.
     - **Uzakta niyetin kendisine ait örnek / geçiş var** (eşleme aşağıda, gap 6/P1-b) → kesin durumu kaydedilir (`Started` / `Recorded` / `Refused`; örnek ya da görev
       uzakta açıkça iptal/kapalıysa `Cancelled`), tx2 yazılır, bağlı makbuz yalnız `Pending` ise `Completed` olur, sonuç
       olayı yazılır. Bu yol şirket kuralından **muaftır** (D15/2).
     - **"Kesin boş" okumadan çıkarılmaz (gap 5/G2):** MOD-0023 sözleşmesinde "bulunamadı / boş okuma son durumdur"
       güvencesi **yoktur** (ilk çağrı hâlâ yolda olabilir, yayılma gecikmesi, başka yüzey); bu yüzden okumaya dayalı
       "kesin boş → yeniden başlat" dalı yoktur. Okuma belirsiz ya da "bulunamadı" ise (zaman aşımı, bağlantı kopması, 5xx,
       okunamayan gövde, 404 / boş sonuç) niyet **asla `Cancelled` yapılmaz**; `Unknown` kalır (`Pending` idiyse
       `Unknown`'a geçer), parti ve makbuz değişmez. Zaman aşımı / 5xx / okunamayan gövdede başka adım yoktur (yalnız
       deneme sayılır); 404 / boş sonuçta tek ek adım aşağıdaki anahtarlı yeniden yazmadır.
     - **Yeniden çağrı yalnız uzak yazmanın kendisi anahtarla idempotent ise — bu modülde yalnız MOD-0023 başlatma ve
       MOD-0023 görev geçişleri.** Ölçüldü (Recovery @4e01e986269065cba6f962e0c13b9b2ae4519f6c): başlatmada tekil kısmi indeks
       `ux_workflow_instances_tenant_idempotency_key_active` (TenantId + IdempotencyKey, `IsDeleted = false`, anahtar
       string — `PlatformSchemaManifest.WorkflowWorkCenter.cs:118-131`) ve işleyici anahtarı önce arayıp var olan örneği
       döndürür (`StartWorkflowInstanceHandler.cs:76-85`); geçişlerde (approve / reject / request-info / cancel /
       delegate) validator anahtarı zorunlu tutar (ör. `ApproveWorkflowTaskValidator.cs:13`), tekil kısmi indeks
       `ux_workflow_transition_logs_task_action_idempotency_active` (TenantId + ApprovalTaskId + Action + IdempotencyKey —
       aynı dosya `:211-225`) ve işleyiciler `GetByTaskActionIdempotencyKeyAsync` ile yeniden oynatır
       (`RejectWorkflowTaskHandler.cs:79-80`, `WorkflowTerminalTransitionWriter.cs:402-407`). Bu yüzden okuma örnek /
       geçiş göstermediyse Reconcile **aynı** uzak yazmayı **aynı deterministik uzak anahtarla** (başlatmada
       `IdempotencyKey`, kararda `RemoteIdempotencyKey`) **bir kez** yeniden yapabilir; en fazla bir örnek / geçişi
       MOD-0023'ün tekil indeksi garanti eder. Yeniden yazmadan önce (i) şirket uygunluğu (`OperationalStatus = Active` ve
       `ApprovalStatus = Approved`, D10/1, D14/4), (ii) SoD (başlatmada aday listesi **yeniden hesaplanmaz** — **KARAR D75/2 (b)**; kullanıcının başlattığı Reconcile dahil — **KARAR D76/3**:
       yeniden başlatma niyetteki ilk listeyi (`PaymentWorkflowStartIntent.CandidatePrincipalIds`, Submit'te R1–R4 ve
       `approve ∩ platform.workflow.tasks.approve` / tier-2 `∩ tier2-approve` kesişiminden sonra yazılmış) aynen gönderir, Auth
       aday sorgusu çağrılmaz (Validation Rules → "Aday tabanı"); kararda niyetteki karar verenle R1–R10), (iii) izin ve (iv) asıl aktör yeniden denetlenir: yeniden yazma MOD-0023'teki
       başlatanı ya da karar vereni **değiştiremez**; Reconcile'ı çağıran, asıl komutun aktörü (`SubmittedBy` /
       `DecidedBy`) değilse uzak yazma çağrısı yapılmaz (409 `PAYMENT_RECONCILE_RESTART_ACTOR_MISMATCH`). Sonuç eşlemesi:
       başarı (yeni ya da yeniden oynatılmış) → niyet sonuçla kapanır (`Started` / `Recorded`, tx2 eşlemesiyle); **kesin
       ret** (tx2 eşlemesi; yinelenen anahtar / tekil indeks çakışması yanıtı ret sayılmaz) → `Refused`; kesin ret olmayan
       her başarısızlık — yinelenen anahtar yarışı yanıtı, 5xx, zaman aşımı, okunamayan gövde → niyet `Unknown` kalır
       (yalnız deneme sayılır).
     - **Güvencenin sınırı — silinen ya da temizlenen uzak kayıt (gap 6/P1-a):** iki tekil indeks de **kısmidir**
       (`IsDeleted = false`); silinmiş ya da temizlenmiş (purge) bir örnek / geçiş kaydı, aynı anahtarla **yenisinin**
       oluşmasına izin verir. Ölçüldü (Recovery @4e01e986269065cba6f962e0c13b9b2ae4519f6c):
       `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/WorkflowRepositories.cs`
       içindeki tek `IsDeleted = true` yazımı `SlaEscalationRule` içindir (`:961-974`, `DeactivateRulesForTemplateAsync`);
       `WorkflowInstance` ya da `WorkflowTransitionLog` için soft delete ya da temizleme yolu bulunmadı;
       `PlatformSchemaManifest.WorkflowWorkCenter.cs` (sha256
       `666b44dcbca19df1f1ebf4f176c9fa86bf66111cc1b935286b625bc1d4706f10`) 0 `ExpireAfter` içerir (TTL yok); okuma tarafında
       `instances/by-objects` silinmiş örnekleri süzer (`GetWorkflowInstancesByObjectsHandler.cs:51`, `DeletedAt is null`),
       yani silinmiş kayıt okumada **görünmez**. "Silme yolu yok" bir ölçümdür, politika değildir; bu yüzden anahtarla
       tekillik güvencesi **yalnız örnek ve geçiş kayıtları silinmediği / temizlenmediği sürece** geçerlidir. Kural: okuma
       silinmiş (`IsDeleted = true` / `DeletedAt` dolu) bir örnek / geçiş gösterirse **ya da** silinmemiş kayıt yokken
       niyette ilk uzak deneme kayıtlıysa ve okuma silinmiş durumu ayırt edemiyorsa (tx1'i commit olmuş her niyet — uzak çağrı yalnız tx1'den sonra yapılır),
       Reconcile aynı anahtarla yeniden yazmaz; niyet `Unknown` kalır (202 `PAYMENT_WORKFLOW_START_OUTCOME_UNKNOWN` /
       `PAYMENT_DECISION_OUTCOME_UNKNOWN`), parti ve bağlı makbuz değişmez. Böylece 404 / boş okumadaki anahtarlı yeniden
       yazma, şu iki koşul kapanana kadar **kapalıdır** (dilim kapısı — DL-2 için; D51/1): (1) MOD-0023'ün saklama + silmeme
       politikası — örnek ve geçiş kayıtları en az niyet ömrü boyunca silinmez, temizlenmez, TTL almaz (Dependencies
       kapanış tablosu; sahibi platform-shared-services); (2) okumanın silinmiş durumu ayırt ettiği ölçüldü ya da eklemeli
       okuma alanıyla sağlandı (D9). İkisi kapanınca "silinmemiş kayıt yok" ilk denemenin MOD-0023'e hiç ulaşmadığı
       anlamına gelir ve yukarıdaki anahtarlı yeniden yazma açılır; silinmiş kayıt görüldüğünde yine yeniden yazma yoktur.
     - **Uzak sonuç doğru denemeye bağlanır (gap 6/P1-b):** `instances/by-objects` aynı partinin **bütün** silinmemiş
       örneklerini döndürür, önceki gönderimlerinkiler dahil (`GetWorkflowInstancesByObjectsHandler.cs:50-69`). Bir okuma
       sonucu bir niyete yalnız şu alanların **hepsi** eşleşirse bağlanır: kiracı; nesne (parti) kimliği; başlatmada
       `SubmissionSequence`, kararda karar kimliği (`PaymentApprovalDecision.Id`); niyetin deterministik uzak anahtarı
       (`IdempotencyKey` / `RemoteIdempotencyKey` — sıra ve karar kimliğini içerir); geçişte ek olarak görev kimliği
       (`WorkflowTaskId`) + eylem (approve / reject / request-info; eylem matrisi MOD-0023 pack §17.4a). Daha eski bir denemenin sonucu
       (önceki `SubmissionSequence`'in örneği, başka bir kararın geçişi ya da iptali) daha yeni bir niyeti **asla**
       kapatmaz; yalnız nesne kimliğiyle ya da "en yeni örnek" sırasıyla eşleme yasaktır. Ölçüldü (Recovery @4e01e986269065cba6f962e0c13b9b2ae4519f6c):
       `WorkflowInstanceStatusDto` (`services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/WorkflowModels.cs:200-207`)
       uzak anahtarı, geçmiş satırı `WorkflowInstanceHistoryEntryDto` (`:451-465`) uzak anahtarı ve görev kimliğini
       taşımaz. Eşleme alanı yanıtta yoksa okuma o niyeti kapatamaz → niyet `Unknown` kalır. Kapanış yolu: MOD-0023 okuma
       yanıtlarına eklemeli alan (`IdempotencyKey`; geçmişte ayrıca `ApprovalTaskId` — D9, MOD-0023 pack değişikliği
       önerisi; Dependencies kapanış tablosu) ya da, P1-a koşulları kapalıysa, aynı anahtarla yeniden yazmanın yanıtı
       (MOD-0023 aynı anahtarla var olan örneği / geçişi döndürür — `StartWorkflowInstanceHandler.cs:76-85`; yanıt anahtara
       bağlıdır).
     - **D21 bağı — yeniden yazma kapısının yeni hâli (sahip kararı D21, MOD-0023 pack §17 (TRM-15)):** yukarıdaki kapının iki
       koşulundan saklama politikası **KARAR D21/1** ile verildi (hiç silinmez, TTL yok) ve MOD-0023 AC-W1 (mimari test) geçince
       kapanır; silinmiş kayıtlar ayrıca okunur (**D25/2**: `isDeleted`, `includeDeleted=true`) — politika + AC-W1 korunur; okumada
       silinmiş kayıt görülürse yeniden yazma yok. §17
       (rev 2, `803fbaa5…`, PR #235) onaylanıp AC-W1 geçince (ve §17 rev 2 kapanışı AC-W1…W17 geçmişse, D22/2, §17.8) 404 / boş okumada aynı deterministik anahtarla yeniden yazma açılır; yalnız asıl
       aktör (**KARAR D21/5**) ve şirket / SoD / izin yeniden kontrolüyle. Silinmiş kayıt yine de görülürse yeniden
       yazma yok, niyet `Unknown`. Eşleme alanları **KARAR D21/3** (§17.3/3) kodda olana kadar okuma niyeti kapatmaz.
     - **Aynı anahtarlı yarış yanıtı (KARAR D21/4, §17.3/4; DL-2 başı kanıt maddesi):** politika — eşzamanlı iki
       istekte son yanıtlar aynı kayıttır ve tek satır oluşur; ara 503 "aynı anahtarla tekrar" olabilir (→ `Unknown`,
       tekrar), 409 yalnız örnek Active'den çıktıysa. Kod okuması (@ef4f3a8bc) bu deseni gösteriyor
       (`StartWorkflowInstanceHandler.cs:300-330, 700-716`, `WorkflowTerminalTransitionWriter.cs:357-…`) ama eşzamanlı
       aynı anahtar testi **ölçülmedi**; MOD-0023 AC-W5…W14 ve AC-W17 (§17 rev 2 DL-2 bitişi: AC-W5…W12, W14, W17) geçene kadar yinelenen anahtar hatası **`Unknown`** sayılır,
       **asla `Refused`** değildir. `Refused` yalnız tx2 tablosundaki kesin iş reddi kodlarıdır (**KARAR D21/7**).
       Hedef yanıtlar MOD-0023 pack §17.3/4 tablosundadır (D22/3): bütünleşmiş başlatmanın ya da kaydedilmiş geçişin
       tekrarı, örnek sonradan kapansa da **200** ve aynı kayıt — yeni ret sayılmaz; hiç bütünleşmemiş ve kurtarmanın
       kapattığı başlatma 409 `WORKFLOW_INSTANCE_START_CONFLICT` → yalnız kurtarmanın kapanış kaydıyla `Cancelled`,
       yoksa `Unknown`; aynı göreve farklı karar çatışmasında kaybeden 409 `WORKFLOW_TASK_INVALID_STATE` → kaybeden
       niyet tx2 eşlemesiyle `Refused` (kendi kararı kaydedilmedi). Kullanılan uzak eylemler: §17.4a matrisi. AC-W5…W12 ve
       AC-W14 MOD-0023 sahibinin WP'sinde geçene kadar yeniden yazma kapısı kapalı kalır (D22/2'nin "yazma yolu değişikliği yok"
       kuralını D25/1 bu WP için kaldırdı; fingerprint düzeltmesi sahibin onayıyla, §17.7/1).
       **MOD-0023 üretici cevabı ve D25 (§17.7):** eşzamanlı aynı anahtarlı geçişte kaybeden 409
       `WORKFLOW_TRANSITION_CONFLICT` alabilir → `Unknown` (kesin ret değil), aynı anahtarla tekrar ya da okuma;
       bütünleşmiş başlatmada farklı olgu bugün 200 döner (üretici düzeltecek) — Treasury anahtarı niyet başına
       deterministiktir, farklı niyette asla tekrar kullanılmaz. Reconcile `includeDeleted=true` ile okur (D25/2);
       §17 kodu MOD-0023 sahibinin WP'siyle gelir (D25/1), bu WP main'e girmeden MOD-0023 kullanan dilim kabul edilmez.
       **§17 rev 2 ve D32 (§17.8, `803fbaa5…`, PR #235):** kodu MOD-0023 sahibi yazar, başka biri merge eder (D32/1);
       bütünleşmiş başlatmada aynı anahtar + farklı olgu ve geçişte aynı anahtar + farklı gövde hedef yanıtı 409, yanıta
       `idempotentReplay` eklenir — Treasury dahil tüketiciler için AC-W17 (D32/4); TRM-15 kapanışı AC-W1…W17.
     - **Anahtarla idempotent olduğu ölçülmemiş uzak yazma yeniden çağrılmaz:** MOD-0031 kanıt bağlamada aynı uzak
       anahtarın tekrarının güvenli olduğu (dedupe) ölçülmedi → Reconcile ve hiçbir kurtarma yolu MOD-0031'e yeniden
       yazma çağrısı yapmaz; yalnız okur, okuma kesin sonuç gösterene kadar sonuç belirsiz kalır. Yeniden çağrıyı açmak için
       MOD-0031 dedupe ölçümü önkoşuldur (Dependencies kapanış tablosu, DL-3 önkoşulu).
     - **`Cancelled` yalnız kesin uzak iptal / kapanış kaydıyla:** o örnek için MOD-0023 iptal geçiş kaydı (ör.
       `CloseAbandonedWorkflowStartHandler.cs`, anahtar `workflow-start-recovery-close:{instanceId:N}`, `:230`) ya da
       görevin iptal kaydı okunduğunda; başka hiçbir yanıt `Cancelled` üretmez.
     - **Uygun değilse** (şirket uygun değil / SoD ya da izin reddi / aktör farklı): uzak yazma/başlatma çağrısı **yapılmaz**; niyet
       `Unknown` kalır ve `LastOutcomeCode` sebep kodunu taşır (`PAYMENT_LEGAL_ENTITY_NOT_ELIGIBLE`, `PAYMENT_SOD_DENIED`,
       `PAYMENT_PERMISSION_DENIED`, `PAYMENT_RECONCILE_RESTART_ACTOR_MISMATCH`); parti durumu ve bağlı `Pending` makbuz
       değişmez (aynı anahtarla tekrar yine 202 `PAYMENT_REQUEST_IN_PROGRESS`), niyet kapanmadığı için sonuç olayı yazılmaz.
       Reconcile'ın kendi makbuzu sebep koduyla (422 / 403 / 409) `Completed` olur. Koşul düzelince (ör. şirket yeniden
       onaylanınca) yeni bir Reconcile tekrar dener. Niyetin kalıcı olarak kapatılması (açık iptal yolu) Follow-up'tadır.
  4. *Kararlar:* SoD ön kontrolü → tx1 `PaymentApprovalDecision(Requested)` + denetim satırı → MOD-0023 görev geçişi,
     karar idempotency anahtarıyla (kaynakta da iletiliyor, `WorkflowClientAdapters.cs:222@da4abb20`) → tx2 `Recorded`
     + parti durumu. Belirsiz → `Unknown`, 202 `PAYMENT_DECISION_OUTCOME_UNKNOWN`, Reconcile kapatır.
     **v1 şablonu tek adımlıdır (`FT-PAYMENT-BATCH-APPROVAL`, Y-12; KARAR D62/2):** Approve'un tx2'si (ya da onu kaydeden
     Reconcile) partiyi yalnız MOD-0023 son adım dediğinde `Approved` yapar; `WorkflowIsFinalStep = false` dönen onay
     `Recorded` olur ama parti `PendingApproval`'da kalır (MOD-0124 Runtime Constraints 3 ile aynı desen). `PartiallyApproved`'a
     hiçbir yoldan geçilmez (AC-46).
     **Parti başına tek açık karar niyeti:** tekil kısmi indeks `(TenantId, BatchId)` where `State ∈ {Requested, Unknown}`;
     tx1 bu indeks + parti `Version` CAS ile atomiktir. Açık karar niyeti varken başka anahtarla ya da başka aktörden
     gelen her karar (çelişen ya da aynı yönde) **uzak sisteme gitmeden** 409 `PAYMENT_DECISION_PENDING` alır; niyet
     tx2 ya da Reconcile ile `Recorded` / `Refused` / `Cancelled` olana kadar yeni karar başlamaz. Aynı anahtarla tekrar →
     makbuz kuralı (kayıtlı yanıt ya da 202 `PAYMENT_REQUEST_IN_PROGRESS`).
  4a. *İki aşamalı denetim (tx1 / tx2) ve çökme kurtarma:*
     - tx1 **deneme olayı** yazar (`…workflow-start-requested`, `…decision-requested`); tx2 **sonuç
       olayı** yazar (`…submitted`, `…approved`, `…rejected`, `…workflow-start-refused` vb.; Release tek aşamalıdır, burada yoktur). Her
       iki olay da kendi işleminde, iş verisiyle birlikte (`RequireEnlistment = true`).
     - Kesin sonuç ancak tx2 commit olunca vardır; tx1 deneme olayı tek başına "yapıldı" anlamına gelmez.
     - **Uzak başarı + tx2 başarısız ya da süreç çöker:** niyet `Requested`/`Pending` kalır; Reconcile (elle) MOD-0023
       örneği/görevi `instances/by-objects` ve görev geçmişinden okur (yalnız niyetin kendi eşleme alanlarıyla — 3a, gap
       6/P1-b), uzak çağrıyı **tekrarlamadan** tx2'yi yazar, sonuç
       olayını üretir ve bağlı makbuz **yalnız `Pending` ise** `Completed` yapılır. Aynı anahtarla tekrar istek kurtarma yapmaz; makbuz `Pending`
       iken 202 `PAYMENT_REQUEST_IN_PROGRESS` alır. İkinci uzak geçiş oluşmaz.
     - **K2 kararları (D11/3):** tx1 deneme olayı yazılamazsa uzak çağrı yapılmaz, komut başarısız; tx2 sonuç olayı
       yazılamazsa niyet açık kalır ve karar "kayıtlı" sayılmaz (Release bunu kabul etmez) — Reconcile tamamlar.
  5. HTTP istemcilerinde **açık zaman aşımı** yapılandırılır (kaynakta yok, paket §H.3 / §N.12); değer
     `FinanceTreasury:Workflow:TimeoutSeconds` — başlangıçta yoksa servis açılmaz; değer 15 sn, **tüketici ayarıdır** (MOD-0023
     şablonunun parçası değildir — Y-12, O-8).
- **SoD politikası tek kaynaktır ve kiracı ayarıdır (sahip kararı D11/1; kayıt yeri KARAR D56/4):** kiracı başına
  PolicyVersion, Tier2Threshold.Amount, Tier2Threshold.CurrencyCode ve **R4 açık / kapalı** anahtarı (R1–R3 kapatılamaz, her
  zaman açık — KARAR D57/4). Kodda varsayılan
  tutar yoktur; kiracının ayarı yoksa **her onay tier-2 sayılır** (güvenli varsayılan) ve parti para birimi eşik para biriminden
  farklıysa da tier-2 sayılır. Çözümleyici (gönderme) ve karar kontrolü **aynı** ayarı, partiye o anda sabitlenen sürümle
  kullanır. **Kayıt yeri (KARAR D56/4):** FinanceTreasuryService içinde kiracı başına **tek** ayar kaydı (`PaymentSodSettings`,
  `payment_sod_settings` — Entity Fields); değişikliği denetlenir (`finance-treasury.payment-sod-settings.updated`, AC-44); kayıt,
  okuma ve yazma komutu DL-2'de yazılır. Her değişiklik `PolicyVersion`'ı bir artırır; gönderme anında sürüm, eşik ve R4
  anahtarı partiye sabitlenir (R6) — sonraki değişiklik gönderilmiş partiyi etkilemez. MOD-0023'ün kendi `SodViolation`
  kontrolü (başlatan onaylayamaz — R2'nin MOD-0023 kısmı) ayar anahtarından etkilenmez. Ayar **ekranı** MOD-0126 ekran
  dilimindedir (DL-4 — Frontend File Contract → "SoD ayar ekranı", KARAR D57/4).
  - **İki durum ayrıdır:** (a) kiracının ayarı **yok** → bu bir hata değildir; R1–R4 açık ve varsayılan eşik — her onay tier-2 sayılır
    (D11/1'in güvenli varsayılanı; KARAR D57/4) ve partiye `SodPolicySource = DefaultAllTier2` olarak sabitlenir; (b) ayar deposu **erişilemiyor** (altyapı arızası) → Submit ve
    kararlar 503 `PAYMENT_SOD_POLICY_UNAVAILABLE`, uzak çağrı yok, kayıt değişmez. Servisin açılışı ayara bağlı değildir.
- **Bankaya dağıtım kapalı (D5):** `IBankDispatchPort`'un tek bağdaştırıcısı `NotConfiguredBankDispatchAdapter`'dır;
  her çağrı `NotConfigured` döner, `PaymentDispatchAttempt` yazılır, parti `Approved` kalır. Gerçek bağdaştırıcı ayrı
  karar ve paket ister.
  - **Release bu dilimde tek işlemdir (gap 3/G3):** bağdaştırıcı süreç içidir ve ağ çağrısı yapmaz; bu yüzden R11
    doğrulaması, bağdaştırıcı çağrısı, `PaymentDispatchAttempt`, makbuz (`Completed`, 200) ve tek `…release-attempted`
    olayı **aynı Mongo işleminde** yazılır. Uzak çağrı olmadığı için niyet, belirsiz sonuç ve Reconcile yoktur; çökme →
    işlem geri alınır, aynı anahtarla tekrar baştan çalışır.
  - **Sözleşme bağı (gap 7–8, `docs/analysis/contracts/bank-dispatch.md` FROZEN 0.1.0 — taslak rev 3'ten, D27/7):** aynı işlemde `ReleaseSequence` +1 (sürüme
    koşullu) ve `dispatchKey` üretilir; makbuz tekrarı portu çağırmaz ve sıra artırmaz; commit edilmiş makbuz yanıtını
    (ve `dispatchKey`'ini) korur; geri alınan deneme sıra ya da anahtar rezerve etmez — arada başka bir Release commit
    ederse yeniden deneme sonraki sırayı alır (gap 8/P2-a; dağıtım kapalı sürüm için yeterli). İstek her talimatı
    kendi `SourceBankAccountRef`'iyle taşır; `DefaultSourceBankAccountRef` talimat değerinin yerine konmaz, talimatlar
    birleştirilmez (D17/1). Port iş sonucunu tipli döndürür: `NotConfigured` → 200; `BankDispatchContractException` /
    sözleşme dışı istisna → geri alma + 500 `PAYMENT_BANK_DISPATCH_CONTRACT_VIOLATION`; iptal → geri alma, kayıt yok;
    `NotConfigured` dışı sonuç → deneme kaydedilir, `ReleaseBlockedForReview = true`, olay sonucu taşır, 502
    `PAYMENT_BANK_DISPATCH_OUTCOME_UNSUPPORTED` (makbuz bu yanıtla `Completed`) — başarılı Release sayılmaz (D17/2).
  - **Gerçek dağıtım açıldığında** Release iki aşamalı olur ve kendi `PaymentReleaseIntent` yaşam döngüsünü (Pending /
    Dispatched / Refused / Unknown → Reconcile), sonuç olayını ve kurtarmasını o kararın paketinde alır; bu dilim bunu
    varsaymaz (Follow-up).
  - **Gerçek dağıtım açılmadan önce zorunlu kapı (bu dilimde opak Guid kontrolünün sınırı):** (1) kaynak banka hesabının
    sahibi ödeyen şirkettir (MOD-0289, güncel durum); (2) hesap ve alacaklı aynı kiracıda ve ödeyen şirket için uygundur
    (Tenant + LE, MOD-0289/MOD-0220); (3) alacaklı referansı ile hedef hesap eşleşir (MOD-0140 `SupplierCompanyRelationship`
    Active / MOD-0289 hedef). Bu üçü sözleşmeli kontrol ve testle kapanmadan `IBankDispatchPort`'a gerçek bağdaştırıcı
    bağlanamaz.
- `GlobalEntity` kullanılmaz; bütün kayıtlar kiracıya aittir.
- Mongo GUID V3 / subtype 4; `decimal` → `Decimal128`.

## Layout & Shell Contract

- `shell: tenant` → `Views/FinanceTreasury/PaymentBatches/` altındaki **tüm** `.cshtml` dosyalarında
  `Layout = "_LayoutTenantShell";` **AÇIKÇA** yazılır; `_ViewStart.cshtml` varsayılanı değiştirilmez. Kaynak ekrandaki
  `Layout = "_LayoutBackbone"` (`frontend/Diten.Web/Views/ERP/Treasury/PaymentProposals/Index.cshtml:3@da4abb20`)
  taşınmaz.
- View klasörü: `Views/FinanceTreasury/PaymentBatches/` · Frontend rotası: `/FinanceTreasury/PaymentBatches`.
- Yetkisiz kullanıcı (UAS-001): `IPermissionSnapshot` ile `finance-treasury.payment-batches.read` yoksa kabuk içinde
  `_AccessDenied` partial'ı; başlık, kart, boş tablo, buton çizilmez; yönlendirme yok; 401 tam sayfa.
- SoD ayar ekranı (KARAR D57/4): `Views/FinanceTreasury/PaymentSodSettings/` · rota `/FinanceTreasury/PaymentSodSettings`; aynı
  `_LayoutTenantShell` kuralı; yetki anahtarı `finance-treasury.payment-sod-settings.read`.

## Backend File Convention

Golden Reference **Compact** birebir (`services/Diten.DevEnablementService/src/Diten.DevEnablementService.Application/Features/GoldenReferenceCompact/`
okundu): `Features/PaymentBatch/` altında

```text
Commands/          CreatePaymentBatchCommand.cs, UpdatePaymentBatchCommand.cs, DeletePaymentBatchCommand.cs,
                   BulkDeletePaymentBatchCommand.cs, ValidatePaymentBatchCommand.cs, SubmitPaymentBatchCommand.cs,
                   ReconcilePaymentBatchWorkflowCommand.cs, ApprovePaymentBatchCommand.cs,
                   ApprovePaymentBatchWithSodWaiverCommand.cs, RejectPaymentBatchCommand.cs,
                   RequestPaymentBatchEvidenceCommand.cs,
                   LinkPaymentBatchEvidenceCommand.cs, GeneratePaymentAttestationCommand.cs, ReleasePaymentBatchCommand.cs
Queries/           GetPaymentBatchListQuery.cs, GetPaymentBatchByIdQuery.cs, GetPaymentBatchTraceQuery.cs,
                   GetPaymentExceptionListQuery.cs, PaymentBatchListSort.cs
Handlers/CommandHandlers/   {Verb}PaymentBatchHandler.cs (her komut için; suffix YOK)
Handlers/QueryHandlers/     Get{…}Handler.cs
Validators/        CreatePaymentBatchValidator.cs, UpdatePaymentBatchValidator.cs, GetPaymentBatchListValidator.cs,
                   PaymentDecisionValidator.cs, PaymentSodWaiverValidator.cs
PaymentBatchModels.cs       (tüm DTO'lar tek dosyada)
```

- Command/Query `sealed record` (`IRequest<Response<T>>`); Handler `sealed class` `{Verb}{Module}Handler` — **Command/Query
  suffix YOK** (kaynak `CreateManualPaymentBatchCommandHandler` aynı dosyada komutla birlikte,
  `CreateManualPaymentBatchCommand.cs:8-25@da4abb20` — taşınmaz). Tek dosyada tek public tip.
- Controller `CustomBaseController`'dan türer, `Response<T>` döner; her aksiyonda `[HasPermission]`.
- İş kuralı servis sınıflarında değil handler + domain'de (`handler-design`); kaynaktaki `PaymentProposalService` /
  `PaymentWorkflowService` gibi büyük servisler (yaklaşık 1000 satır) bölünerek alınır.
- Her yazma komutu `ICentralAuditDeclaration` taşır (`CentralAuditEntityId` açık yazılır); MOD-0023'e yalnız HTTP ile
  dokunup yerelde yazmayan komut yoktur (hepsi yerel niyet yazar), bu yüzden `ICentralAuditOutsideTransaction` kullanılmaz.

## Frontend File Contract

Golden Reference **Compact** seti (`frontend/Diten.Web/Views/DevEnablement/GoldenReferenceCompact/` okundu):

```text
Views/FinanceTreasury/PaymentBatches/
├── Index.cshtml              (Layout = "_LayoutTenantShell"; ① Filter → ② BulkActionBar → ③ DataTable)
├── Create.cshtml             (sayfa kabuğu + _Form)
├── Edit.cshtml               (sayfa kabuğu + _Form; yalnız Draft/Validated)
├── Details.cshtml            (sekme: Talimatlar · İş akışı ve kararlar · İstisnalar · Onay belgeleri · Serbest bırakma denemeleri · İz)
├── _Form.cshtml              (başlık alanları + talimat satırları ızgarası)
├── _Filter.cshtml            (durum, para birimi, değer tarihi aralığı, tutar aralığı)
├── _DataTable.cshtml         (data-dt-standard="v2", data-dt-data-mode="server", iskelet yükleyici)
├── _IndexL10n.cshtml
└── PaymentBatchesIndex.cs    (marker)
wwwroot/assets/js/FinanceTreasury/PaymentBatches/  index.js · index.l10n.js · form.js · details.js
Resources/Views/FinanceTreasury/PaymentBatches/    PaymentBatchesIndex.{en,tr,fr,es,zh,ar,ru}.resx
```

- **YASAK (compact):** `_CreateEditOffcanvas.cshtml`, `_DetailsQuickView.cshtml`, Index içinde create/edit offcanvas.
- Durum filtresi ve rozetleri `PaymentBatchStatus`'un 10 değerinin tamamını gösterir; kaynak ekrandaki
  `PendingValidation` / `Released` (enum'da yok) taşınmaz (paket §I T22).
- DataTable sütunları: Parti kodu, Durum, Para birimi, Toplam tutar (para birimine göre biçim), Talimat sayısı, Değer
  tarihi, İş akışı durumu, Oluşturan, Oluşturma zamanı, İşlemler.
- Karar düğmeleri (Onayla / Reddet / Kanıt iste / Yükselt / Muafiyetle onayla) yalnız Details'ta ve yalnız sunucunun
  verdiği `allowedActions` listesine göre çizilir; SoD nedeniyle karar veremeyen kullanıcıya düğme gösterilmez, yerine
  yerelleştirilmiş açıklama (ör. "Bu partiyi siz hazırladığınız için karar veremezsiniz") çizilir — sunucu kontrolünün
  yerine geçmez.
- API profili: same-origin MVC proxy (`/FinanceTreasury/PaymentBatches/api/...`); proxy `AuthTokenCookies.GetAccessToken`
  ile token'ı ve oturumun çözümlenmiş `X-Tenant-Id` / `X-Legal-Entity-Id` başlıklarını ekler; çözümlenemezse istek
  oluşturulmaz (multi-tenancy §6). Tarayıcı JS `access_token` / `document.cookie` okumaz.
- Uyarı/onay diyalogları SweetAlert2 premium standardı (MOD-0013); hardcoded metin ve `|| 'Export'` tarzı geri dönüş yok.
- Para birimi seçimi `GET /api/lookups/reference/currencies`'ten; sabit liste yok. Banka referansı alanları MOD-0289
  arama ucu gelene kadar opak referans girişi (O-15).

**SoD ayar ekranı (KARAR D57/4; DL-4):** kiracı başına tek kaydın formu — liste / DataTable yoktur.

```text
Views/FinanceTreasury/PaymentSodSettings/
├── Index.cshtml              (Layout = "_LayoutTenantShell"; tek form: eşik tutarı, eşik para birimi, R4 anahtarı; R1–R3 salt okunur "her zaman açık")
├── _IndexL10n.cshtml
└── PaymentSodSettingsIndex.cs (marker)
wwwroot/assets/js/FinanceTreasury/PaymentSodSettings/  index.js · index.l10n.js
Resources/Views/FinanceTreasury/PaymentSodSettings/    PaymentSodSettingsIndex.{en,tr,fr,es,zh,ar,ru}.resx
```

- `finance-treasury.payment-sod-settings.read` yoksa kabuk içinde `_AccessDenied` (UAS-001; başlık / form çizilmez,
  yönlendirme yok); `.update` yoksa form salt okunur, kaydet düğmesi çizilmez — sunucu kontrolünün yerine geçmez.
- Kayıt yoksa ekran varsayılanı gösterir (R1–R4 açık, eşik tanımsız — her onay tier-2) ve ilk kayıt bu formdan oluşturulur.
  R1–R3 için düzenlenebilir alan yoktur. Kaydetme `If-Match` (kayıt varsa) ve `Idempotency-Key` ile; 409
  `PAYMENT_SOD_SETTINGS_VERSION_CONFLICT` yerelleştirilmiş uyarıyla gösterilir (SweetAlert2, MOD-0013).
- Para birimi `GET /api/lookups/reference/currencies`'ten; same-origin MVC proxy (`/FinanceTreasury/PaymentSodSettings/api/...`),
  başlık kuralları PaymentBatches ile aynı.

## Validation Rules

| Alan / kural | Zorunlu | Biçim / kural | DB | Ön kontrol |
|---|---|---|---|---|
| X-Legal-Entity-Id | Evet | Guid; kiracıya ait; Active + Approved; okumada kullanıcının okuma kapsamında (kendi şirket yetkisi ya da `read-tenant-wide`, D14/1); **yazmada** hedef partinin şirketine eşit ve kullanıcının **kendi** şirket yetkisinde (yazma kapsamı — `read-tenant-wide` yazma yetkisi vermez, gap 4/G1); değilse 404 | — | `ILegalEntityEligibilityPort`, `IFinanceTreasuryDataScopeSource` (okuma / yazma kapsamı) |
| Gövde/sorguda tenantId, legalEntityId | — | bulunursa 400 | — | middleware |
| DefaultSourceBankAccountRef | Evet | Guid ≠ Empty | — | — |
| PaymentMethod | Evet | trim, 1..32; MOD-0048 `payment-method` kümesinde yayımlanmış ve `IsActive = true` kod (büyük-küçük harf duyarlı, `StringComparison.Ordinal` — S-56) | — | `IPaymentMethodReferencePort` (MOD-0048 consumable set, D28 / O-13) |
| CurrencyCode | Evet | `^[A-Z]{3}$` (upper normalize), MOD-0048 listesinde (değilse 400 `PAYMENT_CURRENCY_CODE_INVALID`) | — | `ICurrencyReferencePort` |
| RequestedValueDate | Evet | `DateOnly`, default değil; geçmiş tarih O-12 | — | — |
| RiskRating | Hayır | enum | — | — |
| EvidenceRequired | Hayır | bool | — | — |
| Notes | Hayır | trim, ≤ 2000 | — | — |
| Instructions | Evet | 1..500 satır | — | — |
| Instruction.ManualOutflowReference | Evet | trim, 1..64 | — | — |
| Instruction.PayeeType | Evet | tanımlı enum | — | — |
| Instruction.PayeeReferenceId | Koşullu | `PayeeType ≠ Other` → Guid ≠ Empty | — | varlık O-14 |
| Instruction.SourceBankAccountRef | Evet | Guid ≠ Empty | — | — |
| Instruction.DestinationReferenceId | Hayır | Guid ≠ Empty ise kabul | — | — |
| Instruction.Amount | Evet | > 0; küsurat ≤ para birimi hanesi | — | `ICurrencyReferencePort` (hane) |
| Instruction.CurrencyCode | — | gönderilirse partininkine eşit | — | — |
| Instruction.RemittanceReference / SourceDocumentRef | Hayır | trim, ≤ 140 / ≤ 128 | — | — |
| BatchCode | — | istekte bulunursa 400 (OTOMATİK) | Unique (TenantId, BatchCode), kısmi filtre yok — silinen dahil (`ux_payment_batch_code`; RCS kodu DB tekilliğini kaldırmaz) | `IRecordCodeAllocator` |
| Idempotency-Key | **Evet** (her yazma komutu, D12/2) | 1..128 yazdırılabilir ASCII; yoksa 400 `PAYMENT_IDEMPOTENCY_KEY_REQUIRED` | Unique (TenantId, LegalEntityId, ActorId, Operation, TargetId, Key) | makbuz araması (Runtime Constraints sırası) |
| Karar ReasonCode | Reject / RequestEvidence için Evet | trim, ≤ 64 (`PaymentWorkflowService.cs:291-296@da4abb20`) | — | — |
| Karar WorkflowTaskId | Evet | Guid ≠ Empty, partinin örneğine ait | — | MOD-0023 |
| Muafiyet ReasonCode | Evet | kapalı liste (KARAR D49/4): `ACIL-ODEME` \| `TEK-YETKILI` \| `DENETIM-ISTEGI`; büyük harf, sıralı karşılaştırma; yok ya da listede değil → 400 `PAYMENT_SOD_WAIVER_REASON_REQUIRED` | — | — |
| Muafiyet EvidenceRefs | Evet | en az 1 MOD-0031 bağlantı kimliği ya da belge sürüm kimliği (`:364-372@da4abb20`) | — | — |
| Muafiyet atamasının hedef partisi (Auth'tan okunur, istekte değil) | Evet | atamada hedef parti kimliği var, okunabilir ve rotadaki `{id}` ile eşit (KARAR D54); yok, okunamıyor ya da farklı → 403 `PAYMENT_SOD_WAIVER_FORBIDDEN`; yazım ve MOD-0023 çağrısı yok | — | MOD-0018 açık atama kaydı |

**Durum kuralları:** Update yalnız `Draft`/`Validated` ve `WorkflowInstanceId = null`, açık niyet yok; güncelleme partiyi
`Draft`'a döndürür ve talimat doğrulama durumlarını sıfırlar. Validate kaynaktaki kontrolleri yapar (talimat var, tek para
birimi, pozitif tutar, toplam = satır toplamı, sayı = satır sayısı, değer tarihi, hassasiyet —
`PaymentLifecycleService.cs:91-145@da4abb20`). Submit yalnız `Draft`/`Validated`; `EvidenceRequired` ise en az bir kanıt.
Delete / BulkDelete yalnız hiç gönderilmemiş (`SubmissionSequence = 0`) `Draft`/`Validated` parti.

**SoD kuralları (çekirdek — her karar komutunda, MOD-0023 çağrısından ÖNCE):**

| # | Kural | Nerede | Kaynak karşılığı |
|---|---|---|---|
| R1 | Partiyi oluşturan (`CreatedBy`) partide **hiçbir karar** veremez | gönderme çözümleyicisi (aday dışı) + karar kontrolü | `PaymentSodPolicyService.cs:38-44@da4abb20`; `ConfiguredApproverResolver.cs:117-121` |
| R2 | Gönderen (`SubmittedBy` = MOD-0023 başlatanı) karar veremez | aynı + MOD-0023 `SodViolation` | `:46-56@da4abb20`; Recovery `WorkflowTaskTransitionSupport.cs:964-977` |
| R3 | Gönderme anındaki son güncelleyen (`UpdatedByBeforeSubmit`) karar veremez | aynı | kaynakta yalnız çözümleyici dışlıyor (`ConfiguredApproverResolver.cs:117-121@da4abb20`), SoD servisi kontrol etmiyor — Recovery'de iki yerde tutarlı |
| R4 | **Uygulanma koşulu (CT-APR-02 Codex F-1; KARAR D56/4, D57/4):** R4 yalnız `IsTier2 = true` **ve** partiye sabitlenen ayar sürümünün `R4Enabled = true` olduğu partide uygulanır ("R4 uygulanan parti"); bu pakette R4, aday tabanı, muafiyet adayı ve R11 bağlamındaki "tier-2 parti" / "`IsTier2` parti" ifadeleri bu iki koşulu birlikte anlatır. Sabitlenmiş `R4Enabled = false` partide `IsTier2` yine hesaplanır ve sabitlenir (R5, R6) ama R4 uygulanmaz: aday tabanı normal partininkidir (`approve ∩ platform.workflow.tasks.approve`; `tier2-approve` sorgulanmaz), muafiyet adayı kuralı (D13) işlemez, Approve `tier2-approve` istemez ve R11'in tier-2 şartı aranmaz; R1–R3 ve R9'un R1–R3 yasağı aynen uygulanır (AC-44 (d), AC-19). **KARAR D77/1:** sabitlenmiş `R4Enabled = false` partide `ApprovePaymentBatchWithSodWaiverCommand` 403 `PAYMENT_SOD_WAIVER_FORBIDDEN` ile reddedilir — karar, niyet, makbuz yazılmaz, MOD-0023 çağrılmaz, parti değişmez (aşılacak R4 kuralı yoktur; kullanıcı normal onayı kullanır; AC-43 (i)). **KARAR D78:** sabitlenmiş `R4Enabled = true` ama `IsTier2 = false` partide (R4 bu partiye uygulanmaz) de `ApprovePaymentBatchWithSodWaiverCommand` 403 `PAYMENT_SOD_WAIVER_FORBIDDEN` ile reddedilir — `Waived` kaydı oluşmaz, karar, niyet, makbuz yazılmaz, MOD-0023 çağrılmaz, parti değişmez (D77/1 ile aynı mantık: aşılacak R4 kuralı yoktur; AC-43 (j)). R4 uygulanan parti için **Approve** `finance-treasury.payment-batches.tier2-approve` ister (uç izni `finance-treasury.payment-batches.approve`'a ek olarak); gönderme çözümleyicisi tier-2 adaylarını Auth aday sorgusundan (KARAR D66/1) **iki iznin kesişimi** olarak seçer (`approve ∩ tier2-approve`; iş akışı izni `platform.workflow.tasks.approve` ile de kesişir — KARAR D71; aşağıda "Aday tabanı"). **Muafiyet adayı (sahip kararı D13):** R1–R3 dışlamasından sonra kiracıda **hiç** tier-2 aday (kesişimden) kalmıyorsa, Auth'tan **"bu partiye geçerli `waive-sod` ataması olanlar"** sorgusuyla okunan kişiler (**KARAR D56/3**; D66/1 ile tek aday sorgusu — MOD-0018 Treasury Auth ekinin DL-6 aday sorgusunun `waive-sod` + parti biçimi, parti bağı DL-5 kaydından — KARAR D64; MOD-0023 ataması izin bilgisi taşımaz) `IsWaiverCandidate = true` ile aday eklenir; sorgu okunamazsa muafiyet adayı eklenmez ve gönderme kapalı-başarısızdır (yanıt sınıfı Auth uç sözleşmesiyle yazılır); bu adaylar yalnız `ApprovePaymentBatchWithSodWaiverCommand` ile onaylayabilir (normal Approve 403 `PAYMENT_TIER2_APPROVER_REQUIRED`); tier-2 aday varken muafiyet adayı eklenmez. Normal onay yetkisi (R4) ve muafiyet yetkisi (R9) ayrı değerlendirilir | gönderme çözümleyicisi + karar | `PaymentSodPolicyService.cs:58-67@da4abb20`; `ConfiguredApproverResolver.cs:41-43`; kaynakta `WaiverApprovers` listesi tanımlı ama okunmuyordu (paket §E) |
| R5 | `IsTier2` hesabı: `batch.CurrencyCode == Threshold.CurrencyCode && TotalAmount >= Threshold.Amount`; para birimi farklıysa **tier-2 sayılır** (kur çevrimi yok; önerilen ihtiyatlı kural, O-4) | gönderme (sabitlenir) | kaynakta iki ayrı, para birimsiz eşik: `SodPolicyContracts.cs:10@da4abb20` (50 000) ve `ApproverPolicyContracts.cs:13` (null) — ayrışabilir (paket §H.2 (3)); **taşınmaz** |
| R6 | Eşik, para birimi ve politika sürümü gönderme anında partiye ve niyete yazılır; karar kontrolü yalnız sabitlenmiş değeri kullanır | karar | yeni (level-1 koşul 1) |
| R7 | Aktör karşılaştırması token `sub`'ının **Guid** değeri üzerindendir; `sub` Guid değilse karar reddedilir (`FailedRoleConflict`) | her yer | kaynakta SoD ordinal, çözümleyici büyük/küçük harf duyarsız (paket §H.2 (3)) — **taşınmaz** |
| R8 | İzin karar anında yeniden değerlendirilir (o anki token, `[HasPermission]` + handler içi tier-2/muafiyet izni) | karar | SoD değerlendirmesi Q3 "not established" — burada test edilir |
| R9 | Muafiyet yalnız `ApprovePaymentBatchWithSodWaiverCommand` ile: izin `waive-sod` + `ReasonCode` + en az 1 kanıt. **Yalnız R4'ü (tier-2 izni eksikliği) kaldırabilir** (sahip kararı D12/1). R1, R2, R3 muaf tutulamaz → 403 `PAYMENT_SOD_WAIVER_NOT_ALLOWED`; MOD-0023'ün başlatan ve atama kontrolleri de kaldırılamaz. Aday çözümleyici R1–R3 kişilerini dışlar (muafiyetle de aday olamazlar); muafiyet yalnız atanmış ama tier-2 izni olmayan onaylayıcı için anlamlıdır | karar | `PaymentSodPolicyService.cs:78-112@da4abb20` (izin, gerekçe, kanıt sırası korunur); kaynak R1/R3/R4 kaldırabiliyordu — **daraltıldı** |
| R10 | Onaylayan, MOD-0023 görevinin atanmış aktörü olmalıdır (MOD-0023 `WorkflowActorDenied`) | MOD-0023 | SoD değerlendirmesi Q2 |
| R11 | Release, karar zincirini **aynı muafiyet modeliyle** yeniden doğrular: her `Recorded` onayın aktörü R1–R3'e uyar (muafiyet yok); R4 uygulanan partide (`IsTier2` ∧ sabitlenmiş `R4Enabled = true`) en az bir onay ya `tier2-approve` izniyle ya da kayıtlı bir R4 muafiyetiyle verilmiştir — sabitlenmiş `R4Enabled = false` partide bu şart aranmaz (CT-APR-02 Codex F-1); **bu tier-2 şartı tutmazsa Release 403 `PAYMENT_TIER2_RELEASE_FORBIDDEN` (KARAR D77/2): parti `Approved` kalır, `PaymentDispatchAttempt` yazılmaz (AC-19)**; açık (`Requested`/`Unknown`) karar niyeti varsa Release 409 | Release | yeni — Görev Merkezi gibi başka yüzeyden verilmiş karara karşı ikinci kontrol (O-7) |

**Aday tabanı (KARAR D66/1):** gönderme çözümleyicisi (yalnız Submit — kullanıcının başlattığı gönderim; Reconcile'ın yeniden başlatması niyetteki ilk listeyi kullanır, Auth'u yeniden çağırmaz — **KARAR D75/2 (b)**; kullanıcının başlattığı Reconcile da yeniden denemedir, rol değişikliği gerekiyorsa yeni gönderim yapılır — **KARAR D76/3**; Auth iç uç kimliği **KARAR D74/1**: ortak sır + iletilen kullanıcı token'ı, servis kimliği yok) adayları MOD-0018 Treasury Auth
ekinin DL-6 aday sorgusundan okur (`execution/domains/platform-shared-services/module-packs/MOD-0018-FU27-treasury-permission-onboarding.md` Bölüm 8.6; port `IFinanceTreasuryApproverCandidateSource`): normal partide
(ve sabitlenmiş `R4Enabled = false` olan `IsTier2` partide — R4 uygulanmaz, CT-APR-02 Codex F-1) **`approve ∩ platform.workflow.tasks.approve`**; R4 uygulanan partide (`IsTier2` ∧ sabitlenmiş `R4Enabled = true`) **`approve ∩ tier2-approve ∩ platform.workflow.tasks.approve`**
(R4; sahip denetimi 61466e504; **KARAR D71**): servis sorguyu `finance-treasury.payment-batches.approve`, R4 uygulanan partide ayrıca
`finance-treasury.payment-batches.tier2-approve` ve her partide `platform.workflow.tasks.approve` için ayrı ayrı çağırır
(sorgu tek anahtar alır) ve bütün listelerde bulunan kişileri alır — Approve ucu temel `approve`'u, işleyici `tier2-approve`'u,
MOD-0023 görev kararı `platform.workflow.tasks.approve`'u istediği için bunlardan birini taşımayan kişi kararı uygulayamaz ve
aday olmaz. Çağrılardan biri okunamazsa sorgu okunamaz sayılır (aşağıda). **İş akışı kesişimi (KARAR D71; MOD-0018 eki K-A4b
kapandı):** `platform.workflow.tasks.approve` / `.reject` onaylayıcı rollere rol şablonuyla gelir (**KARAR D70/1**, değişmedi);
kesişim, modül onay anahtarı rol dışı elle verilmiş ve iş akışı izni olmayan kişinin aday olmasını önler — karar veremeyecek
kişi aday olmaz. Auth yalnız tabanı verir (kiracı, etkin kullanıcı, token'a giren izin); servis kesişime R1–R3'ü uygular, şirket kapsamı dışındakileri
çıkarır (o paketin K-A2'si: CT önerisi servis `IFinanceTreasuryDataScopeSource` ile süzer), muafiyet adayını R4 kuralıyla ekler
(muafiyet adayı da `platform.workflow.tasks.approve` ile kesişir — karar veremeyecek kişi aday olmaz, **KARAR D71**)
ve kalanı niyete (`PaymentWorkflowStartIntent.CandidatePrincipalIds`) yazıp MOD-0023'e gönderir. R4 uygulanan partide kesişim boşsa (ya
da R1–R3 ve kapsamdan sonra boş kalırsa) "tier-2 aday yok" sayılır: R4'ün muafiyet adayı kuralı (D13) işler. Liste (normal
partide kesişim ve kurallardan sonra, R4 uygulanan partide muafiyet adayı kuralından da sonra) boşsa boş liste
dalı geçerlidir: 422 `PAYMENT_WORKFLOW_RESOLVER_NO_ELIGIBLE_APPROVER`, MOD-0023 çağrılmaz (KARAR D68; Failure Path). Sorgu okunamazsa (zaman aşımı,
5xx, 401 / 403, bozuk gövde, uç yok — herhangi bir anahtar çağrısında) gönderme kapalı-başarısızdır: niyet yazılmaz, parti değişmez, MOD-0023 çağrılmaz (yanıt sınıfı
Auth uç sözleşmesiyle — o paketin K-A1'inin sözleşme dosyası kısmı; kimlik **KARAR D74/1**: `X-Internal-Api-Key` + iletilen kullanıcı
token'ı, kiracı token'dan, servis kimliği kullanılmaz; Auth'un 401'i de "okunamaz"dır). Ayrı dizin istemcisi ya da yapılandırma listesi taşınmaz (O-10).

**SoD ayarı (KARAR D56/4):** `UpdatePaymentSodSettingsCommand` `If-Match` (kayıt varsa) ve `Idempotency-Key` ister (D12/2);
`Tier2ThresholdAmount` > 0 ve para biriminin küsurat hanesini aşmaz; `Tier2ThresholdCurrencyCode` MOD-0048 listesinde (yoksa 400
`PAYMENT_CURRENCY_CODE_INVALID`; liste okunamazsa 503 `PAYMENT_CURRENCY_SOURCE_UNAVAILABLE`); `R4Enabled` zorunlu. **KARAR D57/4:**
R1–R3 kapatılamaz — istekte R1 / R2 / R3 anahtarı bulunursa 400 (bilinmeyen / sunucu alanı; kayıt ve olay yok); R1–R3 her
partide uygulanır. R4'ün bir partide uygulanıp uygulanmayacağı partiye sabitlenen ayar sürümünün `R4Enabled` değerine göredir (R4 satırındaki uygulanma
koşulu `IsTier2` ∧ sabitlenmiş `R4Enabled = true`; aday tabanı, Approve kontrolü ve Release R11 aynı koşulu kullanır — AC-44 (d), AC-19);
R9'un muafiyet modeli ve MOD-0023 `SodViolation` anahtardan etkilenmez. Kayıt yokken okuma (`GetPaymentSodSettingsQuery`)
hata değil, varsayılanı döndürür: R1–R4 açık, eşik yok (her onay tier-2), `SodPolicySource = DefaultAllTier2`.

## Failure Path to Verify

- **Kendi partisini onaylama (oluşturan)** → 403 `PAYMENT_SOD_DENIED`, `FailedSelfApproval`; MOD-0023 çağrılmaz; tek
  istisna satırı; parti durumu değişmez.
- **Gönderenin onayı** → 403 `PAYMENT_SOD_DENIED`, `FailedSubmitterApproval`; MOD-0023 çağrılmaz.
- **Son güncelleyenin onayı** → 403 `PAYMENT_SOD_DENIED`, `FailedUpdaterApproval`.
- **R4 uygulanan parti (`IsTier2` ∧ sabitlenmiş `R4Enabled = true`), tier-2 izni olmayan onaylayıcı** → 403 `PAYMENT_TIER2_APPROVER_REQUIRED`, `FailedAmountTier`.
- **Muafiyet izni yok** → 403 `PAYMENT_SOD_WAIVER_FORBIDDEN`; **gerekçe yok ya da listede değil** (D49/4) → 400
  `PAYMENT_SOD_WAIVER_REASON_REQUIRED`; **kanıt yok** → 422 `PAYMENT_SOD_WAIVER_REQUIRED`; **R1/R2/R3 için muafiyet istemi** →
  403 `PAYMENT_SOD_WAIVER_NOT_ALLOWED`.
- **R4 kapalı partide muafiyetli onay** (sabitlenmiş `R4Enabled = false`; KARAR D77/1) → 403 `PAYMENT_SOD_WAIVER_FORBIDDEN`;
  karar, niyet, makbuz yazılmaz, MOD-0023 çağrılmaz, parti değişmez — aşılacak R4 kuralı yoktur, kullanıcı normal onayı
  kullanır (AC-43 (i)).
- **R4 açık ama tier-2 olmayan partide muafiyetli onay** (sabitlenmiş `R4Enabled = true`, `IsTier2 = false`; KARAR D78) → 403
  `PAYMENT_SOD_WAIVER_FORBIDDEN`; `Waived` kaydı oluşmaz, karar, niyet, makbuz yazılmaz, MOD-0023 çağrılmaz, parti değişmez — R4
  bu partiye uygulanmaz, aşılacak R4 kuralı yoktur (AC-43 (j)).
- **Muafiyet ataması geçersiz** (KARAR D49/4): bitişi geçmiş, başlangıç–bitiş aralığı 24 saati aşıyor, atamayı onaylayan çağıranın
  kendisi ya da partiyi oluşturan / gönderen / son güncelleyen, atama başka bir partiye bağlı, ya da atama kaydının bu alanlarından
  biri okunamıyor → 403 `PAYMENT_SOD_WAIVER_FORBIDDEN`; karar, niyet ve MOD-0023 çağrısı yok (AC-43).
- **Muafiyet atamasında hedef parti yok, okunamıyor ya da rotadaki partiyle eşleşmiyor** (KARAR D54) → 403
  `PAYMENT_SOD_WAIVER_FORBIDDEN`; yazım ve MOD-0023 çağrısı yok. Aynı atama A partisinde kullanıldıktan sonra B partisinde
  denenirse ikinci çağrı aynı 403 ile yazımdan ve MOD-0023 çağrısından önce reddedilir (AC-43).
- **Açık karar niyeti varken başka anahtar/aktörden karar** → 409 `PAYMENT_DECISION_PENDING`, MOD-0023 çağrılmaz.
- **Idempotency-Key yok** → 400 `PAYMENT_IDEMPOTENCY_KEY_REQUIRED`, kayıt yok.
- **Ortak kod servisi (RCS-001) yok, port bağlı değil ya da 200 dışı yanıt** (KARAR D59/1) → Create 503
  `PAYMENT_RECORD_CODE_UNAVAILABLE`; parti yazılmaz (kodsuz kayıt yok), yedek kod üretilmez (AC-45).
- **Uzak başarı sonrası tx2 hatası / çökme** → niyet açık kalır; Reconcile uzak çağrıyı tekrarlamadan sonucu yazar.
- **Ödeyen şirket aktif değil ya da onaylı değil** → Create/Submit/karar/Release 422 `PAYMENT_LEGAL_ENTITY_NOT_ELIGIBLE`,
  kayıt değişmez; MOD-0220 erişilemez → 503 `PAYMENT_LEGAL_ENTITY_UNAVAILABLE`.
- **Başka kiracı / başka şirket partisi** (okuma, karar, silme) → 404 `PAYMENT_BATCH_NOT_FOUND`, varlık sızdırılmaz.
- **Kapsam dışı şirket** (kullanıcının veri kapsamında olmayan şirket, `read-tenant-wide` yok — D14/1) → liste 200 boş,
  detay / iz / yazma 404 `PAYMENT_BATCH_NOT_FOUND`; veri kapsamı çözücüsü hata verirse aynı sonuç (boş kapsam).
- **Yalnız `read-tenant-wide` ile görünen partiye yazma** (kullanıcının o şirkette kendi yetkisi yok — gap 4/G1) →
  Create / Update / Delete / Validate / Submit / her karar / Reconcile / kanıt / onay belgesi / Release 404
  `PAYMENT_BATCH_NOT_FOUND`; liste ve detay okuması 200; MOD-0023'e ve MOD-0220'ye çağrı yok, kayıt ve makbuz yazılmaz.
- **Reconcile, uzakta örnek bulunamadı / okuma belirsiz** (gap 4/G2, gap 5/G2) → niyet `Cancelled` yapılmaz,
  `Unknown` kalır; okuma "kesin boş" kararı vermez. Zaman aşımı / 5xx / okunamayan gövde → uzak yazma/başlatma çağrısı
  yok. 404 / boş okumada yalnız MOD-0023 başlatma / görev geçişi aynı deterministik anahtarla yeniden yazılabilir;
  önce şirket + SoD + izin + aktör denetlenir: şirket uygun değilse uzak yazma/başlatma çağrısı yok, niyet `Unknown` +
  `LastOutcomeCode = PAYMENT_LEGAL_ENTITY_NOT_ELIGIBLE`, Reconcile 422; aktör farklıysa 409
  `PAYMENT_RECONCILE_RESTART_ACTOR_MISMATCH`; uygunsa aynı anahtarla tek uzak yazma — başarı (yeni ya da yeniden
  oynatılmış) niyeti kapatır, yinelenen anahtar hatası / 5xx / zaman aşımı → `Unknown` (asla `Refused` değil).
- **Uzak kayıt silinmiş / temizlenmiş** (gap 6/P1-a) → okuma silinmiş kaydı gösterirse ya da silinmemiş kayıt yokken
  niyetin ilk denemesi kayıtlıysa (okuma silinmiş durumu ayırt edemiyorken) (tx1 commit olmuş) aynı anahtarla yeniden yazma **yapılmaz**; niyet `Unknown`, 202
  `PAYMENT_WORKFLOW_START_OUTCOME_UNKNOWN` / `PAYMENT_DECISION_OUTCOME_UNKNOWN`, parti ve makbuz değişmez, MOD-0023'e
  yazma çağrısı yok. MOD-0023 pack §17 (TRM-15) rev 2 (`803fbaa5…`, PR #235) onaylanıp AC-W1 ve AC-W15 geçene kadar (KARAR D21/1–2, D32/2) 404 / boş okumada yeniden yazma kapalıdır.
- **Aynı partinin eski örneği / eski kararın geçişi okunuyor** (gap 6/P1-b) → yeni niyete bağlanmaz; eşleme (kiracı,
  parti, `SubmissionSequence` / karar kimliği, uzak anahtar, geçişte görev + eylem) tutmazsa ya da yanıt eşleme alanını
  taşımıyorsa niyet `Unknown` kalır.
- **MOD-0031 kanıt bağlama belirsiz** → yeniden çağrı yok (dedupe ölçülmedi, gap 5/G2); yalnız okuma, kesin sonuç
  okunana kadar belirsiz.
- **İş akışı başlatma zaman aşımı / bağlantı kopması / boş 2xx** → 202 `PAYMENT_WORKFLOW_START_OUTCOME_UNKNOWN`, niyet
  `Unknown`, parti `Submitted`; ret istisnası yazılmaz; ikinci Submit 409 `PAYMENT_WORKFLOW_START_PENDING`; Reconcile aynı
  anahtarla ilerler ve ikinci uzak örnek oluşmaz.
- **İş akışı başlatma kesin reddi** (ör. 400 doğrulama, 403 yetkisiz başlatma) → 502 `PAYMENT_WORKFLOW_START_FAILED`,
  niyet `Refused`, parti önceki durumuna döner.
- **MOD-0023 başlatmada 404** (ör. şablon yok) → kesin ret **değildir** (D21/7): 202 `PAYMENT_WORKFLOW_START_OUTCOME_UNKNOWN`,
  niyet `Unknown`, parti `Submitted` kalır (geri alınmaz), sonuç olayı yok; Reconcile okumasıyla kapanır ya da `Unknown`
  kalır (AC-40).
- **Idempotent tekrar, makbuz `Pending`** (iki aşamalı komut sonucu henüz yok / çökme) → 202 `PAYMENT_REQUEST_IN_PROGRESS`,
  kurtarma yapılmaz, uzak çağrı yok.
- **Idempotent tekrar** — aynı anahtar + aynı yük (makbuz `Completed`): kayıtlı yanıt, ikinci kayıt/denetim olayı/uzak örnek yok; aynı anahtar +
  farklı yük: 409 `PAYMENT_IDEMPOTENCY_CONFLICT`.
- **Eksik alan** (PaymentMethod, CurrencyCode, talimat) → 400 + alan düzeyi resx iletisi; kayıt oluşmaz.
- **PaymentMethod kümede yok ya da pasif** (D28 / O-13) → Create / Update / Validate / Submit 422 `PAYMENT_METHOD_NOT_ALLOWED`,
  kayıt değişmez; karşılaştırma `StringComparison.Ordinal` (harfe duyarlı, S-56). Daha önce kaydedilmiş partide kod sonradan pasifleşirse Approve / Release etkilenmez (değer partide sabit);
  yeniden Update / Submit yine kontrol eder.
- **MOD-0048 kümesi okunamıyor** (5xx, zaman aşımı, ayrıştırılamayan gövde, küme yok / izin listesinde değil) → 503
  `PAYMENT_METHOD_SOURCE_UNAVAILABLE`, yazma yok; serbest metne ya da gömülü listeye düşülmez.
- **Para birimi MOD-0048 listesinde yok** (biçimi geçerli, ör. `XYZ`) → 400 `PAYMENT_CURRENCY_CODE_INVALID`, kayıt yok.
- **Hassasiyet aşımı** (ör. JPY'de küsurat) → 400; hane kaynağı yok → 503 `PAYMENT_CURRENCY_PRECISION_UNAVAILABLE`.
- **Para birimi listesi okunamıyor** (MOD-0048 çağrısında zaman aşımı, 5xx, ayrıştırılamayan gövde ya da küme / uç yok)
  → 503 `PAYMENT_CURRENCY_SOURCE_UNAVAILABLE`, kayıt yok; bu bir bağımlılık hatasıdır, **geçersiz kullanıcı girdisi sayılmaz** — 400
  `PAYMENT_CURRENCY_CODE_INVALID` yalnız liste başarıyla okunduğunda ve kod listede olmadığında döner. Test `PaymentCurrencySourceUnavailableIsNotInvalidInputTests`: sahte
  MOD-0048 istemcisi (a) 5xx, (b) zaman aşımı, (c) bozuk gövde döndürür → her biri 503, kayıt yok, asla 400; (d) liste
  okunur, kod yok → 400. Sabotaj: okunamayan listeyi "kod yok" sayan eşleme (a)–(c)'yi kırmızıya çevirir.
- **Eşzamanlılık** (bayat `If-Match`) → 409 `PAYMENT_BATCH_VERSION_CONFLICT`, sessiz üzerine yazma yok.
- **Yetkisiz aktör** (ilgili izin yok) → API 403; UI: kabuk içinde erişim açıklaması, düğme yok (UAS-001).
- **Gönderilmiş partiyi silme/güncelleme** → 409 `PAYMENT_DECISION_STATUS_INVALID`.
- **Denetim kaydı yazılamıyor** (K2 sınıfı komut, outbox yazımı başarısız) → işlem geri alınır, komut başarısız, iş verisi
  yazılmaz (O-6).
- **Kiracının SoD ayarı yok** → hata değil: R1–R4 açık ve varsayılan eşik — her onay tier-2 sayılır (D11/1, KARAR D57/4),
  `SodPolicySource = DefaultAllTier2`; okuma ucu ve ekran bu varsayılanı gösterir.
- **SoD ayar deposu erişilemiyor** → Submit/karar 503 `PAYMENT_SOD_POLICY_UNAVAILABLE`, uzak çağrı yok, kod varsayılanına
  düşülmez.
- **SoD ayarı değişikliği (KARAR D56/4)** → bayat `If-Match` 409 `PAYMENT_SOD_SETTINGS_VERSION_CONFLICT`; geçersiz tutar /
  para birimi 400; R1 / R2 / R3'ü kapatma (ya da istekte göndermek) 400, kayıt ve olay yok (KARAR D57/4); denetim olayı
  yazılamazsa komut başarısız, ayar değişmez; aynı kiracıda ikinci kayıt oluşmaz (tekil indeks); `.update` izni olmayan
  kullanıcı 403 (ekranda kaydet düğmesi çizilmez); gönderilmiş partiler sabitlenmiş eski sürümle karar görür (AC-44).
- **Muafiyet adayı sorgusu okunamıyor (KARAR D56/3)** → muafiyet adayı eklenmez, gönderme kapalı-başarısız; muafiyet
  ataması alanı okunamıyor → muafiyetli onay 403 `PAYMENT_SOD_WAIVER_FORBIDDEN` (AC-42, AC-43). MOD-0018 Treasury Auth
  ekinin DL-5'i main'de değilken (uç yok) aynı iki sonuç geçerlidir — muafiyet yolu kapalı (KARAR D64; D54 değişmedi). D66/1
  ile muafiyet adayı sorgusu DL-6 aday sorgusunun `waive-sod` + parti biçimidir; DL-5 yokken o biçim kapalı-başarısızdır ve
  DL-2 bu durumda kabul edilir (KARAR D66/3; AC-43 (h)).
- **Onaylayıcı aday sorgusu okunamıyor (KARAR D66/1)** (zaman aşımı, 5xx, 401 / 403, bozuk gövde, DL-6 ucu yok — anahtar
  çağrılarından herhangi birinde, `platform.workflow.tasks.approve` çağrısı dahil, KARAR D71) → Submit
  kapalı-başarısız: niyet yazılmaz, parti değişmez, MOD-0023 çağrısı yok. R1–R4 ve şirket kapsamı sonrası boş liste → Submit
  422 `PAYMENT_WORKFLOW_RESOLVER_NO_ELIGIBLE_APPROVER`; MOD-0023 çağrılmaz, niyet yazılmaz, parti değişmez (KARAR D68; AC-11).
  MOD-0023'ün 400 `WORKFLOW_ASSIGNMENT_CANDIDATES_REQUIRED`'ı yalnız savunma hattıdır (gelirse 502 `PAYMENT_WORKFLOW_START_FAILED`).
- **R4 uygulanan partide aday kesişimi (sahip denetimi 61466e504)** → yalnız `tier2-approve` (temel `approve` yok) ya da yalnız
  `approve` taşıyan kişi aday olmaz; iki anahtar çağrısından biri okunamazsa yukarıdaki kapalı-başarısız dal (niyet yok, parti
  değişmez, MOD-0023 çağrısı yok); kesişim boşsa R4 muafiyet adayı kuralı, o da boşsa yukarıdaki boş liste dalı (422, KARAR D68)
  (AC-11).
- **İş akışı izni kesişimi (KARAR D71; MOD-0018 eki K-A4b kapandı)** → modül onay anahtarını (tier-2 partide `approve` ve
  `tier2-approve`'u) taşıyıp `platform.workflow.tasks.approve` taşımayan kişi (onay anahtarı rol dışı elle verilmiş) aday olmaz;
  `platform.workflow.tasks.approve` çağrısı okunamazsa yukarıdaki kapalı-başarısız dal (niyet yok, parti değişmez, MOD-0023
  çağrısı yok); kesişim boşsa normal partide doğrudan, tier-2 partide R4 muafiyet adayı kuralından sonra yukarıdaki boş liste
  dalı (422 `PAYMENT_WORKFLOW_RESOLVER_NO_ELIGIBLE_APPROVER`, KARAR D68) (AC-11).
- **Release** → 200, `dispatchOutcome = NotConfigured`, `PaymentDispatchAttempt` yazılı, parti `Approved` kalır; bankaya
  ağ çağrısı yapılmaz.
- **Release'de R11 tier-2 şartı tutmuyor** (R4 uygulanan partide hiçbir onay `tier2-approve` izniyle ya da kayıtlı R4
  muafiyetiyle verilmemiş — ör. başka yüzeyden verilmiş karar; KARAR D77/2) → 403 `PAYMENT_TIER2_RELEASE_FORBIDDEN`; parti
  `Approved` kalır, `PaymentDispatchAttempt` yazılmaz (AC-19).
- **Port sözleşme ihlali** (sürüm, geçersiz istek, sözleşme dışı istisna) → 500 `PAYMENT_BANK_DISPATCH_CONTRACT_VIOLATION`,
  işlem geri alınır: deneme, sıra artışı, makbuz ve olay yok.
- **Release sırasında iptal** → işlem geri alınır, kayıt yok; geri alınan deneme anahtar rezerve etmez.
- **`ReleaseSequence` tükendi** (999 999 999) → 409 `PAYMENT_RELEASE_SEQUENCE_EXHAUSTED`, port çağrısı ve kayıt yok.
- **Beklenmeyen bağdaştırıcı sonucu** (`Accepted` / `Rejected` / `Unknown`) → 502 `PAYMENT_BANK_DISPATCH_OUTCOME_UNSUPPORTED`,
  deneme alınan sonuçla kayıtlı, parti `Approved` + `ReleaseBlockedForReview`; sonraki Release 409
  `PAYMENT_RELEASE_BLOCKED_FOR_REVIEW`, port çağrılmaz (D17/2).

## Authorization Convention

- Policy: `[Authorize]` (kiracı aktörü) + her aksiyonda `[HasPermission("…")]` (kaynakta MOD-0126 denetleyicilerinde
  `[HasPermission]` yok, paket §C — level-1 koşul 4).
- Biçim (PKS-001): küçük harf, noktalı, ≥ 3 parça, çok kelimede tire. **Ad alanı `finance-treasury.*` — KARAR D11/4**
  (procurement'ın `procurement.*` deseni); MOD-0018 kaydı (anahtarların Auth'a eklenmesi) ayrıca yapılır. Kaynak `mod0126.*` anahtarları **kopyalanmaz**; aşağıdaki eşleme yalnız izlenebilirlik içindir (kaynak anahtar
  hiçbir zaman yayımlanmadığından takma ad gerekmez — PKS-001 §3 yalnız yayımlanmış anahtar için).
- Aktör: `tenant_user`; `platform_admin` geçişi standarttır. Arka plan aktörü bu dilimde yok.

| Recovery anahtarı | Katman | Uçlar | Kaynak anahtar (`PermissionContracts.cs:5-22@da4abb20`) |
|---|---|---|---|
| `finance-treasury.payment-batches.read` | T1 | GET liste, GET detay | `mod0126.payment_batch.read` |
| `finance-treasury.payment-batches.read-tenant-wide` | T3 (yalnız açık atama, `ExplicitGrantOnlyPermissions`) | kiracıdaki tüm şirketlerin partileri, izi ve istisnaları (SEC-002 §3.5, D14/1) — **yalnız okuma**; hiçbir yazma komutunun kapsamını genişletmez (gap 4/G1) | — (Recovery eki) |
| `finance-treasury.payment-batches.create` | T1 | POST | `mod0126.payment_batch.propose` |
| `finance-treasury.payment-batches.update` | T1 | PUT | — (Recovery eki) |
| `finance-treasury.payment-batches.delete` | T1 | DELETE | — |
| `finance-treasury.payment-batches.bulk-delete` | T2 | DELETE bulk | — |
| `finance-treasury.payment-batches.validate` | T3 (sözlükte var) | validate | `mod0126.payment_batch.validate` |
| `finance-treasury.payment-batches.submit` | T3 | submit | `mod0126.payment_batch.submit_workflow` |
| `finance-treasury.payment-batches.reconcile` | T3 | workflow/reconcile | — |
| `finance-treasury.payment-batches.approve` | T2 | approve | `mod0126.payment_batch.approve` |
| `finance-treasury.payment-batches.tier2-approve` | T3 | approve (tier-2 parti, handler içi) | `mod0126.payment_batch.tier2_approve` |
| `finance-treasury.payment-batches.waive-sod` | T3 | approve-with-sod-waiver | `mod0126.payment_batch.waive_sod` |
| `finance-treasury.payment-batches.reject` | T2 | reject | `mod0126.payment_batch.reject` |
| `finance-treasury.payment-batches.request-evidence` | T3 | request-evidence | `mod0126.payment_batch.request_evidence` |
| `finance-treasury.payment-batches.release` | T3 | release | — |
| `finance-treasury.payment-trace.read` | T1 | trace | `mod0126.payment_trace.read` |
| `finance-treasury.payment-attestations.create` | T1 | attestations | `mod0126.payment_attestation.generate` |
| `finance-treasury.payment-evidence.link` | T3 (sözlükte var) | evidence-links | `mod0126.payment_evidence.link` |
| `finance-treasury.payment-evidence.read` | T1 | detay sekmesi | `mod0126.payment_evidence.read` |
| `finance-treasury.payment-exceptions.read` | T1 | GET payment-exceptions | (kaynakta `read` altında) |
| `finance-treasury.payment-sod-settings.read` | T1 | GET SoD ayarı (ekran dahil) — **KARAR D57/4** | — (Recovery eki) |
| `finance-treasury.payment-sod-settings.update` | T3 | PUT SoD ayarı — **KARAR D57/4** | — (Recovery eki) |

- **Taşınmayan kaynak anahtarlar:** `mod0126.audit-outbox.dispatch` (PP-11), `mod0126.payment_batch.admin_direct_proxy`
  (PP-05), `mod0126.payment_evidence.unlink` (kaynakta bildirilmiş, kullanılmıyor — paket §E), `mod0126.payment_evidence.evaluate`
  (değerlendirme akışı bu dilimde yok; O-16).
- Kullanıcının MOD-0023 tarafında da `platform.workflow.instances.start` (gönderen) ve `platform.workflow.tasks.approve` /
  `.reject` / `.request-info` (onaylayıcı) izinleri olmalıdır (`WorkflowModels.cs:14-19`); rol şablonları D46 + D47 (rol matrisi `treasury-intake-owner-decisions-05.md`) (O-5).
- Tier-3 eylemler (`read-tenant-wide`, `submit`, `reconcile`, `tier2-approve`, `waive-sod`, `request-evidence`,
  `release`, `payment-sod-settings.update` — D57/4; `escalate` v1 dışı — D22/1) bu pakette kayıtlıdır (PKS-001 §5 Tier 3). `read-tenant-wide` hiçbir rol şablonunda varsayılan
  değildir (O-5).
- **`waive-sod` yalnız açık atama (KARAR D47/1):** `finance-treasury.payment-batches.waive-sod` hiçbir rol şablonunda yer almaz
  (hazine yöneticisi dahil); `ExplicitGrantOnlyPermissions` listesindedir. Atama **süreli** (bitiş zamanı zorunlu), **gerekçe
  kodlu** ve **bağımsız onaylıdır** (atamayı onaylayan, izni alan kişi olamaz); **kendi kendine muafiyet yasaktır** — kişi
  izni kendine atayamaz / onaylayamaz ve muafiyetle onay R1–R3'ü hiçbir zaman kaldırmaz (partiyi oluşturan, gönderen ve son
  güncelleyen muafiyetle de karar veremez — R9, D12/1). Atama süresi, gerekçe kodu ve bağımsız onay Auth (MOD-0018) açık atama
  kaydında tutulur; Auth tarafı MOD-0018 Treasury Auth ekindedir (`…/MOD-0018-FU27-treasury-permission-onboarding.md` —
  anahtar ve açık atama DL-1, parti bağlı atama kaydı DL-5; Treasury yazar, MOD-0018 sahibi teyit eder — D9, D64). Muafiyetli her onay atlanamaz K2 olayı yazar
  (`…approved-with-sod-waiver`, Audited Events): gerekçe kodu, muafiyet izni atamasının bitişi, etkilenen parti, onaylayan
  aktör. Atamanın bitişi komut anında okunamıyorsa (alan Auth paket metninde tanımlanır — KARAR D56/3) muafiyetli onay
  kapalı-başarısızdır (403 `PAYMENT_SOD_WAIVER_FORBIDDEN`).
- **Muafiyet kuralları (KARAR D49/4) — çalışma anı ve Auth / süreç ayrımı.** Muafiyet tasarımı değişmez: tek uç
  (`approve-with-sod-waiver`), yalnız R4'ü kaldırır (R9, D12/1), izin + gerekçe + kanıt. D49/4'ün altı maddesi şöyle yerleşir:

  | D49/4 maddesi | Nerede denetlenir | Servisin yaptığı (çalışma anı, `ApprovePaymentBatchWithSodWaiverCommand`, MOD-0023 çağrısından önce) | Bağımlılık / süreç |
  |---|---|---|---|
  | Onaylayan: muafiyetten yararlanmayan, **Muafiyet yetkilisi** rolünü taşıyan kişi (**KARAR D74/3**; D49/4'teki "Hazine yöneticisi ya da K1" kalktı) | **Auth (MOD-0018) — atama onayı** | atamayı onaylayanın aktör kimliğini okur ve çağıranla, partiyi oluşturan / gönderen / son güncelleyenle (R1–R3 kişileri) aynı olmadığını denetler (aşağıda "yararlanamaz") | kimin onaylayabileceği Auth'un atama onayı akışıdır — servis rol adını denetlemez. Atama onayı izni `auth.payment-sod-waivers.assign`'dır (okuma `.read`); ikisi yalnız açık atamayla, yalnız Muafiyet yetkilisi rolü (MOD-0018 Treasury Auth eki DL-5, 27. rol; o paketin K-W1'i **KAPANDI — KARAR D74/2–3**; D9, D64); rolü taşımayan atayamaz (403), kendine atama 403; atayan hem izni hem Muafiyet yetkilisi rol üyeliğini taşır — izin başka role elle bağlansa da rolsüz kişi atayamaz (**KARAR D75/1**, Auth tarafı). "Hazine yöneticisi" rol şablonu `waive-sod`'u da atama onayı iznini de taşımaz |
  | Süre en çok 24 saat; bitişte kendiliğinden düşer | **Auth + servis** | atamanın bitişi komut anında gelecekte olmalı ve başlangıçtan en çok 24 saat sonra olmalı; değilse ya da bitiş / başlangıç okunamıyorsa 403 `PAYMENT_SOD_WAIVER_FORBIDDEN` (kapalı-başarısız) | 24 saat üst sınırı atama anında ve kendiliğinden düşme (bitişte iznin token'a girmemesi) Auth'undur; servis yalnız kullanım anında ikinci kontroldür |
  | Gerekçe kodları `ACIL-ODEME`, `TEK-YETKILI`, `DENETIM-ISTEGI` | **servis** (kullanım) + **Auth** (atama) | komutun `ReasonCode`'u kapalı listede olmalı; değilse 400 `PAYMENT_SOD_WAIVER_REASON_REQUIRED` (Validation Rules) | atamanın kendi gerekçe kodunun aynı listeden seçilmesi Auth'un atama formudur |
  | Kapsam yalnız ilgili tek ödeme partisi | **servis** (karar başına) + **Auth** (atamanın partiye bağlanması) | her muafiyet tek partide tek karardır (rota `{id}`); muafiyet saklanıp başka partiye taşınmaz. **KARAR D54:** servis atamanın hedef parti kimliğini okur ve rotadaki partiyle karşılaştırır; hedef parti yoksa, okunamıyorsa ya da farklıysa 403 `PAYMENT_SOD_WAIVER_FORBIDDEN` (kapalı-başarısız; yazım ve MOD-0023 çağrısı yok) | atamanın tek partiye bağlanması (hedef parti kimliği alanı) Auth'undur ve zorunludur (D54) — **bağımlılık: MOD-0018 Treasury Auth eki DL-5 (KARAR D64)**; DL-5 main'e girene kadar muafiyetli onay 403 ile kapalıdır. "Tek parti" süreç denetimine bırakılmaz |
  | Her kullanım 5 iş günü içinde K1 incelemesine düşer | **süreç** | her kullanım atlanamaz K2 olayı yazar (`…approved-with-sod-waiver`, Audited Events) — incelemenin girdisi budur | inceleme, süre takibi ve sonucu servis özelliği değildir; K1 süreci (denetim raporu / MOD-0018 atama geçmişi). Servis inceleme kaydı tutmaz |
  | Onaylayan muafiyetten yararlanamaz (kendi kendine muafiyet yok) | **servis** + **Auth** | çağıran = atamayı onaylayan → 403 `PAYMENT_SOD_WAIVER_FORBIDDEN`; atamayı onaylayan partiyi oluşturan / gönderen / son güncelleyen ise → aynı 403; R1–R3 kişileri muafiyetle karar veremez (R9, mevcut) | kişinin izni kendine atayamaması / onaylayamaması Auth'undur |

  **Auth'tan okunacak atama alanları (bağımlılık, MOD-0018 Treasury Auth eki DL-5 — KARAR D64; Treasury yazar, MOD-0018
  sahibi teyit eder):** bitiş (mevcut madde), başlangıç, atamayı onaylayanın aktör
  kimliği, gerekçe kodu ve hedef parti kimliği (zorunlu, KARAR D54). **KARAR D56/3:** alanlar o ekin DL-5 paket metninde
  tanımlanır (yanıt alanları o paketin Bölüm 8.5'i) — "— DL-2 için" dilim kapısı; kodu DL-2 kabulünü **beklemez** (**KARAR
  D66/3**, D56/3'teki "DL-5 kodu main'de = DL-2 kabul kapısı" kuralının yerine): kod main'e girene kadar muafiyet yolu 403
  `PAYMENT_SOD_WAIVER_FORBIDDEN` ile kapalıdır (D54) ve bunu kanıtlayan test (AC-43 (h)) DL-2 kabulü için yeterlidir; muafiyet
  DL-5 gelince açılır ve ayrıca kabul edilir (Ready-for-dev Checklist). Okuma portu
  `IPaymentSodWaiverAssignmentSource` (Repo Scope). Okunamayan ya da bulunmayan alan kullanım anında kapalı-başarısızdır (403).
  Hedef parti kimliği Auth'ta taşınmadıkça muafiyetli onay kapalı kalır (D54); servis bu alanı kendi tarafında tutmaz (yeni
  servis varlığı açılmaz). **Muafiyet adayı kaynağı (KARAR D56/3):** gönderme çözümleyicisi muafiyet adaylarını (R4) Auth'un
  "bu partiye geçerli `waive-sod` ataması olanlar" sorgusundan okur — **D66/1 ile tek aday sorgusu:** MOD-0018 Treasury Auth
  ekinin DL-6 aday sorgusunun `waive-sod` + parti biçimi (parti bağı DL-5 kaydından, D64; port `IFinanceTreasuryApproverCandidateSource`).
- **Onaylayıcı aday tabanı (KARAR D66/1):** normal ve tier-2 onaylayıcı adayları Auth aday sorgusundan gelir (Validation Rules →
  "Aday tabanı"); rol adı ya da yapılandırma listesi aday kaynağı değildir. Tier-2 aday, Approve ucunun `[HasPermission]`
  izni (`finance-treasury.payment-batches.approve`) ile işleyici içi `finance-treasury.payment-batches.tier2-approve`'u
  **birlikte** taşıyan kişidir (kesişim; sahip denetimi 61466e504) — aday olup kararı uygulayamayan kişi listeye girmez.
  `platform.workflow.tasks.approve` / `.reject` onaylayıcı rollere rol şablonuyla gelir (**KARAR D70/1**; MOD-0018 eki K-4 (b), K-A4 kapandı).
  **İş akışı izni kesişimi (KARAR D71; MOD-0018 eki K-A4b KAPANDI — KARAR D71):** aday listesi MOD-0023 görev kararının izni
  `platform.workflow.tasks.approve`'a sahip kullanıcılarla da kesiştirilir (DL-6 sorgusuna ayrı çağrı; tier-2 kesişimiyle aynı
  desen): normal partide `approve ∩ platform.workflow.tasks.approve`, tier-2 partide `approve ∩ tier2-approve ∩
  platform.workflow.tasks.approve`. Onay anahtarı rol dışı elle verilmiş, iş akışı izni olmayan kişi aday olmaz; çağrı okunamazsa
  gönderme kapalı-başarısız; kesişim boşsa 422 `PAYMENT_WORKFLOW_RESOLVER_NO_ELIGIBLE_APPROVER` (D68). Kesişimi servis yapar;
  Auth sorgusu tek anahtar alır (MOD-0018 eki Bölüm 8.6).
- **SoD ayarı izinleri (KARAR D56/4; anahtarlar KARAR D57/4):** `GetPaymentSodSettingsQuery` →
  `finance-treasury.payment-sod-settings.read` (T1), `UpdatePaymentSodSettingsCommand` →
  `finance-treasury.payment-sod-settings.update` (T3); ikisi de **Hazine yöneticisi** rol şablonunda (D47 rol matrisi, D57/4;
  açık atama listesinde değil). Anahtarlar MOD-0018 Treasury Auth ekinin DL-1 paket metnindedir (kapsam (f); Treasury yazar,
  MOD-0018 sahibi teyit eder) — DL-2 için (Ready-for-dev Checklist). Ekranda (DL-4) `.read` yoksa kabuk içinde erişim açıklaması (UAS-001), `.update` yoksa salt okunur görünüm.

## Audited Events

Yol `b`: `Diten.BuildingBlocks.CentralAudit` ortak ileticisi (AUD-001 §5 K4) — komut `ICentralAuditDeclaration` taşır,
servis `AddCentralAudit<FinanceTreasuryCentralAuditScope>(…, RequireEnlistment = true)` kaydeder, her depo işlemi olayı
**aynı Mongo işlemine** yazar (`CentralAuditTransaction.EnlistAsync`): iş verisi ve merkezi kayıt birlikte commit olur ya
da hiçbiri. Servisin denetim altyapısı **bugün yok** (servis yok): kurulum DL-1'in ilk adımıdır. Defter için CT kararı
**verildi** — `treasury-intake-owner-decisions-02.md` D35 (PR #235): D35/1 iz `finance-treasury-merkezi-iletim | b | işaret |
ICentralAuditDeclaration`; D35/2 borç ve istisna sıfır (`PinnedCounts` sıfır); D35/4 defter dosyası
`tests/architecture/audit-ledger/Diten.FinanceTreasuryService.md`, `CT-DECISIONS.md` satırı ve `AuditTrailStandardTests`
`AcceptedTrails` / `PinnedCounts` DL-1 değişikliğinde birlikte (Repo Scope). Kaynak MOD-0021 aktarımı (kapalı, FailClosed —
paket §C, §L.2) taşınmaz.

`Category`: Platform `AuditCategory`'de finans değeri yok (`services/Diten.Platform/src/Diten.Platform.Domain/Enums/AuditEnums.cs:3-27`);
**O-6 → KARAR D35/3:** yalnız ekleme olarak `Finance` değeri (değer 18; Y-4/7) — 17 main'de ESignature (MOD-0022) için kullanılıyor;
Platform enum'u ve `Diten.BuildingBlocks.CentralAudit/CentralAuditPlatformValues.cs` kopyası birlikte güncellenir, değer main'e
girince Platform haber verir (Platform sahibinin işi, XMC-001 / D9; `MasterData` ara çözümü
seçilmedi). **MOD-0126 DL-1 bu değer main'e girmeden başlamaz**; o zamana kadar değer uydurulmaz.
`SourceModule`: `MOD-0126`. `EntityType`: `PaymentBatch` (aksi yazılmadıkça).

| Komut | Olay adı | Nesne (tür) | Yol | Önceki/sonraki ya da değişen alanlar | K2 (fail-closed) | İstisna sınıfı + gerekçe |
|---|---|---|---|---|---|---|
| CreatePaymentBatchCommand | finance-treasury.payment-batch.created | PaymentBatch | b | AfterState alan adları: BatchCode, Status, CurrencyCode, TotalAmount, PaymentCount, RequestedValueDate | evet (öneri) | — |
| UpdatePaymentBatchCommand | finance-treasury.payment-batch.updated | PaymentBatch | b | değişen alan **adları** (değer değil) | evet (öneri) | — |
| DeletePaymentBatchCommand | finance-treasury.payment-batch.deleted | PaymentBatch | b | BeforeState: BatchCode, Status | evet (öneri) | — |
| BulkDeletePaymentBatchCommand | finance-treasury.payment-batch.bulk-deleted | PaymentBatch | b | silinen kimlik sayısı; her kimlik için ayrı varlık kaydı (CentralAudit tek varlık kimliği taşır — çoklu kimlik biçimi DL-1'de ölçülür) | evet (öneri) | — |
| ValidatePaymentBatchCommand | finance-treasury.payment-batch.validated | PaymentBatch | b | Status Draft→Validated, sorun sayısı | evet (öneri) | — |
| SubmitPaymentBatchCommand | finance-treasury.payment-batch.submitted (sonuç, tx2/Reconcile) | PaymentBatch | b | Status Submitted→PendingApproval, WorkflowInstanceId, SodPolicyVersion, IsTier2 (tx1 deneme olayı `…workflow-start-requested`: Status →Submitted, uzak anahtar) | evet (öneri) | — |
| ReconcilePaymentBatchWorkflowCommand | kapattığı niyetin **sonuç** olayı (geçiş matrisi; ör. `…submitted`, `…decision-cancelled`); niyet değişmediyse olay yok | PaymentBatch | b | niyet/karar durumu önce→sonra; bağlı makbuz yalnız `Pending` ise `Completed` | evet (öneri) | — |
| ApprovePaymentBatchCommand | finance-treasury.payment-batch.approved | PaymentBatch | b | karar kimliği, SodResult, Status | **evet — K2 (D11/3)** | — |
| ApprovePaymentBatchWithSodWaiverCommand | finance-treasury.payment-batch.approved-with-sod-waiver | PaymentBatch | b | karar kimliği, SodResult=Waived, kaldırılan kural kodları, muafiyet gerekçe kodu, kanıt sayısı; **KARAR D47/1:** muafiyet izni atamasının bitişi, etkilenen parti (`EntityId`), onaylayan aktör (`ActorId`); **KARAR D49/4:** atamanın başlangıcı, atamayı onaylayanın aktör kimliği, atamanın gerekçe kodu; **KARAR D54:** atamanın hedef parti kimliği — olay K1'in 5 iş günlük kullanım sonrası incelemesinin girdisidir | **evet — K2 (D11/3)** | — |
| RejectPaymentBatchCommand | finance-treasury.payment-batch.rejected | PaymentBatch | b | karar kimliği, ReasonCode, Status | **evet — K2 (D11/3)** | — |
| RequestPaymentBatchEvidenceCommand | finance-treasury.payment-batch.evidence-requested | PaymentBatch | b | karar kimliği, Status | evet (öneri) | — |
| LinkPaymentBatchEvidenceCommand | finance-treasury.payment-batch.evidence-linked | PaymentBatch | b | bağlantı kimlikleri | evet (öneri) | — |
| GeneratePaymentAttestationCommand | finance-treasury.payment-attestation.generated | PaymentAttestation | b | ContentHash, SchemaVersion | evet (öneri) | — |
| ReleasePaymentBatchCommand | finance-treasury.payment-batch.release-attempted | PaymentBatch | b | DispatchAttempt kimliği, Outcome=NotConfigured | evet (öneri) | — |
| UpdatePaymentSodSettingsCommand (**KARAR D56/4**) | finance-treasury.payment-sod-settings.updated | PaymentSodSettings (`EntityType`) | b | değişen alan **adları** (eşik tutarı / para birimi, R4 anahtarı — R1–R3 kapatılamaz, D57/4) ve `PolicyVersion` önce → sonra | evet (öneri) | — |

- **İki aşamalı komutların deneme olayları (Runtime Constraints 4a):** Submit → `finance-treasury.payment-batch.workflow-start-requested`
  (tx1) + sonuç `…submitted` ya da `…workflow-start-refused` (tx2 / Reconcile); Approve / ApproveWithSodWaiver / Reject /
  RequestEvidence → `finance-treasury.payment-batch.decision-requested` (tx1, karar türü alanda) + yukarıdaki
  sonuç olayı (tx2 / Reconcile). Release bu dilimde tek aşamalıdır (aşağıda) ve yalnız `…release-attempted` yazar. Deneme olayı tek başına
  "yapıldı" anlamına gelmez; tabloda yazılan olay **sonuç** olayıdır. K2 kararlarında deneme olayı yazılamazsa uzak çağrı
  yapılmaz. Kapalı ama olumsuz sonuçların olayları (Owned Objects geçiş matrisi): `…workflow-start-refused`,
  `…workflow-start-cancelled`, `…decision-refused`, `…decision-cancelled`. Reconcile kendi tek olayını
  (`…workflow-reconciled`) değil, kapattığı niyetin **sonuç** olayını yazar; niyet değişmediyse olay yok.
- **Kayda yazılmayan:** banka referans değerleri, alacaklı referansları, tutarların satır ayrıntısı, yorum serbest metni,
  token. Değişen alanların yalnız **adı** yazılır (AUD-001 §3).
- **K2 (sahip kararı D11/3, O-6):** Approve / ApproveWithSodWaiver / Reject — para çıkışına yetki veren kararlar; ödeme onayı
  AUD-001 §4.3 listesinde açıkça sayılmıyor (kimlik, yetki, kiracı durumu, GxP, KVKK-özel). Öneri: K2 sayılsın. Uyulabilir
  yol bugün var: `RequireEnlistment = true` ile kayıt iş işlemiyle aynı Mongo işlemine yazılır; outbox yazımı başarısızsa
  işlem geri alınır ve komut başarısız olur (SupplyChain kabulü, `CT-DECISIONS.md` 2026-10-21 satırı "commit'ten önce aynı
  işleme yazar (K2)"). Diğer yazma komutları da aynı yolla yazıldığı için pratikte hepsi kapalı-başarısızdır.
- **Bilinen boşluk G-AUD-1:** `CentralAuditBehavior` başarısız komut için kayıt bırakmaz (`ICentralAuditDeclaration.cs`
  özet yorumu: "A failed command writes nothing"); AUD-001 §3 reddedilen komutun da (`Outcome = Denied`) kaydını ister.
  SoD reddi, MOD-0023 kesin reddi ve yetkisiz denemeler bu dilimde yalnız modülün kendi izinde görünür
  (`PaymentException`, `PaymentApprovalDecision.State`, `PaymentWorkflowStartIntent`). Merkezi ret kaydı CentralAudit
  sahibinin (BL-501) işidir — follow-up; bu paket kendi iletim kopyasını yazmaz.
- **Okuma denetimi:** gerekmez (§7'deki üç sınıfa girmiyor; liste dışa aktarımı bu dilimde yok).
- **Arka plan işi yok:** uzlaştırma süpürücüsü bu dilimde yazılmaz (servis kimliği engeli), bu yüzden İ2 satırı yok.

## Gateway / API Routing Decision

- **Karar: Gateway değişikliği gerekli.** `ocelot.json`'da `/api/payment-batches` ve `/api/payment-exceptions` yok (yeni
  servis). Gerekli rotalar: `/api/payment-batches`, `/api/payment-batches/{everything}`, `/api/payment-exceptions`,
  `/api/payment-exceptions/{everything}` → `localhost:5071`; `UpstreamHttpMethod` GET, POST, PUT, PATCH, DELETE, **OPTIONS**;
  explicit rotalar catch-all'dan önce (`routes.md`).
- `ocelot.json` korunur: **integration-agent task'ı** olarak ayrı yürütülür; önkoşul AGENTS.md §3 / `ports.md` 5071 kaydı
  (yapıldı — D30, 2026-10-25).
- Kaynak rotaları (`/api/v1/mod0126/*` → 5058, kaynak `gateway/DitenApiGateway/Diten.ApiGateway/ocelot.json:1142-1174@da4abb20`)
  taşınmaz (paket §A).
- Servisten MOD-0023 (`/api/v1/workflow/*`), MOD-0220 (`/api/legal-entities/*`), MOD-0031 (`/api/v1/evidence/*`) ve
  MOD-0048 (`/api/lookups/reference/*` ve `payment-method` için `/api/lookups/reference-data/consumable-sets/*`) çağrıları ve veri kapsamı tüketimi (SEC-002, O-19) Gateway üzerinden ya da ortamın
  servis-servis adresinden; adresler yapılandırmadan, eksikse servis başlamaz. Bu rotaların Gateway'de var olduğu DL-1/DL-2
  başında ölçülür.

## Acceptance Criteria

AC durumu kabul kaydında (`scripts/acceptance_record.py`) tanımlı / uygulanmış / çalıştırılmış / geçmiş olarak izlenir,
pack'te asla izlenmez; bu pack'teki her AC yalnız "tanımlı"dır.

- [ ] AC-1 (zorunlu) — `POST /api/payment-batches` geçerli istekle 201 döner; parti `Draft`, `BatchCode` RCS-001 biçiminde
  ortak servisten (istekte gönderilen kod 400), `TotalAmount` ve `PaymentCount` sunucu hesabı; `TenantId`/`LegalEntityId`
  sunucuda yazılı, yanıt DTO'sunda yok.
- [ ] AC-2 (zorunlu) — Ödeyen şirket `OperationalStatus = Active` ve `ApprovalStatus = Approved` değilse Create, Submit,
  her karar ve Release 422 `PAYMENT_LEGAL_ENTITY_NOT_ELIGIBLE` döner ve kayıt değişmez; kontrol her çağrıda MOD-0220
  `lookup-validation`'dan yapılır (D10/1). MOD-0220 erişilemezse 503 ve geçiş yok.
- [ ] AC-3 (zorunlu) — Başka kiracının ya da başka şirketin partisine okuma, güncelleme, silme, karar ve Release 404 döner.
- [ ] AC-4 (zorunlu) — Partiyi oluşturan kullanıcının Approve / Reject / RequestEvidence çağrısı 403
  `PAYMENT_SOD_DENIED` (`FailedSelfApproval`) döner ve MOD-0023'e hiçbir istek gitmez (sahte istemciyle sayılır).
- [ ] AC-5 (zorunlu) — Gönderen kullanıcının ve gönderme anındaki son güncelleyenin kararı 403 `PAYMENT_SOD_DENIED`
  (`FailedSubmitterApproval` / `FailedUpdaterApproval`).
- [ ] AC-42 (zorunlu) — (önceki kimlik AC-6a; §7 tam sayı kimlik) Muafiyet adayı (D13): R1–R3 dışlamasından sonra tier-2 aday yoksa Auth'un "bu partiye geçerli `waive-sod`
  ataması olanlar" sorgusundan (**KARAR D56/3**; sahte Auth istemcisi) dönen kişiler `IsWaiverCandidate = true` ile aday olur
  ve yalnız muafiyetli onay verebilir (normal Approve 403); tier-2 aday varken muafiyet adayı eklenmez; sorgu okunamazsa aday
  eklenmez ve gönderme kapalı-başarısızdır (üç ayrı test). Sabotaj: aday kaynağı izin listesine ya da MOD-0023 atamasına
  çevrildiğinde (bu partiye bağlı olmayan atama aday olur) test kırmızıya döner. **D66/1:** muafiyet adayı sorgusu aday sorgusunun
  `waive-sod` + parti biçimidir (aynı port, istemcinin aldığı anahtar ve parti doğrulanır). **D66/3:** DL-2 kabulünde "sorgu
  okunamaz / DL-5 yok → aday eklenmez, gönderme kapalı-başarısız" dalı kanıtlanır; aday eklenen dallar muafiyet açılış kabulünde
  gerçek DL-5 ile koşulur.
- [ ] AC-6 (zorunlu) — R4 uygulanan partide (`IsTier2` ∧ sabitlenmiş `R4Enabled = true`) `tier2-approve` izni olmayan onaylayıcı 403 `PAYMENT_TIER2_APPROVER_REQUIRED`
  alır; izni olan geçer. Sabitlenmiş `R4Enabled = false` partide aynı onaylayıcı bu kodu almaz (AC-44 (d)).
- [ ] AC-7 (zorunlu) — Gönderme çözümleyicisi ile karar kontrolü **aynı** eşik nesnesini kullanır: kodda tek eşik tipi
  vardır (mimari testi: `Tier2` eşiği taşıyan ikinci bir yapılandırma tipi/özelliği yok), değer partiye ve niyete
  sabitlenir, politika sürümü her kararda kayıtlıdır. Kiracı ayarı yoksa her onay tier-2 sayılır
  (`SodPolicySource = DefaultAllTier2`, D11/1) ve servis normal açılır; ayar deposu erişilemezse Submit/karar 503
  `PAYMENT_SOD_POLICY_UNAVAILABLE` (iki durum ayrı testtir).
- [ ] AC-8 (zorunlu) — Parti para birimi eşik para biriminden farklıysa parti tier-2 sayılır (O-4 farklı karar verirse bu
  madde o kararla yeniden yazılır, numarası korunur).
- [ ] AC-9 (zorunlu) — Muafiyetli onay: `waive-sod` izni yok → 403 `PAYMENT_SOD_WAIVER_FORBIDDEN`; gerekçe yok → 400
  `PAYMENT_SOD_WAIVER_REASON_REQUIRED`; kanıt yok → 422 `PAYMENT_SOD_WAIVER_REQUIRED`; üçü varsa yalnız R4 hatası kaldırılır
  ve karar `Waived` + kaldırılan kural kodu (`R4`) ile kaydedilir. R1/R2/R3 hatasında muafiyet istemi 403
  `PAYMENT_SOD_WAIVER_NOT_ALLOWED` (D12/1); Release aynı modeli uygular (R11). Gerekçe kodu `ACIL-ODEME` / `TEK-YETKILI` /
  `DENETIM-ISTEGI` dışında bir değerse 400 `PAYMENT_SOD_WAIVER_REASON_REQUIRED` (KARAR D49/4; sabotaj: liste kontrolü
  kaldırıldığında serbest metin gerekçeyle muafiyet kaydedilir ve test kırmızıya döner). **İş akışı izni (KARAR D71):** muafiyet adayı da
  `platform.workflow.tasks.approve` ile kesişir: sahte Auth `waive-sod` + parti için {W, X}, iş akışı için {X} → yalnız X eklenir;
  sabotaj: kesişim kaldırılınca W aday olur ve test kırmızıya döner.
- [ ] AC-43 (zorunlu) — **Muafiyet ataması kullanım anı denetimi (KARAR D49/4):** `waive-sod` izni olan çağıranın atama
  kaydında (Auth'tan okunan alanlar — Auth paket metninde tanımlı, KARAR D56/3) (a) bitiş geçmişse, (b) başlangıç–bitiş aralığı 24 saati aşıyorsa, (c) atamayı onaylayan
  çağıranın kendisiyse, (d) atamayı onaylayan partiyi oluşturan / gönderen / son güncelleyen ise, (e) atamanın hedef partisi
  rotadaki partiyle eşleşmiyorsa, (f) bu alanlardan biri okunamıyorsa, (g) atamada hedef parti kimliği yoksa (KARAR D54)
  muafiyetli onay 403 `PAYMENT_SOD_WAIVER_FORBIDDEN` döner; karar, niyet ve MOD-0023 çağrısı yoktur, parti değişmez (yedi ayrı
  test). Geçerli atamada karar `Waived` kaydedilir ve `…approved-with-sod-waiver` olayı atamanın başlangıcını, bitişini,
  onaylayanını, gerekçe kodunu ve hedef parti kimliğini taşır. **Parti bağı sabotajı (KARAR D54):** A partisine bağlı aynı atama
  A partisinde geçerli muafiyetli onay verdikten sonra B partisinde denenir → ikinci çağrı 403 `PAYMENT_SOD_WAIVER_FORBIDDEN`
  ile herhangi bir yazımdan (karar, niyet, makbuz) ve MOD-0023 çağrısından önce reddedilir; hedef partisiz atama ilk çağrıda aynı
  403'ü alır. Sabotaj: her kontrol tek tek kaldırıldığında ilgili test kırmızıya döner — parti karşılaştırması kaldırılınca
  B partisi onaylanır ve test kırmızıya döner. "Tek parti" süreç denetimine bırakılmaz. **(h) Muafiyet yolu kapalı kanıtı
  (KARAR D66/3):** Auth atama bağdaştırıcısı DL-5 ucunu bulamadığında (404 / bağlantı reddi / bağdaştırıcı kayıtlı değil)
  muafiyetli onay 403 `PAYMENT_SOD_WAIVER_FORBIDDEN` döner; karar, niyet, makbuz ve MOD-0023 çağrısı yoktur. DL-2 bu testle
  kabul edilir; (a)–(e) ve parti bağı sabotajı muafiyet açılış kabulünde gerçek DL-5 ile koşulur. Sabotaj: kapalı yolda
  "atama okunamadı → izinle geç" dalı eklendiğinde (h) kırmızıya döner. **(i) R4 kapalı partide muafiyet (KARAR D77/1):**
  sabitlenmiş `R4Enabled = false` sürümüyle gönderilmiş, eşiği aşan (`IsTier2 = true`) partide `waive-sod` izni, geçerli
  gerekçe kodu, en az 1 kanıt ve sahte Auth atama kaynağından bu partiye bağlı geçerli atama olan, R1–R3 dışı çağıranın
  `ApprovePaymentBatchWithSodWaiverCommand`'ı 403 `PAYMENT_SOD_WAIVER_FORBIDDEN` alır; karar, niyet, makbuz ve denetim olayı
  yazılmaz, MOD-0023 çağrılmaz, parti değişmez. Sabotaj: R4 kapalı kontrolü kaldırıldığında komut işlenir (karar `Waived`
  kaydedilir ya da MOD-0023 çağrılır) ve test kırmızıya döner. Sahte atama kaynağıyla koşulduğu için DL-2'de kanıtlanır (DL-5
  gerekmez). **(j) R4 açık, tier-2 olmayan partide muafiyet (KARAR D78):** sabitlenmiş `R4Enabled = true` sürümüyle gönderilmiş,
  eşiğin altındaki (`IsTier2 = false`) partide `waive-sod` izni, geçerli gerekçe kodu, en az 1 kanıt ve sahte Auth atama
  kaynağından bu partiye bağlı geçerli atama olan, atanmış ve R1–R3 dışı çağıranın `ApprovePaymentBatchWithSodWaiverCommand`'ı
  403 `PAYMENT_SOD_WAIVER_FORBIDDEN` alır; `Waived` kaydı oluşmaz, karar, niyet, makbuz ve denetim olayı yazılmaz, MOD-0023
  çağrılmaz, parti değişmez. Sabotaj: `IsTier2` denetimi kaldırıldığında (yalnız `R4Enabled` aranır) komut işlenir (karar
  `Waived` kaydedilir ya da MOD-0023 çağrılır) ve test kırmızıya döner. Sahte atama kaynağıyla koşulduğu için DL-2'de kanıtlanır
  (DL-5 gerekmez).
- [ ] AC-10 (zorunlu) — Aktör karşılaştırması Guid üzerindendir: aynı kullanıcının büyük/küçük harf farklı yazılmış `sub`'ı
  aynı kişi sayılır; Guid olmayan `sub` ile karar reddedilir.
- [ ] AC-11 (zorunlu) — Submit, MOD-0023'ü çağırmadan önce `PaymentWorkflowStartIntent(Pending)` ve parti `Submitted`'ı
  commit eder; uzak çağrı niyetteki `IdempotencyKey` ile yapılır. Başlatma isteği `TemplateCode =
  FT-PAYMENT-BATCH-APPROVAL` (Y-12) ve çözümleyicinin R1–R4 sonrası adaylarını (`PaymentWorkflowStartIntent.CandidatePrincipalIds`)
  `CandidatePrincipalIds` olarak taşır; sahte MOD-0023 gelen isteği kaydeder ve iki alan ayrı ayrı doğrulanır. Sabotaj: şablon
  kodu değiştirildiğinde ya da aday listesi istekten çıkarıldığında test kırmızıya döner. **Aday tabanı (KARAR D66/1; sahte Auth
  aday istemcisi):** normal partide `finance-treasury.payment-batches.approve` anahtarıyla, `IsTier2` partide hem `.approve` hem
  `.tier2-approve` anahtarıyla ayrı ayrı sorulur (istemcinin aldığı anahtarlar doğrulanır) ve tier-2 aday tabanı iki yanıtın
  **kesişimidir** (sahip denetimi 61466e504); Auth'un döndürdüğü kişilerden partiyi oluşturan, gönderen ve son güncelleyen
  ile şirket kapsamı dışındaki kişi (MOD-0018 eki K-A2) `CandidatePrincipalIds`'e girmez; sorgu okunamazsa (zaman aşımı, 5xx,
  403, bozuk gövde) niyet yazılmaz, parti değişmez ve MOD-0023 çağrılmaz (dört ayrı test). Sabotaj: çözümleyici Auth sonucunu
  atlayıp sabit / yapılandırma listesi kullandığında ya da R1 dışlaması kaldırıldığında (oluşturan aday olur) test kırmızıya döner.
  **Tier-2 kesişimi:** sahte Auth `approve` için {B, C}, `tier2-approve` için {A, B} döndürür → `CandidatePrincipalIds = {B}`
  (yalnız `tier2-approve` taşıyan A dışlanır, ikisini taşıyan B geçer, yalnız `approve` taşıyan C dışlanır); `approve` için {C},
  `tier2-approve` için {A} → kesişim boş, "tier-2 aday yok" sayılır ve AC-42'nin muafiyet adayı dalına girer; `tier2-approve`
  çağrısı başarılı, `approve` çağrısı okunamaz olduğunda niyet yazılmaz ve MOD-0023 çağrılmaz. Sabotaj: kesişim kaldırılıp
  tier-2 partide yalnız `tier2-approve` sorgulandığında A aday olur ve test kırmızıya döner. **İş akışı izni kesişimi (KARAR D71;
  sahte Auth anahtar başına ayrı yanıt):** istemcinin `platform.workflow.tasks.approve` anahtarını da ayrı çağrıyla sorduğu
  doğrulanır (yukarıdaki dallarda sahte iş akışı yanıtı oradaki bütün kişileri içerir). Normal partide `approve` için {D, E},
  `platform.workflow.tasks.approve` için {E} → `CandidatePrincipalIds = {E}` (onay anahtarı olup iş akışı izni olmayan D dışlanır,
  ikisini taşıyan E geçer); tier-2 partide `approve` ve `tier2-approve` için {B, F}, `platform.workflow.tasks.approve` için {B} →
  `{B}` (F dışlanır); `approve` çağrısı başarılı, `platform.workflow.tasks.approve` çağrısı okunamaz (zaman aşımı, 5xx, 403, bozuk
  gövde) olduğunda niyet yazılmaz, parti değişmez ve MOD-0023 çağrılmaz (kapalı-başarısız); normal partide `approve` için {D},
  `platform.workflow.tasks.approve` için {} → aşağıdaki boş liste dalı (422). Sabotaj: iş akışı kesişimi kaldırıldığında D (tier-2
  partide F) aday olur ve test kırmızıya döner. **Boş liste (KARAR D68):** kurallar ve muafiyet
  sonrası aday kalmazsa Submit 422 `PAYMENT_WORKFLOW_RESOLVER_NO_ELIGIBLE_APPROVER`, MOD-0023 sahte istemcisine hiç çağrı gitmez,
  niyet yazılmaz, parti `Draft` kalır; sabotaj: boş liste MOD-0023'e gönderildiğinde (çağrı sayısı 1) test kırmızıya döner.
- [ ] AC-12 (zorunlu) — Başlatma zaman aşımı, bağlantı kopması ve boş/okunamayan 2xx yanıtı 202
  `PAYMENT_WORKFLOW_START_OUTCOME_UNKNOWN` üretir; niyet `Unknown`, parti `Submitted`; ret istisnası yazılmaz.
- [ ] AC-13 (zorunlu) — Açık niyet varken **aynı** anahtarla Submit makbuz kuralına uyar (makbuz `Completed` → kayıtlı yanıt,
  202 belirsiz dahil; `Pending` → 202 `PAYMENT_REQUEST_IN_PROGRESS`, kurtarma yok); **farklı**
  anahtarla Submit 409 `PAYMENT_WORKFLOW_START_PENDING` ve uzak çağrı yok; Reconcile aynı anahtarla ilerler ve MOD-0023'te
  aynı parti için ikinci örnek oluşmaz (sahte MOD-0023 anahtar başına tek örnek sayar).
- [ ] AC-14 (zorunlu) — Kesin başlatma reddi 502 `PAYMENT_WORKFLOW_START_FAILED`, niyet `Refused`, parti önceki durumuna
  (Draft/Validated) döner.
- [ ] AC-15 (zorunlu) — Kararlar yerel `Requested` niyetinden sonra MOD-0023'e karar idempotency anahtarıyla gider;
  belirsiz sonuç 202 `PAYMENT_DECISION_OUTCOME_UNKNOWN`, Reconcile kapatır.
- [ ] AC-16 (zorunlu) — Idempotency (D12/2): her yazma komutu anahtarsız 400 `PAYMENT_IDEMPOTENCY_KEY_REQUIRED`; aynı
  kapsam (kiracı, şirket, aktör, işlem, hedef, anahtar) + aynı yük, makbuz `Completed` ise kayıtlı yanıtı, `Pending` ise
  202 `PAYMENT_REQUEST_IN_PROGRESS` döndürür (replay kurtarma yapmaz; kurtarma yalnız Reconcile komutuyla) ve ikinci kayıt, ikinci uzak çağrı
  ya da ikinci merkezi denetim olayı oluşmaz; aynı kapsam + farklı yük 409; aynı anahtar başka aktörde ayrı makbuzdur.
  **İlk yanıtın kaybolduğu** senaryo (istemci yanıtı almadan aynı istekle tekrar) testte ayrıca koşulur.
- [ ] AC-17 (zorunlu) — Merkezi denetim: tek aşamalı komutlar (Create, Update, Delete, Validate, kanıt, onay belgesi)
  tam **bir** olay bırakır (Release dahil — bu dilimde tek işlem); iki aşamalı komutlar (Submit, kararlar) tx1'de tam bir **deneme** olayı ve sonuç
  kesinleştiğinde tx2'de (ya da Reconcile'da) tam bir **sonuç** olayı bırakır; idempotent tekrar yeni olay üretmez. Olay
  iş verisiyle aynı Mongo işleminde yazılır; outbox yazımı zorla başarısız edildiğinde komut başarısız olur ve iş verisi
  yazılmaz (sabotaj testi); uzak başarı sonrası tx2 zorla başarısız edildiğinde niyet açık kalır ve Reconcile uzak çağrıyı
  tekrarlamadan sonuç olayını yazar (kurtarma testi).
- [ ] AC-18 (zorunlu) — `dotnet test tests/architecture/TenantArchitecture.ArchitectureTests --filter AuditTrailStandard`
  yeşil; `git diff <taban>..HEAD -- tests/architecture/audit-ledger/ | grep -E '^\+- '` boş (borç satırı eklenmemiş).
- [ ] AC-19 (zorunlu) — Release `Approved` partide 200 ve `dispatchOutcome = NotConfigured` döner, `PaymentDispatchAttempt`
  yazılır, parti `Approved` kalır, hiçbir dış ağ çağrısı yapılmaz; `Approved` olmayan partide 409. Release, karar zincirini
  R1–R3'e ve R4 uygulanan partide R4'e göre yeniden doğrular (R11). **R4 kapalı Release (CT-APR-02 Codex F-1; AC-44 (d)
  senaryosunun DL-3 kısmı):** sabitlenmiş `R4Enabled = false` sürümüyle gönderilmiş, eşiği aşan (`IsTier2 = true`) ve yalnız
  N'nin normal onayıyla (`tier2-approve` yok, muafiyet yok) `Approved` olan parti Release'te 200 ve `dispatchOutcome =
  NotConfigured` alır — R11'in tier-2 şartı aranmaz; R1–R3 doğrulaması ve açık karar niyetinde 409 aynen uygulanır. Sabotaj:
  R11'deki uygulanma koşulundan `R4Enabled` çıkarıldığında (yalnız `IsTier2`) ya da koşul ters çevrildiğinde bu Release
  403 `PAYMENT_TIER2_RELEASE_FORBIDDEN` ile reddedilir ve test kırmızıya döner. **R11 tier-2 reddi (KARAR D77/2):** R4
  uygulanan (`IsTier2` ∧ sabitlenmiş `R4Enabled = true`) ve yalnız `tier2-approve` izni olmayan, muafiyetsiz onayla `Approved`
  olmuş parti (fikstür: karar kaydı doğrudan yazılır — başka yüzeyden verilmiş karar, O-7) Release'te 403
  `PAYMENT_TIER2_RELEASE_FORBIDDEN` alır; parti `Approved` kalır, `PaymentDispatchAttempt` yazılmaz, `ReleaseSequence`
  artmaz, dağıtım portu çağrılmaz. Aynı partide bir onay `tier2-approve` izniyle ya da kayıtlı R4 muafiyetiyle verilmişse
  Release 200. Sabotaj: R11'in tier-2 şartı kaldırıldığında bu Release 200 alır ve test kırmızıya döner.
- [ ] AC-20 (zorunlu) — Hiçbir varlık, DTO ya da denetim kaydı IBAN, hesap numarası, BIC/SWIFT ya da banka adı alanı taşımaz
  (alan adı taraması testi); banka referansları yalnız Guid.
- [ ] AC-21 (zorunlu) — Her denetleyici aksiyonunda `[HasPermission]` vardır (mimari testi); izinsiz çağrı 403.
- [ ] AC-22 (zorunlu) — Tutarlar `decimal`; para birimi hanesini aşan tutar 400 (yuvarlama yok); talimat para birimi
  partininkinden farklıysa 400.
- [ ] AC-23 (zorunlu) — Soft delete yalnız hiç gönderilmemiş `Draft`/`Validated` partide; `IsDeleted` + `DeletedAt` (UTC)
  birlikte yazılır, silinen parti listede görünmez, kodu yeniden kullanılmaz. Kod tekilliği veritabanında da tutulur:
  `ux_payment_batch_code` = Unique(TenantId, BatchCode), **kısmi filtre yok** (silinen satır da sayılır); kodun RCS-001
  ortak servisinden gelmesi DB tekilliğini kaldırmaz. Test `PaymentBatchCodeUniquenessTests` (gerçek replica set): aynı
  kiracıda aynı `BatchCode` ile ikinci kayıt — soft delete edilmiş bir partinin koduyla dahil — yinelenen anahtar hatasıyla
  reddedilir; başka kiracıda aynı kod kabul edilir. Sabotaj: indekse `IsDeleted=false` kısmi filtresi eklendiğinde
  silinmiş-kod vakası kırmızıya döner.
- [ ] AC-24 (zorunlu) — Tüm `Views/FinanceTreasury/PaymentBatches/*.cshtml` dosyalarında `Layout = "_LayoutTenantShell"`
  açıkça yazılı; `_CreateEditOffcanvas.cshtml` / `_DetailsQuickView.cshtml` yok;
  `python3 .antigravity/scripts/verify_datatable_page.py . --area FinanceTreasury --module PaymentBatches --format gaps`
  sapma vermez ve `quality-gate-datatable` geçer; `<table data-dt-data-mode="server">` pack ile eşleşir.
- [ ] AC-25 (zorunlu) — 7 dil (`en, tr, fr, es, zh, ar, ru`) resx parite PASS; İngilizce olmayan dosyada İngilizce yer
  tutucu yok; hardcoded metin yok (`grep` 0).
- [ ] AC-26 (zorunlu) — `read` izni olmayan kullanıcı sayfayı açınca kabuk içinde erişim açıklaması görür; başlık, tablo,
  "Yeni" düğmesi çizilmez, yönlendirme olmaz (UAS-001, canlı denendi); SoD nedeniyle karar veremeyen kullanıcıya karar
  düğmesi çizilmez.
- [ ] AC-27 (zorunlu) — Durum filtresi ve rozetleri `PaymentBatchStatus`'un 10 değerini gösterir; enum'da olmayan değer yok.
- [ ] AC-28 (zorunlu) — Sayfa yardım metni `Help.PAYMENTBATCHES.Purpose`, `.Audience`, `.Step1…Step5` + `.Step{n}Title`
  7 dilde var.
- [ ] AC-29 (zorunlu) — `docs/analysis/contracts/payment-batch.openapi.yaml` yayımlanmış; uçlar, hata kodları ve
  idempotency başlığı sözleşmeyle eşleşir (sözleşme testi).
- [ ] AC-30 (zorunlu) — Bir oluşturma, bir güncelleme ve bir silme dev ortamında çalıştırılır; üç merkezi kayıt AUD-001 §8
  sorgusuyla gösterilir ve on alan işaretlenir (rapor ortamın adını yazar).
- [ ] AC-31 (zorunlu) — Yarış testleri (gerçek replica set, paralel istek): (a) aynı aktörün aynı anahtarla iki eşzamanlı
  Approve'u → tek karar, tek uzak geçiş; (b) iki farklı onaylayıcının farklı anahtarla eşzamanlı Approve ve Reject'i → tek
  açık karar niyeti, diğeri 409 `PAYMENT_DECISION_PENDING`, MOD-0023'e tek çağrı; (c) iki eşzamanlı Submit (farklı
  anahtar) → tek başlatma niyeti, diğeri 409. Tekil kısmi indeksler kaldırıldığında (b) ve (c) kırmızıya döner (sabotaj).
- [ ] AC-32 (zorunlu) — Veri kapsamı (SEC-002 §4 kanıt testi, D14/1): aynı yetkiye (`payment-batches.read` + yazma
  izinleri) sahip iki kullanıcı, iki ayrı şirket kapsamı → birinin parti ve istisna listesinde diğerinin şirketinin kaydı
  çıkmaz, detayı / izi 404, kararı ve Release'i 404; hiçbir şirket kapsamı olmayan üçüncü kullanıcı boş liste görür ve her
  detay 404; `read-tenant-wide` izni olan kullanıcı iki şirketin kayıtlarını görür. Çözücü hata verdiğinde liste boş döner.
  Kapsam daraltması (`IFinanceTreasuryDataScopeSource` süzgeci) kaldırıldığında test kırmızıya döner (sabotaj);
  `read-tenant-wide` otomatik yoldan verilmez (`ExplicitGrantOnlyPermissions` listesinde).
- [ ] AC-33 (zorunlu) — Okuma kapsamı yazma kapsamını genişletmez (gap 4/G1): `payment-batches.read-tenant-wide` + bütün
  yazma izinlerine sahip ama LE-B'de **kendi şirket yetkisi olmayan** kullanıcı LE-B partilerini listede ve detayda görür
  (200), ama LE-B partisinde Create (`X-Legal-Entity-Id = LE-B`), Update, Delete, Submit, Approve, Reject, Reconcile ve
  Release 404 `PAYMENT_BATCH_NOT_FOUND` döner; MOD-0023 / MOD-0220'ye çağrı gitmez, kayıt, makbuz ve denetim olayı
  oluşmaz (sahte istemcilerle sayılır). Aynı kullanıcı LE-A'da (kendi yetkisi) aynı komutları çalıştırabilir. Sabotaj:
  yazma yolunda okuma kapsamı kullanıldığında (`TenantWideScope()` yazma kapsamına sızdırıldığında) test kırmızıya döner.
- [ ] AC-34 (zorunlu) — Reconcile önce okur (gap 4/G2; Submit ve her karar türü için ayrı test): (a) uzakta niyetin kendi
  eşleme alanlarıyla (AC-36) örnek / geçiş **var** → şirket `Inactive` ya da `Approved` değilken bile sonuç kaydedilir (MOD-0220 çağrılmaz — D15/2), bağlı
  `Pending` makbuz `Completed`, tek sonuç olayı; (b) okuma 404 / boş / zaman aşımı / 5xx → niyet **`Cancelled` olmaz**,
  `Unknown` kalır, parti ve makbuz değişmez, zaman aşımı / 5xx'te uzak yazma/başlatma çağrısı yok (okuma çağrısı
  yapılır); (c) okuma örnek / geçiş göstermedi + şirket uygun değil → MOD-0023'e uzak yazma/başlatma çağrısı yok, niyet
  `Unknown`, `LastOutcomeCode = PAYMENT_LEGAL_ENTITY_NOT_ELIGIBLE`, Reconcile 422, sonuç olayı yok, makbuz `Pending`;
  (d) okuma örnek / geçiş göstermedi + gap 6/P1-a'nın iki koşulu kapalı (AC-35) + şirket uygun + SoD/izin geçer + aktör
  asıl aktör → başlatma / geçiş **aynı
  deterministik anahtarla** yeniden yazılır ve MOD-0023'te tam **bir** örnek / geçiş vardır (sahte MOD-0023 anahtar
  başına tek örnek / geçiş sayar; ikinci Reconcile ikincisini üretmez; yinelenen anahtar hatası `Unknown` bırakır, asla
  `Refused` değil); aktör farklıysa 409 `PAYMENT_RECONCILE_RESTART_ACTOR_MISMATCH`, uzak yazma/başlatma çağrısı yok;
  **(d) eki — başlatmada ilk aday listesi (KARAR D75/2 (b); kullanıcının başlattığı Reconcile dahil — KARAR D76/3):** yeniden başlatma isteğinin `CandidatePrincipalIds`'i niyetteki
  `PaymentWorkflowStartIntent.CandidatePrincipalIds`'e birebir eşittir ve sahte Auth aday istemcisi Reconcile boyunca hiç
  çağrılmaz (çağrı sayısı 0) — sahte Auth Submit'ten sonra farklı bir liste döndürecek şekilde ayarlansa da; sabotaj: Reconcile
  aday çözümleyicisini yeniden çağırınca çağrı sayısı ve liste eşitliği testi kırmızıya döner;
  (e) MOD-0031 kanıt bağlama Reconcile ya da herhangi bir kurtarma yolu tarafından hiç yeniden çağrılmaz; belirsiz
  okumada sonuç belirsiz kalır (sahte MOD-0031 yazma çağrısı sayısı 1). Sabotaj: okuma adımı atlanıp doğrudan yeniden
  yazıldığında, "bulunamadı" `Cancelled`'a bağlandığında, farklı anahtarla yeniden yazıldığında ya da MOD-0031 yeniden
  çağrıldığında test kırmızıya döner.
- [ ] AC-35 (zorunlu) — Silinmiş uzak kayıttan sonra yeniden yazma güvenli sayılmaz (gap 6/P1-a; Submit ve her karar türü
  için ayrı test): sahte MOD-0023'te niyetin örneği / geçişi oluşturulup sonra soft delete edilir (ya da temizlenir) ve
  niyet `Unknown` bırakılır; Reconcile (a) silinmiş kaydı gösteren okumada ve (b) silinmemiş kayıt dönmeyen ve silinmiş durumu göstermeyen okumada niyetin
  ilk denemesi kayıtlı (tx1 commit olmuş) durumda aynı anahtarla yeniden yazmaz: niyet `Unknown`, 202
  `PAYMENT_WORKFLOW_START_OUTCOME_UNKNOWN` / `PAYMENT_DECISION_OUTCOME_UNKNOWN`, parti ve bağlı makbuz değişmez, sahte
  MOD-0023'te yazma çağrısı 0 ve yeni örnek / geçiş yok. Sabotaj: silinmiş durum ve ilk deneme kontrolü kaldırıldığında
  Reconcile ikinci bir örnek / geçiş oluşturur ve test kırmızıya döner.
- [ ] AC-36 (zorunlu) — Uzak sonuç doğru denemeye bağlanır (gap 6/P1-b): (a) aynı partide önceki `SubmissionSequence`'in
  kapanmış (`Approved` / `Rejected` / iptal) örneği ve yeni gönderimin `Unknown` niyeti varken Reconcile yeni niyeti eski
  örnekle kapatmaz; yeni niyet `Unknown` kalır ya da yalnız kendi uzak anahtarıyla eşleşen örnekle kapanır; (b) aynı
  görevde başka bir kararın geçişi (farklı `RemoteIdempotencyKey`, görev ya da eylem) açık karar niyetini kapatmaz;
  (c) okuma yanıtı eşleme alanını (uzak anahtar; geçişte görev kimliği) taşımıyorsa niyet `Unknown` kalır. Sabotaj:
  eşleme yalnız nesne (parti) kimliğine indirildiğinde test kırmızıya döner.
- [ ] AC-37 (zorunlu) — MOD-0269 sözleşme negatif testleri (gap 7/P2-b): `docs/analysis/contracts/bank-dispatch.md` (FROZEN 0.1.0) §5.1 tablosundaki
  katmanlar ayrı beklentilerle koşulur: (A) bağdaştırıcı doğrudan → `BankDispatchContractException`; (B1) API'den üretilebilen
  bozuk veri → MOD-0126 doğrulaması porttan önce kendi 400 / 409 / 422 kodunu döner, port çağrısı yok; (B2) porta ulaşan
  ihlal (bozuk istek kurucu) → 500 `PAYMENT_BANK_DISPATCH_CONTRACT_VIOLATION`; A, B1 ve B2'de hiçbir deneme / makbuz /
  olay / sıra artışı kalmaz. Ayrıca: (a) iki kaynak hesaplı partide istekteki her talimat kendi
  `SourceBankAccountRef`'ini taşır, birleştirme yok; (b) aynı istemci anahtarıyla tekrar portu çağırmaz ve
  `ReleaseSequence`'i artırmaz; (c) commit edilmiş makbuzun tekrarı aynı yanıtı ve aynı `dispatchKey`'i döner; geri
  alınan deneme hiçbir sıra / anahtar bırakmaz (geri alma sonrası `ReleaseSequence` önceki değerinde, deneme kaydı yok) ve
  arada başka Release commit ederse yeniden deneme sonraki sırayı alır; (c2) `ReleaseSequence = 999 999 999` partide
  409 `PAYMENT_RELEASE_SEQUENCE_EXHAUSTED`, port çağrısı yok;
  (d) iki eşzamanlı Release aynı sırayı alamaz (biri 409); (e) Release sırasında iptal hiçbir kayıt bırakmaz.
  Sabotaj: `Guid.Empty` ya da toplam kontrolü kaldırıldığında ya da talimatlar parti hesabına indirildiğinde test
  kırmızıya döner.
- [ ] AC-38 (zorunlu) — Beklenmeyen bağdaştırıcı sonucu başarı sayılmaz (D17/2): sahte bağdaştırıcı `Accepted`,
  `Rejected`, `Unknown` döndürdüğünde her biri için Release 502 `PAYMENT_BANK_DISPATCH_OUTCOME_UNSUPPORTED`, deneme alınan
  sonuçla kayıtlı, parti `Approved` ve `ReleaseBlockedForReview = true`, tek `…release-attempted` olayı sonucu taşır;
  sonraki Release 409 `PAYMENT_RELEASE_BLOCKED_FOR_REVIEW` ve port çağrı sayısı değişmez. Sabotaj: sonuç kontrolü
  kaldırılıp her sonuç 200 sayıldığında test kırmızıya döner.
- [ ] AC-39 (zorunlu) — Şirket uygunluk yanıtının sınıflandırılması (TRM-14, gap 9/2): sahte MOD-0220 ile her vaka ayrı —
  tutarlı 200 + `APPROVED` → işlem sürer; tutarlı 200 + `DRAFT` / `SUBMITTED` / `REJECTED` ve 404 → 422
  `PAYMENT_LEGAL_ENTITY_NOT_ELIGIBLE`; `approvalStatus` yok / `null` / `""` / `"approved"` (küçük harf) / `"UNKNOWN"`,
  `legalEntityId` yok ya da farklı, `referenceable = false`, `lifecycleState = SUSPENDED`, ayrıştırılamayan gövde, 403, 500
  ve zaman aşımı → 503 `PAYMENT_LEGAL_ENTITY_UNAVAILABLE`; **reddedilen (422) ya da geçersiz / erişilemeyen (503)
  yanıt vakalarında** iş kaydı, makbuz `Completed` ve uzak yazma / başlatma çağrısı oluşmaz (MOD-0023 ve diğer uzak
  okumalara izin vardır; Reconcile'ın uzak okuması bu yasağa girmez). Pozitif vaka (`APPROVED`) normal akışla sürer.
  Reconcile: var olan uzak sonucu kaydetme yolu şirket `REJECTED` iken de kapanır; aynı anahtarla yeniden yazma yolu
  aynı yanıtla 422, uzak çağrı yok. Sabotaj: `approvalStatus` kontrolü "alan yoksa onaylı" yapıldığında ya da kimlik
  karşılaştırması kaldırıldığında test kırmızıya döner. DL-1'de kapanır (Reconcile kısmı DL-2).
- [ ] AC-40 (zorunlu) — MOD-0023 404'ü kesin ret değildir (gap 17/P1, D21/7): başlatma ve her karar geçişinde sahte
  MOD-0023 404 döndürdüğünde niyet **`Unknown`** kalır (`Refused` değil), iş kaydının durumu geri alınmaz (önceki duruma
  dönüş yok), yanıt 202 `…_OUTCOME_UNKNOWN`, sonuç olayı yok; ardından Reconcile okumasıyla kapanır ya da `Unknown`
  kalır. Sabotaj: 404 tx2'de kesin ret grubuna geri eklendiğinde test kırmızıya döner.
- [ ] AC-41 (zorunlu) — Ödeme yöntemi (D28 / O-13, gap 19/2): sahte MOD-0048 kümesiyle (a) yayımlanmış aktif kod → Create
  geçer; (b) kümede olmayan kod ve `IsActive = false` kod → 422 `PAYMENT_METHOD_NOT_ALLOWED`, kayıt yok; (c) küme 5xx / zaman
  aşımı / ayrıştırılamayan gövde / küme bulunamadı → 503 `PAYMENT_METHOD_SOURCE_UNAVAILABLE`, kayıt yok; (d) partide kayıtlı
  kod sonradan pasifleşince Approve ve Release etkilenmez, Update 422 döner; (e) karşılaştırma büyük-küçük harfe duyarlı
  (`StringComparison.Ordinal`, S-56). Update için (b) ve (c) aynıdır. Okuma `IPaymentMethodReferencePort` üzerinden, zaman
  aşımı yapılandırılmış `HttpClient` ile yapılır. Sabotaj: küme kontrolü kaldırılıp serbest metin kabul edildiğinde (b),
  `Ordinal` yerine harfe duyarsız karşılaştırma konduğunda (e) kırmızıya döner. DL-1'de kısmi kapanır ((a)–(c), (e); Create /
  Update); (d) Approve kısmı DL-2'de, (d) Release kısmı DL-3'te — AC-41 tam kapanışı DL-3'tedir (Teslim dilimleri).
- [ ] AC-44 (zorunlu) — **SoD ayar kaydı ve değişiklik denetimi (KARAR D56/4):** (a) kiracıda ayar yokken ilk yazma tek
  `PaymentSodSettings` kaydı oluşturur; aynı kiracıda ikinci kayıt `ux_payment_sod_settings_tenant` ile reddedilir (gerçek replica
  set); başka kiracının ayarı okunamaz / değiştirilemez (404); (b) her başarılı değişiklik `PolicyVersion`'ı tam bir artırır ve
  aynı Mongo işleminde tam **bir** `finance-treasury.payment-sod-settings.updated` olayı yazar (değişen alan adları, `PolicyVersion`
  önce → sonra); bayat `If-Match` 409 `PAYMENT_SOD_SETTINGS_VERSION_CONFLICT`, kayıt ve olay yok; tutar ≤ 0, fazla küsurat ya da
  listede olmayan para birimi 400; (c) değişiklikten önce gönderilmiş partinin kararları sabitlenmiş eski sürümü, eşiği ve R4
  anahtarını kullanır (R6); değişiklikten sonra gönderilen parti yenisini; (d) R4 kapatıldığında (`R4Enabled = false`) yalnız o
  sürümle gönderilen partide R4 uygulanmaz. **R4 kapalı Submit → Approve (CT-APR-02 Codex F-1; R4 uygulanma koşulu `IsTier2` ∧
  sabitlenmiş `R4Enabled = true`):** eşiği aşan (`IsTier2 = true`) ve `R4Enabled = false` sürümüyle gönderilen partide sahte
  Auth `approve` ve `platform.workflow.tasks.approve` için {N}, `tier2-approve` için {} döndürür (N R1–R3 kapsamında değil,
  `tier2-approve` ve `waive-sod` taşımaz) → Submit `tier2-approve` anahtarını ve muafiyet adayı sorgusunu çağırmaz (sahte Auth
  çağrı kaydında yok), `CandidatePrincipalIds = {N}`, 422 dalı yok; N'nin normal Approve'u 403 almaz, karar `Recorded` olur ve sahte
  MOD-0023'ün son adım yanıtıyla parti `Approved` olur (aynı partinin Release'i AC-19'da, DL-3). Aynı Auth fikstürüyle `R4Enabled = true` sürümünde gönderilen eş
  partide kesişim boştur, muafiyet adayı sorgusu (sahte {}) boş döner ve Submit 422
  `PAYMENT_WORKFLOW_RESOLVER_NO_ELIGIBLE_APPROVER` alır (AC-11, AC-42) — iki sürüm ayrı testtir; AC-6, AC-11 ve AC-42'nin
  tier-2 dalları `R4Enabled = true` sürümüyle koşulur. Sabotaj (çözümleyicide ve Approve kontrolünde ayrı ayrı): uygulanma
  koşulundan `R4Enabled` çıkarıldığında (yalnız `IsTier2`) R4 kapalı partide Submit 422 alır ya da N'nin Approve'u 403
  `PAYMENT_TIER2_APPROVER_REQUIRED` alır; koşul ters çevrildiğinde (R4 yalnız `R4Enabled = false` iken uygulanır) R4 kapalı partide
  aynı ret oluşur, R4 açık eş partide N aday olur — her durumda test kırmızıya döner. R1–R3 kapatılamaz (**KARAR D57/4**) — istekte R1 / R2 / R3 anahtarı 400, kayıt ve
  olay yok, R1–R3 her sürümde uygulanır; MOD-0023 `SodViolation` yine reddeder; (e) kiracıda kayıt yokken okuma hata değil,
  varsayılanı döndürür (R1–R4 açık, her onay tier-2 — D11/1, D57/4); `.read` olmadan okuma, `.update` olmadan değiştirme 403;
  (f) ekran (DL-4): ayar ekranı `.read` ile açılır, R1–R3 açık ve değiştirilemez gösterilir, yalnız eşik ve R4 düzenlenir;
  `.read` yoksa kabuk içinde erişim açıklaması (UAS-001, canlı denendi), `.update` yoksa kaydet düğmesi çizilmez; resx 7 dil
  parite. Sabotaj: olay yazımı zorla başarısız edildiğinde ayar değişmez (komut başarısız); olay yazımı kaldırıldığında, tekil
  indeks kaldırıldığında (ikinci kayıt oluşur), R1–R3 kapatma isteği kabul edildiğinde ya da karar kontrolü sabitlenmiş sürüm
  yerine güncel ayarı okuduğunda test kırmızıya döner. (a)–(e) DL-2'de; (f) DL-4'te kanıtlanır, AC-44 DL-4'te kapanır.
- [ ] AC-45 (zorunlu) — **Kayıt kodu portu (KARAR D59/1):** sahte `IRecordCodeAllocator` ile Create, `BatchCode`'u tek
  `AllocateAsync(legalEntityId = ödeyen şirket, recordType = PAY, idempotencyKey)` çağrısıyla alır; aynı idempotency
  anahtarlı tekrar aynı kodu döner ve ikinci parti açmaz. Sahte allocator 422 / 409 / 404 / 503 ya da zaman aşımı
  döndürdüğünde Create 503 `PAYMENT_RECORD_CODE_UNAVAILABLE` ve parti yazılmaz. **Sabotaj:** üretim kompozisyonunda port
  bağlı değilken (bağdaştırıcı kaydı kaldırılır) Create 503 döner ve koleksiyona yazma olmaz; fail-closed korunağı kaldırılıp
  kodsuz parti açılabildiğinde test kırmızıya döner. Mimari testi: servis kodunda kendi sayaç / yedek kod üreticisi yok.
- [ ] AC-46 (zorunlu) — **`PartiallyApproved` v1'de kapalı (KARAR D62/2):** `PaymentBatchStatus.PartiallyApproved` değeri (9)
  ve adı enum'da kalır (AC-27'nin 10 değeri değişmez), ama v1'de bu duruma giriş yolu yoktur: (a) mimari testi — üretim
  kodunda `PaymentBatchStatus.PartiallyApproved` değerine atama yok (0); (b) durum makinesi birim testi — hiçbir durumdan
  hiçbir komutla (Approve, Reject, RequestEvidence, ApproveWithSodWaiver, Reconcile) `PartiallyApproved`'a geçilmez;
  (c) sahte MOD-0023 Approve için `WorkflowIsFinalStep = false` döndürdüğünde karar `Recorded` olur ve parti
  `PendingApproval`'da kalır, onaylanmış sayılmaz (KARAR D66/2: bu yanıt v1'de beklenmez). Sabotaj: Approve'a "son adım değil → `PartiallyApproved`" dalı
  eklendiğinde (a) ve (c) kırmızıya döner. DL-2'de kapanır.

## Test Expectations

- **Birim:** validatorlar (her alan); SoD politika servisi R1–R11 (oluşturan/gönderen/güncelleyen, tier-2 sınır değeri
  `TotalAmount == eşik`, para birimi farkı, muafiyet üç dalı, Guid karşılaştırması); durum makinesi geçişleri; idempotency
  parmak izi; RCS kod portu (`IRecordCodeAllocator`) sahtesiyle kod biçimi ve port bağlı değilken 503 (AC-45, sabotajlı).
- **Entegrasyon (Mongo replika kümesi, işlem):** kiracı/şirket izolasyonu (404); veri kapsamı (AC-32, sabotajlı); soft
  delete; idempotent tekrar; CAS 409;
  CentralAudit kaydının iş işlemiyle birlikte commit/rollback'i ve outbox yazım hatasında komutun başarısız olması (sabotaj);
  tekil indeksler (aynı kod — silinmiş partinin kodu dahil, `PaymentBatchCodeUniquenessTests`, AC-23 sabotajıyla; aynı anahtar).
- **İş akışı sahte sunucusu:** zaman aşımı, bağlantı kopması, boş 2xx, 5xx, kesin 403, 404 (belirsiz → `Unknown`, D21/7), MOD-0023 "başlatma tamamlanmadı"
  409; aynı anahtarla tekrar → tek örnek; karar geçişinde anahtar iletimi; silinmiş örnek / geçiş (AC-35) ve aynı partinin
  eski örneği (AC-36).
- **MOD-0220 sahtesi:** Active+Approved / Active+Draft / Inactive+Approved / 404 / erişilemez; TRM-14 gelince gerçek
  sözleşmeye karşı sözleşme testi.
- **Auth sahteleri (KARAR D66/1, D56/3):** aday sorgusu (izinli liste / boş / okunamaz / 403) ve `waive-sod` + parti biçimi;
  anahtar başına ayrı yanıt (`approve`, tier-2 partide `tier2-approve` ve her partide `platform.workflow.tasks.approve`
  listeleri farklı kişilerle; yalnız biri okunamaz — iş akışı izni çağrısı dahil, KARAR D71) — kesişim ve sabotajı AC-11'de; atama alanları; DL-5 yokken muafiyet yolu kapalı (KARAR D66/3, AC-43 (h)).
- **Mimari:** her aksiyonda `[HasPermission]`; tek eşik tipi; banka verisi alan adı yasağı; `float`/`double` para alanı yok;
  `AuditTrailStandard` filtresi yeşil.
- **Sözleşme:** `payment-batch.openapi.yaml` ile uç şekli; MOD-0269 sözleşmesi gelince `IBankDispatchPort` uyumu.
- **Frontend:** browser smoke (liste, oluştur, düzenle, detay, karar düğmeleri izin/SoD'ye göre); yetkisiz kullanıcı canlı
  denemesi (UAS-001); DataTable doğrulayıcı PASS; RESX 7 dil parite PASS.
- **Build:** `Diten.FinanceTreasuryService` + `Diten.Web` + Gateway 0 hata; yerel kapıda `scripts/` Python testleri de
  koşulur.
- Kaynak testleri (R1, 510/510) **kanıt değildir**, Recovery'de yeniden yazılır; kaynakta hiç olmayan testler (başlatma
  zaman aşımı, boş yanıt, AdminDirect atlatması, eşik ayrışması — paket §N.11) burada zorunludur.

## Page Help (UXH-001)

- Sayfa kodu `PAYMENTBATCHES` (`Nav.Page.PAYMENTBATCHES`); Create/Edit/Details aynı kodun alt sayfalarıdır.
- Anahtarlar (kiracı 7 dil): `Help.PAYMENTBATCHES.Purpose` (ödeme partisi hazırlama ve onaya gönderme; bankaya ödeme
  gitmediği açıkça yazılır), `Help.PAYMENTBATCHES.Audience` (ödeme hazırlayıcı, ödeme onaylayıcı, serbest bırakıcı, hazine yöneticisi, ödeme görüntüleyici — KARAR D49/1),
  `Help.PAYMENTBATCHES.Step1…Step5` + `Step{n}Title` (Parti oluştur · Talimat ekle · Doğrula · Onaya gönder · Karar ve
  onay ayrılığı). `Immediate` yok (sayfada onaysız hemen geçerli işlem yok). İlgili sayfa: yok (Görev Merkezi köprüsü bu
  dilimde yok; `PageHelpCatalog` satırı yalnız adım ikonları için).
- **Tur:** gerekmez — beş akıştan biri değil → "tur yok".
- SoD ayar ekranı (KARAR D57/4): sayfa kodu `PAYMENTSODSETTINGS`; `Help.PAYMENTSODSETTINGS.Purpose` (tier-2 eşiği ve R4;
  R1–R3'ün kapatılamadığı açıkça yazılır), `.Audience` (hazine yöneticisi), `.Step1…Step3` + `Step{n}Title` (Eşiği gir ·
  R4'ü seç · Kaydet), kiracı 7 dil; tur yok.

## Ready-for-dev Checklist

**Biçim maddeleri (paket içi):**
- [x] Golden Reference Compact okundu (backend + frontend + `DEV-0001`)
- [x] Frontmatter tam (service, shell, golden_reference, data_mode, entity_base, form_field_count)
- [x] Layout & Shell Contract'ta `Layout = "_LayoutTenantShell"` açık
- [x] Backend / Frontend file convention Golden Reference ile birebir (suffix yok, CommandHandlers/QueryHandlers ayrı)
- [x] Validation her alan için yazılı; SoD kuralları R1–R11
- [x] Failure Path ≥ 4 senaryo (duplicate/idempotency, missing, unauthorized, concurrency) + kiracılar arası + SoD + belirsiz sonuç
- [x] Authorization: izin listesi + policy + aktör
- [x] Gateway kararı (gerekli, integration-agent task'ı)
- [x] Audited Events her yazma komutunu tam bir kez içeriyor (yol b)
- [x] Veri kapsamı cümlesi (SEC-002 §4, D14/1) ve kanıt testi (AC-32)
- [x] Sayfa yardım metni anahtarları ve "tur yok"
- [x] Acceptance criteria kimlikli ve test edilebilir; test expectations build/verifier/RESX/smoke/sözleşme kapsıyor

**ENGELLER — paket kapısı (D51/1).** Tek kural (MOD-0217 / MOD-0289 deseni): işaretsiz madde **paket düzeyidir** — bütün
dilimleri ilgilendirir ya da DL-1'in kendi önkoşuludur; hepsi kapanmadan `ready-for-dev` olmaz. **"— DL-n için"** işaretli
madde paket kapısı **değildir**: yalnız o dilimin (ve sonrakilerin) önkoşuludur, "Teslim dilimleri"nde o dilimin Önkoşul
metninde yazılıdır ve o dilim başlamadan kapanır; önceki dilimler o madde açıkken ilerler. Yeni kodun testlerinin geçmesi
iki kapının da maddesi değildir — o dilimin kabul kapısıdır ("Bitiş" AC'leri). Auth (MOD-0018) iki
parçadır (KARAR D53): paket metni paket kapısıdır — `ready-for-dev`'den önce main'de ve MOD-0018 sahibince teyitli (**KARAR D70/3**; şartlı teyit Y-19 / **KARAR D80/2** — DL-1'i bekletmez); kayıt kodu DL-1 kabul kapısıdır.
**RCS-001 / BL-565 servisi DL-1 kabul kapısıdır (KARAR D59/1), paket kapısı değildir:** DL-1 kodu `IRecordCodeAllocator`
portuna karşı sahte uygulamayla yazılır; gerçek servis DL-1 kabulünden önce main'de olmalıdır.
- [ ] CT-APR-02 bağımsız inceleme bu paket sürümü için kaydedildi (`execution/approvals/reviews.tsv`), açık ENGEL yok — inceleme künyesi `ready-for-dev` olan sürüme yazılır (S-49, KARAR D34/2 — `treasury-intake-owner-decisions-02.md` D34, PR #235)
- [ ] CT-APR-01 sürüme bağlı onay satırı (`execution/approvals/approvals.tsv`); yüksek risk — paketi yazmamış, `high: evet` onaylayıcı. Satır künyesi `ready-for-dev` olan sürüme yazılır; `draft` sürüme yazılan satır yalnız uyarıyla kabul edilir ve kodu yetkilendirmez (S-49, D34)
- [ ] DCP (DCP-011-finance-treasury-intake) `approved` ve numarası atanmış — numara atandı: DCP-011 (Y-4/2; dosya adı 2026-10-27; D34/2 sırası); `approved` açık
- [ ] Auth paket metni main'de **ve MOD-0018 sahibinin teyidi** (**KARAR D70/3**: metnin main'de olması tek başına yetmez; teyit karar dosyasına kaynak olarak yazılır) — **şartlı teyit, Y-19 / KARAR D80/2** (D69–D78 sürümü, main `4c08fc75a`): bu paketin DL-1'ini bekletmez; iki şart (K-W5 denetim izi için defter kararı; rol atama adımlarının — rola izin bağlama, kişiye rol verme — denetim borcu) Auth ekinin DL-5 kodundan önce kapanır; D79'dan sonraki değişiklikler (7 dil tablosu vb.) sahip teyidine ayrıca gider; açık kalan: o ayrı teyit ve D80'in main'e girmesi (D62/1) — MOD-0018 Treasury Auth eki `execution/domains/platform-shared-services/module-packs/MOD-0018-FU27-treasury-permission-onboarding.md` (FU27 — K-1 kapandı, Y-19 / D80/1; Treasury yazar, MOD-0018 sahibi teyit eder — Y-15, Y-16): o paketin DL-1'inde anahtarlar, açık atama listesi ve rol şablonları (rol matrisi D46–D50, `treasury-intake-owner-decisions-05.md`; **KARAR D53/1**)
- [ ] Auth kayıt kodu (o ekin DL-1 kodu) main'de; DL-1 testleri gerçek izinle koşar (**KARAR D53/2**) — DL-1 için (kabul kapısı)
- [ ] SEC-002 veri kapsamı çözücüsünün FinanceTreasury'de tüketim yolu (O-19; platform-shared-services) — tasarım: yol Platform sahibi yanıtı Y-4/6 ile belirlendi (servis içi `IFinanceTreasuryDataScopeSource` + Platform `IDataScopeResolver` bağdaştırıcısı, fail-closed); açık kalan (paket kapısı): `read-tenant-wide`'ın Auth'ta açık atamalı izin olarak tanımı — MOD-0018 Treasury Auth ekinin DL-1'inde (Bölüm 14.1), ek main'de değil; FinanceTreasury'nin Platform kapsam ucuna bağlanması o ekin K-6 açık kararı. Kapsamı soran kod + kanıt testi (AC-32) bu kapının maddesi değildir — **DL-1 kabul kapısındadır** (D51/2; AC-32'nin DL-1 uçları kısmı DL-1 "Bitiş"te, karar uçları DL-2'de, Release ve kalan okumalar DL-3'te — tam kapanış)
- [x] Port 5071 AGENTS.md §3 ve `.antigravity/rules/ports.md`'ye yazıldı (D30); AGENTS §2 (domain/servis) ve §9 (`fin`) güncellendi (TRM-03/04)
- [x] Kapı kararları 0.3 / 0.6 / 0.7 (KARAR D27/1, D27/3, D27/4) — T32 sapması bitişi: MOD-0003 çıkana kadar, en geç Treasury v1 sonu
- [ ] TRM-14: MOD-0220 pack değişikliği onaylı + `lookup-validation` yanıtında `ApprovalStatus` + MOD-0220 sözleşme belgesi (DL-1'in kendi önkoşulu: AC-2, AC-39)
- [x] Sahip MOD-0126 için kod alma iznini verdi (KARAR D27/5; kaynak S `da4abb20`)
- [ ] RCS-001 ortak kod servisi (BL-565, Beste — D44) ve şirket kısa kodu kaynağı main'de; v0 sözleşmesi (`POST /api/internal/record-codes/allocate`) 6 Kasım'da kesinleşti (önek `PAY` KARAR D29/2; `.antigravity` RCS-001 §3 satırı uygulandı (D30)) — DL-1 için (kabul kapısı; **KARAR D59/1**). DL-1 `IRecordCodeAllocator`'a karşı sahte uygulamayla ilerler; üretimde port bağlı değilse Create 503 (AC-45)
- [x] Yeni servis defteri + CT-DECISIONS satırı için Control Tower kararı (yol b izi kabulü) — **verildi: KARAR D35** (`treasury-intake-owner-decisions-02.md` D35, PR #235): iz `finance-treasury-merkezi-iletim | b | işaret | ICentralAuditDeclaration`, borç / istisna 0; defter dosyası + CT-DECISIONS satırı + `AuditTrailStandardTests` DL-1 değişikliğinde birlikte (D35/4)
- [x] Platform `AuditCategory`'ye eklemeli `Finance` (değer 18; Y-4/7) değeri main'de (KARAR D35/3, O-6; sahibi Platform / Beste, XMC-001) — **DL-1 bu değer main'e girmeden başlamaz** — **main'de: PR #298** (`AuditEnums.cs:29`, `CentralAuditPlatformValues.cs:30` @ `317a48b13`)
- [x] İzin ad alanı `finance-treasury.*` onayı (O-5 → D11/4)
- [x] Para birimi küsurat hanesi kaynağı (O-11) — DL-1 Create'in kendi önkoşulu — `currency-minor-units` başlangıç listesi olarak kabul (Y-4/3); seed main'de — **MOD-0048-FU01 seed'i PR #297 ile main'de** (`aaa3381df`; `treasury-reference.json`, `ConsumableSets` izin listesi)
- [x] MOD-0048 `payment-method` kümesi tanımlı, yayımlanmış ve `ConsumableSets` izin listesinde (D28 / O-13; MOD-0048 sahibi) — DL-1 Create'in kendi önkoşulu — öneri başlangıç listesi olarak kabul (Y-4/3); seed main'de — **MOD-0048-FU01 seed'i PR #297 ile main'de** (`aaa3381df`; `treasury-reference.json`, `ConsumableSets` izin listesi)

**Dilim kapıları ("— DL-n için"; paketi `ready-for-dev`'den alıkoymaz, yalnız o dilimi bekletir — D51/1):**
- [x] SoD eşiği modeli sahip kararıyla yazıldı (O-4 → D11/1: kiracı ayarı, para birimli; ayar yoksa tier-2) — DL-2 için
- [ ] MOD-0023'te MOD-0126 iş akışı şablonu main'de — **kod geldi (Y-12):** `FT-PAYMENT-BATCH-APPROVAL`, tek aşama / tek onay
  adımı / atamasız, adaylar `CandidatePrincipalIds` ile (MOD-0023 sahip kararları W-1, W-6 —
  `mod0023-workflow-template-owner-decisions-01.md`, origin/main @ `28c68e7a6`, #277); **açık:** şablonu kuran kod (Platform
  şablon kataloğu + `--ensure-workflow-templates`; sahibi platform-shared-services / MOD-0023 sahibi) main'de değil, WP
  numarası ve tarih gelmedi (2026-10-28) — DL-2 için
- [ ] `waive-sod` atama alanları — hedef parti, başlangıç / bitiş, onaylayan, gerekçe kodu (KARAR D54) — MOD-0018 Treasury Auth
  ekinin **DL-5** paket metninde, muafiyet adayı sorgusu ("bu partiye geçerli `waive-sod` ataması olanlar", **KARAR D56/3**;
  D66/1 ile DL-6 aday sorgusunun `waive-sod` + parti biçimi; **KARAR D67/2**) **DL-6** paket metninde tanımlı ve main'de (`…/MOD-0018-FU27-treasury-permission-onboarding.md`; Treasury yazar, MOD-0018 sahibi teyit
  eder — **KARAR D64**, D63/3'ün yerine; DL-5 paket kapısı D64'ün main'e girmesi — D62/1; D64 main'de, PR #276) — DL-2 için
- [x] Onaylayıcı aday tabanı kararı (O-10) — **KARAR D66/1**: Auth aday sorgusu, servis R1–R4'ü uygular, `CandidatePrincipalIds`; iş akışı izniyle kesişim **KARAR D71** (MOD-0018 eki K-A4b kapandı) — DL-2 için
- [ ] Onaylayıcı aday sorgusu MOD-0018 Treasury Auth ekinin **DL-6** paket metninde tanımlı ve main'de (Bölüm 8.6; Treasury
  yazar, MOD-0018 sahibi teyit eder — **KARAR D66/1**; DL-6 paket kapısı D66'nın main'e girmesi — D62/1; D66 main'de, PR #281) — DL-2 için
- [ ] O ekin DL-6 kodu main'de (**KARAR D66/1**, **D67/1** — aday sorgusu ekin DL-6'sı, kodu bu dilimin kabul kapısı; D53 / D56/3 deseni) — DL-2 için (kabul kapısı)
- [ ] O ekin DL-5 kodu main'de (**KARAR D64**) — **DL-2 kabul kapısı değildir (KARAR D66/3**, D56/3'teki kuralın yerine): DL-2,
  muafiyet yolu 403 `PAYMENT_SOD_WAIVER_FORBIDDEN` ile kapalıyken bunu kanıtlayan testle (AC-43 (h)) kabul edilir; muafiyet DL-5
  main'e girince açılır ve **ayrıca kabul edilir** (AC-9 `Waived` dalı, AC-42 aday dalları, AC-43 (a)–(e) ve parti bağı
  sabotajı gerçek DL-5 ile) — muafiyet açılış kabulü için
- [x] SoD ayarının kayıt yeri — FinanceTreasuryService'te kiracı başına tek kayıt, tek eşik + R4 açık / kapalı (R1–R3
  kapatılamaz; kayıt yoksa R1–R4 açık ve varsayılan eşik — **KARAR D57/4**), denetlenir, DL-2'de yazılır (**KARAR D56/4**); ekranı
  DL-4 (D57/4) — DL-2 için
- [x] SoD ayarı izin anahtarları ve rol yerleşimi kararı — `finance-treasury.payment-sod-settings.read` / `.update`, Hazine
  yöneticisi (**KARAR D57/4**; Authorization Convention → "SoD ayarı izinleri")
- [ ] Bu iki anahtar ve Hazine yöneticisi yerleşimi MOD-0018 Treasury Auth eki paket metninde main'de (o paketin DL-1'i,
  kapsam (f); Treasury yazar, MOD-0018 sahibi teyit eder — Y-15, Y-16) — DL-2 için
- [x] MOD-0023 pack §17 (TRM-15) rev 2 **onaylandı** (sha256 `803fbaa5272373846d6c60507525cc9259220a64fab02cf8889f362136a05fb2`, PR #235; CT-APR-02/01 bu sürüme — `execution/approvals/approvals.tsv:16`, 2026-10-26, #243; yarış yanıtı politikası KARAR D21/4, D22/3; D32) — DL-2 için. **Testlerin geçmesi ready-for-dev engeli değildir** (gap 16/P2): §17 kodu MOD-0023 sahibinin WP'siyle gelir (D25/1) — sahibi yazar, başka biri merge eder (D32/1) — ve DL-2 kabulünden önce main'de olmalıdır; AC-W1…W17 (§17.8 kapanışı) DL-2 uygulama kabul kapısındadır; AC-W5…W14 ve AC-W17 geçene kadar yinelenen anahtar hatası `Unknown` (D21/7)
- [x] Silmeme politikası (KARAR D21/1) MOD-0023 pack §17 rev 2'de onaylı (`approvals.tsv:16`); silinmiş kayıtlar `isDeleted` / `includeDeleted=true` ile okunur (D25/2; AC-W13), tek silme işareti `IsDeleted` ve miras silme kapalı (D32/2; AC-W15), `includeDeleted` kiracı ve `instances.view` içinde (D32/6; AC-W16) — DL-2 için; AC-W1 (silmeme mimari testi) ve AC-W15 DL-2 uygulama kabul kapısındadır (gap 16/P2); kapanmazsa 404 / boş okumada anahtarlı yeniden yazma kapalı kalır (gap 6/P1-a)
- [x] MOD-0023 okuma eşleme alanları (KARAR D21/3) §17 rev 2'de onaylı (`approvals.tsv:16`) — DL-2 için; kod MOD-0023 sahibinin WP'siyle gelir (D25/1), AC-W2/W3 uygulama kabul kapısındadır (gap 16/P2); yoksa okuma niyeti kapatmaz (gap 6/P1-b)
- [x] MOD-0269 arayüz sözleşmesi dondu — `docs/analysis/contracts/bank-dispatch.md` FROZEN 0.1.0 (KARAR D27/7, D27/8; TRM-06 kapandı) — DL-3 için
- [ ] MOD-0031 kanıt bağlama dedupe ölçümü — kurtarmada yeniden çağrıyı açmak için önkoşul (ölçülmezse yeniden
  çağrı kapalı kalır, gap 5/G2) — DL-3 için

## Implementation Notes

### Kaynak ve kanıt (provenance)

- Producer paketi: MOD-0126 v1.2 (TRM-B7-01), `source-batch7-v1.0/…/mod0126-transfer-packet-v1.2/`, `manifest.json`
  sha256 `660ab4c00af74ccfdac298979d6a3cd20d9b9ceeca2d526540072e10d4cabcc8` (bu oturumda `shasum -a 256` ile ölçüldü);
  paket dosyası `MOD-0126-TRANSFER-PACKET-v1.2.md` sha256 `7003ea95…a1f5` (manifest girdisi). v1.1 (`f6fab005…`) yerini aldı.
- Kaynak commit: `da4abb204e05fbd47ec088a6b0da08b8f7c6dc5d` (S, yalnız bu makinede; `git bundle` yedeği önerildi —
  inceleme §6.5).
- Derleme/test kanıtı (devralınmış, kaynak kaydı): R1 @ da4abb20, BUILD ok, **MOD-0126 TEST 510/510** (toplam 1604/1604) —
  `treasury-build-evidence-20261023/SUMMARY-v1.1.md@ebdc38b8` (paket §O); handoff `source-build-evidence-v1.0` SHA256SUMS
  `7ec2cd3332a4ff8d8f7c5fbfe1885b49a5f1e552ee4954806090b59726c536eb`. Modül eşlemesi sınıf adı sezgiseldir (paket §N.11);
  Recovery hazırlığının ölçüsü değildir.
- Kod alma (D1, kopya değil): her alınan dosya `path@da4abb20` + sha256 ile iz kaydına yazılır; aşağıdaki liste taşınmaz.
- Paket B taslağı (`PKG-B-MOD-0021-FU01-MOD-0126-AUDIT-INGEST-DRAFT-v0.1.md`, güvenilir denetim niyeti alımı) bu pakette
  **kullanılmaz**: yol `b` CentralAudit ortak ileticisidir (K4).

### Level-1 koşulları (`MOD-0126-v1.2.md`) → bu paket

1. Tek, para birimli eşik + politika sürümü → R4–R6, AC-7/8 (değer O-4).
2. Başlatma idempotency anahtarı + yerel niyet + belirsiz sonuç → Runtime Constraints, AC-11…15.
3. LE uygunluğu (MOD-0220 + `ApprovalStatus`) → AC-2, TRM-14; banka referansları opak → AC-20, MOD-0289 follow-up.
4. `[HasPermission]` → Authorization Convention, AC-21.
5. Sözleşme → `payment-batch.openapi.yaml`, AC-29.

### Taşınmayacaklar (do-not-carry)

1. **FailClosed istemci varsayılanlarını uygulama saymak:** kaynakta iş akışı, kanıt, onaylayıcı dizini ve denetim
   istemcileri varsayılan `FailClosed` ve kayıtlı yapılandırmada iletim kapalı (paket §C; `WorkflowClientOptions.cs:7`,
   `AuditDispatchClientOptions.cs:7`, `appsettings.json:106-135@da4abb20`). Recovery'de her port gerçek bir bağdaştırıcıya
   bağlanır ya da (MOD-0269 gibi) açıkça "yapılandırılmamış" sonucunu kaydeder; FailClosed sahte "yapıldı" sayılmaz.
2. `_LayoutBackbone` ve kaynak ekran (`Views/ERP/Treasury/PaymentProposals/Index.cshtml`, yer tutucu `console.log`
   betiği — paket §I).
3. Hardcoded İngilizce (kaynak ekranda 0 `Localizer` — paket §I) ve serbest İngilizce hata iletileri.
4. UI durum listesi uyumsuzluğu (`PendingValidation`, `Released` var; `Submitted`, `Cancelled`, `EvidenceRequested`,
   `Escalated`, `PartiallyApproved` yok — paket §I T22).
5. Uzak-önce-yerel sırası: başlatma (`PaymentWorkflowService.cs:158` yerel kayıt `:235`'ten önce) ve geçişler (`:474`
   önce, `:609` sonra) @da4abb20; belirsiz sonucun ret olarak kaydı (`:171-183`, paket §H.3).
6. İki ayrı, para birimsiz eşik (`SodPolicyContracts.cs:10`, `ApproverPolicyContracts.cs:13` @da4abb20) ve `50_000m` kod
   varsayılanı.
7. AdminDirect vekil yolu (PP-05) ve `PassedByAdminProxy` sonucu (`PaymentLifecycleService.cs:13-15, 271-276, 436-437@da4abb20`).
8. `[Authorize]`-only denetleyiciler ve servis içi izin kontrolü (paket §C, §G); web sayfasının izinsiz doğrudan URL
   erişimi (paket §G).
9. Kullanıcıdan alınan parti numarası ve `CountAsync()+1` sayaç (`PaymentProposalService.cs:126-128, 471-475@da4abb20`);
   `IsDeleted=false` kısmi tekil parti numarası indeksi.
10. SoD reddinde çift istisna satırı (`PaymentWorkflowService.cs:390-412@da4abb20`).
11. Sıralı (büyük/küçük harf duyarlı) ile duyarsız aktör karşılaştırması karışımı; `string` aktör kimliği, `"system"`
    varsayılan `CreatedBy` (`TenantEntity.cs:10@da4abb20`).
12. Kaynak denetim outbox'ı (`PaymentOutboxMessage`, `PaymentOutboxAttemptLog`), `/api/audit/write` istemcisi ve PP-11.
13. `mod0126.*` izin anahtarları, `/api/v1/mod0126` rotaları, `mod0126_payment_*` koleksiyon adları, 5058 portu.
14. Kanıt bağlamanın oluşturma içinde, yerel kayıttan sonra, işlem dışında yapılması (`PaymentProposalService.cs:235-298@da4abb20`).
15. Zaman aşımı yapılandırılmamış `HttpClient` (`I/DependencyInjection.cs:177-190@da4abb20`, paket §H.3).
16. Bildirilmiş ama kullanılmayan `mod0126.payment_evidence.unlink` ve `WaiverApprovers` (paket §E, §L.11).
17. Kaynak bulgusu (MOD-0217 ile ortak 67 dosya, Platform `Contracts/Audit` + `Building.Blocks.Audit`): MOD-0126 kodu
    bunlara derlenmiyor (paket §D) — alınmaz.

### Tasarım notları

- MOD-0023 başlatanın onayını zaten reddediyor; MOD-0126 bunu yeniden yazmaz, yalnız MOD-0023'te olmayan kuralları
  (oluşturan, son güncelleyen, tier-2, muafiyet, para birimli eşik) ekler ve gönderme çözümleyicisinde aday dışı bırakır
  (domain-config "modül kendi SoD kontrolünü icat etmez" ile tutarlı: başlatan kuralı miras, ödeme kuralları ek).
- Aday dışı bırakma + MOD-0023 atama kontrolü nedeniyle oluşturan, gönderen ve son güncelleyen hiçbir zaman aday olmaz;
  sahip kararı D12/1 ile muafiyet yalnız R4'ü (atanmış onaylayıcının tier-2 izni eksikliği) kaldırır. Aday seçimi, karar
  kaydı ve Release (R11) aynı modeli kullanır.
- `target: 2026-12-15` planlama yer tutucusudur, sahip kararı değildir.

## Follow-up Items

- Güvenilir servis kimliği (MOD-0033-FU02, Paket A) gelince: güvenilir başlatma + `start-result` + arka plan uzlaştırma
  süpürücüsü (İ2 tetikleyici; her değişiklik `ReconcilePaymentBatchWorkflowCommand` ile denetlenir).
- MOD-0289 banka hesabı arama/çözümleme — sözleşme taslağı `docs/analysis/contracts/drafts/bank-account-lookup-validation-v1.0-proposal.md` (BANK-ACCOUNT-LOOKUP 1.0.0 DRAFT); dondurulunca
  banka referansı seçimi, dağıtım anında çözümleme ve F-E1…F-E9 / F-503 fikstürlerinin bu modülün sınıflandırıcısına karşı
  koşulması. **KARAR D75/4:** MDM'nin 422 `MDM_BANK_LEGAL_ENTITY_UNKNOWN` yanıtı (taslak §3, §5) "hesap uygun değil"dir —
  servis kendi 422'sini döner (paket öneki `PAYMENT_`; bu pakette bugün banka hesabı "uygun değil" kodu yok, kod adı tüketim
  açıldığında hata sözlüğüne yazılır), kayıt yazılmaz, otomatik yeniden deneme yok; tüketimi açan AC bu yanıtın 503'e
  eşlenmesini sabotajla kırmızıya çevirir.
- MOD-0269 gerçek bağdaştırıcı, banka geri çağrısı, ekstre → ayrı karar ve paket (D5); o pakette Release iki aşamalı
  olur (`PaymentReleaseIntent`, belirsiz sonuç, Reconcile, sonuç olayı) ve gerçek dağıtım kapısı (hesap sahipliği,
  Tenant/LE, alacaklı eşleşmesi) kapanır.
- `ReleaseBlockedForReview` kilidinin elle inceleme ve açma komutu (D17/2; izin, K2 ve denetim o kararla) — bu sürümde
  kilit açılmaz.
- CentralAudit reddedilen komut kaydı (`Outcome = Denied`) — BL-501 sahibine (G-AUD-1).
- Yeniden başlatması engellenen (şirket uygun değil, SoD/izin reddi, aktör farklı) ve bu yüzden `Unknown` kalan niyetin
  kalıcı kapatılması için açık iptal yolu (gap 4/G2) — sahip kararıyla ayrı komut; bu sürümde niyet açık kalır.
- Görev Merkezi köprüsü (WC-D1: yeni sınıf değil, yapılandırma satırı) — onay görevlerinin panoda görünmesi.
- Alacaklı varlık doğrulaması (MOD-0140 `validate`, MOD-0220, çalışan kaynağı) (O-14).
- AP kaynaklı parti (`ApLinked`, MOD-0119) — TRM-S2 sonrası.
- Liste dışa aktarımı (export) ve okuma denetimi gerekirse.
- `execution/registries/module-implementation-status.md` satırı ilk kodla açılır.

## Açık kararlar

| # | Karar | Seçenekler | Öneri | Sahibi |
|---|---|---|---|---|
| O-1 | PP-05 AdminDirect kalsın mı? | (a) düşür (b) SoD ile koru | **KARAR: (a) düşür** — sahip D11/2 (2026-10-24) | sahip (K1) |
| O-2 | PP-11 dispatch-once kalsın mı? | (a) düşür (b) İ3 komutu olarak tut | **KARAR D28 (2026-10-25): öneri kabul** — **(a) düşür** — iletim CentralAudit'in | sahip (K1) |
| O-3 | Taslak düzenleme ve silme (kaynakta yok) | (a) Update + soft Delete/BulkDelete yalnız gönderilmemiş taslak (b) yalnız iptal | **KARAR D28 (2026-10-25): öneri kabul** — (a) — Golden Reference Compact seti; reddedilirse Edit/Delete çıkarılır, sapma kaydedilir | sahip (K1) |
| O-4 | SoD tier-2 eşiği **değeri**, **para birimi**, politika sürüm adı; farklı para birimi kuralı; ortam mı kiracı mı | — | **KARAR (D11/1): kiracı ayarı, para birimli**; ayar yoksa her onay tier-2; farklı para birimi → tier-2. **Kayıt yeri KARAR D56/4 (2026-10-27):** FinanceTreasuryService'te kiracı başına tek ayar kaydı (tek eşik + R1–R4 açık / kapalı), denetlenir, DL-2'de yazılır (`PaymentSodSettings`, AC-44). **Ayrıntılar KARAR D57/4 (2026-10-28):** izinler `finance-treasury.payment-sod-settings.read` / `.update` (Hazine yöneticisi); ayar ekranı MOD-0126 ekran diliminde (DL-4); R1–R3 kapatılamaz (yalnız eşik ve R4 ayarlanır); kayıt yoksa R1–R4 açık ve varsayılan eşik. Kalan: yok | sahip (K1) |
| O-5 | İzin ad alanı ve rol şablonları | `finance-treasury.*` / `treasury.*` / `fin.*` | **KARAR (D11/4): `finance-treasury.*`**; **KARAR D46 + D47** (2026-10-27; rol matrisi `treasury-intake-owner-decisions-05.md`) rol şablonları verildi; `waive-sod` yalnız açık atama (D47/1); **D49/1:** modül bazında görüntüleyici (ödeme görüntüleyici; Hazine konsolide görüntüleyici yalnız gerekçeyle); **D49/4:** muafiyet ataması kuralları (Authorization Convention); Auth paket metni MOD-0018 Treasury Auth eki (`…/MOD-0018-FU27-treasury-permission-onboarding.md`; FU27 — K-1 kapandı, Y-19 / D80/1; Treasury yazar, MOD-0018 sahibi teyit eder — Y-15, Y-16) | sahip + MOD-0018 sahibi |
| O-6 | Onay/ret/muafiyet AUD-001 K2 sınıfı mı; `AuditCategory` değeri | — | **KARAR (D11/3): evet K2**; **KARAR D35/3 (2026-10-26, `treasury-intake-owner-decisions-02.md` D35, PR #235):** Platform `AuditCategory`'ye yalnız ekleme olarak `Finance` (değer 18; Y-4/7) (`MasterData` ara çözümü seçilmedi); Platform sahibinin işi (XMC-001, D9); **DL-1 bu değer main'e girmeden başlamaz**. Defter izi D35/1–2: `finance-treasury-merkezi-iletim \| b \| işaret \| ICentralAuditDeclaration`, borç / istisna 0; defter + CT-DECISIONS satırı + `AuditTrailStandardTests` DL-1 değişikliğinde (D35/4) | CT + sahip; `Finance` değeri: Platform (Beste) |
| O-7 | Onay yüzeyi ve başlatma kimliği | (a) kararlar yalnız MOD-0126 ucundan, kullanıcı token'ıyla genel MOD-0023 uçları (b) güvenilir tüketici yolu | **KARAR D29/1 (sahip payı, 2026-10-25): öneri kabul; MOD-0023 sahibi teyidi yerine sahip teyidi — KARAR D42 (2026-10-26)**; MOD-0023 sahibi (Ali) teyidi: "ikisi de uygun" (Y-1, 2026-10-27) — (a) v1 — (b) MDM'ye kilitli; Görev Merkezi'nden verilen karara karşı R11 | sahip + MOD-0023 sahibi |
| O-8 | MOD-0023 şablon kodu, çok adımlı onay (PartiallyApproved) adım tanımı, HTTP zaman aşımı değeri | — | **Kod geldi (Y-12, 2026-10-28):** `FT-PAYMENT-BATCH-APPROVAL` — MOD-0023 sahip kararları W-1 (tek adım; eşik üstü partide onayı yalnız `tier2-approve` sahibi verir, R4) ve W-6 (kod) (`docs/records/decisions/2026-10/mod0023-workflow-template-owner-decisions-01.md`, dal `chore/ct/pss-wf-ct-01` @ `c927047f1`; origin/main @ `28c68e7a6`, #277); tek aşama, tek onay adımı, atama yok; onaylayıcıları `StartWorkflowInstanceRequest.CandidatePrincipalIds` belirler (R1–R4 aday çözümleyicisi), boş liste 400 `WORKFLOW_ASSIGNMENT_CANDIDATES_REQUIRED`. Çok adımlı / kademeli onay v1'de yok → `PartiallyApproved` v1'de kapalı (**KARAR D62/2**; AC-46). Zaman aşımı 15 sn, tüketici ayarı (`FinanceTreasury:Workflow:TimeoutSeconds`). **Kalan:** şablonu kuran MOD-0023 kodu main'de değil — WP numarası ve tarih MOD-0023 kuyruğu açılınca (Y-12); Ready-for-dev Checklist maddesi açık | MOD-0023 sahibi |
| O-9 | RCS-001: tür öneki ve şirket kısa kodu kaynağı; ortak kod servisi | `PAY` / başka | **KARAR D29/2 (2026-10-25): önek kabul** — `.antigravity` RCS-001 §3 satırı uygulandı (D30) — `PAY` (§3'te çakışma yok, ölçüldü); servis BL-565 — DL-1 kabul kapısı, `IRecordCodeAllocator` portu (KARAR D59/1) | sahip + CT (`.antigravity` onayı) |
| O-10 | Onaylayıcı adayları nereden (kaynakta yapılandırma listesi / dizin) | MOD-0023 atama (pozisyon/rol) / izin dizini | **KARAR D29/1 (sahip payı, 2026-10-25): öneri kabul; MOD-0023 sahibi teyidi yerine sahip teyidi — KARAR D42 (2026-10-26)**; MOD-0023 sahibi (Ali) teyidi: "ikisi de uygun" (Y-1, 2026-10-27) — MOD-0023 şablonunun atama kuralı + R1–R4 dışlaması; ayrı dizin istemcisi taşınmaz. **Muafiyet adayları (R4) KARAR D56/3:** Auth'un "bu partiye geçerli `waive-sod` ataması olanlar" sorgusu (uç MOD-0018 Treasury Auth ekinin DL-5'inde — KARAR D64). **Y-12:** adaylar başlatmada `CandidatePrincipalIds` ile gönderilir (şablon atamasız). **KARAR D66/1 (2026-10-28) — Y-12 gerilimi kapandı:** şablon atamasız olduğundan "şablonun atama kuralı" aday tabanı değildir; aday tabanı Auth aday sorgusudur ("bu izne sahip kullanıcılar" — MOD-0018 Treasury Auth eki DL-6, Bölüm 8.6; normal partide `approve`, tier-2 partide `approve ∩ tier2-approve` — iki çağrının kesişimi, sahip denetimi 61466e504); servis R1–R4'ü uygular ve sonucu `CandidatePrincipalIds` olarak gönderir; ayrı dizin istemcisi taşınmaz (korundu). Muafiyet adayları aynı sorgunun `waive-sod` + parti biçimi. MOD-0119 / 0120 / 0124 / MOD-0217 için aynı karar. **Kalan:** MOD-0018 sahibinin teyidi (D66/1) ve o paketin K-A1…K-A4'ü (uç / kimlik, şirket kapsamı, etkin kullanıcı, bağımlı iş akışı anahtarı) — DL-2 önkoşulu ve kabul kapısı Ready-for-dev Checklist'te | sahip + MOD-0023 sahibi + MOD-0018 sahibi |
| O-11 | Para birimi küsurat hanesi kaynağı | MOD-0048 `LookupOptionDto.Metadata["minorUnits"]` eki / başka | öneri: MOD-0048-FU01 §21 `currency-minor-units` kümesi (TRM-17, D31/4) — MOD-0048'e eklemeli alan (D9); **Y-4/3 (2026-10-27, Platform): başlangıç listesi olarak kabul** (Finance listeyi sonradan değiştirebilir); seed JSON ve `ConsumableSets` izin listesi Platform'da — seed main'e girene kadar önkoşul açık | MOD-0048 sahibi |
| O-12 | Geçmiş değer tarihine izin | — | **KARAR D28 (2026-10-25): öneri kabul** — Create/Submit'te bugünden (UTC) önce ret | sahip |
| O-13 | `PaymentMethod` değer listesi | serbest metin / referans listesi | **KARAR D28 (2026-10-25): öneri kabul** — MOD-0048 referans listesi; küme önerisi Y-4/3 ile başlangıç listesi olarak kabul (seed önkoşul açık) | sahip |
| O-14 | Alacaklı referansının varlık doğrulaması | v1 biçim / sözleşmeli doğrulama | **KARAR D28 (2026-10-25): öneri kabul** — v1 biçim; follow-up | sahip |
| O-15 | MOD-0289 gelene kadar banka referansı giriş biçimi | opak referans alanı / DL-4'ü beklet | **KARAR D28 (2026-10-25): öneri kabul** — opak referans alanı | sahip |
| O-16 | Kanıt gereksinimi değerlendirmesi (`evaluate`) | bu dilim / sonra | **KARAR D28 (2026-10-25): öneri kabul** — sonra | sahip |
| O-17 | Muafiyetin kapsamı (bkz. Tasarım notları) | yalnız R4 / R3+R4 / kaldır | **KARAR (D12/1): yalnız R4** (tier-2 izni eksikliği); R1–R3 ve MOD-0023 kontrolleri muaf tutulamaz | sahip (K1) |
| O-18 | Idempotency-Key zorunluluğu | her yazma komutu / yalnız kritik | **KARAR (D12/2): her yazma komutunda zorunlu**; makbuz kapsamı kiracı + şirket + aktör + işlem + hedef. Makbuz tipi servis genelinde tek: **KARAR (D14/2)** `FinanceTreasuryCommandReceipt` | sahip (K1) |
| O-19 | SEC-002 veri kapsamı çözücüsünün FinanceTreasury'de tüketim yolu (kapsamın kendisi **KARAR (D14/1)**: `LegalEntityId`, `read-tenant-wide` ayrı izin) | Platform kapsam ucu / servis içi çözücü kaydı | **Platform sahibi yanıtı Y-4/6 (2026-10-27):** FinanceTreasuryService kendi Application katmanında aynı desende bir port tanımlar (`IFinanceTreasuryDataScopeSource`; girdi kiracı + kullanıcı + modül + izin anahtarı, çıktı izinli `LegalEntityId` listesi); bağdaştırıcısı Platform'un kapsam çözümünü (`Diten.Platform.Common` içindeki `IDataScopeResolver`) çağırır; hata, boş liste ya da belirsizlikte erişim verilmez (fail-closed); MDM'nin `ITrustedLegalEntityScopeProvider`'ı (MDM'in kendi Application portu, ortak paket değil) doğrudan kullanılmaz; `read-tenant-wide` yalnız Auth'ta açık atamalı izin olarak tanımlanır (MOD-0018 Treasury Auth eki DL-1 — Treasury yazar, MOD-0018 sahibi teyit eder, Y-15, Y-16; Platform kapsam ucunun FinanceTreasury'yi kabul etmesi o ekin K-6 açık kararı); servis ortak tek karar (MOD-0119, MOD-0120 O-14, MOD-0124 ile aynı); `NoOp` kaydı yasak (kapsamı kapatır) | sahip + platform-shared-services |

## Teslim dilimleri

| dilim | ad | kapsadığı bölümler |
|---|---|---|
| DL-1 | servis iskeleti, parti çekirdeği ve denetim altyapısı | Module Summary, Ownership and Boundaries, Owned Objects, Entity Fields, Repo Scope, Protected Paths, Dependencies, Backend File Convention, Acceptance Criteria, Test Expectations, Ready-for-dev Checklist, Implementation Notes, Follow-up Items, Açık kararlar |
| DL-2 | iş akışı ve onay ayrılığı (SoD) | Runtime Constraints, Validation Rules, Failure Path to Verify, Authorization Convention |
| DL-3 | kanıt, onay belgesi, serbest bırakma (dağıtım kapalı) ve denetim kanıtı | Audited Events |
| DL-4 | kiracı ekranı | Layout & Shell Contract, Frontend File Contract, Page Help (UXH-001), Gateway / API Routing Decision |

Her bölüm tabloda bir kez, **kapandığı** dilimde yazılıdır; önceki dilimler o bölümün kendilerine düşen kısmını uygular
(ör. Audited Events tablosundaki denetim kaydı DL-1'in ilk komutuyla gelir, bölüm DL-3'te K2 sabotaj testi ve AUD-001 §8
dev kanıtıyla kapanır; Validation Rules'un alan kuralları DL-1'de, SoD kuralları DL-2'de, R11 DL-3'te uygulanır). Ortak
bölümler (Acceptance Criteria, Test Expectations, Ready-for-dev Checklist, Follow-up Items) her dilimde aşağıdaki AC
kimlikleriyle ölçülür.

Dilim ayrıntıları (yollar · önkoşul · bitiş):

- **DL-1** — Yollar: servis iskeleti, `Features/PaymentBatch/` içinde Create/Update/Delete/BulkDelete/Validate + dört
  sorgu, Domain/Persistence, MOD-0220 ve MOD-0048 bağdaştırıcıları (MOD-0048: para birimi `ICurrencyReferencePort` ve
  ödeme yöntemi `IPaymentMethodReferencePort` — ikincisi MOD-0126'ya özgü, bağdaştırıcıları `Infrastructure/Currency/`),
  RCS kod portu `IRecordCodeAllocator` + v0 bağdaştırıcısı (KARAR D59/1), CentralAudit kaydı, denetim defteri (D35),
  servis ortak yapı taşları (`FinanceTreasuryCommandReceipt` + iki makbuz indeksi, `IFinanceTreasuryDataScopeSource` okuma
  ve yazma kapsamıyla, ortak LE / para birimi / kod portları ve bağdaştırıcıları, CentralAudit kaydı — D14/1, D14/2; tek
  sahibi bu dilim, MOD-0119/0120/0124 DL-1'inin önkoşulu — gap 4/G5), `payment-batch.openapi.yaml`. **Neden denetim DL-1'de:** AUD-001 K4 — ileticisi olmayan serviste yeni yazma komutu
  yazılamaz; ilk komutla birlikte gelir. Önkoşul: yalnız paket kapısı — Ready-for-dev Checklist'in işaretsiz ENGELLER maddeleri
  (CT-APR-02/01, DCP, Auth paket metni (MOD-0018, D53/1; kayıt kodu DL-1 kabulünde, D53/2), TRM-14, O-11, O-13 kümesi, O-19 tasarımı + Auth'ta `read-tenant-wide` tanımı, Platform `AuditCategory`
  `Finance` main'de — D35/3; port D30 ile, CT defter kararı D35 ile, kod alma D27/5 ile verildi); "— DL-n için" işaretli maddeler
  DL-1'i bekletmez (D51/1). RCS-001 / BL-565 servisi önkoşul değildir — DL-1 kabul kapısıdır (KARAR D59/1): kod
  `IRecordCodeAllocator`'a karşı sahte uygulamayla yazılır. **WP dalı ve iskelet (D51/3):** DL-1 WP'si kendi dalında çalışır — `feature/fin/wp-0126-dl1`
  (künyedeki `branch` paket dalıdır; her WP kendi dalını açar); `/add-module` ile `Diten.FinanceTreasuryService` servis
  iskeleti DL-1 WP'sinin ilk adımıdır (ayrı ön WP yok). Bitiş: AC-1, 2, 3, 39 (Create kısmı), 41 (a–c, e; Create / Update), 16 (Create kısmı), 17, 18, 20, 21, 22, 23, 29, 32 (DL-1 uçları kısmı: liste, detay, Create / Update / Delete / Validate — SEC-002 kapsamı soran kod + kanıt testi DL-1 kabul kapısı, D51/2), 33 (Create / Update / Delete /
  Validate kısmı), 45. **Kabul kapısı (KARAR D59/1):** gerçek RCS-001 servisi (BL-565) main'de ve dev ortamında gerçek
  servisle `PAY` kodu alındı (kapanış tablosu RCS-001 satırı).
- **DL-2** — Yollar: Submit, Reconcile, Approve/Reject/RequestEvidence/ApproveWithSodWaiver, SoD politika
  servisi ve tek eşik yapılandırması — `PaymentSodSettings` varlığı, deposu ve `ux_payment_sod_settings_tenant` indeksi,
  `UpdatePaymentSodSettingsCommand` / `GetPaymentSodSettingsQuery` (KARAR D56/4), Auth muafiyet ataması portu ve
  bağdaştırıcısı (`IPaymentSodWaiverAssignmentSource`, `Infrastructure/SodWaivers/**` — KARAR D56/3), servis ortak onaylayıcı
  aday kaynağı (`IFinanceTreasuryApproverCandidateSource`, `Application/Common/ApproverCandidates/**`, `Infrastructure/ApproverCandidates/**`
  — KARAR D66/1; tek yazarı bu dilim, MOD-0119 DL-2 / MOD-0120 DL-3 / MOD-0124 DL-3 tüketir), MOD-0023 bağdaştırıcısı
  (zaman aşımlı; servis ortak yapı taşı, tek yazarı bu dilim; MOD-0119 DL-2, MOD-0120 DL-3, MOD-0124 DL-3 bunu bekler — KARAR D57/5; Repo Scope), `payment-batch.openapi.yaml`'a DL-2 uçları (eklemeli; sürüm 1.1.0). **Neden ayrı:** SoD ve belirsiz sonuç
  modülün çekirdeği ve en riskli kısmıdır; bağımsız inceleme tek başına yapılabilsin. Önkoşul (dilim kapısı — Ready-for-dev
  Checklist'te "— DL-2 için" maddeleri, D51/1): DL-1, O-4, O-7, O-8 (kod `FT-PAYMENT-BATCH-APPROVAL` geldi — Y-12), O-10 (aday tabanı — **KARAR D66/1**, kapandı), MOD-0023
  şablonunu kuran kodun main'de olması, MOD-0018 Treasury Auth ekinin DL-5 paket metninde muafiyet atama alanları (D56/3,
  **D64**; kodu DL-2 kabulünü beklemez — muafiyet yolu 403 ile kapalıyken DL-2 kabul edilir, D54 / **KARAR D66/3**) ve DL-6 paket
  metninde aday sorgusu (**KARAR D66/1**; MOD-0018 sahibinin teyidi; kodu DL-2 kabul kapısı), SoD ayarı izin
  anahtarlarının Auth paket metninde (o ekin DL-1'i) olması (D56/4; adlar KARAR D57/4), MOD-0023 pack §17 (TRM-15) rev 2 onayı (`803fbaa5…`, PR #235) ve §17 rev 2'deki silmeme politikası (D21/1) ile okuma eşleme alanları (D21/3) — kod MOD-0023 sahibinin WP'siyle gelir (D25/1), sahibi yazar, başka biri merge eder (D32/1); AC-W1…W17 geçişi dilimin **kabul kapısıdır, önkoşulu değil** (gap 16/P2) (KARAR D21/1–4, D22/2–3; gap 5/G2, gap 6/P1-a, P1-b). Bitiş: AC-40, AC-4…15 (AC-9 `Waived` dalı hariç — muafiyet açılışında), AC-42 (kapalı muafiyet dalı), AC-43 ((f), (g), (h) — kapalı yol; KARAR D66/3; (i) — R4 kapalı partide muafiyet, KARAR D77/1; (j) — R4 açık, tier-2 olmayan partide muafiyet, KARAR D78), AC-44 ((a)–(e) arka uç kısmı — D57/4), AC-46 (D62/2, D66/2), 41 (d: Approve kısmı), 39 (Reconcile kısmı), 16 (kararlar), 31, 32 (karar uçları kısmı), 33 (Submit, kararlar, Reconcile kısmı), 34, 35, 36. Validation Rules bu dilimde **R11 hariç** kapanır; R11 (Release) DL-3'te uygulanır ve DL-3 kabulünde kanıtlanır (AC-19).
  **Muafiyet açılış kabulü (KARAR D66/3; dilim değildir, tablo değişmez):** MOD-0018 Treasury Auth ekinin DL-5 kodu main'e
  girince ayrı kabul kaydı — AC-9 `Waived` dalı, AC-42 aday dalları, AC-43 (a)–(e) ve parti bağı sabotajı gerçek DL-5 ucuyla.
- **DL-3** — Yollar: LinkEvidence, Attestation (v2), Release + `NotConfiguredBankDispatchAdapter`, R11 doğrulaması, K2 sabotaj
  testi, dev ortamı denetim kanıtı. **Neden ayrı:** MOD-0269 sözleşmesine ve MOD-0031 davranış ölçümüne bağlı; DL-2'yi
  bekletmesin. Önkoşul (dilim kapısı — "— DL-3 için" maddeleri, D51/1): DL-2, MOD-0269 BANK-DISPATCH 0.1.0 dondu (D27/7, TRM-06), MOD-0031 dedupe ölçümü (yeniden çağrı için; ölçülmezse
  kurtarmada yeniden çağrı yok, gap 5/G2). Bitiş: AC-16 (kalan), 17 (sabotaj), 19, 30, 37, 38, 41 (d: Release kısmı — AC-41 tam kapanışı), 32 (Release ve kalan okumalar kısmı — tam kapanış), 33 (Release,
  kanıt, onay belgesi kısmı).
- **DL-4** — Yollar: `Views/FinanceTreasury/PaymentBatches/**`, SoD ayar ekranı `Views/FinanceTreasury/PaymentSodSettings/**`
  (KARAR D57/4), JS, resx (7 dil), MVC proxy, sayfa yardımı, nav anahtarları;
  Gateway rotası integration-agent task'ı olarak açılır (bu dilim yazmaz). **Neden ayrı:** ekran API sözleşmesine
  bağlıdır, arka uç değişmeden ilerler. Önkoşul: DL-1 (liste/oluşturma), DL-2 (karar düğmeleri, SoD ayarı uçları), Gateway
  rotası (SoD ayarı uç yolu dahil), O-15. Bitiş: AC-24…28, AC-44 ((f) ekran kısmı — AC-44 bu dilimde kapanır, D57/4).
