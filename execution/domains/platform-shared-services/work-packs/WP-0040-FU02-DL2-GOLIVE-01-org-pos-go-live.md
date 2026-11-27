# WP-0040-FU02-DL2-GOLIVE-01 — Kayıt kodu servisi DL-2 (ORG / POS): canlıya alma kapısının Ali'yi beklemeyen kısmı

| Alan | Değer |
|---|---|
| Durum | **draft** — ürün kodu yazmaz; CT / sahip adımları ve ölçüm. Bu WP push, PR, dağıtım ya da `.antigravity/**` değişikliği izni değildir |
| Sahip | Beste (platform-shared-services, K2). Uygulayıcı: MVP-1 CT (bildirim, sürüm notu, sayım) + Beste (tarayıcı ölçümü — giriş sahipte) |
| Tarih | 2026-11-27 · Sahip kararı: "MOD-0040-FU02 DL-2 (MDM kısa kod kilidi Ali'de — yapılabilen kısım)" (Beste, `docs/records/decisions/2026-11/ct-owner-decisions-2026-11-27.md` §3) |
| Modül | **MOD-0040-FU02** Record Code Service (ebeveyn MOD-0040; `Diten.Platform` 5057; RCS-001) |
| Kimlik ön kontrolü | `python -P -X utf8 .antigravity/scripts/verify_module_id.py . --check-id MOD-0040-FU02 --name "Record Code Service" --parent MOD-0040` → `OK … proven against Blueprint/registry`, çıkış 0 (2026-11-27) |
| Paket | `execution/domains/platform-shared-services/module-packs/MOD-0040-FU02-record-code-service.md` **r18**, LF sha256 `1dbd718093a2e098bc09a1db502b5ba71edd0103b27e5219505ffa34f5c12273`; `pack_approval.py check --ref origin/main` → **onaylı — HIGH · GMG-ERP · 2026-11-14, çıkış 0** (`approvals.tsv:111`). DL-2 kodu **r13** onayıyla yazıldı (`approvals.tsv:73-74`, sha256 `325e0624…`); DL-2 bölümleri r13 → r18 arasında değişmedi (r14–r18 yalnız DL-3 / DL-4 / DL-11 ekledi) |
| DL-2 kod durumu | **main'de** (PR #402, `c3737ae1c`; `origin/main`'de `IInProcessRecordCodeAllocator`, `CreateOrganizationUnitCommandHandler`, `CreatePositionCommandHandler` — ölçüldü 2026-11-27). CT kabulü **VERIFIED**: `docs/records/audits/2026-11/mod-0040-fu02-dl2-ct-acceptance-2026-11-12.md` (KOŞULLU) + `…-dl2-fix01-ct-acceptance-2026-11-12.md` (VERIFIED) |

## 0. Ayrım — Ali'ye bağlı olan ve olmayan

**Paket §18.1, "DL-2 canlıya alma kapısı" satırı (alıntı, kısaltılmadan):**

> **A-18 kararı** (MDM kalıcı kısa kod kullanım kilidi, sözleşme N-12) verilmiş ve kayda geçmiş olmalı — **r15: karar düzeyinde kapandı** (Beste 2026-11-13, sahip kararları 04 eki, `docs/records/decisions/2026-11/mod0040-fu02-owner-decisions-04.md:14-22`, `f827bf948`): bir şirkette ilk kayıt kodu üretildikten sonra MDM kısa kodu değiştirilemez; MDM kilidi (Ali'nin MDM iş paketi) uygulanana kadar ara dönem kilidi (Bölüm 8.13) ve Ali'ye verilen operasyon talimatı (kayıt kodu üretilmiş şirketin kısa kodu MDM ekranından değiştirilmez) birlikte korur; Ali itiraz ederse karar yeniden açılır. Açık kalan: aşağıdaki bildirim ve MDM işinin teslimi. Gerekçe: DL-2'den sonra organizasyon birimi açılmış her şirket ara dönem kilidine girer; MDM'de kısa kod değişikliği o şirketin **bütün** LegalEntity kayıt kodlarını (Treasury `PAY` / `APD` / `ARI` / `CPS` / `CTP`, MDM `BAC` / `AGR`, ORG / POS) 409 `LEGAL_ENTITY_SHORT_CODE_CHANGED`'e düşürür; serbest kalan kodu alan başka şirket de durur (Bölüm 8.14 "iki yön"). **Bildirim:** canlıya almadan önce CT, Treasury CT'ye ve MDM sahibine (Ali) iki yönü, "aynı önek" durumunu, TC-8 metnini, kurtarma yolunu (MDM kodunu defterdeki son öneke geri almak) ve A-18 kararını yazılı bildirir; bildirim kaydı DL-2 kabul kaydına bağlanır. Kod yazımını bekletmez (K-17); merge'ün ayrıca bekleyip beklemediği sahip kararında yazılı değildir — **CT kararı, açık** (main'e giren kodun canlıya çıkış biçimine bağlı)

| Parça | Kimde | Bugün (2026-11-27) |
|---|---|---|
| A-18 kararı (kalıcı kilit) | Beste + Ali | **kapalı** — karar 2026-11-13; Ali kararı (a) kalıcı kilit (`mvp5-wms-ct-owner-decisions-2026-11-13.md:193`) |
| Ali'ye operasyon talimatı ("kayıt kodu üretilmiş şirketin kısa kodu MDM ekranından değiştirilmez") | Ali | **kapalı** — Ali "evet" (`approvals.tsv:103` notu, MOD-0220-FU01 onay satırı, 2026-11-19) |
| Merge | CT kararı | **fiilen kapandı** — DL-2 #402 ile main'de; bu WP'de kayda geçirilir (B-5) |
| **MDM kalıcı kilidi kodu** (MOD-0220-FU01 DL-1 / DL-2, `UpdateLegalEntityHandler`, rezervasyon koleksiyonu, `LegalEntityShortCodeLock:Enabled`) | **Ali (MDM ekibi)** | **açık** — paket MOD-0220-FU01 onaylı (`approvals.tsv:103`, 2026-11-19), kod main'de ve hat dalında yok (`LEGAL_ENTITY_SHORT_CODE_*` MdmService'te 0 — ölçüldü) |
| Yazılı bildirim (Treasury CT + Ali) | CT | **açık — bu WP, B-1** |
| Sürüm notu SN-1…SN-7 (§8.16) | CT | **açık — bu WP, B-2** |
| AC-55 eski kod sayımı (kiracı başına, hedef ortam) | CT | dev'de 0 (2026-11-12); hedef ortamlar ölçülmedi — **bu WP, B-3** |
| AC-60 tarayıcı ölçümü | Beste (giriş) + CT | ölçülmedi — **bu WP, B-4** |

**Sonuç:** Ali'yi beklemeden yazılabilen kısım var — B-1…B-5. Ali'ye bağlı tek kalem MDM kalıcı kilidinin **kodu**dur; karar kaydı (04 eki) ara dönem kilidi + operasyon talimatının o koda kadar koruduğunu söyler, bu yüzden bu WP onu canlıya alma için ön koşul saymaz — bunun teyidi **S-1** (sahip sorusu, §5).

**Ali'den beklenen tek adım:** MOD-0220-FU01 DL-1'i (MDM kısa kod kalıcı kilidi, onaylı paket) MDM ekibiyle yazıp main'e almak — ya da kodu kimin yazacağını ve tarihini söylemek. Bu adım bu WP'yi bekletmez; geldiğinde MOD-0220-FU01'in kendi canlı sırası (anahtar kapalı dağıtım → geri doldurma → AK-3 uzlaştırma raporu sıfır → anahtar açık) uygulanır ve Platform tarafının payı (AK-3'te `record_code_allocations` salt-okunur çıkarımı; ara dönem kilidinin kalıp kalmayacağı — MOD-0220-FU01 F-1, öneri: kalsın) ayrı bir WP ile açılır.

## 1. Başlama kapıları

| # | Kapı | Durum |
|---|---|---|
| G-1 | DL-2 kodu main'de ve VERIFIED | **kapalı** (yukarıda) |
| G-2 | A-18 kararı ve operasyon talimatı | **kapalı** |
| G-3 | Hedef ortam listesi (DL-2'nin "canlıya alınacağı" ortamlar) | **açık (S-2)** — bugün ölçülebilen tek ortam dev; başka ortam (test / müşteri) var mı CT bilmiyor |
| G-4 | S-1 (MDM kilidi kodu canlıya alma ön koşulu mu) | **açık** — B-1…B-4 S-1'i beklemez; yalnız B-5'in sonucu ("canlıya alma kapısı kapandı") S-1'e bağlı |

**Sonuç:** başlama kapısı **açık** — B-1…B-4 bugün başlar; B-5 kapanış satırı S-1 ve S-2 cevabını bekler.

## 2. Kapsam

| Adım | İş | Çıktı (yer `docs-organization` beş sorusuyla) |
|---|---|---|
| B-1 | **Yazılı bildirim** Treasury CT'ye ve MDM sahibine (Ali), §8.14'ün dört maddesiyle: (1) yön 2 ve "aynı önek" durumu, (2) TC-8 metni ("şirket kısa kodu değişmiş, MDM yöneticisine başvurun"), (3) kurtarma yolu (MDM kısa kodunu defterdeki son öneke geri almak; "aynı önek"te önce Y'nin kodu başka değere, sonra X'in kodu eski değere; Platform'da "kilidi çöz" ucu yok), (4) A-18 kararı ve ara dönem kilidinin süreceği. Metin paketin §8.14 tablosundan alınır, yeni kural eklenmez | bildirim metni `docs/records/decisions/2026-11/mod0040-fu02-dl2-golive-notice-<tarih>.md` (kayıt; iletim kanalı ve tarih satırıyla). Gönderimi Beste yapar (sohbet / e-posta); kayıt gönderim tarihini taşır |
| B-2 | **Sürüm notu** SN-1…SN-7 (§8.16 tablosu aynen; kim etkilenir sütunu dahil) — kullanıcı ve tüketici ekip dilinde, 2 dil gerekmez (iç kayıt) | `docs/records/releases/<tarih>-mod-0040-fu02-dl2-release-note.md` (emsal `2026-11-16-mod-0286-release-note.md`; `docs/records/releases/README.md` kuralına göre) |
| B-3 | **AC-55 eski kod sayımı** — her hedef ortamda, kiracı başına, desene `^[A-Z]{2,5}-(ORG|POS)-[0-9]{6,}$` uyan eski (DL-2 öncesi, defterde ayrılmamış) ORG / POS kodu sayısı; bir (kiracı, önek, tür) için sayım **3'ü aşarsa** o kiracıda devreye almadan önce CT'ye bildirim (DL-6 tetiklenir) | salt-okunur sorgu + sonuç tablosu → kabul eki (B-5) |
| B-4 | **AC-60 tarayıcı ölçümü** — Gateway 5000 / Web 5001, kiracı kabuğu: ORG ve POS oluşturma formunda kod salt okunur ve boş; düzenleme formunda kod salt okunur, saklı değer görünür; 409 `LEGAL_ENTITY_SHORT_CODE_CHANGED` / `…_MISSING`, 503 `LEGAL_ENTITY_SOURCE_UNAVAILABLE`, 403 `RECORD_CODE_FORBIDDEN` cevaplarında yerelleştirilmiş metin (tr, en, ar), `errors[]` görünmez; 375 px | ekran görüntüleri / UIC seçici çıktısı → kabul eki (B-5) |
| B-5 | **Kabul eki** — DL-2 kabul kaydına (`mod-0040-fu02-dl2-ct-acceptance-2026-11-12.md`) bağlanan ek kayıt: B-1 bildirim kaydı bağlantısı, B-2 sürüm notu bağlantısı, B-3 sayım (ortam × kiracı), B-4 ölçüm, "merge #402 ile fiilen yapıldı" notu (§18.1 "merge için CT kararı açık" maddesinin kapanışı), S-1 / S-2 cevapları. Sonuç satırı: S-1 "evet, ön koşul değil" ise **canlıya alma kapısı kapandı**; "hayır" ise **MOD-0220-FU01 DL-1 bekler** | `docs/records/audits/<ay>/mod-0040-fu02-dl2-golive-ct-<tarih>.md` (yeni kayıt; eski kayıt değiştirilmez — ekleme) |

**Kapsam DIŞI:** her türlü ürün kodu (`services/**`, `frontend/**`); Platform ara dönem kilidinin (§8.13) kaldırılması ya da değiştirilmesi; MDM (`services/Diten.MdmService/**`, MOD-0220 / MOD-0220-FU01 — Ali, XMC-001, CT dokunmaz); sözleşme `docs/analysis/contracts/record-code-allocate.md` (1.0.0 donmuş; N-12 satırının "karar verildi" güncellemesi bir **sonraki sözleşme sürümünün** işidir — FU, bu WP değil); paket dosyası (değişirse onay düşer); DL-5 / DL-6 / DL-10; veri düzeltmesi (sayımda sorun çıkarsa yalnız bildirilir).

## 3. Dokunulacak dosyalar

Yalnız yeni kayıt dosyaları (B-1 karar / bildirim kaydı, B-2 sürüm notu, B-5 kabul eki). Var olan kayıtlar değişmez (ekleme). CT kuyruğuna satır gerekiyorsa `execution/domains/platform-shared-services/work-packs/CT-QUEUE.tsv`'ye **yalnız ekleme**.

## 4. Dokunulmayacaklar ve XMC-001

Korunan yollar: `.antigravity/**`, `gateway/**`, `services/**`, `frontend/**`, `docs/analysis/contracts/**`, paket dosyaları, `execution/approvals/**`.

| # | Konu | Sahip | Durum |
|---|---|---|---|
| X-1 | MDM kalıcı kilidi kodu (MOD-0220-FU01) | Ali (MDM) | bu WP dokunmaz; yalnız bildirim |
| X-2 | Dev / hedef ortamda veri sorgusu (B-3) | ortam sahibi | **salt okuma**; yazma, indeks, sayaç değişikliği yok. 27017'deki paylaşılan dev Mongo'ya yalnız okuma sorgusu; test koşusu yok |

## 5. Açık sorular

| # | Soru | Seçenekler / öneri | Sahip |
|---|---|---|---|
| S-1 | MDM kalıcı kilidinin **kodu** (MOD-0220-FU01) DL-2 canlıya alma için ön koşul mu? | Öneri: **hayır** — 04 eki "MDM kilidi uygulanana kadar ara dönem kilidi ve operasyon talimatı birlikte korur" der; §18.1'deki "MDM işinin teslimi açık" ifadesi takip maddesi okunur. "Evet" derseniz B-5 sonucu "MOD-0220-FU01 DL-1 bekler" olur | Beste |
| S-2 | "Canlıya alma" hangi ortam(lar)? Bugün yalnız dev ölçülebiliyor | Öneri: dev + (varsa) müşteri / tur ortamı adıyla; ortam yoksa B-3 dev sonucuyla kapanır ve "başka ortam yok" yazılır | Beste |
| S-3 | Bildirimi kim iletir? | Öneri: metni CT yazar, Beste Treasury CT'ye ve Ali'ye iletir; kayıt iletim tarihini taşır | Beste |

## 6. Kabul ölçütleri (bu WP)

1. B-1 kaydı dört maddeyi paketteki biçimiyle taşır; iletim tarihi ve alıcılar (Treasury CT, Ali) yazılı.
2. B-2 sürüm notu SN-1…SN-7'nin yedisini de içerir; her satırda "kimi etkiler" ve paket bölümü.
3. B-3: her hedef ortam ve kiracı için sayı; sorgu metni kayıtta; 3'ü aşan (kiracı, önek, tür) varsa adıyla ve CT bildirimi.
4. B-4: AC-60'ın beş maddesi ekran kanıtıyla; tr / en / ar; 375 px.
5. B-5: S-1, S-2 cevapları ve sonuç satırı; DL-2 kabul kaydına bağlantı.
Sabotaj yoktur (ürün kodu yok; AC-55 / AC-60 bekçileri DL-2 kabulünde sabotajla kanıtlandı — FIX-01 kaydı B1…B4).

## 7. Test / ölçüm notları

- Test koşusu yok (ürün kodu değişmiyor). DL-2 bekçilerinin bugünkü durumunu görmek gerekirse Platform takımı **ayrık worktree'de**, `DITEN_TEST_MONGOD` = mongod 9.0, **27017 yok**, `Diten.AuthService.AccountKindAcceptanceHost.Tests` yok; bu makinede aynı anda tek Platform koşusu (makine geneli Mongo kilidi); süre ~45 dk.
- B-3 sorgusu salt okumadır; sonuç kiracı kimliği + önek + tür + sayı biçiminde, kişisel veri yok.
- B-4 tarayıcı turu Beste'nin oturumuyla; fleet ana checkout'tan koşuyor — CT ana checkout'ta derleme yapmaz.

## 8. Git kuralları

Yalnız yeni kayıt dosyaları; açık yolla stage; `main`'e commit / push yok; push / PR yalnız sahip onayıyla; CT kendi dalında yerel commit (GIT-002 §4–§5).

## 9. Teslim raporu

B-1…B-5 dosya yolları; S-1 / S-2 / S-3 cevapları; B-3 sayım tablosu; B-4 kanıtı; sonuç satırı (canlıya alma kapısı kapandı / MOD-0220-FU01 DL-1 bekler); Ali'ye iletilen tek adım ve tarih.

## 10. Kural haritası (AGENTS.md §6.1)

`docs-organization` (kayıt yerleri), `status-reporting-and-evidence` (REP-001), `record-coding-standard` (RCS-001 §5.4, §5.8), `cross-module-change-consent` (XMC-001 — MDM'e dokunulmaz), `localization-standard` (B-4), `git-safety`. Dosyalar `.antigravity/rules/<ad>.md`; okumak serbest, yazmak yasak.
